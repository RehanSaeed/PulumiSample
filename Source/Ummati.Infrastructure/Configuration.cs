namespace Ummati.Infrastructure;

using System.Globalization;
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

    public double ContainerCpu => double.Parse(this.config.Require(nameof(this.ContainerCpu)), CultureInfo.InvariantCulture);

    public string ContainerMemory => this.config.Require(nameof(this.ContainerMemory));

    public int ContainerMaxReplicas => int.Parse(this.config.Require(nameof(this.ContainerMaxReplicas)), CultureInfo.InvariantCulture);

    public int ContainerMinReplicas => int.Parse(this.config.Require(nameof(this.ContainerMinReplicas)), CultureInfo.InvariantCulture);

    public int ContainerConcurrentRequests => int.Parse(this.config.Require(nameof(this.ContainerConcurrentRequests)), CultureInfo.InvariantCulture);
}
