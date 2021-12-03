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
        var resources = await Testing.RunAsync<AzureContainerAppStack>().ConfigureAwait(false);

        foreach (var resource in resources)
        {
            var tagsOutput = GetTags(resource);
            if (tagsOutput is not null)
            {
                var tags = await OutputUtilities.GetValueAsync(tagsOutput).ConfigureAwait(false);
                Assert.NotNull(tags);
                Assert.Equal("d", tags![TagName.Application]);
                Assert.Equal("d", tags![TagName.Environment]);
            }
        }
    }

    private static Output<ImmutableDictionary<string, string>?>? GetTags(Resource resource)
    {
        var tagsProperty = resource.GetType().GetProperty("Tags");
        if (tagsProperty?.PropertyType == typeof(Output<ImmutableDictionary<string, string>?>))
        {
            return (Output<ImmutableDictionary<string, string>?>?)tagsProperty.GetValue(null);
        }

        return null;
    }
}
