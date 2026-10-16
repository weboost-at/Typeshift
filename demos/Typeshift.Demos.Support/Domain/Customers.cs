namespace Typeshift.Demos.Support.Domain;

// The customer of the current scope, as a web host fills it from the request; the demo fills it per customer
public sealed class CurrentCustomer
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Plan { get; private set; } = "";

    public void Set(Guid id, string name, string plan) => (Id, Name, Plan) = (id, name, plan);
}

public sealed record Ticket(int Number, Guid Customer, string Title, string Status);

public sealed record Invoice(string Number, Guid Customer, int AmountCents, int RefundedCents);

// What the tools read and change: a few tickets and invoices per customer, in memory
public sealed class CrmData
{
    public static readonly Guid Ada = Guid.Parse("00000000-0000-0000-0000-00000000a0da");

    public static readonly Guid Grace = Guid.Parse("00000000-0000-0000-0000-000000006ace");

    public List<Ticket> Tickets { get; } =
    [
        new(1001, Ada, "Cannot log in since the update", "open"),
        new(1002, Ada, "Export to CSV is empty", "closed"),
        new(1003, Ada, "Invoice INV-7 charged twice", "open"),
        new(2001, Grace, "API rate limit too low", "open")
    ];

    public List<Invoice> Invoices { get; } =
    [
        new("INV-7", Ada, 4900, 0),
        new("INV-8", Ada, 4900, 0),
        new("INV-21", Grace, 19900, 0)
    ];

    public List<string> Log { get; } = [];
}
