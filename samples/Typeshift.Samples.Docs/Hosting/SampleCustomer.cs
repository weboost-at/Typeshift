namespace Typeshift.Samples.Hosting;

// The customer of the guide's scenarios; a web host fills its own from the request
internal sealed class SampleCustomer : ICurrentCustomer
{
    public int Id => 7;

    public string Name => "Ada";

    public string Plan => "Business";
}
