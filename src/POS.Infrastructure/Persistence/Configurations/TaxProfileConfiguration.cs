using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class TaxProfileConfiguration : IEntityTypeConfiguration<TaxProfile>
{
    public void Configure(EntityTypeBuilder<TaxProfile> builder)
    {
        builder.ToTable("TAX_PROFILES");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).HasConversion<string>();
        builder.Property(profile => profile.Code).IsRequired().HasMaxLength(50);
        builder.Property(profile => profile.Name).IsRequired().HasMaxLength(120);
        builder.Property(profile => profile.Rate).HasColumnName("rate");
        builder.HasIndex(profile => profile.Code).IsUnique();
    }
}
