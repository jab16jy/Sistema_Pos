using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CustomersViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsCustomersAndKeepsSelectionMessageDidactic()
    {
        var repository = new InMemoryCustomerRepository();
        await repository.AddAsync(new Customer("Ana Gomez", "123"), TestContext.Current.CancellationToken);
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        CustomerSearchResult item = Assert.Single(viewModel.Customers);
        Assert.Equal("Ana Gomez", item.FullName);
        Assert.Contains("Selecciona", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_CanSurfaceInactiveCustomersForManagement()
    {
        var search = new StaticCustomerSearchUseCase([
            new CustomerSearchResult(Guid.NewGuid(), "Ana Gomez", "123", null, null, "Main Street 1", true),
            new CustomerSearchResult(Guid.NewGuid(), "Inactive Customer", "999", null, null, "Closed Store", false)
        ]);
        var viewModel = CreateViewModel(new SearchCustomersUseCase(search));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.IncludeInactive = true;
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.Customers.Count);
        Assert.Contains(viewModel.Customers, customer => customer.FullName == "Inactive Customer" && !customer.IsActive);
    }

    [Fact]
    public async Task SelectingCustomer_CarriesAddressIntoEditFormSoSaveDoesNotEraseIt()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        customer.UpdateContactInfo("Ana Gomez", "123", "300", "ana@example.com", "Main Street 1");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedCustomer = viewModel.Customers.Single();
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Main Street 1", repository.Customers.Single().Address);
    }

    [Fact]
    public async Task SaveAsync_WithMissingIdentity_ShowsRecoverableValidationMessage()
    {
        var repository = new InMemoryCustomerRepository();
        var viewModel = CreateViewModel(repository);
        viewModel.FullName = "";
        viewModel.Identification = "123";

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Empty(repository.Customers);
        Assert.Contains("Customer name is required", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_WithValidCustomer_ClearsFormAndRefreshesSearchResults()
    {
        var repository = new InMemoryCustomerRepository();
        var viewModel = CreateViewModel(repository);
        viewModel.FullName = "Ana Gomez";
        viewModel.Identification = "123";

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        CustomerSearchResult result = Assert.Single(viewModel.Customers);
        Assert.Equal("Ana Gomez", result.FullName);
        Assert.Equal(string.Empty, viewModel.FullName);
        Assert.Contains("guardado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenHistoryAsync_ForSelectedCustomer_ShowsPersistedHistoryOrZeroState()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var history = new StaticCustomerHistoryQueryService(new CustomerHistoryResult(customer.Id, [
            new CustomerHistoryEntry(DateTimeOffset.UtcNow, "Sale", "POS-1", 12_000m, "Completed")
        ]));
        var viewModel = new CustomersViewModel(new SaveCustomerUseCase(repository, new InMemoryUnitOfWork()), new GetCustomerHistoryUseCase(history), new SearchCustomersUseCase(new InMemoryCustomerLookupQueryService(repository)));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedCustomer = viewModel.Customers.Single();

        await viewModel.OpenHistoryAsync(TestContext.Current.CancellationToken);

        CustomerHistoryEntry entry = Assert.Single(viewModel.HistoryEntries);
        Assert.Equal("Sale", entry.Kind);
        Assert.Contains("1 movimiento", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_ForSelectedCustomer_DeactivatesInsteadOfHardDeleting()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedCustomer = viewModel.Customers.Single();

        await viewModel.DeleteAsync(TestContext.Current.CancellationToken);

        Assert.False(repository.Customers.Single().IsActive);
        Assert.Contains("eliminado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SelectedCustomer);
    }

    [Fact]
    public async Task OpenHistoryAsync_WithSaleFilter_RestrictsFilteredEntriesButKeepsFullHistory()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var history = new StaticCustomerHistoryQueryService(new CustomerHistoryResult(customer.Id, [
            new CustomerHistoryEntry(DateTimeOffset.UtcNow, "Sale", "POS-1", 12_000m, "Completed"),
            new CustomerHistoryEntry(DateTimeOffset.UtcNow, "Quote", "COT-1", 8_000m, "Active")
        ]));
        var viewModel = new CustomersViewModel(new SaveCustomerUseCase(repository, new InMemoryUnitOfWork()), new GetCustomerHistoryUseCase(history), new SearchCustomersUseCase(new InMemoryCustomerLookupQueryService(repository)));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedCustomer = viewModel.Customers.Single();
        await viewModel.OpenHistoryAsync(TestContext.Current.CancellationToken);

        viewModel.HistoryFilter = "sale";

        Assert.Equal(2, viewModel.HistoryEntries.Count);
        CustomerHistoryEntry filtered = Assert.Single(viewModel.FilteredHistoryEntries);
        Assert.Equal("Sale", filtered.Kind);
        Assert.Equal(1, viewModel.HistoryPurchaseCount);
    }

    [Fact]
    public async Task OpenHistoryAsync_WhenHistoryQueryFails_SetsRecoverableMessage()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var viewModel = new CustomersViewModel(
            new SaveCustomerUseCase(repository, new InMemoryUnitOfWork()),
            new GetCustomerHistoryUseCase(new ThrowingCustomerHistoryQueryService()),
            new SearchCustomersUseCase(new InMemoryCustomerLookupQueryService(repository)));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedCustomer = viewModel.Customers.Single();

        await viewModel.OpenHistoryAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar historial", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectingCustomer_WhenLoyaltyLookupFails_ResetsPointsWithoutThrowing()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = new Customer("Ana Gomez", "123");
        await repository.AddAsync(customer, TestContext.Current.CancellationToken);
        var viewModel = new CustomersViewModel(
            new SaveCustomerUseCase(repository, new InMemoryUnitOfWork()),
            new GetCustomerHistoryUseCase(new StaticCustomerHistoryQueryService(new CustomerHistoryResult(customer.Id, []))),
            new SearchCustomersUseCase(new InMemoryCustomerLookupQueryService(repository)),
            new GetLoyaltyBalanceUseCase(new ThrowingLoyaltyQueryService()));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedCustomer = viewModel.Customers.Single();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, viewModel.LoyaltyPoints);
    }

    [Fact]
    public void NewCommand_ClearsSelectionAndResetsHistory()
    {
        var viewModel = CreateViewModel(new InMemoryCustomerRepository());
        viewModel.FullName = "Should be cleared";

        viewModel.NewCommand.Execute(null);

        Assert.Null(viewModel.SelectedCustomer);
        Assert.Equal(string.Empty, viewModel.FullName);
        Assert.False(viewModel.HasSelectedCustomer);
        Assert.Empty(viewModel.HistoryEntries);
    }

    [Fact]
    public void MainShell_ExposesCustomerModuleAsImplementedWithoutSelectingOtherModules()
    {
        var shell = new MainShellViewModel { Customers = CreateViewModel(new InMemoryCustomerRepository()) };
        var customers = shell.Modules.Single(module => module.Key == "customers");

        shell.SelectedModule = customers;

        Assert.True(customers.IsImplemented);
        Assert.True(shell.IsCustomersSelected);
        Assert.False(shell.IsCashierSelected);
        Assert.Equal("Abrir módulo", shell.MainActionText);
    }

    private static CustomersViewModel CreateViewModel(InMemoryCustomerRepository repository)
        => new(new SaveCustomerUseCase(repository, new InMemoryUnitOfWork()), new GetCustomerHistoryUseCase(new StaticCustomerHistoryQueryService(new CustomerHistoryResult(Guid.NewGuid(), []))), new SearchCustomersUseCase(new InMemoryCustomerLookupQueryService(repository)));

    private static CustomersViewModel CreateViewModel(SearchCustomersUseCase searchCustomers)
        => new(new SaveCustomerUseCase(new InMemoryCustomerRepository(), new InMemoryUnitOfWork()), new GetCustomerHistoryUseCase(new StaticCustomerHistoryQueryService(new CustomerHistoryResult(Guid.NewGuid(), []))), searchCustomers);

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Customers.FirstOrDefault(customer => customer.Id == id));

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Customer>> SearchAsync(string? searchText, bool activeOnly, CancellationToken cancellationToken = default)
        {
            IEnumerable<Customer> query = Customers;
            if (activeOnly)
            {
                query = query.Where(customer => customer.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(customer => customer.FullName.Contains(searchText, StringComparison.OrdinalIgnoreCase) || customer.Identification.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<Customer> results = query.ToList();
            return Task.FromResult(results);
        }
    }

    private sealed class StaticCustomerHistoryQueryService : ICustomerHistoryQueryService
    {
        private readonly CustomerHistoryResult _result;

        public StaticCustomerHistoryQueryService(CustomerHistoryResult result) => _result = result;

        public Task<CustomerHistoryResult> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_result with { CustomerId = customerId });
    }

    private sealed class InMemoryCustomerLookupQueryService : ICustomerLookupQueryService
    {
        private readonly InMemoryCustomerRepository _repository;

        public InMemoryCustomerLookupQueryService(InMemoryCustomerRepository repository) => _repository = repository;

        public async Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Customer> customers = await _repository.SearchAsync(request.SearchText, !request.IncludeInactive, cancellationToken);
            return customers.Select(CustomerSearchResult.FromCustomer).ToList();
        }
    }

    private sealed class StaticCustomerSearchUseCase : ICustomerLookupQueryService
    {
        private readonly IReadOnlyList<CustomerSearchResult> _customers;

        public StaticCustomerSearchUseCase(IReadOnlyList<CustomerSearchResult> customers) => _customers = customers;

        public Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CustomerSearchResult> results = request.IncludeInactive
                ? _customers
                : _customers.Where(customer => customer.IsActive).ToList();
            return Task.FromResult(results);
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class ThrowingCustomerHistoryQueryService : ICustomerHistoryQueryService
    {
        public Task<CustomerHistoryResult> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló historial");
    }

    private sealed class ThrowingLoyaltyQueryService : ILoyaltyRepository
    {
        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló loyalty");

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<LoyaltyRule?>(null);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
