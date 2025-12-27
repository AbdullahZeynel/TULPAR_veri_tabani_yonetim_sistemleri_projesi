using Newtonsoft.Json;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Contact Editor window (Add/Edit).
/// </summary>
public class ContactEditorViewModel : ViewModelBase
{
    private readonly ContactRepository _contactRepository;
    private readonly Contact? _originalContact;
    private readonly int _listId;
    private readonly bool _isEditMode;

    private string _email = string.Empty;
    private string _fullName = string.Empty;
    private string _company = string.Empty;
    private string _customData = string.Empty;
    private string _status = "Active";
    private string _errorMessage = string.Empty;

    public ContactEditorViewModel(int listId, Contact? contactToEdit = null)
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _contactRepository = new ContactRepository(factory);
        
        _listId = listId;
        _originalContact = contactToEdit;
        _isEditMode = contactToEdit != null;

        // Initialize status options
        StatusOptions = Enum.GetNames<ContactStatus>().ToList();

        // If editing, populate fields
        if (_isEditMode && contactToEdit != null)
        {
            _email = contactToEdit.Email;
            _fullName = contactToEdit.FullName ?? string.Empty;
            _company = contactToEdit.Company ?? string.Empty;
            _customData = contactToEdit.CustomData ?? string.Empty;
            _status = contactToEdit.Status.ToString();
        }

        // Commands
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
    }

    #region Properties

    public string WindowTitle => _isEditMode ? "Edit Contact" : "Add New Contact";
    public string WindowSubtitle => _isEditMode ? "Modify contact details" : "Create a new contact entry";
    public string SaveButtonText => _isEditMode ? "Update" : "Add Contact";

    public List<string> StatusOptions { get; }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Company
    {
        get => _company;
        set => SetProperty(ref _company, value);
    }

    public string CustomData
    {
        get => _customData;
        set => SetProperty(ref _customData, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// The saved/updated contact after successful save.
    /// </summary>
    public Contact? SavedContact { get; private set; }

    /// <summary>
    /// Event raised when save is successful and window should close.
    /// </summary>
    public event Action? SaveCompleted;

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }

    #endregion

    #region Methods

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(Email) && IsValidEmail(Email);
    }

    private async Task SaveAsync()
    {
        // Validate email
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email is required.";
            return;
        }

        if (!IsValidEmail(Email))
        {
            ErrorMessage = "Please enter a valid email address.";
            return;
        }

        // Parse status
        if (!Enum.TryParse<ContactStatus>(Status, out var statusEnum))
        {
            statusEnum = ContactStatus.Active;
        }

        try
        {
            // Parse custom data JSON to dictionary
            Dictionary<string, object>? customDataDict = null;
            if (!string.IsNullOrWhiteSpace(CustomData))
            {
                try
                {
                    customDataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(CustomData.Trim());
                }
                catch
                {
                    ErrorMessage = "Invalid JSON format in Custom Data.";
                    return;
                }
            }

            var contact = new Contact
            {
                ContactId = _originalContact?.ContactId ?? 0,
                ListId = _listId,
                Email = Email.Trim().ToLowerInvariant(),
                FullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim(),
                Company = string.IsNullOrWhiteSpace(Company) ? null : Company.Trim(),
                CustomData = customDataDict != null ? JsonConvert.SerializeObject(customDataDict) : null,
                Status = statusEnum
            };

            if (_isEditMode)
            {
                await _contactRepository.UpdateAsync(contact);
            }
            else
            {
                contact.ContactId = await _contactRepository.AddAsync(contact);
            }

            SavedContact = contact;
            SaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving contact: {ex.Message}";
        }
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return false;

        var dotIndex = email.LastIndexOf('.');
        return dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }

    #endregion
}
