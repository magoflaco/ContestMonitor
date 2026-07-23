using System.Net.Http;
using ContestMonitor.Configuration;
using ContestMonitor.Models;
using HtmlAgilityPack;

namespace ContestMonitor.Services;

/// <summary>
/// Downloads the contests page and extracts the "Upcoming contests" table.
///
/// Improvement over the original Python heuristic: the table is bounded so it
/// can only be the one that sits between the "Upcoming contests" heading and
/// the "Past contests" heading. If the upcoming section is empty (no table),
/// we return nothing instead of accidentally grabbing the past-contests table.
/// </summary>
public sealed class ContestScraper
{
    private static readonly string[] HeadingTags = { "h1", "h2", "h3", "h4", "h5" };

    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public ContestScraper(AppSettings settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9");
    }

    public async Task<IReadOnlyList<Contest>> GetUpcomingContestsAsync(CancellationToken ct = default)
    {
        var html = await _http.GetStringAsync(_settings.ContestsUrl, ct).ConfigureAwait(false);
        return Parse(html, _settings.ContestsUrl);
    }

    /// <summary>Pure parsing step, separated so it can be unit-tested offline.</summary>
    public static IReadOnlyList<Contest> Parse(string html, string pageUrl)
    {
        Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var upcoming = FindHeading(doc, "Upcoming contests");
        if (upcoming is null)
            return Array.Empty<Contest>();

        // Boundary: the past-contests heading (if present) marks where the
        // upcoming section ends.
        var past = FindHeading(doc, "Past contests");
        var boundary = past?.StreamPosition ?? int.MaxValue;

        var table = upcoming.SelectSingleNode("following::table[1]");
        if (table is null || table.StreamPosition > boundary)
            return Array.Empty<Contest>();

        var contests = new List<Contest>();
        foreach (var row in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count == 0)
                continue; // header row (th) or spacer

            var name = Clean(cells[0].InnerText);
            if (string.IsNullOrEmpty(name))
                continue;

            contests.Add(new Contest
            {
                Name = name,
                Start = cells.Count > 1 ? Clean(cells[1].InnerText) : string.Empty,
                RegistrationEnd = cells.Count > 2 ? Clean(cells[2].InnerText) : string.Empty,
                EnrollUrl = ExtractEnrollUrl(row, baseUri),
            });
        }

        return contests;
    }

    /// <summary>
    /// Pull the enrollment link from a row. The row's last cell contains
    /// anchors like "Sign up" (/teams/new) and "Log in" (/teams_sessions/new).
    /// Prefer the sign-up/register one; resolve to an absolute URL. Falls back
    /// to the contests page itself if no link is found.
    /// </summary>
    private static string ExtractEnrollUrl(HtmlNode row, Uri? baseUri)
    {
        var anchors = row.SelectNodes(".//a[@href]");
        HtmlNode? chosen = null;
        if (anchors is not null)
        {
            chosen = anchors.FirstOrDefault(a =>
            {
                var t = a.InnerText;
                return t.Contains("sign up", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("inscri", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("register", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("enroll", StringComparison.OrdinalIgnoreCase);
            }) ?? anchors[0];
        }

        var href = chosen?.GetAttributeValue("href", string.Empty).Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(href))
            return baseUri?.ToString() ?? string.Empty;

        if (baseUri is not null && Uri.TryCreate(baseUri, href, out var abs))
            return abs.ToString();

        return href;
    }

    private static HtmlNode? FindHeading(HtmlDocument doc, string text) =>
        doc.DocumentNode
            .Descendants()
            .FirstOrDefault(n =>
                HeadingTags.Contains(n.Name) &&
                n.InnerText.Contains(text, StringComparison.OrdinalIgnoreCase));

    private static string Clean(string raw) =>
        HtmlEntity.DeEntitize(raw ?? string.Empty).Replace('\u00A0', ' ').Trim();
}
