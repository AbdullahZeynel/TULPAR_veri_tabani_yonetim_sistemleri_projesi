using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SiberMailer.Core.Models;

/// <summary>
/// A selectable wrapper for RecipientListWithCount to support multi-select in Campaign Wizard.
/// </summary>
public class SelectableRecipientList : INotifyPropertyChanged
{
    private bool _isSelected;

    public int ListId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public int ContactCount { get; set; }
    public int ActiveCount { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
