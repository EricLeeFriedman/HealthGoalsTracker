namespace HealthGoalsTracker.Messages;

// Sent via WeakReferenceMessenger when the user marks a goal complete.
// AllGoalsComplete: true when every goal for the day is now checked off.
// CardTapOrigin: window-relative position of the tap, used for the burst explosion origin.
public record CelebrationMessage(bool AllGoalsComplete, Point CardTapOrigin);
