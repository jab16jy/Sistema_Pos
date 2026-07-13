using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> builder)
    {
        builder.ToTable("BUSINESS_SETTINGS");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).HasConversion<string>();
        builder.Property(settings => settings.BusinessName).IsRequired().HasMaxLength(200);
        builder.Property(settings => settings.BusinessAddress).HasMaxLength(500);
        builder.Property(settings => settings.TaxId).HasMaxLength(100);
        builder.Property(settings => settings.SmtpHost).HasMaxLength(200);
        builder.Property(settings => settings.SmtpPort);
        builder.Property(settings => settings.SmtpUser).HasMaxLength(200);
        builder.Property(settings => settings.SmtpPassword)
            .HasConversion(
                password => SmtpPasswordProtector.Protect(password),
                storedPassword => SmtpPasswordProtector.Unprotect(storedPassword))
            .HasMaxLength(500);
        builder.Property(settings => settings.SmtpUseSsl);
        builder.Property(settings => settings.SmtpSenderAddress).HasMaxLength(200);
        builder.Property(settings => settings.PrinterCommand).HasMaxLength(500);
        builder.Property(settings => settings.PrinterWorkingDirectory).HasMaxLength(500);
        builder.Property(settings => settings.PrinterPort).HasMaxLength(100);
        builder.Property(settings => settings.BackupDirectory).HasMaxLength(500);
        builder.Property(settings => settings.LastBackupAtUtc).HasColumnType("TEXT");
        builder.Property(settings => settings.LastBackupSizeBytes);
        builder.Property(settings => settings.LastBackupLocation).HasMaxLength(500);
        builder.Property(settings => settings.LastBackupUsername).HasMaxLength(100);
        builder.Property(settings => settings.CurrencyCode).IsRequired().HasMaxLength(3);
    }
}
