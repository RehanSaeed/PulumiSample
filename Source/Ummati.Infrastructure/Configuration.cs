namespace Ummati.Infrastructure;

using System.Text.Json;
using Pulumi;

#pragma warning disable CA1724 // Conflicts with System.Configuration
public class Configuration : IConfiguration
#pragma warning restore CA1724 // Conflicts with System.Configuration
{
    private readonly Config config = new();

    public string ApplicationName => this.config.Require(nameof(this.ApplicationName));

    public string CommonLocation => this.config.Require(nameof(this.CommonLocation));

    public IEnumerable<string> Locations =>
        this.config
            .RequireObject<JsonElement>(nameof(this.Locations))
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .Where(x => x is not null);

    public string Environment => this.config.Require(nameof(this.Environment));

    public string ContainerImageName => this.config.Require(nameof(this.ContainerImageName));
}
