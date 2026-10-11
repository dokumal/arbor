using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace QuireClone.Data;

/// <summary>Stores a list as a JSON array in a single text column.</summary>
internal static class JsonColumn
{
    // PascalCase on write matches the data written before this mapping existed.
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static PropertyBuilder<List<T>> HasJsonConversion<T>(this PropertyBuilder<List<T>> property, string columnName) =>
        property.HasConversion(
                value => Serialize(value),
                json => Deserialize<T>(json),
                new ValueComparer<List<T>>(
                    (left, right) => Serialize(left) == Serialize(right),
                    value => Serialize(value).GetHashCode(),
                    value => Deserialize<T>(Serialize(value))))
            .HasColumnName(columnName)
            .HasColumnType("TEXT")
            .IsRequired();

    private static string Serialize<T>(List<T>? value) => JsonSerializer.Serialize(value ?? [], Options);

    private static List<T> Deserialize<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
}
