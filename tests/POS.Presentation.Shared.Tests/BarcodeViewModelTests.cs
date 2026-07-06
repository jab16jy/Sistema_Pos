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
    public async Task GenerateCommand_WithValidValue_SetsPreviewImage()
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
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.NotNull(vm.PreviewImageBytes);
        Assert.Contains("generado", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateCommand_WithEmptyValue_SetsErrorMessageAndPreviewIsNull()
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
        await Task.Delay(50, TestContext.Current.CancellationToken);

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

        Assert.Contains("lote esta vacio", vm.Message, StringComparison.OrdinalIgnoreCase);
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

        vm.BatchItems.Add(new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product A", "BAR001", 5000m, 1));
        vm.BatchItems.Add(new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product B", "BAR002", 3000m, 1));

        await vm.PrintBulkAsync(TestContext.Current.CancellationToken);

        Assert.True(printService.WasCalled);
        Assert.Equal(2, printService.LastLabels?.Count ?? 0);
        Assert.Contains("enviada", vm.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.BatchItems);
    }

    [Fact]
    public async Task PrintBulkAsync_ExpandsPerItemCopiesBeforeSending()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);

        vm.BatchItems.Add(new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product A", "BAR001", 5000m, 2));
        vm.BatchItems.Add(new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product B", "BAR002", 3000m, 3));

        await vm.PrintBulkAsync(TestContext.Current.CancellationToken);

        Assert.True(printService.WasCalled);
        Assert.Equal(5, printService.LastLabels?.Count ?? 0);
        Assert.All(printService.LastLabels!, label => Assert.Contains(label.ProductName, new[] { "Product A", "Product B" }));
        Assert.Contains("5 etiqueta", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddCatalogItemCommand_AddsProductToBatch()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);
        var product = new ProductLookupResult(Guid.NewGuid(), "Product A", "BAR001", 5000m, 10m);

        vm.AddCatalogItemCommand.Execute(product);

        Assert.Single(vm.BatchItems);
        Assert.Equal("Product A", vm.BatchItems[0].Name);
    }

    [Fact]
    public void IncrementAndDecrementBatchItemCopiesCommand_ChangeCopies()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);
        var item = new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product A", "BAR001", 5000m, 1);
        vm.BatchItems.Add(item);

        vm.IncrementBatchItemCopiesCommand.Execute(item);
        Assert.Equal(2, item.Copies);

        vm.DecrementBatchItemCopiesCommand.Execute(item);
        vm.DecrementBatchItemCopiesCommand.Execute(item);
        Assert.Equal(1, item.Copies); // clamped to minimum of 1
    }

    [Fact]
    public void RemoveBatchItemCommand_RemovesItemFromBatch()
    {
        var barcodeService = new FakeBarcodeGenerationService([]);
        var printService = new FakeLabelPrintService();
        var productLookup = new FakeProductLookupService();
        var generate = new GenerateBarcodeUseCase(barcodeService);
        var print = new PrintLabelsUseCase(printService);
        var vm = new BarcodeViewModel(generate, print, productLookup);
        var item = new BarcodeBatchItem(Guid.NewGuid().ToString(), "Product A", "BAR001", 5000m, 1);
        vm.BatchItems.Add(item);

        vm.RemoveBatchItemCommand.Execute(item);

        Assert.Empty(vm.BatchItems);
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
