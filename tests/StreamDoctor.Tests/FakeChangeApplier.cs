using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

/// <summary>In-memory effector used to test the journaled executor without a Windows box.</summary>
internal sealed class FakeChangeApplier : IChangeApplier
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public List<string> AppliedTargets { get; } = [];

    public List<string> RevertedTargets { get; } = [];

    public HashSet<string> FailOnApply { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Unsupported { get; } = new(StringComparer.Ordinal);

    public bool FailReads { get; init; }

    public bool CanApply(FixAction action) => !Unsupported.Contains(action.Target);

    public Task<string?> ReadAsync(FixAction action, CancellationToken cancellationToken) =>
        FailReads
            ? throw new IOException("cannot read the current value")
            : Task.FromResult(_values.GetValueOrDefault(action.Target));

    public Task ApplyAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken)
    {
        if (FailOnApply.Contains(action.Target))
        {
            throw new UnauthorizedAccessException("access denied");
        }

        _values[action.Target] = action.DesiredValue;
        AppliedTargets.Add(action.Target);
        return Task.CompletedTask;
    }

    public Task RevertAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken)
    {
        _values[action.Target] = beforeValue;
        RevertedTargets.Add(action.Target);
        return Task.CompletedTask;
    }

    public string? Value(string target) => _values.GetValueOrDefault(target);
}

/// <summary>Shared playbooks and options for the fix tests.</summary>
internal static class FixTestData
{
    public static FixExecutionOptions Approved() => new()
    {
        UserApproved = true,
        IsElevated = true,
    };

    public static FixPlaybook TwoStepPlaybook() => new()
    {
        Id = "test.two-step",
        Title = "Two-step test playbook",
        Summary = "For tests",
        Risk = FixRisk.Low,
        Actions =
        [
            new FixAction
            {
                Kind = FixActionKind.RegistryValue,
                Target = "a",
                DesiredValue = "0",
                Description = "Set a",
            },
            new FixAction
            {
                Kind = FixActionKind.RegistryValue,
                Target = "b",
                DesiredValue = "1",
                Description = "Set b",
            },
        ],
    };
}
