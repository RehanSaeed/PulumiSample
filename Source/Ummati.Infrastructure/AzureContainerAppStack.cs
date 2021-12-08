namespace Ummati.Infrastructure;

using System.Collections.Immutable;
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

        var containerAppOutputs = new List<Output<ContainerAppOutput>>();
        foreach (var location in Configuration.Locations)
        {
            var resourceGroup = GetResourceGroup("app", location);
            var kubeEnvironment = GetKubeEnvironment(location, resourceGroup, workspace, workspacePrimarySharedKey);
            var containerApp = GetContainerApp(location, resourceGroup, kubeEnvironment);

            var containerAppOutput = containerApp.Configuration
                .Apply(x => x!.Ingress)
                .Apply(x => new ContainerAppOutput(location, x!.Fqdn, $"https://{x!.Fqdn}"));
            containerAppOutputs.Add(containerAppOutput);
        }

        var containerApps = Output.All(containerAppOutputs);

        var trafficManager = GetTrafficManager(commonResourceGroup, containerApps);
    }

    public static IConfiguration Configuration { get; set; } = default!;

    // [Output]
    // public Output<ImmutableArray<ContainerAppOutput>> ContainerApps { get; private set; }

    // [Output]
    // public Output<string> TrafficManagerFqdn { get; private set; }

    private static Dictionary<string, string> GetTags() =>
        new()
        {
            { TagName.Application, Configuration.ApplicationName },
            { TagName.Environment, Configuration.Environment },
        };

    private static ResourceGroup GetResourceGroup(string name, string location) =>
        new(
            $"{Configuration.ApplicationName}-{name}-{location}-{Configuration.Environment}-",
            new ResourceGroupArgs()
            {
                Location = location,
                Tags = GetTags(),
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
                Tags = GetTags(),
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
                Tags = GetTags(),
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
                                Cpu = 0.25,
                                Memory = "0.5Gi",
                            },
                        },
                    },
                    Scale = new ScaleArgs()
                    {
                        MinReplicas = 1,
                        MaxReplicas = 10,
                        Rules = new List<ScaleRuleArgs>()
                        {
                            new ScaleRuleArgs()
                            {
                                Name = "http-scale-rule",
                                Http = new HttpScaleRuleArgs()
                                {
                                    Metadata = new Dictionary<string, string>()
                                    {
                                        { "concurrentRequests", "30" },
                                    },
                                },
                            },
                        },
                    },
                },
                Tags = GetTags(),
            },
            new CustomResourceOptions()
            {
                CustomTimeouts = new CustomTimeouts()
                {
                    Create = TimeSpan.FromHours(1),
                    Update = TimeSpan.FromHours(1),
                    Delete = TimeSpan.FromHours(1),
                },
            });

    private static Profile GetTrafficManager(
        ResourceGroup commonResourceGroup,
        Output<ImmutableArray<ContainerAppOutput>> containerAppOutput)
    {
        var name = string.Equals(Configuration.Environment, EnvironmentName.Production, StringComparison.Ordinal) ?
            "ummati-" :
            $"ummati-{Configuration.Environment}-";

        var endpoints = containerAppOutput.Apply(
            containerApps => containerApps.Select(containerApp => new EndpointArgs()
            {
                Name = $"endpoint-{containerApp.Location}",
                EndpointStatus = EndpointStatus.Enabled,
                Target = containerApp.Fqdn,
                Type = "Microsoft.Network/trafficManagerProfiles/externalEndpoints",
                EndpointLocation = containerApp.Location,
            }));

        return new(
            name,
            new ProfileArgs()
            {
                DnsConfig = new DnsConfigArgs()
                {
                    RelativeName = "ummati-development",
                    Ttl = 60,
                },
                Endpoints = endpoints,
                Location = "global",
                MaxReturn = 0,
                MonitorConfig = new MonitorConfigArgs()
                {
                    Path = "/",
                    Port = 443,
                    Protocol = MonitorProtocol.HTTPS,
                },
                ProfileStatus = ProfileStatus.Enabled,
                ResourceGroupName = commonResourceGroup.Name,
                TrafficRoutingMethod = TrafficRoutingMethod.Performance,
                TrafficViewEnrollmentStatus = "Disabled",
                Tags = GetTags(),
            });
    }
}
