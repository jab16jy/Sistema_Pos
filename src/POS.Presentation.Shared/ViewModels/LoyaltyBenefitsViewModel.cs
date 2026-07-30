using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// Benefit catalogue pane of the Loyalty module: what a reward is called and what it costs in points.
/// </summary>
/// <remarks>
/// A child view model rather than more properties on <see cref="LoyaltyViewModel"/>, which already
/// carries nine optional dependencies — the parent only needs to expose this one property.
/// </remarks>
public sealed class LoyaltyBenefitsViewModel : ViewModelBase
{
    private readonly GetLoyaltyBenefitsUseCase _getBenefits;
    private readonly SaveLoyaltyBenefitUseCase? _saveBenefit;
    private readonly DeleteLoyaltyBenefitUseCase? _deleteBenefit;

    private LoyaltyBenefitPresentation? _selectedBenefit;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _pointsCostText = string.Empty;
    private bool _isActive = true;
    private string _message = string.Empty;
    private bool _isLoading;

    public LoyaltyBenefitsViewModel(
        GetLoyaltyBenefitsUseCase getBenefits,
        SaveLoyaltyBenefitUseCase? saveBenefit = null,
        DeleteLoyaltyBenefitUseCase? deleteBenefit = null)
    {
        _getBenefits = getBenefits ?? throw new ArgumentNullException(nameof(getBenefits));
        _saveBenefit = saveBenefit;
        _deleteBenefit = deleteBenefit;

        Benefits = [];
        Benefits.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBenefits));

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar beneficios: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar beneficio: {ex.Message}"));
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar beneficio: {ex.Message}"));
        NewCommand = new RelayCommand(_ => ResetForm());

        RunSafe(LoadAsync(), ex => Message = $"Error al cargar beneficios: {ex.Message}");
    }

    public ObservableCollection<LoyaltyBenefitPresentation> Benefits { get; }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand NewCommand { get; }

    public bool HasBenefits => Benefits.Count > 0;

    /// <summary>Selecting a row loads it into the editor; clearing it starts a new benefit.</summary>
    public LoyaltyBenefitPresentation? SelectedBenefit
    {
        get => _selectedBenefit;
        set
        {
            if (!SetProperty(ref _selectedBenefit, value))
            {
                return;
            }

            if (value is not null)
            {
                Name = value.Name;
                Description = value.Description;
                PointsCostText = value.PointsCost.ToString("N0", LoyaltyBenefitPresentation.ColombianCulture);
                IsActive = value.IsActive;
            }

            OnPropertyChanged(nameof(IsEditingExistingBenefit));
        }
    }

    public bool IsEditingExistingBenefit => _selectedBenefit is not null;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Kept as text so a half-typed value never throws mid-keystroke, and so "1.000" typed the
    /// Colombian way parses as one thousand rather than one.
    /// </summary>
    public string PointsCostText
    {
        get => _pointsCostText;
        set => SetProperty(ref _pointsCostText, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<LoyaltyBenefitResult> benefits = await _getBenefits
                .ExecuteAsync(activeOnly: false, cancellationToken)
                .ConfigureAwait(true);

            Benefits.Clear();
            foreach (LoyaltyBenefitResult benefit in benefits)
            {
                Benefits.Add(LoyaltyBenefitPresentation.From(benefit));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_saveBenefit is null)
        {
            Message = "La edición del catálogo no está disponible.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            Message = "El beneficio necesita un nombre.";
            return;
        }

        if (!TryParsePointsCost(PointsCostText, out int pointsCost))
        {
            Message = "El costo en puntos debe ser un número entero mayor a cero.";
            return;
        }

        IsLoading = true;
        try
        {
            string savedName = Name.Trim();
            await _saveBenefit
                .ExecuteAsync(new SaveLoyaltyBenefitRequest(Name, pointsCost, Description, IsActive, SelectedBenefit?.Id), cancellationToken)
                .ConfigureAwait(true);

            await LoadAsync(cancellationToken).ConfigureAwait(true);
            ResetForm();
            Message = $"Beneficio \"{savedName}\" guardado.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (_deleteBenefit is null)
        {
            Message = "La edición del catálogo no está disponible.";
            return;
        }

        if (SelectedBenefit is null)
        {
            Message = "Selecciona un beneficio para eliminarlo.";
            return;
        }

        Guid benefitId = SelectedBenefit.Id;
        string benefitName = SelectedBenefit.Name;

        IsLoading = true;
        try
        {
            bool removed = await _deleteBenefit.ExecuteAsync(benefitId, cancellationToken).ConfigureAwait(true);

            await LoadAsync(cancellationToken).ConfigureAwait(true);
            ResetForm();
            Message = removed
                ? $"Beneficio \"{benefitName}\" eliminado."
                : $"\"{benefitName}\" ya tiene canjes registrados, así que se desactivó en lugar de eliminarse.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Clears the editor so the next save creates a benefit instead of updating one.</summary>
    public void ResetForm()
    {
        SelectedBenefit = null;
        Name = string.Empty;
        Description = string.Empty;
        PointsCostText = string.Empty;
        IsActive = true;
    }

    private static bool TryParsePointsCost(string? text, out int pointsCost)
    {
        pointsCost = 0;
        return !string.IsNullOrWhiteSpace(text)
            && int.TryParse(
                text.Trim(),
                NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                LoyaltyBenefitPresentation.ColombianCulture,
                out pointsCost)
            && pointsCost > 0;
    }
}

public sealed record LoyaltyBenefitPresentation(Guid Id, string Name, string Description, int PointsCost, bool IsActive)
{
    internal static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public string PointsCostLabel => $"{PointsCost.ToString("N0", ColombianCulture)} pts";

    public string StatusLabel => IsActive ? "Activo" : "Inactivo";

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public static LoyaltyBenefitPresentation From(LoyaltyBenefitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new LoyaltyBenefitPresentation(result.Id, result.Name, result.Description, result.PointsCost, result.IsActive);
    }
}
