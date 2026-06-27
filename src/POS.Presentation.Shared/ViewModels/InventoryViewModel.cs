using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Inventory;
using POS.Application.UseCases.Inventory;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class InventoryViewModel : ViewModelBase
{
    private readonly QueryStockUseCase _queryStock;
    private readonly QueryLowStockUseCase _queryLowStock;
    private readonly QueryMovementHistoryUseCase _queryMovementHistory;
    private readonly AdjustStockUseCase _adjustStock;
    private StockLevelResult? _selectedProduct;
    private decimal _adjustmentDelta;
    private string _adjustmentReason = string.Empty;
    private string _message = "Consulta el stock de productos o revisa alertas de stock bajo.";

    public InventoryViewModel(
        QueryStockUseCase queryStock,
        QueryLowStockUseCase queryLowStock,
        QueryMovementHistoryUseCase queryMovementHistory,
        AdjustStockUseCase adjustStock)
    {
        _queryStock = queryStock;
        _queryLowStock = queryLowStock;
        _queryMovementHistory = queryMovementHistory;
        _adjustStock = adjustStock;
        LoadStockCommand = new RelayCommand(_ => _ = LoadStockAsync());
        LoadLowStockCommand = new RelayCommand(_ => _ = LoadLowStockAsync());
        LoadMovementHistoryCommand = new RelayCommand(_ => _ = LoadMovementHistoryAsync(), _ => SelectedProduct is not null);
        AdjustStockCommand = new RelayCommand(_ => _ = AdjustStockAsync(), _ => SelectedProduct is not null && !string.IsNullOrWhiteSpace(AdjustmentReason));
    }

    public ObservableCollection<StockLevelResult> StockLevels { get; } = [];

    public ObservableCollection<LowStockProductResult> LowStockProducts { get; } = [];

    public ObservableCollection<InventoryMovementResult> MovementHistory { get; } = [];

    public ICommand LoadStockCommand { get; }

    public ICommand LoadLowStockCommand { get; }

    public ICommand LoadMovementHistoryCommand { get; }

    public ICommand AdjustStockCommand { get; }

    public StockLevelResult? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                MovementHistory.Clear();
                AdjustmentDelta = 0;
                AdjustmentReason = string.Empty;

                if (LoadMovementHistoryCommand is RelayCommand historyCommand)
                {
                    historyCommand.RaiseCanExecuteChanged();
                }

                if (AdjustStockCommand is RelayCommand adjustCommand)
                {
                    adjustCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public decimal AdjustmentDelta
    {
        get => _adjustmentDelta;
        set => SetProperty(ref _adjustmentDelta, value);
    }

    public string AdjustmentReason
    {
        get => _adjustmentReason;
        set
        {
            if (SetProperty(ref _adjustmentReason, value))
            {
                if (AdjustStockCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public Guid OperatorUserId { get; set; }

    public async Task LoadStockAsync(CancellationToken cancellationToken = default)
    {
        var results = await _queryStock.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        StockLevels.Clear();
        foreach (var stock in results)
        {
            StockLevels.Add(stock);
        }

        LowStockProducts.Clear();
        Message = StockLevels.Count == 0
            ? "No hay productos registrados en el sistema."
            : $"Mostrando {StockLevels.Count} producto(s). Selecciona uno para ver su historial de movimientos.";
    }

    public async Task LoadLowStockAsync(CancellationToken cancellationToken = default)
    {
        var results = await _queryLowStock.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        LowStockProducts.Clear();
        foreach (var product in results)
        {
            LowStockProducts.Add(product);
        }

        StockLevels.Clear();
        Message = LowStockProducts.Count == 0
            ? "No hay productos con stock bajo. ¡Excelente!"
            : $"Hay {LowStockProducts.Count} producto(s) con stock bajo o agotado.";
    }

    public async Task LoadMovementHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null)
        {
            Message = "Selecciona un producto para ver su historial de movimientos.";
            return;
        }

        var results = await _queryMovementHistory.ExecuteAsync(SelectedProduct.ProductId, cancellationToken).ConfigureAwait(true);
        MovementHistory.Clear();
        foreach (var movement in results)
        {
            MovementHistory.Add(movement);
        }

        Message = MovementHistory.Count == 0
            ? $"{SelectedProduct.ProductName} no tiene movimientos registrados."
            : $"Mostrando {MovementHistory.Count} movimiento(s) de {SelectedProduct.ProductName}.";
    }

    public async Task AdjustStockAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null)
        {
            Message = "Selecciona un producto para ajustar su stock.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AdjustmentReason))
        {
            Message = "Debes ingresar una razón para el ajuste de stock.";
            return;
        }

        try
        {
            var request = new AdjustStockRequest(
                SelectedProduct.ProductId,
                AdjustmentDelta,
                AdjustmentReason,
                OperatorUserId);

            var result = await _adjustStock.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            Message = $"Stock ajustado: {SelectedProduct.ProductName} ahora tiene {result.CurrentStock} unidad(es).";

            AdjustmentDelta = 0;
            AdjustmentReason = string.Empty;

            await LoadStockAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }
}
