# Validation Test Plan: `PostValidator` input domains

Covers README "7. Validation Testing". Written 2026-11-10 against the current code. Behaviour marked "confirmed" was
checked by running it.

## 1. Scope

Validation tests drive `PostValidator.IsValid` with **classes of real-world input data**: required fields, ranges,
lengths, character sets and every kind of whitespace. The unit tests (README §3) prove each rule's logic; these tests
prove the rules give the right verdict across the whole input domain, and record what kinds of data the validator
lets through.

| In scope | Out of scope |
|---|---|
| Required fields, `UserId` ranges, `Title`/`Body` content, lengths, special characters, Unicode, whitespace | The validator's logic structure, null post, combinations of invalid fields (unit tests, README §3) |
| Pure in-memory checks of `PostValidator.IsValid` | Filtering lists (mock and component tests, README §4 and §5); the API (README §2 and §6) |

- **System under test:** [PostValidator.cs](../src/Api/Validators/PostValidator.cs).
- **Test file:** [PostValidationTests.cs](../tests/ValidationTests/PostValidationTests.cs), the file the README names
  (25 tests today).
- **Category:** `[Category("Validation")]`, run with `dotnet test --filter "Category=Validation"`.

## 2. Rules and input domains

| Field | Rule | Valid domain | Invalid domain |
|---|---|---|---|
| `UserId` | `>= 1` | 1 to `int.MaxValue` | `int.MinValue` to 0 |
| `Title`, `Body` | not `null`, and at least one character that isn't whitespace | Any string with one non-whitespace character, any length | `null`, `""`, strings made only of whitespace |
| `Id` | not checked | any | none |

**Whitespace** is exactly what `char.IsWhiteSpace` accepts. Confirmed, these 25 characters:

| Group | Characters |
|---|---|
| ASCII control whitespace | U+0009 tab, U+000A line feed, U+000B vertical tab, U+000C form feed, U+000D carriage return |
| Spaces | U+0020 space, U+00A0 no-break space, U+1680 Ogham space, U+2000 to U+200A (11 typographic spaces), U+202F narrow no-break space, U+205F medium mathematical space, U+3000 ideographic space |
| Line breaks | U+0085 next line, U+2028 line separator, U+2029 paragraph separator |

**Looks empty but is valid** (confirmed): control characters (U+0000 NUL, U+0007 BEL, U+001F), zero-width and
direction marks (U+200B, U+200C, U+200D, U+2060, U+FEFF, U+200F), a lone combining accent (U+0301), the Hangul filler
(U+3164), the braille blank (U+2800), and an unpaired surrogate (half of an emoji).

**No limits:** there is no maximum length and no character restriction. A string of 1,000,000 spaces is invalid
and a 1,000,000-character text is valid.

## 3. Test design

- **One field varies, the others are valid:** a `ValidPost(userId, title, body)` builder starts from
  `{ UserId = 1, Title = "Valid Title", Body = "Valid Body" }`.
- **Title and Body share one data set:** the same `TestCaseSource` rows are run against both fields, so both get the
  same verdict for every input (README "Title/Body constraints").
- **Readable names:** invisible inputs get display names such as `"no-break space U+00A0"` via
  `SetArgDisplayNames`.
- **Expected verdict in the data:** rows carry `ExpectedResult = true/false` so valid and invalid classes sit side by
  side.
- **Traceability:** `[Description("<ID>")]` with the IDs below, as in the other suites.
- **Assertions:** NUnit `Assert.That` and `ExpectedResult`, matching the existing tests.

## 4. Test cases

### Required fields

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| REQ-01 | All required fields present | `UserId` 1, title `"T"`, body `"B"` | `true` | Exists (part of `IsValid_RequiredFieldsValidation`) |
| REQ-02 | Each required field missing on its own (data-driven) | `UserId` 0; `Title` `null`; `Title` `""`; `Body` `null`; `Body` `""` | `false` | Exists as one sequential test; split into rows |
| REQ-03 | All required fields missing | `new Post()` | `false` | New |

### Title/Body constraints

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| TB-01 | Same verdict for Title and Body (data-driven: every row of TB-02, WS-01 to WS-04, SPC-01, SPC-02, LEN-01 run on each field) | as listed | Title verdict = Body verdict | New |
| TB-02 | Content classes (data-driven) | Letters, digits only (`"123"`), punctuation only (`"..."`), a single emoji, mixed text | `true` | Partly exists |

### UserId constraints

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| UID-01 | Valid range (data-driven) | 1, 2, 10, 100, 1,000,000, `int.MaxValue` | `true` | Partly exists (1, 100, `int.MaxValue`) |
| UID-02 | Invalid range (data-driven) | 0, -1, -100, `int.MinValue` | `false` | Exists |

### Boundary values

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| BND-01 | `UserId` either side of the limit | 0 / 1 | `false` / `true` | Exists (1); 0 in UID-02 |
| BND-02 | Text length either side of the limit (Title and Body) | 0 characters / 1 character | `false` / `true` | Exists for both fields together; split per field |
| BND-03 | One real character is enough (Title and Body) | 1 space + `"x"`; 1,000 spaces + `"x"`; `"x"` + 1,000 spaces | `true` | New |
| BND-04 | Whitespace-only at different lengths (Title and Body) | 1, 2, 100, 10,000 spaces | `false` | New |

