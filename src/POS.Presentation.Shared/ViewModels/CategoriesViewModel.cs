using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Products;
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
    private readonly SearchProductsUseCase? _searchProducts;
    private readonly AssignProductsToCategoryUseCase? _assignProductsToCategory;

    private CategoryResult? _selectedCategory;
    private string _name = string.Empty;
    private bool _isActive = true;
    private string _message = string.Empty;
    private string _productSearchText = string.Empty;
    private Guid? _assignmentCategoryId;

    public event EventHandler? CategoriesChanged;
    public event EventHandler? ProductsAssigned;

    public CategoriesViewModel(
        SearchCategoriesUseCase searchCategories,
        CreateCategoryUseCase createCategory,
        UpdateCategoryUseCase updateCategory,
        DeleteCategoryUseCase deleteCategory)
        : this(searchCategories, createCategory, updateCategory, deleteCategory, null, null)
    {
    }

    public CategoriesViewModel(
        SearchCategoriesUseCase searchCategories,
        CreateCategoryUseCase createCategory,
        UpdateCategoryUseCase updateCategory,
        DeleteCategoryUseCase deleteCategory,
        SearchProductsUseCase? searchProducts,
        AssignProductsToCategoryUseCase? assignProductsToCategory)
    {
        _searchCategories = searchCategories ?? throw new ArgumentNullException(nameof(searchCategories));
        _createCategory = createCategory ?? throw new ArgumentNullException(nameof(createCategory));
        _updateCategory = updateCategory ?? throw new ArgumentNullException(nameof(updateCategory));
        _deleteCategory = deleteCategory ?? throw new ArgumentNullException(nameof(deleteCategory));
        _searchProducts = searchProducts;
        _assignProductsToCategory = assignProductsToCategory;

        Categories = [];
        ActiveCategories = [];
        AssignmentProducts = [];
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar categorias: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar categoria: {ex.Message}"));
        DeleteCommand = new RelayCommand(
            _ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar categoria: {ex.Message}"),
            _ => SelectedCategory is not null);
        ClearCommand = new RelayCommand(_ => ClearForm());
        LoadAssignmentProductsCommand = new RelayCommand(
            _ => RunSafe(LoadAssignmentProductsAsync(), ex => Message = $"Error al buscar productos: {ex.Message}"),
            _ => _searchProducts is not null);
        AssignProductsCommand = new RelayCommand(
            _ => RunSafe(AssignProductsAsync(), ex => Message = $"Error al asignar categoria: {ex.Message}"),
            _ => _assignProductsToCategory is not null && AssignmentCategoryId.HasValue && SelectedProductCount > 0);
    }

    public ObservableCollection<CategoryResult> Categories { get; }

    public ObservableCollection<CategoryResult> ActiveCategories { get; }

    public ObservableCollection<ProductCategoryAssignmentItem> AssignmentProducts { get; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand LoadAssignmentProductsCommand { get; }

    public ICommand AssignProductsCommand { get; }

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

    public string ProductSearchText
    {
        get => _productSearchText;
        set => SetProperty(ref _productSearchText, value);
    }

    public Guid? AssignmentCategoryId
    {
        get => _assignmentCategoryId;
        set
        {
            if (SetProperty(ref _assignmentCategoryId, value))
            {
                (AssignProductsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public int SelectedProductCount => AssignmentProducts.Count(product => product.IsSelected);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _searchCategories.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            Categories.Clear();
            ActiveCategories.Clear();
            foreach (var category in results)
            {
                Categories.Add(category);
                if (category.IsActive)
                {
                    ActiveCategories.Add(category);
                }
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

    public async Task LoadAssignmentProductsAsync(CancellationToken cancellationToken = default)
    {
        if (_searchProducts is null)
        {
            Message = "La asignación masiva no está disponible.";
            return;
        }

        try
        {
            IReadOnlyList<ProductSearchResult> results = await _searchProducts
                .ExecuteAsync(new SearchProductsRequest(ProductSearchText), cancellationToken)
                .ConfigureAwait(true);

            foreach (ProductCategoryAssignmentItem item in AssignmentProducts)
            {
                item.PropertyChanged -= OnAssignmentProductPropertyChanged;
            }

            AssignmentProducts.Clear();
            foreach (ProductSearchResult product in results)
            {
                var item = new ProductCategoryAssignmentItem(product.Id, product.Name, product.CategoryName);
                item.PropertyChanged += OnAssignmentProductPropertyChanged;
                AssignmentProducts.Add(item);
            }

            OnPropertyChanged(nameof(SelectedProductCount));
            (AssignProductsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            Message = AssignmentProducts.Count == 0
                ? "No encontramos productos para asignar."
                : $"Selecciona los productos que deseas agrupar. Mostrando {AssignmentProducts.Count}.";
        }
        catch (Exception ex)
        {
            Message = $"Error al buscar productos: {ex.Message}";
        }
    }

    public async Task AssignProductsAsync(CancellationToken cancellationToken = default)
    {
        if (_assignProductsToCategory is null || !AssignmentCategoryId.HasValue)
        {
            Message = "Selecciona una categoría activa para asignar.";
            return;
        }

        ProductCategoryAssignmentItem[] selectedProducts = AssignmentProducts
            .Where(product => product.IsSelected)
            .ToArray();
        if (selectedProducts.Length == 0)
        {
            Message = "Selecciona al menos un producto.";
            return;
        }

        try
        {
            CategoryResult? category = ActiveCategories.FirstOrDefault(item => item.Id == AssignmentCategoryId.Value);
            if (category is null)
            {
                Message = "Selecciona una categoría activa para asignar.";
                return;
            }

            int updated = await _assignProductsToCategory.ExecuteAsync(
                new AssignProductsToCategoryRequest(category.Id, selectedProducts.Select(product => product.Id).ToArray()),
                cancellationToken).ConfigureAwait(true);

            foreach (ProductCategoryAssignmentItem product in selectedProducts)
            {
                product.CategoryName = category.Name;
                product.IsSelected = false;
            }

            Message = updated == 0
                ? "Los productos seleccionados ya pertenecen a esta categoría."
                : $"Se asignó la categoría '{category.Name}' a {updated} producto(s).";
            ProductsAssigned?.Invoke(this, EventArgs.Empty);
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
            Message = $"Error al asignar categoria: {ex.Message}";
        }
    }

    private void ClearForm()
    {
        SelectedCategory = null;
        Name = string.Empty;
        IsActive = true;
    }

    private void OnAssignmentProductPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductCategoryAssignmentItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedProductCount));
            (AssignProductsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
