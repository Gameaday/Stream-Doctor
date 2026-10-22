using StreamDoctor.Core.Updates;

namespace StreamDoctor.Tests;

public sealed class SoftwareVersionTests
{
    [Theory]
    [InlineData("31.0.1", "31.0.2", -1)]
    [InlineData("31.0.2", "31.0.1", 1)]
    [InlineData("31.0", "31.0.0", 0)]
    [InlineData("1.2.3.4", "1.2.3", 1)]
    [InlineData("2024.10", "2024.9", 1)]
    public void Compare_HandlesDottedVersions(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(SoftwareVersion.Parse(left).CompareTo(SoftwareVersion.Parse(right))));
    }

    [Fact]
    public void PreRelease_SortsBeforeTheRelease()
    {
        Assert.True(SoftwareVersion.Parse("1.2.0-beta1") < SoftwareVersion.Parse("1.2.0"));
    }

    [Theory]
    [InlineData("v2.1.3", true)]
    [InlineData("31.0.1", true)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void TryParse_IsForgivingAboutPrefixesAndStrictAboutNumbers(string text, bool expected)
    {
        Assert.Equal(expected, SoftwareVersion.TryParse(text, out _));
    }

    [Fact]
    public void ToString_RoundTripsTheNumericParts()
    {
        Assert.Equal("31.0.2", SoftwareVersion.Parse("31.0.2").ToString());
    }

    [Theory]
    [InlineData("31.0.1", "31.0.2", true)]
    [InlineData("31.0.1", "31.0.10", true)]
    [InlineData("31.0.1", "31.1.0", false)]
    [InlineData("30.9.9", "31.0.0", false)]
    [InlineData("31.0.2", "31.0.1", false)]
    public void IsPatchLevelUpgradeFrom_OnlyAcceptsSameMajorMinorBumps(string installed, string available, bool expected)
    {
        Assert.Equal(
            expected,
            SoftwareVersion.Parse(available).IsPatchLevelUpgradeFrom(SoftwareVersion.Parse(installed)));
    }

    [Theory]
    [InlineData("*", "31.0.1")]
    [InlineData("31.0.1", "31.0.1")]
    [InlineData("31.0.*", "31.0.9")]
    [InlineData("31.0.0..31.0.4", "31.0.3")]
    [InlineData(">=31.0.0 <31.1.0", "31.0.7")]
    public void VersionRange_MatchesExpectedVersions(string spec, string version)
    {
        Assert.True(VersionRange.Parse(spec).Matches(version));
    }

    [Theory]
    [InlineData("31.0.1", "31.0.2")]
    [InlineData("31.0.*", "31.1.0")]
    [InlineData("31.0.0..31.0.4", "31.0.4")]
    [InlineData(">=31.0.0 <31.1.0", "31.1.0")]
    public void VersionRange_RejectsVersionsOutsideTheRange(string spec, string version)
    {
        Assert.False(VersionRange.Parse(spec).Matches(version));
    }

    [Fact]
    public void VersionRange_RejectsGarbage()
    {
        Assert.Throws<FormatException>(() => VersionRange.Parse("not-a-version"));
    }

    [Fact]
    public void KnownIssue_MatchesOnlyItsPackageAndRange()
    {
        var issue = new KnownIssue
        {
            Id = "obs-31.0.2-scene-switch",
            PackageId = "OBSProject.OBSStudio",
            AffectedVersions = VersionRange.Parse("31.0.2"),
            Summary = "Scene switching can hang the encoder.",
            Severity = UpdateSeverity.Critical,
            SafeVersionHint = "31.0.3",
        };

        Assert.True(issue.Matches("31.0.2"));
        Assert.False(issue.Matches("31.0.1"));
        Assert.False(issue.Matches("Unknown"));
    }
}
