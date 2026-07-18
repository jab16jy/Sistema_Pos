using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.UseCases.Invoices;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class InvoicesViewModelExceptionTests
{
    [Fact]
    public async Task LoadCommand_WhenHistoryQueryFails_SetsRecoverableMessage()
    {
        var viewModel = new InvoicesViewModel(
            new QuerySalesHistoryUseCase(new ThrowingSalesHistoryQueryService()),
            new ReprintReceiptUseCase(new EmptySaleRepository(), new NoOpPrintService()),
            new ProcessReturnUseCase(
                new EmptySaleRepository(),
                new EmptyProductRepository(),
                new EmptyInventoryRepository(),
                new EmptySaleReturnRepository(),
                new NoOpUnitOfWork(),
                new EmptyUserRepository()),
            new FakeSession());

        viewModel.LoadCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar facturas", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingSalesHistoryQueryService : ISalesHistoryQueryService
    {
        public Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(SalesHistoryRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<SaleHistoryResult>>(new InvalidOperationException("DB caída"));

        public Task<InvoiceDetailResult?> GetDetailAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromException<InvoiceDetailResult?>(new InvalidOperationException("DB caída"));
    }

    private sealed class NoOpPrintService : IPrintService
    {
        public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptySaleRepository : ISaleRepository
    {
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sale?>(null);
        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class EmptyProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyInventoryRepository : IInventoryRepository
    {
        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);
        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);
        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptySaleReturnRepository : ISaleReturnRepository
    {
        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult<SaleReturn?>(null);

        public Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SaleReturn>>([]);

        public Task<SaleReturn?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default) => Task.FromResult<SaleReturn?>(null);
    }

    private sealed class EmptyUserRepository : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class EmptyRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeSession : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.NewGuid();
        public string Username => "operator";
        public Guid RoleId => Guid.NewGuid();
        public string RoleName => "Custom";
        public IReadOnlySet<string> Permissions => new HashSet<string>();
        public bool IsAuthenticated => true;
        public bool HasPermission(string permissionKey) => false;
    }
}
