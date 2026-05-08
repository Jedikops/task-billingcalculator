# Task: Billable Access Calculator

## Context

You are working on a SaaS platform that manages software tool licenses for development teams. Organizations create **projects** and add members who get access to various tools (e.g., Figma, New Relic, Jira, Azure DevOps, etc.).

The platform tracks when users are granted and revoked access to tools. At the end of each billing period (typically one month), the system needs to calculate billing entries — determining **who used what tool, for how long, and at which license tier**.

Your task is to implement the **`BillableAccessCalculator.Calculate()`** method that performs this calculation.

---

## Input: Access Periods

The input to your method is a list of **pre-computed access periods** (`UserToolAccessPeriod`). Each period represents a continuous time range during which a user had access to a specific tool capability:

```csharp
public class UserToolAccessPeriod
{
    public string? UserEmail { get; set; }      // Unique user identifier for grouping
    public string? UserName { get; set; }       // Display name
    public string? Tool { get; set; }           // Tool key, e.g., "FIGMA", "NEWRELIC", "JIRA"
    public string? Capability { get; set; }     // Capability within the tool, e.g., "UX", "MONITORING"
    public string? Role { get; set; }           // Custom role (tool-specific), empty if N/A
    public DateTime GrantedAt { get; set; }     // When access was granted
    public DateTime? RevokedAt { get; set; }    // When access was revoked; null = still active
}
```

### Key Facts About Access Periods

- A single user can have **multiple periods** for the same tool/capability (e.g., added on Apr 1, removed on Apr 5, re-added on Apr 10).
- `RevokedAt == null` means the user **still has active access** at the time of calculation.
- The `Role` field is only relevant for tools that have tiered licensing (e.g., Figma: View/Collab/Dev/Full). For tools without role tiers, `Role` is empty or null.
- Periods are **not guaranteed** to be sorted in any particular order.

---

## Output: Billable Access Entries

Your method produces one `BillableAccessEntry` per unique combination of **(UserEmail, Tool, Capability)**.

Even if a user was added and removed 5 times within the billing period, they produce **exactly one** billable entry for that tool/capability.

```csharp
public class BillableAccessEntry
{
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? Tool { get; set; }
    public string? Capability { get; set; }
    public TimeSpan TotalAccessDuration { get; set; }     // Sum of all clamped periods
    public bool IsStillActive { get; set; }               // Any period with RevokedAt == null?
    public int AccessPeriodCount { get; set; }            // Number of distinct access periods
    public List<RoleAccessSummary> RoleHistory { get; set; }  // Duration per role, desc by duration
    public string? BillableRoleName { get; set; }         // Highest-priority qualifying role
    public TimeSpan BillableRoleDuration { get; set; }    // Duration of the billable role
    public string? CurrentActiveRole { get; set; }        // Role of the still-open period
}
```

---

## Business Rules

### 1. Billing Window Clamping

The method receives `billingPeriodStart` and `billingPeriodEnd` parameters defining the billing window.

- **Periods that overlap the billing window** must be **clamped** to fit within it:
  - If `GrantedAt < billingPeriodStart` → treat as starting at `billingPeriodStart`
  - If `RevokedAt > billingPeriodEnd` (or is null) → treat end as `billingPeriodEnd`
- **Periods entirely outside the billing window** (clamped start ≥ clamped end) must be **excluded**.
- A period with `RevokedAt == null` is treated as ending at `billingPeriodEnd` for duration calculation purposes, but the entry should still be marked as `IsStillActive = true`.

### 2. Grouping

Group all periods by **(UserEmail, Tool, Capability)** using **case-insensitive** comparison.

- `"ALICE@ACME.COM"` and `"alice@acme.com"` are the same user.
- `"FIGMA"` and `"figma"` are the same tool.
- `null` or empty values should be treated as empty strings for grouping purposes.

### 3. Duration Calculation

For each group:
- Calculate `TotalAccessDuration` = sum of `(clampedEnd - clampedStart)` for all periods in the group.
- Only positive durations are summed (skip zero-duration periods).

### 4. Active Status

