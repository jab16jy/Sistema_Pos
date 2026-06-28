using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for the Loyalty module.
/// Manages balance display, transaction history, and point redemption at checkout.
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

    public LoyaltyViewModel(
        GetLoyaltyBalanceUseCase getBalance,
        RedeemLoyaltyPointsUseCase redeemPoints)
    {
        _getBalance = getBalance ?? throw new ArgumentNullException(nameof(getBalance));
        _redeemPoints = redeemPoints ?? throw new ArgumentNullException(nameof(redeemPoints));

        Transactions = [];
        LoadBalanceCommand = new RelayCommand(async _ => await LoadBalanceAsync().ConfigureAwait(true));
        RedeemCommand = new RelayCommand(async _ => await RedeemAsync().ConfigureAwait(true));
    }

    public ObservableCollection<LoyaltyTransactionResult> Transactions { get; }

    public ICommand LoadBalanceCommand { get; }

    public ICommand RedeemCommand { get; }

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
