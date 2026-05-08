using System.Text.Json;
using BillingCalculator;
using BillingCalculator.Models;
using FluentAssertions;
using Xunit;

namespace BillingCalculator.Tests;

public class BillableAccessCalculatorTests
{
    private readonly DateTime _billingStart = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _billingEnd = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<UserToolAccessPeriod> LoadTestData(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", filename);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<UserToolAccessPeriod>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    // ─── Basic Behavior ────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_EmptyInput_ReturnsEmptyList()
    {
        var result = BillableAccessCalculator.Calculate(
            new List<UserToolAccessPeriod>(), _billingStart, _billingEnd);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_PeriodsEntirelyOutsideBillingWindow_ReturnsEmptyList()
    {
        var periods = new List<UserToolAccessPeriod>
        {
            new()
            {
                UserEmail = "user@test.com",
                UserName = "Test User",
                Tool = "JIRA",
                Capability = "BACKLOG",
                Role = "",
                GrantedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                RevokedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_PeriodSpanningStartBoundary_IsClampedToWindowStart()
    {
        // Period starts before billing window, ends during it
        var periods = new List<UserToolAccessPeriod>
        {
            new()
            {
                UserEmail = "user@test.com",
                UserName = "Test User",
                Tool = "JIRA",
                Capability = "BACKLOG",
                Role = "",
                GrantedAt = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                RevokedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        result.Should().ContainSingle();
        // Duration should be 9 days (Apr 1 00:00 → Apr 10 00:00), NOT 21 days (Mar 20 → Apr 10)
        result[0].TotalAccessDuration.Should().BeCloseTo(TimeSpan.FromDays(9), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Calculate_PeriodSpanningEndBoundary_IsClampedToWindowEnd()
    {
        // Period starts during billing window, ends after it
        var periods = new List<UserToolAccessPeriod>
        {
            new()
            {
                UserEmail = "user@test.com",
                UserName = "Test User",
                Tool = "JIRA",
                Capability = "BACKLOG",
                Role = "",
                GrantedAt = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc),
                RevokedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        result.Should().ContainSingle();
        // Duration should be 6 days (Apr 25 → May 1), NOT 20 days (Apr 25 → May 15)
        result[0].TotalAccessDuration.Should().BeCloseTo(TimeSpan.FromDays(6), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Calculate_ZeroDurationPeriod_IsExcluded()
    {
        // Period where GrantedAt == RevokedAt after clamping → zero duration → excluded
        var periods = new List<UserToolAccessPeriod>
        {
            new()
            {
                UserEmail = "user@test.com",
                UserName = "Test User",
                Tool = "JIRA",
                Capability = "BACKLOG",
                Role = "",
                GrantedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                RevokedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        result.Should().BeEmpty();
    }

    // ─── Grouping and Counting ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_UserAddedAndRemovedTwice_ProducesOneEntryWithTwoPeriods()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var jira = result.Single(e => e.Tool == "JIRA");
        jira.UserEmail.Should().Be("alice.johnson@acme.com");
        jira.AccessPeriodCount.Should().Be(2, "user was added and removed twice");
        jira.IsStillActive.Should().BeFalse("both periods have RevokedAt set");
    }

    [Fact]
    public void Calculate_StillActivePeriod_SetsIsStillActiveTrue()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var newRelic = result.Single(e => e.Tool == "NEWRELIC");
        newRelic.IsStillActive.Should().BeTrue("RevokedAt is null");
    }

    [Fact]
    public void Calculate_StillActivePeriod_UsesBillingEndForDuration()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var newRelic = result.Single(e => e.Tool == "NEWRELIC");
        // Granted Apr 5 09:00, billing ends May 1 → ~25.625 days
        var expectedDuration = _billingEnd - new DateTime(2026, 4, 5, 9, 0, 0, DateTimeKind.Utc);
        newRelic.TotalAccessDuration.Should().BeCloseTo(expectedDuration, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Calculate_CaseInsensitiveGrouping_GroupsDifferentCasesAsOne()
    {
        var periods = LoadTestData("scenario-4-edge-cases.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // "UPPER@TEST.COM" + "upper@test.com" with "figma"/"FIGMA" + "ux"/"UX" should be ONE entry
        var figmaEntries = result.Where(e =>
            string.Equals(e.Tool, "FIGMA", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.UserEmail, "upper@test.com", StringComparison.OrdinalIgnoreCase)).ToList();
        figmaEntries.Should().ContainSingle("case-insensitive grouping should merge both periods");
        figmaEntries[0].AccessPeriodCount.Should().Be(2);
    }

    [Fact]
    public void Calculate_MultipleUsersMultipleTools_ProducesSeparateEntries()
    {
        var periods = LoadTestData("scenario-3-multiple-users.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // Alice: FIGMA + JIRA = 2 entries
        // Bob: FIGMA + JIRA = 2 entries
        // Carol: NEWRELIC + FIGMA = 2 entries (SONARQUBE is outside billing window)
        result.Should().HaveCount(6);
        result.Where(e => string.Equals(e.UserEmail, "alice.johnson@acme.com", StringComparison.OrdinalIgnoreCase))
            .Should().HaveCount(2);
        result.Where(e => string.Equals(e.UserEmail, "bob.smith@widgets.io", StringComparison.OrdinalIgnoreCase))
            .Should().HaveCount(2);
        result.Where(e => string.Equals(e.UserEmail, "carol.white@example.org", StringComparison.OrdinalIgnoreCase))
            .Should().HaveCount(2);
    }

    [Fact]
    public void Calculate_PeriodsOutsideWindow_AreExcludedFromResults()
    {
        var periods = LoadTestData("scenario-3-multiple-users.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // Carol's SONARQUBE period (Mar 15-Mar 20) is entirely before billing window
        result.Should().NotContain(e =>
            string.Equals(e.Tool, "SONARQUBE", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Output Ordering ───────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ResultsAreOrderedByToolThenUserEmail()
    {
        var periods = LoadTestData("scenario-3-multiple-users.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // Should be sorted by Tool ascending, then UserEmail ascending
        var tools = result.Select(e => e.Tool?.ToUpperInvariant()).ToList();
        tools.Should().BeInAscendingOrder();

        // Within each tool group, emails should be sorted
        var figmaEmails = result
            .Where(e => string.Equals(e.Tool, "FIGMA", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.UserEmail?.ToUpperInvariant())
            .ToList();
        figmaEmails.Should().BeInAscendingOrder();
    }

    // ─── Role History ──────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ToolWithNoCustomRoles_HasEmptyRoleHistory()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var jira = result.Single(e => e.Tool == "JIRA");
        jira.RoleHistory.Should().BeEmpty("JIRA has no custom roles — Role field is empty");
    }

    [Fact]
    public void Calculate_ToolWithRoleChanges_TracksRoleHistoryOrderedByDuration()
    {
        var periods = LoadTestData("scenario-2-role-changes.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var figma = result.Single(e => e.Tool == "FIGMA");
        figma.RoleHistory.Should().HaveCount(3, "user had Full, Dev, and Collab roles");

        // Collab is still active (Apr 1 08:25 → May 1) = ~29.65 days → longest
        // Full: 15 min, Dev: 10 min
        figma.RoleHistory[0].Role.Should().Be("Collab", "Collab has the longest duration");
        figma.RoleHistory.Should().Contain(r => r.Role == "Full");
        figma.RoleHistory.Should().Contain(r => r.Role == "Dev");

        // Verify durations are descending
        for (int i = 1; i < figma.RoleHistory.Count; i++)
        {
            figma.RoleHistory[i].Duration.Should()
                .BeLessThanOrEqualTo(figma.RoleHistory[i - 1].Duration);
        }
    }

    [Fact]
    public void Calculate_StillActiveWithRole_SetsCurrentActiveRole()
    {
        var periods = LoadTestData("scenario-2-role-changes.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var figma = result.Single(e => e.Tool == "FIGMA");
        figma.IsStillActive.Should().BeTrue();
        figma.CurrentActiveRole.Should().Be("Collab", "last period (still active) has role Collab");
    }

    [Fact]
    public void Calculate_NotActiveEntry_CurrentActiveRoleIsNull()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var jira = result.Single(e => e.Tool == "JIRA");
        jira.IsStillActive.Should().BeFalse();
        jira.CurrentActiveRole.Should().BeNull();
    }

    // ─── Billable Role (Priority-Based) ────────────────────────────────────────────

    [Fact]
    public void Calculate_FigmaHighestRoleAboveThreshold_IsBillableRole()
    {
        // Bob had Full (15 min), Dev (10 min), Collab (still active, ~29 days)
        // With 10 min threshold: Full (15 min, priority 4) qualifies → billable role = Full
        var periods = LoadTestData("scenario-2-role-changes.json");

        var result = BillableAccessCalculator.Calculate(
            periods, _billingStart, _billingEnd, TimeSpan.FromMinutes(10));

        var figma = result.Single(e => e.Tool == "FIGMA");
        figma.BillableRoleName.Should().Be("Full",
            "Full has the highest priority (4) and duration (15 min) exceeds the 10 min threshold");
        figma.BillableRoleDuration.Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Calculate_FigmaHighestRoleBelowThreshold_FallsBackToNextQualifying()
    {
        // User had Full for 5 min, Dev for 5 min, Collab for 30 min
        // With 10 min threshold: Full (5 min) doesn't qualify, Dev (5 min) doesn't qualify
        // Collab (30 min, priority 2) qualifies → billable role = Collab
        var t0 = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var periods = new List<UserToolAccessPeriod>
        {
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Full", GrantedAt = t0, RevokedAt = t0.AddMinutes(5) },
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Dev", GrantedAt = t0.AddMinutes(5), RevokedAt = t0.AddMinutes(10) },
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Collab", GrantedAt = t0.AddMinutes(10), RevokedAt = t0.AddMinutes(40) },
        };

        List<BillableAccessEntry>? result = BillableAccessCalculator.Calculate(
            periods, _billingStart, _billingEnd, TimeSpan.FromMinutes(10));

        var entry = result.Should().ContainSingle().Subject;
        entry.BillableRoleName.Should().Be("Collab",
            "Full and Dev are below threshold; Collab is the highest qualifying role");
    }

    [Fact]
    public void Calculate_NoRoleMeetsThreshold_BillableRoleIsNull()
    {
        // All roles have < 10 min duration
        var t0 = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var periods = new List<UserToolAccessPeriod>
        {
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Full", GrantedAt = t0, RevokedAt = t0.AddMinutes(3) },
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Collab", GrantedAt = t0.AddMinutes(3), RevokedAt = t0.AddMinutes(8) },
        };

        var result = BillableAccessCalculator.Calculate(
            periods, _billingStart, _billingEnd, TimeSpan.FromMinutes(10));

        result.Should().ContainSingle().Which.BillableRoleName.Should().BeNull(
            "no role meets the minimum duration threshold");
    }

    [Fact]
    public void Calculate_ToolWithoutRolePriority_BillableRoleIsAlwaysNull()
    {
        // JIRA is not in the ToolRolePriority map
        var periods = new List<UserToolAccessPeriod>
        {
            new() { UserEmail = "u@x.com", UserName = "U", Tool = "JIRA", Capability = "BACKLOG",
                    Role = "SomeRole", GrantedAt = _billingStart.AddDays(1), RevokedAt = _billingStart.AddDays(20) },
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        result.Should().ContainSingle().Which.BillableRoleName.Should().BeNull(
            "JIRA has no role-priority map, so BillableRoleName must be null");
    }

    [Fact]
    public void Calculate_NewRelicFullAccessQualifies_BillableRoleIsFullAccess()
    {
        var periods = LoadTestData("scenario-2-role-changes.json");

        var result = BillableAccessCalculator.Calculate(
            periods, _billingStart, _billingEnd, TimeSpan.FromMinutes(10));

        var newRelic = result.Single(e => e.Tool == "NEWRELIC");
        newRelic.BillableRoleName.Should().Be("Full Access",
            "Full Access had 20 min duration (above 10 min threshold) and is priority 2 (highest for NR)");
        newRelic.BillableRoleDuration.Should().BeCloseTo(TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(1));
    }

    // ─── Null / Edge Case Handling ─────────────────────────────────────────────────

    [Fact]
    public void Calculate_NullEmailToolCapability_DoesNotThrow()
    {
        var periods = LoadTestData("scenario-4-edge-cases.json");

        var act = () => BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        act.Should().NotThrow();
    }

    [Fact]
    public void Calculate_NullFields_AreGroupedTogether()
    {
        var periods = LoadTestData("scenario-4-edge-cases.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // Two periods with null email/tool/capability should group into one entry
        var nullEntry = result.SingleOrDefault(e =>
            string.IsNullOrEmpty(e.UserEmail) && string.IsNullOrEmpty(e.Tool));
        nullEntry.Should().NotBeNull();
        nullEntry!.AccessPeriodCount.Should().Be(2);
    }

    [Fact]
    public void Calculate_BoundarySpanningPeriod_IsClampedCorrectly()
    {
        var periods = LoadTestData("scenario-4-edge-cases.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // "boundary@test.com" NEWRELIC period: Mar 20 08:00 → Apr 10 08:00
        // Clamped to: Apr 1 00:00 → Apr 10 08:00 = 9 days 8 hours
        var boundary = result.SingleOrDefault(e =>
            string.Equals(e.UserEmail, "boundary@test.com", StringComparison.OrdinalIgnoreCase));
        boundary.Should().NotBeNull();
        boundary!.TotalAccessDuration.Should().BeCloseTo(
            TimeSpan.FromDays(9).Add(TimeSpan.FromHours(8)), TimeSpan.FromMinutes(1));
        boundary.IsStillActive.Should().BeFalse();
    }

    [Fact]
    public void Calculate_PeriodEntirelyBeforeWindow_IsExcluded()
    {
        var periods = LoadTestData("scenario-4-edge-cases.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        // "outside@test.com" period (Mar 1 → Mar 15) should be excluded
        result.Should().NotContain(e =>
            string.Equals(e.UserEmail, "outside@test.com", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Total Duration Constraints ───────────────────────────────────────────────

    [Fact]
    public void Calculate_TotalDuration_NeverExceedsBillingPeriodLength()
    {
        var periods = LoadTestData("scenario-3-multiple-users.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var maxDuration = _billingEnd - _billingStart;
        result.Should().AllSatisfy(e =>
            e.TotalAccessDuration.Should().BeLessThanOrEqualTo(maxDuration));
    }

    [Fact]
    public void Calculate_MultiplePeriodsDuration_IsSumOfClampedPeriods()
    {
        var periods = LoadTestData("scenario-1-basic.json");

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var jira = result.Single(e => e.Tool == "JIRA");
        // Period 1: Apr 5 09:00 → Apr 10 17:30 = 5 days 8.5 hours
        // Period 2: Apr 15 08:00 → Apr 20 16:00 = 5 days 8 hours
        // Total ≈ 10 days 16.5 hours
        var expected = (new DateTime(2026, 4, 10, 17, 30, 0, DateTimeKind.Utc) -
                        new DateTime(2026, 4, 5, 9, 0, 0, DateTimeKind.Utc)) +
                       (new DateTime(2026, 4, 20, 16, 0, 0, DateTimeKind.Utc) -
                        new DateTime(2026, 4, 15, 8, 0, 0, DateTimeKind.Utc));
        jira.TotalAccessDuration.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    // ─── Default minBillableRoleDuration ───────────────────────────────────────────

    [Fact]
    public void Calculate_DefaultThresholdZero_AnyRoleQualifies()
    {
        // With default threshold (TimeSpan.Zero), even a 1-minute role qualifies
        var t0 = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var periods = new List<UserToolAccessPeriod>
        {
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "Full", GrantedAt = t0, RevokedAt = t0.AddMinutes(1) },
            new() { UserEmail = "x@x.com", UserName = "X", Tool = "FIGMA", Capability = "UX",
                    Role = "View", GrantedAt = t0.AddMinutes(1), RevokedAt = t0.AddHours(2) },
        };

        var result = BillableAccessCalculator.Calculate(periods, _billingStart, _billingEnd);

        var entry = result.Should().ContainSingle().Subject;
        entry.BillableRoleName.Should().Be("Full",
            "Full has priority 4 (highest) and with zero threshold, 1 minute is enough");
    }
}
