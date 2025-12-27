using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Contact List view.
/// </summary>
public class ContactListViewModel : ViewModelBase
{
    private readonly RecipientListRepository _listRepository;
    private readonly ContactRepository _contactRepository;
    private readonly BranchRepository _branchRepository;

    private RecipientListWithCount? _selectedList;
    private Contact? _selectedContact;
    private string _searchText = string.Empty;
    private string? _statusFilter;
    private bool _isLoading;
    private string _statusMessage = "Select a list to view contacts.";
    private int _totalContacts;
    private int _activeContacts;
    private string _newListName = string.Empty;
    private bool _showNewListDialog;
    private Branch? _selectedBranch;

    public ContactListViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _listRepository = new RecipientListRepository(factory);
        _contactRepository = new ContactRepository(factory);
        _branchRepository = new BranchRepository(factory);

        Lists = new ObservableCollection<RecipientListWithCount>();
        Contacts = new ObservableCollection<Contact>();
        FilteredContacts = new ObservableCollection<Contact>();
        AvailableBranches = new ObservableCollection<Branch>();

        // Commands
        RefreshCommand = new AsyncRelayCommand(LoadListsAsync);
        AddContactCommand = new RelayCommand(_ => ShowAddContactDialog());
        EditContactCommand = new RelayCommand(contact => ShowEditContactDialog(contact as Contact));
        DeleteContactCommand = new RelayCommand(contact => DeleteContactAsync(contact as Contact));
        SearchCommand = new RelayCommand(_ => ApplyFilter());
        ClearSearchCommand = new RelayCommand(_ => ClearSearch());
        AddListCommand = new RelayCommand(_ => ShowCreateListDialog());
        ConfirmAddListCommand = new AsyncRelayCommand(CreateNewListAsync);
        CancelAddListCommand = new RelayCommand(_ => { ShowNewListDialog = false; NewListName = ""; });
        DeleteListCommand = new AsyncRelayCommand<RecipientListWithCount>(DeleteListAsync);

        // Subscribe to list change events for cross-ViewModel sync
        EventAggregator.ListCreated += OnListChangedByOther;
        EventAggregator.ListUpdated += OnListChangedByOther;

        // Status filter options
        StatusFilters = new List<string> { "All", "Active", "Not-Active", "RedListed" };
        StatusFilter = "All";

