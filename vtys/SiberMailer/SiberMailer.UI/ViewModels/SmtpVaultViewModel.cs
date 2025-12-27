using SiberMailer.Business.Services;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the SMTP Vault view.
/// Manages encrypted SMTP accounts with full CRUD, statistics, and audit trails.
/// </summary>
public class SmtpVaultViewModel : ViewModelBase
{
    private readonly SmtpAccountRepository _repository;
    private readonly SmtpVaultService _vaultService;
    private readonly CryptoService _cryptoService;

    // Collections
    private SmtpAccount? _selectedAccount;
    private bool _isLoading;
    private string _statusMessage = "Secure SMTP Vault - Your credentials are AES-256 encrypted.";

    // Dialog fields
    private bool _showAccountDialog;
    private bool _isEditMode;
    private int _editingAccountId;
    private string _dialogAccountName = string.Empty;
    private string _dialogSmtpHost = string.Empty;
    private int _dialogSmtpPort = 587;
    private bool _dialogUseSsl = true;
    private string _dialogEmail = string.Empty;
    private string _dialogPassword = string.Empty;
    private string _dialogPin = string.Empty;
    private int _dialogDailyLimit = 500;
    private bool _dialogIsShared = false;

    public SmtpVaultViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _repository = new SmtpAccountRepository(factory);
        _cryptoService = new CryptoService();
        _vaultService = new SmtpVaultService(_repository, _cryptoService);

        Accounts = new ObservableCollection<SmtpAccount>();

