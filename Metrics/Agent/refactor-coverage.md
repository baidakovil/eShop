# Increase test coverage for OpenCover branch coverage

- Refactor code in the given namespace to achieve the required `OpenCoverBranchCoverage` metric: all methods in classes must have sufficient branch coverage 75 for methods and 50 for classes. The goal is to ensure comprehensive test coverage for all code paths, including edge cases and error conditions.
- When working with coverage metrics, use the exact metric identifiers (e.g., `OpenCoverBranchCoverage`, `OpenCoverSequenceCoverage`) — aliases like `Coverage` are not supported.

## Requirements

- Use the `metricsreporter` CLI to update and retrieve metric values for symbols requiring test coverage improvement, one class at a time. Description and usage examples are provided in `docs/3-reference/3.2 - metricsreporter-cli.md`.
- Strictly follow the workflow described below to achieve the goal: increase branch coverage for all methods in all classes within the namespace mentioned above to acceptable levels.
- When writing tests, follow the testing best practices:
	•	Use Arrange-Act-Assert (AAA) pattern for test structure
	•	Follow requirements from `.github/instructions/csharp-nunit.instructions.md`
	•	Use mock objects (NSubstitute) for dependency isolation
	•	Use meaningful test names that reflect the scenario being tested (pattern: `MethodName_Scenario_ExpectedBehavior`)
	•	Add a 1–4 line description before each test explaining what it verifies and why it matters
	•	Test both successful and failure scenarios
	•	Check boundary conditions and edge cases
	•	Do not ignore exceptions, nullable annotations, or complex states
	•	Write 2 to 5 test scenarios per method (1 only if the method is trivial)
- Tests must be of high quality: they should verify not only "happy path" scenarios but also edge cases, exception handling, nullable reference type behavior, and complex state transitions.
- Follow SOLID principles and the rules established in the project.
- Maintain nullable reference type annotations correctly when writing tests.

## Strictly follow this instruction until all classes are covered

### 1. Get problematic class

Using the `metricsreporter read` command, get the first "problematic" class that requires test coverage improvement. A problematic class is one that contains methods with insufficient branch coverage (`metricsreporter` handles this comparison automatically). Use `--group-by type` to get classes grouped by type. Always specify the full metric identifier (e.g., `OpenCoverBranchCoverage`).

Example request to `metricsreporter` with the required options:

```powershell
metricsreporter read --namespace <given_namespace> --metric OpenCoverBranchCoverage --group-by type
```

If you receive a message that no suitable symbols are found (instead of an object with fields `metric`, `namespace`, `symbolKind`, `groupBy`, `violationsGroupsCount`, `violationsGroups`), this means there are no problematic classes: complete the task.

### 2. Analyze class and identify methods requiring tests

Begin working on the class. Study the code of the class, its methods with insufficient coverage, and related classes. Understand the business logic, dependencies, and code paths that need to be tested.

For each method in the class that has insufficient branch coverage, identify:
- All code paths (branches) that need to be covered
- Dependencies that need to be mocked
- Edge cases and boundary conditions
- Exception scenarios
- Nullable reference type scenarios
- Complex state transitions

### 3. Cancel conditions (prefer suppression over exclusion)

Before writing tests, decide whether the metric should be suppressed instead of excluded. See full guidance in `docs/3-reference/3.4 - suppression-guidelines.md`.

- Prefer suppression when at least one of these holds:
  - The method requires complex fixtures or real external objects (e.g., file system, native handles) that cannot be reasonably mocked.
  - Achieving coverage would force testing private methods (breaking encapsulation) rather than public behavior.
  - The class is a thin wrapper/orchestrator with trivial logic where added tests would only assert delegation.
  - Branches are configuration-driven guard rails already validated through higher-level integration paths and adding synthetic inputs would not increase confidence.
  - The code path exists solely for defensive fallbacks (e.g., dummy nodes, excluded assemblies) that cannot occur in valid production inputs.
- Two ways to place the attribute (same effect, differs only by location):
  - **Assembly-level**: `[assembly: SuppressMessage(..., Target = "~T:Namespace.Type", Justification = "...")]` in `GlobalSuppressions.cs` (preferred when the rationale applies to the whole type or a nested type tree).
  - **Symbol-level**: `[SuppressMessage(...)]` directly on the type/member (preferred when only a specific member is noisy).
- Always set `Target` to the exact type/member (`~T:` for type, `~M:/~P:/~E:/~F:` for member kinds). Do not rely on `Scope`; the engine uses `Target`.
- Exclude only if you intentionally want to remove the metric from the report entirely; exclusion hides data and history.

### 4. Write tests

If tests should be written, proceed as follows:

If tests reveal a logic gap, brittleness, or untestable design, fix the production code first (including small refactors) to uphold the intended business behavior and make it testable. Keep changes focused and cover them with tests.

1. Plan the test scenarios for each method:
   - Identify 2 to 5 test scenarios per method (1 only if the method is trivial)
   - Include happy path, edge cases, exception scenarios, and nullable cases
   - Determine which dependencies need to be mocked
2. Write test methods following the AAA pattern:
   - **Arrange**: Set up test data, mocks, and initial state
   - **Act**: Execute the method under test
   - **Assert**: Verify the expected outcome using FluentAssertions
   - Precede the test with a 1–4 line description of the scenario and its purpose
3. Use meaningful test names following the pattern `MethodName_Scenario_ExpectedBehavior`
4. Ensure tests follow all requirements from `.github/instructions/csharp-nunit.instructions.md`
5. Verify that the solution build is successful: `dotnet build --no-incremental`. If the build fails, fix the code until the build is green.
6. Check that there are no compiler warnings or errors in the modified files. If there are, fix them.
7. Run tests to verify they pass. **IMPORTANT: Run tests only for the specific test project that corresponds to the project being worked on, not all tests.**
   Do not run general `dotnet test` command without specifying the test project. If tests fail, fix the tests or the code until all tests pass.

### 5. Verify coverage result

Using the `metricsreporter read` command with `--all` flag, verify that all methods in the class you worked on have sufficient branch coverage.

Example request to `metricsreporter` with the required options:

```powershell
metricsreporter read --namespace <class_full_name> --metric OpenCoverBranchCoverage --all
```

Where `<class_full_name>` is the fully qualified name of the class (type) for which tests were written.

If the command returns any methods with insufficient coverage (non-empty `violationsGroups` array), return to step 2 to write additional tests for the remaining uncovered methods. Coverage is collected automatically during the metrics update process.

If the command returns no violations (empty list or message indicating no problematic symbols), proceed to the next class as described in step 1.
