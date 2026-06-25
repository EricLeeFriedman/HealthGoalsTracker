namespace HealthGoalsTracker.Controls;

// Two confetti modes:
//   PlayBurstAsync(origin)  — particles explode outward from a point (single-goal tap)
//   PlayAllGoalsAsync()     — particles rain down from the top (all goals complete)
public class ConfettiView : GraphicsView, IDrawable
{
    static readonly Color[] Palette =
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

    struct Particle
    {
        // Shared
        public float Delay, EndTime;
        public float W, H, CornerR;
        public float StartRot, SpinDeg;
        public Color Color;
        public bool IsBurst;

        // Rain mode
        public float StartX, StartY, DriftX, FallDist;

        // Burst mode
        public float OriginX, OriginY;
        public float LaunchDX, LaunchDY;  // pixels of travel at t=1 (before gravity)
        public float Gravity;             // extra downward pixels at t=1 (t^2 term)
    }

    readonly Random _rng = new();
    readonly List<Particle> _particles = [];
    Particle[] _snapshot = [];
    IDispatcherTimer? _timer;
    DateTime _timerStart;
    float _elapsed;

    public ConfettiView()
    {
        InputTransparent = true;
        IsVisible = false;
        Drawable = this;
    }

    public Task PlayBurstAsync(Point origin)
    {
        AddBurst(origin, count: 40, durationSec: 1.4f);
        return Task.CompletedTask;
    }

    public Task PlayAllGoalsAsync()
    {
        AddRain(count: 80, durationSec: 3.75f);
        return Task.CompletedTask;
    }

    // ── Burst: explosion from a point ────────────────────────────────────────

    void AddBurst(Point origin, int count, float durationSec)
    {
        float timeOffset = _timer != null ? _elapsed : 0f;
        float maxDelay   = durationSec * 0.15f;   // short stagger so it feels snappy

        for (int i = 0; i < count; i++)
        {
            float w     = _rng.Next(5, 12);
            float h     = _rng.Next(5, 12);
            float delay = (float)(_rng.NextDouble() * maxDelay);
            float speed = 120 + (float)(_rng.NextDouble() * 220); // 120-340 px
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);

            _particles.Add(new Particle
            {
                IsBurst  = true,
                OriginX  = (float)origin.X,
                OriginY  = (float)origin.Y,
                LaunchDX = speed * MathF.Cos(angle),
                LaunchDY = speed * MathF.Sin(angle),   // screen Y: positive = down
                Gravity  = 300 + (float)(_rng.NextDouble() * 250),  // 300-550 px downward pull
                StartRot = _rng.Next(0, 360),
                SpinDeg  = (float)((_rng.NextDouble() - 0.5) * 720),
                Delay    = timeOffset + delay,
                EndTime  = timeOffset + delay + Math.Max(durationSec - delay, 0.3f),
                W        = w,
                H        = h,
                CornerR  = _rng.Next(0, (int)(Math.Min(w, h) / 2 + 1)),
                Color    = Palette[_rng.Next(Palette.Length)],
            });
        }

        EnsureTimer();
    }

    // ── Rain: particles fall from the top ────────────────────────────────────

    void AddRain(int count, float durationSec)
    {
        double pageW = Width  > 1 ? Width  : Application.Current!.Windows[0].Width;
        double pageH = Height > 1 ? Height : Application.Current!.Windows[0].Height;

        float timeOffset = _timer != null ? _elapsed : 0f;
        float maxDelay   = durationSec / 3f;

        for (int i = 0; i < count; i++)
        {
            float w     = _rng.Next(6, 14);
            float h     = _rng.Next(6, 14);
            float delay = (float)(_rng.NextDouble() * maxDelay);

            _particles.Add(new Particle
            {
                IsBurst  = false,
                StartX   = (float)(_rng.NextDouble() * pageW),
                StartY   = -h,
                DriftX   = (float)((_rng.NextDouble() - 0.5) * pageW * 0.45),
                FallDist = (float)(pageH + h + _rng.NextDouble() * pageH * 0.25),
                StartRot = _rng.Next(0, 360),
                SpinDeg  = (float)((_rng.NextDouble() - 0.5) * 720),
                Delay    = timeOffset + delay,
                EndTime  = timeOffset + delay + Math.Max(durationSec - delay, 0.3f),
                W        = w,
                H        = h,
                CornerR  = _rng.Next(0, (int)(Math.Min(w, h) / 2 + 1)),
                Color    = Palette[_rng.Next(Palette.Length)],
            });
        }

        EnsureTimer();
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    void EnsureTimer()
    {
        IsVisible = true;

        if (_timer != null) return;

        _timerStart = DateTime.UtcNow;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    void OnTick(object? sender, EventArgs e)
    {
        _elapsed  = (float)(DateTime.UtcNow - _timerStart).TotalSeconds;
        _snapshot = [.. _particles];

        Invalidate();

        if (_particles.All(p => _elapsed >= p.EndTime))
        {
            _timer!.Stop();
            _timer.Tick -= OnTick;
            _timer     = null;
            _particles.Clear();
            _snapshot  = [];
            IsVisible  = false;
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float elapsed  = _elapsed;
        var   snapshot = _snapshot;

        foreach (var p in snapshot)
        {
            float age = elapsed - p.Delay;
            if (age <= 0) continue;

            float duration = p.EndTime - p.Delay;
            float t        = Math.Min(age / duration, 1f);

            float x, y;

            if (p.IsBurst)
            {
                // Projectile arc: constant horizontal + vertical launch, plus gravity (t^2)
                x = p.OriginX + p.LaunchDX * t;
                y = p.OriginY + p.LaunchDY * t + p.Gravity * t * t;
            }
            else
            {
                // Rain: SinIn easing on the fall
                float easedT = MathF.Sin(t * MathF.PI / 2);
                x = p.StartX + p.DriftX * t;
                y = p.StartY + p.FallDist * easedT;
            }

            float rot   = p.StartRot + p.SpinDeg * t;
            float alpha = p.IsBurst
                ? Math.Max(0f, 1f - t * 1.6f)          // burst fades quickly
                : (t < 0.6f ? 1f : (1f - t) / 0.4f);  // rain lingers then fades

            canvas.SaveState();
            canvas.FillColor = p.Color.WithAlpha(alpha);
            canvas.Rotate(rot, x + p.W / 2, y + p.H / 2);
            canvas.FillRoundedRectangle(x, y, p.W, p.H, p.CornerR);
            canvas.RestoreState();
        }
    }
}
