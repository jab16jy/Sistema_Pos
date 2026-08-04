using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Persistence;

/// <summary>
/// Editing a saved product re-sends its whole presentation list. These tests pin the second save
/// down to real SQLite, because the failure only shows up once the rows already exist.
/// </summary>
public sealed class ProductPresentationRoundTripTests
{
    [Fact]
    public async Task EditingAProduct_RemovingOnePresentation_PersistsTheRemainder()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(
            new ProductPresentationDto("Caja", 24, 100_000m),
            new ProductPresentationDto("Cuarto de caja", 6, 30_000m));

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Caja", 24, 100_000m));

        Assert.Equal(["Caja"], await harness.ReadPresentationNamesAsync(productId));
    }

    [Fact]
    public async Task EditingAProduct_WithoutTouchingPresentations_StillSaves()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));

        // Changing only the category still resends the presentation list unchanged.
        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Caja", 24, 100_000m));

        Assert.Equal(["Caja"], await harness.ReadPresentationNamesAsync(productId));
    }

    [Fact]
    public async Task EditingAProduct_AddingANewPresentation_PersistsIt()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));

        await harness.UpdateProductAsync(
            productId,
            new ProductPresentationDto("Caja", 24, 100_000m),
            new ProductPresentationDto("Paquete de 3", 3, 15_000m));

        Assert.Equal(["Caja", "Paquete de 3"], await harness.ReadPresentationNamesAsync(productId));
    }

    [Fact]
    public async Task EditingAProduct_AddingThePresentationToAProductThatHadNone_PersistsIt()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync();

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Paquete de 3", 3, 15_000m));

        Assert.Equal(["Paquete de 3"], await harness.ReadPresentationNamesAsync(productId));
    }

    [Fact]
    public async Task EditingAProduct_RenamingAPresentation_ReplacesIt()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Bulto", 24, 100_000m));

        Assert.Equal(["Bulto"], await harness.ReadPresentationNamesAsync(productId));
    }

    [Fact]
    public async Task EditingAProduct_KeepsTheRowIdentityOfASurvivingPresentation()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));
        Guid originalRowId = (await harness.ReadPresentationIdsAsync(productId)).Single();

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Caja", 24, 120_000m));

        Assert.Equal(originalRowId, (await harness.ReadPresentationIdsAsync(productId)).Single());
    }

    [Fact]
    public async Task APresentationAddedInProductos_IsVisibleToTheCashierLookup()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));

        await harness.UpdateProductAsync(
            productId,
            new ProductPresentationDto("Caja", 24, 100_000m),
            new ProductPresentationDto("Paquete de 3", 3, 15_000m));

        ProductLookupResult found = Assert.Single(await harness.CashierSearchAsync("X3"));
        Assert.Equal(
            [("Caja", 24, 100_000m), ("Paquete de 3", 3, 15_000m)],
            found.Presentations.Select(p => (p.Name, p.Units, p.Price)));
    }

    [Fact]
    public async Task APresentationRemovedInProductos_DisappearsFromTheCashierLookup()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(
            new ProductPresentationDto("Caja", 24, 100_000m),
            new ProductPresentationDto("Cuarto de caja", 6, 30_000m));

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Caja", 24, 100_000m));

        ProductLookupResult found = Assert.Single(await harness.CashierSearchAsync("X3"));
        Assert.Equal(["Caja"], found.Presentations.Select(p => p.Name));
    }

    [Fact]
    public async Task APriceEditedInProductos_ReachesTheCashierLookup()
    {
        await using var harness = await ProductHarness.CreateAsync();
        Guid productId = await harness.CreateProductAsync(new ProductPresentationDto("Caja", 24, 100_000m));

        await harness.UpdateProductAsync(productId, new ProductPresentationDto("Caja", 24, 137_500m));

        ProductLookupResult found = Assert.Single(await harness.CashierSearchAsync("X3"));
        Assert.Equal(137_500m, Assert.Single(found.Presentations).Price);
    }

    private sealed class ProductHarness : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly DbContextOptions<AppDbContext> _options;

        private ProductHarness(string databasePath, DbContextOptions<AppDbContext> options)
        {
            _databasePath = databasePath;
            _options = options;
        }

        public Guid CategoryId { get; private set; }

        public static async Task<ProductHarness> CreateAsync()
        {
            string databasePath = Path.Combine(Path.GetTempPath(), $"pos_presentation_roundtrip_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            var harness = new ProductHarness(databasePath, options);

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var category = new Category("General");
            db.Categories.Add(category);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            harness.CategoryId = category.Id;

            return harness;
        }

        public async Task<Guid> CreateProductAsync(params ProductPresentationDto[] presentations)
        {
            await using var db = new AppDbContext(_options);
            var useCase = new CreateProductUseCase(new ProductRepository(db), new CategoryRepository(db), db);
            return await useCase.ExecuteAsync(
                new CreateProductRequest(
                    "X3", CategoryId, 2_000m, 5_000m, 0m, 1m, "", null, 19m, presentations, Guid.Empty),
                TestContext.Current.CancellationToken);
        }

        public async Task UpdateProductAsync(Guid productId, params ProductPresentationDto[] presentations)
        {
            await using var db = new AppDbContext(_options);
            var useCase = new UpdateProductUseCase(new ProductRepository(db), new CategoryRepository(db), db);
            await useCase.ExecuteAsync(
                new UpdateProductRequest(
                    productId, "X3", CategoryId, 2_000m, 5_000m, 1m, "", null, 19m, presentations),
                TestContext.Current.CancellationToken);
        }

        /// <summary>The exact path Caja uses: a fresh AsNoTracking lookup, not the Productos cache.</summary>
        public async Task<IReadOnlyList<ProductLookupResult>> CashierSearchAsync(string query)
        {
            await using var db = new AppDbContext(_options);
            return await new ProductLookupService(db).SearchAsync(query, TestContext.Current.CancellationToken);
        }

        public async Task<IReadOnlyList<string>> ReadPresentationNamesAsync(Guid productId)
        {
            await using var db = new AppDbContext(_options);
            Product product = await db.Products
                .Include(p => p.Presentations)
                .SingleAsync(p => p.Id == productId, TestContext.Current.CancellationToken);
            return product.Presentations.Select(p => p.Name).ToList();
        }

        public async Task<IReadOnlyList<Guid>> ReadPresentationIdsAsync(Guid productId)
        {
            await using var db = new AppDbContext(_options);
            Product product = await db.Products
                .Include(p => p.Presentations)
                .SingleAsync(p => p.Id == productId, TestContext.Current.CancellationToken);
            return product.Presentations.Select(p => p.Id).ToList();
        }

        public async ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            await Task.Yield();
            try { File.Delete(_databasePath); } catch (IOException) { }
        }
    }
}
