using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Typeshift.Generators.Models;

// An immutable array with sequence equality, so models that hold one stay cacheable
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _items = items;

    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Count => Items.Length;

    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode() => Items.Aggregate(17, (hash, item) => unchecked(hash * 31 + item.GetHashCode()));

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArray
{
    extension<T>(IEnumerable<T> items) where T : IEquatable<T>
    {
        public EquatableArray<T> ToEquatableArray() => new(items.ToImmutableArray());
    }
}
