using HealthGoalsTracker.Models;
using Microsoft.Maui.Graphics;

namespace HealthGoalsTracker.Controls;

// GraphicsView dual-axis line chart for body measurements.
// Weight uses the left axis, body-fat % uses the right axis.
public class MeasurementsChartView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList<BodyMeasurement>),
        typeof(MeasurementsChartView),
        null,
        propertyChanged: OnItemsSourceChanged);

    public IList<BodyMeasurement>? ItemsSource
    {
        get => (IList<BodyMeasurement>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public MeasurementsChartView()
    {
        Drawable = this;
        HeightRequest = 280;
    }

    public static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((MeasurementsChartView)bindable).Invalidate();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.Antialias = true;

        var measurements = (ItemsSource ?? [])
            .Where(m => m.WeightLbs.HasValue || m.BodyFatPercent.HasValue)
            .OrderBy(m => m.Date)
            .ToList();

        if (measurements.Count == 0)
        {
            DrawEmptyState(canvas, dirtyRect);
            canvas.RestoreState();
            return;
        }

        var weightValues = measurements.Where(m => m.WeightLbs.HasValue).Select(m => m.WeightLbs!.Value).ToList();
        var bodyFatValues = measurements.Where(m => m.BodyFatPercent.HasValue).Select(m => m.BodyFatPercent!.Value).ToList();

        var plotRect = new RectF(48, 34, Math.Max(0, dirtyRect.Width - 96), Math.Max(0, dirtyRect.Height - 78));
        if (plotRect.Width <= 0 || plotRect.Height <= 0)
        {
            canvas.RestoreState();
            return;
        }

        var weightRange = GetRange(weightValues, 3);
        var bodyFatRange = GetRange(bodyFatValues, 2);

        DrawLegend(canvas, dirtyRect);
        DrawGrid(canvas, plotRect);
        DrawAxisLabels(canvas, plotRect, weightRange, bodyFatRange);
        DrawDateLabels(canvas, plotRect, measurements);
        DrawSeries(canvas, plotRect, measurements, weightRange, bodyFatRange, true, Color.FromArgb("#1976D2"));
        DrawSeries(canvas, plotRect, measurements, weightRange, bodyFatRange, false, Color.FromArgb("#8E24AA"));

        canvas.RestoreState();
    }

    public void DrawEmptyState(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Color.FromArgb("#777777");
        canvas.FontSize = 14;
        canvas.DrawString(
            "Add a weight or body-fat entry to see your chart.",
            dirtyRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    public (double Min, double Max) GetRange(List<double> values, double minPadding)
    {
        if (values.Count == 0)
            return (0, 1);

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 0.001)
        {
            min -= minPadding;
            max += minPadding;
        }
        else
        {
            var padding = Math.Max(minPadding, (max - min) * 0.12);
            min -= padding;
            max += padding;
        }

        if (min < 0) min = 0;
        return (min, max);
    }

    public void DrawLegend(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontSize = 12;
        canvas.FontColor = Color.FromArgb("#555555");

        canvas.FillColor = Color.FromArgb("#1976D2");
        canvas.FillCircle(20, 16, 4);
        canvas.DrawString("Weight", 30, 8, 70, 16, HorizontalAlignment.Left, VerticalAlignment.Center);

        canvas.FillColor = Color.FromArgb("#8E24AA");
        canvas.FillCircle(110, 16, 4);
        canvas.DrawString("Body Fat %", 120, 8, 90, 16, HorizontalAlignment.Left, VerticalAlignment.Center);

        canvas.FontColor = Color.FromArgb("#999999");
        canvas.DrawString("Recent trend", dirtyRect.Width - 120, 8, 100, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
    }

    public void DrawGrid(ICanvas canvas, RectF plotRect)
    {
        canvas.StrokeColor = Color.FromArgb("#E6E6E6");
        canvas.StrokeSize = 1;

        for (int i = 0; i < 4; i++)
        {
            var y = plotRect.Top + (plotRect.Height / 3f) * i;
            canvas.DrawLine(plotRect.Left, y, plotRect.Right, y);
        }

        canvas.StrokeColor = Color.FromArgb("#CCCCCC");
        canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
        canvas.DrawLine(plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
        canvas.DrawLine(plotRect.Right, plotRect.Top, plotRect.Right, plotRect.Bottom);
    }

    public void DrawAxisLabels(ICanvas canvas, RectF plotRect, (double Min, double Max) weightRange, (double Min, double Max) bodyFatRange)
    {
        canvas.FontSize = 11;

        DrawAxisLabelSet(canvas, plotRect.Left - 42, plotRect, weightRange, "#1976D2", "lbs", HorizontalAlignment.Right);
        DrawAxisLabelSet(canvas, plotRect.Right + 6, plotRect, bodyFatRange, "#8E24AA", "%", HorizontalAlignment.Left);
    }

    public void DrawAxisLabelSet(ICanvas canvas, float x, RectF plotRect, (double Min, double Max) range, string colorHex, string suffix, HorizontalAlignment alignment)
    {
        canvas.FontColor = Color.FromArgb(colorHex);

        for (int i = 0; i < 4; i++)
        {
            var value = range.Max - ((range.Max - range.Min) / 3d) * i;
            var y = plotRect.Top + (plotRect.Height / 3f) * i - 8;
            canvas.DrawString($"{value:0.#}{suffix}", x, y, 40, 16, alignment, VerticalAlignment.Center);
        }
    }

    public void DrawDateLabels(ICanvas canvas, RectF plotRect, List<BodyMeasurement> measurements)
    {
        var labels = new HashSet<int> { 0, measurements.Count - 1 };
        if (measurements.Count > 2) labels.Add(measurements.Count / 2);

        canvas.FontSize = 11;
        canvas.FontColor = Color.FromArgb("#777777");

        foreach (var index in labels.OrderBy(i => i))
        {
            var x = GetX(index, measurements.Count, plotRect) - 24;
            var y = plotRect.Bottom + 8;
            var label = measurements[index].MeasurementDate.ToString("M/d");
            canvas.DrawString(label, x, y, 48, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    public void DrawSeries(
        ICanvas canvas,
        RectF plotRect,
        List<BodyMeasurement> measurements,
        (double Min, double Max) weightRange,
        (double Min, double Max) bodyFatRange,
        bool isWeightSeries,
        Color color)
    {
        var previousPoint = new PointF?();
        canvas.StrokeColor = color;
        canvas.StrokeSize = 3;

        for (int i = 0; i < measurements.Count; i++)
        {
            var measurement = measurements[i];
            var value = isWeightSeries ? measurement.WeightLbs : measurement.BodyFatPercent;
            if (!value.HasValue) continue;

            var x = GetX(i, measurements.Count, plotRect);
            var y = GetY(value.Value, isWeightSeries ? weightRange : bodyFatRange, plotRect);
            var point = new PointF(x, y);

            if (previousPoint.HasValue)
                canvas.DrawLine(previousPoint.Value, point);

            previousPoint = point;
        }

        foreach (var (measurement, index) in measurements.Select((m, i) => (m, i)))
        {
            var value = isWeightSeries ? measurement.WeightLbs : measurement.BodyFatPercent;
            if (!value.HasValue) continue;

            var x = GetX(index, measurements.Count, plotRect);
            var y = GetY(value.Value, isWeightSeries ? weightRange : bodyFatRange, plotRect);
            canvas.FillColor = color;
            canvas.FillCircle(x, y, 4.5f);
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(x, y, 4.5f);
            canvas.StrokeColor = color;
            canvas.StrokeSize = 3;
        }
    }

    public float GetX(int index, int count, RectF plotRect)
    {
        if (count <= 1) return plotRect.Left + plotRect.Width / 2f;
        return plotRect.Left + (plotRect.Width / (count - 1)) * index;
    }

    public float GetY(double value, (double Min, double Max) range, RectF plotRect)
    {
        if (Math.Abs(range.Max - range.Min) < 0.001)
            return plotRect.Top + plotRect.Height / 2f;

        var normalized = (value - range.Min) / (range.Max - range.Min);
        return (float)(plotRect.Bottom - normalized * plotRect.Height);
    }
}
