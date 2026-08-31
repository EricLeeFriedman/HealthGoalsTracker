using SQLite;

namespace HealthGoalsTracker.Models;

public class BodyMeasurement
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string UserId { get; set; } = "local";

    [Indexed]
    public string Date { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    public double? WeightLbs { get; set; }
    public double? BodyFatPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public DateOnly MeasurementDate => DateOnly.Parse(Date);

    [Ignore]
    public string DisplayDate => MeasurementDate.ToString("MMM d, yyyy");

    [Ignore]
    public string MeasurementSummary
    {
        get
        {
            var parts = new List<string>();
            if (WeightLbs.HasValue) parts.Add($"{WeightLbs.Value:0.##} lbs");
            if (BodyFatPercent.HasValue) parts.Add($"{BodyFatPercent.Value:0.##}% BF");
            return parts.Count > 0 ? string.Join(" • ", parts) : "Notes only";
        }
    }

    [Ignore]
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
