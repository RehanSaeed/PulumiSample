namespace Ummati.Infrastructure;

using System.Text.Json;
using Pulumi;

#pragma warning disable CA1724 // Conflicts with System.Configuration
public static class Configuration
#pragma warning restore CA1724 // Conflicts with System.Configuration
{
    private static readonly Config Config = new();

    public static string ApplicationName => Config.Require(nameof(ApplicationName));

    public static string CommonLocation => Config.Require(nameof(CommonLocation));

    public static IEnumerable<string> Locations =>
        Config
            .RequireObject<JsonElement>(nameof(Locations))
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .Where(x => x is not null);

    public static string Environment => Config.Require(nameof(Environment));

    public static string ContainerImageName => Config.Require(nameof(ContainerImageName));
}
