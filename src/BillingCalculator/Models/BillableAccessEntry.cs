namespace BillingCalculator.Models;

/// <summary>
/// The output of the billing calculation: one entry per unique (UserEmail, Tool, Capability) combination.
/// Summarizes all access periods within the billing window for billing purposes.
/// </summary>
public class BillableAccessEntry
{
    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Display name of the user.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Tool identifier (e.g., "FIGMA", "NEWRELIC").
    /// </summary>
    public string? Tool { get; set; }

    /// <summary>
    /// Tool capability identifier (e.g., "UX", "MONITORING").
    /// </summary>
    public string? Capability { get; set; }

    /// <summary>
    /// Total duration the user had access within the billing period.
    /// Multiple access periods are summed. Periods are clamped to the billing window.
    /// </summary>
    public TimeSpan TotalAccessDuration { get; set; }

    /// <summary>
    /// Whether the user still has active access at the end of the billing period
    /// (i.e., at least one period has RevokedAt == null).
    /// </summary>
    public bool IsStillActive { get; set; }

    /// <summary>
    /// The number of distinct access periods (grant → revoke cycles) within the billing period.
    /// </summary>
    public int AccessPeriodCount { get; set; }

    /// <summary>
    /// For tools with custom roles — the distinct roles the user held during the billing period,
    /// ordered from longest to shortest duration.
    /// Empty list when the tool has no custom roles.
    /// </summary>
    public List<RoleAccessSummary> RoleHistory { get; set; } = new();

    /// <summary>
    /// For tools with a known role-priority map (see <see cref="ToolRolePriority"/>) — the highest-priority
    /// role the user held during the billing period where the role's accumulated duration meets
    /// the minimum billable threshold. Null when the tool has no role tiers or no role qualifies.
    /// </summary>
    public string? BillableRoleName { get; set; }

    /// <summary>
    /// Total accumulated duration of <see cref="BillableRoleName"/> within the billing period.
    /// Zero when BillableRoleName is null.
    /// </summary>
    public TimeSpan BillableRoleDuration { get; set; }

    /// <summary>
    /// For still-active entries: the role currently in effect (the role of the still-open period).
    /// Null when the entry is not still active or the tool has no roles.
    /// </summary>
    public string? CurrentActiveRole { get; set; }
}
