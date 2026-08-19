using FirebirdTraceAnalyzer.Core;

namespace FirebirdTraceParser.Tests;

public sealed class ByteSizeFormatterTests
{
    [Fact]
    public void FormatBytes_WhenCalled_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatBytes(1536);
        Assert.Equal("1,5 KB", result);
    }
    
    [Fact]
    public void FormatSpeed_WhenCalled_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatSpeed(1536);
        Assert.Equal("1,5 KB/s", result);
    }
    
    [Fact]
    public void BothMethods_WhenEquals_ReturnsEqualStrings()
    {
        var resultBytes = ByteSizeFormatter.FormatBytes(1536);
        var resultSpeed = ByteSizeFormatter.FormatSpeed(1536);
        
        Assert.Equal(resultBytes + "/s", resultSpeed);
    }
    
    [Fact]
    public void FormatBytes_WhenCalledWithLargeValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatBytes(1073741824);
        Assert.Equal("1 GB", result);
    }
    
    [Fact]
    public void FormatSpeed_WhenCalledWithLargeValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatSpeed(1073741824);
        Assert.Equal("1 GB/s", result);
    }

    [Fact]
    public void FormatBytes_WhenCalledWithSmallValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatBytes(512);
        Assert.Equal("512 B", result);
    }
    
    [Fact]
    public void FormatSpeed_WhenCalledWithSmallValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatSpeed(512);
        Assert.Equal("512 B/s", result);
    }
    
    [Fact]
    public void FormatBytes_WhenCalledWithZero_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatBytes(0);
        Assert.Equal("0 B", result);
    }

    [Fact]
    public void FormatSpeed_WhenCalledWithZero_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatSpeed(0);
        Assert.Equal("0 B/s", result);
    }

    [Fact]
    public void FormatBytes_WhenCalledWithNegativeValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatBytes(-1536);
        Assert.Equal("-1,5 KB", result);
    }

    [Fact]
    public void FormatSpeed_WhenCalledWithNegativeValue_ReturnsCorrectString()
    {
        var result = ByteSizeFormatter.FormatSpeed(-1536);
        Assert.Equal("-1,5 KB/s", result);
    }
}