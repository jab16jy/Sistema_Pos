using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.BarcodeLabels;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests.Infrastructure;

public sealed class ThermalPrinterIntegrationTests
{
    [Fact]
    public async Task PrinterSettingsResolver_PrefersPersistedPrinterConfiguration()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "COM7",
            "C:\\printer",
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var runtime = new PrinterRuntimeOptions
        {
            Command = "192.168.1.10:9100",
            WorkingDirectory = "C:\\runtime"
        };

        var resolver = new PrinterSettingsResolver(runtime, fixture.DbContext);

        ThermalPrinterTarget result = await resolver.ResolveRequiredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("COM7", result.Command);
        Assert.Equal("C:\\printer", result.WorkingDirectory);
        Assert.Equal(ThermalPrinterTargetKind.Serial, result.Kind);
    }

    [Fact]
    public async Task LabelPrintService_UsesResolvedThermalTarget()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "COM5",
            null,
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new LabelPrintService(resolver, transport);

        await service.PrintAsync(
            [new LabelRequest("Cafe molido", 12000m, "770123456789", BarcodeSymbology.Ean13)],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("COM5", transport.LastTarget?.Command);
        Assert.NotNull(transport.LastPayload);
        Assert.NotEmpty(transport.LastPayload!);
    }

    [Fact]
    public async Task LabelPrintService_RejectsNonDirectThermalCommand()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "C:\\tools\\print-receipt.cmd",
            "C:\\tools",
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new LabelPrintService(resolver, transport);

        var ex = await Assert.ThrowsAsync<ApplicationException>(() => service.PrintAsync(
            [new LabelRequest("Cafe molido", 12000m, "CODE128-01", BarcodeSymbology.Code128)],
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("impresión térmica directa", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(transport.LastPayload);
    }

    private sealed class FakeThermalPrinterTransport : IThermalPrinterTransport
    {
        public ThermalPrinterTarget? LastTarget { get; private set; }
        public byte[]? LastPayload { get; private set; }

        public Task SendAsync(ThermalPrinterTarget target, byte[] payload, CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDbContextFactory : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDbContextFactory(SqliteConnection connection, AppDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public AppDbContext DbContext { get; }

        public static async Task<TestDbContextFactory> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new TestDbContextFactory(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
