namespace Ummati.Infrastructure;

using Pulumi;

public static class Program
{
    public static Task<int> Main() => Deployment.RunAsync<AzureContainerAppStack>();
}
