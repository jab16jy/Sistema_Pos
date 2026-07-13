using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(permission => new { permission.RoleId, permission.PermissionKey });
        builder.Property(permission => permission.RoleId).HasConversion<string>();
        builder.Property(permission => permission.PermissionKey).IsRequired().HasMaxLength(100);
        builder.HasOne<Role>().WithMany().HasForeignKey(permission => permission.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
