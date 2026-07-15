using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.UseCases.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Invoices;

public sealed class InvoiceUseCaseTests
{
    [Fact]
    public async Task QuerySalesHistoryAsync_DelegatesSearchRequest()
    {
        var expected = new SaleHistoryResult(Guid.NewGuid(), "FAC-0042", null, "Juan Perez", SaleStatus.Completed, DateTimeOffset.UtcNow, 10m);
        var query = new StubSalesHistoryQueryService([expected]);
        var useCase = new QuerySalesHistoryUseCase(query);

        IReadOnlyList<SaleHistoryResult> results = await useCase.ExecuteAsync(
            new SalesHistoryRequest(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "Perez"),
            TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Perez", query.LastRequest?.SearchText);
    }

    [Fact]
    public async Task ReprintReceiptAsync_WithCompletedSale_PrintsReceipt()
    {
        var sale = CreateCompletedSale(CreateProduct());
        var printer = new StubPrintService();
        var useCase = new ReprintReceiptUseCase(new InMemorySaleRepository(sale), printer);

        await useCase.ExecuteAsync(sale.Id, TestContext.Current.CancellationToken);

        Assert.Equal(sale.Id, printer.PrintedSaleId);
    }

    [Fact]
    public async Task ReprintReceiptAsync_WithReturnedOrCancelledSale_Succeeds()
    {
        var product = CreateProduct();
        var saleReturned = CreateCompletedSale(product);
        saleReturned.MarkReturned();

        var saleCancelled = CreateCompletedSale(product);
        saleCancelled.MarkCancelled();

        var printer = new StubPrintService();
        var useCase = new ReprintReceiptUseCase(new InMemorySaleRepository(saleReturned), printer);

        await useCase.ExecuteAsync(saleReturned.Id, TestContext.Current.CancellationToken);
        Assert.Equal(saleReturned.Id, printer.PrintedSaleId);

        var useCase2 = new ReprintReceiptUseCase(new InMemorySaleRepository(saleCancelled), printer);
        await useCase2.ExecuteAsync(saleCancelled.Id, TestContext.Current.CancellationToken);
        Assert.Equal(saleCancelled.Id, printer.PrintedSaleId);
    }

    [Fact]
    public async Task ProcessReturnAsync_RestoresStockRecordsMovementAndReturn()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var inventory = new InMemoryInventoryRepository();
        var returns = new InMemorySaleReturnRepository();
        var unitOfWork = new CountingUnitOfWork();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            unitOfWork,
            new InMemoryUserRepository(adminUser));

        Guid returnId = await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Customer refund"),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, returnId);
        Assert.Equal(SaleStatus.Returned, sale.Status);
        Assert.Equal(10m, product.CurrentStock);
        InventoryMovement movement = Assert.Single(inventory.AddedMovements);
        Assert.Equal(InventoryMovementType.Return, movement.Type);
        Assert.Equal(sale.Id, movement.ReferenceId);
        Assert.NotNull(await returns.GetBySaleIdAsync(sale.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProcessReturnAsync_WhenSaleAlreadyReturned_RejectsReturn()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        sale.MarkReturned();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork(),
            new InMemoryUserRepository(adminUser));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Duplicate"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_PartialReturn_RestoresOnlyReturnedQty()
    {
        var product = CreateProduct();  // stock 10, qty added: 2
        var sale = CreateCompletedSale(product);  // stock now 8
        var saleItemId = sale.Items.Single().Id;
        var inventory = new InMemoryInventoryRepository();
        var returns = new InMemorySaleReturnRepository();
        var uow = new CountingUnitOfWork();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            uow,
            new InMemoryUserRepository(adminUser));

        // Return only 1 of the 2 items sold
        Guid returnId = await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Partial",
                [new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, returnId);
        // Stock should increase by 1 (only the returned qty)
        Assert.Equal(9m, product.CurrentStock);
        // Only one inventory movement
        Assert.Single(inventory.AddedMovements);
        Assert.Equal(1m, inventory.AddedMovements[0].Quantity);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ProcessReturnAsync_PartialReturn_WhenQtyExceedsOriginal_Throws()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var saleItemId = sale.Items.Single().Id;
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork(),
            new InMemoryUserRepository(adminUser));

