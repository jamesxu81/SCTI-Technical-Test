# Unit Test Plan: `PostValidator`

Covers README "3. Unit Tests". Written 2026-11-10 against the current code.

## 1. Scope

| In scope | Out of scope |
|---|---|
| `PostValidator.IsValid(Post?)`: every rule on its own, null handling, boundaries, edge cases, combinations of invalid fields | `PostProcessor` filtering (mock and component tests, README §4 and §5) |
| Pure in-memory checks: no network, no files, no mocks | Data-class coverage such as special characters and very long strings (validation tests, README §7) |
| | The API's own rules (API tests, README §2) |

- **System under test:** [PostValidator.cs](../src/Api/Validators/PostValidator.cs).
- **Test file:** [PostValidatorTests.cs](../tests/UnitTests/PostValidatorTests.cs), the file the README names.
- **Category:** `[Category("Unit")]`, run with `dotnet test --filter "Category=Unit"`.

## 2. Rules under test

```csharp
public static bool IsValid(Post? post)
{
    if (post == null) return false;
    if (post.UserId <= 0) return false;
    if (string.IsNullOrWhiteSpace(post.Title)) return false;
    if (string.IsNullOrWhiteSpace(post.Body)) return false;
    return true;
}
```

| Rule | Valid when | Invalid partitions | Boundary |
|---|---|---|---|
| R1 Post | not `null` | `null` | - |
| R2 `UserId` | `>= 1` | `0`, negative | `0` / `1`; extremes `int.MinValue`, `int.MaxValue` |
| R3 `Title` | has at least one non-whitespace character | `null`, `""`, whitespace only | length 0 / 1 |
| R4 `Body` | same as `Title` | same as `Title` | length 0 / 1 |
| - `Id` | not checked | - | - |

Whitespace is whatever `char.IsWhiteSpace` says: spaces, tabs, line breaks and Unicode spaces such as the
no-break space (U+00A0) and the em space (U+2003). Zero-width characters such as U+200B are **not** whitespace.

`Post.Title` and `Post.Body` are non-nullable `string`s, but `null` still reaches the validator in practice:
`System.Text.Json` sets them to `null` for `"title": null`. Tests pass `null!`.

## 3. Test design

- **One rule per test:** start from a valid post and break one field, so a failing test points at one rule. A
  `ValidPost(userId: 1, title: "Valid Title", body: "Valid Body", id: 1)` builder with optional parameters returns a
  valid post, and tests pass only the field under test.
- **Data-driven:** `[TestCase]` rows for each partition and boundary, so each value shows as its own result.
- **Traceability:** `[Description("<ID>")]` with the IDs below, as in the API tests.
- **Assertions:** NUnit `Assert.That`, matching the other non-UI tests.
- **Parallel:** the validator is static and stateless, so all tests run in parallel.

## 4. Test cases

### Null checks

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| NUL-01 | Null post | `IsValid(null)` | `false` | Exists |
| NUL-02 | Null title | `Title = null` | `false` | Exists |
| NUL-03 | Null body | `Body = null` | `false` | Exists |
| NUL-04 | Default post | `new Post()` (`UserId` 0, empty title and body) | `false` | New |

### Field validations (each rule on its own)

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| FLD-01 | Valid post | `ValidPost()` | `true` | Exists |
| FLD-02 | Invalid `UserId`, other fields valid (data-driven) | `0`, `-1` | `false` | Exists as two tests; merge |
| FLD-03 | Empty or whitespace `Title`, other fields valid (data-driven) | `""`, `"   "` | `false` | Exists as two tests; merge |
| FLD-04 | Empty or whitespace `Body`, other fields valid (data-driven) | `""`, `"   "` | `false` | Exists as two tests; merge |
| FLD-05 | `Id` is not validated (data-driven) | `Id` = `0`, `-1`, `int.MinValue` | `true` | New; documents current behaviour (section 6, O2) |

### Boundary values

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| BND-01 | `UserId` around the lower limit and at the extremes (data-driven) | `int.MinValue`, `-1`, `0` | `false` | Partly exists (0, -1) |
| | | `1`, `2`, `int.MaxValue` | `true` | Partly exists (1); the current `WithBoundaryUserId` test repeats FLD-01 |
| BND-02 | `Title` length 0 and 1 | `""` / `"T"` | `false` / `true` | New in unit tests (1 character is only in validation tests) |
| BND-03 | `Body` length 0 and 1 | `""` / `"B"` | `false` / `true` | New in unit tests |

