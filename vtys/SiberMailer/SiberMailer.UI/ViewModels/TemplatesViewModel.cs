using Microsoft.Win32;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Templates view.
/// Manages email template CRUD operations, filtering, and HTML import.
/// </summary>
public class TemplatesViewModel : ViewModelBase
{
    private readonly TemplateRepository _templateRepository;
    private readonly BranchRepository _branchRepository;

    // Collections
    private Template? _selectedTemplate;
    private Branch? _selectedBranchFilter;
    private string? _selectedStatusFilter;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string _statusMessage = "Select a template to preview.";

    // Template Dialog (Add/Edit)
    private bool _showTemplateDialog;
    private bool _isEditMode;
    private int _editingTemplateId;
    private string _dialogTemplateName = string.Empty;
    private string _dialogTemplateHtmlContent = string.Empty;
    private Branch? _dialogTemplateBranch;

    public TemplatesViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _templateRepository = new TemplateRepository(factory);
        _branchRepository = new BranchRepository(factory);

        Templates = new ObservableCollection<Template>();
        FilteredTemplates = new ObservableCollection<Template>();
        BranchFilterList = new ObservableCollection<Branch>();
        StatusFilterList = new List<string> { "All", "Active", "Disabled" };

        // Commands
        RefreshCommand = new AsyncRelayCommand(LoadTemplatesAsync);
        SearchCommand = new RelayCommand(_ => ApplyFilter());
        AddTemplateCommand = new RelayCommand(_ => ShowAddDialog());
        EditTemplateCommand = new RelayCommand(_ => ShowEditDialog(), _ => SelectedTemplate != null);
        SaveTemplateCommand = new AsyncRelayCommand(SaveTemplateAsync, () => CanSaveTemplate());
        CancelDialogCommand = new RelayCommand(_ => HideDialog());
        LoadHtmlFromFileCommand = new RelayCommand(_ => ImportHtmlFromFile());
        DeleteTemplateCommand = new AsyncRelayCommand(DeleteSelectedTemplateAsync, () => SelectedTemplate != null);
        ToggleStatusCommand = new AsyncRelayCommand(ToggleSelectedTemplateStatusAsync, () => SelectedTemplate != null);
        
        // Row-specific commands for DataGrid
        EditRowCommand = new RelayCommand(template => EditTemplate(template as Template));
        DeleteRowCommand = new AsyncRelayCommand<Template>(DeleteTemplateAsync);

