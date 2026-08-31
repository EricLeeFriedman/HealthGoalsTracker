using HealthGoalsTracker.Models;
using HealthGoalsTracker.ViewModels;
using Microsoft.Maui.Graphics;

namespace HealthGoalsTracker.Tests;

public class PresentationModelTests
{
    [Theory]
    [InlineData(0, 14, 0)]
    [InlineData(7, 14, 50)]
    [InlineData(14, 14, 100)]
    [InlineData(5, 0, 0)]
    public void DailyRecord_CompletionPercentUsesEarnedOverPossible(
        int earned,
        int possible,
        double expected)
    {
        var record = new DailyRecord
        {
            TotalPointsEarned = earned,
            TotalPointsPossible = possible
        };

        Assert.Equal(expected, record.CompletionPercent);
    }

    [Theory]
    [InlineData(false, 0, "#F44336")]
    [InlineData(false, 1, "#FF9800")]
    [InlineData(false, 49.9, "#FF9800")]
    [InlineData(false, 50, "#FFC107")]
    [InlineData(false, 99.9, "#FFC107")]
    [InlineData(false, 100, "#4CAF50")]
    [InlineData(false, 120, "#4CAF50")]
    public void CalendarDay_BackgroundMatchesHeatmapThresholds(
        bool isFuture,
        double completionPercent,
        string expectedColor)
    {
        var day = new CalendarDayViewModel
        {
            Date = new DateOnly(2026, 8, 31),
            IsFuture = isFuture,
            CompletionPercent = completionPercent
        };

        Assert.Equal(Color.FromArgb(expectedColor), day.BackgroundColor);
    }

    [Fact]
    public void CalendarDay_FutureUsesGrayWhileNoDataAndPaddingAreEmpty()
    {
        var noData = new CalendarDayViewModel { Date = new DateOnly(2026, 8, 31) };
        var future = new CalendarDayViewModel
        {
            Date = new DateOnly(2026, 9, 1),
            IsFuture = true,
            CompletionPercent = 100
        };
        var padding = new CalendarDayViewModel { IsEmpty = true };

        Assert.Equal(Colors.Transparent, noData.BackgroundColor);
        Assert.Equal(Color.FromArgb("#E0E0E0"), future.BackgroundColor);
        Assert.Equal(Colors.Transparent, padding.BackgroundColor);
        Assert.True(noData.IsSelectable);
        Assert.False(future.IsSelectable);
        Assert.False(padding.IsSelectable);
    }

    [Fact]
    public void GoalCard_ReflectsCompletionAndPointOrWeeklyBadge()
    {
        var card = new GoalCardViewModel { Points = 1 };
        Assert.Equal(Color.FromArgb("#E53935"), card.CardColor);
        Assert.Equal("1 pt", card.PointsBadgeText);
        Assert.Equal("", card.CompletionIcon);

        card.Points = 3;
        card.IsCompleted = true;
        Assert.Equal(Color.FromArgb("#43A047"), card.CardColor);
        Assert.Equal("3 pts", card.PointsBadgeText);
        Assert.Equal("✓", card.CompletionIcon);

        card.IsWeeklyOnly = true;
        Assert.Contains("Weekly", card.PointsBadgeText);
    }

    [Fact]
    public void BodyMeasurement_FormatsDateValuesAndNotesForRecentHistory()
    {
        var measurement = new BodyMeasurement
        {
            Date = "2026-08-31",
            WeightLbs = 180.25,
            BodyFatPercent = 20,
            Notes = "Morning"
        };

        Assert.Equal(new DateOnly(2026, 8, 31), measurement.MeasurementDate);
        Assert.Equal("Aug 31, 2026", measurement.DisplayDate);
        Assert.Equal("180.25 lbs • 20% BF", measurement.MeasurementSummary);
        Assert.True(measurement.HasNotes);
    }

    [Fact]
    public void GoalBreakdownItem_UsesCompletionStatusIcons()
    {
        var item = new GoalBreakdownItem();
        Assert.Equal("❌", item.StatusIcon);

        item.IsCompleted = true;

        Assert.Equal("✅", item.StatusIcon);
    }
}
