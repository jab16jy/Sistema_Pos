using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for the Loyalty module.
/// Manages program status display, rule editing, tier display, and campaign tracking.
/// Per the wireframe, day-to-day point earning/redemption happens in Clientes and Caja —
/// this module only configures rules, tiers, and program-wide tracking.
/// </summary>
public sealed class LoyaltyViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly GetLoyaltyBalanceUseCase _getBalance;
    private readonly RedeemLoyaltyPointsUseCase _redeemPoints;
    private readonly GetLoyaltyProgramOverviewUseCase? _getProgramOverview;
    private readonly SetLoyaltyRuleUseCase? _setLoyaltyRule;
    private readonly GetLoyaltyTiersUseCase? _getTiers;
    private readonly CreateLoyaltyTierUseCase? _createTier;
    private readonly UpdateLoyaltyTierUseCase? _updateTier;
    private readonly DeleteLoyaltyTierUseCase? _deleteTier;

    private int _balance;
    private CustomerResult? _selectedCustomer;
    private string _message = string.Empty;
    private bool _isLoading;
    private int _pointsToRedeem;
    private decimal _pointsPerCurrencyUnit = 0.001m;
    private decimal _redemptionValuePerPoint = 10m;
    private int _minimumRedeemPoints = 100;
    private string _ruleDescription = "1 punto por cada $1.000 vendidos; cada punto descuenta $10.";
    private string _totalPointsIssued = "0";
    private string _enrolledCustomers = "0";
    private string _checkoutUsageRate = "Según ventas con cliente";
    private string _redemptionsThisMonth = "0";
    private LoyaltyTierPresentation? _selectedTier;
    private string _tierName = string.Empty;
    private decimal _tierMinimumPoints;
    private decimal _tierDiscountPercent;
    private decimal _tierPointsMultiplier = 1m;

    public LoyaltyViewModel(
        GetLoyaltyBalanceUseCase getBalance,
        RedeemLoyaltyPointsUseCase redeemPoints,
        GetLoyaltyProgramOverviewUseCase? getProgramOverview = null,
        SetLoyaltyRuleUseCase? setLoyaltyRule = null,
        GetLoyaltyTiersUseCase? getTiers = null,
        CreateLoyaltyTierUseCase? createTier = null,
        UpdateLoyaltyTierUseCase? updateTier = null,
        DeleteLoyaltyTierUseCase? deleteTier = null)
    {
        _getBalance = getBalance ?? throw new ArgumentNullException(nameof(getBalance));
        _redeemPoints = redeemPoints ?? throw new ArgumentNullException(nameof(redeemPoints));
        _getProgramOverview = getProgramOverview;
        _setLoyaltyRule = setLoyaltyRule;
        _getTiers = getTiers;
        _createTier = createTier;
        _updateTier = updateTier;
        _deleteTier = deleteTier;

        Transactions = [];
        ProgramTransactions = [];
        Tiers = [];

        LoadBalanceCommand = new RelayCommand(_ => RunSafe(LoadBalanceAsync(), ex => Message = $"Error al consultar saldo: {ex.Message}"));
        RedeemCommand = new RelayCommand(_ => RunSafe(RedeemAsync(), ex => Message = $"Error al redimir puntos: {ex.Message}"));
        LoadProgramCommand = new RelayCommand(_ => RunSafe(LoadProgramAsync(), ex => Message = $"Error al cargar puntos: {ex.Message}"));
        SaveRulesCommand = new RelayCommand(_ => RunSafe(SaveRulesAsync(), ex => Message = $"Error al guardar reglas: {ex.Message}"));
        LoadTiersCommand = new RelayCommand(_ => RunSafe(LoadTiersAsync(), ex => Message = $"Error al cargar tiers: {ex.Message}"));
        SaveTierCommand = new RelayCommand(_ => RunSafe(SaveTierAsync(), ex => Message = $"Error al guardar tier: {ex.Message}"));
        DeleteTierCommand = new RelayCommand(_ => RunSafe(DeleteTierAsync(), ex => Message = $"Error al eliminar tier: {ex.Message}"));
        NewTierCommand = new RelayCommand(_ => BeginNewTier());

        if (_getProgramOverview is not null)
        {
            RunSafe(LoadProgramAsync(), ex => Message = $"Error al cargar puntos: {ex.Message}");
        }

        if (_getTiers is not null)
        {
            RunSafe(LoadTiersAsync(), ex => Message = $"Error al cargar tiers: {ex.Message}");
        }
    }

    public ObservableCollection<LoyaltyTransactionResult> Transactions { get; }
    public ObservableCollection<LoyaltyTierPresentation> Tiers { get; }
    public ObservableCollection<LoyaltyProgramTransactionEntry> ProgramTransactions { get; }

    public ICommand LoadBalanceCommand { get; }
    public ICommand RedeemCommand { get; }
    public ICommand LoadProgramCommand { get; }
    public ICommand SaveRulesCommand { get; }
    public ICommand LoadTiersCommand { get; }
    public ICommand SaveTierCommand { get; }
    public ICommand DeleteTierCommand { get; }
    public ICommand NewTierCommand { get; }

    public int Balance
    {
        get => _balance;
        private set => SetProperty(ref _balance, value);
    }

    public CustomerResult? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    public int PointsToRedeem
    {
        get => _pointsToRedeem;
        set => SetProperty(ref _pointsToRedeem, value);
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

    public string TotalPointsIssued
    {
        get => _totalPointsIssued;
        private set => SetProperty(ref _totalPointsIssued, value);
    }

    public string EnrolledCustomers
    {
        get => _enrolledCustomers;
        private set => SetProperty(ref _enrolledCustomers, value);
    }

    public string CheckoutUsageRate
    {
        get => _checkoutUsageRate;
        private set => SetProperty(ref _checkoutUsageRate, value);
    }

    public string RedemptionsThisMonth
    {
        get => _redemptionsThisMonth;
        private set => SetProperty(ref _redemptionsThisMonth, value);
    }

    public decimal PointsPerCurrencyUnit
    {
        get => _pointsPerCurrencyUnit;
        set
        {
            if (SetProperty(ref _pointsPerCurrencyUnit, value))
            {
                OnPropertyChanged(nameof(AccumulationRate));
            }
        }
    }

    public decimal RedemptionValuePerPoint
    {
        get => _redemptionValuePerPoint;
        set
        {
            if (SetProperty(ref _redemptionValuePerPoint, value))
            {
                OnPropertyChanged(nameof(RedemptionRate));
            }
        }
    }

    public int MinimumRedeemPoints
    {
        get => _minimumRedeemPoints;
        set => SetProperty(ref _minimumRedeemPoints, value);
    }

    public string RuleDescription
    {
        get => _ruleDescription;
        set => SetProperty(ref _ruleDescription, value ?? string.Empty);
    }

    public string AccumulationRate => $"{PointsPerCurrencyUnit.ToString("0.####", ColombianCulture)} punto(s) por cada $1 vendido";

    public string RedemptionRate => $"Cada punto descuenta ${RedemptionValuePerPoint.ToString("N0", ColombianCulture)}";

    public string CheckoutUsageMode => "Caja acumula y canjea usando la regla activa guardada.";

    public string ExpirationPolicy => "Sin vencimiento configurado en esta versión.";

    public LoyaltyTierPresentation? SelectedTier
    {
        get => _selectedTier;
        set
        {
            if (SetProperty(ref _selectedTier, value) && value is not null)
            {
                TierName = value.Name;
                TierMinimumPoints = value.MinPoints;
                TierDiscountPercent = value.DiscountPercent;
                TierPointsMultiplier = value.PointsMultiplier;
            }
        }
    }

    public string TierName
    {
        get => _tierName;
        set => SetProperty(ref _tierName, value ?? string.Empty);
    }

    public decimal TierMinimumPoints
    {
        get => _tierMinimumPoints;
        set => SetProperty(ref _tierMinimumPoints, value);
    }

    public decimal TierDiscountPercent
    {
        get => _tierDiscountPercent;
        set => SetProperty(ref _tierDiscountPercent, value);
    }

    public decimal TierPointsMultiplier
    {
        get => _tierPointsMultiplier;
        set => SetProperty(ref _tierPointsMultiplier, value);
    }

    public async Task LoadProgramAsync(CancellationToken cancellationToken = default)
    {
        if (_getProgramOverview is null)
        {
            Message = "El resumen real de puntos no está disponible en este entorno.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var overview = await _getProgramOverview.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            ApplyOverview(overview);
            await LoadTiersAsync(cancellationToken).ConfigureAwait(true);
            Message = "Reglas y métricas de puntos cargadas desde la base de datos.";
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar puntos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadTiersAsync(CancellationToken cancellationToken = default)
    {
        if (_getTiers is null)
        {
            return;
        }

        var tiers = await _getTiers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var selectedId = SelectedTier?.Id;
        Tiers.Clear();
        foreach (var tier in tiers)
        {
            Tiers.Add(LoyaltyTierPresentation.From(tier));
        }

        SelectedTier = selectedId is { } id
            ? Tiers.FirstOrDefault(tier => tier.Id == id)
            : Tiers.FirstOrDefault();
    }

    public async Task SaveTierAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(TierName) || TierMinimumPoints < 0 || TierDiscountPercent is < 0 or > 100 || TierPointsMultiplier <= 0)
        {
            Message = "Revisa el tier: nombre obligatorio, mínimo no negativo, descuento entre 0 y 100 y multiplicador mayor a cero.";
            return;
        }

        if (SelectedTier is null && _createTier is null || SelectedTier is not null && _updateTier is null)
        {
            Message = "La edición de tiers no está disponible en este entorno.";
            return;
        }

        IsLoading = true;
        try
        {
            if (SelectedTier is null)
            {
                await _createTier!.ExecuteAsync(new CreateLoyaltyTierRequest(TierName, TierMinimumPoints, TierDiscountPercent, TierPointsMultiplier), cancellationToken).ConfigureAwait(true);
                Message = "Tier creado correctamente.";
            }
            else
            {
                await _updateTier!.ExecuteAsync(new UpdateLoyaltyTierRequest(SelectedTier.Id, TierName, TierMinimumPoints, TierDiscountPercent, TierPointsMultiplier), cancellationToken).ConfigureAwait(true);
                Message = "Tier actualizado correctamente.";
            }

            await LoadTiersAsync(cancellationToken).ConfigureAwait(true);
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

    public async Task DeleteTierAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedTier is null)
        {
            Message = "Selecciona un tier para eliminarlo.";
            return;
        }

        if (_deleteTier is null)
        {
            Message = "La edición de tiers no está disponible en este entorno.";
            return;
        }

        IsLoading = true;
        try
        {
            await _deleteTier.ExecuteAsync(SelectedTier.Id, cancellationToken).ConfigureAwait(true);
            BeginNewTier();
            await LoadTiersAsync(cancellationToken).ConfigureAwait(true);
            Message = "Tier eliminado correctamente.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void BeginNewTier()
    {
        SelectedTier = null;
        TierName = string.Empty;
        TierMinimumPoints = 0;
        TierDiscountPercent = 0;
        TierPointsMultiplier = 1m;
    }

    public async Task SaveRulesAsync(CancellationToken cancellationToken = default)
    {
        if (_setLoyaltyRule is null)
        {
            Message = "No hay caso de uso disponible para guardar reglas de puntos.";
            return;
        }

        if (PointsPerCurrencyUnit <= 0m || RedemptionValuePerPoint <= 0m || MinimumRedeemPoints < 0)
        {
            Message = "Revisa la regla: acumulación y redención deben ser mayores a cero, y el mínimo no puede ser negativo.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var saved = await _setLoyaltyRule.ExecuteAsync(
                new SetLoyaltyRuleRequest(PointsPerCurrencyUnit, RedemptionValuePerPoint, MinimumRedeemPoints, RuleDescription),
                cancellationToken).ConfigureAwait(true);

            ApplyRule(saved);
            Message = "Reglas de puntos guardadas en la base de datos.";

            if (_getProgramOverview is not null)
            {
                var overview = await _getProgramOverview.ExecuteAsync(cancellationToken).ConfigureAwait(true);
                ApplyOverview(overview);
            }
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
            Message = $"Error al guardar reglas: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadBalanceAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCustomer is null)
        {
            Message = "Selecciona un cliente para consultar su saldo de puntos.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var result = await _getBalance.ExecuteAsync(SelectedCustomer.Id, cancellationToken: cancellationToken).ConfigureAwait(true);

            if (result is null)
            {
                Balance = 0;
                Transactions.Clear();
                Message = "Este cliente no tiene cuenta de puntos activa.";
                return;
            }

            Balance = result.Balance;
            Transactions.Clear();
            foreach (var tx in result.RecentTransactions)
            {
                Transactions.Add(tx);
            }

            Message = $"Saldo: {result.Balance} puntos. Cliente: {SelectedCustomer.FullName}.";
        }
        catch (Exception ex)
        {
            Message = $"Error al consultar saldo: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RedeemAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCustomer is null)
        {
            Message = "Selecciona un cliente antes de redimir puntos.";
            return;
        }

        if (PointsToRedeem <= 0)
        {
            Message = "Ingresa una cantidad de puntos mayor a cero.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            var request = new RedeemPointsRequest(SelectedCustomer.Id, PointsToRedeem, SaleTotal: 0m);
            var result = await _redeemPoints.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            Message = $"Redimidos {result.PointsRedeemed} puntos. Descuento aplicado: ${result.DiscountAmount.ToString("N0", ColombianCulture)}.";
            PointsToRedeem = 0;

            // Reload balance after redemption
            await LoadBalanceAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al redimir puntos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyOverview(LoyaltyProgramOverviewResult overview)
    {
        TotalPointsIssued = overview.TotalPointsIssued.ToString("N0");
        EnrolledCustomers = overview.EnrolledCustomers.ToString("N0");
        RedemptionsThisMonth = overview.RedemptionsThisMonth.ToString("N0");
        CheckoutUsageRate = overview.EnrolledCustomers == 0
            ? "Sin clientes inscritos"
            : "Basado en ventas con cliente";

        if (overview.ActiveRule is not null)
        {
            ApplyRule(overview.ActiveRule);
        }

        ProgramTransactions.Clear();
        foreach (var transaction in overview.RecentTransactions)
        {
            ProgramTransactions.Add(new LoyaltyProgramTransactionEntry(
                transaction.Type.Equals("Redeemed", StringComparison.OrdinalIgnoreCase) ? "Canje de puntos" : "Puntos acumulados",
                BuildTransactionMeta(transaction),
                transaction.Points > 0 ? $"+{transaction.Points.ToString("N0", ColombianCulture)}" : transaction.Points.ToString("N0", ColombianCulture),
                transaction.Points >= 0 ? "earn" : "redeem",
                transaction.CustomerName,
                transaction.InvoiceNumber,
                transaction.CreatedAt,
                transaction.Description,
                transaction.ResponsibleUser));
        }

        if (ProgramTransactions.Count == 0)
        {
            ProgramTransactions.Add(new LoyaltyProgramTransactionEntry(
                "Sin movimientos todavía",
                "Cuando Caja acumule o canjee puntos, aparecerá aquí con datos reales.",
                "0",
                "neutral",
                null,
                null,
                null,
                null,
                null));
        }
    }

    private static string BuildTransactionMeta(LoyaltyTransactionResult transaction)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(transaction.CustomerName)) details.Add($"Cliente: {transaction.CustomerName}");
        if (!string.IsNullOrWhiteSpace(transaction.InvoiceNumber)) details.Add($"Factura: {transaction.InvoiceNumber}");
        details.Add(transaction.CreatedAt.ToLocalTime().ToString("g"));
        if (!string.IsNullOrWhiteSpace(transaction.ResponsibleUser)) details.Add($"Responsable: {transaction.ResponsibleUser}");
        return string.Join(" · ", details);
    }

    private void ApplyRule(LoyaltyRuleResult rule)
    {
        PointsPerCurrencyUnit = rule.PointsPerCurrencyUnit;
        RedemptionValuePerPoint = rule.RedemptionValuePerPoint;
        MinimumRedeemPoints = rule.MinimumRedeemPoints;
        RuleDescription = string.IsNullOrWhiteSpace(rule.Description)
            ? "Regla activa sin descripción."
            : rule.Description;
        OnPropertyChanged(nameof(AccumulationRate));
        OnPropertyChanged(nameof(RedemptionRate));
    }
}

/// <summary>Presentation-only tier display record.</summary>
public sealed record LoyaltyTierPresentation(
    Guid Id,
    string Name,
    decimal MinPoints,
    decimal DiscountPercent,
    decimal PointsMultiplier)
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public string PointRange => $"Desde {MinPoints.ToString("N0", ColombianCulture)} pts";
    public string BenefitDescription =>
        $"{DiscountPercent.ToString("0.##", ColombianCulture)}% descuento · {PointsMultiplier.ToString("0.##", ColombianCulture)}x puntos";
    public string ChipKind => DiscountPercent > 0 ? "warning" : PointsMultiplier > 1 ? "primary" : "neutral";

    public static LoyaltyTierPresentation From(LoyaltyTierResult tier)
        => new(tier.Id, tier.Name, tier.MinPoints, tier.DiscountPercent, tier.PointsMultiplier);
}

/// <summary>Presentation row for real persisted program movements.</summary>
public sealed record LoyaltyProgramTransactionEntry(
    string Title,
    string Meta,
    string ValueLabel,
    string Kind,
    string? CustomerName,
    string? InvoiceNumber,
    DateTimeOffset? Date,
    string? Reason,
    string? ResponsibleUser);
