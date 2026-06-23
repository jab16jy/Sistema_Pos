using System.Windows;
using System.Windows.Controls;

namespace POS.Presentation.Helpers;

public static class PasswordHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordHelper),
            new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static string GetBoundPassword(DependencyObject d) =>
        (string)d.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject d, string value) =>
        d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
            return;

        box.PasswordChanged -= PasswordChanged;

        if (e.NewValue is string newPassword && box.Password != newPassword)
            box.Password = newPassword;

        box.PasswordChanged += PasswordChanged;
    }

    private static void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            SetBoundPassword(box, box.Password);
    }
}
