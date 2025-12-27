using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for viewing campaign send logs from /logs folder.
/// </summary>
public class LogViewModel : ViewModelBase
{
    private LogFileItem? _selectedLog;
    private string _selectedLogContent = string.Empty;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;

    public LogViewModel()
    {
        Logs = new ObservableCollection<LogFileItem>();
        
        // Commands
        RefreshCommand = new RelayCommand(_ => LoadLogs());
        ViewLogCommand = new RelayCommand(log => ViewLog(log as LogFileItem));
        DeleteLogCommand = new RelayCommand(log => DeleteLog(log as LogFileItem));
        SearchCommand = new RelayCommand(_ => SearchLogs());
        ClearSearchCommand = new RelayCommand(_ => ClearSearch());

        // Load logs on startup
        LoadLogs();
    }

    #region Properties

    public ObservableCollection<LogFileItem> Logs { get; }

    public LogFileItem? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (SetProperty(ref _selectedLog, value) && value != null)
            {
                LoadLogContent(value);
            }
        }
    }

    public string SelectedLogContent
    {
        get => _selectedLogContent;
        set => SetProperty(ref _selectedLogContent, value);
    }

    public bool HasSelectedLog => SelectedLog != null;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public int TotalLogs => Logs.Count;

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand ViewLogCommand { get; }
    public ICommand DeleteLogCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }

    #endregion

    #region Methods

    private void LoadLogs()
    {
        try
        {
            Logs.Clear();
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
                StatusMessage = "No logs found - logs folder created";
                return;
            }

            var logFiles = Directory.GetFiles(logDir, "*.txt")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToList();

            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                
                // Parse log type from filename
                var logType = fileName.StartsWith("send_") ? "📧 Campaign" :
                            fileName.StartsWith("error_") ? "❌ Error" :
                            "📄 Other";

                Logs.Add(new LogFileItem
                {
                    FileName = fileInfo.Name,
                    FilePath = fileInfo.FullName,
                    LogType = logType,
                    CreatedDate = fileInfo.LastWriteTime,
                    SizeKB = fileInfo.Length / 1024.0
                });
            }

            StatusMessage = $"Loaded {Logs.Count} log files";
            OnPropertyChanged(nameof(TotalLogs));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading logs: {ex.Message}";
        }
    }

    private void LoadLogContent(LogFileItem log)
    {
        try
        {
            if (File.Exists(log.FilePath))
            {
                SelectedLogContent = File.ReadAllText(log.FilePath);
                StatusMessage = $"Viewing: {log.FileName}";
            }
        }
        catch (Exception ex)
        {
            SelectedLogContent = $"Error reading log file:\n{ex.Message}";
            StatusMessage = "Error reading log file";
        }
        
        OnPropertyChanged(nameof(HasSelectedLog));
    }

    private void ViewLog(LogFileItem? log)
    {
        if (log != null)
        {
            SelectedLog = log;
        }
    }

    private void DeleteLog(LogFileItem? log)
    {
        if (log == null) return;

        try
        {
            if (File.Exists(log.FilePath))
            {
                File.Delete(log.FilePath);
                Logs.Remove(log);
                SelectedLog = null;
                SelectedLogContent = string.Empty;
                StatusMessage = $"Deleted: {log.FileName}";
                OnPropertyChanged(nameof(TotalLogs));
                OnPropertyChanged(nameof(HasSelectedLog));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting log: {ex.Message}";
        }
    }

    private void SearchLogs()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            LoadLogs();
            return;
        }

        var allLogs = Logs.ToList();
        Logs.Clear();

        foreach (var log in allLogs)
        {
            if (log.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                log.LogType.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                Logs.Add(log);
            }
        }

        StatusMessage = $"Found {Logs.Count} matching logs";
        OnPropertyChanged(nameof(TotalLogs));
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        LoadLogs();
    }

    #endregion
}

/// <summary>
/// Represents a log file.
/// </summary>
public class LogFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string LogType { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public double SizeKB { get; set; }
    
    public string DisplayDate => CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
    public string DisplaySize => $"{SizeKB:F1} KB";
}
