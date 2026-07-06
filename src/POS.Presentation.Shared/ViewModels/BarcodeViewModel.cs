using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.BarcodeLabels;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for the Barcode / Label printing module.
/// Generates barcode previews and sends label print jobs.
/// </summary>
public sealed class BarcodeViewModel : ViewModelBase
{
    private readonly GenerateBarcodeUseCase _generateBarcode;
    private readonly PrintLabelsUseCase _printLabels;
    private readonly IProductLookupService _productLookup;

    private string _barcodeValue = string.Empty;
    private byte[]? _previewImageBytes;
    private int _copies = 1;
    private string _message = string.Empty;
    private bool _isLoading;

    // ── Presentation-only: generator fields ──
    private string _selectedFormat = "CODE 128";
    private string _labelText = string.Empty;
    private decimal? _labelPrice;
    private string _catalogSearchText = string.Empty;

    public BarcodeViewModel(
        GenerateBarcodeUseCase generateBarcode,
        PrintLabelsUseCase printLabels,
        IProductLookupService productLookup)
    {
        _generateBarcode = generateBarcode ?? throw new ArgumentNullException(nameof(generateBarcode));
        _printLabels = printLabels ?? throw new ArgumentNullException(nameof(printLabels));
        _productLookup = productLookup ?? throw new ArgumentNullException(nameof(productLookup));

        SelectedProducts = [];
        BatchItems = [];
        CatalogSearchResults = [];

        GenerateCommand = new RelayCommand(_ => RunSafe(GenerateAsync(), ex => Message = $"Error al generar el código: {ex.Message}"));
        PrintSingleCommand = new RelayCommand(_ => RunSafe(PrintSingleAsync(), ex => Message = $"Error al imprimir la etiqueta: {ex.Message}"));
        PrintBulkCommand = new RelayCommand(_ => RunSafe(PrintBulkAsync(), ex => Message = $"Error al imprimir etiquetas: {ex.Message}"));
        AddToBatchCommand = new RelayCommand(_ => AddGeneratedToBatch());
        SearchCatalogCommand = new RelayCommand(_ => RunSafe(SearchCatalogAsync(), ex => Message = $"Error buscando productos: {ex.Message}"));
        ClearBatchCommand = new RelayCommand(_ => ClearBatch());

        // ── Row-action command wrappers ──
        // AddCatalogProductToBatch/ChangeBatchItemCopies/RemoveBatchItem are plain methods (not ICommand) so
        // callers other than XAML (e.g. tests) can invoke them directly. XAML per-row buttons need an ICommand
        // binding target — same pattern as InventoryView's QuickAdjustCommand / PurchasesView's RemoveDraftItemCommand.
        AddCatalogItemCommand = new RelayCommand(p =>
        {
            if (p is ProductLookupResult product)
            {
                AddCatalogProductToBatch(product);
            }
        });
        IncrementBatchItemCopiesCommand = new RelayCommand(p =>
        {
            if (p is BarcodeBatchItem item)
            {
                ChangeBatchItemCopies(item, 1);
            }
        });
        DecrementBatchItemCopiesCommand = new RelayCommand(p =>
        {
            if (p is BarcodeBatchItem item)
            {
                ChangeBatchItemCopies(item, -1);
            }
        });
        RemoveBatchItemCommand = new RelayCommand(p =>
        {
            if (p is BarcodeBatchItem item)
            {
                RemoveBatchItem(item);
            }
        });
    }

    public ObservableCollection<ProductLookupResult> SelectedProducts { get; }
    public ObservableCollection<BarcodeBatchItem> BatchItems { get; }
    public ObservableCollection<ProductLookupResult> CatalogSearchResults { get; }

    public ICommand GenerateCommand { get; }
    public ICommand PrintSingleCommand { get; }
    public ICommand PrintBulkCommand { get; }
    public ICommand AddToBatchCommand { get; }
    public ICommand SearchCatalogCommand { get; }
    public ICommand ClearBatchCommand { get; }
    public ICommand AddCatalogItemCommand { get; }
    public ICommand IncrementBatchItemCopiesCommand { get; }
    public ICommand DecrementBatchItemCopiesCommand { get; }
    public ICommand RemoveBatchItemCommand { get; }

