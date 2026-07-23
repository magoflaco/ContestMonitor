using ContestMonitor.Configuration;
using ContestMonitor.Models;

namespace ContestMonitor.Services;

public enum CheckStatus { Success, NoNewContests, Failed }

public sealed record CheckResult(
    CheckStatus Status,
    IReadOnlyList<Contest> AllContests,
    IReadOnlyList<Contest> NewContests,
    string? Error,
    DateTime TimestampLocal);

/// <summary>
/// The single source of truth for "check the site, diff against seen, notify".
/// Both the scheduler and the manual "Check now" button funnel through
/// <see cref="CheckAsync"/> (DRY) so behaviour is identical everywhere.
/// </summary>
public sealed class MonitorService
{
    private readonly AppSettings _settings;
    private readonly ContestScraper _scraper;
    private readonly SeenStore _seen;
    private readonly ToastNotifier _notifier;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<CheckResult>? CheckCompleted;

    public MonitorService(
        AppSettings settings,
        ContestScraper scraper,
        SeenStore seen,
        ToastNotifier notifier)
    {
        _settings = settings;
        _scraper = scraper;
        _seen = seen;
        _notifier = notifier;
    }

    public async Task<CheckResult> CheckAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CheckResult result;
            try
            {
                var contests = await _scraper.GetUpcomingContestsAsync(ct).ConfigureAwait(false);
                var fresh = _seen.FilterNew(contests);

                if (fresh.Count > 0)
                {
                    _seen.MarkSeen(fresh);
                    _notifier.NotifyNewContests(fresh);
                    result = new CheckResult(CheckStatus.Success, contests, fresh, null, DateTime.Now);
                }
                else
                {
                    result = new CheckResult(CheckStatus.NoNewContests, contests, fresh, null, DateTime.Now);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = new CheckResult(
                    CheckStatus.Failed, Array.Empty<Contest>(), Array.Empty<Contest>(),
                    ex.Message, DateTime.Now);
            }

            CheckCompleted?.Invoke(this, result);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }
}
