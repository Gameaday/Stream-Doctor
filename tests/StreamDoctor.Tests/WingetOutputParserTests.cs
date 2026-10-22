using StreamDoctor.Core.Updates;

namespace StreamDoctor.Tests;

public sealed class WingetOutputParserTests
{
    private const string UpgradeTable = """
        Name                              Id                              Version      Available    Source
        -------------------------------------------------------------------------------------------------
        OBS Studio                        OBSProject.OBSStudio            31.0.1       31.0.2       winget
        Microsoft Visual C++ Redistributable Microsoft.VCRedist.2015+.x64   14.38.33130  14.40.33810  winget
        Elgato Stream Deck                Elgato.StreamDeck               6.5.0        6.6.1        winget
        """;

    [Fact]
    public void Parse_ReadsTheUpgradeTable()
    {
        var packages = WingetOutputParser.Parse(UpgradeTable);

        Assert.Equal(3, packages.Count);
        Assert.Equal("OBSProject.OBSStudio", packages[0].Id);
        Assert.Equal("OBS Studio", packages[0].Name);
        Assert.Equal("31.0.1", packages[0].InstalledVersion);
        Assert.Equal("31.0.2", packages[0].AvailableVersion);
        Assert.Equal("winget", packages[0].Source);
        Assert.Equal("Microsoft.VCRedist.2015+.x64", packages[1].Id);
    }

    [Fact]
    public void Parse_IgnoresAnsiEscapesAndProgressNoise()
    {
        var noisy = "\u001b[?25l\u001b[2K\r   - \r\n" + UpgradeTable + "\n\u001b[?25h";

        var packages = WingetOutputParser.Parse(noisy);

        Assert.Equal(3, packages.Count);
    }

    [Fact]
    public void Parse_ReturnsNothingWhenWingetHasNoUpgrades()
    {
        Assert.Empty(WingetOutputParser.Parse("No installed package found matching input criteria."));
    }

    [Fact]
    public void Parse_TreatsUnknownVersionsAsMissing()
    {
        const string Table = """
            Name           Id                 Version   Available   Source
            -----------------------------------------------------------------
            Some App       Vendor.SomeApp     Unknown   2.0.0       winget
            """;

        var package = Assert.Single(WingetOutputParser.Parse(Table));

        Assert.Null(package.InstalledVersion);
        Assert.Equal("2.0.0", package.AvailableVersion);
    }

    [Fact]
    public void Parse_UsesTheFallbackWhenThereIsNoHeader()
    {
        const string Output = "OBS Studio  OBSProject.OBSStudio  31.0.1  31.0.2";

        var package = Assert.Single(WingetOutputParser.Parse(Output));

        Assert.Equal("OBSProject.OBSStudio", package.Id);
        Assert.Equal("31.0.2", package.AvailableVersion);
    }

    [Fact]
    public void FindInstalledVersion_MatchesThePackageIdCaseInsensitively()
    {
        Assert.Equal("31.0.1", WingetOutputParser.FindInstalledVersion(UpgradeTable, "obsproject.obsstudio"));
        Assert.Null(WingetOutputParser.FindInstalledVersion(UpgradeTable, "Missing.Package"));
    }

    [Fact]
    public void Parse_DoesNotTreatNamesAsPackageIds()
    {
        const string Table = """
            Name                    Id                          Version   Available   Source
            --------------------------------------------------------------------------------
            A Very Long Name Here   Not A Package Id            1.0       2.0         winget
            Real App                Vendor.RealApp              1.0       1.1         winget
            """;

        var packages = WingetOutputParser.Parse(Table);

        var package = Assert.Single(packages);
        Assert.Equal("Vendor.RealApp", package.Id);
    }
}
