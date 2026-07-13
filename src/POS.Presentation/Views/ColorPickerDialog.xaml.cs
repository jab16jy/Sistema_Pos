using System.Windows;
using System.Windows.Media;

namespace POS.Presentation.Views;

public partial class ColorPickerDialog : Window
{
    public string SelectedColorHex { get; private set; } = "#64748B";

    public ColorPickerDialog(string initialColorHex)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(initialColorHex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(initialColorHex);
                ColorCanvasControl.SelectedColor = color;
            }
            catch
            {
                ColorCanvasControl.SelectedColor = Colors.Gray;
            }
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (ColorCanvasControl.SelectedColor is { } color)
        {
            SelectedColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            DialogResult = true;
        }
        else
        {
            DialogResult = false;
        }
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
