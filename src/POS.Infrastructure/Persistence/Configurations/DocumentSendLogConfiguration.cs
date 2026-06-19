using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class DocumentSendLogConfiguration : IEntityTypeConfiguration<DocumentSendLog>
{
    public void Configure(EntityTypeBuilder<DocumentSendLog> builder)
    {
        builder.ToTable("DOCUMENT_SEND_LOGS");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion<string>();
        builder.Property(l => l.DocumentId).HasConversion<string>();
        builder.Property(l => l.DocumentType).HasConversion<int>();
        builder.Property(l => l.Channel).HasConversion<int>();
        builder.Property(l => l.RecipientContact).IsRequired().HasMaxLength(200);
        builder.Property(l => l.ErrorMessage).HasMaxLength(2000);
    }
}
