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

    public BarcodeViewModel(
        GenerateBarcodeUseCase generateBarcode,
        PrintLabelsUseCase printLabels,
        IProductLookupService productLookup)
    {
        _generateBarcode = generateBarcode ?? throw new ArgumentNullException(nameof(generateBarcode));
        _printLabels = printLabels ?? throw new ArgumentNullException(nameof(printLabels));
        _productLookup = productLookup ?? throw new ArgumentNullException(nameof(productLookup));

        SelectedProducts = [];
        GenerateCommand = new RelayCommand(_ => Generate());
        PrintSingleCommand = new RelayCommand(async _ => await PrintSingleAsync().ConfigureAwait(true));
        PrintBulkCommand = new RelayCommand(async _ => await PrintBulkAsync().ConfigureAwait(true));
    }

    public ObservableCollection<ProductLookupResult> SelectedProducts { get; }

    public ICommand GenerateCommand { get; }

    public ICommand PrintSingleCommand { get; }

    public ICommand PrintBulkCommand { get; }

    public string BarcodeValue
    {
        get => _barcodeValue;
        set => SetProperty(ref _barcodeValue, value ?? string.Empty);
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
            int normalized = value < 1 ? 1 : value;
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

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(BarcodeValue))
        {
            Message = "Ingresa un valor para generar el codigo de barras.";
            PreviewImageBytes = null;
            return;
        }

        try
        {
            var result = _generateBarcode.ExecuteAsync(BarcodeValue).GetAwaiter().GetResult();
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

    public async Task PrintSingleAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(BarcodeValue))
        {
            Message = "Ingresa un valor de codigo de barras para imprimir.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var productName = SelectedProducts.Count > 0
                ? SelectedProducts[0].Name
                : BarcodeValue;
            var price = SelectedProducts.Count > 0
                ? SelectedProducts[0].UnitPrice
                : 0m;

            var symbology = GenerateBarcodeUseCase.ResolveSymbology(BarcodeValue);
            var label = new LabelRequest(productName, price, BarcodeValue, symbology);
            await _printLabels.ExecuteAsync([label], Copies, cancellationToken).ConfigureAwait(true);
            Message = $"Etiqueta enviada a imprimir ({Copies} copia(s)).";
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
        if (SelectedProducts.Count == 0)
        {
            Message = "Selecciona al menos un producto para imprimir etiquetas masivas.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var labels = new List<LabelRequest>(SelectedProducts.Count);
            foreach (var product in SelectedProducts)
            {
                var barcodeVal = product.Barcode ?? product.ProductId.ToString();
                var symbology = GenerateBarcodeUseCase.ResolveSymbology(barcodeVal);
                labels.Add(new LabelRequest(product.Name, product.UnitPrice, barcodeVal, symbology));
            }

            await _printLabels.ExecuteAsync(labels, Copies, cancellationToken).ConfigureAwait(true);
            Message = $"{labels.Count} etiqueta(s) enviada(s) a imprimir ({Copies} copia(s) cada una).";
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
