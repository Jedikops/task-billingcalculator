using BillingCalculator.Models;

namespace BillingCalculator;

/// <summary>
/// Calculates billable access entries from raw access periods within a billing window.
/// See docs/TASK.md for the full specification of the expected behavior.
/// </summary>
public static class BillableAccessCalculator
{
    /// <summary>
    /// Takes pre-computed access periods and a billing window, then produces one
    /// <see cref="BillableAccessEntry"/> per unique (UserEmail, Tool, Capability) combination.
    /// Each user is billed at most once per tool/capability, even if they were added/removed multiple times.
    /// For tools with custom roles, the distinct roles and their durations are tracked.
    /// </summary>
    /// <param name="accessPeriods">Pre-computed access periods to process.</param>
    /// <param name="billingPeriodStart">Start of the billing window (inclusive).</param>
    /// <param name="billingPeriodEnd">End of the billing window (exclusive).</param>
    /// <param name="minBillableRoleDuration">
    /// Minimum accumulated duration a role must have been held to qualify as the billable role.
    /// Defaults to TimeSpan.Zero (any duration qualifies).
    /// </param>
    /// <returns>
    /// List of billable entries, ordered by Tool ascending then UserEmail ascending.
    /// </returns>
    public static List<BillableAccessEntry> Calculate(
        IEnumerable<UserToolAccessPeriod> accessPeriods,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        TimeSpan minBillableRoleDuration = default)
    {
        // TODO: Implement this method according to the specification in docs/TASK.md
        throw new NotImplementedException("Implement the billing calculation logic.");
    }
}