    public string BarcodeValue
    {
        get => _barcodeValue;
        set
        {
            if (SetProperty(ref _barcodeValue, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasPreview));
                OnPropertyChanged(nameof(PreviewLabel));
                OnPropertyChanged(nameof(PreviewPriceFormatted));
                OnPropertyChanged(nameof(HasPreviewPrice));
            }
        }
    }

    public byte[]? PreviewImageBytes
    {
        get => _previewImageBytes;
        private set => SetProperty(ref _previewImageBytes, value);
    }

    public int Copies
    {
        get => _copies;
        set
        {
            int normalized = value < 1 ? 1 : value > 100 ? 100 : value;
            SetProperty(ref _copies, normalized);
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    // ── Presentation-only: generator fields ──

    public string SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value ?? "CODE 128");
    }

    public string LabelText
    {
        get => _labelText;
        set
        {
            if (SetProperty(ref _labelText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(PreviewLabel));
            }
        }
    }

    public decimal? LabelPrice
    {
        get => _labelPrice;
        set
        {
            if (SetProperty(ref _labelPrice, value))
            {
                OnPropertyChanged(nameof(PreviewPriceFormatted));
                OnPropertyChanged(nameof(HasPreviewPrice));
            }
        }
    }

    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set => SetProperty(ref _catalogSearchText, value ?? string.Empty);
    }

    // ── Presentation-only: preview computed ──

    public bool HasPreview => !string.IsNullOrWhiteSpace(BarcodeValue);

    public string PreviewLabel => string.IsNullOrWhiteSpace(LabelText) ? "PRODUCTO" : LabelText.ToUpperInvariant();

    public string PreviewPriceFormatted => LabelPrice.HasValue && LabelPrice.Value > 0 ? $"${LabelPrice.Value:N0}" : string.Empty;

    public bool HasPreviewPrice => LabelPrice.HasValue && LabelPrice.Value > 0;

    public int BatchItemCount => BatchItems.Count;

    public int BatchTotalLabels => BatchItems.Sum(b => b.Copies);

    public bool HasBatchItems => BatchItems.Count > 0;

    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeValue))
        {
            Message = "Ingresa el valor del codigo";
            PreviewImageBytes = null;
            return;
        }

        try
        {
            var result = await _generateBarcode.ExecuteAsync(BarcodeValue).ConfigureAwait(true);
            PreviewImageBytes = result.PngBytes;
            Message = $"Codigo generado ({result.Symbology}).";
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
            PreviewImageBytes = null;
        }
        catch (ApplicationException ex)
        {
            Message = ex.Message;
            PreviewImageBytes = null;
        }
    }

    private void AddGeneratedToBatch()
    {
        if (string.IsNullOrWhiteSpace(BarcodeValue))
        {
            Message = "Ingresa el valor del codigo";
            return;
        }

        var name = string.IsNullOrWhiteSpace(LabelText) ? BarcodeValue : LabelText;
        var price = LabelPrice ?? 0m;

        // Check for duplicate: increment copies instead of adding a new row
        var existing = BatchItems.FirstOrDefault(b => b.BarcodeValue == BarcodeValue);
        if (existing is not null)
        {
            existing.Copies += Copies;
            RefreshBatchProperties();
            Message = $"Copias incrementadas a {existing.Copies} para {name}";
            return;
        }

        BatchItems.Add(new BarcodeBatchItem(
            Guid.NewGuid().ToString(),
            name,
            BarcodeValue,
            price,
            Copies));

        RefreshBatchProperties();
        Message = "Agregado al lote";
    }

    public void AddCatalogProductToBatch(ProductLookupResult product)
    {
        var barcodeVal = product.Barcode ?? product.ProductId.ToString();

        // Check for duplicate: increment copies instead of adding a new row
        var existing = BatchItems.FirstOrDefault(b => b.ProductId == product.ProductId.ToString());
        if (existing is not null)
        {
            existing.Copies++;
            RefreshBatchProperties();
            Message = $"Copias incrementadas a {existing.Copies} para {product.Name}";
            return;
        }

        BatchItems.Add(new BarcodeBatchItem(
            product.ProductId.ToString(),
            product.Name,
            barcodeVal,
            product.UnitPrice,
            1));

        RefreshBatchProperties();
        CatalogSearchResults.Clear();
        CatalogSearchText = string.Empty;
    }

    public void ChangeBatchItemCopies(BarcodeBatchItem item, int delta)
    {
        item.Copies = Math.Max(1, item.Copies + delta);
        RefreshBatchProperties();
    }

    public void RemoveBatchItem(BarcodeBatchItem item)
    {
        BatchItems.Remove(item);
        RefreshBatchProperties();
    }

    private void ClearBatch()
    {
        BatchItems.Clear();
        RefreshBatchProperties();
    }

    private void RefreshBatchProperties()
    {
        OnPropertyChanged(nameof(BatchItemCount));
        OnPropertyChanged(nameof(BatchTotalLabels));
        OnPropertyChanged(nameof(HasBatchItems));
    }

    private async Task SearchCatalogAsync()
    {
        CatalogSearchResults.Clear();

        if (string.IsNullOrWhiteSpace(CatalogSearchText) || CatalogSearchText.Length < 2)
        {
            return;
        }

        try
        {
            var results = await _productLookup.SearchAsync(CatalogSearchText).ConfigureAwait(true);
            foreach (var result in results.Take(10))
            {
                CatalogSearchResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            Message = $"Error buscando productos: {ex.Message}";
        }
    }

    public async Task PrintSingleAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(BarcodeValue))
        {
            Message = "Ingresa el valor del codigo";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var productName = string.IsNullOrWhiteSpace(LabelText) ? BarcodeValue : LabelText;
            var price = LabelPrice ?? 0m;

            var symbology = GenerateBarcodeUseCase.ResolveSymbology(BarcodeValue);
            var label = new LabelRequest(productName, price, BarcodeValue, symbology);
            await _printLabels.ExecuteAsync([label], Copies, cancellationToken).ConfigureAwait(true);
            Message = $"{Copies} etiqueta(s) enviada(s) a imprimir";
        }
        catch (ApplicationException ex)
        {
            Message = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PrintBulkAsync(CancellationToken cancellationToken = default)
    {
        if (BatchItems.Count == 0)
        {
            Message = "El lote esta vacio";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var labels = new List<LabelRequest>();
            var totalCopies = 0;

            foreach (var item in BatchItems)
            {
                var symbology = GenerateBarcodeUseCase.ResolveSymbology(item.BarcodeValue);
                for (int copyIndex = 0; copyIndex < item.Copies; copyIndex++)
                {
                    labels.Add(new LabelRequest(item.Name, item.Price, item.BarcodeValue, symbology));
                    totalCopies++;
                }
            }

            await _printLabels.ExecuteAsync(labels, 1, cancellationToken).ConfigureAwait(true);
            Message = $"{totalCopies} etiqueta(s) enviada(s) a imprimir";

            // Reset batch after successful print
            BatchItems.Clear();
            RefreshBatchProperties();
        }
        catch (ApplicationException ex)
        {
            Message = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Presentation-only batch item for barcode label printing.</summary>
public sealed class BarcodeBatchItem : ViewModelBase
{
    private int _copies;

    public BarcodeBatchItem(string productId, string name, string barcodeValue, decimal price, int copies)
    {
        ProductId = productId;
        Name = name;
        BarcodeValue = barcodeValue;
        Price = price;
        _copies = copies;
    }

    public string ProductId { get; }
    public string Name { get; }
    public string BarcodeValue { get; }
    public decimal Price { get; }

    public int Copies
    {
        get => _copies;
        set => SetProperty(ref _copies, value < 1 ? 1 : value);
    }

    public string PriceFormatted => Price > 0 ? $"${Price:N0}" : string.Empty;
    public string MetaLine => Price > 0 ? $"{BarcodeValue} · ${Price:N0}" : BarcodeValue;
}
