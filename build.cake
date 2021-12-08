var target = Argument("Target", "Default");
var configuration =
    HasArgument("Configuration") ? Argument<string>("Configuration") :
    EnvironmentVariable("Configuration", "Release");
var stack =
    HasArgument("Stack") ? Argument<string>("Stack") :
    EnvironmentVariable("Stack", "development");
var servicePrincipalName =
    HasArgument("ServicePrincipalName") ? Argument<string>("ServicePrincipalName") :
    EnvironmentVariable("ServicePrincipalName");

var artefactsDirectory = Directory("./Artefacts");

Task("Clean")
    .Description("Cleans the artefacts, bin and obj directories.")
    .Does(() =>
    {
        CleanDirectory(artefactsDirectory);
        DeleteDirectories(GetDirectories("**/bin"), new DeleteDirectorySettings() { Force = true, Recursive = true });
        DeleteDirectories(GetDirectories("**/obj"), new DeleteDirectorySettings() { Force = true, Recursive = true });
    });

Task("Restore")
    .Description("Restores NuGet packages.")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetRestore();
    });

Task("Build")
    .Description("Builds the solution.")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetBuild(
            ".",
            new DotNetBuildSettings()
            {
                Configuration = configuration,
                NoRestore = true,
            });
    });

Task("Test")
    .Description("Runs unit tests and outputs test results to the artefacts directory.")
    .DoesForEach(GetFiles("./Tests/**/*.csproj"), project =>
    {
        DotNetTest(
            project.ToString(),
            new DotNetTestSettings()
            {
                Blame = true,
                Collectors = new string[] { "Code Coverage", "XPlat Code Coverage" },
                Configuration = configuration,
                Loggers = new string[]
                {
                    $"trx;LogFileName={project.GetFilenameWithoutExtension()}.trx",
                    $"html;LogFileName={project.GetFilenameWithoutExtension()}.html",
                },
                NoBuild = true,
                NoRestore = true,
                ResultsDirectory = artefactsDirectory,
            });
    });

Task("UpdateServicePrincipal")
    .Description("")
    .Does(() =>
    {
        var subscriptionId = GetSubscriptionId();
        var (clientId, clientSecret, tenantId) = CreateServicePrincipal(servicePrincipalName);

        Information($"ClientId: {clientId}");
        Information($"ClientSecret: {clientSecret}");
        Information($"TenantId: {tenantId}");
        Information($"SubscriptionId: {subscriptionId}");

        SetPulumiConfig("azure-native:clientId", clientId);
        SetPulumiConfig("azure-native:clientSecret", clientSecret, secret: true);
        SetPulumiConfig("azure-native:tenantId", tenantId);
        SetPulumiConfig("azure-native:subscriptionId", subscriptionId);
    });

Task("Default")
    .Description("Cleans, restores NuGet packages, builds the solution and then runs unit tests.")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

RunTarget(target);

(string clientId, string clientSecret, string tenantId) CreateServicePrincipal(string name)
{
    StartProcess(
        "powershell",
        new ProcessSettings()
            .WithArguments(x => x
                .Append("az")
                .Append("ad")
                .Append("sp")
                .Append("create-for-rbac")
                .AppendSwitchQuoted("--name", name)
                .AppendSwitchQuoted("--role", "Contributor"))
            .SetRedirectStandardOutput(true),
            out var lines);

    var document = System.Text.Json.JsonDocument.Parse(string.Join(string.Empty, lines)).RootElement;
    var clientId = document.GetProperty("appId").GetString();
    var clientSecret = document.GetProperty("password").GetString();
    var tenantId = document.GetProperty("tenant").GetString();
    return (clientId, clientSecret, tenantId);
}

string GetSubscriptionId()
{
    StartProcess(
        "powershell",
        new ProcessSettings()
            .WithArguments(x => x
                .Append("az")
                .Append("account")
                .Append("show")
                .AppendSwitch("--query", "id")
                .AppendSwitch("--output", "tsv"))
            .SetRedirectStandardOutput(true),
            out var lines);
    return lines.First();
}

void SetPulumiConfig(string key, string value, bool secret = false)
{
    StartProcess(
        "pulumi",
        new ProcessSettings()
            .UseWorkingDirectory(GetFiles("**/Pulumi.yaml").Single().GetDirectory())
            .WithArguments(builder =>
            {
                builder
                    .Append("config")
                    .Append("set")
                    .Append(key)
                    .AppendQuoted(value)
                    .AppendSwitchQuoted("--stack", stack);
                if (secret)
                {
                    builder.Append("--secret");
                }
            }));
}
