using Microsoft.Win32;
using SiberMailer.Business.Services;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Import view - handles CSV file import.
/// </summary>
public class ImportViewModel : ViewModelBase
{
    private readonly ImportService _importService;
    private readonly ContactRepository _contactRepository;
    private readonly RecipientListRepository _listRepository;
    private readonly BranchRepository _branchRepository;

    private string _selectedFilePath = string.Empty;
    private string _fileName = string.Empty;
    private bool _isFileSelected;
    private bool _isImporting;
    private bool _isPreviewVisible;
    private int _progressValue;
    private int _progressMax = 100;
    private string _statusMessage = "Select a CSV or Excel file to import contacts.";
    private string _resultMessage = string.Empty;
    private bool _hasResult;
    private bool _isSuccess;
    private RecipientList? _selectedList;
    private string _newListName = string.Empty;
    private bool _showNewListDialog;
    private Branch? _selectedBranch;

    // Preview data
    private string[] _previewHeaders = Array.Empty<string>();
    private List<string[]> _previewRows = new();
    private ImportService.ImportParseResult? _parseResult;

    public ImportViewModel()
    {
        _importService = new ImportService();
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _contactRepository = new ContactRepository(factory);
        _listRepository = new RecipientListRepository(factory);
        _branchRepository = new BranchRepository(factory);

        // Initialize collections
        AvailableLists = new ObservableCollection<RecipientList>();
        Branches = new ObservableCollection<Branch>();

        // Commands
        BrowseCommand = new RelayCommand(_ => ExecuteBrowse());
        ImportCommand = new AsyncRelayCommand(ExecuteImportAsync, () => CanImport && !IsImporting);
        CancelCommand = new RelayCommand(_ => ExecuteCancel(), _ => IsImporting);
        ClearCommand = new RelayCommand(_ => ExecuteClear());
        CreateListCommand = new RelayCommand(_ => ShowCreateListDialog());
        ConfirmCreateListCommand = new AsyncRelayCommand(CreateNewListAsync);
        CancelCreateListCommand = new RelayCommand(_ => { ShowNewListDialog = false; NewListName = ""; });

        // Subscribe to list events for cross-ViewModel sync
        EventAggregator.ListCreated += OnListChanged;
        EventAggregator.ListDeleted += OnListChanged;
        EventAggregator.ListUpdated += OnListChanged;

        // Load data
        _ = LoadListsAsync();
        _ = LoadBranchesAsync();
    }

    private async void OnListChanged()
    {
        // Refresh lists when a list is created, deleted, or updated anywhere in the app
        await LoadListsAsync();
    }

