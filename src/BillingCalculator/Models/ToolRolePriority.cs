namespace BillingCalculator.Models;

/// <summary>
/// Defines per-tool role priorities for billing purposes.
/// Higher priority = more expensive license tier.
/// A user is always billed at the highest-priority role they held during the billing period
/// (provided the role's accumulated duration meets the minimum threshold).
/// This class is PROVIDED — you do NOT need to modify it.
/// </summary>
public static class ToolRolePriority
{
    private static readonly Dictionary<string, Dictionary<string, int>> Priorities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FIGMA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = 1,
                ["Collab"] = 2,
                ["Dev"] = 3,
                ["Full"] = 4,
            },
            ["NEWRELIC"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Basic"] = 1,
                ["Full Access"] = 2,
            },
        };

    /// <summary>
    /// Returns the priority of a role for a given tool. Higher value = more expensive tier.
    /// Returns 0 for unknown tools or unknown roles.
    /// </summary>
    public static int GetPriority(string? toolKey, string? roleName)
    {
        if (string.IsNullOrEmpty(toolKey) || string.IsNullOrEmpty(roleName))
            return 0;

        return Priorities.TryGetValue(toolKey, out var roles)
            && roles.TryGetValue(roleName, out var priority)
            ? priority
            : 0;
    }

    /// <summary>
    /// Returns true if the tool has a known role-priority map (i.e., is billed per role tier).
    /// </summary>
    public static bool HasRolePriority(string? toolKey)
        => !string.IsNullOrEmpty(toolKey) && Priorities.ContainsKey(toolKey);
}