### Special characters handling

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| SPC-01 | Symbols and markup are accepted as text (data-driven) | ASCII punctuation `!@#$%^&*()_+-={}[]\|:;<>?,./`; quotes `' " \``; backslash and path `C:\temp\x`; HTML `<script>alert(1)</script>`; SQL `'; DROP TABLE posts; --`; JSON `{"a":1}`; URL with query string; email address | `true` | Partly exists (one punctuation string) |
| SPC-02 | Unicode text is accepted (data-driven) | Chinese `你好`, Greek `αβγ`, Māori macrons `ā ē ī ō ū`, Arabic (right-to-left) `مرحبا`, Hindi `नमस्ते`, emoji `🎉`, emoji with modifiers `👍🏽`, family emoji with joiners `👨‍👩‍👧`, accented letters written as letter + combining mark | `true` | Partly exists (one mixed string) |
| SPC-03 | Invisible or control characters only (data-driven) | U+0000, U+0007, U+001F, U+200B, U+200C, U+200D, U+2060, U+FEFF, U+200F, U+0301, U+3164, U+2800, unpaired surrogate U+D83D | `true` today; known gap (section 6, V1) | New |

### Very long strings handling

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| LEN-01 | Long text is valid (data-driven, Title and Body) | 255, 1,000, 10,000, 100,000, 1,000,000 characters | `true` | Partly exists (10,000 on both fields) |
| LEN-02 | Long multi-line text | 1,000 lines separated by `\n` | `true` | New |
| LEN-03 | Long whitespace-only text | 1,000,000 spaces; 10,000 mixed whitespace characters | `false` | New |

### Whitespace validation

| ID | Test | Input | Expected | Status |
|---|---|---|---|---|
| WS-01 | Every whitespace character alone (data-driven: the 25 in section 2, Title and Body) | one character | `false` | Partly exists (space, tab, line feed) |
| WS-02 | Mixed whitespace only | `" \t\r\n\u00A0\u3000"` | `false` | New |
| WS-03 | Text with surrounding or inner whitespace | `"  Title  "`, `"\tTitle\n"`, `"Two  words"`, `"\u00A0Title\u00A0"` | `true` | New |
| WS-04 | Repeated single whitespace | `"   "` (3 spaces), `"\n\n\n"`, `"\t\t"` | `false` | Exists (`"   "`) |

**Totals:** 21 test cases (9 new, 12 existing, split or extended). With data-driven rows and both text fields they
run as about 190 tests; most come from WS-01 (25 characters × 2 fields), SPC-03 and the special-character sets.

## 5. README traceability

| README requirement | Test cases |
|---|---|
| Required fields validation | REQ-01 to REQ-03 |
| Title/Body constraints | TB-01, TB-02, BND-02 to BND-04 |
| UserId constraints | UID-01, UID-02 |
| Boundary values | BND-01 to BND-04 |
| Special characters handling | SPC-01 to SPC-03 |
| Very long strings handling | LEN-01 to LEN-03 |
| Whitespace validation | WS-01 to WS-04, LEN-03 |

## 6. Observations and decisions

| ID | Observation | Decision for these tests |
|---|---|---|
| V1 | **Invisible text passes.** A title or body made only of control characters, zero-width or direction marks, a lone combining accent, the Hangul filler or the braille blank is valid, because none of these are whitespace. Confirmed. | SPC-03 asserts the current behaviour (`true`) and names the gap, extending the unit plan's O1 to every such character. Raise it; if the rule changes, flip SPC-03. |
| V2 | No maximum length: 1,000,000 characters is valid. | LEN-01 documents it. Whether the API or a database would accept it is outside the validator. |
| V3 | Markup and SQL-like text are valid. The validator checks presence, not safety; encoding is the job of whatever displays or stores the text. | SPC-01 documents it. |
| V4 | An unpaired surrogate (broken emoji) is valid. | Part of SPC-03. |
| V5 | The existing `IsValid_WithMultipleInvalidFields_ReturnsFalse` loops over 4 posts in one test, so only the first failure is reported. | Combinations of invalid fields belong to the unit tests (MUL-01, MUL-02); remove it here. |

## 7. How this differs from the unit tests (README §3)

| Topic | Unit tests | Validation tests (this plan) |
|---|---|---|
| Rule logic, null post, invalid-field combinations | Yes | No |
| `UserId` range | Boundaries 0/1 and extremes | Wider valid and invalid ranges |
| Whitespace | 8 common kinds | All 25 whitespace characters, mixed, at length |
| Invisible characters | U+200B, U+FEFF | Every invisible or control class found |
| Special characters, Unicode scripts, markup | No | Yes |
| Long strings | No | Up to 1,000,000 characters |

## 8. Implementation outline

- Rewrite [PostValidationTests.cs](../tests/ValidationTests/PostValidationTests.cs): add the `ValidPost` builder and
  shared text data sets run against both `Title` and `Body`; split the sequential required-fields test into rows;
  remove the multiple-invalid-fields loop (V5); add the new cases above.
- Keep `[Category("Validation")]` and NUnit; add `[Description("<ID>")]`.
- No production code changes.
- Run `dotnet test --filter "Category=Validation"`; all tests should pass, including SPC-03, which documents current
  behaviour.

**Status (2026-11-10):** implemented. 21 test cases run as 191 tests; all pass. WS-01 also checks that the list of
25 whitespace characters still matches the .NET runtime. Invisible and whitespace inputs are passed as code points,
so control characters never appear in test names or reports.
