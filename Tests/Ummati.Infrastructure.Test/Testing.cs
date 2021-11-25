namespace Ummati.Infrastructure.Test;

using System.Collections.Immutable;
using Moq;
using Pulumi;
using Pulumi.Testing;

#pragma warning disable CA1724 // Conflicts with Pulumi.Testing
public static class Testing
#pragma warning restore CA1724 // Conflicts with Pulumi.Testing
{
    public static Task<ImmutableArray<Resource>> RunAsync<T>()
        where T : Stack, new()
    {
        var mocks = new Mock<IMocks>();
        mocks
            .Setup(x => x.NewResourceAsync(It.IsAny<MockResourceArgs>()))
            .ReturnsAsync((MockResourceArgs args) => (args.Id ?? string.Empty, args.Inputs));
        mocks
            .Setup(x => x.CallAsync(It.IsAny<MockCallArgs>()))
            .ReturnsAsync((MockCallArgs args) => args.Args);
        return Deployment.TestAsync<T>(mocks.Object, new TestOptions { IsPreview = false });
    }
}