        // Load initial data
        _ = InitializeAsync();
    }

    #region Properties

    public ObservableCollection<Template> Templates { get; }
    public ObservableCollection<Template> FilteredTemplates { get; }
    public ObservableCollection<Branch> BranchFilterList { get; }
    public List<string> StatusFilterList { get; }

    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(HasSelectedTemplate));
                OnPropertyChanged(nameof(SelectedTemplateStatusText));
                OnPropertyChanged(nameof(SelectedTemplateStatusColor));
            }
        }
    }

    public bool HasSelectedTemplate => SelectedTemplate != null;

    public string SelectedTemplateStatusText => SelectedTemplate?.IsActive == true ? "Active" : "Disabled";
    
    public string SelectedTemplateStatusColor => SelectedTemplate?.IsActive == true ? "#6FCF7C" : "#E85050";

    public Branch? SelectedBranchFilter
    {
        get => _selectedBranchFilter;
        set
        {
            if (SetProperty(ref _selectedBranchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilter();
            }
        }
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

    // Template Dialog Properties (Add/Edit)
    public bool ShowTemplateDialog
    {
        get => _showTemplateDialog;
        set => SetProperty(ref _showTemplateDialog, value);
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
            }
        }
    }

    public string DialogTitle => IsEditMode ? "Edit Template" : "Add New Template";
    public string SaveButtonText => IsEditMode ? "Update Template" : "Save Template";

    public string DialogTemplateName
    {
        get => _dialogTemplateName;
        set => SetProperty(ref _dialogTemplateName, value);
    }

    public string DialogTemplateHtmlContent
    {
        get => _dialogTemplateHtmlContent;
        set
        {
            if (SetProperty(ref _dialogTemplateHtmlContent, value))
            {
                OnPropertyChanged(nameof(HasHtmlContent));
            }
        }
    }

    public Branch? DialogTemplateBranch
    {
        get => _dialogTemplateBranch;
        set => SetProperty(ref _dialogTemplateBranch, value);
    }

    public bool HasHtmlContent => !string.IsNullOrWhiteSpace(DialogTemplateHtmlContent);

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand AddTemplateCommand { get; }
    public ICommand EditTemplateCommand { get; }
    public ICommand SaveTemplateCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand LoadHtmlFromFileCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand ToggleStatusCommand { get; }
    
    // Row-specific commands for DataGrid inline buttons
    public ICommand EditRowCommand { get; }
    public ICommand DeleteRowCommand { get; }

    #endregion

    #region Methods

    private async Task InitializeAsync()
    {
        await LoadBranchesAsync();
        await LoadTemplatesAsync();
    }

    public async Task LoadTemplatesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading templates...";

        try
        {
            var templates = await _templateRepository.GetAllAsync();

            Templates.Clear();
            foreach (var template in templates)
            {
                Templates.Add(template);
            }

            ApplyFilter();
            StatusMessage = $"Loaded {Templates.Count} template(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading templates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            var branches = await _branchRepository.GetAllAsync();

            BranchFilterList.Clear();
            
            // Add "All Branches" option
            BranchFilterList.Add(new Branch { BranchId = 0, BranchName = "All Branches" });
            
            foreach (var branch in branches)
            {
                BranchFilterList.Add(branch);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading branches: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        FilteredTemplates.Clear();

        var filtered = Templates.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(t =>
                (t.TemplateName?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (t.BranchName?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Apply branch filter
        if (SelectedBranchFilter != null && SelectedBranchFilter.BranchId > 0)
        {
            filtered = filtered.Where(t => t.BranchId == SelectedBranchFilter.BranchId);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "All")
        {
            var isActive = SelectedStatusFilter == "Active";
            filtered = filtered.Where(t => t.IsActive == isActive);
        }

        foreach (var template in filtered)
        {
            FilteredTemplates.Add(template);
        }

        StatusMessage = $"Showing {FilteredTemplates.Count} of {Templates.Count} template(s).";
    }

    private void ShowAddDialog()
    {
        IsEditMode = false;
        _editingTemplateId = 0;
        DialogTemplateName = string.Empty;
        DialogTemplateHtmlContent = string.Empty;
        DialogTemplateBranch = BranchFilterList.FirstOrDefault(b => b.BranchId > 0);
        ShowTemplateDialog = true;
    }

    private void ShowEditDialog()
    {
        if (SelectedTemplate == null) return;

        // Ensure branches are loaded before showing dialog
        if (BranchFilterList.Count == 0)
        {
            StatusMessage = "⚠️ Loading branches... please try again.";
            _ = LoadBranchesAsync();
            return;
        }

        IsEditMode = true;
        _editingTemplateId = SelectedTemplate.TemplateId;
        DialogTemplateName = SelectedTemplate.TemplateName;
        DialogTemplateHtmlContent = SelectedTemplate.HtmlContent;
        DialogTemplateBranch = BranchFilterList.FirstOrDefault(b => b.BranchId == SelectedTemplate.BranchId) 
                            ?? BranchFilterList.FirstOrDefault(b => b.BranchId > 0);
        ShowTemplateDialog = true;
    }

    private void HideDialog()
    {
        ShowTemplateDialog = false;
        IsEditMode = false;
        _editingTemplateId = 0;
        DialogTemplateName = string.Empty;
        DialogTemplateHtmlContent = string.Empty;
        DialogTemplateBranch = null;
    }

    private bool CanSaveTemplate()
    {
        return !string.IsNullOrWhiteSpace(DialogTemplateName) &&
               !string.IsNullOrWhiteSpace(DialogTemplateHtmlContent) &&
               DialogTemplateBranch != null &&
               DialogTemplateBranch.BranchId > 0;
    }

    private void ImportHtmlFromFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import HTML Template",
            Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                DialogTemplateHtmlContent = File.ReadAllText(dialog.FileName);
                StatusMessage = $"✅ Loaded HTML from '{Path.GetFileName(dialog.FileName)}'";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error reading file: {ex.Message}";
                MessageBox.Show(
                    $"Failed to read file:\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private async Task SaveTemplateAsync()
    {
        if (!CanSaveTemplate()) return;

        IsLoading = true;
        try
        {
            if (IsEditMode)
            {
                // Update existing template
                StatusMessage = "Updating template...";

                var template = new Template
                {
                    TemplateId = _editingTemplateId,
                    TemplateName = DialogTemplateName.Trim(),
                    HtmlContent = DialogTemplateHtmlContent,
                    BranchId = DialogTemplateBranch!.BranchId,
                    IsActive = SelectedTemplate?.IsActive ?? true
                };

                var success = await _templateRepository.UpdateAsync(template);

                if (success)
                {
                    StatusMessage = $"✅ Template '{DialogTemplateName}' updated successfully!";
                    HideDialog();
                    await LoadTemplatesAsync(); // Auto-refresh
                }
                else
                {
                    StatusMessage = "❌ Failed to update template.";
                }
            }
            else
            {
                // Create new template
                StatusMessage = "Saving template...";

                var newTemplate = new Template
                {
                    TemplateName = DialogTemplateName.Trim(),
                    HtmlContent = DialogTemplateHtmlContent,
                    BranchId = DialogTemplateBranch!.BranchId,
                    IsActive = true
                };

                var templateId = await _templateRepository.CreateAsync(newTemplate);

                if (templateId > 0)
                {
                    StatusMessage = $"✅ Template '{DialogTemplateName}' saved successfully!";
                    HideDialog();
                    await LoadTemplatesAsync(); // Auto-refresh
                }
                else
                {
                    StatusMessage = "❌ Failed to save template.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error saving template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteSelectedTemplateAsync()
    {
        if (SelectedTemplate == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedTemplate.TemplateName}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            var success = await _templateRepository.DeleteAsync(SelectedTemplate.TemplateId);

            if (success)
            {
                StatusMessage = $"✅ Template deleted.";
                SelectedTemplate = null;
                await LoadTemplatesAsync(); // Auto-refresh
            }
            else
            {
                StatusMessage = "❌ Failed to delete template.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleSelectedTemplateStatusAsync()
    {
        if (SelectedTemplate == null) return;

        IsLoading = true;
        try
        {
            var newStatus = !SelectedTemplate.IsActive;
            var statusText = newStatus ? "enabled" : "disabled";

            var success = await _templateRepository.ToggleStatusAsync(SelectedTemplate.TemplateId);

            if (success)
            {
                SelectedTemplate.IsActive = newStatus;
                OnPropertyChanged(nameof(SelectedTemplateStatusText));
                OnPropertyChanged(nameof(SelectedTemplateStatusColor));
                StatusMessage = $"✅ Template {statusText}.";
                
                await LoadTemplatesAsync(); // Auto-refresh
            }
            else
            {
                StatusMessage = "❌ Failed to update template status.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error updating status: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens the edit dialog for the specified template (for DataGrid row button).
    /// </summary>
    private void EditTemplate(Template? template)
    {
        if (template == null) return;

        // Ensure branches are loaded before showing dialog  
        if (BranchFilterList.Count == 0)
        {
            StatusMessage = "⚠️ Loading branches... please try again.";
            _ = LoadBranchesAsync();
            return;
        }

        IsEditMode = true;
        _editingTemplateId = template.TemplateId;
        DialogTemplateName = template.TemplateName;
        DialogTemplateHtmlContent = template.HtmlContent;
        DialogTemplateBranch = BranchFilterList.FirstOrDefault(b => b.BranchId == template.BranchId)
                            ?? BranchFilterList.FirstOrDefault(b => b.BranchId > 0);
        ShowTemplateDialog = true;
    }

    /// <summary>
    /// Deletes the specified template with confirmation dialog (for DataGrid row button).
    /// </summary>
    private async Task DeleteTemplateAsync(Template? template)
    {
        if (template == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{template.TemplateName}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            var success = await _templateRepository.DeleteAsync(template.TemplateId);

            if (success)
            {
                StatusMessage = $"✅ Template '{template.TemplateName}' deleted.";
                
                // Clear selection if deleted template was selected
                if (SelectedTemplate?.TemplateId == template.TemplateId)
                {
                    SelectedTemplate = null;
                }
                
                await LoadTemplatesAsync(); // Auto-refresh
            }
            else
            {
                StatusMessage = "❌ Failed to delete template.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error deleting template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
