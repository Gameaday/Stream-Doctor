using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Fixes;

/// <summary>Whether a fix runs automatically or through the wizard.</summary>
public enum FixMode
{
    Guided = 0,
    Auto = 1,
}

/// <summary>Context for one execution attempt.</summary>
public sealed record FixExecutionOptions
{
    public bool DryRun { get; init; }

    /// <summary>The user pressed "Fix it for me" (or approved the wizard step).</summary>
    public bool UserApproved { get; init; }

    public FixMode Mode { get; init; } = FixMode.Guided;

    /// <summary>True whenever a stream or game is running. v1 never fixes then.</summary>
    public bool IsLive { get; init; }

    public bool IsElevated { get; init; }

    /// <summary>Confidence of the finding that triggered this fix.</summary>
    public double Confidence { get; init; } = 1.0;

    public double AutoFixThreshold { get; init; } = 0.85;
}

/// <summary>Why a fix was refused. Blocking is a feature, not an error.</summary>
public sealed record FixBlock(string Code, string Reason);

/// <summary>Extra read-only context the guard needs (device map for denylists).</summary>
public sealed record FixGuardOptions
{
    public IReadOnlyList<UsbDevice> Devices { get; init; } = [];
}

/// <summary>
/// Enforces the safety rules from the design doc §3 and §5. Every rule here is a
/// hard block: there is no override flag, because a wrong fix is unrecoverable trust damage.
/// </summary>
public sealed class FixGuard
{
    public const string CodeLive = "guard.live";
    public const string CodeApproval = "guard.approval";
    public const string CodeAutoUnsupported = "guard.auto-unsupported";
    public const string CodeLowConfidence = "guard.low-confidence";
    public const string CodeElevation = "guard.elevation";
    public const string CodeNoActions = "guard.no-actions";

    private readonly FixGuardOptions _defaultContext;

    public FixGuard(FixGuardOptions? defaultContext = null)
    {
        _defaultContext = defaultContext ?? new FixGuardOptions();
    }

    public FixBlock? Evaluate(FixPlaybook playbook, FixExecutionOptions options, FixGuardOptions? context = null)
    {
        ArgumentNullException.ThrowIfNull(playbook);
        ArgumentNullException.ThrowIfNull(options);

        if (options.IsLive)
        {
            return new FixBlock(CodeLive, "Stream Doctor never changes your setup while you are live or in a game. Run the fix after the stream.");
        }

        if (playbook.Actions.Count == 0)
        {
            return new FixBlock(CodeNoActions, "This playbook has nothing to execute; it is guidance only.");
        }

        if (options.Mode == FixMode.Auto && !playbook.AutoFixSupported)
        {
            return new FixBlock(CodeAutoUnsupported, $"'{playbook.Title}' always needs a human decision. Pick 'Walk me through it' instead.");
        }

        if (options.Mode == FixMode.Auto && options.Confidence < options.AutoFixThreshold)
        {
            return new FixBlock(
                CodeLowConfidence,
                $"Confidence is {options.Confidence:0.00}, below the auto-fix threshold of {options.AutoFixThreshold:0.00}. Confirm it manually.");
        }

        if (!options.UserApproved && (options.Mode == FixMode.Guided || playbook.Risk >= FixRisk.Medium))
        {
            return new FixBlock(CodeApproval, "This change needs your explicit approval first.");
        }

        if (playbook.Actions.Any(static action => action.RequiresElevation) && !options.IsElevated)
        {
            return new FixBlock(CodeElevation, "This change needs administrator rights. Approve the Windows prompt when it appears.");
        }

        var devices = (context ?? _defaultContext).Devices;
        foreach (var action in playbook.Actions.Where(static action => action.Kind == FixActionKind.DeviceReEnumeration))
        {
            var device = devices.FirstOrDefault(candidate =>
                string.Equals(candidate.InstanceId, action.DeviceInstanceId ?? action.Target, StringComparison.OrdinalIgnoreCase));

            if (device is null)
            {
                return new FixBlock(DeviceReEnumerationGuard.CodeUnknown, "The device could not be identified, so it will not be reset.");
            }

            var decision = DeviceReEnumerationGuard.Evaluate(device, devices);
            if (!decision.Allowed)
            {
                return new FixBlock(decision.Code, decision.Reason);
            }
        }

        return null;
    }
}
