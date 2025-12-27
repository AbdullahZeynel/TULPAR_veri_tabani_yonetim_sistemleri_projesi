using Microsoft.Win32;
using SiberMailer.Business.Services;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using SiberMailer.UI.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Campaign wizard.
/// Manages 3-step wizard: SMTP Selection → Audience → Content & Finalize
/// </summary>
public class CampaignViewModel : ViewModelBase
{
    private readonly SmtpVaultService _smtpVaultService;
    private readonly RecipientListRepository _listRepository;
    private readonly MailJobRepository _jobRepository;
    private readonly TemplateRepository _templateRepository;
    private readonly MailService _mailService;

    private SmtpAccount? _selectedSmtpAccount;
    private DecryptedSmtpConfig? _unlockedSmtpConfig;
    private Template? _selectedTemplate;
    private string _subject = string.Empty;
    private string _htmlBody = string.Empty;
    private string _templateFileName = string.Empty;
    private string _jobName = string.Empty;
    private string _adminNotes = string.Empty;
    private bool _isSmtpUnlocked;
    private string _statusMessage = "Select an SMTP account to begin.";
    private int _currentStep = 1;
    private bool _isLoading;
    private bool _isSending;
    private DateTime? _scheduledDate;
    private TimeSpan? _scheduledTime;

    // User context (would be set from MainWindow)
    public User? CurrentUser { get; set; }

    public CampaignViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        var smtpRepo = new SmtpAccountRepository(factory);
        var cryptoService = new CryptoService();
        _smtpVaultService = new SmtpVaultService(smtpRepo, cryptoService);
        _listRepository = new RecipientListRepository(factory);
        _jobRepository = new MailJobRepository(factory);
        _templateRepository = new TemplateRepository(factory);
        _mailService = new MailService();

        SmtpAccounts = new ObservableCollection<SmtpAccount>();
        SelectableLists = new ObservableCollection<SelectableRecipientList>();
        Templates = new ObservableCollection<Template>();
        Attachments = new ObservableCollection<string>();
        AvailableTimes = new ObservableCollection<string>();
        GenerateTimeSlots();

        // Commands
        RefreshSmtpAccountsCommand = new AsyncRelayCommand(LoadSmtpAccountsAsync);
        UnlockSmtpCommand = new AsyncRelayCommand(ExecuteUnlockSmtpAsync);
        NextStepCommand = new RelayCommand(_ => GoToNextStep(), _ => CanGoNext());
        PreviousStepCommand = new RelayCommand(_ => GoToPreviousStep(), _ => CurrentStep > 1);
        LoadTemplateCommand = new RelayCommand(_ => ExecuteLoadTemplate());
        SendCampaignCommand = new AsyncRelayCommand(ExecuteSendCampaignAsync, () => CanSend());
        SendTestEmailCommand = new AsyncRelayCommand(SendTestEmailAsync, () => IsSmtpUnlocked);
        UploadAttachmentCommand = new RelayCommand(_ => ExecuteUploadAttachment());
        RemoveAttachmentCommand = new RelayCommand(path => ExecuteRemoveAttachment(path as string));
        OpenLogsCommand = new RelayCommand(_ => ExecuteOpenLogs());
        OpenImagesFolderCommand = new RelayCommand(_ => ExecuteOpenImagesFolder());

        // Subscribe to list change events for cross-ViewModel sync
        EventAggregator.ListCreated += OnListChanged;
        EventAggregator.ListDeleted += OnListChanged;
        EventAggregator.ListUpdated += OnListChanged;

