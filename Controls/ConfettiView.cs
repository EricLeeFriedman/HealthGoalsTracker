namespace HealthGoalsTracker.Controls;

// Confetti overlay built with pure MAUI animations — no SkiaSharp package required.
// Add to a page as a transparent, input-transparent child that spans the full area.
public class ConfettiView : AbsoluteLayout
{
    static readonly Color[] _palette =
    [
        Color.FromArgb("#F44336"), // red
        Color.FromArgb("#FFEB3B"), // yellow
        Color.FromArgb("#2196F3"), // blue
        Color.FromArgb("#4CAF50"), // green
        Color.FromArgb("#FF9800"), // orange
        Color.FromArgb("#9C27B0"), // purple
        Color.FromArgb("#E91E63"), // pink
        Color.FromArgb("#00BCD4"), // cyan
    ];

    readonly Random _rng = new();
    bool _isPlaying;

    public ConfettiView()
    {
        InputTransparent = true;
        IsVisible = false;
        BackgroundColor = Colors.Transparent;
    }

    public Task PlaySingleGoalAsync() => PlayAsync(particleCount: 35, durationMs: 1500);
    public Task PlayAllGoalsAsync()   => PlayAsync(particleCount: 80, durationMs: 3000);

    async Task PlayAsync(int particleCount, int durationMs)
    {
        if (_isPlaying) return;
        _isPlaying = true;
        IsVisible = true;
        Children.Clear();

        // Read layout size; fall back to window size if not yet measured.
        double pageW = Width  > 1 ? Width  : Application.Current!.Windows[0].Width;
        double pageH = Height > 1 ? Height : Application.Current!.Windows[0].Height;

        var tasks = new List<Task>(particleCount);

        for (int i = 0; i < particleCount; i++)
        {
            double w = _rng.Next(6, 14);
            double h = _rng.Next(6, 14);
            double startX = _rng.NextDouble() * pageW;

            var box = new BoxView
            {
                Color        = _palette[_rng.Next(_palette.Length)],
                WidthRequest = w,
                HeightRequest = h,
                CornerRadius = new CornerRadius(_rng.Next(0, (int)(Math.Min(w, h) / 2 + 1))),
                Rotation     = _rng.Next(0, 360),
                Opacity      = 1,
            };

            AbsoluteLayout.SetLayoutBounds(box, new Rect(startX, -h, w, h));
            Children.Add(box);

            int    delay      = _rng.Next(0, durationMs / 3);
            double driftX     = (_rng.NextDouble() - 0.5) * pageW * 0.45;
            double fallY      = pageH + h + _rng.NextDouble() * pageH * 0.25;
            double spinDeg    = (_rng.NextDouble() - 0.5) * 720;
            uint   effectiveD = (uint)Math.Max(durationMs - delay, 300);

            tasks.Add(AnimateParticle(box, delay, effectiveD, driftX, fallY, spinDeg));
        }

        await Task.WhenAll(tasks);

        Children.Clear();
        IsVisible = false;
        _isPlaying = false;
    }

    static async Task AnimateParticle(
        BoxView box, int delay, uint duration,
        double driftX, double fallY, double spinDeg)
    {
        if (delay > 0) await Task.Delay(delay);

        await Task.WhenAll(
            box.TranslateToAsync(driftX, fallY, duration, Easing.SinIn),
            box.RotateToAsync(box.Rotation + spinDeg, duration),
            FadeOutLate(box, duration)
        );
    }

    // Stays fully visible for 60% of the flight, then fades out.
    static async Task FadeOutLate(BoxView box, uint duration)
    {
        await Task.Delay((int)(duration * 0.6));
        await box.FadeToAsync(0, (uint)(duration * 0.4));
    }
}
