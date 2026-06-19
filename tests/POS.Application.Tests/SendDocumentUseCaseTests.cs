using POS.Application.DTOs.Communication;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests;

public sealed class SendDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmailChannel_CallsEmailServiceAndSavesLog()
    {
        var pdfService = new FakePdfService([1, 2, 3]);
        var emailService = new FakeEmailService(returnsSuccess: true);
        var whatsAppService = new FakeWhatsAppService(returnsSuccess: false);
        var sendLogs = new InMemoryDocumentSendLogRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new SendDocumentUseCase(pdfService, emailService, whatsAppService, sendLogs, unitOfWork);

        var result = await useCase.ExecuteAsync(new SendDocumentRequest(
            DocumentType.Quote,
            Guid.NewGuid(),
            SendChannel.Email,
            "client@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(emailService.WasCalled);
        Assert.Single(sendLogs.Logs);
        Assert.True(sendLogs.Logs[0].Success);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailServiceThrows_SavesLogWithSuccessFalse()
    {
        var pdfService = new FakePdfService([1, 2, 3]);
        var emailService = new FakeEmailService(throws: true);
        var whatsAppService = new FakeWhatsAppService(returnsSuccess: false);
        var sendLogs = new InMemoryDocumentSendLogRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new SendDocumentUseCase(pdfService, emailService, whatsAppService, sendLogs, unitOfWork);

        var result = await useCase.ExecuteAsync(new SendDocumentRequest(
            DocumentType.Quote,
            Guid.NewGuid(),
            SendChannel.Email,
            "client@example.com"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Single(sendLogs.Logs);
        Assert.False(sendLogs.Logs[0].Success);
        Assert.NotNull(sendLogs.Logs[0].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhatsAppChannel_CallsWhatsAppServiceAndSavesLog()
    {
        var pdfService = new FakePdfService([1, 2, 3]);
        var emailService = new FakeEmailService(returnsSuccess: false);
        var whatsAppService = new FakeWhatsAppService(returnsSuccess: true);
        var sendLogs = new InMemoryDocumentSendLogRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new SendDocumentUseCase(pdfService, emailService, whatsAppService, sendLogs, unitOfWork);

        var result = await useCase.ExecuteAsync(new SendDocumentRequest(
            DocumentType.Quote,
            Guid.NewGuid(),
            SendChannel.WhatsApp,
            "+1234567890"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(whatsAppService.WasCalled);
        Assert.Single(sendLogs.Logs);
        Assert.True(sendLogs.Logs[0].Success);
    }

    private sealed class FakePdfService : IPdfService
    {
        private readonly byte[] _bytes;

        public FakePdfService(byte[] bytes) => _bytes = bytes;

        public Task<byte[]> GenerateQuotePdfAsync(Guid quoteId, CancellationToken ct = default)
            => Task.FromResult(_bytes);

        public Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default)
            => Task.FromResult(_bytes);
    }

    private sealed class FakeEmailService : IEmailService
    {
        private readonly bool _returnsSuccess;
        private readonly bool _throws;
        public bool WasCalled { get; private set; }

        public FakeEmailService(bool returnsSuccess = true, bool throws = false)
        {
            _returnsSuccess = returnsSuccess;
            _throws = throws;
        }

        public Task<bool> SendAsync(
            string to,
            string subject,
            string body,
            byte[] attachment,
            string attachmentName,
            CancellationToken ct = default)
        {
            WasCalled = true;

            if (_throws)
            {
                throw new InvalidOperationException("Email service unavailable.");
            }

            return Task.FromResult(_returnsSuccess);
        }
    }

    private sealed class FakeWhatsAppService : IWhatsAppService
    {
        private readonly bool _returnsSuccess;
        public bool WasCalled { get; private set; }

        public FakeWhatsAppService(bool returnsSuccess = true) => _returnsSuccess = returnsSuccess;

        public Task<bool> SendAsync(
            string phoneNumber,
            string message,
            string? attachmentPath,
            CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(_returnsSuccess);
        }
    }

    private sealed class InMemoryDocumentSendLogRepository : IDocumentSendLogRepository
    {
        public List<DocumentSendLog> Logs { get; } = [];

        public Task AddAsync(DocumentSendLog log, CancellationToken ct = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentSendLog>>(Logs.Where(l => l.DocumentId == documentId).ToList());

        public Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Logs.FirstOrDefault(l => l.Id == id));
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }
}
