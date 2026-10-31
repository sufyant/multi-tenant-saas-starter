using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Notifications.Infrastructure.Persistence;

// A dictionary stored as one jsonb object, compared by its content, so a change of one value saves the column.
internal static class JsonColumn
{
    public static ValueConverter<Dictionary<string, TValue>, string> Converter<TValue>() =>
        new(
            values => JsonSerializer.Serialize(values, JsonSerializerOptions.Default),
            json => JsonSerializer.Deserialize<Dictionary<string, TValue>>(json, JsonSerializerOptions.Default) ?? new());

    public static ValueComparer<Dictionary<string, TValue>> Comparer<TValue>() =>
        new(
            (left, right) => left!.Count == right!.Count && left.All(pair => right.ContainsKey(pair.Key) && Equals(right[pair.Key], pair.Value)),
            values => values.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            values => new Dictionary<string, TValue>(values, StringComparer.Ordinal));

    // A value of nested dictionaries, such as a category's choice in each channel, compared by its JSON, so a change deep inside saves the
    // column too.
    public static ValueConverter<T, string> DocumentConverter<T>()
        where T : class, new() =>
        new(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
            json => JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Default) ?? new());

    public static ValueComparer<T> DocumentComparer<T>()
        where T : class, new() =>
        new(
            (left, right) => JsonSerializer.Serialize(left, JsonSerializerOptions.Default) == JsonSerializer.Serialize(right, JsonSerializerOptions.Default),
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonSerializerOptions.Default), JsonSerializerOptions.Default)!);
}
