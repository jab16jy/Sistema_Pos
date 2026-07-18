using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("QUOTES");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasConversion<string>();
        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(q => q.QuoteNumber).IsUnique();
        builder.Property(q => q.CustomerId).HasConversion<string?>();
        builder.Property(q => q.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(q => q.CustomerPhone).HasMaxLength(30);
        builder.Property(q => q.CustomerEmail).HasMaxLength(200);
        builder.Property(q => q.Observations).HasMaxLength(1000);
        builder.Property(q => q.Status).HasConversion<int>();
        builder.Property(q => q.ConvertedSaleId).HasConversion<string?>();
        builder.HasMany(q => q.Items)
               .WithOne()
               .HasForeignKey(qi => qi.QuoteId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Customer>()
               .WithMany()
               .HasForeignKey(q => q.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Sale>()
               .WithMany()
               .HasForeignKey(q => q.ConvertedSaleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
