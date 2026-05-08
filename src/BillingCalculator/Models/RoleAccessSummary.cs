namespace BillingCalculator.Models;

/// <summary>
/// Represents the accumulated duration a user spent in a specific role for a tool.
/// </summary>
public class RoleAccessSummary
{
    /// <summary>
    /// The role name (e.g., "Full", "Collab", "Basic").
    /// </summary>
    public string Role { get; set; } = "";

    /// <summary>
    /// Total accumulated duration the user held this role within the billing period.
    /// </summary>
    public TimeSpan Duration { get; set; }
}