### Edge cases

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| EDG-01 | Whitespace-only `Title` of every kind (data-driven) | `" "`, `"\t"`, `"\n"`, `"\r\n"`, `"\u00A0"` (no-break space), `"\u2003"` (em space), `" \t\n "` | `false` | New (only `"   "` exists) |
| EDG-02 | Whitespace-only `Body` of every kind (data-driven) | same as EDG-01 | `false` | New |
| EDG-03 | Text with surrounding whitespace is kept valid | `Title = "  Title  "`, `Body = "\tBody\n"` | `true` | New |
| EDG-04 | Zero-width characters only (data-driven) | `Title = "\u200B"` (zero-width space), `"\uFEFF"` (zero-width no-break space) | `true` | New; documents a gap (section 6, O1) |
| EDG-05 | The validator doesn't change the post | Call `IsValid` on `ValidPost()` and on an invalid post | Every field is unchanged after the call | New |

### Multiple invalid fields

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| MUL-01 | Every combination of two or three invalid fields (data-driven) | `UserId`+`Title`: `0`, `""`. `UserId`+`Body`: `-1`, `null`. `Title`+`Body`: `"  "`, `"\t"`. All three: `0`, `null`, `""` | `false` | Partly exists (all three: `0`, `""`, `""`) |
| MUL-02 | Every rule is enforced on its own (data-driven: 3 rows) | Start with all three fields invalid and fix them one at a time; each row leaves a different field to fix last | `false` until the last field is fixed, then `true` | New |

**Totals:** 19 test cases (10 new, 9 existing, merged or extended). With data-driven rows they run as about 50 tests.
The current file has 12 tests.

## 5. README traceability

| README requirement | Test cases |
|---|---|
| PostValidator logic testing | All; FLD-01 to FLD-05 cover each rule on its own |
| Null checks | NUL-01 to NUL-04, MUL-01 (null body row) |
| Field validations (UserId, Title, Body) | FLD-02 (UserId), FLD-03 (Title), FLD-04 (Body), FLD-05 (Id not validated) |
| Edge cases | EDG-01 to EDG-05, NUL-04 |
| Boundary value testing | BND-01 to BND-03 |
| Multiple invalid fields handling | MUL-01, MUL-02 |

## 6. Observations and decisions

| ID | Observation | Decision for these tests |
|---|---|---|
| O1 | Zero-width characters (U+200B, U+FEFF) aren't whitespace, so a title or body made only of them is valid even though it looks empty. | EDG-04 asserts the current behaviour (`true`) and names the gap. Raise it; if the rule changes, flip the expectation. |
| O2 | `Id` isn't checked. A new post has `Id` 0 until the API assigns one, so this looks intentional. The API schema (plan 2) needs `id` >= 1 only in responses. | FLD-05 documents it. |
| O3 | No upper length limit for `Title` or `Body`. | Not a unit-test concern; very long strings are covered by the validation tests (README §7). |
| O4 | `IsValid` returns only `bool`, so a test can't tell which rule failed. With several invalid fields, the tests can only assert `false`. | MUL-02 proves each rule is enforced on its own by fixing fields one at a time. Returning the failed rules would be a code change, not proposed here. |
| O5 | `IsValid_WithBoundaryUserId_ReturnsTrue` uses exactly the same data as `IsValid_WithValidPost_ReturnsTrue`. | Replace it with BND-01. |

## 7. Overlap with the validation tests (README §7)

[PostValidationTests.cs](../tests/ValidationTests/PostValidationTests.cs) tests the same validator by data class. To
avoid duplication:

| Topic | Unit tests (this plan) | Validation tests (README §7) |
|---|---|---|
| Each rule on its own, combinations, null post | Yes | Only as data rows |
| `UserId` boundaries | Yes (BND-01) | Ranges of valid and invalid values |
| Whitespace kinds, zero-width characters | Yes (EDG-01, EDG-02, EDG-04) | Basic spaces, tab, newline |
| Special characters, very long strings | No | Yes |

## 8. Implementation outline

- Rewrite [PostValidatorTests.cs](../tests/UnitTests/PostValidatorTests.cs): add the `ValidPost()` builder, merge
  the existing single-value tests into `[TestCase]` tests (FLD-02 to FLD-04), replace the duplicate boundary test
  with BND-01, and add the new cases above.
- Keep `[Category("Unit")]`, NUnit `Assert.That`, and add `[Description("<ID>")]`.
- No production code changes.
- Run `dotnet test --filter "Category=Unit"`; all tests should pass, including EDG-04 and FLD-05, which document
  current behaviour.

**Status (2026-11-10):** implemented. 19 test cases run as 52 tests; all pass.
