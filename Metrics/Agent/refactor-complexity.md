Refactor code in the given namespace to achieve the required Cyclomatic Complexity metric: the metric value must be no more than 15 for classes and no more than 100 for methods. The corresponding levels are called Type and Member.

## Requirements

- Use the `metricsreporter` CLI to update and retrieve metric values for symbols requiring refactoring, one request at a time. Description and usage examples are provided in `@3.2 - metricsreporter-cli.md`.
- Strictly follow the workflow described below to achieve the goal: reduce the metric for all symbols in the namespace mentioned above to acceptable limits.
- When reducing Complexity, use simplification techniques provided by C# and .NET:
	•	Splitting large methods into small, single‑purpose methods and moving branching logic into them.  
	•	Using guard clauses and early returns to reduce nesting, but be aware that some analyzers count each `return` as a branch.  
	•	Replacing long `if/else if` or `switch` chains with polymorphism, strategy/state patterns, or lookup tables (dictionaries mapping values to handlers/results) and configuration structures instead of hardcoded branches.  
	•	Merging duplicate or overlapping conditions into single ones, eliminating repeated branches, and removing unreachable or dead code.  
	•	Extracting complex loops or conditions into well‑named helper methods (each handling one processing step) or LINQ chains if it doesn't harm readability or create hidden branching.  
	•	Moving complex branching logic into separate classes using strategy, state, or template method patterns to reduce decisions within a single method.  
- It is **forbidden to use "dummy" classes and methods**, i.e., delegation wrapper methods created solely to reduce the metric but lacking architectural meaning. Prefer suppression in case when there is no way to reduce metrics further.
- Follow SOLID principles and the rules established in the project.
- Maintain nullable reference type annotations correctly when refactoring.

## Strictly follow this instruction until all symbols are fixed

### 1. Get problematic symbol

Using the `metricsreporter read` command, get the first "problematic" symbol that requires refactoring. A problematic symbol is one where the threshold is exceeded (`metricsreporter` handles this comparison automatically). Use `--symbol-kind Any` to prioritize classes first, then members in the same namespace.

Example request to `metricsreporter` with the required options:

```powershell
metricsreporter read --namespace <given_namespace> --metric Complexity --symbol-kind Any
```

If you receive a message that no suitable symbols are found (instead of an object with fields `symbolFqn`, `symbolType`, `metric`, `value`, `threshold`, `delta`, `filePath`, `status`, `isSuppressed`), this means there are no problematic symbols: complete the task.

### 2. Analyze symbol

Begin working on the symbol. Study the code of the class or method and related classes and methods. Make a decision about the possibility of refactoring.

There is only one reason to cancel refactoring: refactoring should not be performed if further simplification techniques lead to deterioration in code readability and maintainability. If refactoring is canceled, make a suppression for the symbol. Example suppression:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Maintainability",
    "CA1502:AvoidExcessiveComplexity",
    Justification = "Complex validation logic requires multiple conditional branches to handle all edge cases; splitting would reduce clarity of the validation flow.")]
```

If refactoring is canceled, add the suppression to the code and start from step 1.

### 3. Perform refactoring

If refactoring is possible, proceed as follows:

1. Plan the refactoring.
2. Perform the refactoring. Update XML documentation for all modified public members according to project rules.
3. Verify that the solution build is successful: `dotnet build --no-incremental`. If the build fails, fix the code until the build is green. Use the test-driven-development method (red-refactor-green).
4. Check that there are no compiler warnings or errors in the modified files. If there are, fix them.
5. After verifying the build, check that tests pass: `dotnet test --no-build`. If tests fail, fix the code as described in the previous step.

### 4. Verify result

Using the `metricsreporter test` command, verify that the symbol you worked on is fixed.

Example request to `metricsreporter` with the required options:

```powershell
metricsreporter test --symbol <symbol_been_refactored> --metric Complexity
```

If you see `"isOk": false` in the response, return to step 2 with this symbol. The number of additional refactoring attempts to achieve the required metric: 5 attempts per symbol (applies to both classes and methods). If after the fifth additional attempt the required metric is not achieved, then add a suppression attribute with a Justification message in English that fully explains the essence of the problem, if any (for example, that five attempts were insufficient for a proper refactoring), or simply a description of the reason why this symbol cannot be refactored (for example, that it contains complex business logic with many conditional branches that cannot be simplified without losing domain clarity).

If `"isOk": true`, proceed to the next symbol as described in step 1.

