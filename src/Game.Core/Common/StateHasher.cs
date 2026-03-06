using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Game.Core.Common;

public static class StateHasher
{
    public static string Hash<TState>(TState state)
    {
        var canonical = Canonicalize(state);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes);
    }

    private static string Canonicalize(object? value)
    {
        var builder = new StringBuilder();
        AppendCanonical(builder, value);
        return builder.ToString();
    }

    private static void AppendCanonical(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case string stringValue:
                builder.Append("str:").Append(stringValue.Length).Append(':').Append(stringValue);
                return;
            case bool boolValue:
                builder.Append(boolValue ? "bool:1" : "bool:0");
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                builder.Append("int:").Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case float floatValue:
                builder.Append("float:").Append(floatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case double doubleValue:
                builder.Append("double:").Append(doubleValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case decimal decimalValue:
                builder.Append("decimal:").Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                return;
            case Enum enumValue:
                builder.Append("enum:").Append(enumValue.GetType().FullName).Append(':').Append(enumValue.ToString());
                return;
        }

        var type = value.GetType();

        if (value is IDictionary dictionary)
        {
            AppendDictionary(builder, dictionary, type);
            return;
        }

        if (IsReadOnlyDictionaryType(type))
        {
            AppendDictionary(builder, (IEnumerable)value, type);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            AppendEnumerable(builder, enumerable, type);
            return;
        }

        AppendObject(builder, value, type);
    }

    private static void AppendDictionary(StringBuilder builder, IDictionary dictionary, Type type)
    {
        AppendDictionary(builder, (IEnumerable)dictionary, type);
    }

    private static void AppendDictionary(StringBuilder builder, IEnumerable dictionary, Type type)
    {
        builder.Append("dict:").Append(type.FullName).Append('[');

        var entries = EnumerateDictionaryEntries(dictionary)
            .Select(entry => new KeyValuePair<string, object?>(Canonicalize(entry.Key), entry.Value))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(entries[i].Key).Append("=>");
            AppendCanonical(builder, entries[i].Value);
        }

        builder.Append(']');
    }

    private static bool IsReadOnlyDictionaryType(Type type)
    {
        return type
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
    }

    private static IEnumerable<(object? Key, object? Value)> EnumerateDictionaryEntries(IEnumerable dictionary)
    {
        foreach (var entry in dictionary)
        {
            if (entry is DictionaryEntry dictionaryEntry)
            {
                yield return (dictionaryEntry.Key, dictionaryEntry.Value);
                continue;
            }

            if (entry is null)
            {
                throw new InvalidOperationException("Dictionary entry cannot be null.");
            }

            var entryType = entry.GetType();
            var keyProperty = entryType.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
            var valueProperty = entryType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (keyProperty is null || valueProperty is null)
            {
                throw new InvalidOperationException($"Unsupported dictionary entry type: {entryType.FullName}.");
            }

            yield return (keyProperty.GetValue(entry), valueProperty.GetValue(entry));
        }
    }

    private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable, Type type)
    {
        builder.Append("seq:").Append(type.FullName).Append('[');

        var isFirst = true;
        foreach (var item in enumerable)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            AppendCanonical(builder, item);
            isFirst = false;
        }

        builder.Append(']');
    }

    private static void AppendObject(StringBuilder builder, object value, Type type)
    {
        builder.Append("obj:").Append(type.FullName).Append('{');

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.CanRead && prop.GetIndexParameters().Length == 0)
            .OrderBy(prop => prop.Name, StringComparer.Ordinal)
            .ToArray();

        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var property = properties[i];
            builder.Append(property.Name).Append('=');
            AppendCanonical(builder, property.GetValue(value));
        }

        builder.Append('}');
    }
}
