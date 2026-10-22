using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Separates OBS's three failure modes, which streamers constantly confuse
/// (design doc §2.2): dropped = network, lagged = GPU, skipped = encoder.
/// </summary>
public sealed partial class ObsFrameHealthRule : IFindingRule
{
    private const double BaseWeight = 0.45;

    public string Id => "obs.frames.health";

    public ScanModule OwnerModule => ScanModule.FramePacing;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = context.Obs?.LastStreamSession
            ?? context.Obs?.RecordingSessions.LastOrDefault(static candidate => candidate.IsComplete);

        if (session is null)
        {
            yield break;
        }

        var total = EffectiveTotal(session.Stats);
        foreach (var failure in Describe(session.Stats, total))
        {
            yield return Create(context, session, failure);
        }
    }

    private static IEnumerable<FrameFailure> Describe(ObsSessionStats stats, int total)
    {
        if (stats.DroppedFrames is > 0)
        {
            var percent = PercentOf(stats.DroppedPercent, stats.DroppedFrames.Value, total);
            yield return new FrameFailure(
                ScanEventCodes.ObsFramesDropped,
                "obs.frames.dropped",
                "dropped",
                percent,
                SeverityFor(percent, warning: 1, critical: 5),
                "Your PC sent the frames, but the network lost some of them. Dropped frames never reach Twitch, so this is a connection problem, not a PC problem.",
                "Test wired vs Wi-Fi, drop the bitrate by about 20%, or switch ingest servers.");
        }

        if (stats.LaggedFrames is > 0)
        {
            var percent = PercentOf(stats.LaggedPercent, stats.LaggedFrames.Value, total);
            yield return new FrameFailure(
                ScanEventCodes.ObsRenderLag,
                "obs.frames.lagged",
                "lagged",
                percent,
                SeverityFor(percent, warning: 0.5, critical: 3),
                "OBS could not render in time, so the stream stuttered while the game itself ran fine. That is a GPU bottleneck, not the network.",
                "Cap the game frame rate, lower graphics a notch, or output 1080p60 instead of 1440p/4K.");
        }

        if (stats.SkippedFrames is > 0)
        {
            var percent = PercentOf(stats.SkippedPercent, stats.SkippedFrames.Value, total);
            yield return new FrameFailure(
                ScanEventCodes.ObsEncoderOverload,
                "obs.frames.skipped",
                "skipped",
                percent,
                SeverityFor(percent, warning: 1, critical: 5),
                "The GPU rendered fine, but the encoder could not keep up, so OBS threw frames away before sending them. That is an encoder or thermals problem.",
                "Use NVENC/AMF/QSV, move the x264 preset one step toward faster, or lower output resolution.");
        }
    }
}
