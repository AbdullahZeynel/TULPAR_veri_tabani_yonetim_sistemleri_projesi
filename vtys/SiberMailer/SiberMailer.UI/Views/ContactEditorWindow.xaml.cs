using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for ContactEditorWindow.xaml
/// </summary>
public partial class ContactEditorWindow : Window
{
    public ContactEditorWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
