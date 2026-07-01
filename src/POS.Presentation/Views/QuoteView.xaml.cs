using System.Windows;
using System.Windows.Controls;
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
        DependencyProperty.Register(nameof(HistoryViewModel), typeof(QuoteHistoryViewModel), typeof(QuoteView), new PropertyMetadata(null));

    public QuoteView()
    {
        InitializeComponent();
    }

    public QuoteHistoryViewModel? HistoryViewModel
    {
        get => (QuoteHistoryViewModel?)GetValue(HistoryViewModelProperty);
        set => SetValue(HistoryViewModelProperty, value);
    }
}
