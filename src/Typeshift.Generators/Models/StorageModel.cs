namespace Typeshift.Generators.Models;

internal enum StorageKind
{
    Scalar,
    Number,
    Enum,
    Object,
    Array,
    List,
    Set,
    Map,
    EnumMap
}

// D19: how a type settings can store is written and read. Type is the global::-qualified type without nullability;
// an object has its members, a collection its Element (a map's values), an enum map its Key
internal sealed record StorageModel(
    StorageKind Kind,
    string Type,
    EquatableArray<StoredMember> Members = default,
    StorageModel? Element = null,
    string? Key = null);

// Property is the C# member, JsonName its camelCase name in the stored JSON
internal sealed record StoredMember(string Property, string JsonName, StorageModel Storage);
