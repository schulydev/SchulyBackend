using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Schuly.Domain.Mcp;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>Stores a permission set as one JSON column. Unknown areas in stored JSON are dropped rather than failing the read.</summary>
    public static class McpPermissionJson
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

        public static string Serialize(McpPermissionSet? value) => JsonSerializer.Serialize(value ?? McpPermissionSet.None(), Options);

        public static McpPermissionSet Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return McpPermissionSet.None();
            try
            {
                return JsonSerializer.Deserialize<McpPermissionSet>(json, Options) ?? McpPermissionSet.None();
            }
            catch (JsonException)
            {
                return McpPermissionSet.None();
            }
        }

        public static readonly ValueConverter<McpPermissionSet, string> Converter = new(v => Serialize(v), v => Deserialize(v));

        public static readonly ValueConverter<McpPermissionSet?, string?> NullableConverter = new(v => v == null ? null : Serialize(v), v => v == null ? null : Deserialize(v));

        public static readonly ValueComparer<McpPermissionSet> Comparer = new((a, b) => Serialize(a) == Serialize(b), v => Serialize(v).GetHashCode(), v => Deserialize(Serialize(v)));

        public static readonly ValueComparer<McpPermissionSet?> NullableComparer = new((a, b) => Serialize(a) == Serialize(b), v => Serialize(v).GetHashCode(), v => v == null ? null : Deserialize(Serialize(v)));
    }
}
