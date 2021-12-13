namespace Ummati.Infrastructure;

using System.Collections.Immutable;
using System.Globalization;
using Pulumi;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web.V20210301;
using Pulumi.AzureNative.Web.V20210301.Inputs;
using EndpointArgs = Pulumi.AzureNative.Network.Inputs.EndpointArgs;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class AzureContainerAppStack : Stack
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public AzureContainerAppStack()
    {
        if (Configuration is null)
        {
            Configuration = new Configuration();
        }

        var commonResourceGroup = GetResourceGroup("common", Configuration.CommonLocation);
        var workspace = GetWorkspace(Configuration.CommonLocation, commonResourceGroup);
        var workspacePrimarySharedKey = GetWorkspacePrimarySharedKey(commonResourceGroup, workspace);

        var containerAppOutputs = new List<Output<(string Location, string Fqdn, string Url)>>();
        foreach (var location in Configuration.Locations)
        {
            var resourceGroup = GetResourceGroup("app", location);
            var kubeEnvironment = GetKubeEnvironment(location, resourceGroup, workspace, workspacePrimarySharedKey);
            var containerApp = GetContainerApp(location, resourceGroup, kubeEnvironment);

            var containerAppOutput = containerApp.Configuration
                .Apply(x => x!.Ingress)
                .Apply(x => (location, x!.Fqdn, $"https://{x!.Fqdn}"));
            containerAppOutputs.Add(containerAppOutput);
        }

        this.ContainerApps = Output.All(containerAppOutputs.Select(x => x.Apply(y => y.Url)));

        var trafficManager = GetTrafficManager(Configuration.CommonLocation, commonResourceGroup, containerAppOutputs);
        this.TrafficManagerUrl = Output.Format($"https://{trafficManager.DnsConfig.Apply(x => x!.RelativeName)}");
    }

    public static IConfiguration Configuration { get; set; } = default!;

    [Output]
    public Output<ImmutableArray<string>> ContainerApps { get; private set; }

    [Output]
    public Output<string> TrafficManagerUrl { get; private set; }

    private static Dictionary<string, string> GetTags(string location) =>
        new()
        {
            { TagName.Application, Configuration.ApplicationName },
            { TagName.Environment, Configuration.Environment },
            { TagName.Location, location },
        };

    private static ResourceGroup GetResourceGroup(string name, string location) =>
        new(
            $"{Configuration.ApplicationName}-{name}-{location}-{Configuration.Environment}-",
            new ResourceGroupArgs()
            {
                Location = location,
                Tags = GetTags(location),
            });

    private static Workspace GetWorkspace(string location, ResourceGroup resourceGroup) =>
        new(
            $"log-analytics-{location}-{Configuration.Environment}-",
            new WorkspaceArgs()
            {
                Location = location,
                ResourceGroupName = resourceGroup.Name,
                RetentionInDays = 30,
                Sku = new WorkspaceSkuArgs()
                {
                    Name = WorkspaceSkuNameEnum.PerGB2018,
                },
                Tags = GetTags(location),
            });

    private static Output<string> GetWorkspacePrimarySharedKey(ResourceGroup resourceGroup, Workspace workspace)
    {
        var workspaceSharedKeys = GetSharedKeys.Invoke(
            new GetSharedKeysInvokeArgs()
            {
                ResourceGroupName = resourceGroup.Name,
                WorkspaceName = workspace.Name,
            });
        return workspaceSharedKeys.Apply(x => x.PrimarySharedKey!);
    }

    private static KubeEnvironment GetKubeEnvironment(
        string location,
        ResourceGroup resourceGroup,
        Workspace workspace,
        Output<string> workspacePrimarySharedKey) =>
        new(
            $"kube-environment-{location}-{Configuration.Environment}-",
            new KubeEnvironmentArgs
            {
                AppLogsConfiguration = new AppLogsConfigurationArgs()
                {
                    Destination = "log-analytics",
                    LogAnalyticsConfiguration = new LogAnalyticsConfigurationArgs()
                    {
                        CustomerId = workspace.CustomerId,
                        SharedKey = workspacePrimarySharedKey,
                    },
                },
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                Tags = GetTags(location),
                Type = "Managed",
            });

    private static ContainerApp GetContainerApp(
        string location,
        ResourceGroup resourceGroup,
        KubeEnvironment kubeEnvironment) =>
        new(
            $"app-{location}",
            new ContainerAppArgs
            {
                ResourceGroupName = resourceGroup.Name,
                KubeEnvironmentId = kubeEnvironment.Id,
                Location = resourceGroup.Location,
                Configuration = new ConfigurationArgs
                {
                    Ingress = new IngressArgs
                    {
                        AllowInsecure = true,
                        External = true,
                        TargetPort = 80,
                    },

                    // Registries = {
                    //     //new RegistryCredentialsArgs
                    //     //{
                    //     //    Server = registry.LoginServer,
                    //     //    Username = adminUsername,
                    //     //    PasswordSecretRef = "pwd"
                    //     //}
                    // },
                    // Secrets =
                    // {
                    //     new SecretArgs
                    //     {
                    //         Name = "pwd",
                    //         Value = adminPassword
                    //     }
                    // },
                },
                Template = new TemplateArgs
                {
                    Containers =
                    {
                        new ContainerArgs
                        {
                            Name = "aspnet-sample",
                            Image = Configuration.ContainerImageName,
                            Resources = new ContainerResourcesArgs()
                            {
                                Cpu = Configuration.ContainerCpu,
                                Memory = Configuration.ContainerMemory,
                            },
                            Env = new List<EnvironmentVarArgs>()
                            {
                                new EnvironmentVarArgs()
                                {
                                    Name = "AllowedHosts",
                                    Value = "*",
                                },
                            },
                        },
                    },
                    Scale = new ScaleArgs()
                    {
                        MinReplicas = Configuration.ContainerMinReplicas,
                        MaxReplicas = Configuration.ContainerMaxReplicas,
                        Rules = new List<ScaleRuleArgs>()
                        {
                            new ScaleRuleArgs()
                            {
                                Name = "http-scale-rule",
                                Http = new HttpScaleRuleArgs()
                                {
                                    Metadata = new Dictionary<string, string>()
                                    {
                                        { "concurrentRequests", Configuration.ContainerConcurrentRequests.ToString(CultureInfo.InvariantCulture) },
                                    },
                                },
                            },
                        },
                    },
                },
                Tags = GetTags(location),
            });

    private static Profile GetTrafficManager(
        string location,
        ResourceGroup commonResourceGroup,
        List<Output<(string Location, string Fqdn, string Url)>> containerAppOutputs)
    {
        var dnsName = string.Equals(Configuration.Environment, EnvironmentName.Production, StringComparison.Ordinal) ?
            "ummati" :
            $"ummati-{Configuration.Environment}";

        var endpoints = Output.All(containerAppOutputs
            .Select(containerAppOutput => containerAppOutput.Apply(containerApp =>
                new EndpointArgs()
                {
                    Name = $"endpoint-{containerApp.Location}",
                    EndpointStatus = EndpointStatus.Enabled,
                    Target = containerApp.Fqdn,
                    Type = "Microsoft.Network/trafficManagerProfiles/externalEndpoints",
                    EndpointLocation = containerApp.Location,
                    CustomHeaders = new List<EndpointPropertiesCustomHeadersArgs>()
                    {
                        new EndpointPropertiesCustomHeadersArgs()
                        {
                            Name = "Host",
                            Value = containerApp.Fqdn,
                        },
                    },
                })));

        return new(
            $"traffic-manager-{location}-{Configuration.Environment}-",
            new ProfileArgs()
            {
                DnsConfig = new DnsConfigArgs()
                {
                    RelativeName = dnsName,
                    Ttl = 1,
                },
                Endpoints = endpoints,
                Location = "global",
                MaxReturn = 0,
                MonitorConfig = new MonitorConfigArgs()
                {
                    Path = "/",
                    Port = 443,
                    Protocol = MonitorProtocol.HTTPS,
                    ExpectedStatusCodeRanges = new List<MonitorConfigExpectedStatusCodeRangesArgs>()
                    {
                        new MonitorConfigExpectedStatusCodeRangesArgs()
                        {
                            Min = 200,
                            Max = 299,
                        },
                    },

                    // CustomHeaders = new List<MonitorConfigCustomHeadersArgs>()
                    // {
                    //     new MonitorConfigCustomHeadersArgs()
                    //     {
                    //         Name = "Host",
                    //         Value = dnsName,
                    //     },
                    // },
                },
                ProfileStatus = ProfileStatus.Enabled,
                ResourceGroupName = commonResourceGroup.Name,
                TrafficRoutingMethod = TrafficRoutingMethod.Performance,
                TrafficViewEnrollmentStatus = "Disabled",
                Tags = GetTags(location),
            });
    }
}
