using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("LOYALTY_ACCOUNTS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion<string>();
        builder.Property(a => a.CustomerId).HasConversion<string>();
        builder.HasIndex(a => a.CustomerId).IsUnique();
        builder.HasOne<Customer>()
               .WithMany()
               .HasForeignKey(a => a.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
