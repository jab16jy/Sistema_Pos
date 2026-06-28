using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UNIT_OF_MEASURES");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasConversion<string>();
        builder.Property(u => u.Name).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Abbreviation).IsRequired().HasMaxLength(10);
    }
}
