using System.Globalization;
using System.Reflection;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class QuoteTests
{
    [Fact]
    public void AddItem_WhenStatusIsConverted_ThrowsQuoteAlreadyConvertedException()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.Convert(Guid.NewGuid());

        var item = new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(1m), 0m);

        Assert.Throws<QuoteAlreadyConvertedException>(() => quote.AddItem(item));
    }

    [Fact]
    public void Convert_WhenAlreadyConverted_ThrowsQuoteAlreadyConvertedException()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.Convert(Guid.NewGuid());

        Assert.Throws<QuoteAlreadyConvertedException>(() => quote.Convert(Guid.NewGuid()));
    }

    [Fact]
    public void Convert_WhenActive_SetsStatusToConvertedAndSetsConvertedSaleId()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var saleId = Guid.NewGuid();

        quote.Convert(saleId);

        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(saleId, quote.ConvertedSaleId);
    }

    [Fact]
    public void Expire_WhenActive_SetsStatusToExpired()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);

        quote.Expire();

        Assert.Equal(QuoteStatus.Expired, quote.Status);
    }

    [Fact]
    public void Expire_WhenConverted_DoesNotChangeStatus()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var saleId = Guid.NewGuid();
        quote.Convert(saleId);

        quote.Expire();

        Assert.Equal(QuoteStatus.Converted, quote.Status);
    }

    [Fact]
    public void Total_IsCorrectSumOfItemSubtotals()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var item1 = new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m);
        var item2 = new QuoteItem(Guid.NewGuid(), "Product B", new Money(500m), new Quantity(3m), 10m);

        quote.AddItem(item1);
        quote.AddItem(item2);

        var expectedTotal = item1.Subtotal().Amount + item2.Subtotal().Amount;
        Assert.Equal(expectedTotal, quote.Total);
    }

    [Fact]
    public void QuoteItem_Subtotal_CalculatesCorrectly()
    {
        // UnitPrice=100, Quantity=2, Discount=10 → 100 * 2 * (1 - 10/100) = 200 * 0.90 = 180
        var item = new QuoteItem(Guid.NewGuid(), "Product A", new Money(100m), new Quantity(2m), 10m);

        var subtotal = item.Subtotal();

        Assert.Equal(180m, subtotal.Amount);
    }

    [Fact]
    public void Constructor_WithPhoneAndEmail_PreservesValidatedContactData()
    {
        var quote = new Quote("COT-2026-0001", "Ana Gomez", 30, null, "+57 300 123 4567", "ana@example.com", "  Observación  ");

        Assert.Equal("+57 300 123 4567", quote.CustomerPhone);
        Assert.Equal("ana@example.com", quote.CustomerEmail);
        Assert.Equal("Observación", quote.Observations);
    }

    [Theory]
    [InlineData(QuoteStatus.Draft)]
    [InlineData(QuoteStatus.Active)]
    public void Update_WhenQuoteIsStillAProposal_AppliesEveryField(QuoteStatus status)
    {
        // Active is a persisted status the domain cannot currently reach through a public
        // transition, but a QUOTES row can carry it, so the editability rule must hold for it too.
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        SetStatus(quote, status);
        var customerId = Guid.NewGuid();

        quote.Update("Ana Gomez", customerId, 15, "+57 300 123 4567", "ana@example.com", "  Pedido urgente  ");

        Assert.Equal("Ana Gomez", quote.CustomerName);
        Assert.Equal(customerId, quote.CustomerId);
        Assert.Equal(15, quote.ValidityDays);
        Assert.Equal("+57 300 123 4567", quote.CustomerPhone);
        Assert.Equal("ana@example.com", quote.CustomerEmail);
        Assert.Equal("Pedido urgente", quote.Observations);
    }

    [Theory]
    [InlineData(QuoteStatus.Draft)]
    [InlineData(QuoteStatus.Active)]
    public void CanEdit_IsTrueOnlyWhileTheQuoteIsStillAProposal(QuoteStatus status)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        Assert.True(quote.CanEdit);

        SetStatus(quote, status);

        Assert.True(quote.CanEdit);
    }

    [Theory]
    [InlineData(QuoteStatus.Converted)]
    [InlineData(QuoteStatus.Cancelled)]
    [InlineData(QuoteStatus.Expired)]
    public void CanEdit_IsFalseOnceTheQuoteIsFrozen(QuoteStatus status)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        SetStatus(quote, status);

        Assert.False(quote.CanEdit);
    }

    /// <summary>
    /// C4b at the aggregate. The edit form keeps the customer picker live, so Update has to be able to
    /// link a quote AND to clear it back to a walk-in. A quote created without an association is the
    /// starting point here because that is the case that was previously impossible to express: the
    /// operator picked a customer and the save silently discarded the choice.
    /// </summary>
    [Fact]
    public void Update_AssignsTheCustomerAssociation_IncludingClearingIt()
    {
        var quote = new Quote("COT-2026-LINK", "Test Customer", 30);

        var customerId = Guid.NewGuid();
        quote.Update("Test Customer", customerId, 30, null, null, null);
        Assert.Equal(customerId, quote.CustomerId);

        quote.Update("Test Customer", null, 30, null, null, null);
        Assert.Null(quote.CustomerId);
    }

    /// <summary>Re-linking in the other direction: a quote that named a customer can be pointed at another one.</summary>
    [Fact]
    public void Update_ReplacesAnExistingCustomerAssociation()
    {
        var quote = new Quote("COT-2026-RELINK", "Test Customer", 30, Guid.NewGuid());
        var otherCustomerId = Guid.NewGuid();

        quote.Update("Test Customer", otherCustomerId, 30, null, null, null);

        Assert.Equal(otherCustomerId, quote.CustomerId);
    }

    /// <summary>
    /// C3. <c>Expire()</c> has exactly one caller in src/ — the conversion path — so a quote whose
    /// deadline passed keeps <c>Draft</c> forever. A status-only rule therefore left a lapsed proposal
    /// editable, and saving it with a larger ValidityDays would have resurrected a lapsed offer as a
    /// live, convertible one. The maintainer decision: a calendar-expired quote is treated exactly like
    /// <c>QuoteStatus.Expired</c>, and renewal means creating a new quote.
    /// </summary>
    [Fact]
    public void CanEditAt_ADraftQuoteWhoseDeadlinePassed_IsNotEditable()
    {
        var quote = new Quote("COT-2026-LAPSED", "Test Customer", 30);
        SetStatus(quote, QuoteStatus.Draft);
        SetExpiresAt(quote, ColombiaMidnightUtc(-1));

        Assert.False(quote.CanEditAt(ColombiaMidnightUtc(0)));
    }

    [Fact]
    public void CanEditAt_ADraftQuoteExpiringToday_IsStillEditable()
    {
        // The deadline day itself is still valid — this matches ConvertQuoteToSaleUseCase, which allows
        // conversion on the expiration date in UTC-5. Off-by-one here would silently freeze quotes early.
        var quote = new Quote("COT-2026-TODAY", "Test Customer", 30);
        SetStatus(quote, QuoteStatus.Draft);
        SetExpiresAt(quote, ColombiaMidnightUtc(0));

        Assert.True(quote.CanEditAt(ColombiaMidnightUtc(0)));
    }

    [Fact]
    public void CanEditAt_UsesTheColombianCalendarDayNotTheUtcOne()
    {
        // A deadline of 03:00 UTC on the 2nd is already the 2nd at 22:00 on the 1st in Bogotá. Judged on
        // the UTC date it would read as two days of validity left; judged on the Colombian date, one.
        var quote = new Quote("COT-2026-TZ", "Test Customer", 30);
        SetExpiresAt(quote, new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 10, 1), quote.ColombiaExpirationDate);

        // 2026-10-02 02:00 UTC is 2026-10-01 21:00 in Colombia: same Colombian day, still valid.
        Assert.True(quote.CanEditAt(new DateTime(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc)));
        // Five minutes later it is 2026-10-02 in Colombia and the quote has lapsed.
        Assert.False(quote.CanEditAt(new DateTime(2026, 10, 2, 5, 5, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Update_ADraftQuoteWhoseDeadlinePassed_ThrowsAsExpiredAndLeavesTheQuoteUntouched()
    {
        var quote = new Quote("COT-2026-LAPSED-UPD", "Test Customer", 30, null, null, null, "Original");
        quote.AddItem(new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m));
        SetExpiresAt(quote, ColombiaMidnightUtc(-1));

        // Reported as expired, not as "estado Draft": the status alone would tell the operator the quote
        // is editable right before the write is refused.
        var exception = Assert.Throws<QuoteNotEditableException>(
            () => quote.Update("Nuevo Cliente", null, 90, null, null, "Cambio"));

        Assert.Contains("vencida", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(QuoteStatus.Draft, quote.Status);
        Assert.Equal("Test Customer", quote.CustomerName);
        Assert.Equal(30, quote.ValidityDays);
        Assert.Equal("Original", quote.Observations);
        Assert.Single(quote.Items);
    }

    [Fact]
    public void ReplaceItems_ADraftQuoteExpiringToday_StillAcceptsTheCorrection()
    {
        var quote = new Quote("COT-2026-TODAY-REP", "Test Customer", 30);
        SetExpiresAt(quote, ColombiaMidnightUtc(0));

        quote.ReplaceItems([new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(1m), 0m)]);

        Assert.Single(quote.Items);
    }

    /// <summary>
    /// C5a. The exception carries no Converted arm and no fallback arm, because no caller can construct
    /// it for a converted quote — QuoteAlreadyConvertedException owns that wording. This pins that the
    /// reachable statuses each get their own message, so collapsing the switch cannot quietly make a
    /// cancelled quote read as merely expired.
    /// </summary>
    [Fact]
    public void QuoteNotEditableException_SaysWhyForEachReachableStatus()
    {
        Assert.Contains("anulada", new QuoteNotEditableException("COT-1", QuoteStatus.Cancelled).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vencida", new QuoteNotEditableException("COT-1", QuoteStatus.Expired).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vencida", new QuoteNotEditableException("COT-1", QuoteStatus.Draft).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vencida", new QuoteNotEditableException("COT-1", QuoteStatus.Active).Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Midnight of the Colombian day <paramref name="offsetDays"/> from today, in UTC (Colombia = UTC-5).</summary>
    private static DateTime ColombiaMidnightUtc(int offsetDays) =>
        DateTime.UtcNow.AddHours(-5).Date.AddDays(offsetDays).AddHours(5);

    [Theory]
    [InlineData(QuoteStatus.Converted, typeof(QuoteAlreadyConvertedException))]
    [InlineData(QuoteStatus.Cancelled, typeof(QuoteNotEditableException))]
    [InlineData(QuoteStatus.Expired, typeof(QuoteNotEditableException))]
    public void Update_WhenQuoteIsFrozen_ThrowsAndLeavesTheQuoteUntouched(QuoteStatus status, Type expected)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30, null, "+57 300 123 4567", "ana@example.com", "Original");
        quote.AddItem(new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m));
        DateTime createdAt = quote.CreatedAt;
        SetStatus(quote, status);

        Exception exception = Assert.Throws(expected, () => quote.Update("Nuevo Cliente", null, 60, null, null, "Cambio"));

        Assert.Equal(status, quote.Status);
        Assert.Equal("Test Customer", quote.CustomerName);
        Assert.Equal(30, quote.ValidityDays);
        Assert.Equal("Original", quote.Observations);
        Assert.Equal(createdAt, quote.CreatedAt);
        Assert.Single(quote.Items);
        Assert.Contains("COT-2026-0001", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(QuoteStatus.Converted)]
    [InlineData(QuoteStatus.Cancelled)]
    [InlineData(QuoteStatus.Expired)]
    public void ReplaceItems_WhenQuoteIsFrozen_ThrowsAndKeepsTheExistingLines(QuoteStatus status)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        QuoteItem original = new(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m);
        quote.AddItem(original);
        SetStatus(quote, status);

        Assert.ThrowsAny<DomainException>(() => quote.ReplaceItems(
            [new QuoteItem(Guid.NewGuid(), "Product B", new Money(500m), new Quantity(1m), 0m)]));

        Assert.Equal(original, Assert.Single(quote.Items));
    }

    [Fact]
    public void ReplaceItems_ClearsTheCollectionAndAddsTheNewLines()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.AddItem(new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m));

        var replacement = new QuoteItem(Guid.NewGuid(), "Product B", new Money(500m), new Quantity(3m), 10m);
        quote.ReplaceItems([replacement]);

        Assert.Equal(replacement, Assert.Single(quote.Items));
        Assert.Equal(1_350m, quote.Total);
    }

    [Fact]
    public void Update_RecomputesExpiresAtFromCreatedAt_NotFromNow()
    {
        // I3: renewing the validity window must slide the deadline from the original creation
        // instant, otherwise repeated edits would push a lapsed offer back into being valid.
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        DateTime createdAt = quote.CreatedAt;

        quote.Update("Test Customer", null, 45, null, null, null);

        Assert.Equal(createdAt.AddDays(45), quote.ExpiresAt);
    }

    [Fact]
    public void Update_WithNonPositiveValidityDays_Throws()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);

        // Was ArgumentOutOfRangeException. The aggregate now refuses the window itself with a message
        // naming the accepted range, because an ArgumentOutOfRangeException told the operator nothing
        // about which rule they had broken — it came from DateTime.AddDays, deep inside the aggregate.
        var exception = Assert.Throws<QuoteValidityDaysOutOfRangeException>(
            () => quote.Update("Test Customer", null, 0, null, null, null));

        Assert.Contains("COT-2026-0001", exception.Message, StringComparison.Ordinal);
        // The aggregate is left untouched, so the form on screen still describes the stored quote.
        Assert.Equal(30, quote.ValidityDays);
    }

    /// <summary>
    /// C6b. The upper bound. <c>DateTime.AddDays</c> accepts a window of up to roughly 2.9 million days
    /// (<c>DateTime.MaxValue</c> is year 9999), so before this bound every absurd value below that was
    /// representable, passed every guard and got persisted — then reached the client as a rendered
    /// deadline, since <c>PdfService</c> prints "Válida hasta" from <c>ExpiresAt</c>.
    ///
    /// A typo is enough to reach it: holding Ctrl in the validity spinner and pressing Up runs the number
    /// into the thousands, well past the form's own 365 maximum.
    /// </summary>
    [Theory]
    [InlineData(3651)]
    [InlineData(500_000)]
    [InlineData(2_900_000)]
    [InlineData(int.MaxValue)]
    public void Update_WithValidityDaysBeyondTheCeiling_ThrowsDomainException(int validityDays)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);

        QuoteValidityDaysOutOfRangeException exception = Assert.Throws<QuoteValidityDaysOutOfRangeException>(
            () => quote.Update("Test Customer", null, validityDays, null, null, null));

        // A clear domain message, not an arithmetic failure: the operator can see the rule and the value
        // they typed, which is the whole point of refusing it at this boundary.
        Assert.Contains("COT-2026-0001", exception.Message, StringComparison.Ordinal);
        Assert.Contains(validityDays.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        Assert.Contains(Quote.MaximumValidityDays.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        Assert.Equal(30, quote.ValidityDays);
        Assert.Equal(30, quote.ExpiresAt.Subtract(quote.CreatedAt).TotalDays);
    }

    /// <summary>
    /// The creation path is guarded by the same method, so a request that bypasses the form cannot
    /// introduce a value the edit path would refuse. The two entry points must not disagree.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithValidityDaysOutsideTheAcceptedRange_Throws(int validityDays) =>
        Assert.Throws<QuoteValidityDaysOutOfRangeException>(
            () => new Quote("COT-2026-0001", "Test Customer", validityDays));

    /// <summary>
    /// Negative control: the bound must not be so tight that it refuses a legitimate window. The form's
    /// <c>IntegerUpDown</c> caps at 365, so a full year has to be accepted on both paths — this is what
    /// stops the ceiling from quietly becoming the next form of a duplicated rule that disagrees with the
    /// control the operator actually uses.
    /// </summary>
    [Fact]
    public void TheCeilingAcceptsTheLongestWindowTheFormOffers()
    {
        Assert.True(
            Quote.MaximumValidityDays >= 365,
            "The domain ceiling must never be tighter than the form's own maximum (365).");

        var quote = new Quote("COT-2026-0001", "Test Customer", 365);
        quote.Update("Test Customer", null, 365, null, null, null);

        Assert.Equal(365, quote.ValidityDays);
        Assert.Equal(quote.CreatedAt.AddDays(365), quote.ExpiresAt);
    }

    [Fact]
    public void Update_AcrossSeveralEdits_KeepsQuoteNumberIdAndCreatedAt()
    {
        // I2 + negative control 4: three edits in a row still describe one quote.
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        Guid id = quote.Id;
        DateTime createdAt = quote.CreatedAt;

        quote.Update("Ana Gomez", null, 10, null, null, null);
        quote.Update("Ana Gómez", null, 20, "+57 300 123 4567", null, null);
        quote.Update("Ana Gomez Soto", null, 45, null, "ana@example.com", "Listo");

        Assert.Equal("COT-2026-0001", quote.QuoteNumber);
        Assert.Equal(id, quote.Id);
        Assert.Equal(createdAt, quote.CreatedAt);
        Assert.Equal(createdAt.AddDays(45), quote.ExpiresAt);
        Assert.Equal(QuoteStatus.Draft, quote.Status);
    }

    /// <summary>
    /// QuoteStatus.Active is persisted but not reachable through a public transition, so the only
    /// way to exercise the Active branch of the editability rule is to stamp the private setter —
    /// the same escape hatch CommercialPresentationFlowTests uses for Quote.ExpiresAt.
    /// </summary>
    private static void SetStatus(Quote quote, QuoteStatus status) =>
        typeof(Quote)
            .GetProperty(nameof(Quote.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(quote, status);

    /// <summary>
    /// <c>ExpiresAt</c> is anchored to <c>CreatedAt</c>, and the constructor only accepts a positive
    /// validity, so a quote cannot be constructed already lapsed. Stamping the private setter is the
    /// same escape hatch the existing Status tests use.
    /// </summary>
    private static void SetExpiresAt(Quote quote, DateTime expiresAt) =>
        typeof(Quote)
            .GetProperty(nameof(Quote.ExpiresAt), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(quote, expiresAt);
}
