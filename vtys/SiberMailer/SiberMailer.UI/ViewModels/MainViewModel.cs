using SiberMailer.Business.Services;
using SiberMailer.Core.Models;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// Navigation item for the sidebar.
/// </summary>
public class NavItem : ViewModelBase
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ViewKey { get; set; } = string.Empty;
    public Core.Enums.UserRole? RequiredRole { get; set; } // Minimum role required
    
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// ViewModel for the main application window.
/// Handles navigation and current user state.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private NavItem? _selectedNavItem;
    private ViewModelBase? _currentViewModel;
    private string _currentViewTitle = "Dashboard";

    public MainViewModel() : this(null, null) { }

    public MainViewModel(User? user, AuthService? authService)
    {
        _authService = authService ?? CreateDefaultAuthService();
        CurrentUser = user ?? _authService.CurrentUser;

        // Build navigation items based on user role
        var allNavItems = new List<NavItem>
        {
            new() { Name = "Dashboard", Icon = "📊", ViewKey = "Dashboard", IsSelected = true },
            new() { Name = "Contacts", Icon = "👥", ViewKey = "Contacts" },
            new() { Name = "Import", Icon = "📥", ViewKey = "Import", RequiredRole = Core.Enums.UserRole.Manager }, // Manager+ only
            new() { Name = "Campaigns", Icon = "📧", ViewKey = "Campaigns" },
            new() { Name = "Logs", Icon = "📋", ViewKey = "Logs", RequiredRole = Core.Enums.UserRole.Manager }, // Manager+ only
            new() { Name = "Templates", Icon = "📝", ViewKey = "Templates" },
            new() { Name = "SMTP Vault", Icon = "🔐", ViewKey = "SmtpVault", RequiredRole = Core.Enums.UserRole.Manager }, // Manager+ only
            new() { Name = "Admin Panel", Icon = "🛡️", ViewKey = "Admin Panel", RequiredRole = Core.Enums.UserRole.Admin }, // Admin only
            new() { Name = "Settings", Icon = "⚙️", ViewKey = "Settings" } // Always last, available to all
        };

        // Filter navigation items based on user role
        NavItems = allNavItems.Where(item => IsNavItemVisibleForRole(item, CurrentUser?.Role)).ToList();

        _selectedNavItem = NavItems[0];

        // Initialize commands
        NavigateCommand = new RelayCommand(ExecuteNavigate);
        LogoutCommand = new RelayCommand(_ => ExecuteLogout());
        MinimizeCommand = new RelayCommand(_ => MinimizeRequested?.Invoke());
        MaximizeCommand = new RelayCommand(_ => MaximizeRequested?.Invoke());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        // Register static navigation service for cross-ViewModel navigation
        NavigationService.NavigateAction = key => ExecuteNavigate(key);

        // Set initial view
        LoadView("Dashboard");
    }

    private static AuthService CreateDefaultAuthService()
    {
        var factory = new Data.DbConnectionFactory(Data.DbConnectionFactory.DefaultConnectionString);
        var userRepo = new Data.Repositories.UserRepository(factory);
        return new AuthService(userRepo);
    }

    #region Properties

    public User? CurrentUser { get; }

    public string UserDisplayName => CurrentUser?.FullName ?? "Guest";
    public string UserRole => CurrentUser?.Role.ToString() ?? "Unknown";
    public string UserInitials => GetInitials(CurrentUser?.FullName);

    public List<NavItem> NavItems { get; }

    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (_selectedNavItem != null)
                _selectedNavItem.IsSelected = false;

            if (SetProperty(ref _selectedNavItem, value) && value != null)
            {
                value.IsSelected = true;
                LoadView(value.ViewKey);
            }
        }
    }

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    #endregion

    #region Commands

    public ICommand NavigateCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeCommand { get; }
    public ICommand CloseCommand { get; }

    #endregion

    #region Events

    public event Action? LogoutRequested;
    public event Action? MinimizeRequested;
    public event Action? MaximizeRequested;
    public event Action? CloseRequested;

    #endregion

    #region Methods

    private void ExecuteNavigate(object? parameter)
    {
        string? viewKey = null;

        // Handle both NavItem objects (from Sidebar) and string keys (from Dashboard)
        if (parameter is string key)
        {
            viewKey = key;
        }
        else if (parameter is NavItem item)
        {
            viewKey = item.ViewKey;
        }

        if (!string.IsNullOrEmpty(viewKey))
        {
            // Find matching nav item (case-insensitive)
            var navItem = NavItems.FirstOrDefault(n => 
                string.Equals(n.ViewKey, viewKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n.Name, viewKey, StringComparison.OrdinalIgnoreCase));
            
            if (navItem != null)
            {
                SelectedNavItem = navItem;
            }
            else
            {
                // Direct load for views without nav items
                LoadView(viewKey);
            }
        }
    }

    private void LoadView(string viewKey)
    {
        CurrentViewTitle = viewKey;

        // TODO: Implement view switching with actual ViewModels
        // For now, we'll use the title to indicate the current view
        // CurrentViewModel = viewKey switch
        // {
        //     "Dashboard" => new DashboardViewModel(),
        //     "Contacts" => new ContactsViewModel(),
        //     "Campaigns" => new CampaignsViewModel(),
        //     _ => null
        // };
    }

    private void ExecuteLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "?";

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpper();

        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    /// <summary>
    /// Determines if a navigation item should be visible for the given user role.
    /// </summary>
    private static bool IsNavItemVisibleForRole(NavItem item, Core.Enums.UserRole? userRole)
    {
        // If no role required, item is visible to all
        if (item.RequiredRole == null)
            return true;

        // If user has no role, they see only unrestricted items
        if (userRole == null)
            return false;

        // Admin sees everything
        if (userRole == Core.Enums.UserRole.Admin)
            return true;

        // Manager sees Manager+ items (not Admin-only)
        if (userRole == Core.Enums.UserRole.Manager && item.RequiredRole != Core.Enums.UserRole.Admin)
            return true;

        // Member sees only unrestricted items
        return item.RequiredRole == null;
    }

    #endregion
}
