using System.IO;
using Microsoft.Win32;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class ReportsView : System.Windows.Controls.UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ReportsViewModel? _vm;

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.FileExportRequested -= OnFileExportRequested;

        _vm = e.NewValue as ReportsViewModel;

        if (_vm is not null)
            _vm.FileExportRequested += OnFileExportRequested;
    }

    private void OnFileExportRequested(object? sender, (string FileName, byte[] Data) args)
    {
        bool isPdf = args.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        var dialog = new SaveFileDialog
        {
            FileName = args.FileName,
            DefaultExt = isPdf ? ".pdf" : ".xlsx",
            Filter = isPdf
                ? "PDF|*.pdf|Todos los archivos|*.*"
                : "Excel|*.xlsx|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        File.WriteAllBytes(dialog.FileName, args.Data);
    }
}