- `IsStillActive = true` if **any** period in the group has `RevokedAt == null` (after the period survives clamping — i.e., it was not excluded).
- **Important**: when clamping a period that has `RevokedAt == null`, the period remains "still active" even though its duration is calculated using `billingPeriodEnd`. The `RevokedAt` property of the clamped period should remain null to preserve this signal.
- When `RevokedAt` is not null but is beyond `billingPeriodEnd`, the period is **not** considered still active (it's simply clamped).

### 5. Role History

For tools with custom roles (non-empty `Role` field):
- Build a dictionary of `Role → accumulated duration` across all periods in the group.
- Convert to `List<RoleAccessSummary>` **ordered by Duration descending**.
- Ignore empty/null roles when building role history.

### 6. Billable Role (Maximum Historical Tier)

Some tools have a **role priority map** defined in `ToolRolePriority`:

| Tool      | Role         | Priority |
|-----------|--------------|----------|
| FIGMA     | View         | 1        |
| FIGMA     | Collab       | 2        |
| FIGMA     | Dev          | 3        |
| FIGMA     | Full         | 4        |
| NEWRELIC  | Basic        | 1        |
| NEWRELIC  | Full Access  | 2        |

**Billable role determination:**
1. Check if the tool has a role-priority map (`ToolRolePriority.HasRolePriority(tool)`).
2. If yes, from the role history, find roles where:
   - `ToolRolePriority.GetPriority(tool, role) > 0` (known role)
   - `duration >= minBillableRoleDuration` (meets threshold)
3. Among qualifying roles, pick the one with the **highest priority**.
4. If priorities are tied, pick the one with the **longest duration**.
5. Set `BillableRoleName` and `BillableRoleDuration` accordingly.
6. If no role qualifies (or the tool has no priority map), `BillableRoleName = null` and `BillableRoleDuration = TimeSpan.Zero`.

**Business context:** This ensures that if a user temporarily had a "Full" license (highest tier) for 15 minutes and was then downgraded to "Collab", they are still billed at the "Full" tier for that billing period. This prevents gaming where expensive licenses are briefly assigned and then downgraded.

### 7. Current Active Role

- If the entry `IsStillActive == true`, find the role of the still-open period (the one with `RevokedAt == null` after clamping).
- If there are multiple still-open periods with non-empty roles, pick the first one found.
- Set `CurrentActiveRole` to that role, or `null` if no role applies.

### 8. Output Ordering

Return the result list sorted by:
1. `Tool` ascending (case-insensitive)
2. Then `UserEmail` ascending (case-insensitive)

### 9. Entry Properties

For each grouped set of periods, populate the `BillableAccessEntry` using:
- `UserEmail`, `UserName`, `Tool`, `Capability` — from the first period in the group.
- `TotalAccessDuration` — sum of all clamped period durations.
- `IsStillActive` — true if any period has null RevokedAt (after clamping).
- `AccessPeriodCount` — number of periods that survived clamping (were not excluded).
- `RoleHistory` — accumulated durations per role, ordered descending.
- `BillableRoleName` / `BillableRoleDuration` — determined by priority rules.
- `CurrentActiveRole` — role of the still-active period.

---

## Method Signature

```csharp
public static List<BillableAccessEntry> Calculate(
    IEnumerable<UserToolAccessPeriod> accessPeriods,
    DateTime billingPeriodStart,
    DateTime billingPeriodEnd,
    TimeSpan minBillableRoleDuration = default)
```

---

## Examples

### Example 1: Simple Revoked Access

**Input:**
```
Period: alice@acme.com, JIRA, BACKLOG, role="", Apr 5 → Apr 10
```

**Billing window:** Apr 1 – May 1

**Output:**
```
UserEmail: alice@acme.com
Tool: JIRA
Capability: BACKLOG
TotalAccessDuration: 5 days
IsStillActive: false
AccessPeriodCount: 1
RoleHistory: []
BillableRoleName: null
```

### Example 2: Role Changes with Billing

**Input:**
```
Period 1: bob@x.com, FIGMA, UX, role="Full",   Apr 1 08:00 → Apr 1 08:15
Period 2: bob@x.com, FIGMA, UX, role="Dev",    Apr 1 08:15 → Apr 1 08:25
Period 3: bob@x.com, FIGMA, UX, role="Collab", Apr 1 08:25 → (still active)
```

**Billing window:** Apr 1 – May 1, **minBillableRoleDuration = 10 minutes**

**Output:**
```
UserEmail: bob@x.com
Tool: FIGMA
Capability: UX
TotalAccessDuration: ~30 days (sum of all three)
IsStillActive: true
AccessPeriodCount: 3
RoleHistory: [Collab: ~29.65 days, Full: 15 min, Dev: 10 min]
BillableRoleName: "Full" (priority 4, duration 15 min ≥ 10 min threshold)
BillableRoleDuration: 15 minutes
CurrentActiveRole: "Collab"
```

### Example 3: Period Outside Window

**Input:**
```
Period: user@x.com, JIRA, BACKLOG, role="", Mar 1 → Mar 15
```

**Billing window:** Apr 1 – May 1

**Output:** Empty list (period is entirely before the window).

---

## Provided Code (Do NOT Modify)

- `Models/UserToolAccessPeriod.cs` — input model
- `Models/BillableAccessEntry.cs` — output model
- `Models/RoleAccessSummary.cs` — role duration summary
- `Models/ToolRolePriority.cs` — static role priority configuration

## What You Need to Implement

- `BillableAccessCalculator.cs` → the `Calculate()` method

---

## Evaluation Criteria

1. **Correctness** — All unit tests pass.
2. **Code clarity** — Clean, readable implementation.
3. **Edge case handling** — Null values, empty inputs, boundary conditions.
4. **Performance awareness** — Reasonable approach (no unnecessary O(n³) loops).
