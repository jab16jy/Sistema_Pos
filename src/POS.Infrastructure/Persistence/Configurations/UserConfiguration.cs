using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        // Existing rows (and every FK column that references users, e.g. Sales.CashierUserId)
        // store the Guid as lowercase TEXT via HasConversion<string>. Without this conversion,
        // EF binds Guid parameters as uppercase TEXT, so lookups by Id never match — that broke
        // Permission checks rely on this mapping because SQLite Guid parameter casing must match stored text.
        builder.Property(u => u.Id).HasConversion<string>();
        builder.Property(u => u.RoleId).HasConversion<string>();
        builder.HasOne<Role>().WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.Username).IsUnique();
    }
}
