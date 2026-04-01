# Test Coverage Review

After completing tests according to `@refactor-coverage.md`, review each new or updated test class or method created in this session.

Summary of my test work:
## --- Summary Start ---
(Briefly list test classes/methods that were added or changed)
## --- Summary End ---

## Review Criteria

### 1. Coverage and Completeness
**Question:** Do the tests deliver the required `OpenCoverBranchCoverage` (and, when applicable, `OpenCoverSequenceCoverage`) and verify all scenarios from `@refactor-coverage.md`?
**Answer format (6 lines):**
- Happy-path branches covered (yes/no + brief)
- Error/exception branches covered (yes/no + brief)
- Boundary and nullable scenarios covered (yes/no + brief)
- Mocks/stubs isolate dependencies correctly (yes/no + brief)
- AAA structure and meaningful names followed (yes/no + brief)
- Coverage completeness verdict 1–5 (1=poor, 5=excellent)

### 2. Rules and Best Practices Compliance
**Question:** Do the tests comply with `.github/instructions/csharp-nunit.instructions.md`, `refactor-coverage.md` (branch coverage targets, 2–5 scenarios, meaningful names, edge/exception cases, a 1–4 line pre-test description of intent), and MS/NUnit recommendations?
**Answer format:**
- If professional: one line with advantages (e.g., "Clear AAA, focused asserts, boundary mocks").
- If there are issues: one line per problem or `.mdc` non-compliance (AAA broken, missing edge/exception cases, weak isolation, duplicate Arrange/Act, missing FluentAssertions, nullable contract ignored, vague names, missing 1–4 line intent description before the test, shallow asserts, etc.).

### 3. Improvement Recommendations
**Question:** How can the test be improved or removed if needed?
**Answer format:**
- How to strengthen branch coverage: add scenarios (errors, null/empty, boundary), assert exception messages, negative paths.
- How to improve structure: move shared Arrange to helper/factory, tighten mocks/verification, replace duplication with `[SetUp]`/`[TestCase]`, use more precise asserts.
- If the test is redundant or a thin duplicate, state whether it can be removed; if removal would push more dependency into the parent, mention the need for a justified `SuppressMessage`.

