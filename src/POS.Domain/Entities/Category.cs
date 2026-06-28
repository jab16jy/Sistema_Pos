namespace POS.Domain.Entities;

public sealed class Category
{
    private Category()
    {
        Name = string.Empty;
    }

    public Category(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
}
