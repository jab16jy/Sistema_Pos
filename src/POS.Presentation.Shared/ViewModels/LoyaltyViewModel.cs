using System.Collections.ObjectModel;
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
    private readonly GetLoyaltyBalanceUseCase _getBalance;
    private readonly RedeemLoyaltyPointsUseCase _redeemPoints;

    private int _balance;
    private CustomerResult? _selectedCustomer;
    private string _message = string.Empty;
    private bool _isLoading;
    private int _pointsToRedeem;

    // ── Presentation-only: rule editing ──
    private string _accumulationRate = "1 punto por cada $1.000 en compras";
    private string _redemptionRate = "100 puntos = $1.000 de descuento";
    private string _expirationPolicy = "12 meses sin compras";
    private string _checkoutUsageMode = "Permitir ganar y canjear";

    public LoyaltyViewModel(
        GetLoyaltyBalanceUseCase getBalance,
        RedeemLoyaltyPointsUseCase redeemPoints)
    {
        _getBalance = getBalance ?? throw new ArgumentNullException(nameof(getBalance));
        _redeemPoints = redeemPoints ?? throw new ArgumentNullException(nameof(redeemPoints));

        Transactions = [];
        Tiers =
        [
            new LoyaltyTierPresentation("Bronze", "0-499 pts", "Acumula normal. Sin descuento automatico.", "neutral"),
            new LoyaltyTierPresentation("Silver", "500-999 pts", "+10% de puntos en compras especiales.", "primary"),
            new LoyaltyTierPresentation("Gold", "1000+ pts", "Canje prioritario y campanas exclusivas.", "warning"),
        ];

        CampaignEntries =
        [
            new LoyaltyCampaignEntry("campaign", "Campana \"Semana de la piel\"", "Multiplica x2 los puntos en cosmeticos hasta 2026-07-07", "Activa", "earn"),
            new LoyaltyCampaignEntry("event_upcoming", "Puntos proximos a vencer", "18 clientes con vencimiento en los proximos 15 dias", "18", "redeem"),
            new LoyaltyCampaignEntry("analytics", "Impacto en ventas", "38% de las ventas del mes tuvieron cliente identificado", "+12%", "earn"),
        ];

        LoadBalanceCommand = new RelayCommand(async _ => await LoadBalanceAsync().ConfigureAwait(true));
        RedeemCommand = new RelayCommand(async _ => await RedeemAsync().ConfigureAwait(true));
        SaveRulesCommand = new RelayCommand(_ => Message = "Reglas guardadas");
        EditBenefitsCommand = new RelayCommand(_ => Message = "Editor de tiers no disponible en este entorno.");
    }

    public ObservableCollection<LoyaltyTransactionResult> Transactions { get; }
    public ObservableCollection<LoyaltyTierPresentation> Tiers { get; }
    public ObservableCollection<LoyaltyCampaignEntry> CampaignEntries { get; }

    public ICommand LoadBalanceCommand { get; }
    public ICommand RedeemCommand { get; }
    public ICommand SaveRulesCommand { get; }
    public ICommand EditBenefitsCommand { get; }

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

    // ── Presentation-only: program status card ──
    // These are static/placeholder values since no aggregate use case exposes program-wide metrics.
    // Flagged as a gap consistent with Phase 6's KPI placeholder pattern.

    public string TotalPointsIssued => "142.850";
    public string EnrolledCustomers => "247";
    public string CheckoutUsageRate => "38%";
    public string RedemptionsThisMonth => "12";

    // ── Presentation-only: rule editing ──

    public string AccumulationRate
    {
        get => _accumulationRate;
        set => SetProperty(ref _accumulationRate, value ?? string.Empty);
    }

    public string RedemptionRate
    {
        get => _redemptionRate;
        set => SetProperty(ref _redemptionRate, value ?? string.Empty);
    }

    public string ExpirationPolicy
    {
        get => _expirationPolicy;
        set => SetProperty(ref _expirationPolicy, value ?? string.Empty);
    }

    public string CheckoutUsageMode
    {
        get => _checkoutUsageMode;
        set => SetProperty(ref _checkoutUsageMode, value ?? string.Empty);
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
            Message = $"Redimidos {result.PointsRedeemed} puntos. Descuento aplicado: ${result.DiscountAmount:N0}.";
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
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Presentation-only tier display record.</summary>
public sealed record LoyaltyTierPresentation(
    string Name,
    string PointRange,
    string BenefitDescription,
    string ChipKind);

/// <summary>Presentation-only campaign/history entry.</summary>
public sealed record LoyaltyCampaignEntry(
    string IconKind,
    string Title,
    string Meta,
    string ValueLabel,
    string Kind);
