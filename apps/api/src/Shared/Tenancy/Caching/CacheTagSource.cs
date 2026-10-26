namespace Tenancy.Caching;

/// <summary>
/// A row of a module's tag table: a saved change of an entity of this type, added, changed or deleted, removes the tag of this kind
/// after the commit. The id of the tag is the value of the property, before and after the change; without a property, the tag is the
/// kind alone. The tenant column of a row that the database fills reads as the tenant the transaction declares.
/// </summary>
public sealed record CacheTagSource(Type Entity, string Kind, string? Property);
