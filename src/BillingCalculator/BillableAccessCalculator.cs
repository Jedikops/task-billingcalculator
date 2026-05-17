using BillingCalculator.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

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

    private record ClampedAccessPeriod(UserToolAccessPeriod AccessPeriod, DateTime ClampedStart, DateTime ClampedEnd);

    public static List<BillableAccessEntry> Calculate(
        IEnumerable<UserToolAccessPeriod> accessPeriods,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        TimeSpan minBillableRoleDuration = default)
    {
        var clumpedAccessPeriods = accessPeriods.Select(p =>
        {
            return new ClampedAccessPeriod(
                p,
                p.GrantedAt < billingPeriodStart ? billingPeriodStart : p.GrantedAt,
                p.RevokedAt != null ? (p.RevokedAt > billingPeriodEnd ? billingPeriodEnd : p.RevokedAt.Value) : billingPeriodEnd);

        }).Where(p => p.ClampedStart < p.ClampedEnd)
        .GroupBy(x => (x.AccessPeriod.UserEmail?.ToLowerInvariant(), x.AccessPeriod.Tool?.ToLowerInvariant(), x.AccessPeriod.Capability?.ToLowerInvariant()));

        var billableAccessEntries = clumpedAccessPeriods.Select(p =>
        {

            var firstPeriod = p.First().AccessPeriod;
            var totalAccessDuration = p.Sum(i => (i.ClampedEnd - i.ClampedStart).Ticks);
            var accessByRoles = p.Where(i => !string.IsNullOrEmpty(i.AccessPeriod.Role))
            .ToLookup(i => i.AccessPeriod.Role!); // A Lookup<TKey,TElement> resembles a Dictionary<TKey,TValue>.

            var roleHistory = accessByRoles
            //.ToDictionary(i => i.Key, j => TimeSpan.FromTicks(j.Sum(k => (k.ClampedEnd - k.ClampedStart).Ticks))) // Build a dictionary of Role => accumulated duration across all periods in the group.
            .Select(i => new RoleAccessSummary
            {
                Role = i.Key,
                Duration = TimeSpan.FromTicks(i.Sum(k => (k.ClampedEnd - k.ClampedStart).Ticks))
            }).OrderByDescending(i => i.Duration).ToList();

            var highestRoleAndDuration = ToolRolePriority.HasRolePriority(firstPeriod.Tool) ? roleHistory.Where(i => ToolRolePriority.GetPriority(firstPeriod.Tool, i.Role) > 0 && (i.Duration >= minBillableRoleDuration))
                .MaxBy(i => (ToolRolePriority.GetPriority(firstPeriod.Tool, i.Role), i.Duration)) : null;

            var isStillActive = p.Any(i => i.AccessPeriod.RevokedAt == null);

            var currentActiveRole = isStillActive ? p.FirstOrDefault(i => i.AccessPeriod.RevokedAt == null)?.AccessPeriod.Role : null;

            return new BillableAccessEntry()
            {
                UserEmail = firstPeriod.UserEmail,
                UserName = firstPeriod.UserName,
                Tool = firstPeriod.Tool,
                Capability = firstPeriod.Capability,
                TotalAccessDuration = TimeSpan.FromTicks(totalAccessDuration),
                IsStillActive = isStillActive, //When RevokedAt is not null but is beyond billingPeriodEnd, the period is not considered still active
                AccessPeriodCount = p.Count(),
                RoleHistory = roleHistory,
                BillableRoleName = highestRoleAndDuration?.Role,
                BillableRoleDuration = highestRoleAndDuration?.Duration ?? TimeSpan.Zero,
                CurrentActiveRole = currentActiveRole
            };
        }).OrderBy(p => p.Tool?.ToLowerInvariant()).ThenBy(p => p.UserEmail?.ToLowerInvariant());

        return billableAccessEntries.OrderBy(p => p.Tool).ThenBy(p => p.UserName).ToList();

    }

}
