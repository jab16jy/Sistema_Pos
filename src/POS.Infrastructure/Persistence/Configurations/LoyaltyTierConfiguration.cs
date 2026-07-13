using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyTierConfiguration : IEntityTypeConfiguration<LoyaltyTier>
{
    public void Configure(EntityTypeBuilder<LoyaltyTier> builder)
    {
        builder.ToTable("LOYALTY_TIERS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion<string>();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);
        builder.Property(t => t.MinPoints).HasColumnType("TEXT");
        builder.Property(t => t.DiscountPercent).HasColumnType("TEXT");
        builder.Property(t => t.PointsMultiplier).HasColumnType("TEXT").HasDefaultValue(1.0m);
        builder.Property(t => t.Color).IsRequired().HasMaxLength(9).HasDefaultValue("#64748B");
        builder.Property(t => t.TextForeground).HasConversion<int>().IsRequired().HasDefaultValue(LoyaltyTierTextForeground.White);
    }
}
