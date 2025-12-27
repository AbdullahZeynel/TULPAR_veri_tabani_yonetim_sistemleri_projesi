using System.Windows;

namespace SiberMailer.UI.Views;

/// <summary>
/// Dialog for entering SMTP account PIN.
/// </summary>
public partial class PinDialog : Window
{
    public string? EnteredPin { get; private set; }
    public bool IsUnlocked { get; private set; }

    public PinDialog()
    {
        InitializeComponent();
        PinBox.Focus();
        
        // Allow Enter key to submit
        PinBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                Unlock_Click(s, e);
        };

        // Allow dragging
        MouseLeftButtonDown += (s, e) => DragMove();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PinBox.Password))
        {
            ShowError("Please enter a PIN.");
            return;
        }

        EnteredPin = PinBox.Password;
        IsUnlocked = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        IsUnlocked = false;
        DialogResult = false;
        Close();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        PinBox.Password = string.Empty;
        PinBox.Focus();
    }
}
