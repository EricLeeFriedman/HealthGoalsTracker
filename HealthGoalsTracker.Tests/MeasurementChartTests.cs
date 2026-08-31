using HealthGoalsTracker.Controls;

namespace HealthGoalsTracker.Tests;

public class MeasurementChartTests
{
    [Fact]
    public void GetRange_AddsPaddingForMultipleValues()
    {
        var range = MeasurementChartView.GetRange([100, 200]);

        Assert.Equal(90, range.Min);
        Assert.Equal(210, range.Max);
    }

    [Fact]
    public void GetRange_AddsNonzeroPaddingForSingleValue()
    {
        var range = MeasurementChartView.GetRange([20]);

        Assert.True(range.Min < 20);
        Assert.True(range.Max > 20);
        Assert.True(range.Max - range.Min > 0);
    }
}
