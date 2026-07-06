using System.IO;
using Microsoft.Win32;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class InvoicesView
{
    public InvoicesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private InvoicesViewModel? _viewModel;

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FileExportRequested -= OnFileExportRequested;
        }

        _viewModel = e.NewValue as InvoicesViewModel;

        if (_viewModel is not null)
        {
            _viewModel.FileExportRequested += OnFileExportRequested;
        }
    }

    private static void OnFileExportRequested(object? sender, (string FileName, byte[] Data) args)
    {
        var dialog = new SaveFileDialog
        {
            FileName = args.FileName,
            DefaultExt = ".pdf",
            Filter = "PDF|*.pdf|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllBytes(dialog.FileName, args.Data);
    }
}
