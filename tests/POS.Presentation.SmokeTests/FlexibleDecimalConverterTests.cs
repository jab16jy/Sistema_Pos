using System.Globalization;
using System.Windows.Data;
using POS.Presentation.Converters;
using Xunit;

namespace POS.Presentation.SmokeTests;

public sealed class FlexibleDecimalConverterTests
{
    private readonly FlexibleDecimalConverter _converter = new();

    [Theory]
    [InlineData("10,8", 10.8)]
    [InlineData("10.8", 10.8)]
    [InlineData("9,6", 9.6)]
    [InlineData("100", 100)]
    public void ConvertBack_AcceptsCommaOrDotAsDecimalSeparator(string input, double expected)
    {
        var result = _converter.ConvertBack(input, typeof(decimal), null, CultureInfo.InvariantCulture);

        Assert.Equal((decimal)expected, result);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("10,8,5")]
    public void ConvertBack_WithInvalidText_DoesNotOverwriteSource(string input)
    {
        var result = _converter.ConvertBack(input, typeof(decimal), null, CultureInfo.InvariantCulture);

        Assert.Equal(Binding.DoNothing, result);
    }

    [Fact]
    public void Convert_DisplaysWithComma()
    {
        var result = _converter.Convert(10.8m, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("10,8", result);
    }
}
