namespace POS.Presentation.Shared.ViewModels;

public sealed class ProductCategoryAssignmentItem : ViewModelBase
{
    private bool _isSelected;
    private string _categoryName;

    public ProductCategoryAssignmentItem(Guid id, string name, string categoryName)
    {
        Id = id;
        Name = name;
        _categoryName = categoryName;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
