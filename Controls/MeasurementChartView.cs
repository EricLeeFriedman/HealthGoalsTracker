using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Controls;

public class MeasurementChartView : GraphicsView, IDrawable
{
    public static readonly BindableProperty MeasurementsProperty = BindableProperty.Create(
        nameof(Measurements),
        typeof(ObservableCollection<BodyMeasurement>),
        typeof(MeasurementChartView),
        null,
        propertyChanged: OnMeasurementsChanged);

    public ObservableCollection<BodyMeasurement>? Measurements
    {
        get => (ObservableCollection<BodyMeasurement>?)GetValue(MeasurementsProperty);
        set => SetValue(MeasurementsProperty, value);
    }

    public MeasurementChartView()
    {
        Drawable = this;
        HeightRequest = 280;
    }

    public static void OnMeasurementsChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        var chart = (MeasurementChartView)bindable;

        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= chart.OnCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += chart.OnCollectionChanged;

        chart.Invalidate();
    }

    public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Invalidate();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);

        var points = Measurements?
            .Select(item => new
            {
                Measurement = item,
                HasDate = DateOnly.TryParseExact(
                    item.Date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date),
                Date = date
            })
            .Where(item => item.HasDate)
            .OrderBy(item => item.Date)
            .ToList();

        if (points == null || points.Count == 0)
        {
            DrawEmptyState(canvas, dirtyRect);
            return;
        }

        const float left = 48;
        const float right = 48;
        const float top = 30;
        const float bottom = 42;

        var plot = new RectF(
            dirtyRect.Left + left,
            dirtyRect.Top + top,
            Math.Max(1, dirtyRect.Width - left - right),
            Math.Max(1, dirtyRect.Height - top - bottom));

        DrawAxes(canvas, plot);

        var firstDate = points[0].Date;
        var lastDate = points[^1].Date;
        var dateSpan = Math.Max(1, lastDate.DayNumber - firstDate.DayNumber);

        float GetX(DateOnly date) =>
            plot.Left + ((date.DayNumber - firstDate.DayNumber) / (float)dateSpan) * plot.Width;

        DrawDateLabels(canvas, plot, firstDate, lastDate);

        var weightValues = points
            .Where(item => item.Measurement.WeightLbs.HasValue)
            .Select(item => item.Measurement.WeightLbs!.Value)
            .ToList();
        var bodyFatValues = points
            .Where(item => item.Measurement.BodyFatPercent.HasValue)
            .Select(item => item.Measurement.BodyFatPercent!.Value)
            .ToList();

        if (weightValues.Count > 0)
        {
            var weightRange = GetRange(weightValues);
            DrawScaleLabels(canvas, plot, weightRange, true, "lbs", Color.FromArgb("#5D3FD3"));
            DrawSeries(
                canvas,
                points
                    .Where(item => item.Measurement.WeightLbs.HasValue)
                    .Select(item => (
                        X: GetX(item.Date),
                        Value: item.Measurement.WeightLbs!.Value)),
                plot,
                weightRange,
                Color.FromArgb("#5D3FD3"));
        }

        if (bodyFatValues.Count > 0)
        {
            var bodyFatRange = GetRange(bodyFatValues);
            DrawScaleLabels(canvas, plot, bodyFatRange, false, "%", Color.FromArgb("#00897B"));
            DrawSeries(
                canvas,
                points
                    .Where(item => item.Measurement.BodyFatPercent.HasValue)
                    .Select(item => (
                        X: GetX(item.Date),
                        Value: item.Measurement.BodyFatPercent!.Value)),
                plot,
                bodyFatRange,
                Color.FromArgb("#00897B"));
        }

        if (weightValues.Count == 0 && bodyFatValues.Count == 0)
            DrawEmptyState(canvas, plot, "Add weight or body fat to draw the chart.");
    }

    public static (double Min, double Max) GetRange(IReadOnlyCollection<double> values)
    {
        var min = values.Min();
        var max = values.Max();
        var span = max - min;
        var padding = span > 0 ? span * 0.1 : Math.Max(Math.Abs(min) * 0.05, 1);
        return (min - padding, max + padding);
    }

    public static void DrawAxes(ICanvas canvas, RectF plot)
    {
        canvas.StrokeColor = Color.FromArgb("#D0D0D0");
        canvas.StrokeSize = 1;

        for (var step = 0; step <= 4; step++)
        {
            var y = plot.Top + plot.Height * step / 4;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
        }

        canvas.StrokeColor = Color.FromArgb("#707070");
        canvas.DrawLine(plot.Left, plot.Top, plot.Left, plot.Bottom);
        canvas.DrawLine(plot.Right, plot.Top, plot.Right, plot.Bottom);
        canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }

    public static void DrawDateLabels(
        ICanvas canvas,
        RectF plot,
        DateOnly firstDate,
        DateOnly lastDate)
    {
        canvas.FontColor = Color.FromArgb("#606060");
        canvas.FontSize = 11;
        canvas.DrawString(
            firstDate.ToString("MMM d"),
            plot.Left,
            plot.Bottom + 8,
            70,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Top);
        canvas.DrawString(
            lastDate.ToString("MMM d"),
            plot.Right - 70,
            plot.Bottom + 8,
            70,
            20,
            HorizontalAlignment.Right,
            VerticalAlignment.Top);
    }

    public static void DrawScaleLabels(
        ICanvas canvas,
        RectF plot,
        (double Min, double Max) range,
        bool isLeft,
        string unit,
        Color color)
    {
        canvas.FontColor = color;
        canvas.FontSize = 10;

        for (var step = 0; step <= 4; step++)
        {
            var value = range.Max - (range.Max - range.Min) * step / 4;
            var y = plot.Top + plot.Height * step / 4 - 8;
            var x = isLeft ? plot.Left - 46 : plot.Right + 4;
            canvas.DrawString(
                $"{value:0.#}{unit}",
                x,
                y,
                42,
                16,
                isLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }
    }

    public static void DrawSeries(
        ICanvas canvas,
        IEnumerable<(float X, double Value)> source,
        RectF plot,
        (double Min, double Max) range,
        Color color)
    {
        var values = source.ToList();
        if (values.Count == 0) return;

        float GetY(double value) =>
            plot.Bottom - (float)((value - range.Min) / (range.Max - range.Min)) * plot.Height;

        if (values.Count > 1)
        {
            var path = new PathF();
            path.MoveTo(values[0].X, GetY(values[0].Value));
            foreach (var value in values.Skip(1))
                path.LineTo(value.X, GetY(value.Value));

            canvas.StrokeColor = color;
            canvas.StrokeSize = 2.5f;
            canvas.DrawPath(path);
        }

        canvas.FillColor = color;
        foreach (var value in values)
            canvas.FillCircle(value.X, GetY(value.Value), 4);
    }

    public static void DrawEmptyState(
        ICanvas canvas,
        RectF bounds,
        string message = "No measurements yet.")
    {
        canvas.FontColor = Color.FromArgb("#707070");
        canvas.FontSize = 14;
        canvas.DrawString(
            message,
            bounds,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }
}
