using System.Windows;
using POS.Application.DTOs.Quotes;

namespace POS.Presentation.Views;

/// <summary>Small read-only preview of a quote's line items — an alternative to generating a PDF just to look at it.</summary>
public partial class QuoteDetailWindow : Window
{
    public QuoteDetailWindow(QuoteDetailResult detail)
    {
        InitializeComponent();
        DataContext = detail;
        ContactInfoBorder.Visibility = string.IsNullOrWhiteSpace(detail.CustomerEmail) && string.IsNullOrWhiteSpace(detail.CustomerPhone)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
