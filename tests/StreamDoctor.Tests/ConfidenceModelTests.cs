using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class ConfidenceModelTests
{
    [Fact]
    public void Score_WithNoSignals_IsZero()
    {
        Assert.Equal(0, ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 0, 0));
    }

    [Fact]
    public void Score_AddsBaseWeightPerDistinctSignal()
    {
        Assert.Equal(0.35, ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 1, 0), precision: 3);
        Assert.Equal(0.70, ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 2, 0), precision: 3);
    }

    [Fact]
    public void Score_SaturatesAndClampsToOne()
    {
        Assert.Equal(1.0, ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, ConfidenceModel.MaxSupportingSignals, 0), precision: 3);
        Assert.Equal(1.0, ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 100, 0), precision: 3);
    }

    [Fact]
    public void Score_ContradictingSignalsLowerConfidence()
    {
        var supported = ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 2, 0);
        var contradicted = ConfidenceModel.Score(ConfidenceModel.DefaultBaseWeight, 2, 1);

        Assert.True(contradicted < supported);
        Assert.Equal(0.45, contradicted, precision: 3);
    }

    [Fact]
    public void Score_NeverGoesNegative()
    {
        Assert.Equal(0, ConfidenceModel.Score(0.1, 1, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Score_WithUnusableBaseWeight_IsZero(double baseWeight)
    {
        Assert.Equal(0, ConfidenceModel.Score(baseWeight, 3, 0));
    }

    [Fact]
    public void Tally_CountsARepeatedSignalOnceButKeepsEveryOccurrence()
    {
        var tally = new EvidenceTally();
        var first = Event(TestData.At(20, 0));
        var second = Event(TestData.At(21, 0));

        tally.Support("device:mic", first);
        tally.Support("device:mic", second);

        Assert.Equal(1, tally.DistinctSupportingSignals);
        Assert.Equal(2, tally.SupportingEventCount);
        Assert.Equal(2, tally.Evidence.Count);
        Assert.Equal(0.35, tally.Score(ConfidenceModel.DefaultBaseWeight), precision: 3);
    }

    [Fact]
    public void Tally_OrdersEvidenceOldestFirst()
    {
        var tally = new EvidenceTally();
        tally.Support("b", Event(TestData.At(21)));
        tally.Support("a", Event(TestData.At(20)));

        Assert.Equal(TestData.At(20), tally.Evidence[0].Timestamp);
    }

    [Fact]
    public void Tally_UsesDeviceKeyWhenNoExplicitSignalKeyIsGiven()
    {
        var tally = new EvidenceTally();
        tally.Support(new ScanEvent
        {
            Timestamp = TestData.At(20),
            Source = ScanSource.ObsLog,
            Module = ScanModule.FramePacing,
            Category = ScanEventCodes.ObsSourceDisconnected,
            Severity = Severity.Warning,
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ScanEventPayload.Device] = "Microphone",
            },
        });

        Assert.Equal(1, tally.DistinctSupportingSignals);
    }

    private static ScanEvent Event(DateTimeOffset timestamp) => new()
    {
        Timestamp = timestamp,
        Source = ScanSource.ObsLog,
        Module = ScanModule.UsbSentinel,
        Category = ScanEventCodes.ObsSourceDisconnected,
        Severity = Severity.Warning,
    };
}
