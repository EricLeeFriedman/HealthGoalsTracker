namespace HealthGoalsTracker.Messages;

// Sent via WeakReferenceMessenger when the user marks a goal complete.
// AllGoalsComplete is true when every goal for the day is now checked off.
public record CelebrationMessage(bool AllGoalsComplete);
