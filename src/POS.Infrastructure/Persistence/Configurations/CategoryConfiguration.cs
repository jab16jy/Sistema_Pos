using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("CATEGORIES");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion<string>();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
    }
}
