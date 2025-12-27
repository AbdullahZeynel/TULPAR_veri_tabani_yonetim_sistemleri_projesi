using System.Windows;
using System.Windows.Controls;
using SiberMailer.UI.ViewModels;

namespace SiberMailer.UI.Views;

public partial class AdminPanelView : UserControl
{
    public AdminPanelView()
    {
        InitializeComponent();
    }

    private void UserPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminPanelViewModel viewModel)
        {
            viewModel.UserPassword = ((PasswordBox)sender).Password;
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
