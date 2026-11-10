namespace Typeshift.Tests.Tools.Support;

/// <param name="Name">The dog's name.</param>
/// <param name="Breed">The dog's breed.</param>
public sealed record Dog(string Name, string Breed);

/// <param name="Name">The cat's name.</param>
/// <param name="Lives">The lives the cat has left.</param>
public sealed record Cat(string Name, int Lives);

public union Pet(Dog, Cat);

public union TicketKey(int, string);

public union Seats(int, Dog);

public sealed record TicketLookup(int? Number, string? Title);

/// <param name="Shelter">The shelter the pet comes from.</param>
/// <param name="Pet">The pet.</param>
public sealed record Adoption(string Shelter, Pet Pet);

// Each tool hands back the case it received, so a test reads how the argument bound
public partial class UnionToolset : Toolset
{
    public UnionToolset()
    {
        AddTool(Adopt)
            .WithDescription("Adopts a pet.");

        AddTool(FindTicket)
            .WithDescription("Finds a ticket by its number or its title.");

        AddTool(Reserve)
            .WithDescription("Reserves seats, or a seat for a dog.");
    }

    /// <param name="pet">The pet to adopt.</param>
    public static Pet Adopt(Pet pet) => pet;

    /// <param name="key">The ticket's number or its title.</param>
    public static TicketLookup FindTicket(TicketKey key) =>
        key switch
        {
            int number => new TicketLookup(number, null),
            string title => new TicketLookup(null, title),
            null => new TicketLookup(null, null)
        };

    /// <param name="seats">How many seats, or the dog that needs one.</param>
    public static int Reserve(Seats seats) =>
        seats switch
        {
            int count => count,
            Dog => 1,
            null => 0
        };
}
