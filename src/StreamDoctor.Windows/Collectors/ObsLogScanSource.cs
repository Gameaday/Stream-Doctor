using System.Globalization;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Scanning;

namespace StreamDoctor.Windows.Collectors;

/// <summary>Finds and parses OBS Studio logs (design doc §10 step 1).</summary>
public static class ObsLogLoader
{
    private const int CandidateLogs = 3;
    private const string LogFilePattern = "*.txt";
    private const string LogFileDateFormat = "yyyy-MM-dd HH-mm-ss";

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "obs-studio",
        "logs");

    /// <summary>
    /// Parses the most relevant recent log: one whose session overlaps the requested
    /// window, otherwise the newest log that contains any session.
    /// </summary>
    public static ObsLogDocument? LoadLatest(
        ScanRequest request,
        string? directory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var folder = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var candidates = new DirectoryInfo(folder)
            .GetFiles(LogFilePattern)
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .Take(CandidateLogs)
            .Select(file => Parse(file, cancellationToken))
            .Where(static document => document is not null)
            .Select(static document => document!)
            .ToList();

        return candidates.FirstOrDefault(document => OverlapsWindow(document, request))
            ?? candidates.FirstOrDefault(static document => document.Sessions.Count > 0)
            ?? candidates.FirstOrDefault();
    }

    private static ObsLogDocument? Parse(FileInfo file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var text = File.ReadAllText(file.FullName);
            return ObsLogParser.Parse(text, LogDate(file), TimeZoneInfo.Local, file.Name);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool OverlapsWindow(ObsLogDocument document, ScanRequest request)
    {
        if (request.WindowStart is null)
        {
            return false;
        }

        var end = request.WindowEnd ?? request.Now;

        return document.Sessions.Any(session =>
            session.StartedAt <= end && (session.EndedAt ?? end) >= request.WindowStart);
    }

    private static DateOnly LogDate(FileInfo file) =>
        DateOnly.TryParseExact(
            Path.GetFileNameWithoutExtension(file.Name),
            LogFileDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : DateOnly.FromDateTime(file.LastWriteTime);
}

/// <summary>Scan source that contributes the parsed OBS log to a sweep.</summary>
public sealed class ObsLogScanSource : IScanSource
{
    private readonly string? _directory;

    public ObsLogScanSource(string? directory = null) => _directory = directory;

    public string Id => "obs-logs";

    public ScanModule OwnerModule => ScanModule.FramePacing;

    public bool IsOptional => true;

    public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () =>
            {
                var document = ObsLogLoader.LoadLatest(request, _directory, cancellationToken);
                if (document is null)
                {
                    return new ScanSourceResult
                    {
                        Note = "No OBS logs found. Install OBS or point Stream Doctor at its log folder.",
                    };
                }

                var events = document.Events.ToList();
                foreach (var warning in document.ParseWarnings)
                {
                    events.Add(new ScanEvent
                    {
                        Timestamp = document.FirstTimestamp ?? request.Now,
                        Source = ScanSource.ObsLog,
                        Module = ScanModule.ObsConfig,
                        Category = ScanEventCodes.ObsParseWarning,
                        Severity = Severity.Info,
                        Message = warning,
                    });
                }

                return new ScanSourceResult
                {
                    Events = events,
                    Obs = document,
                };
            },
            cancellationToken);
    }
}
