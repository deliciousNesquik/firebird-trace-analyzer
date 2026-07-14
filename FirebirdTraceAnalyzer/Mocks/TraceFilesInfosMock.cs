using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Mocks;

public class TraceFilesInfosMock
{
    
    public static readonly List<TraceFileInfoModel> Mocks = [
        new(
            "2026_01_01__00_00_01.log",
            string.Empty,
            123_456_890,
            new DateTime(2026, 1, 1, 0, 0, 1),
            new DateTime(2026, 1, 1, 0, 20, 0),
            12_345,
            "design-sample-1"),
        new(
            "2026_01_01__00_20_01.log",
            string.Empty,
            123_456_890,
            new DateTime(2026, 1, 1, 0, 20, 1),
            new DateTime(2026, 1, 1, 0, 40, 0),
            67_890,
            "design-sample-2"),
        new(
            "2026_01_01__00_40_01.log",
            string.Empty,
            123_456_890,
            new DateTime(2026, 1, 1, 0, 40, 1),
            new DateTime(2026, 1, 1, 1, 0, 0),
            123_456,
            "design-sample-3")
        
    ];
}