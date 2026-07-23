using ContestMonitor.Models;

namespace ContestMonitor.ViewModels;

/// <summary>Display wrapper for a contest row, with a "new/unseen" flag for the UI.</summary>
public sealed class ContestRow : ObservableObject
{
    private bool _isNew;

    public ContestRow(Contest contest, bool isNew)
    {
        Name = contest.Name;
        Start = string.IsNullOrWhiteSpace(contest.Start) ? "—" : contest.Start;
        RegistrationEnd = string.IsNullOrWhiteSpace(contest.RegistrationEnd) ? "—" : contest.RegistrationEnd;
        _isNew = isNew;
    }

    public string Name { get; }
    public string Start { get; }
    public string RegistrationEnd { get; }

    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }
}
