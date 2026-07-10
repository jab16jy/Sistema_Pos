using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class BarcodeTests
{
    [Fact]
    public void Constructor_WithValidEan13_AcceptsTheStoredCheckDigit()
    {
        var barcode = new Barcode("4006381333931");

        Assert.Equal("4006381333931", barcode.Value);
    }

    [Fact]
    public void Constructor_WithInvalidEan13CheckDigit_RejectsTheValue()
    {
        Assert.Throws<ArgumentException>(() => new Barcode("4006381333930"));
    }

    [Theory]
    [InlineData("ABC\n123")]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Constructor_WithUnsafeOrOutOfRangeCode128_RejectsTheValue(string value)
    {
        Assert.Throws<ArgumentException>(() => new Barcode(value));
    }
}
