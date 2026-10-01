using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Quote
{
    private readonly List<QuoteItem> _items = new();

    private Quote()
    {
        QuoteNumber = string.Empty;
        CustomerName = string.Empty;
    }

    public Quote(string quoteNumber, string customerName, int validityDays, Guid? customerId = null, string? customerPhone = null, string? customerEmail = null, string? observations = null)
    {
        if (string.IsNullOrWhiteSpace(quoteNumber))
        {
            throw new ArgumentException("Quote number is required.", nameof(quoteNumber));
        }

        EnsureValidityDaysInRange(validityDays, quoteNumber);

        Id = Guid.NewGuid();
        QuoteNumber = quoteNumber.Trim();
        CustomerName = ContactInfo.NormalizeName(customerName);
        ValidityDays = validityDays;
        CustomerId = customerId;
        CustomerPhone = ContactInfo.NormalizePhone(customerPhone);
        CustomerEmail = ContactInfo.NormalizeEmail(customerEmail);
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        Status = QuoteStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddDays(validityDays);
    }

    /// <summary>
    /// Bounds on the validity window, enforced by both the constructor and <see cref="Update"/>.
    /// </summary>
    /// <remarks>
    /// The ceiling is 3650 days — a decade. Deliberately generous rather than tight: the form's own
    /// <c>IntegerUpDown</c> caps at 365 (one year), so every value the system can legitimately produce sits
    /// far below this and the bound can never contradict the form's maximum. A quote is a commercial
    /// proposal a client holds for weeks — the reporting horizons in this app are weeks to months — so a
    /// decade is already nonsense. The point of the bound is to refuse nonsense HERE, with a message that
    /// names the rule, rather than to encode a business policy the domain states nowhere else. Lifting it
    /// later is a one-line change and touches no schema.
    ///
    /// The failure it prevents is subtler than an exception. <c>DateTime.AddDays</c> does not overflow
    /// until roughly 2.9 million days from today (<c>DateTime.MaxValue</c> is year 9999), so every absurd
    /// value below that — 500,000 days included — is perfectly representable, passed every previous guard
    /// and persisted. It then reaches the client as a rendered deadline, because <c>PdfService</c> prints
    /// "Válida hasta" from <c>ExpiresAt</c>, on a proposal nobody can act on.
    /// </remarks>
    public const int MinimumValidityDays = 1;
    public const int MaximumValidityDays = 3650;

    public Guid Id { get; private set; }
    public string QuoteNumber { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? Observations { get; private set; }
    public int ValidityDays { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? ConvertedSaleId { get; private set; }
    public IReadOnlyCollection<QuoteItem> Items => _items.AsReadOnly();

    public decimal Total => _items.Sum(i => i.Subtotal().Amount);

    /// <summary>
    /// Colombia has no daylight saving, so UTC-5 is a fixed offset. A quote's deadline is judged on
    /// the Colombian calendar day, not the UTC one: at 20:00 in Bogotá it is already the next day for
    /// the shop, and a quote that lapsed for the client lapsed on the day the client reads.
    /// </summary>
    private const int ColombiaUtcOffsetHours = -5;

    /// <summary>
    /// The deadline as a Colombian calendar day, which is the unit a validity window is actually
    /// expressed in. <see cref="ExpiresAt"/> is stored in UTC.
    /// </summary>
    public DateOnly ColombiaExpirationDate => DateOnly.FromDateTime(ExpiresAt.AddHours(ColombiaUtcOffsetHours));

    /// <summary>
    /// A quote stays editable only while it is still a proposal AND has not lapsed. Converted is the
    /// point where it became a Sale, so that plus Cancelled and Expired are frozen for good.
    /// </summary>
    /// <remarks>
    /// The lapsed case is why this is not a status check alone. <see cref="Expire"/> has exactly one
    /// caller in <c>src/</c> — the conversion path — so a quote whose deadline passed keeps
    /// <see cref="QuoteStatus.Draft"/> indefinitely. Left as a pure status rule, a lapsed proposal stays
    /// editable and saving it with a larger <c>ValidityDays</c> would resurrect it as a live, convertible
    /// quote, re-pricing an offer the client already let lapse. A lapsed quote is renewed by creating a
    /// new one, which is the same guidance <c>QuoteSummaryRow.ExpirationGuidance</c> already gives.
    /// </remarks>
    public bool CanEdit => CanEditAt(DateTime.UtcNow);

    /// <summary>
    /// <see cref="CanEdit"/> with the clock supplied, so the rule is deterministic and testable instead
    /// of reading wall-clock time inside a property. Uses the same UTC-5 comparison
    /// <c>ConvertQuoteToSaleUseCase</c> applies — one rule, not two subtly different ones.
    /// </summary>
    public bool CanEditAt(DateTime utcNow) =>
        Status is QuoteStatus.Draft or QuoteStatus.Active &&
        ColombiaExpirationDate >= DateOnly.FromDateTime(utcNow.AddHours(ColombiaUtcOffsetHours));

    public void AddItem(QuoteItem item)
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        _items.Add(item);
    }

    /// <summary>
    /// Corrects the header of a quote that is still a proposal. QuoteNumber, Id and CreatedAt are
    /// deliberately out of reach: the number is part of what the client was quoted, so an unlimited
    /// number of corrections still has to describe the same proposal.
    /// </summary>
    /// <param name="customerId">
    /// The registered customer to link, or null to clear the link back to a walk-in. This is assigned
    /// here rather than left alone because the edit form keeps the customer picker live: an operator who
    /// searches a customer and clicks "Usar" expects that choice to be saved, not displayed and dropped.
    /// Passing null clears it deliberately, which is why this cannot be expressed as "leave unchanged" —
    /// a caller that must not touch the association passes the quote's current <see cref="CustomerId"/>.
    /// </param>
    public void Update(string customerName, Guid? customerId, int validityDays, string? phone, string? email, string? observations)
    {
        EnsureEditable();

        EnsureValidityDaysInRange(validityDays, QuoteNumber);

        CustomerName = ContactInfo.NormalizeName(customerName);
        CustomerId = customerId;
        ValidityDays = validityDays;
        CustomerPhone = ContactInfo.NormalizePhone(phone);
        CustomerEmail = ContactInfo.NormalizeEmail(email);
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        // Anchored to CreatedAt, never to DateTime.UtcNow: a validity window is part of what was
        // offered, so editing it slides the deadline instead of silently restarting the countdown
        // and re-validating an offer that had already lapsed.
        ExpiresAt = CreatedAt.AddDays(validityDays);
    }

    /// <summary>
    /// Swaps the whole line collection. QuoteItem is immutable, so a corrected quantity or price is a
    /// different item rather than a mutated one — which also keeps the discount range check in the
    /// QuoteItem constructor.
    /// </summary>
    /// <remarks>
    /// This REPLACES the line set; it does not reconcile it against what is already there. Every item
    /// passed in is treated as new, whatever its Id: the QuoteItem constructor assigns a fresh Guid, and
    /// QuoteLineBuilder builds fresh items on every save, so an edit is a delete-all plus insert-all.
    /// (An earlier version of this comment claimed the repository could tell a dropped line from a
    /// corrected one by Id. It cannot — that claim was false, and it is exactly the kind of contract a
    /// maintainer would trust. See QuoteItem's own note on the load-bearing immutability.)
    /// </remarks>
    public void ReplaceItems(IEnumerable<QuoteItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        EnsureEditable();

        _items.Clear();
        _items.AddRange(items);
    }

    public void Convert(Guid saleId)
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        Status = QuoteStatus.Converted;
        ConvertedSaleId = saleId;
    }

    public void Expire()
    {
        if (Status is QuoteStatus.Draft or QuoteStatus.Active)
        {
            Status = QuoteStatus.Expired;
        }
    }

    public void Cancel()
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new InvalidOperationException($"La cotización '{QuoteNumber}' ya fue convertida a venta y no se puede anular.");
        }

        if (Status == QuoteStatus.Cancelled)
        {
            return;
        }

        Status = QuoteStatus.Cancelled;
    }

    /// <summary>
    /// The single editability guard, with the clock supplied. Public so callers can validate before doing
    /// any work and still get the aggregate's exact rejection, rather than re-deriving the rule and
    /// drifting from it — a duplicated copy of this check already reported a lapsed quote as
    /// "estado Draft" instead of vencida.
    /// </summary>
    public void EnsureEditableAt(DateTime utcNow)
    {
        if (CanEditAt(utcNow))
        {
            return;
        }

        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        // A lapsed-but-still-Draft quote is reported as expired, because that is what it is from the
        // client's point of view. Reporting it as "estado Draft" would tell the operator the quote is
        // editable right before the write is rejected.
        throw new QuoteNotEditableException(QuoteNumber, IsLapsedByCalendar(utcNow) ? QuoteStatus.Expired : Status);
    }

    private void EnsureEditable()
    {
        if (CanEdit)
        {
            return;
        }

        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        // A lapsed-but-still-Draft quote is reported as expired, because that is what it is from the
        // client's point of view. Reporting it as "estado Draft" would tell the operator the quote is
        // editable right before the write is rejected.
        throw new QuoteNotEditableException(QuoteNumber, IsLapsedByCalendar(DateTime.UtcNow) ? QuoteStatus.Expired : Status);
    }

    /// <summary>
    /// One validity guard for both entry points, so creation and editing cannot disagree about what a
    /// legal window is. Both callers run it before assigning anything, so a refused window leaves the
    /// aggregate exactly as it was — on the edit path that means the header on screen still describes the
    /// quote that is actually stored.
    /// </summary>
    private static void EnsureValidityDaysInRange(int validityDays, string quoteNumber)
    {
        if (validityDays < MinimumValidityDays || validityDays > MaximumValidityDays)
        {
            throw new QuoteValidityDaysOutOfRangeException(quoteNumber, validityDays);
        }
    }

    /// <summary>
    /// True when the deadline has passed on the Colombian calendar while the quote is still a proposal
    /// and therefore has not been transitioned to <see cref="QuoteStatus.Expired"/> by any caller.
    /// </summary>
    public bool IsLapsedByCalendar(DateTime utcNow) =>
        Status is QuoteStatus.Draft or QuoteStatus.Active &&
        ColombiaExpirationDate < DateOnly.FromDateTime(utcNow.AddHours(ColombiaUtcOffsetHours));
}
