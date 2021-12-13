namespace Ummati.Infrastructure.Test;

using System.Collections.Generic;

public class TestConfiguration : IConfiguration
{
    public string ApplicationName { get; init; } = default!;

    public string CommonLocation { get; init; } = default!;

    public IEnumerable<string> Locations { get; init; } = default!;

    public string Environment { get; init; } = default!;

    public string ContainerImageName { get; init; } = default!;

    public double ContainerCpu { get; init; } = default!;

    public string ContainerMemory { get; init; } = default!;

    public int ContainerMaxReplicas { get; init; } = default!;

    public int ContainerMinReplicas { get; init; } = default!;

    public int ContainerConcurrentRequests { get; init; } = default!;
}