    #region Properties

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                FileName = string.IsNullOrEmpty(value) ? "" : System.IO.Path.GetFileName(value);
                IsFileSelected = !string.IsNullOrEmpty(value);
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public bool IsFileSelected
    {
        get => _isFileSelected;
        set => SetProperty(ref _isFileSelected, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        set => SetProperty(ref _isImporting, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set => SetProperty(ref _isPreviewVisible, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public int ProgressMax
    {
        get => _progressMax;
        set => SetProperty(ref _progressMax, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ResultMessage
    {
        get => _resultMessage;
        set => SetProperty(ref _resultMessage, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public string[] PreviewHeaders
    {
        get => _previewHeaders;
        set => SetProperty(ref _previewHeaders, value);
    }

    public List<string[]> PreviewRows
    {
        get => _previewRows;
        set => SetProperty(ref _previewRows, value);
    }

    // List Selection Properties
    public ObservableCollection<RecipientList> AvailableLists { get; }

    public RecipientList? SelectedList
    {
        get => _selectedList;
        set
        {
            if (SetProperty(ref _selectedList, value))
            {
                OnPropertyChanged(nameof(IsListSelected));
                OnPropertyChanged(nameof(CanImport));
            }
        }
    }

    public bool IsListSelected => SelectedList != null;
    public bool CanImport => IsFileSelected && IsListSelected;

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

    public ObservableCollection<Branch> Branches { get; }

    public Branch? SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    #endregion

    #region Commands

    public ICommand BrowseCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CreateListCommand { get; }
    public ICommand ConfirmCreateListCommand { get; }
    public ICommand CancelCreateListCommand { get; }

    #endregion

    #region Methods

    private void ExecuteBrowse()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Data Files|*.csv;*.xlsx;*.xls|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
            Title = "Select Contacts File (CSV or Excel)"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;
            HasResult = false;
            ResultMessage = string.Empty;
            LoadPreview();
        }
    }

    private void LoadPreview()
    {
        try
        {
            var extension = System.IO.Path.GetExtension(SelectedFilePath).ToLowerInvariant();
            StatusMessage = $"Parsing {extension.ToUpperInvariant().TrimStart('.')} file...";
            
            // For CSV, show preview
            if (extension == ".csv")
            {
                using var stream = System.IO.File.OpenRead(SelectedFilePath);
                var (headers, rows) = _importService.PreviewCsv(stream, 5);
                PreviewHeaders = headers;
                PreviewRows = rows;
                IsPreviewVisible = true;
            }
            else
            {
                // For Excel, preview not yet implemented - just show file name
                PreviewHeaders = new[] { "Excel file selected" };
                PreviewRows = new List<string[]> { new[] { FileName } };
                IsPreviewVisible = true;
            }

            // Parse the full file for import (auto-detects format)
            _parseResult = _importService.ParseFile(SelectedFilePath);
            
            StatusMessage = $"Ready to import {_parseResult.ValidRows} contacts from {_parseResult.TotalRows} rows.";
            
            if (_parseResult.Errors.Count > 0)
            {
                StatusMessage += $" ({_parseResult.Errors.Count} errors found)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading file: {ex.Message}";
            IsPreviewVisible = false;
        }
    }

    private async Task ExecuteImportAsync()
    {
        if (_parseResult == null || _parseResult.Contacts.Count == 0)
        {
            StatusMessage = "No valid contacts to import.";
            return;
        }

        if (SelectedList == null)
        {
            StatusMessage = "Please select a target list first.";
            return;
        }

        IsImporting = true;
        ProgressValue = 0;
        ProgressMax = 100;
        HasResult = false;

        try
        {
            StatusMessage = $"Importing contacts to '{SelectedList.ListName}'...";
            ProgressValue = 10;

            // Convert to ContactImportDto list
            var contacts = _parseResult.Contacts;
            
            ProgressValue = 30;
            StatusMessage = $"Inserting {contacts.Count} contacts into list #{SelectedList.ListId}...";

            // Call bulk insert via stored procedure WITH the selected list ID
            var result = await Task.Run(() => 
                _contactRepository.BulkInsertContactsAsync(SelectedList.ListId, contacts));

            ProgressValue = 100;

            // Show results
            IsSuccess = true;
            HasResult = true;
            ResultMessage = $"✅ Import completed to '{SelectedList.ListName}'!\n" +
                           $"• Inserted: {result.Inserted}\n" +
                           $"• Updated: {result.Updated}\n" +
                           $"• Skipped: {result.Skipped}";

            StatusMessage = "Import completed successfully!";
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            HasResult = true;
            ResultMessage = $"❌ Import failed: {ex.Message}";
            StatusMessage = "Import failed. See error details.";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void ExecuteCancel()
    {
        // In a real implementation, use CancellationToken
        IsImporting = false;
        StatusMessage = "Import cancelled.";
    }

    private void ExecuteClear()
    {
        SelectedFilePath = string.Empty;
        IsPreviewVisible = false;
        PreviewHeaders = Array.Empty<string>();
        PreviewRows = new List<string[]>();
        HasResult = false;
        ResultMessage = string.Empty;
        ProgressValue = 0;
        StatusMessage = "Select a CSV or Excel file to import contacts.";
        _parseResult = null;
    }

    private async Task LoadListsAsync()
    {
        try
        {
            var lists = await _listRepository.GetAllAsync();
            AvailableLists.Clear();
            foreach (var list in lists)
            {
                AvailableLists.Add(list);
            }

            // Auto-select first list if available
            if (AvailableLists.Count > 0 && SelectedList == null)
            {
                SelectedList = AvailableLists[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading lists: {ex.Message}";
        }
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            var branches = await _branchRepository.GetAllAsync();
            Branches.Clear();
            foreach (var branch in branches)
            {
                Branches.Add(branch);
            }

            // Auto-select first branch if available
            if (Branches.Count > 0 && SelectedBranch == null)
            {
                SelectedBranch = Branches[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading branches: {ex.Message}";
        }
    }

    private void ShowCreateListDialog()
    {
        NewListName = string.Empty;
        SelectedBranch = Branches.FirstOrDefault();
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
                Description = $"Created via Import on {DateTime.Now:yyyy-MM-dd HH:mm}",
                BranchId = SelectedBranch.BranchId
            };

            newList.ListId = await _listRepository.CreateAsync(newList);
            
            AvailableLists.Add(newList);
            SelectedList = newList;
            
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

    #endregion
}
