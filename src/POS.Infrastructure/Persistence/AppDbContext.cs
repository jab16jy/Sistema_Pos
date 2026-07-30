using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    private readonly ICurrentOperatorSession? _session;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentOperatorSession? session) : base(options)
    {
        _session = session;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TaxProfile> TaxProfiles => Set<TaxProfile>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<CashClosing> CashClosings => Set<CashClosing>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
    public DbSet<QuoteSequence> QuoteSequences => Set<QuoteSequence>();
    public DbSet<DocumentSendLog> DocumentSendLogs => Set<DocumentSendLog>();
    public DbSet<Receivable> Receivables => Set<Receivable>();
    public DbSet<ReceivableSettlement> ReceivableSettlements => Set<ReceivableSettlement>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<ReturnSettlement> ReturnSettlements => Set<ReturnSettlement>();
    public DbSet<ReturnReceivableReconciliation> ReturnReceivableReconciliations => Set<ReturnReceivableReconciliation>();
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyRule> LoyaltyRules => Set<LoyaltyRule>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();
    public DbSet<LoyaltyBenefit> LoyaltyBenefits => Set<LoyaltyBenefit>();
    public DbSet<LoyaltyBenefitClaim> LoyaltyBenefitClaims => Set<LoyaltyBenefitClaim>();
    public DbSet<ProductImportBatch> ProductImportBatches => Set<ProductImportBatch>();
    public DbSet<ProductImportLine> ProductImportLines => Set<ProductImportLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite provider can't translate ORDER BY / comparisons over the CLR DateTimeOffset type
        // (throws NotSupportedException) -- it only rejects the CLR type at the translation layer,
        // not the underlying TEXT data. EF/SQLite already store DateTimeOffset as an ISO-8601-ish
        // TEXT string by default ("yyyy-MM-dd HH:mm:ss[.fffffff]zzz", trimmed trailing zero digits,
        // always the same whole-second prefix width). A string HasConversion using that exact fixed
        // round-trip format keeps the column TEXT (matches InitialCreate migration + all existing
        // rows byte-for-byte where fractional digits are already 7-wide, and stays chronologically
        // sortable even against older trimmed-width rows: trimming only drops trailing zeros, so the
        // leading/most-significant digits -- the ones string comparison checks first -- never change).
        // This makes ORDER BY/range comparisons translate to SQL without any migration or backfill.
        // Verified empirically (spike): old trimmed-format rows and new fixed-width rows interleave
        // in correct chronological order under both EF-translated OrderBy and raw SQL ORDER BY.
        modelBuilder.Entity<Sale>()
.Property(s => s.CreatedAt)
.HasConversion(
v => v.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
v => DateTimeOffset.Parse(v))
.HasColumnType("TEXT");

        modelBuilder.Entity<Purchase>()
.Property(p => p.CreatedAt)
.HasConversion(
v => v.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
v => DateTimeOffset.Parse(v))
.HasColumnType("TEXT");

        // Same conversion as Sale.CreatedAt above, so GetRecentAsync can ORDER BY/paginate at the
        // SQL level instead of loading the whole AUDIT_LOGS table to sort client-side.
        modelBuilder.Entity<AuditLog>()
.Property(a => a.CreatedAt)
.HasConversion(
v => v.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
v => DateTimeOffset.Parse(v))
.HasColumnType("TEXT");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        AddAuditEntries();
        return SaveChangesWithConcurrencyHandling(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddAuditEntries();
        return SaveChangesWithConcurrencyHandling(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddAuditEntries();
        return SaveChangesWithConcurrencyHandlingAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddAuditEntries();
        return SaveChangesWithConcurrencyHandlingAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private int SaveChangesWithConcurrencyHandling(bool acceptAllChangesOnSuccess = true)
    {
        try
        {
            AdvanceProductVersions();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            ChangeTracker.Clear();
            throw new ConcurrencyConflictException(
                "The entity was modified or deleted by another operation.",
                exception);
        }
    }

    private async Task<int> SaveChangesWithConcurrencyHandlingAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        try
        {
            AdvanceProductVersions();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            ChangeTracker.Clear();
            throw new ConcurrencyConflictException(
                "The entity was modified or deleted by another operation.",
                exception);
        }
    }

    private void AdvanceProductVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Modified)
            {
                int originalVersion = entry.Property(p => p.Version).OriginalValue;
                entry.Property(p => p.Version).CurrentValue = originalVersion + 1;
            }
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = (SqliteConnection)Database.GetDbConnection();
            await using var transaction = connection.BeginTransaction(deferred: true);
            await Database.UseTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);

            try
            {
                await operation(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                ChangeTracker.Clear();

                if (IsSqliteTransactionConflict(exception))
                    throw new TransactionConflictException(exception);

                throw;
            }
        }
        finally
        {
            await Database.UseTransactionAsync(null, cancellationToken).ConfigureAwait(false);
            await Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static bool IsSqliteTransactionConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: 5 or 6 })
                return true;
        }

        return false;
    }

    private void AddAuditEntries()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            AuditLogs.Add(new AuditLog(
                _session?.IsAuthenticated == true ? _session.OperatorId : null,
                _session?.IsAuthenticated == true ? _session.Username : null,
                entry.State.ToString(),
                entry.Entity.GetType().Name,
                TryGetEntityId(entry),
                BuildDetails(entry)));
        }
    }

    // ponytail: entities with a natural business reference show that instead of the raw Id; extend as needed.
    private static string? TryGetEntityId(EntityEntry entry) => entry.Entity switch
    {
        Sale sale => sale.InvoiceNumber,
        Quote quote => quote.QuoteNumber,
        Customer customer => customer.Identification,
        User user => user.Username,
        Supplier supplier => supplier.TaxId,
        _ => entry.Metadata.FindPrimaryKey() is { } key
            ? string.Join("-", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()))
            : null
    };

    private string? BuildDetails(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return DescribeCreated(entry.Entity) ?? Prefixed("Se creó", entry.Entity) ?? "Registro creado.";
        }

        if (entry.State == EntityState.Deleted)
        {
            return Prefixed("Se eliminó", entry.Entity) ?? "Registro eliminado.";
        }

        var changed = entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name)
            .Distinct()
            .Take(12)
            .ToList();

        string fields = changed.Count == 0
            ? "Registro actualizado."
            : $"Campos modificados: {string.Join(", ", changed.Select(TranslateFieldName))}.";
        string? subject = DescribeSubject(entry.Entity);

        return subject is null ? fields : $"{subject} — {fields}";
    }

    // ponytail: only entities where the raw Id/field-name list is not self-explanatory get a custom description; extend as needed.
    private string? DescribeCreated(object entity) => entity switch
    {
        InventoryMovement movement => DescribeInventoryMovement(movement),
        _ => null
    };

    /* ponytail: first name-ish property is the business subject; covers every named entity without a per-type switch. */
    private static readonly string[] SubjectPropertyNames =
    [
        "FullName",
        "Name",
        "Username",
        "QuoteNumber",
        "InvoiceNumber",
        "Description"
    ];

    private static readonly IReadOnlyDictionary<string, string> FieldLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "Nombre",
            ["FullName"] = "Nombre",
            ["Identification"] = "Identificación",
            ["Description"] = "Descripción",
            ["SalePrice"] = "Precio de venta",
            ["CostPrice"] = "Precio de costo",
            ["CurrentStock"] = "Existencias",
            ["MinimumStock"] = "Existencias mínimas",
            ["Barcode"] = "Código de barras",
            ["IsActive"] = "Estado",
            ["Username"] = "Usuario",
            ["RoleId"] = "Rol",
            ["Email"] = "Correo",
            ["Phone"] = "Teléfono",
            ["Address"] = "Dirección",
            ["CategoryId"] = "Categoría",
            ["SubCategoryId"] = "Subcategoría",
            ["TaxRate"] = "Impuesto",
            ["TaxId"] = "NIT",
            ["Status"] = "Estado",
            ["Points"] = "Puntos",
            ["Balance"] = "Saldo",
            ["Quantity"] = "Cantidad",
            ["SourceCode"] = "Código",
            ["TracksInventory"] = "Control de inventario",
            ["Version"] = "Versión"
        };

    private static string TranslateFieldName(string name) =>
        FieldLabels.TryGetValue(name, out string? label) ? label : name;

    private static string? DescribeSubject(object entity)
    {
        Type type = entity.GetType();

        foreach (string propertyName in SubjectPropertyNames)
        {
            if (type.GetProperty(propertyName)?.GetValue(entity) is string value
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? Prefixed(string verb, object entity) =>
        DescribeSubject(entity) is { } subject ? $"{verb}: {subject}." : null;

    private string DescribeInventoryMovement(InventoryMovement movement)
    {
        string productName = ChangeTracker.Entries<Product>()
            .FirstOrDefault(p => p.Entity.Id == movement.ProductId)?.Entity.Name
            ?? movement.ProductId.ToString();

        string typeLabel = movement.Type switch
        {
            InventoryMovementType.Entry => "Entrada",
            InventoryMovementType.Exit => "Salida",
            InventoryMovementType.Adjustment => "Ajuste",
            InventoryMovementType.Return => "Devolución",
            InventoryMovementType.InitialBalance => "Saldo inicial",
            _ => movement.Type.ToString()
        };

        return $"{typeLabel} de {movement.Quantity} unidad(es) de {productName}. Motivo: {movement.Reason}.";
    }
}
