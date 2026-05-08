# Billable Access Calculator — Coding Task

## Overview

Implement a billing calculation engine that determines how much each user should be billed for tool access within a given billing period.

This is a real-world problem from a SaaS platform that manages tool licenses (Figma, New Relic, Jira, etc.) for development teams. Users are granted and revoked access over time, and at the end of each month the system needs to produce billing entries.

## Time Expectation

**~2 hours** — This is designed as a focused coding exercise, not a multi-day project.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Getting Started

1. Read the full specification: **[docs/TASK.md](docs/TASK.md)**
2. Implement your solution in: **`src/BillingCalculator/BillableAccessCalculator.cs`**
3. Run the tests to verify: `dotnet test`

## Project Structure

```
├── docs/
│   └── TASK.md                              ← Full specification & business rules
├── src/
│   └── BillingCalculator/
│       ├── BillableAccessCalculator.cs      ← YOUR IMPLEMENTATION GOES HERE
│       └── Models/
│           ├── UserToolAccessPeriod.cs      ← Input model (provided)
│           ├── BillableAccessEntry.cs       ← Output model (provided)
│           ├── RoleAccessSummary.cs         ← Helper model (provided)
│           └── ToolRolePriority.cs          ← Static config (provided, do NOT modify)
└── tests/
    └── BillingCalculator.Tests/
        ├── BillableAccessCalculatorTests.cs ← Test suite (25 tests)
        └── TestData/                        ← JSON test scenarios
```

## What's Provided (Do NOT Modify)

- All model classes in `Models/`
- The `ToolRolePriority` static class (defines role priority tiers per tool)
- The full test suite

## What You Need to Implement

The single static method:

```csharp
public static List<BillableAccessEntry> Calculate(
    IEnumerable<UserToolAccessPeriod> accessPeriods,
    DateTime billingPeriodStart,
    DateTime billingPeriodEnd,
    TimeSpan minBillableRoleDuration = default)
```

## Running Tests

```bash
dotnet test
```

All 25 tests should pass when your implementation is correct.

## Evaluation Criteria

| Criteria | Weight |
|----------|--------|
| **Correctness** — All tests pass | High |
| **Code clarity** — Clean, readable, well-structured | Medium |
| **Edge case handling** — Nulls, boundaries, empty inputs | Medium |
| **Performance awareness** — No unnecessary complexity | Low |

## Tips

- Start by reading `docs/TASK.md` carefully — all rules are documented there.
- The tests are ordered from simple to complex — tackle them in order.
- Pay attention to case-insensitive comparisons.
- The `ToolRolePriority` class is already provided — just call its methods.
- `minBillableRoleDuration` defaults to `TimeSpan.Zero` — handle both default and non-default cases.

## Result

Please submit your implementation as a GitHub repository publicly accessible.



Good luck!
