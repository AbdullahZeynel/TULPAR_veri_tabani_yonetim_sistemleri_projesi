using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using SiberMailer.UI.Views;
using static SiberMailer.UI.Views.CustomMessageBox;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Admin Panel - User and Branch management.
/// Admin-only feature for CRUD operations.
/// </summary>
public class AdminPanelViewModel : ViewModelBase
{
    private readonly UserRepository _userRepository;
    private readonly BranchRepository _branchRepository;

    private int _selectedTabIndex;
    private User? _selectedUser;
    private Branch? _selectedBranch;
    private bool _isLoading;
    private string _statusMessage = "Admin Panel Ready";
    private bool _isEditMode;

    // User form fields
    private string _userUsername = string.Empty;
    private string _userEmail = string.Empty;
    private string _userFullName = string.Empty;
    private string _userPassword = string.Empty;
    private UserRole _userRole = UserRole.Member;
    private bool _userIsActive = true;

    // Branch form fields
    private string _branchCode = string.Empty;
    private string _branchName = string.Empty;
    private string _branchDescription = string.Empty;
    private bool _branchIsActive = true;

    public AdminPanelViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _userRepository = new UserRepository(factory);
        _branchRepository = new BranchRepository(factory);

        Users = new ObservableCollection<User>();
        Branches = new ObservableCollection<Branch>();
        UserRoles = new ObservableCollection<UserRole> { UserRole.Member, UserRole.Manager, UserRole.Admin };

