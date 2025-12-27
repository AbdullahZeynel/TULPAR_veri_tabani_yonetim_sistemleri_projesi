using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for TemplatesView.xaml
/// </summary>
public partial class TemplatesView : UserControl, IRefreshable
{
    public TemplatesView()
    {
        InitializeComponent();
        
        // Auto-refresh on visibility change
        IsVisibleChanged += OnVisibilityChanged;
    }

    /// <summary>
    /// Refreshes the templates data from the database.
    /// </summary>
    public async Task RefreshData()
    {
        if (DataContext is TemplatesViewModel vm)
        {
            await vm.LoadTemplatesAsync();
        }
    }

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            await RefreshData();
        }
    }
}

/// <summary>
/// Attached property to bind HTML content to a WebBrowser control.
/// </summary>
public static class WebBrowserBehavior
{
    public static readonly DependencyProperty HtmlProperty =
        DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(WebBrowserBehavior),
            new FrameworkPropertyMetadata(OnHtmlChanged));

    public static string GetHtml(DependencyObject obj)
    {
        return (string)obj.GetValue(HtmlProperty);
    }

    public static void SetHtml(DependencyObject obj, string value)
    {
        obj.SetValue(HtmlProperty, value);
    }

    private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WebBrowser browser)
        {
            var html = e.NewValue as string;
            
            if (string.IsNullOrEmpty(html))
            {
                // Navigate to blank page if no content
                browser.NavigateToString("<html><body style='background:#1E1E1E;color:#999;font-family:Segoe UI;text-align:center;padding-top:50px;'><p>No HTML content</p></body></html>");
            }
            else
            {
                // Wrap content with dark theme styling if needed
                if (!html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase) &&
                    !html.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                {
                    // Wrap partial HTML in a document with dark theme background
                    html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            background: #1E1E1E; 
            color: #FFFFFF; 
            font-family: Segoe UI, sans-serif;
            padding: 20px;
        }}
    </style>
</head>
<body>
{html}
</body>
</html>";
                }
                
                browser.NavigateToString(html);
            }
        }
    }
}
