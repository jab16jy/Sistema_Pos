using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Reason).IsRequired();

        // ReferenceId is intentionally left unconstrained: it is a polymorphic business
        // reference (sale, purchase, adjustment, etc. depending on Type) with no single principal table.
        builder.HasOne<Product>()
               .WithMany()
               .HasForeignKey(movement => movement.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
