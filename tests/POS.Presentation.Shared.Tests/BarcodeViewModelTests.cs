using POS.Application.DTOs.BarcodeLabels;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.BarcodeLabels;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class BarcodeViewModelTests
{
    [Fact]
    public void GenerateCommand_WithValidValue_SetsPreviewImage()
    {
        // Minimal valid PNG bytes (1x1 transparent pixel)
        byte[] fakePng = CreateMinimalPng();
        var barcodeService = new FakeBarcodeGenerationService(fakePng);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup)
        {
            BarcodeValue = "770123456789"
        };

        vm.GenerateCommand.Execute(null);

        Assert.NotNull(vm.PreviewImageBytes);
        Assert.Contains("generado", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateCommand_WithEmptyValue_SetsErrorMessageAndPreviewIsNull()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup)
        {
            BarcodeValue = ""
        };

        vm.GenerateCommand.Execute(null);

        Assert.Null(vm.PreviewImageBytes);
        Assert.False(string.IsNullOrWhiteSpace(vm.Message));
    }

    [Fact]
    public async Task PrintBulkAsync_WithNoProducts_SetsErrorMessage()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);

        await vm.PrintBulkAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Selecciona", vm.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(printService.WasCalled);
    }

    [Fact]
    public async Task PrintBulkAsync_WithProducts_InvokesPrintLabelsUseCase()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);

        vm.SelectedProducts.Add(new ProductLookupResult(Guid.NewGuid(), "Product A", "BAR001", 5000m, 10m));
        vm.SelectedProducts.Add(new ProductLookupResult(Guid.NewGuid(), "Product B", "BAR002", 3000m, 5m));

        await vm.PrintBulkAsync(TestContext.Current.CancellationToken);

        Assert.True(printService.WasCalled);
        Assert.Equal(2, printService.LastLabels?.Count ?? 0);
        Assert.Contains("enviada", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a minimal valid PNG (1x1 transparent pixel) for BitmapImage parsing.
    /// </summary>
    private static byte[] CreateMinimalPng()
    {
        // Minimal PNG: signature + IHDR + IDAT + IEND
        // This is a 1x1 transparent pixel PNG (67 bytes)
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, // RGBA, filters
            0x89,
            0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, // IDAT chunk
            0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x01, 0xE5, 0x27, 0xDE, 0xFC,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82
        ];
    }

    // ─────────────────────────────────────────────────────
    // Fakes
    // ─────────────────────────────────────────────────────

    private sealed class FakeBarcodeGenerationService : IBarcodeGenerationService
    {
        private readonly byte[] _result;

        public FakeBarcodeGenerationService(byte[] result) => _result = result;

        public Task<byte[]> GenerateAsync(string value, BarcodeSymbology symbology, CancellationToken ct = default)
            => Task.FromResult(_result);

        public Task<byte[]> GenerateAutoAsync(string value, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeLabelPrintService : ILabelPrintService
    {
        public bool WasCalled { get; private set; }
        public IReadOnlyList<LabelRequest>? LastLabels { get; private set; }

        public Task PrintAsync(IEnumerable<LabelRequest> labels, int copies = 1, CancellationToken ct = default)
        {
            WasCalled = true;
            LastLabels = labels.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductLookupService : IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([]);
    }
}
