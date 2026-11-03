using System.Buffers.Binary;
using Emlak.Api.Data;
using Emlak.Api.Photos;
using Xunit;

namespace Emlak.Api.Tests;

public class PlaceholderImageTests
{
    [Theory]
    [InlineData(1, 800, 600)]
    [InlineData(12, 320, 240)]
    public void Create_ReturnsValidPng_WithRequestedSize(int index, int width, int height)
    {
        var bytes = PlaceholderImage.Create(index, width, height);

        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        // İmzadan sonra IHDR: uzunluk(4) + "IHDR"(4) + genişlik(4) + yükseklik(4)
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.Equal("png", PhotoValidator.DetectExtension(bytes));
        Assert.True(bytes.Length < PhotoValidator.MaxFileBytes);
    }

    [Fact]
    public void Create_DifferentIndexes_GiveDifferentImages()
    {
        Assert.NotEqual(PlaceholderImage.Create(1, 200, 150), PlaceholderImage.Create(2, 200, 150));
    }
}
