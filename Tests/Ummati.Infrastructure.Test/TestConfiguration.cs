namespace Ummati.Infrastructure.Test;

using System.Collections.Generic;

public class TestConfiguration : IConfiguration
{
    public string ApplicationName { get; init; } = default!;

    public string CommonLocation { get; init; } = default!;

    public IEnumerable<string> Locations { get; init; } = default!;

    public string Environment { get; init; } = default!;

    public string ContainerImageName { get; init; } = default!;
}
