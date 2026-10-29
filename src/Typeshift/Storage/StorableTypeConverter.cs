using System.ComponentModel;
using System.Globalization;

namespace Typeshift;

// D44: Newtonsoft.Json writes a type that converts to string as that string, so Typeshift needs no reference to it
internal sealed class StorableTypeConverter<T> : TypeConverter where T : class, IStorable<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string json
            ? T.Deserialize(json)
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType) =>
        destinationType == typeof(string) && value is T storable
            ? storable.Serialize()
            : base.ConvertTo(context, culture, value, destinationType);
}
