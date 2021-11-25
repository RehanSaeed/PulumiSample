namespace AzureContainerApps;

using Pulumi;

public class Program
{
    public static Task<int> Main() => Deployment.RunAsync<AzureContainerAppStack>();
}
