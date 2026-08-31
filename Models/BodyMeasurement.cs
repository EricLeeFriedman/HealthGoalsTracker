using SQLite;

namespace HealthGoalsTracker.Models;

public class BodyMeasurement
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string UserId { get; set; } = "local";

    [Indexed]
    public string Date { get; set; } = string.Empty;

    public double? WeightLbs { get; set; }
    public double? BodyFatPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}