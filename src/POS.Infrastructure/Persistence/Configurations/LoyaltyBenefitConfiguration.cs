using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyBenefitConfiguration : IEntityTypeConfiguration<LoyaltyBenefit>
{
    public void Configure(EntityTypeBuilder<LoyaltyBenefit> builder)
    {
        builder.ToTable("LOYALTY_BENEFITS");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion<string>();
        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Description).IsRequired().HasMaxLength(500).HasDefaultValue(string.Empty);
        builder.Property(b => b.PointsCost).IsRequired();
        builder.Property(b => b.IsActive).IsRequired().HasDefaultValue(true);
    }
}
