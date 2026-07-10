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
    private readonly DeleteCategoryUseCase _deleteCategory;

    private CategoryResult? _selectedCategory;
    private string _name = string.Empty;
    private bool _isActive = true;
    private string _message = string.Empty;

    public event EventHandler? CategoriesChanged;

    public CategoriesViewModel(
        SearchCategoriesUseCase searchCategories,
        CreateCategoryUseCase createCategory,
        UpdateCategoryUseCase updateCategory,
        DeleteCategoryUseCase deleteCategory)
    {
        _searchCategories = searchCategories ?? throw new ArgumentNullException(nameof(searchCategories));
        _createCategory = createCategory ?? throw new ArgumentNullException(nameof(createCategory));
        _updateCategory = updateCategory ?? throw new ArgumentNullException(nameof(updateCategory));
        _deleteCategory = deleteCategory ?? throw new ArgumentNullException(nameof(deleteCategory));

        Categories = [];
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar categorias: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar categoria: {ex.Message}"));
        DeleteCommand = new RelayCommand(
            _ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar categoria: {ex.Message}"),
            _ => SelectedCategory is not null);
        ClearCommand = new RelayCommand(_ => ClearForm());
    }

    public ObservableCollection<CategoryResult> Categories { get; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ClearCommand { get; }

    public CategoryResult? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                if (value is not null)
                {
                    Name = value.Name;
                    IsActive = value.IsActive;
                }
                else
                {
                    Name = string.Empty;
                    IsActive = true;
                }
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
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
            string successMessage;
            if (SelectedCategory is null)
            {
                var request = new CreateCategoryRequest(Name, IsActive);
                await _createCategory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                successMessage = $"Categoria '{Name}' creada exitosamente.";
            }
            else
            {
                var request = new UpdateCategoryRequest(SelectedCategory.Id, Name, IsActive);
                await _updateCategory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                successMessage = $"Categoria '{Name}' actualizada exitosamente.";
            }

            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = successMessage;
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
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
            Message = $"Error al guardar categoria: {ex.Message}";
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCategory is null) return;

        try
        {
            var categoryName = SelectedCategory.Name;
            await _deleteCategory.ExecuteAsync(SelectedCategory.Id, cancellationToken).ConfigureAwait(true);
            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = $"Categoria '{categoryName}' eliminada exitosamente.";
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al eliminar categoria: {ex.Message}";
        }
    }

    private void ClearForm()
    {
        SelectedCategory = null;
        Name = string.Empty;
        IsActive = true;
    }
}
