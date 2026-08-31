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

    [Fact]
    public void GetDateX_SpacesMeasurementsByElapsedDays()
    {
        var plot = new Microsoft.Maui.Graphics.RectF(10, 0, 100, 50);
        var first = new DateOnly(2026, 8, 1);
        var last = new DateOnly(2026, 8, 11);

        Assert.Equal(10, MeasurementChartView.GetDateX(first, first, last, plot));
        Assert.Equal(60, MeasurementChartView.GetDateX(new DateOnly(2026, 8, 6), first, last, plot));
        Assert.Equal(110, MeasurementChartView.GetDateX(last, first, last, plot));
    }

    [Fact]
    public void GetDateX_CentersSingleDate()
    {
        var plot = new Microsoft.Maui.Graphics.RectF(10, 0, 100, 50);
        var date = new DateOnly(2026, 8, 31);

        Assert.Equal(60, MeasurementChartView.GetDateX(date, date, date, plot));
    }
}
