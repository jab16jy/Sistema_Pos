using System.Windows.Input;
using POS.Application.DTOs.Communication;
using POS.Application.UseCases.Communication;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class SendDocumentViewModel : ViewModelBase
{
    private readonly SendDocumentUseCase _sendUseCase;
    private DocumentType _documentType;
    private Guid _documentId;
    private string _recipientContact = string.Empty;
    private SendChannel _selectedChannel;
    private bool _isSending;
    private string _resultMessage = string.Empty;

    public SendDocumentViewModel(SendDocumentUseCase sendUseCase)
    {
        _sendUseCase = sendUseCase ?? throw new ArgumentNullException(nameof(sendUseCase));
        SendCommand = new RelayCommand(_ => RunSafe(SendAsync(), ex => ResultMessage = $"Error inesperado: {ex.Message}"));
    }

    public ICommand SendCommand { get; }

    public DocumentType DocumentType
    {
        get => _documentType;
        set => SetProperty(ref _documentType, value);
    }

    public Guid DocumentId
    {
        get => _documentId;
        set => SetProperty(ref _documentId, value);
    }

    public string RecipientContact
    {
        get => _recipientContact;
        set => SetProperty(ref _recipientContact, value ?? string.Empty);
    }

    public SendChannel SelectedChannel
    {
        get => _selectedChannel;
        set => SetProperty(ref _selectedChannel, value);
    }

    public bool IsSending
    {
        get => _isSending;
        private set => SetProperty(ref _isSending, value);
    }

    public string ResultMessage
    {
        get => _resultMessage;
        private set => SetProperty(ref _resultMessage, value ?? string.Empty);
    }

    private async Task SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RecipientContact))
        {
            ResultMessage = "El contacto del destinatario es obligatorio.";
            return;
        }

        IsSending = true;
        ResultMessage = string.Empty;

        try
        {
            SendDocumentRequest request = new(DocumentType, DocumentId, SelectedChannel, RecipientContact);
            SendDocumentResult result = await _sendUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            ResultMessage = result.Success
                ? "Documento enviado correctamente."
                : $"Error al enviar: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            ResultMessage = $"Error inesperado: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }
}
