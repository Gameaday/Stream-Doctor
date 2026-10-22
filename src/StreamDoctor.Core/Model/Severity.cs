namespace StreamDoctor.Core.Model;

/// <summary>
/// How much a finding hurts the user, independent of how confident the engine is.
/// Values are ordered so that <c>&gt;=</c> comparisons express "at least as bad as".
/// </summary>
public enum Severity
{
    /// <summary>Neutral observation; useful in a timeline but never a problem.</summary>
    Info = 0,

    /// <summary>Worth knowing, low risk. Does not turn the board yellow on its own.</summary>
    Advisory = 1,

    /// <summary>Degrades the stream experience; should be fixed before going live.</summary>
    Warning = 2,

    /// <summary>Breaks the stream or the setup; fix before going live.</summary>
    Critical = 3,
}
