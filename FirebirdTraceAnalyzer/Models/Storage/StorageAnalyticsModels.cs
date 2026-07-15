namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>Готовый запрос-шаблон для окна «Анализ хранилища» (имя + SQL-отправная точка).</summary>
public sealed record PrebuiltQuery(string Name, string Sql);

/// <summary>Таблица схемы хранилища для дерева-подсказки (имя + список колонок).</summary>
public sealed record SchemaTable(string Name, IReadOnlyList<string> Columns);
