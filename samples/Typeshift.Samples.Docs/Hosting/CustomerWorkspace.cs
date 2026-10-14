using Typeshift.Toolsets;

namespace Typeshift.Samples.Hosting;

// begin-snippet: file-system-root
internal sealed class CustomerWorkspace(ICurrentCustomer customer) : IFileSystemRoot
{
    public string Path => System.IO.Path.Combine("/srv/workspaces", customer.Id.ToString());
}
// end-snippet
