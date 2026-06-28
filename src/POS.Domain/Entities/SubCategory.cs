namespace POS.Domain.Entities;

public sealed class SubCategory
{
    private SubCategory()
    {
        Name = string.Empty;
    }

    public SubCategory(string name, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Subcategory name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        CategoryId = categoryId;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }

    public Category Category { get; private set; } = null!;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Subcategory name is required.", nameof(name));
        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
}
