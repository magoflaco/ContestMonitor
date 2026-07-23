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

        if (Uri.TryCreate(contest.EnrollUrl, UriKind.Absolute, out var uri))
            EnrollUri = uri;
    }

    public string Name { get; }
    public string Start { get; }
    public string RegistrationEnd { get; }

    /// <summary>Enrollment link; null when the page had no usable URL.</summary>
    public Uri? EnrollUri { get; }

    public bool HasEnroll => EnrollUri is not null;

    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }
}
