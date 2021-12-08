namespace Ummati.Infrastructure;

using Pulumi;

[OutputType]
public class ContainerAppOutput
{
    [OutputConstructor]
#pragma warning disable CA1054 // URI-like parameters should not be strings. Pulumi requires this.
    public ContainerAppOutput(string location, string fqdn, string url)
#pragma warning restore CA1054 // URI-like parameters should not be strings. Pulumi requires this.
    {
        this.Location = location;
        this.Fqdn = fqdn;
        this.Url = url;
    }

    public string Location { get; }

    public string Fqdn { get; }

#pragma warning disable CA1056 // URI-like properties should not be strings. Pulumi requires this.
    public string Url { get; }
#pragma warning restore CA1056 // URI-like properties should not be strings. Pulumi requires this.
}