        // Original qty was 2, trying to return 3
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Over-qty",
                [new ReturnLineRequest(saleItemId, 3, 4_500m)]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_PartialReturn_WhenSaleItemIsDuplicated_RejectsWithoutStagingAnyMutation()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var saleItemId = sale.Items.Single().Id;
        var inventory = new InMemoryInventoryRepository();
        var returns = new InMemorySaleReturnRepository();
        var unitOfWork = new CountingUnitOfWork();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            unitOfWork,
            new InMemoryUserRepository(adminUser));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Duplicate line",
                [new ReturnLineRequest(saleItemId, 1, 1_500m), new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken));

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(8m, product.CurrentStock);
        Assert.Empty(inventory.AddedMovements);
        Assert.Null(await returns.GetBySaleIdAsync(sale.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProcessReturnAsync_MultipleReturns_AllowedUpToOriginalQuantity()
    {
        var product = CreateProduct(); // qty: 2
        var sale = CreateCompletedSale(product);
        var saleItemId = sale.Items.Single().Id;
        var returns = new InMemorySaleReturnRepository();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var inventory = new InMemoryInventoryRepository();
        var uow = new CountingUnitOfWork();

        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            uow,
            new InMemoryUserRepository(adminUser));

        // First return returns 1 unit
        await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "First return", [new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Completed, sale.Status);

        // Second return returns remaining 1 unit
        await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Second return", [new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Returned, sale.Status);

        // Third return attempts to return 1 more, but remaining quantity is 0 (and status is now Returned)
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, adminUser.Id, "Third return", [new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_WhenUserIsNotAdmin_ThrowsUnauthorizedActionException()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var cashierUser = new User("cashier", "hash", POS.Domain.Authorization.RoleConstants.CashierRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork(),
            new InMemoryUserRepository(cashierUser));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, cashierUser.Id, "Cashier attempt"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_CustomRoleWithInvoicesCancel_RemainsDeniedWithoutMutatingSaleOrStock()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var role = new Role("Invoice Canceller");
        var grant = new RolePermission(role.Id, POS.Domain.Authorization.PermissionKeys.Invoices.Cancel);
        var user = new User("canceller", "hash", role.Id);
        var inventory = new InMemoryInventoryRepository();
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork(),
            new InMemoryUserRepository(user));
        decimal stockBefore = product.CurrentStock;

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, user.Id, "Attempted return"),
            TestContext.Current.CancellationToken));

        Assert.Equal(POS.Domain.Authorization.PermissionKeys.Invoices.Cancel, grant.PermissionKey);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(stockBefore, product.CurrentStock);
        Assert.Empty(inventory.AddedMovements);
    }

    [Fact]
    public async Task ProcessReturnAsync_WhenUserNotFound_ThrowsUnauthorizedActionException()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork(),
            new InMemoryUserRepository(adminUser));

        // Use a different user ID that won't be found in the repository
        await Assert.ThrowsAsync<UnauthorizedActionException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Unknown user return"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelSaleAsync_RestoresStockRecordsMovementAndCancelsSale()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var inventory = new InMemoryInventoryRepository();
        var unitOfWork = new CountingUnitOfWork();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new CancelSaleUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            new InMemoryUserRepository(adminUser),
            new TestRoleRepository(),
            unitOfWork);

        await useCase.ExecuteAsync(
            new CancelSaleRequest(sale.Id, adminUser.Id, "Anulación administrativa"),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(10m, product.CurrentStock);
        InventoryMovement movement = Assert.Single(inventory.AddedMovements);
        Assert.Equal(InventoryMovementType.Return, movement.Type);
        Assert.Equal(sale.Id, movement.ReferenceId);
        Assert.Contains("Anulación administrativa", movement.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelSaleAsync_ClosesLinkedReceivable()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var receivable = new Receivable(Guid.NewGuid(), sale.Id, new Money(3_000m));
        var receivables = new InMemoryReceivableRepository(receivable);
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new CancelSaleUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemoryUserRepository(adminUser),
            new TestRoleRepository(),
            new CountingUnitOfWork(),
            receivables: receivables);

        await useCase.ExecuteAsync(
            new CancelSaleRequest(sale.Id, adminUser.Id, "Cancela deuda asociada"),
            TestContext.Current.CancellationToken);

        Assert.True(receivable.IsCancelled);
        Assert.Equal(0m, receivable.OutstandingBalance.Amount);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenUserIsNotAdmin_ThrowsUnauthorizedActionException()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var cashierUser = new User("cashier", "hash", POS.Domain.Authorization.RoleConstants.CashierRoleId);
        var useCase = new CancelSaleUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemoryUserRepository(cashierUser),
            new TestRoleRepository(),
            new CountingUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => useCase.ExecuteAsync(
            new CancelSaleRequest(sale.Id, cashierUser.Id, "Intento sin permisos"),
            TestContext.Current.CancellationToken));

        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenCustomRoleHasInvoicesCancel_CancelsSale()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var role = new Role("Invoice Canceller");
        var user = new User("canceller", "hash", role.Id);
        var useCase = new CancelSaleUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemoryUserRepository(user),
            new CustomRoleRepository(role, new HashSet<string> { POS.Domain.Authorization.PermissionKeys.Invoices.Cancel }),
            new CountingUnitOfWork());

        await useCase.ExecuteAsync(new CancelSaleRequest(sale.Id, user.Id, "Permiso personalizado"), TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenSaleAlreadyReturned_RejectsCancellation()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        sale.MarkReturned();
        var adminUser = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var useCase = new CancelSaleUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemoryUserRepository(adminUser),
            new TestRoleRepository(),
            new CountingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CancelSaleRequest(sale.Id, adminUser.Id, "Conflicto de estado"),
            TestContext.Current.CancellationToken));
    }

    private static Product CreateProduct() => new(
        "Rice",
        Guid.NewGuid(),
        new Money(1_000m),
        new Money(1_500m),
        new StockQuantity(10m),
        new StockQuantity(2m));

    private static Sale CreateCompletedSale(Product product)
    {
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(3_000m), new Money(3_000m)));
        sale.Complete();
        return sale;
    }

    private sealed class StubSalesHistoryQueryService : ISalesHistoryQueryService
    {
        private readonly IReadOnlyList<SaleHistoryResult> _results;
        private readonly IReadOnlyDictionary<Guid, InvoiceDetailResult> _details;

        public StubSalesHistoryQueryService(IReadOnlyList<SaleHistoryResult> results, IReadOnlyList<InvoiceDetailResult>? details = null)
        {
            _results = results;
            _details = (details ?? []).ToDictionary(detail => detail.SaleId);
        }

        public SalesHistoryRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(SalesHistoryRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_results);
        }

        public Task<InvoiceDetailResult?> GetDetailAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_details.TryGetValue(saleId, out InvoiceDetailResult? detail) ? detail : null);
    }

    private sealed class StubPrintService : IPrintService
    {
        public Guid? PrintedSaleId { get; private set; }

        public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
        {
            PrintedSaleId = saleId;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        private readonly Sale _sale;

        public InMemorySaleRepository(Sale sale)
        {
            _sale = sale;
        }

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _sale.Id ? _sale : null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product _product;

        public InMemoryProductRepository(Product product)
        {
            _product = product;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryReceivableRepository(Receivable receivable) : IReceivableRepository
    {
        public Task AddAsync(Receivable value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Receivable?>(id == receivable.Id ? receivable : null);

        public Task<Receivable?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult<Receivable?>(saleId == receivable.SaleId ? receivable : null);

        public Task UpdateAsync(Receivable value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> AddedMovements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            AddedMovements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySaleReturnRepository : ISaleReturnRepository
    {
        private readonly List<SaleReturn> _returns = [];

        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default)
        {
            _returns.Add(saleReturn);
            return Task.CompletedTask;
        }

        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_returns.SingleOrDefault(saleReturn => saleReturn.OriginalSaleId == saleId));

        public Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
        {
            var list = _returns.Where(saleReturn => saleReturn.OriginalSaleId == saleId).ToList();
            return Task.FromResult<IReadOnlyList<SaleReturn>>(list);
        }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly User _user;

        public InMemoryUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _user.Id ? _user : null);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_user.Username == username ? _user : null);

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>([_user]);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
            => Task.FromResult(_user.RoleId == roleId && _user.IsActive ? 1 : 0);
    }

    private sealed class TestRoleRepository : IRoleRepository
    {
        private static readonly Role Administrator = Role.CreateSystemRole(POS.Domain.Authorization.RoleConstants.AdministratorRoleId, POS.Domain.Authorization.RoleConstants.AdministratorRoleName);
        private static readonly Role Cashier = Role.CreateSystemRole(POS.Domain.Authorization.RoleConstants.CashierRoleId, POS.Domain.Authorization.RoleConstants.CashierRoleName);
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(id == Administrator.Id ? Administrator : id == Cashier.Id ? Cashier : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([Administrator, Cashier]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class CustomRoleRepository(Role role, IReadOnlySet<string> grants) : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(id == role.Id ? role : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(name == role.Name ? role : null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([role]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(roleId == role.Id && grants.Contains(permissionKey));
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(grants);
        public Task AddAsync(Role addedRole, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role updatedRole, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
