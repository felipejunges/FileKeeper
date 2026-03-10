using FileKeeper.Core.Extensions;

namespace FileKeeper.Tests.Core.Extensions;

public class LongExtensionsTests
{
    [Fact]
    public void ToHumanReadableSize_WithBytesLessThan1024_ReturnsBytes()
    {
        // Arrange
        long sizeInBytes = 512;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("512 B", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithZeroBytes_ReturnsZero()
    {
        // Arrange
        long sizeInBytes = 0;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("0 B", result);
    }

    [Fact]
    public void ToHumanReadableSize_With1023Bytes_ReturnsBytesNotKB()
    {
        // Arrange
        long sizeInBytes = 1023;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("1023 B", result);
    }

    [Fact]
    public void ToHumanReadableSize_With1024Bytes_ReturnsKilobytes()
    {
        // Arrange
        long sizeInBytes = 1024;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("1.00 KB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithKilobytes_ReturnsFormattedKB()
    {
        // Arrange
        long sizeInBytes = 1024 * 512; // 512 KB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("512.00 KB", result);
    }
    
    [Fact]
    public void ToHumanReadableSize_WithKilobytes_ValueCreatedByMe_ReturnsFormattedKB()
    {
        // Arrange
        long sizeInBytes = 556076;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("543.04 KB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithMegabytes_ReturnsFormattedMB()
    {
        // Arrange
        long sizeInBytes = 1024L * 1024 * 256; // 256 MB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("256.00 MB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithGigabytes_ReturnsFormattedGB()
    {
        // Arrange
        long sizeInBytes = 1024L * 1024 * 1024 * 2; // 2 GB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("2.00 GB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithTerabytes_ReturnsFormattedTB()
    {
        // Arrange
        long sizeInBytes = 1024L * 1024 * 1024 * 1024 * 5; // 5 TB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("5.00 TB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithPetabytes_ReturnsFormattedPB()
    {
        // Arrange
        long sizeInBytes = 1024L * 1024 * 1024 * 1024 * 1024; // 1 PB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Contains("PB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithExabytes_ReturnsFormattedEB()
    {
        // Arrange
        long sizeInBytes = 1024L * 1024 * 1024 * 1024 * 1024 * 1024; // 1 EB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("1.00 EB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithDecimalValue_ReturnsFormattedWithTwoDecimalPlaces()
    {
        // Arrange
        long sizeInBytes = 1536; // 1.5 KB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("1.50 KB", result);
    }

    [Fact]
    public void ToHumanReadableSize_With1_5MB_ReturnsFormattedMB()
    {
        // Arrange
        long sizeInBytes = (long)(1.5 * 1024 * 1024); // 1.5 MB
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("1.50 MB", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithNegativeValue_ReturnsNegativeBytes()
    {
        // Arrange
        long sizeInBytes = -512;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        Assert.Equal("-512 B", result);
    }

    [Fact]
    public void ToHumanReadableSize_WithMaxLongValue_ReturnsFormattedEB()
    {
        // Arrange
        long sizeInBytes = long.MaxValue;
        
        // Act
        var result = sizeInBytes.ToHumanReadableSize();
        
        // Assert
        // MaxValue é 9223372036854775807 bytes = ~8 EB
        Assert.Contains("EB", result);
        Assert.DoesNotContain("NaN", result);
        Assert.DoesNotContain("Infinity", result);
    }
}