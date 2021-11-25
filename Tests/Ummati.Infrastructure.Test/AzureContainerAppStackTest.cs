namespace Ummati.Infrastructure.Test;

using System.Collections.Immutable;
using Pulumi;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.Utilities;
using Xunit;

public class AzureContainerAppStackTest
{
    [Fact]
    public async Task AllResourcesHaveTagsAsync()
    {
        var resources = await Testing.RunAsync<AzureContainerAppStack>().ConfigureAwait(false);

        foreach (var resource in resources)
        {
            var tagsOutput = GetTags(resource);
            var tags = await OutputUtilities.GetValueAsync(tagsOutput).ConfigureAwait(false);
            Assert.NotNull(tags);
            Assert.Equal("d", tags![TagName.Application]);
            Assert.Equal("d", tags![TagName.Environment]);
        }
    }

    private static Output<ImmutableDictionary<string, string>?> GetTags(Pulumi.Resource resource) =>
        resource switch
        {
            ResourceGroup resourceGroup => resourceGroup.Tags,
            Workspace workspace => workspace.Tags,
            KubeEnvironment kubeEnvironment => kubeEnvironment.Tags,
            ContainerApp containerApp => containerApp.Tags,
            _ => throw new ArgumentException(
                $"Resource with type '{resource.GetType().Name}' not recognized",
                nameof(resource)),
        };
}