        // Commands
        RefreshUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
        RefreshBranchesCommand = new AsyncRelayCommand(LoadBranchesAsync);
        NewUserCommand = new RelayCommand(_ => StartNewUser());
        NewBranchCommand = new RelayCommand(_ => StartNewBranch());
        SaveUserCommand = new AsyncRelayCommand(SaveUserAsync, () => CanSaveUser());
        SaveBranchCommand = new AsyncRelayCommand(SaveBranchAsync, () => CanSaveBranch());
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync, () => SelectedUser != null);
        DeleteBranchCommand = new AsyncRelayCommand(DeleteBranchAsync, () => SelectedBranch != null);
        CancelUserEditCommand = new RelayCommand(_ => CancelUserEdit());
        CancelBranchEditCommand = new RelayCommand(_ => CancelBranchEdit());

        // Load initial data
        _ = LoadUsersAsync();
        _ = LoadBranchesAsync();
    }

    #region Properties

    public ObservableCollection<User> Users { get; }
    public ObservableCollection<Branch> Branches { get; }
    public ObservableCollection<UserRole> UserRoles { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value != null)
            {
                LoadUserToForm(value);
            }
        }
    }

    public Branch? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (SetProperty(ref _selectedBranch, value) && value != null)
            {
                LoadBranchToForm(value);
            }
        }
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

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    // User form properties
    public string UserUsername { get => _userUsername; set => SetProperty(ref _userUsername, value); }
    public string UserEmail { get => _userEmail; set => SetProperty(ref _userEmail, value); }
    public string UserFullName { get => _userFullName; set => SetProperty(ref _userFullName, value); }
    public string UserPassword { get => _userPassword; set => SetProperty(ref _userPassword, value); }
    public UserRole UserRole { get => _userRole; set => SetProperty(ref _userRole, value); }
    public bool UserIsActive { get => _userIsActive; set => SetProperty(ref _userIsActive, value); }

    // Branch form properties
    public string BranchCode { get => _branchCode; set => SetProperty(ref _branchCode, value); }
    public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }
    public string BranchDescription { get => _branchDescription; set => SetProperty(ref _branchDescription, value); }
    public bool BranchIsActive { get => _branchIsActive; set => SetProperty(ref _branchIsActive, value); }

    #endregion

    #region Commands

    public ICommand RefreshUsersCommand { get; }
    public ICommand RefreshBranchesCommand { get; }
    public ICommand NewUserCommand { get; }
    public ICommand NewBranchCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand SaveBranchCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand DeleteBranchCommand { get; }
    public ICommand CancelUserEditCommand { get; }
    public ICommand CancelBranchEditCommand { get; }

    #endregion

    #region User Management

    private async Task LoadUsersAsync()
    {
        IsLoading = true;
        try
        {
            var users = await _userRepository.GetAllUsersAsync();
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
            StatusMessage = $"✅ Loaded {Users.Count} users";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading users: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartNewUser()
    {
        IsEditMode = false;
        SelectedUser = null;
        UserUsername = string.Empty;
        UserEmail = string.Empty;
        UserFullName = string.Empty;
        UserPassword = string.Empty;
        UserRole = UserRole.Member;
        UserIsActive = true;
        StatusMessage = "➕ Creating new user";
    }

    private void LoadUserToForm(User user)
    {
        IsEditMode = true;
        UserUsername = user.Username;
        UserEmail = user.Email;
        UserFullName = user.FullName;
        UserPassword = string.Empty; // Don't load password
        UserRole = user.Role;
        UserIsActive = user.IsActive;
        StatusMessage = $"✏️ Editing user: {user.Username}";
    }

    private bool CanSaveUser()
    {
        return !string.IsNullOrWhiteSpace(UserUsername) &&
               !string.IsNullOrWhiteSpace(UserEmail) &&
               !string.IsNullOrWhiteSpace(UserFullName) &&
               (IsEditMode || !string.IsNullOrWhiteSpace(UserPassword));
    }

    private async Task SaveUserAsync()
    {
        IsLoading = true;
        try
        {
            if (IsEditMode && SelectedUser != null)
            {
                // Update existing user
                SelectedUser.Username = UserUsername;
                SelectedUser.Email = UserEmail;
                SelectedUser.FullName = UserFullName;
                SelectedUser.Role = UserRole;
                SelectedUser.IsActive = UserIsActive;

                await _userRepository.UpdateUserAsync(SelectedUser);

                // Update password if provided
                if (!string.IsNullOrWhiteSpace(UserPassword))
                {
                    var hashedPassword = UserRepository.HashPassword(UserPassword);
                    await _userRepository.UpdatePasswordAsync(SelectedUser.UserId, hashedPassword);
                }

                StatusMessage = $"✅ User '{UserUsername}' updated successfully";
            }
            else
            {
                // Create new user
                var newUser = new User
                {
                    Username = UserUsername,
                    Email = UserEmail,
                    FullName = UserFullName,
                    PasswordHash = UserRepository.HashPassword(UserPassword),
                    Role = UserRole,
                    IsActive = UserIsActive
                };

                await _userRepository.CreateUserAsync(newUser);
                StatusMessage = $"✅ User '{UserUsername}' created successfully";
            }

            await LoadUsersAsync();
            CancelUserEdit();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error saving user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null) return;

        var deleteAction = CustomMessageBox.ShowDeleteConfirmation(
            SelectedUser.Username,
            "Delete User");

        if (deleteAction == DeleteAction.Cancel) return;

        IsLoading = true;
        try
        {
            bool success;
            if (deleteAction == DeleteAction.HardDelete)
            {
                success = await _userRepository.HardDeleteUserAsync(SelectedUser.UserId);
                StatusMessage = success
                    ? $"✅ User '{SelectedUser.Username}' permanently deleted"
                    : "❌ Failed to delete user";
            }
            else // Soft delete
            {
                success = await _userRepository.DeleteUserAsync(SelectedUser.UserId);
                StatusMessage = success
                    ? $"✅ User '{SelectedUser.Username}' marked as inactive"
                    : "❌ Failed to deactivate user";
            }

            if (success)
            {
                await LoadUsersAsync();
                CancelUserEdit();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelUserEdit()
    {
        IsEditMode = false;
        SelectedUser = null;
        UserUsername = string.Empty;
        UserEmail = string.Empty;
        UserFullName = string.Empty;
        UserPassword = string.Empty;
        UserRole = UserRole.Member;
        UserIsActive = true;
    }

    #endregion

    #region Branch Management

    private async Task LoadBranchesAsync()
    {
        IsLoading = true;
        try
        {
            var branches = await _branchRepository.GetAllBranchesAsync();
            Branches.Clear();
            foreach (var branch in branches)
            {
                Branches.Add(branch);
            }
            StatusMessage = $"✅ Loaded {Branches.Count} branches";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading branches: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartNewBranch()
    {
        IsEditMode = false;
        SelectedBranch = null;
        BranchCode = string.Empty;
        BranchName = string.Empty;
        BranchDescription = string.Empty;
        BranchIsActive = true;
        StatusMessage = "➕ Creating new branch";
    }

    private void LoadBranchToForm(Branch branch)
    {
        IsEditMode = true;
        BranchCode = branch.BranchCode;
        BranchName = branch.BranchName;
        BranchDescription = branch.Description ?? string.Empty;
        BranchIsActive = branch.IsActive;
        StatusMessage = $"✏️ Editing branch: {branch.BranchName}";
    }

    private bool CanSaveBranch()
    {
        return !string.IsNullOrWhiteSpace(BranchCode) &&
               !string.IsNullOrWhiteSpace(BranchName);
    }

    private async Task SaveBranchAsync()
    {
        IsLoading = true;
        try
        {
            if (IsEditMode && SelectedBranch != null)
            {
                // Update existing branch
                SelectedBranch.BranchCode = BranchCode;
                SelectedBranch.BranchName = BranchName;
                SelectedBranch.Description = BranchDescription;
                SelectedBranch.IsActive = BranchIsActive;

                await _branchRepository.UpdateBranchAsync(SelectedBranch);
                StatusMessage = $"✅ Branch '{BranchName}' updated successfully";
            }
            else
            {
                // Create new branch
                var newBranch = new Branch
                {
                    BranchCode = BranchCode,
                    BranchName = BranchName,
                    Description = BranchDescription,
                    IsActive = BranchIsActive
                };

                await _branchRepository.CreateBranchAsync(newBranch);
                StatusMessage = $"✅ Branch '{BranchName}' created successfully";
            }

            await LoadBranchesAsync();
            CancelBranchEdit();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error saving branch: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteBranchAsync()
    {
        if (SelectedBranch == null) return;

        var deleteAction = CustomMessageBox.ShowDeleteConfirmation(
            SelectedBranch.BranchName,
            "Delete Branch");

        if (deleteAction == DeleteAction.Cancel) return;

        IsLoading = true;
        try
        {
            bool success;
            if (deleteAction == DeleteAction.HardDelete)
            {
                success = await _branchRepository.HardDeleteBranchAsync(SelectedBranch.BranchId);
                StatusMessage = success
                    ? $"✅ Branch '{SelectedBranch.BranchName}' permanently deleted"
                    : "❌ Failed to delete branch";
            }
            else // Soft delete
            {
                success = await _branchRepository.DeleteBranchAsync(SelectedBranch.BranchId);
                StatusMessage = success
                    ? $"✅ Branch '{SelectedBranch.BranchName}' marked as inactive"
                    : "❌ Failed to deactivate branch";
            }

            if (success)
            {
                await LoadBranchesAsync();
                CancelBranchEdit();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting branch: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelBranchEdit()
    {
        IsEditMode = false;
        SelectedBranch = null;
        BranchCode = string.Empty;
        BranchName = string.Empty;
        BranchDescription = string.Empty;
        BranchIsActive = true;
    }

    #endregion
}
