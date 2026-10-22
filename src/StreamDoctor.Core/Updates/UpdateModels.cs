namespace StreamDoctor.Core.Updates;

/// <summary>What kind of thing an update touches.</summary>
public enum UpdateKind
{
    Application,
    Plugin,
    Driver,
    Firmware,
    WindowsPatch,
}

/// <summary>Where the update comes from. Only two sources are verifiable by the OS.</summary>
public enum UpdateSource
{
    /// <summary>Windows Package Manager: installer signature verification is done by winget.</summary>
    Winget,

    /// <summary>Windows Update Agent: WHQL-signed drivers, firmware and Microsoft patches.</summary>
    WindowsUpdate,

    /// <summary>Vendor tool (iCUE, G HUB, Elgato…). Stream Doctor cannot verify these.</summary>
    VendorTool,

    /// <summary>Manual download; guidance only.</summary>
    Manual,
}

/// <summary>How urgent an update is.</summary>
public enum UpdateSeverity
{
    Optional = 0,
    Recommended = 1,
    Security = 2,
    Critical = 3,
}

/// <summary>What Stream Doctor will do about one update.</summary>
public enum UpdateDecision
{
    /// <summary>Patch-level, allowlisted and policy-enabled: may run without a click.</summary>
    AutoApply,

    /// <summary>Safe source, but needs the user to approve the fix (drivers, firmware, majors).</summary>
    ApprovalRequired,

    /// <summary>Informational only: vendor tools and manual downloads.</summary>
    NotifyOnly,

    /// <summary>Refused, e.g. the target version is on the known-bad list.</summary>
    Blocked,
}

/// <summary>
/// One available (or installed) update. <see cref="IsGuaranteedSource"/> is true only
/// for sources whose packages are verified by the OS itself.
/// </summary>
public sealed record UpdateCandidate
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required UpdateKind Kind { get; init; }

    public required UpdateSource Source { get; init; }

    public string? InstalledVersion { get; init; }

    public string? AvailableVersion { get; init; }

    public string? Publisher { get; init; }

    public UpdateSeverity Severity { get; init; } = UpdateSeverity.Recommended;

    public bool RequiresElevation { get; init; } = true;

    public bool RequiresRestart { get; init; }

    /// <summary>Name of the vendor tool that owns this update, for guided flows.</summary>
    public string? VendorToolName { get; init; }

    public string? Details { get; init; }

    public string? ReferenceUrl { get; init; }

    public long? SizeBytes { get; init; }

    /// <summary>True when the OS verifies the package signature (winget or Windows Update).</summary>
    public bool IsGuaranteedSource => Source is UpdateSource.Winget or UpdateSource.WindowsUpdate;

    public bool HasUpdate => !string.IsNullOrWhiteSpace(AvailableVersion);
}

/// <summary>One decision about one update, including the reason shown to the user.</summary>
public sealed record PlannedUpdate(
    UpdateCandidate Candidate,
    UpdateDecision Decision,
    string Reason,
    string? PlaybookId,
    KnownIssue? InstalledIssue = null);

/// <summary>Planner output for a sweep.</summary>
public sealed record UpdatePlan
{
    public IReadOnlyList<PlannedUpdate> Updates { get; init; } = [];

    public IEnumerable<PlannedUpdate> AutoApplicable =>
        Updates.Where(static update => update.Decision == UpdateDecision.AutoApply);

    public IEnumerable<PlannedUpdate> ApprovalRequired =>
        Updates.Where(static update => update.Decision == UpdateDecision.ApprovalRequired);

    public IEnumerable<PlannedUpdate> Notifications =>
        Updates.Where(static update => update.Decision is UpdateDecision.NotifyOnly or UpdateDecision.Blocked);

    public static UpdatePlan Empty { get; } = new();
}

/// <summary>
/// Guardrails for update execution. The defaults are deliberately conservative:
/// only patch-level application updates from winget may ever run without a click.
/// Drivers, firmware and Windows patches are always one-click approvals because the
/// FixGuard treats them as high-risk changes.
/// </summary>
public sealed record UpdatePolicy
{
    public bool AutoApplyApplications { get; init; } = true;

    /// <summary>When true, only x.y.z → x.y.z+n upgrades are auto-applied, never a major bump.</summary>
    public bool AutoApplyPatchLevelOnly { get; init; } = true;

    public int MaxAutoUpdatesPerSweep { get; init; } = 3;

    /// <summary>Known-bad versions. A target on this list is blocked; an installed one is flagged.</summary>
    public IReadOnlyList<KnownIssue> KnownIssues { get; init; } = [];

    public static UpdatePolicy Safe { get; } = new();

    /// <summary>Allows major application upgrades to auto-apply; everything else stays approval-only.</summary>
    public static UpdatePolicy Aggressive { get; } = new()
    {
        AutoApplyApplications = true,
        AutoApplyPatchLevelOnly = false,
        MaxAutoUpdatesPerSweep = 10,
    };
}
