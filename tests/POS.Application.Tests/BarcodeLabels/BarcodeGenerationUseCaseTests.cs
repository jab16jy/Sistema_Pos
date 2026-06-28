using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using POS.Application.UseCases.BarcodeLabels;
using Xunit;

namespace POS.Application.Tests.BarcodeLabels;

public sealed class BarcodeGenerationUseCaseTests
{
    [Theory]
    [InlineData("123456789012", BarcodeSymbology.Ean13)]   // exactly 12 digits → EAN-13
    [InlineData("12345678901A", BarcodeSymbology.Code128)] // 12 chars but not all digits
    [InlineData("1234567890123", BarcodeSymbology.Code128)] // 13 digits
    [InlineData("PROD-001", BarcodeSymbology.Code128)]     // product code
    [InlineData("", BarcodeSymbology.Code128)]             // empty treated as Code128 by resolver
    public void ResolveSymbology_MatchesRule(string value, BarcodeSymbology expected)
    {
        var actual = GenerateBarcodeUseCase.ResolveSymbology(value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidValue_ReturnsBarcodeResult()
    {
        var service = new StubBarcodeService();
        var useCase = new GenerateBarcodeUseCase(service);

        var result = await useCase.ExecuteAsync("PRODUCT-42", TestContext.Current.CancellationToken);

        Assert.NotNull(result.PngBytes);
        Assert.Equal(BarcodeSymbology.Code128, result.Symbology);
        Assert.Equal("PRODUCT-42", service.LastValue);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyValue_ThrowsArgumentException()
    {
        var useCase = new GenerateBarcodeUseCase(new StubBarcodeService());

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync("  ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_With12Digits_UsesEan13Symbology()
    {
        var service = new StubBarcodeService();
        var useCase = new GenerateBarcodeUseCase(service);

        var result = await useCase.ExecuteAsync("123456789012", TestContext.Current.CancellationToken);

        Assert.Equal(BarcodeSymbology.Ean13, result.Symbology);
    }

    // --- Stub ---

    private sealed class StubBarcodeService : IBarcodeGenerationService
    {
        public string? LastValue { get; private set; }

        public Task<byte[]> GenerateAsync(string value, BarcodeSymbology symbology, CancellationToken ct = default)
        {
            LastValue = value;
            return Task.FromResult(new byte[] { 0xFF });
        }

        public Task<byte[]> GenerateAutoAsync(string value, CancellationToken ct = default)
        {
            LastValue = value;
            return Task.FromResult(new byte[] { 0xFF });
        }
    }
}