        // Load initial data
        _ = LoadDataAsync();
    }

    private async void OnListChanged()
    {
        await LoadRecipientListsAsync();
    }

    #region Properties

    public ObservableCollection<SmtpAccount> SmtpAccounts { get; }
    public ObservableCollection<SelectableRecipientList> SelectableLists { get; }
    public ObservableCollection<Template> Templates { get; }
    public ObservableCollection<string> Attachments { get; }
    public ObservableCollection<string> AvailableTimes { get; }

    public SmtpAccount? SelectedSmtpAccount
    {
        get => _selectedSmtpAccount;
        set
        {
            if (SetProperty(ref _selectedSmtpAccount, value))
            {
                IsSmtpUnlocked = false;
                UnlockedSmtpConfig = null;

                if (value != null)
                {
                    StatusMessage = "🔒 Click 'Unlock' to enter PIN and access this SMTP account.";
                }
            }
        }
    }

    public DecryptedSmtpConfig? UnlockedSmtpConfig
    {
        get => _unlockedSmtpConfig;
        private set => SetProperty(ref _unlockedSmtpConfig, value);
    }

    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && value != null)
            {
                // Auto-fill subject and body from template
                Subject = value.TemplateName;
                HtmlBody = value.HtmlContent;
                TemplateFileName = value.TemplateName;
            }
        }
    }

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    public string HtmlBody
    {
        get => _htmlBody;
        set => SetProperty(ref _htmlBody, value);
    }

    public string TemplateFileName
    {
        get => _templateFileName;
        set => SetProperty(ref _templateFileName, value);
    }

    public string JobName
    {
        get => _jobName;
        set => SetProperty(ref _jobName, value);
    }

    public string AdminNotes
    {
        get => _adminNotes;
        set => SetProperty(ref _adminNotes, value);
    }

    public bool IsSmtpUnlocked
    {
        get => _isSmtpUnlocked;
        private set => SetProperty(ref _isSmtpUnlocked, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(NextButtonText));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSending
    {
        get => _isSending;
        set => SetProperty(ref _isSending, value);
    }

    public DateTime? ScheduledDate
    {
        get => _scheduledDate;
        set => SetProperty(ref _scheduledDate, value);
    }

    public TimeSpan? ScheduledTime
    {
        get => _scheduledTime;
        set 
        {
            if (SetProperty(ref _scheduledTime, value))
            {
                OnPropertyChanged(nameof(SelectedTimeStr));
            }
        }
    }

    public string? SelectedTimeStr
    {
        get => ScheduledTime.HasValue ? $"{ScheduledTime.Value.Hours:00}:{ScheduledTime.Value.Minutes:00}" : null;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                ScheduledTime = null;
            }
            else if (TimeSpan.TryParse(value, out var time))
            {
                ScheduledTime = time;
            }
            OnPropertyChanged();
        }
    }

    // Step visibility
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    // Computed: Total selected contacts across all selected lists
    public int TotalSelectedContacts => SelectableLists
        .Where(l => l.IsSelected)
        .Sum(l => l.ActiveCount);

    public int SelectedListsCount => SelectableLists.Count(l => l.IsSelected);

    public string AudienceSummary => SelectedListsCount > 0
        ? $"Targeting {TotalSelectedContacts:N0} Active Contacts across {SelectedListsCount} List(s)"
        : "No lists selected";

    // Role-based UI
    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public bool IsMember => CurrentUser?.Role == UserRole.Member;
    public string SendButtonText => IsAdmin ? "Send Now" : "Submit for Approval";
    public string NextButtonText => CurrentStep == 3 ? SendButtonText : "Next →";

    #endregion

    #region Commands

    public ICommand RefreshSmtpAccountsCommand { get; }
    public ICommand UnlockSmtpCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand LoadTemplateCommand { get; }
    public ICommand SendCampaignCommand { get; }
    public ICommand SendTestEmailCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenImagesFolderCommand { get; }
    public ICommand UploadAttachmentCommand { get; }
    public ICommand RemoveAttachmentCommand { get; }

    #endregion

    #region Methods

    private async Task LoadDataAsync()
    {
        await LoadSmtpAccountsAsync();
        await LoadRecipientListsAsync();
        await LoadTemplatesAsync();
    }

    private async Task LoadSmtpAccountsAsync()
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
            StatusMessage = $"Loaded {SmtpAccounts.Count} SMTP accounts.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading SMTP accounts: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRecipientListsAsync()
    {
        try
        {
            var lists = await _listRepository.GetAllWithCountsAsync();
            SelectableLists.Clear();
            foreach (var list in lists)
            {
                var selectable = new SelectableRecipientList
                {
                    ListId = list.ListId,
                    ListName = list.ListName,
                    Description = list.Description,
                    BranchId = list.BranchId,
                    BranchName = list.BranchName,
                    IsActive = list.IsActive,
                    ContactCount = list.ContactCount,
                    ActiveCount = list.ActiveCount,
                    IsSelected = false
                };
                
                // Subscribe to property changes for real-time updates
                selectable.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableRecipientList.IsSelected))
                    {
                        OnPropertyChanged(nameof(TotalSelectedContacts));
                        OnPropertyChanged(nameof(SelectedListsCount));
                        OnPropertyChanged(nameof(AudienceSummary));
                    }
                };
                
                SelectableLists.Add(selectable);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading recipient lists: {ex.Message}";
        }
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            var templates = await _templateRepository.GetAllAsync();
            Templates.Clear();
            foreach (var template in templates.Where(t => t.IsActive))
            {
                Templates.Add(template);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading templates: {ex.Message}";
        }
    }

    private async Task ExecuteUnlockSmtpAsync()
    {
        if (SelectedSmtpAccount == null) return;

        var dialog = new PinDialog
        {
            Owner = Application.Current.MainWindow
        };

        while (true)
        {
            var result = dialog.ShowDialog();

            if (result != true || string.IsNullOrEmpty(dialog.EnteredPin))
            {
                StatusMessage = "🔒 Account unlock cancelled.";
                return;
            }

            await ValidatePinAsync(dialog.EnteredPin, dialog);
            
            if (IsSmtpUnlocked)
                break;
        }
    }

    private async Task ValidatePinAsync(string pin, PinDialog dialog)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Validating PIN...";

            var config = await _smtpVaultService.GetDecryptedConfigAsync(SelectedSmtpAccount!.SmtpAccountId, pin);

            if (config != null)
            {
                UnlockedSmtpConfig = config;
                IsSmtpUnlocked = true;
                StatusMessage = $"✅ SMTP account unlocked: {config.SmtpHost}:{config.SmtpPort}";
            }
            else
            {
                dialog.ShowError("Invalid PIN. Please try again.");
            }
        }
        catch (Exception ex)
        {
            dialog.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteLoadTemplate()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All Files (*.*)|*.*",
            Title = "Select Email Template"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                HtmlBody = File.ReadAllText(dialog.FileName);
                TemplateFileName = Path.GetFileName(dialog.FileName);
                StatusMessage = $"✅ Loaded template: {TemplateFileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error loading template: {ex.Message}";
            }
        }
    }

    private void ExecuteUploadAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|PDF Files (*.pdf)|*.pdf|Documents (*.docx;*.doc)|*.docx;*.doc",
            Title = "Select Attachment",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (!Attachments.Contains(file))
                {
                    Attachments.Add(file);
                }
            }
            StatusMessage = $"📎 {Attachments.Count} attachment(s) added.";
        }
    }

    private void GenerateTimeSlots()
    {
        AvailableTimes.Clear();
        for (int i = 0; i < 24; i++)
        {
            AvailableTimes.Add($"{i:00}:00");
            AvailableTimes.Add($"{i:00}:30");
        }
    }

    private void ExecuteRemoveAttachment(string? path)
    {
        if (!string.IsNullOrEmpty(path) && Attachments.Contains(path))
        {
            Attachments.Remove(path);
            StatusMessage = $"📎 Attachment removed. {Attachments.Count} remaining.";
        }
    }

    private bool CanSend()
    {
        return IsSmtpUnlocked &&
               SelectedListsCount > 0 &&
               !string.IsNullOrWhiteSpace(Subject) &&
               !string.IsNullOrWhiteSpace(HtmlBody) &&
               !IsSending;
    }

    private async Task ExecuteSendCampaignAsync()
    {
        if (!CanSend()) return;
        
        if (UnlockedSmtpConfig == null)
        {
            StatusMessage = "❌ Unlock SMTP account first!";
            return;
        }

        IsSending = true;

        try
        {
            // Get selected lists
            var selectedLists = SelectableLists.Where(l => l.IsSelected).ToList();
            if (!selectedLists.Any())
            {
                StatusMessage = "⚠️ Select at least one recipient list!";
                return;
            }

            StatusMessage = "📥 Loading contacts...";

            // Load contacts
            var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
            var contactRepo = new ContactRepository(factory);
            
            var allContacts = new List<Contact>();
            foreach (var list in selectedLists)
            {
                var contacts = await contactRepo.GetByListIdAsync(list.ListId);
                allContacts.AddRange(contacts);
            }

            if (!allContacts.Any())
            {
                StatusMessage = "⚠️ No contacts found in selected lists!";
                return;
            }

            // Remove duplicates by email
            allContacts = allContacts.GroupBy(c => c.Email).Select(g => g.First()).ToList();

            // Setup logging
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"send_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            File.AppendAllText(logFile, "===============================================\n");
            File.AppendAllText(logFile, "SIBERMAILER - CAMPAIGN SEND LOG\n");
            File.AppendAllText(logFile, $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            File.AppendAllText(logFile, $"SMTP: {SelectedSmtpAccount?.Email}\n");
            File.AppendAllText(logFile, $"Subject: {Subject}\n");
            File.AppendAllText(logFile, $"Recipients: {allContacts.Count}\n");
            File.AppendAllText(logFile, "===============================================\n\n");

            StatusMessage = $"📧 Sending to {allContacts.Count} recipients via SMTP...";

            // SEND EMAILS VIA SMTP!
            var result = await _mailService.SendBatchAsync(
                UnlockedSmtpConfig,
                allContacts,
                Subject,
                HtmlBody,
                null, // No plain text
                new Progress<(int Sent, int Total, string CurrentEmail)>(p =>
                {
                    StatusMessage = $"📧 Sending... {p.Sent}/{p.Total}\n{p.CurrentEmail}";
                })
            );

            // Log detailed results
            File.AppendAllText(logFile, "\n===============================================\n");
            File.AppendAllText(logFile, "SEND RESULTS\n");
            File.AppendAllText(logFile, "===============================================\n");
            File.AppendAllText(logFile, $"Total Attempted: {result.TotalAttempted}\n");
            File.AppendAllText(logFile, $"Successful:      {result.SuccessCount}\n");
            File.AppendAllText(logFile, $"Failed:          {result.FailedCount}\n");
            File.AppendAllText(logFile, $"Success Rate:    {result.SuccessRate:F1}%\n");
            File.AppendAllText(logFile, $"Duration:        {result.TotalDuration.TotalSeconds:F1} seconds\n");
            File.AppendAllText(logFile, "\n");

            File.AppendAllText(logFile, "INDIVIDUAL RESULTS:\n");
            File.AppendAllText(logFile, "-----------------------------------------------\n");
            foreach (var r in result.Results)
            {
                var status = r.Success ? "✓ SENT" : "✗ FAILED";
                File.AppendAllText(logFile, $"{status,-10} {r.Email}");
                if (!r.Success && !string.IsNullOrEmpty(r.ErrorMessage))
                {
                    File.AppendAllText(logFile, $" - ERROR: {r.ErrorMessage}");
                }
                File.AppendAllText(logFile, $"\n");
            }

            File.AppendAllText(logFile, $"\nCompleted: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            // Get first error for display
            var firstError = result.Results.FirstOrDefault(r => !r.Success);
            var errorSample = firstError != null ? $"\n\n⚠️ Sample Error:\n{firstError.Email}\n{firstError.ErrorMessage}" : "";

            // Show final status
            if (result.SuccessCount > 0)
            {
                StatusMessage = $"✅ EMAILS SENT VIA SMTP!\n\n" +
                              $"✓ Successful: {result.SuccessCount}\n" +
                              $"✗ Failed: {result.FailedCount}\n" +
                              $"📊 Total: {result.TotalAttempted}\n" +
                              $"⏱ Time: {result.TotalDuration.TotalSeconds:F1}s\n\n" +
                              $"📋 Log: {logFile}";
                
                ResetForm();
            }
            else
            {
                StatusMessage = $"❌ ALL EMAILS FAILED TO SEND!{errorSample}\n\n" +
                              $"📂 Full error log:\n{logFile}\n\n" +
                              $"💡 Click 'Open Logs' button to view details";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ ERROR:\n{ex.Message}";
            
            // Log error
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var errorLog = Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(errorLog, $"ERROR DETAILS:\n{ex}\n");
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task CreatePendingJobAsync()
    {
        StatusMessage = "📤 Submitting campaigns for approval...";

        var selectedLists = SelectableLists.Where(l => l.IsSelected).ToList();
        if (!selectedLists.Any()) return;

        int successCount = 0;
        var createdJobIds = new List<int>();

        foreach (var list in selectedLists)
        {
            var job = new CreateJobDto
            {
                ListId = list.ListId,
                SmtpAccountId = SelectedSmtpAccount!.SmtpAccountId,
                Subject = Subject,
                HtmlBody = HtmlBody,
                PlainTextBody = null,
                CreatedByUserId = CurrentUser?.UserId ?? 1,
                EmailSubject = Subject,
                AdminNotes = AdminNotes,
                AttachmentPaths = Attachments.Count > 0 ? JsonSerializer.Serialize(Attachments.ToList()) : null
            };

            var result = await _jobRepository.CreateJobAsync(job);
            if (result.Success && result.JobId.HasValue)
            {
                successCount++;
                createdJobIds.Add(result.JobId.Value);
            }
        }

        if (successCount > 0)
        {
            StatusMessage = $"✅ {successCount} campaign(s) submitted for approval!\n" +
                          $"Job IDs: {string.Join(", ", createdJobIds)}\n" +
                          $"Status: PENDING (Awaiting admin approval)";
            
            ResetForm();
        }
        else
        {
            StatusMessage = "❌ Failed to create any campaigns.";
        }
    }

    private async Task CreateAndSendJobAsync()
    {
        StatusMessage = "📤 Creating and sending campaigns...";

        var selectedLists = SelectableLists.Where(l => l.IsSelected).ToList();
        if (!selectedLists.Any()) return;

        int successCount = 0;
        var createdJobIds = new List<int>();

        foreach (var list in selectedLists)
        {
            var job = new CreateJobDto
            {
                ListId = list.ListId,
                SmtpAccountId = SelectedSmtpAccount!.SmtpAccountId,
                Subject = Subject,
                HtmlBody = HtmlBody,
                PlainTextBody = null,
                CreatedByUserId = CurrentUser?.UserId ?? 1,
                EmailSubject = Subject,
                AdminNotes = AdminNotes,
                AttachmentPaths = Attachments.Count > 0 ? JsonSerializer.Serialize(Attachments.ToList()) : null
            };

            var result = await _jobRepository.CreateJobAsync(job);

            if (result.Success && result.JobId.HasValue)
            {
                // Auto-approve since user is Admin
                await _jobRepository.ApproveJobAsync(result.JobId.Value, CurrentUser?.UserId ?? 1);
                successCount++;
                createdJobIds.Add(result.JobId.Value);
            }
        }

        if (successCount > 0)
        {
            StatusMessage = $"✅ {successCount} campaign(s) approved and queued!\n" +
                          $"Job IDs: {string.Join(", ", createdJobIds)}\n" +
                          $"Status: APPROVED (Ready to send)";

            ResetForm();
        }
        else
        {
            StatusMessage = "❌ Failed to create any campaigns.";
        }
    }

    private void ExecuteOpenLogs()
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            System.Diagnostics.Process.Start("explorer.exe", logDir);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error opening logs: {ex.Message}";
        }
    }

    private void ExecuteOpenImagesFolder()
    {
        try
        {
            var imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html_image_sources");
            Directory.CreateDirectory(imagesDir);
            System.Diagnostics.Process.Start("explorer.exe", imagesDir);
            StatusMessage = $"📂 Opened: {imagesDir}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error opening images folder: {ex.Message}";
        }
    }

    private void ResetForm()
    {
        CurrentStep = 1;
        Subject = string.Empty;
        HtmlBody = string.Empty;
        TemplateFileName = string.Empty;
        JobName = string.Empty;
        AdminNotes = string.Empty;
        ScheduledDate = null;
        ScheduledTime = null;
        Attachments.Clear();
        SelectedTemplate = null;
        
        foreach (var list in SelectableLists)
        {
            list.IsSelected = false;
        }
    }

    private async Task SendTestEmailAsync()
    {
        if (UnlockedSmtpConfig == null)
        {
            StatusMessage = "⚠️ Please unlock an SMTP account first.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Sending test email...";

            var result = await _mailService.SendTestEmailAsync(
                UnlockedSmtpConfig,
                "test@example.com",
                Subject.Length > 0 ? Subject : "SiberMailer Test",
                HtmlBody.Length > 0 ? HtmlBody : "<h1>Test Email</h1><p>This is a test from SiberMailer.</p>");

            StatusMessage = result.Success 
                ? "✅ Test email sent successfully!" 
                : $"❌ Failed to send test email: {result.ErrorMessage}";
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

    private bool CanGoNext()
    {
        return CurrentStep switch
        {
            1 => IsSmtpUnlocked,
            2 => SelectedListsCount > 0,
            3 => !string.IsNullOrWhiteSpace(Subject) && !string.IsNullOrWhiteSpace(HtmlBody),
            _ => false
        };
    }

    private void GoToNextStep()
    {
        if (CurrentStep < 3)
        {
            CurrentStep++;
        }
        else if (CurrentStep == 3 && CanSend())
        {
            // On Step 3, Next button submits the campaign
            _ = ExecuteSendCampaignAsync();
        }
    }

    private void GoToPreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    #endregion
}
