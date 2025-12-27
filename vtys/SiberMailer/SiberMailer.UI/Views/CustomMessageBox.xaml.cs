using System.Windows;
using System.Windows.Media;

namespace SiberMailer.UI.Views;

/// <summary>
/// A custom themed message box dialog that matches the SiberMailer 2.0 design.
/// Supports Error, Warning, Info, and Success message types.
/// </summary>
public partial class CustomMessageBox : Window
{
    /// <summary>
    /// Defines the type of message to display, affecting icon and styling.
    /// </summary>
    public enum MessageType
    {
        Error,
        Warning,
        Info,
        Success
    }

    public CustomMessageBox()
    {
        InitializeComponent();
        // Enable window dragging
        MouseLeftButtonDown += (s, e) => DragMove();
    }

    /// <summary>
    /// Shows a custom message box with the specified parameters.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">The dialog title</param>
    /// <param name="type">The type of message (affects icon and colors)</param>
    /// <param name="owner">Optional owner window for centering</param>
    public static void Show(string message, string title, MessageType type = MessageType.Info, Window? owner = null)
    {
        var dialog = new CustomMessageBox();
        dialog.SetMessageType(type);
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;

        if (owner != null)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();
    }

    /// <summary>
    /// Convenience method to show an error message.
    /// </summary>
    public static void ShowError(string message, string title = "Error", Window? owner = null)
    {
        Show(message, title, MessageType.Error, owner);
    }

    /// <summary>
    /// Convenience method to show a warning message.
    /// </summary>
    public static void ShowWarning(string message, string title = "Warning", Window? owner = null)
    {
        Show(message, title, MessageType.Warning, owner);
    }

    /// <summary>
    /// Convenience method to show an info message.
    /// </summary>
    public static void ShowInfo(string message, string title = "Info", Window? owner = null)
    {
        Show(message, title, MessageType.Info, owner);
    }

    /// <summary>
    /// Convenience method to show a success message.
    /// </summary>
    public static void ShowSuccess(string message, string title = "Success", Window? owner = null)
    {
        Show(message, title, MessageType.Success, owner);
    }

    /// <summary>
    /// Shows a confirmation dialog and returns the result.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">The dialog title</param>
    /// <param name="owner">Optional owner window for centering</param>
    /// <returns>True if user clicked OK, false otherwise</returns>
    public static bool? ShowConfirmation(string message, string title = "Confirm", Window? owner = null)
    {
        return MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    }

    /// <summary>
    /// Deletion action options.
    /// </summary>
    public enum DeleteAction
    {
        Cancel,
        SoftDelete,
        HardDelete
    }

    /// <summary>
    /// Shows a delete confirmation dialog with options for soft or hard delete.
    /// </summary>
    /// <param name="itemName">The name of the item being deleted</param>
    /// <param name="title">The dialog title</param>
    /// <returns>DeleteAction indicating user's choice</returns>
    public static DeleteAction ShowDeleteConfirmation(string itemName, string title = "Confirm Delete")
    {
        var result = MessageBox.Show(
            $"How would you like to delete '{itemName}'?\n\n" +
            $"• Yes = Soft Delete (mark as inactive)\n" +
            $"• No = Hard Delete (permanently remove)\n" +
            $"• Cancel = Don't delete",
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => DeleteAction.SoftDelete,
            MessageBoxResult.No => DeleteAction.HardDelete,
            _ => DeleteAction.Cancel
        };
    }

    /// <summary>
    /// Configures the visual appearance based on the message type.
    /// </summary>
    private void SetMessageType(MessageType type)
    {
        string icon;
        Brush iconBackground;
        Brush iconForeground;

        switch (type)
        {
            case MessageType.Error:
                icon = "✕";
                iconBackground = (Brush)FindResource("ErrorBrush");
                iconForeground = Brushes.White;
                break;
            case MessageType.Warning:
                icon = "⚠";
                iconBackground = (Brush)FindResource("WarningBrush");
                iconForeground = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12));
                break;
            case MessageType.Success:
                icon = "✓";
                iconBackground = (Brush)FindResource("SuccessBrush");
                iconForeground = Brushes.White;
                break;
            case MessageType.Info:
            default:
                icon = "ℹ";
                iconBackground = (Brush)FindResource("InfoBrush");
                iconForeground = Brushes.White;
                break;
        }

        IconText.Text = icon;
        IconText.Foreground = iconForeground;
        IconContainer.Background = iconBackground;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
