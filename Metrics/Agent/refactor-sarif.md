# Perform SARIF-focused refactoring

## Requirements
- Use the `metricsreporter` CLI to update metrics and fetch SARIF violation data for the symbols that require refactoring. Refer to `@3.2 - metricsreporter-cli.md` for CLI syntax and examples.
- Follow the workflow below so that every analyzer violation reported within the provided namespace is either refactored or intentionally suppressed.
- When you evaluate a violation, take guidance from the analyzer’s `message` and `shortDescription` and consult Microsoft’s documentation for the rule (for example, `https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CAxxxx`).
- Keep SOLID principles and the repository’s coding conventions front and center while modifying code.

## Follow this process until every violation is addressed

### 1. Fetch the current violation group

Run `metricsreporter readsarif` for the target namespace to retrieve the first SARIF group. Because `--metric` defaults to `Any`, you do not need to specify it unless you want to focus on a single SARIF metric:

```powershell
metricsreporter readsarif --namespace <target_namespace>
```

If the command responds with a human-readable message (instead of an object containing `ruleId`, `shortDescription`, `count`, and `violations`), there are no violations left in this namespace—stop; add `--report Metrics/MetricsReport.g.json` if not using the default config.

### 2. Study one violation

Pick the first violation in the returned group and examine the code referenced by `uri`, `startLine`–`endLine`, `message`, and `shortDescription`. Determine whether refactoring can resolve the issue without making the code harder to read or maintain.

Only suppress a violation after careful consideration (for example, after two failed refactoring attempts or when a change would degrade clarity). Add a `[SuppressMessage(...)]` attribute with an English justification (and `using System.Diagnostics.CodeAnalysis;` if missing). Examples:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Maintainability",
    "CA1506:AvoidExcessiveClassCoupling",
    Justification = "This orchestration point coordinates multiple services; splitting it would scatter closely related steps.")]
```

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "IDE0028:SimplifyCollectionInitialization",
    Justification = "The downstream serializer expects a concrete List<T>, so the explicit construction must be retained.")]
```

When you add a suppression, continue with the next violation in the group. When the group is finished, return to step 1.

### 3. Refactor and verify

If refactoring is feasible:

1. Plan the change in light of the analyzer’s recommendation.
2. Implement the refactor.
3. Run `dotnet build --no-incremental` (preferably the whole solution; focus on affected projects when time is constrained).
4. Run `dotnet test --no-build` (solution-wide if possible; targeted tests are acceptable after localized changes) and fix any regressions.

Steps 3 and 4 can cover an entire violation group once the required fixes are small.

### 4. Validate and repeat

After you have addressed all violations in a group, rerun `metricsreporter readsarif` with the namespace and the group's `ruleId`. 

```powershell
metricsreporter readsarif --namespace <target_namespace> --ruleid <ruleId>
```

If the command reports no violations, move on to the next group (return to step 1). If violations persist, go back to step 2. Two refactoring passes are allowed per group; after the second unsuccessful attempt, document the remaining issues with a suppression (English justification) and proceed.
