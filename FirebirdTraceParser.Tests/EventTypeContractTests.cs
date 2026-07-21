using FirebirdTraceParser.Models.Enums;

namespace FirebirdTraceParser.Tests;

/// <summary>
/// EventType сохраняется в хранилище как целочисленный ординал, поэтому значения — это контракт.
/// Тест фиксирует их и ловит случайное переупорядочивание/удаление членов.
/// </summary>
public sealed class EventTypeContractTests
{
    [Theory]
    [InlineData(EventType.TraceInit, 0)]
    [InlineData(EventType.ExecuteStatementStart, 4)]
    [InlineData(EventType.ExecuteStatementFinish, 6)]
    [InlineData(EventType.ExecuteProcedureStart, 7)]
    [InlineData(EventType.ExecuteProcedureRestart, 8)]
    [InlineData(EventType.ExecuteProcedureFinish, 9)]
    [InlineData(EventType.FailedExecuteTriggerFinish, 14)]
    [InlineData(EventType.Error, 15)]
    public void Ordinal_IsPinned(EventType type, int expected)
        => Assert.Equal(expected, (int)type);
}