        // Commands
        RefreshCommand = new AsyncRelayCommand(LoadAccountsAsync);
        AddAccountCommand = new RelayCommand(_ => ShowAddDialog());
        EditAccountCommand = new RelayCommand(_ => ShowEditDialog(), _ => SelectedAccount != null);
        SaveAccountCommand = new AsyncRelayCommand(SaveAccountAsync, CanSaveAccount);
        CancelDialogCommand = new RelayCommand(_ => HideDialog());
        ToggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync, () => SelectedAccount != null);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync, () => SelectedAccount != null);
        
        // Row-specific commands for DataGrid
        EditRowCommand = new RelayCommand(account => EditAccount(account as SmtpAccount));
        DeleteRowCommand = new AsyncRelayCommand<SmtpAccount>(DeleteAccountWithConfirmAsync);

        // Load initial data
        _ = LoadAccountsAsync();
    }

    #region Properties

    public ObservableCollection<SmtpAccount> Accounts { get; }

    public SmtpAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(HasSelectedAccount));
            }
        }
    }

    public bool HasSelectedAccount => SelectedAccount != null;

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

    // Dialog Properties
    public bool ShowAccountDialog
    {
        get => _showAccountDialog;
        set => SetProperty(ref _showAccountDialog, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(DialogTitle));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(ShowPasswordFields));
            }
        }
    }

    public string DialogTitle => IsEditMode ? "Edit SMTP Account" : "Add Secure SMTP Account";
    public string SaveButtonText => IsEditMode ? "Update Account" : "Save Securely";
    public bool ShowPasswordFields => !IsEditMode; // Only show password fields for new accounts

    public string DialogAccountName
    {
        get => _dialogAccountName;
        set => SetProperty(ref _dialogAccountName, value);
    }

    public string DialogSmtpHost
    {
        get => _dialogSmtpHost;
        set => SetProperty(ref _dialogSmtpHost, value);
    }

    public int DialogSmtpPort
    {
        get => _dialogSmtpPort;
        set => SetProperty(ref _dialogSmtpPort, value);
    }

    public bool DialogUseSsl
    {
        get => _dialogUseSsl;
        set => SetProperty(ref _dialogUseSsl, value);
    }

    public string DialogEmail
    {
        get => _dialogEmail;
        set => SetProperty(ref _dialogEmail, value);
    }

    public string DialogPassword
    {
        get => _dialogPassword;
        set => SetProperty(ref _dialogPassword, value);
    }

    public string DialogPin
    {
        get => _dialogPin;
        set => SetProperty(ref _dialogPin, value);
    }

    public int DialogDailyLimit
    {
        get => _dialogDailyLimit;
        set => SetProperty(ref _dialogDailyLimit, value);
    }

    public bool DialogIsShared
    {
        get => _dialogIsShared;
        set => SetProperty(ref _dialogIsShared, value);
    }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand EditAccountCommand { get; }
    public ICommand SaveAccountCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand ToggleStatusCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand EditRowCommand { get; }
    public ICommand DeleteRowCommand { get; }

    #endregion

    #region Methods

    public async Task LoadAccountsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading SMTP accounts...";

        try
        {
            // Try to load from stats view first, fall back to regular table
            IEnumerable<SmtpAccount> accounts;
            try
            {
                accounts = await _repository.GetAllWithStatsAsync(activeOnly: false);
            }
            catch
            {
                // View doesn't exist yet, use regular query
                accounts = await _repository.GetAllAsync(activeOnly: false);
            }

            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            StatusMessage = $"🔐 {Accounts.Count} secure SMTP account(s) loaded.";
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

    private void ShowAddDialog()
    {
        IsEditMode = false;
        _editingAccountId = 0;
        DialogAccountName = string.Empty;
        DialogSmtpHost = string.Empty;
        DialogSmtpPort = 587;
        DialogUseSsl = true;
        DialogEmail = string.Empty;
        DialogPassword = string.Empty;
        DialogPin = string.Empty;
        DialogDailyLimit = 500;
        DialogIsShared = false;
        ShowAccountDialog = true;
    }

    private void ShowEditDialog()
    {
        if (SelectedAccount == null) return;
        EditAccount(SelectedAccount);
    }

    private void EditAccount(SmtpAccount? account)
    {
        if (account == null) return;

        IsEditMode = true;
        _editingAccountId = account.SmtpAccountId;
        DialogAccountName = account.AccountName;
        DialogSmtpHost = account.SmtpHost;
        DialogSmtpPort = account.SmtpPort;
        DialogUseSsl = account.UseSsl;
        DialogEmail = string.Empty; // Don't show encrypted email
        DialogPassword = string.Empty; // Don't show encrypted password
        DialogPin = string.Empty;
        DialogDailyLimit = account.DailyLimit;
        DialogIsShared = account.IsShared;
        ShowAccountDialog = true;
    }

    private void HideDialog()
    {
        ShowAccountDialog = false;
        DialogAccountName = string.Empty;
        DialogSmtpHost = string.Empty;
        DialogEmail = string.Empty;
        DialogPassword = string.Empty;
        DialogPin = string.Empty;
    }

    private bool CanSaveAccount()
    {
        if (string.IsNullOrWhiteSpace(DialogAccountName)) return false;
        if (string.IsNullOrWhiteSpace(DialogSmtpHost)) return false;
        if (DialogSmtpPort <= 0) return false;

        if (!IsEditMode)
        {
            // New account requires email, password, and PIN
            if (string.IsNullOrWhiteSpace(DialogEmail)) return false;
            if (string.IsNullOrWhiteSpace(DialogPassword)) return false;
            if (string.IsNullOrWhiteSpace(DialogPin) || DialogPin.Length < 4) return false;
        }

        return true;
    }

    private async Task SaveAccountAsync()
    {
        if (!CanSaveAccount()) return;

        IsLoading = true;
        try
        {
            if (IsEditMode)
            {
                // Update existing account (non-credential fields only)
                StatusMessage = "Updating account...";

                var account = await _repository.GetByIdAsync(_editingAccountId);
                if (account == null)
                {
                    StatusMessage = "❌ Account not found.";
                    return;
                }

                account.AccountName = DialogAccountName.Trim();
                account.SmtpHost = DialogSmtpHost.Trim();
                account.SmtpPort = DialogSmtpPort;
                account.UseSsl = DialogUseSsl;
                account.DailyLimit = DialogDailyLimit;
                account.IsShared = DialogIsShared;

                var success = await _repository.UpdateAsync(account);

                // If new credentials provided, re-encrypt them
                if (!string.IsNullOrWhiteSpace(DialogEmail) && 
                    !string.IsNullOrWhiteSpace(DialogPassword) &&
                    !string.IsNullOrWhiteSpace(DialogPin))
                {
                    await _vaultService.UpdateCredentialsAsync(
                        _editingAccountId, 
                        DialogEmail.Trim(), 
                        DialogPassword, 
                        DialogPin);
                }

                if (success)
                {
                    StatusMessage = $"✅ Account '{DialogAccountName}' updated.";
                    HideDialog();
                    await LoadAccountsAsync();
                }
                else
                {
                    StatusMessage = "❌ Failed to update account.";
                }
            }
            else
            {
                // Create new account with encrypted credentials
                StatusMessage = "Encrypting and saving account...";

                var dto = new CreateSmtpAccountDto
                {
                    AccountName = DialogAccountName.Trim(),
                    SmtpHost = DialogSmtpHost.Trim(),
                    SmtpPort = DialogSmtpPort,
                    UseSsl = DialogUseSsl,
                    Email = DialogEmail.Trim(),
                    Password = DialogPassword,
                    Pin = DialogPin,
                    DailyLimit = DialogDailyLimit,
                    IsShared = DialogIsShared,
                    OwnerBranchId = 1 // TODO: Get from current user's branch
                };

                var accountId = await _vaultService.CreateAccountAsync(dto);

                if (accountId > 0)
                {
                    StatusMessage = $"✅ Account '{DialogAccountName}' saved securely with AES-256 encryption!";
                    HideDialog();
                    await LoadAccountsAsync();
                }
                else
                {
                    StatusMessage = "❌ Failed to save account.";
                }
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

    private async Task ToggleStatusAsync()
    {
        if (SelectedAccount == null) return;

        IsLoading = true;
        try
        {
            var newStatus = !SelectedAccount.IsActive;
            var statusText = newStatus ? "enabled" : "disabled";

            var success = await _repository.ToggleStatusAsync(SelectedAccount.SmtpAccountId);

            if (success)
            {
                StatusMessage = $"✅ Account {statusText}.";
                await LoadAccountsAsync();
            }
            else
            {
                StatusMessage = "❌ Failed to update status.";
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

    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount == null) return;
        await DeleteAccountWithConfirmAsync(SelectedAccount);
    }

    private async Task DeleteAccountWithConfirmAsync(SmtpAccount? account)
    {
        if (account == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{account.AccountName}'?\n\n⚠️ This will permanently remove the encrypted credentials.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            var success = await _vaultService.DeleteAccountAsync(account.SmtpAccountId);

            if (success)
            {
                StatusMessage = $"✅ Account '{account.AccountName}' deleted.";
                
                if (SelectedAccount?.SmtpAccountId == account.SmtpAccountId)
                {
                    SelectedAccount = null;
                }
                
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

    #endregion
}
