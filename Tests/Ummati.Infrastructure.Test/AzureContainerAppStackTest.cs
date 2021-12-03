namespace Ummati.Infrastructure.Test;

using System.Collections.Immutable;
using Pulumi;
using Pulumi.Utilities;
using Xunit;

public class AzureContainerAppStackTest
{
    [Fact]
    public async Task AllResourcesHaveTagsAsync()
    {
        AzureContainerAppStack.Configuration = new TestConfiguration()
        {
            ApplicationName = "test-app",
            CommonLocation = "northeurope",
            ContainerImageName = "image-name",
            Environment = "test",
            Locations = ImmutableArray.Create("northeurope", "canadacentral"),
        };

        var resources = await Testing.RunAsync<AzureContainerAppStack>().ConfigureAwait(false);

        foreach (var resource in resources)
        {
            var tagsOutput = GetTags(resource);
            if (tagsOutput is not null)
            {
                var tags = await OutputUtilities.GetValueAsync(tagsOutput).ConfigureAwait(false);
                Assert.NotNull(tags);
                Assert.Equal("test-app", tags![TagName.Application]);
                Assert.Equal("test", tags![TagName.Environment]);
            }
        }
    }

    private static Output<ImmutableDictionary<string, string>?>? GetTags(Resource resource)
    {
        var tagsProperty = resource.GetType().GetProperty("Tags");
        if (tagsProperty?.PropertyType == typeof(Output<ImmutableDictionary<string, string>?>))
        {
            return (Output<ImmutableDictionary<string, string>?>?)tagsProperty.GetValue(resource);
        }

        return null;
    }
}
