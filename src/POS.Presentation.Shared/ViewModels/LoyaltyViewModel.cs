using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
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
    private readonly GetLoyaltyTierLimitsUseCase? _getTierLimits;

    private int _balance;
    private CustomerResult? _selectedCustomer;
    private string _message = string.Empty;
    private bool _isLoading;
    private int _pointsToRedeem;
    private decimal _pointsPerCurrencyUnit = 0.001m;
    private decimal _redemptionValuePerPoint = 10m;
    private int _minimumRedeemPoints = 100;
    private string _accumulationCurrencyAmountText = "1.000";
    private string _redemptionValuePerPointText = "10";
    private string _minimumRedeemPointsText = "100";
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
    private string _tierPointsMultiplierText = "1";
    private string _tierColor = "#64748B";
    private LoyaltyTierTextForeground _tierTextForeground = LoyaltyTierTextForeground.White;
    private string _tierBenefits = string.Empty;
    private bool _tierAllowsRedemption;
    private decimal? _pendingTierPointsMultiplier = 1m;
    private bool _isTierPointsMultiplierTextValid = true;
    private LoyaltyPane _pane = LoyaltyPane.TiersList;
    private decimal _maximumTierDiscountPercent = BusinessSettings.DefaultMaximumCashierDiscountPercent;
    private decimal _maximumTierPointsMultiplier = BusinessSettings.DefaultMaximumPointsMultiplier;


    public LoyaltyViewModel(
        GetLoyaltyBalanceUseCase getBalance,
        RedeemLoyaltyPointsUseCase redeemPoints,
        GetLoyaltyProgramOverviewUseCase? getProgramOverview = null,
        SetLoyaltyRuleUseCase? setLoyaltyRule = null,
        GetLoyaltyTiersUseCase? getTiers = null,
        CreateLoyaltyTierUseCase? createTier = null,
        UpdateLoyaltyTierUseCase? updateTier = null,
        DeleteLoyaltyTierUseCase? deleteTier = null,
        GetLoyaltyTierLimitsUseCase? getTierLimits = null,
        LoyaltyBenefitsViewModel? benefitsViewModel = null)
    {
        BenefitsViewModel = benefitsViewModel;
        _getBalance = getBalance ?? throw new ArgumentNullException(nameof(getBalance));
        _redeemPoints = redeemPoints ?? throw new ArgumentNullException(nameof(redeemPoints));
        _getProgramOverview = getProgramOverview;
        _setLoyaltyRule = setLoyaltyRule;
        _getTiers = getTiers;
        _createTier = createTier;
        _updateTier = updateTier;
        _deleteTier = deleteTier;
        _getTierLimits = getTierLimits;

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
        ShowTiersListCommand = new RelayCommand(_ => CloseTierEditor());
        ShowTierEditorCommand = new RelayCommand(_ => IsTierEditorOpen = true);
        // Guarded inside the action too: RelayCommand.Execute does not consult CanExecute, and the
        // pane must never open without a view model to fill it.
        ShowBenefitsCommand = new RelayCommand(
            _ =>
            {
                if (BenefitsViewModel is not null)
                {
                    ShowPane(LoyaltyPane.Benefits);
                }
            },
            _ => BenefitsViewModel is not null);

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
    public IReadOnlyList<LoyaltyTierTextForeground> TierTextForegroundOptions { get; } = [LoyaltyTierTextForeground.Black, LoyaltyTierTextForeground.White];

    public ICommand LoadBalanceCommand { get; }
    public ICommand RedeemCommand { get; }
    public ICommand LoadProgramCommand { get; }
    public ICommand SaveRulesCommand { get; }
    public ICommand LoadTiersCommand { get; }
    public ICommand SaveTierCommand { get; }
    public ICommand DeleteTierCommand { get; }
    public ICommand NewTierCommand { get; }
    public ICommand ShowTiersListCommand { get; }
    public ICommand ShowTierEditorCommand { get; }
    public ICommand ShowBenefitsCommand { get; }

    /// <summary>
    /// Benefit catalogue pane. Null when the catalogue use cases could not be resolved, in which
    /// case the tab is hidden rather than opening an empty pane.
    /// </summary>
    public LoyaltyBenefitsViewModel? BenefitsViewModel { get; }

    public bool HasBenefitsModule => BenefitsViewModel is not null;

    /// <summary>
    /// Which pane column 2 shows. They swap rather than stack so none gets squeezed into a sliver,
    /// and exactly one is always open.
    /// </summary>
    public bool IsTierEditorOpen
    {
        get => _pane == LoyaltyPane.TierEditor;
        set => ShowPane(value ? LoyaltyPane.TierEditor : LoyaltyPane.TiersList);
    }

    public bool IsTiersListOpen => _pane == LoyaltyPane.TiersList;

    /// <summary>Benefit catalogue pane. Its own state lives in <see cref="BenefitsViewModel"/>.</summary>
    public bool IsBenefitsOpen => _pane == LoyaltyPane.Benefits;

    private void ShowPane(LoyaltyPane pane)
    {
        if (_pane == pane)
        {
            return;
        }

        _pane = pane;
        OnPropertyChanged(nameof(IsTierEditorOpen));
        OnPropertyChanged(nameof(IsTiersListOpen));
        OnPropertyChanged(nameof(IsBenefitsOpen));
    }

    private enum LoyaltyPane
    {
        TiersList,
        TierEditor,
        Benefits
    }

    /// <summary>Ceiling a tier's discount may reach — the same one that caps a manual cashier discount.</summary>
    public decimal MaximumTierDiscountPercent
    {
        get => _maximumTierDiscountPercent;
        private set
        {
            if (SetProperty(ref _maximumTierDiscountPercent, value))
            {
                OnPropertyChanged(nameof(TierDiscountHint));
            }
        }
    }

    public decimal MaximumTierPointsMultiplier
    {
        get => _maximumTierPointsMultiplier;
        private set
        {
            if (SetProperty(ref _maximumTierPointsMultiplier, value))
            {
                OnPropertyChanged(nameof(TierPointsMultiplierHint));
            }
        }
    }

    public string TierDiscountHint =>
        $"Máximo permitido: {_maximumTierDiscountPercent.ToString("0.##", ColombianCulture)}% (definido en Configuración).";

    public string TierPointsMultiplierHint =>
        $"Máximo permitido: {_maximumTierPointsMultiplier.ToString("0.##", ColombianCulture)}x (definido en Configuración).";

    public bool HasTiers => Tiers.Count > 0;

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

    public string AccumulationCurrencyAmountText
    {
        get => _accumulationCurrencyAmountText;
        set
        {
            if (SetProperty(ref _accumulationCurrencyAmountText, value ?? string.Empty))
            {
                if (TryParseCurrencyAmount(value, out decimal amount) && amount > 0m)
                {
                    _pointsPerCurrencyUnit = 1m / amount;
                }
                OnPropertyChanged(nameof(PointsPerCurrencyUnit));
                OnPropertyChanged(nameof(AccumulationRate));
                OnPropertyChanged(nameof(AccumulationExampleHint));
                OnPropertyChanged(nameof(IsAccumulationCurrencyAmountValid));
            }
        }
    }

    public decimal PointsPerCurrencyUnit
    {
        get => _pointsPerCurrencyUnit;
        set
        {
            if (SetProperty(ref _pointsPerCurrencyUnit, value))
            {
                if (value > 0m)
                {
                    decimal amount = Math.Round(1m / value);
                    _accumulationCurrencyAmountText = amount.ToString("N0", ColombianCulture);
                    OnPropertyChanged(nameof(AccumulationCurrencyAmountText));
                }
                OnPropertyChanged(nameof(AccumulationRate));
                OnPropertyChanged(nameof(AccumulationExampleHint));
                OnPropertyChanged(nameof(IsAccumulationCurrencyAmountValid));
            }
        }
    }

    public string RedemptionValuePerPointText
    {
        get => _redemptionValuePerPointText;
        set
        {
            if (SetProperty(ref _redemptionValuePerPointText, value ?? string.Empty))
            {
                if (TryParseCurrencyAmount(value, out decimal val) && val > 0m)
                {
                    _redemptionValuePerPoint = val;
                }
                OnPropertyChanged(nameof(RedemptionValuePerPoint));
                OnPropertyChanged(nameof(RedemptionRate));
                OnPropertyChanged(nameof(RedemptionExampleHint));
                OnPropertyChanged(nameof(IsRedemptionValueValid));
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
                _redemptionValuePerPointText = value.ToString("N0", ColombianCulture);
                OnPropertyChanged(nameof(RedemptionValuePerPointText));
                OnPropertyChanged(nameof(RedemptionRate));
                OnPropertyChanged(nameof(RedemptionExampleHint));
                OnPropertyChanged(nameof(IsRedemptionValueValid));
            }
        }
    }

    public string MinimumRedeemPointsText
    {
        get => _minimumRedeemPointsText;
        set
        {
            if (SetProperty(ref _minimumRedeemPointsText, value ?? string.Empty))
            {
                if (TryParseNonNegativeInt(value, out int minPoints))
                {
                    _minimumRedeemPoints = minPoints;
                }
                OnPropertyChanged(nameof(MinimumRedeemPoints));
                OnPropertyChanged(nameof(IsMinimumRedeemPointsValid));
            }
        }
    }

    public int MinimumRedeemPoints
    {
        get => _minimumRedeemPoints;
        set
        {
            if (SetProperty(ref _minimumRedeemPoints, value))
            {
                _minimumRedeemPointsText = value.ToString();
                OnPropertyChanged(nameof(MinimumRedeemPointsText));
                OnPropertyChanged(nameof(IsMinimumRedeemPointsValid));
            }
        }
    }

    public string RuleDescription
    {
        get => _ruleDescription;
        set => SetProperty(ref _ruleDescription, value ?? string.Empty);
    }

    public string AccumulationRate => TryParseCurrencyAmount(AccumulationCurrencyAmountText, out decimal amount) && amount > 0m
        ? $"1 punto por cada ${amount.ToString("N0", ColombianCulture)} vendidos"
        : "Monto inválido para acumulación";

    public string AccumulationExampleHint => TryParseCurrencyAmount(AccumulationCurrencyAmountText, out decimal amount) && amount > 0m
        ? $"Ejemplo: Una compra de ${(amount * 10m).ToString("N0", ColombianCulture)} otorga 10 puntos básicos."
        : "Ingresa un monto en pesos mayor a cero (ej. 8.000).";

    public string RedemptionRate => TryParseCurrencyAmount(RedemptionValuePerPointText, out decimal value) && value > 0m
        ? $"Cada 1 punto descuenta ${value.ToString("N0", ColombianCulture)}"
        : "Valor de canje inválido";

    public string RedemptionExampleHint => TryParseCurrencyAmount(RedemptionValuePerPointText, out decimal value) && value > 0m
        ? $"Ejemplo: 100 puntos otorgan ${(value * 100m).ToString("N0", ColombianCulture)} de descuento."
        : "Ingresa un valor en pesos mayor a cero (ej. 10).";

    public bool IsAccumulationCurrencyAmountValid => TryParseCurrencyAmount(AccumulationCurrencyAmountText, out decimal amount) && amount > 0m;
    public bool IsRedemptionValueValid => TryParseCurrencyAmount(RedemptionValuePerPointText, out decimal val) && val > 0m;
    public bool IsMinimumRedeemPointsValid => TryParseNonNegativeInt(MinimumRedeemPointsText, out int minPoints);

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
                TierColor = value.Color;
                TierTextForeground = value.TextForeground;
                TierBenefits = value.Benefits;
                TierAllowsRedemption = value.AllowsRedemption;
            }

            OnPropertyChanged(nameof(SelectedTierAssignedBenefits));
            OnPropertyChanged(nameof(HasSelectedTierAssignedBenefits));
        }
    }

    public IReadOnlyList<LoyaltyBenefitPresentation> SelectedTierAssignedBenefits
    {
        get
        {
            if (SelectedTier is null || BenefitsViewModel is null)
            {
                return [];
            }
            return BenefitsViewModel.Benefits
                .Where(b => b.RequiredTierId == SelectedTier.Id && b.IsActive)
                .ToList();
        }
    }

    public bool HasSelectedTierAssignedBenefits => SelectedTierAssignedBenefits.Count > 0;

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

    /// <summary>Free-text benefits unlocked at this tier, one per line.</summary>
    public string TierBenefits
    {
        get => _tierBenefits;
        set => SetProperty(ref _tierBenefits, value ?? string.Empty);
    }

    /// <summary>When false, customers on this tier are not offered point redemption at checkout.</summary>
    public bool TierAllowsRedemption
    {
        get => _tierAllowsRedemption;
        set => SetProperty(ref _tierAllowsRedemption, value);
    }

    public decimal TierDiscountPercent
    {
        get => _tierDiscountPercent;
        set => SetProperty(ref _tierDiscountPercent, value);
    }

    public decimal TierPointsMultiplier
    {
        get => _tierPointsMultiplier;
        set
        {
            if (SetProperty(ref _tierPointsMultiplier, value))
            {
                _pendingTierPointsMultiplier = value;
                _isTierPointsMultiplierTextValid = value > 0m;
                _tierPointsMultiplierText = value.ToString("0.##", ColombianCulture);
                OnPropertyChanged(nameof(TierPointsMultiplierText));
                OnPropertyChanged(nameof(IsTierPointsMultiplierTextValid));
            }
        }
    }

    public string TierPointsMultiplierText
    {
        get => _tierPointsMultiplierText;
        set
        {
            if (SetProperty(ref _tierPointsMultiplierText, value ?? string.Empty))
            {
                _isTierPointsMultiplierTextValid = TryParsePositiveDecimal(value, out decimal parsedValue);
                _pendingTierPointsMultiplier = _isTierPointsMultiplierTextValid ? parsedValue : null;
                if (_isTierPointsMultiplierTextValid)
                    _tierPointsMultiplier = parsedValue;
                OnPropertyChanged(nameof(TierPointsMultiplier));
                OnPropertyChanged(nameof(IsTierPointsMultiplierTextValid));
            }
        }
    }

    public bool IsTierPointsMultiplierTextValid => _isTierPointsMultiplierTextValid;

    public LoyaltyTierTextForeground TierTextForeground
    {
        get => _tierTextForeground;
        set
        {
            if (SetProperty(ref _tierTextForeground, value))
                OnPropertyChanged(nameof(TierTextForegroundPreviewBrush));
        }
    }

    public string TierColor
    {
        get => _tierColor;
        set
        {
            if (SetProperty(ref _tierColor, value ?? "#64748B"))
            {
                OnPropertyChanged(nameof(TierColorPreviewBrush));
            }
        }
    }

    public System.Windows.Media.Brush TierColorPreviewBrush => TryParseBrush(TierColor);
    public System.Windows.Media.Brush TierTextForegroundPreviewBrush => TierTextForeground == LoyaltyTierTextForeground.Black
        ? System.Windows.Media.Brushes.Black
        : System.Windows.Media.Brushes.White;

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

        if (_getTierLimits is not null)
        {
            LoyaltyTierLimitsResult limits = await _getTierLimits.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            MaximumTierDiscountPercent = limits.MaximumDiscountPercent;
            MaximumTierPointsMultiplier = limits.MaximumPointsMultiplier;
        }

        var tiers = await _getTiers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var selectedId = SelectedTier?.Id;

        Tiers.Clear();
        foreach (var tier in tiers)
        {
            List<string> tierBenefits = [];
            if (BenefitsViewModel is not null)
            {
                tierBenefits = BenefitsViewModel.Benefits
                    .Where(b => b.IsActive && b.RequiredTierId == tier.Id)
                    .Select(b => b.Name)
                    .ToList();
            }

            Tiers.Add(LoyaltyTierPresentation.From(tier, tierBenefits.Count > 0 ? tierBenefits : null));
        }

        // Only re-attach an existing selection; never pick one for the user. An auto-selected first
        // tier would make the list open with a row highlighted and turn "Configuración" into "edit
        // that tier" instead of "create a new one".
        SelectedTier = selectedId is { } id ? Tiers.FirstOrDefault(tier => tier.Id == id) : null;

        OnPropertyChanged(nameof(HasTiers));

        // With no tier to look at, the list pane would be an empty box — open the editor so the
        // first thing the business sees is how to create one.
        if (Tiers.Count == 0)
        {
            IsTierEditorOpen = true;
        }
    }

    public async Task SaveTierAsync(CancellationToken cancellationToken = default)
    {
        bool hasValidColor = !string.IsNullOrWhiteSpace(TierColor) && System.Text.RegularExpressions.Regex.IsMatch(TierColor, "^#[0-9A-Fa-f]{6}$");
        if (string.IsNullOrWhiteSpace(TierName) || TierMinimumPoints < 0 || TierDiscountPercent is < 0 or > 100 || !_isTierPointsMultiplierTextValid || _pendingTierPointsMultiplier is not > 0m || !hasValidColor)
        {
            Message = "Revisa el tier: nombre obligatorio, mínimo no negativo, descuento entre 0 y 100, multiplicador mayor a cero y color en formato #RRGGBB.";
            return;
        }

        // Same ceilings the use case enforces — checked here too so the editor says what is wrong
        // before a round trip, and keeps the entered values on screen.
        if (TierDiscountPercent > MaximumTierDiscountPercent)
        {
            Message = $"El descuento del nivel no puede superar el máximo configurado de {MaximumTierDiscountPercent.ToString("0.##", ColombianCulture)}%.";
            return;
        }

        if (_pendingTierPointsMultiplier.Value > MaximumTierPointsMultiplier)
        {
            Message = $"El multiplicador de puntos no puede superar el máximo configurado de {MaximumTierPointsMultiplier.ToString("0.##", ColombianCulture)}x.";
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
                await _createTier!.ExecuteAsync(new CreateLoyaltyTierRequest(TierName, TierMinimumPoints, TierDiscountPercent, _pendingTierPointsMultiplier.Value, TierColor, TierTextForeground, TierBenefits, TierAllowsRedemption), cancellationToken).ConfigureAwait(true);
                Message = "Tier creado correctamente.";
            }
            else
            {
                await _updateTier!.ExecuteAsync(new UpdateLoyaltyTierRequest(SelectedTier.Id, TierName, TierMinimumPoints, TierDiscountPercent, _pendingTierPointsMultiplier.Value, TierColor, TierTextForeground, TierBenefits, TierAllowsRedemption), cancellationToken).ConfigureAwait(true);
                Message = "Tier actualizado correctamente.";
            }

            await LoadTiersAsync(cancellationToken).ConfigureAwait(true);
            // Saved successfully: show the result in the list instead of leaving the form open.
            CloseTierEditor();
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
            ResetTierForm();
            await LoadTiersAsync(cancellationToken).ConfigureAwait(true);
            // Back to the list unless nothing is left to list.
            IsTierEditorOpen = Tiers.Count == 0;
            Message = "Tier eliminado correctamente.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Opens the editor on a blank tier.</summary>
    public void BeginNewTier()
    {
        ResetTierForm();
        IsTierEditorOpen = true;
    }

    /// <summary>
    /// Leaves the editor for the list. Clears the selection so the list comes back with nothing
    /// highlighted and reopening the editor starts a new tier — otherwise the last tier stays
    /// selected and "Configuración" silently reopens it instead of creating one.
    /// </summary>
    public void CloseTierEditor()
    {
        ResetTierForm();
        IsTierEditorOpen = false;
    }

    /// <summary>
    /// Resets the view state to its initial default when navigating to this screen.
    /// Shows the Tiers list, closes the editor, and clears the selected tier.
    /// </summary>
    public void ResetViewOnNavigate()
    {
        CloseTierEditor();
    }

    private void ResetTierForm()
    {
        SelectedTier = null;
        TierName = string.Empty;
        TierMinimumPoints = 0;
        TierDiscountPercent = 0;
        TierPointsMultiplier = 1m;
        TierColor = "#64748B";
        TierTextForeground = LoyaltyTierTextForeground.White;
        TierBenefits = string.Empty;
        TierAllowsRedemption = false;
    }

    private static System.Windows.Media.Brush TryParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return System.Windows.Media.Brushes.Gray;
        }

        try { return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!; }
        catch { return System.Windows.Media.Brushes.Gray; }
    }

    private static bool TryParseCurrencyAmount(string? input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string text = input.Trim().Replace("$", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (decimal.TryParse(text, NumberStyles.Currency | NumberStyles.Number, ColombianCulture, out decimal colVal) && colVal > 0m)
        {
            result = colVal;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Currency | NumberStyles.Number, CultureInfo.InvariantCulture, out decimal invVal) && invVal > 0m)
        {
            result = invVal;
            return true;
        }

        string normalized = text.Replace(".", string.Empty).Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal fallbackVal) && fallbackVal > 0m)
        {
            result = fallbackVal;
            return true;
        }

        return false;
    }

    private static bool TryParseNonNegativeInt(string? input, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string cleaned = input.Trim().Replace("$", string.Empty).Replace(".", string.Empty).Replace(",", string.Empty).Trim();
        return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= 0;
    }

    private static bool TryParsePositiveDecimal(string? input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string normalized = input.Trim().Replace(',', '.');
        return System.Text.RegularExpressions.Regex.IsMatch(normalized, "^\\d+(\\.\\d+)?$")
            && decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            && result > 0m;
    }

    public async Task SaveRulesAsync(CancellationToken cancellationToken = default)
    {
        if (_setLoyaltyRule is null)
        {
            Message = "No hay caso de uso disponible para guardar reglas de puntos.";
            return;
        }

        if (!TryParseCurrencyAmount(AccumulationCurrencyAmountText, out decimal accumulationAmount) || accumulationAmount <= 0m)
        {
            Message = "El monto en pesos para ganar 1 punto debe ser mayor a $0 (ej. 8.000).";
            return;
        }

        if (!TryParseCurrencyAmount(RedemptionValuePerPointText, out decimal redemptionVal) || redemptionVal <= 0m)
        {
            Message = "El valor en pesos por cada punto de canje debe ser mayor a $0 (ej. 10).";
            return;
        }

        if (!TryParseNonNegativeInt(MinimumRedeemPointsText, out int minPoints) || minPoints < 0)
        {
            Message = "El mínimo de puntos para canjear no puede ser negativo.";
            return;
        }

        IsLoading = true;
        Message = string.Empty;

        try
        {
            decimal pointsPerCurrencyUnit = 1m / accumulationAmount;
            var saved = await _setLoyaltyRule.ExecuteAsync(
                new SetLoyaltyRuleRequest(pointsPerCurrencyUnit, redemptionVal, minPoints, RuleDescription),
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
                TransactionTitle(transaction.Type),
                BuildTransactionMeta(transaction),
                transaction.Points > 0 ? $"+{transaction.Points.ToString("N0", ColombianCulture)}" : transaction.Points.ToString("N0", ColombianCulture),
                TransactionKind(transaction.Type),
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

    private static string TransactionTitle(string type) => type switch
    {
        "Earned" => "Puntos acumulados",
        "EarnReversed" => "Reversión de puntos acumulados",
        "Redeemed" => "Canje de puntos",
        "RedemptionRestored" => "Restauración de canje",
        _ => type
    };

    private static string TransactionKind(string type) => type switch
    {
        "Earned" or "RedemptionRestored" => "earn",
        "Redeemed" or "EarnReversed" => "redeem",
        _ => "neutral"
    };

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
    decimal PointsMultiplier,
    string Color,
    LoyaltyTierTextForeground TextForeground,
    string Benefits = "",
    bool AllowsRedemption = false,
    IReadOnlyList<string>? DynamicBenefitLines = null)
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public string PointRange => $"Desde {MinPoints.ToString("N0", ColombianCulture)} pts";

    /// <summary>The calculated benefits — the ones the engine applies automatically.</summary>
    public string BenefitDescription =>
        $"{DiscountPercent.ToString("0.##", ColombianCulture)}% descuento · {PointsMultiplier.ToString("0.##", ColombianCulture)}x puntos";

    /// <summary>The benefit lines defined for this tier from the catalogue or legacy text.</summary>
    public IReadOnlyList<string> BenefitLines => (DynamicBenefitLines is not null && DynamicBenefitLines.Count > 0)
        ? DynamicBenefitLines
        : POS.Domain.Entities.LoyaltyTier.SplitBenefits(Benefits);

    public bool HasBenefitLines => BenefitLines.Count > 0;

    public bool HasDiscount => DiscountPercent > 0m;
    public bool HasMultiplier => PointsMultiplier > 1m;

    public string RedemptionLabel => AllowsRedemption ? "Canje de puntos habilitado" : "Canje de puntos deshabilitado";

    public string ChipKind => DiscountPercent > 0 ? "warning" : PointsMultiplier > 1 ? "primary" : "neutral";
    public System.Windows.Media.Brush ColorBrush => TryParseBrush(Color);
    public System.Windows.Media.Brush TextForegroundBrush => TextForeground == LoyaltyTierTextForeground.Black ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;

    private static System.Windows.Media.Brush TryParseBrush(string hex)
    {
        try { return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!; }
        catch { return System.Windows.Media.Brushes.Gray; }
    }

    public static LoyaltyTierPresentation From(LoyaltyTierResult tier, IReadOnlyList<string>? benefitLines = null)
        => new(tier.Id, tier.Name, tier.MinPoints, tier.DiscountPercent, tier.PointsMultiplier, tier.Color, tier.TextForeground, tier.Benefits, tier.AllowsRedemption, benefitLines);
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
