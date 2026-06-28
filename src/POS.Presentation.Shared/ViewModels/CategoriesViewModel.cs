using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for category CRUD operations.
/// Used as a sub-section inside the Products module.
/// </summary>
public sealed class CategoriesViewModel : ViewModelBase
{
    private readonly SearchCategoriesUseCase _searchCategories;
    private readonly CreateCategoryUseCase _createCategory;
    private readonly UpdateCategoryUseCase _updateCategory;

    private CategoryResult? _selectedCategory;
    private string _name = string.Empty;
    private string _message = string.Empty;

    public CategoriesViewModel(
        SearchCategoriesUseCase searchCategories,
        CreateCategoryUseCase createCategory,
        UpdateCategoryUseCase updateCategory)
    {
        _searchCategories = searchCategories ?? throw new ArgumentNullException(nameof(searchCategories));
        _createCategory = createCategory ?? throw new ArgumentNullException(nameof(createCategory));
        _updateCategory = updateCategory ?? throw new ArgumentNullException(nameof(updateCategory));

        Categories = [];
        LoadCommand = new RelayCommand(async _ => await LoadAsync().ConfigureAwait(true));
        SaveCommand = new RelayCommand(async _ => await SaveAsync().ConfigureAwait(true));
        ClearCommand = new RelayCommand(_ => ClearForm());
    }

    public ObservableCollection<CategoryResult> Categories { get; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ClearCommand { get; }

    public CategoryResult? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value) && value is not null)
            {
                Name = value.Name;
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _searchCategories.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            Categories.Clear();
            foreach (var category in results)
            {
                Categories.Add(category);
            }

            Message = Categories.Count == 0
                ? "No hay categorias registradas."
                : $"Mostrando {Categories.Count} categoria(s).";
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar categorias: {ex.Message}";
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (SelectedCategory is null)
            {
                var request = new CreateCategoryRequest(Name);
                await _createCategory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = $"Categoria '{Name}' creada exitosamente.";
            }
            else
            {
                var request = new UpdateCategoryRequest(SelectedCategory.Id, Name);
                await _updateCategory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = $"Categoria '{Name}' actualizada exitosamente.";
            }

            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
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

    private void ClearForm()
    {
        SelectedCategory = null;
        Name = string.Empty;
    }
}
