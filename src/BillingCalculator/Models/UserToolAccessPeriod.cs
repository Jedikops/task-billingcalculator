namespace BillingCalculator.Models;

/// <summary>
/// Represents a single access period for a user on a specific tool/capability.
/// A user may have multiple access periods for the same tool if they were added, removed, and re-added.
/// </summary>
public class UserToolAccessPeriod
{
    /// <summary>
    /// Email address of the user (used as the unique identifier for billing grouping).
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Display name of the user.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Tool identifier (e.g., "FIGMA", "NEWRELIC", "JIRA").
    /// </summary>
    public string? Tool { get; set; }

    /// <summary>
    /// Tool capability identifier (e.g., "UX", "MONITORING", "BACKLOG").
    /// </summary>
    public string? Capability { get; set; }

    /// <summary>
    /// Custom role within the tool (e.g., "Full", "Collab", "Basic").
    /// Empty string or null when the tool has no custom roles.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// When the user was granted access to this tool/capability.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// When the user's access was revoked. Null if the user still has active access.
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}
