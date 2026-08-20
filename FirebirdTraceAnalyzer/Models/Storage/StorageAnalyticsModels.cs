namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>A ready-made query template for the "Storage analysis" window (name + starting SQL).</summary>
/// <param name="Name">Display name of the query template.</param>
/// <param name="Sql">The starting SQL of the query template.</param>
public sealed record PrebuiltQuery(string Name, string Sql);

/// <summary>A storage schema table for the hint tree (name + list of columns).</summary>
/// <param name="Name">The table name.</param>
/// <param name="Columns">The table's column names.</param>
public sealed record SchemaTable(string Name, IReadOnlyList<string> Columns);
