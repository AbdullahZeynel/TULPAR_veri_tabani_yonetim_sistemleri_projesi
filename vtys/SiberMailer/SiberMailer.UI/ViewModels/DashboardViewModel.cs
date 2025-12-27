using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// Static navigation service to enable cross-ViewModel navigation.
/// Solves the Frame visual tree isolation issue.
/// </summary>
public static class NavigationService
{
    public static Action<string>? NavigateAction { get; set; }
    
    public static void Navigate(string viewKey)
    {
        NavigateAction?.Invoke(viewKey);
    }
}

/// <summary>
/// Stat card model for dashboard display.
/// </summary>
public class StatCard : ViewModelBase
{
    private string _value = "0";

    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "AccentBrush";

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>
/// ViewModel for the Dashboard view.
/// </summary>
public class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly BranchRepository _branchRepository;
    private readonly System.Timers.Timer _refreshTimer;
    private bool _isLoading;
    private string _welcomeMessage = "Welcome back!";
    private string _lastUpdated = string.Empty;

    public DashboardViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _branchRepository = new BranchRepository(factory);

        // Initialize stat cards with EXPLICIT "0" defaults (no hardcoded fake numbers)
        StatCards = new List<StatCard>
        {
            new() { Title = "Total Contacts", Value = "0", Icon = "👥", ColorKey = "AccentBrush" },
            new() { Title = "Active Contacts", Value = "0", Icon = "✓", ColorKey = "SuccessBrush" },
            new() { Title = "Emails Sent", Value = "0", Icon = "📧", ColorKey = "InfoBrush" },
            new() { Title = "Sent Today", Value = "0", Icon = "📤", ColorKey = "SuccessBrush" },
            new() { Title = "Campaigns", Value = "0", Icon = "📊", ColorKey = "WarningBrush" },
            new() { Title = "Templates", Value = "0", Icon = "📝", ColorKey = "AccentBrush" },
            new() { Title = "SMTP Accounts", Value = "0", Icon = "🔐", ColorKey = "SuccessBrush" }
        };

        SecondaryStats = new List<StatCard>
        {
            new() { Title = "Bounced", Value = "0", Icon = "⚠️", ColorKey = "WarningBrush" },
            new() { Title = "Red Listed", Value = "0", Icon = "🚫", ColorKey = "ErrorBrush" },
            new() { Title = "Active Users", Value = "0", Icon = "👤", ColorKey = "InfoBrush" },
            new() { Title = "Active Campaigns", Value = "0", Icon = "▶️", ColorKey = "SuccessBrush" }
        };

        // Initialize commands
        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);
        NavigateCommand = new RelayCommand(ExecuteNavigate);

        // Load real stats from database on init
        _ = LoadStatsAsync();

        // Auto-refresh every 30 seconds
        _refreshTimer = new System.Timers.Timer(30000);
        _refreshTimer.Elapsed += async (s, e) => await LoadStatsAsync();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    /// <summary>
    /// Public method to refresh stats. Called by View on visibility change.
    /// </summary>
    public Task RefreshAsync() => LoadStatsAsync();

    private void ExecuteNavigate(object? parameter)
    {
        if (parameter is string viewKey)
        {
            NavigationService.Navigate(viewKey);
        }
    }

    #region Properties

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public List<StatCard> StatCards { get; }
    public List<StatCard> SecondaryStats { get; }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }

    #endregion

    #region Methods

    private async Task LoadStatsAsync()
    {
        IsLoading = true;

        try
        {
            var stats = await _branchRepository.GetDashboardStatsAsync();

            // Update main stat cards
            StatCards[0].Value = FormatNumber(stats.TotalContacts);
            StatCards[1].Value = FormatNumber(stats.TotalActiveContacts);
            StatCards[2].Value = FormatNumber(stats.TotalEmailsSent);
            StatCards[3].Value = FormatNumber(stats.SentToday);
            StatCards[4].Value = stats.TotalCampaigns.ToString();
            StatCards[5].Value = stats.TotalTemplates.ToString();
            StatCards[6].Value = stats.ActiveSmtpAccounts.ToString();

            // Update secondary stats
            SecondaryStats[0].Value = stats.BouncedContacts.ToString();
            SecondaryStats[1].Value = stats.RedListedContacts.ToString();
            SecondaryStats[2].Value = stats.ActiveUsers.ToString();
            SecondaryStats[3].Value = stats.ActiveCampaigns.ToString();

            LastUpdated = $"Last updated: {DateTime.Now:HH:mm:ss}";
            WelcomeMessage = "Dashboard Overview"; // Reset message on success
        }
        catch (Exception ex)
        {
            // Show detailed error in UI
            WelcomeMessage = $"⚠️ Database Error";
            LastUpdated = $"Error: {ex.Message}";
            
            // Log to console for debugging
            System.Diagnostics.Debug.WriteLine($"Dashboard Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:0.#}M",
            >= 1_000 => $"{number / 1_000.0:0.#}K",
            _ => number.ToString("N0")
        };
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }

    #endregion
}
