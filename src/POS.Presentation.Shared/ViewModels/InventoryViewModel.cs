using System.Collections.ObjectModel;
using System.Globalization;
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
    private string _searchText = string.Empty;
    private string _statusFilter = string.Empty;
    private bool _isAdjustmentPositive = true;
    private decimal _adjustmentQuantity = 1m;
    private string _adjustmentNote = string.Empty;
    private string _movementTypeFilter = string.Empty;

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
        LoadStockCommand = new RelayCommand(_ => RunSafe(LoadStockAsync(), ex => Message = $"Error al cargar inventario: {ex.Message}"));
        LoadLowStockCommand = new RelayCommand(_ => RunSafe(LoadLowStockAsync(), ex => Message = $"Error al cargar stock bajo: {ex.Message}"));
        LoadMovementHistoryCommand = new RelayCommand(_ => RunSafe(LoadMovementHistoryAsync(), ex => Message = $"Error al cargar movimientos: {ex.Message}"), _ => SelectedProduct is not null);
        AdjustStockCommand = new RelayCommand(_ => RunSafe(AdjustStockAsync(), ex => Message = $"Error al ajustar stock: {ex.Message}"), _ => SelectedProduct is not null && !string.IsNullOrWhiteSpace(AdjustmentReason));
        ToggleSignCommand = new RelayCommand(_ => IsAdjustmentPositive = !IsAdjustmentPositive);
        ClearAdjustmentCommand = new RelayCommand(_ => ClearAdjustment());
        QuickAdjustCommand = new RelayCommand(param =>
        {
            if (param is StockLevelResult product)
            {
                SelectedProduct = product;
            }
        });
    }

    public ObservableCollection<StockLevelResult> StockLevels { get; } = [];

    public ObservableCollection<LowStockProductResult> LowStockProducts { get; } = [];

    public ObservableCollection<InventoryMovementResult> MovementHistory { get; } = [];

    public ICommand LoadStockCommand { get; }

    public ICommand LoadLowStockCommand { get; }

    public ICommand LoadMovementHistoryCommand { get; }

    public ICommand AdjustStockCommand { get; }

    public ICommand ToggleSignCommand { get; }

    public ICommand ClearAdjustmentCommand { get; }

    public ICommand QuickAdjustCommand { get; }

    // ── KPI properties (presentation-only) ──

    public int TotalSkus => StockLevels.Count;

    public int LowStockCount => StockLevels.Count(s => s.IsLowStock);

    public int OutOfStockCount => StockLevels.Count(s => s.CurrentStock == 0);

    // ── Search and filter ──

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(FilteredStockLevels));
            }
        }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                OnPropertyChanged(nameof(FilteredStockLevels));
            }
        }
    }

    public IReadOnlyList<StockLevelResult> FilteredStockLevels
    {
        get
        {
            IEnumerable<StockLevelResult> query = StockLevels;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                query = query.Where(s =>
                    s.ProductName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    (s.Barcode?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(_statusFilter))
            {
                query = _statusFilter switch
                {
                    "ok" => query.Where(s => s.CurrentStock > 0 && !s.IsLowStock),
                    "low" => query.Where(s => s.IsLowStock),
                    "out" => query.Where(s => s.CurrentStock == 0),
                    _ => query
                };
            }

            return query.ToList();
        }
    }

    // ── Adjustment panel ──

    public StockLevelResult? SelectedProduct
    {
        get => _selectedProduct;
        set => SetSelectedProduct(value, autoLoadHistory: true);
    }

    // autoLoadHistory: false lets LoadStockAsync restore the WPF-deselected ListBox
    // selection after a reload without racing its own explicit, awaited
    // LoadMovementHistoryAsync call with a second, fire-and-forget one from here.
    private void SetSelectedProduct(StockLevelResult? value, bool autoLoadHistory)
    {
        if (SetProperty(ref _selectedProduct, value))
        {
            MovementHistory.Clear();
            AdjustmentDelta = 0;
            AdjustmentReason = string.Empty;
            AdjustmentQuantity = 1m;
            IsAdjustmentPositive = true;
            AdjustmentNote = string.Empty;

            if (LoadMovementHistoryCommand is RelayCommand historyCommand)
            {
                historyCommand.RaiseCanExecuteChanged();
            }

            if (AdjustStockCommand is RelayCommand adjustCommand)
            {
                adjustCommand.RaiseCanExecuteChanged();
            }

            RecalculateAdjustmentPreview();
            OnPropertyChanged(nameof(SelectedProductChipLabel));
            OnPropertyChanged(nameof(HasSelectedProduct));

            if (autoLoadHistory && value is not null)
            {
                RunSafe(LoadMovementHistoryAsync(), ex => Message = $"Error al cargar movimientos: {ex.Message}");
            }
        }
    }

    public bool HasSelectedProduct => _selectedProduct is not null;

    public string SelectedProductChipLabel =>
        _selectedProduct is not null ? _selectedProduct.ProductName : "Selecciona un producto";

    public bool IsAdjustmentPositive
    {
        get => _isAdjustmentPositive;
        set
        {
            if (SetProperty(ref _isAdjustmentPositive, value))
            {
                RecalculateAdjustmentPreview();
            }
        }
    }

    public decimal AdjustmentQuantity
    {
        get => _adjustmentQuantity;
        set
        {
            if (SetProperty(ref _adjustmentQuantity, value))
            {
                RecalculateAdjustmentPreview();
            }
        }
    }

    public string AdjustmentNote
    {
        get => _adjustmentNote;
        set => SetProperty(ref _adjustmentNote, value);
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

    // ── Adjustment preview (presentation-only) ──

    public bool HasAdjustmentPreview => _selectedProduct is not null;

    public decimal AdjustmentPreviewCurrent => _selectedProduct?.CurrentStock ?? 0;

    public decimal AdjustmentPreviewNew
    {
        get
        {
            if (_selectedProduct is null) return 0;
            var delta = _isAdjustmentPositive ? _adjustmentQuantity : -_adjustmentQuantity;
            return Math.Max(0, _selectedProduct.CurrentStock + delta);
        }
    }

    public bool IsPreviewBelowMinimum =>
        _selectedProduct is not null && AdjustmentPreviewNew < _selectedProduct.MinimumStock;

    // ── Movements filter ──

    public string MovementTypeFilter
    {
        get => _movementTypeFilter;
        set
        {
            if (SetProperty(ref _movementTypeFilter, value))
            {
                OnPropertyChanged(nameof(FilteredMovements));
            }
        }
    }

    public IReadOnlyList<InventoryMovementResult> FilteredMovements
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_movementTypeFilter))
            {
                return MovementHistory.ToList();
            }

            return MovementHistory.Where(m =>
                m.Type.ToString().Equals(_movementTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
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
        try
        {
            var results = await _queryStock.ExecuteAsync(cancellationToken).ConfigureAwait(true);

            // Clearing StockLevels deselects the bound ListBox item, which pushes
            // SelectedProduct = null back through the TwoWay binding. Restore it by
            // id so a caller-awaited LoadMovementHistoryAsync right after this still
            // has a product to query.
            var selectedProductId = SelectedProduct?.ProductId;

            StockLevels.Clear();
            foreach (var stock in results)
            {
                StockLevels.Add(stock);
            }

            if (selectedProductId is Guid productId)
            {
                SetSelectedProduct(StockLevels.FirstOrDefault(stock => stock.ProductId == productId), autoLoadHistory: false);
            }

            LowStockProducts.Clear();
            OnPropertyChanged(nameof(TotalSkus));
            OnPropertyChanged(nameof(LowStockCount));
            OnPropertyChanged(nameof(OutOfStockCount));
            OnPropertyChanged(nameof(FilteredStockLevels));

            Message = StockLevels.Count == 0
                ? "No hay productos registrados en el sistema."
                : $"Mostrando {StockLevels.Count} producto(s). Selecciona uno para ver su historial de movimientos.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar inventario: {ex.Message}";
        }
    }

    public async Task LoadLowStockAsync(CancellationToken cancellationToken = default)
    {
        try
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
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar stock bajo: {ex.Message}";
        }
    }

    public async Task LoadMovementHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null)
        {
            Message = "Selecciona un producto para ver su historial de movimientos.";
            return;
        }

        try
        {
            var results = await _queryMovementHistory.ExecuteAsync(SelectedProduct.ProductId, cancellationToken).ConfigureAwait(true);
            MovementHistory.Clear();
            foreach (var movement in results)
            {
                // CreatedAt is stored as UTC; convert for display like AuditLogViewModel does.
                MovementHistory.Add(movement with { CreatedAt = movement.CreatedAt.ToLocalTime() });
            }

            OnPropertyChanged(nameof(FilteredMovements));

            Message = MovementHistory.Count == 0
                ? $"{SelectedProduct.ProductName} no tiene movimientos registrados."
                : $"Mostrando {MovementHistory.Count} movimiento(s) de {SelectedProduct.ProductName}.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar movimientos: {ex.Message}";
        }
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
            Message = "Selecciona un motivo";
            return;
        }

        try
        {
            var delta = _isAdjustmentPositive ? _adjustmentQuantity : -_adjustmentQuantity;

            var request = new AdjustStockRequest(
                SelectedProduct.ProductId,
                delta,
                AdjustmentReason,
                OperatorUserId);

            var result = await _adjustStock.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            Message = "Ajuste aplicado correctamente";

            // Reload movements before clearing: ClearAdjustment nulls SelectedProduct,
            // and LoadMovementHistoryAsync needs it to know which product to query.
            await LoadStockAsync(cancellationToken).ConfigureAwait(true);
            await LoadMovementHistoryAsync(cancellationToken).ConfigureAwait(true);
            ClearAdjustment();
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al ajustar stock: {ex.Message}";
        }
    }

    private void ClearAdjustment()
    {
        _selectedProduct = null;
        OnPropertyChanged(nameof(SelectedProduct));
        AdjustmentDelta = 0;
        AdjustmentReason = string.Empty;
        AdjustmentQuantity = 1m;
        IsAdjustmentPositive = true;
        AdjustmentNote = string.Empty;
        OnPropertyChanged(nameof(SelectedProductChipLabel));
        OnPropertyChanged(nameof(HasSelectedProduct));
        RecalculateAdjustmentPreview();

        if (LoadMovementHistoryCommand is RelayCommand historyCommand)
        {
            historyCommand.RaiseCanExecuteChanged();
        }

        if (AdjustStockCommand is RelayCommand adjustCommand)
        {
            adjustCommand.RaiseCanExecuteChanged();
        }
    }

    private void RecalculateAdjustmentPreview()
    {
        OnPropertyChanged(nameof(HasAdjustmentPreview));
        OnPropertyChanged(nameof(AdjustmentPreviewCurrent));
        OnPropertyChanged(nameof(AdjustmentPreviewNew));
        OnPropertyChanged(nameof(IsPreviewBelowMinimum));
    }
}
