using SiberMailer.Business.Services;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the SMTP Configuration View.
/// Manages SMTP account creation with PIN encryption.
/// </summary>
public class SmtpConfigViewModel : ViewModelBase
{
    private readonly SmtpVaultService _smtpVaultService;

    // Form fields
    private string _accountName = string.Empty;
    private string _smtpHost = string.Empty;
    private int _smtpPort = 587;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _useSsl = true;
    private int _dailyLimit = 500;

    private SmtpAccount? _selectedAccount;
    private bool _isLoading;
    private string _statusMessage = "Configure your SMTP accounts";
    private bool _isEditing;

    public SmtpConfigViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        var smtpRepo = new SmtpAccountRepository(factory);
        var cryptoService = new CryptoService();
        _smtpVaultService = new SmtpVaultService(smtpRepo, cryptoService);

        SmtpAccounts = new ObservableCollection<SmtpAccount>();

        // Commands
        RefreshCommand = new AsyncRelayCommand(LoadAccountsAsync);
        SaveSecurelyCommand = new RelayCommand(_ => ExecuteSaveSecurely(), _ => CanSave());
        NewAccountCommand = new RelayCommand(_ => StartNewAccount());
        DeleteAccountCommand = new AsyncRelayCommand(DeleteSelectedAccountAsync, () => SelectedAccount != null);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => CanSave());

        // Load initial data
        _ = LoadAccountsAsync();
    }

    #region Properties

    public ObservableCollection<SmtpAccount> SmtpAccounts { get; }

    public SmtpAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value) && value != null)
            {
                // Load account details into form (except password)
                AccountName = value.AccountName;
                SmtpHost = value.SmtpHost;
                SmtpPort = value.SmtpPort;
                UseSsl = value.UseSsl;
                DailyLimit = value.DailyLimit;
                Email = string.Empty; // Can't show encrypted
                Password = string.Empty;
                IsEditing = true;
                StatusMessage = $"Editing '{value.AccountName}' - Enter PIN to update credentials";
            }
        }
    }

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public string SmtpHost
    {
        get => _smtpHost;
        set => SetProperty(ref _smtpHost, value);
    }

    public int SmtpPort
    {
        get => _smtpPort;
        set => SetProperty(ref _smtpPort, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool UseSsl
    {
        get => _useSsl;
        set => SetProperty(ref _useSsl, value);
    }

    public int DailyLimit
    {
        get => _dailyLimit;
        set => SetProperty(ref _dailyLimit, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand SaveSecurelyCommand { get; }
    public ICommand NewAccountCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand TestConnectionCommand { get; }

    #endregion

    #region Methods

    public async Task LoadAccountsAsync()
    {
        IsLoading = true;
        try
        {
            var accounts = await _smtpVaultService.GetAllAccountsAsync();
            SmtpAccounts.Clear();
            foreach (var account in accounts)
            {
                SmtpAccounts.Add(account);
            }
            StatusMessage = $"Loaded {SmtpAccounts.Count} SMTP account(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading accounts: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartNewAccount()
    {
        SelectedAccount = null;
        AccountName = string.Empty;
        SmtpHost = string.Empty;
        SmtpPort = 587;
        Email = string.Empty;
        Password = string.Empty;
        UseSsl = true;
        DailyLimit = 500;
        IsEditing = false;
        StatusMessage = "Enter new SMTP account details";
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(AccountName) &&
               !string.IsNullOrWhiteSpace(SmtpHost) &&
               SmtpPort > 0 &&
               !string.IsNullOrWhiteSpace(Email) &&
               !string.IsNullOrWhiteSpace(Password);
    }

    private void ExecuteSaveSecurely()
    {
        // Show PIN dialog
        var dialog = new PinDialog
        {
            Owner = Application.Current.MainWindow
        };

        var result = dialog.ShowDialog();

        if (result != true || string.IsNullOrEmpty(dialog.EnteredPin))
        {
            StatusMessage = "⚠️ Save cancelled - PIN required for encryption.";
            return;
        }

        if (dialog.EnteredPin.Length < 4)
        {
            StatusMessage = "⚠️ PIN must be at least 4 characters.";
            return;
        }

        // Save with PIN encryption
        _ = SaveAccountWithPinAsync(dialog.EnteredPin);
    }

    private async Task SaveAccountWithPinAsync(string pin)
    {
        IsLoading = true;
        try
        {
            StatusMessage = "🔐 Encrypting and saving...";

            var dto = new CreateSmtpAccountDto
            {
                AccountName = AccountName,
                SmtpHost = SmtpHost,
                SmtpPort = SmtpPort,
                UseSsl = UseSsl,
                Email = Email,
                Password = Password,
                Pin = pin,
                DailyLimit = DailyLimit,
                OwnerBranchId = 1, // Would come from current user's branch
                IsShared = false
            };

            var accountId = await _smtpVaultService.CreateAccountAsync(dto);

            if (accountId > 0)
            {
                StatusMessage = $"✅ SMTP account '{AccountName}' saved securely!";
                
                // Clear form and refresh list
                StartNewAccount();
                await LoadAccountsAsync();
            }
            else
            {
                StatusMessage = "❌ Failed to save account.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error saving: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteSelectedAccountAsync()
    {
        if (SelectedAccount == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedAccount.AccountName}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            StatusMessage = $"Deleting '{SelectedAccount.AccountName}'...";
            
            var success = await _smtpVaultService.DeactivateAccountAsync(SelectedAccount.SmtpAccountId);
            
            if (success)
            {
                StatusMessage = $"✅ Account deleted.";
                StartNewAccount();
                await LoadAccountsAsync();
            }
            else
            {
                StatusMessage = "❌ Failed to delete account.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        IsLoading = true;
        try
        {
            StatusMessage = "🔄 Testing connection...";

            // Create a temporary config for testing
            var testConfig = new DecryptedSmtpConfig
            {
                SmtpHost = SmtpHost,
                SmtpPort = SmtpPort,
                UseSsl = UseSsl,
                Email = Email,
                Password = Password
            };

            var mailService = new MailService();
            var testResult = await mailService.ValidateConnectionAsync(testConfig);

            StatusMessage = testResult.Success 
                ? "✅ Connection successful!" 
                : $"❌ Connection failed: {testResult.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Test failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
