using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Approval Center.
/// Uses the simplified MailJob from MailJobRepository for display.
/// </summary>
public class ApprovalCenterViewModel : ViewModelBase
{
    private readonly MailJobRepository _jobRepository;

    private PendingJobItem? _selectedJob;
    private string _rejectionReason = string.Empty;
    private bool _isLoading;
    private string _statusMessage = "Loading pending jobs...";
    private bool _showRejectDialog;

    // Would be set from MainWindow
    public User? CurrentUser { get; set; }

    public ApprovalCenterViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _jobRepository = new MailJobRepository(factory);

        PendingJobs = new ObservableCollection<PendingJobItem>();

        // Commands
        RefreshCommand = new AsyncRelayCommand(LoadPendingJobsAsync);
        ApproveCommand = new AsyncRelayCommand(ApproveSelectedJobAsync, () => SelectedJob != null);
        ShowRejectDialogCommand = new RelayCommand(_ => ShowRejectDialog = true, _ => SelectedJob != null);
        RejectCommand = new AsyncRelayCommand(RejectSelectedJobAsync);
        CancelRejectCommand = new RelayCommand(_ => CancelReject());

        // Load initial data
        _ = LoadPendingJobsAsync();
    }

    #region Properties

    public ObservableCollection<PendingJobItem> PendingJobs { get; }

    public PendingJobItem? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                OnPropertyChanged(nameof(HasSelectedJob));
            }
        }
    }

    public bool HasSelectedJob => SelectedJob != null;

    public string RejectionReason
    {
        get => _rejectionReason;
        set => SetProperty(ref _rejectionReason, value);
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

    public bool ShowRejectDialog
    {
        get => _showRejectDialog;
        set => SetProperty(ref _showRejectDialog, value);
    }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand ShowRejectDialogCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand CancelRejectCommand { get; }

    #endregion

    #region Methods

    private async Task LoadPendingJobsAsync()
    {
        IsLoading = true;
        try
        {
            var jobs = await _jobRepository.GetPendingJobsAsync();
            
            PendingJobs.Clear();
            foreach (var job in jobs)
            {
                PendingJobs.Add(new PendingJobItem
                {
                    JobId = job.JobId,
                    JobName = job.JobName,
                    Subject = job.JobName,
                    TotalRecipients = job.TotalRecipients,
                    SmtpAccountId = job.SmtpAccountId,
                    ListId = job.ListId
                });
            }

            StatusMessage = PendingJobs.Count > 0 
                ? $"📋 {PendingJobs.Count} job(s) awaiting approval" 
                : "✅ No pending approvals";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading jobs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApproveSelectedJobAsync()
    {
        if (SelectedJob == null) return;

        IsLoading = true;
        var jobName = SelectedJob.JobName;
        var jobId = SelectedJob.JobId;

        try
        {
            StatusMessage = $"⏳ Approving '{jobName}'...";

            var success = await _jobRepository.ApproveJobAsync(
                jobId, 
                CurrentUser?.UserId ?? 1);

            if (success)
            {
                StatusMessage = $"✅ Job '{jobName}' approved successfully!";
                
                // Remove from list
                PendingJobs.Remove(SelectedJob);
                SelectedJob = null;
            }
            else
            {
                StatusMessage = $"⚠️ Could not approve job. It may have already been processed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error approving job: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RejectSelectedJobAsync()
    {
        if (SelectedJob == null) return;
        if (string.IsNullOrWhiteSpace(RejectionReason))
        {
            StatusMessage = "⚠️ Please provide a reason for rejection.";
            return;
        }

        IsLoading = true;
        ShowRejectDialog = false;
        var jobName = SelectedJob.JobName;
        var jobId = SelectedJob.JobId;

        try
        {
            StatusMessage = $"⏳ Rejecting '{jobName}'...";

            var success = await _jobRepository.RejectJobAsync(
                jobId, 
                CurrentUser?.UserId ?? 1,
                RejectionReason);

            if (success)
            {
                StatusMessage = $"🚫 Job '{jobName}' rejected.";
                
                // Remove from list
                PendingJobs.Remove(SelectedJob);
                SelectedJob = null;
                RejectionReason = string.Empty;
            }
            else
            {
                StatusMessage = $"⚠️ Could not reject job. It may have already been processed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error rejecting job: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelReject()
    {
        ShowRejectDialog = false;
        RejectionReason = string.Empty;
    }

    #endregion
}

/// <summary>
/// View model item for pending jobs display.
/// </summary>
public class PendingJobItem
{
    public int JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SmtpAccountId { get; set; }
    public int ListId { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}
