using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class QuoteView : UserControl
{
    /// <summary>
    /// Bridges the "Historial" subtab to <see cref="QuoteHistoryViewModel"/> without changing this
    /// UserControl's own DataContext (bound to <see cref="QuoteViewModel"/> for the "Nueva cotización" subtab).
    /// Purely visual composition plumbing — no business logic — per pos-wpf-mvvm.
    /// </summary>
    public static readonly DependencyProperty HistoryViewModelProperty =
        DependencyProperty.Register(
            nameof(HistoryViewModel),
            typeof(QuoteHistoryViewModel),
            typeof(QuoteView),
            new PropertyMetadata(null, OnHistoryViewModelChanged));

    public QuoteView()
    {
        InitializeComponent();
    }

    public QuoteHistoryViewModel? HistoryViewModel
    {
        get => (QuoteHistoryViewModel?)GetValue(HistoryViewModelProperty);
        set => SetValue(HistoryViewModelProperty, value);
    }

    private static void OnHistoryViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not QuoteView view)
        {
            return;
        }

        if (e.OldValue is QuoteHistoryViewModel oldViewModel)
        {
            oldViewModel.FileExportRequested -= view.OnFileExportRequested;
            oldViewModel.SaleConversionConfirmationRequested -= view.OnSaleConversionConfirmationRequested;
        }

        if (e.NewValue is QuoteHistoryViewModel newViewModel)
        {
            newViewModel.FileExportRequested += view.OnFileExportRequested;
            newViewModel.SaleConversionConfirmationRequested += view.OnSaleConversionConfirmationRequested;
        }
    }

    private void OnFileExportRequested(object? sender, (string FileName, byte[] Data) args)
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

    private async void OnSaleConversionConfirmationRequested(object? sender, POS.Presentation.Shared.Quotes.QuoteSummaryRow quote)
    {
        if (sender is not QuoteHistoryViewModel viewModel)
        {
            return;
        }

        if (MessageBox.Show(
                QuoteHistoryViewModel.SaleConversionConfirmationMessage,
                "Realizar venta",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await viewModel.ConfirmSaleConversionAsync(quote);
    }
}
