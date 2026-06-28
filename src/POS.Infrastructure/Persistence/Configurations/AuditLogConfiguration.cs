using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AUDIT_LOGS");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion<string>();
        builder.Property(l => l.UserId).HasConversion<string?>();
        builder.Property(l => l.Username).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Action).IsRequired().HasMaxLength(40);
        builder.Property(l => l.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(l => l.EntityId).HasMaxLength(100);
        builder.Property(l => l.Details).HasMaxLength(1000);
        builder.HasIndex(l => l.CreatedAt);
        builder.HasIndex(l => l.EntityName);
    }
}
