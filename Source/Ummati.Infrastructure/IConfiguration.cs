namespace Ummati.Infrastructure;

using System.Collections.Generic;

public interface IConfiguration
{
    string ApplicationName { get; }

    string CommonLocation { get; }

    IEnumerable<string> Locations { get; }

    string Environment { get; }

    string ContainerImageName { get; }

    double ContainerCpu { get; }

    string ContainerMemory { get; }

    int ContainerMaxReplicas { get; }

    int ContainerMinReplicas { get; }

    int ContainerConcurrentRequests { get; }
}
