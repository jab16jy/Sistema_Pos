using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class QuoteSequenceConfiguration : IEntityTypeConfiguration<QuoteSequence>
{
    public void Configure(EntityTypeBuilder<QuoteSequence> builder)
    {
        builder.ToTable("QUOTE_SEQUENCES");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).ValueGeneratedNever();
        builder.Property(s => s.LastIssued).IsRequired();
    }
}
