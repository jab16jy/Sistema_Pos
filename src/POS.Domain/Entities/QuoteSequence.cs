namespace POS.Domain.Entities;

// ponytail: pure persistence row for the year-scoped counter (Infrastructure reserves numbers via
// raw SQL UPSERT/RETURNING); this entity only exists so EF can map/track the table shape.
public sealed class QuoteSequence
{
    private QuoteSequence()
    {
    }

    public QuoteSequence(int year, int lastIssued)
    {
        Year = year;
        LastIssued = lastIssued;
    }

    public int Year { get; private set; }
    public int LastIssued { get; private set; }
}