        // Load data
        _ = InitializeAsync();
    }

    private async void OnListChangedByOther()
    {
        // Refresh when another ViewModel creates or updates a list
        await LoadListsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadBranchesAsync();
        await LoadListsAsync();
    }

    #region Properties

    public ObservableCollection<RecipientListWithCount> Lists { get; }
    public ObservableCollection<Contact> Contacts { get; }
    public ObservableCollection<Contact> FilteredContacts { get; }
    public List<string> StatusFilters { get; }

    public RecipientListWithCount? SelectedList
    {
        get => _selectedList;
        set
        {
            if (SetProperty(ref _selectedList, value) && value != null)
            {
                _ = LoadContactsAsync(value.ListId);
            }
        }
    }

    public Contact? SelectedContact
    {
        get => _selectedContact;
        set => SetProperty(ref _selectedContact, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                ApplyFilter();
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

    public int TotalContacts
    {
        get => _totalContacts;
        set => SetProperty(ref _totalContacts, value);
    }

    public int ActiveContacts
    {
        get => _activeContacts;
        set => SetProperty(ref _activeContacts, value);
    }

    public string NewListName
    {
        get => _newListName;
        set => SetProperty(ref _newListName, value);
    }

    public bool ShowNewListDialog
    {
        get => _showNewListDialog;
        set => SetProperty(ref _showNewListDialog, value);
    }

    public ObservableCollection<Branch> AvailableBranches { get; }

    public Branch? SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand AddContactCommand { get; }
    public ICommand EditContactCommand { get; }
    public ICommand DeleteContactCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand AddListCommand { get; }
    public ICommand ConfirmAddListCommand { get; }
    public ICommand CancelAddListCommand { get; }
    public ICommand DeleteListCommand { get; }

    #endregion

    #region Methods

    private async Task LoadBranchesAsync()
    {
        try
        {
            var branches = await _branchRepository.GetAllAsync();
            AvailableBranches.Clear();
            foreach (var branch in branches)
            {
                AvailableBranches.Add(branch);
            }

            // Auto-select first branch
            if (AvailableBranches.Count > 0 && SelectedBranch == null)
            {
                SelectedBranch = AvailableBranches.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading branches: {ex.Message}";
        }
    }

    private async Task LoadListsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading lists...";

        try
        {
            var lists = await _listRepository.GetAllWithCountsAsync();

            Lists.Clear();

            // Add "All Contacts" as the first item
            Lists.Add(new RecipientListWithCount
            {
                ListId = 0,
                ListName = "📋 All Contacts",
                Description = "View contacts from all lists",
                ContactCount = lists.Sum(l => l.ContactCount),
                ActiveCount = lists.Sum(l => l.ActiveCount)
            });

            foreach (var list in lists)
            {
                Lists.Add(list);
            }

            StatusMessage = $"Loaded {Lists.Count - 1} recipient lists.";

            // Auto-select "All Contacts" by default
            if (Lists.Count > 0 && SelectedList == null)
            {
                SelectedList = Lists[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading lists: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadContactsAsync(int listId)
    {
        IsLoading = true;
        StatusMessage = "Loading contacts...";

        try
        {
            IEnumerable<Contact> contacts;

            if (listId == 0)
            {
                // "All Contacts" - load from all lists
                contacts = await _contactRepository.GetAllAsync();
            }
            else
            {
                // Specific list - use direct query instead of function
                contacts = await _contactRepository.GetByListIdAsync(listId);
            }

            Contacts.Clear();
            foreach (var contact in contacts)
            {
                Contacts.Add(contact);
            }

            TotalContacts = Contacts.Count;
            ActiveContacts = Contacts.Count(c => c.Status == ContactStatus.Active);

            ApplyFilter();

            StatusMessage = $"Showing {FilteredContacts.Count} of {TotalContacts} contacts.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading contacts: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredContacts.Clear();

        var filtered = Contacts.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(c =>
                (c.Email?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (c.FullName?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (c.Company?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
        {
            if (StatusFilter == "Not-Active")
            {
                // "Not-Active" includes all non-active statuses (Unsubscribed, Bounced, Pending)
                filtered = filtered.Where(c => c.Status != ContactStatus.Active && c.Status != ContactStatus.RedListed);
            }
            else if (Enum.TryParse<ContactStatus>(StatusFilter, out var status))
            {
                filtered = filtered.Where(c => c.Status == status);
            }
        }

        foreach (var contact in filtered)
        {
            FilteredContacts.Add(contact);
        }

        StatusMessage = $"Showing {FilteredContacts.Count} of {TotalContacts} contacts.";
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        StatusFilter = null;
    }

    private async void ShowAddContactDialog()
    {
        if (SelectedList == null)
        {
            System.Windows.MessageBox.Show(
                "Please select a recipient list first.",
                "No List Selected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var viewModel = new ContactEditorViewModel(SelectedList.ListId);
        var window = new Views.ContactEditorWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };

        viewModel.SaveCompleted += () =>
        {
            window.DialogResult = true;
            window.Close();
        };

        if (window.ShowDialog() == true && viewModel.SavedContact != null)
        {
            // Auto-refresh: reload all contacts
            await LoadContactsAsync(SelectedList.ListId);
            await LoadListsAsync(); // Update counts
            StatusMessage = "✅ Contact added successfully.";
        }
    }

    private async void ShowEditContactDialog(Contact? contact)
    {
        if (contact == null || SelectedList == null) return;

        var viewModel = new ContactEditorViewModel(SelectedList.ListId, contact);
        var window = new Views.ContactEditorWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };

        viewModel.SaveCompleted += () =>
        {
            window.DialogResult = true;
            window.Close();
        };

        if (window.ShowDialog() == true && viewModel.SavedContact != null)
        {
            // Auto-refresh: reload all contacts
            await LoadContactsAsync(SelectedList.ListId);
            StatusMessage = "✅ Contact updated successfully.";
        }
    }

    private async void DeleteContactAsync(Contact? contact)
    {
        if (contact == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete '{contact.Email}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _contactRepository.DeleteAsync(contact.ContactId);
            
            // Auto-refresh: reload contacts and lists
            await LoadContactsAsync(SelectedList?.ListId ?? 0);
            await LoadListsAsync(); // Update counts
            
            StatusMessage = "✅ Contact deleted successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting contact: {ex.Message}";
        }
    }

    private void ShowCreateListDialog()
    {
        NewListName = string.Empty;
        ShowNewListDialog = true;
    }

    private async Task CreateNewListAsync()
    {
        if (string.IsNullOrWhiteSpace(NewListName))
        {
            StatusMessage = "Please enter a list name.";
            return;
        }

        if (SelectedBranch == null)
        {
            StatusMessage = "Please select a branch.";
            return;
        }

        try
        {
            var newList = new RecipientList
            {
                ListName = NewListName.Trim(),
                Description = $"Created on {DateTime.Now:yyyy-MM-dd HH:mm}",
                BranchId = SelectedBranch.BranchId
            };

            newList.ListId = await _listRepository.CreateAsync(newList);
            
            // Create a RecipientListWithCount for the UI
            var listWithCount = new RecipientListWithCount
            {
                ListId = newList.ListId,
                ListName = newList.ListName,
                Description = newList.Description,
                BranchId = newList.BranchId,
                ContactCount = 0,
                ActiveCount = 0
            };
            
            // Auto-refresh: reload lists to get updated counts
            await LoadListsAsync();
            SelectedList = Lists.FirstOrDefault(l => l.ListId == newList.ListId) ?? Lists[0];
            
            ShowNewListDialog = false;
            NewListName = string.Empty;
            StatusMessage = $"✅ List '{newList.ListName}' created for {SelectedBranch.BranchName}.";

            // Notify other ViewModels to refresh their lists
            EventAggregator.RaiseListCreated();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error creating list: {ex.Message}";
        }
    }

    private async Task DeleteListAsync(RecipientListWithCount? list)
    {
        if (list == null || list.ListId == 0)
        {
            StatusMessage = "Cannot delete 'All Contacts'.";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete the list '{list.ListName}'?\n\nThis will NOT delete the contacts, only the list.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            await _listRepository.DeleteAsync(list.ListId);
            
            // Auto-refresh: reload lists
            await LoadListsAsync();
            
            // Select "All Contacts" after deletion
            if (Lists.Count > 0)
            {
                SelectedList = Lists[0];
            }
            
            StatusMessage = $"✅ List '{list.ListName}' deleted.";
            
            // Notify other ViewModels
            EventAggregator.RaiseListDeleted();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting list: {ex.Message}";
        }
    }

    #endregion
}
