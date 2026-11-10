# Component Test Plan: `PostProcessor` with the real `PostValidator`

Covers README "5. Component Tests". Written 2026-11-10 against the current code. Behaviour marked "confirmed" was
checked by running it.

## 1. Scope

The component is everything in-process that turns the client's data into the list of valid posts: `PostProcessor`
plus the real `PostValidator`. For partial data, the real `PublicApiClient` is included too, fed JSON by a stub HTTP
handler, because that is where partial data is turned into `Post` objects. Nothing touches the network.

| In scope | Out of scope |
|---|---|
| Filtering logic over whole data sets: what is kept, dropped, ordered, duplicated | How the processor reacts to client errors, `null`, empty lists, and how it calls the client (mock tests, README §4) |
| The business rules as applied by the processor (every combination of field states) | `PostValidator` rule by rule (unit tests, README §3) |
| Partial and malformed records, as JSON through the real client | The live API (API and integration tests, README §2 and §6) |
| Mixed valid and invalid data sets of different sizes and ratios | |
| The validator and processor agreeing on every post | |

- **System under test:** [PostProcessor.cs](../src/Api/Processors/PostProcessor.cs) with
  [PostValidator.cs](../src/Api/Validators/PostValidator.cs); for PD cases also
  [PublicApiClient.cs](../src/Api/Clients/PublicApiClient.cs).
- **Test file:** [PostProcessorComponentTests.cs](../tests/ComponentTests/PostProcessorComponentTests.cs), the file the
  README names. It has the `Mock<IPublicApiClient>` setup and no tests yet.
- **Category:** `[Category("Component")]`, run with `dotnet test --filter "Category=Component"`.

## 2. Business rules

A post is kept only when all of these hold (from `PostValidator`):

| Rule | Kept when | Dropped when |
|---|---|---|
| BR-UserId | `UserId >= 1` | `0` or negative |
| BR-Title | `Title` has a non-whitespace character | `null`, `""`, whitespace only |
| BR-Body | `Body` has a non-whitespace character | `null`, `""`, whitespace only |
| (none) | `Id` is never checked | - |

`PostProcessor` keeps the posts that pass, in their original order, as a new list. It doesn't remove duplicates.

**How JSON becomes a `Post`** (confirmed through `PublicApiClient`, which uses `JsonSerializerDefaults.Web`):

| Record in the JSON array | Becomes | Processor result |
|---|---|---|
| Missing `title` or `body` | `""` (the property default) | Dropped |
| Missing `userId` | `0` | Dropped |
| `"title": null` | `null` | Dropped |
| `{}` | `UserId` 0, empty strings | Dropped |
| `null` item | `null` | Dropped |
| Missing `id` | `Id` 0 | **Kept** (no rule on `Id`) |
| Extra unknown property, e.g. `"tags"` | Ignored | Kept |
| Property names in other casing, e.g. `"UserId"`, `"TITLE"` | Matched case-insensitively | Kept |
| `"userId": "1"` (number as a string) | `1` (Web defaults read numbers from strings) | Kept |
| `"userId": null`, `"userId": "abc"`, `"userId": 1.5`, `"title": 123` | `JsonException` while reading the list | **The whole call fails; no posts at all** |

## 3. Test design

- **Two ways to feed data:**
  - *Object-fed* (FLT, BR, MIX, INT-01, INT-02): `Mock<IPublicApiClient>` returns `List<Post>` built in the test. The
    mock is only a data source here; interactions are the mock tests' job.
  - *JSON-fed* (PD, INT-03): a stub `HttpMessageHandler` returns a JSON body to a real `PublicApiClient`, which feeds a
    real `PostProcessor`. This is the path real data takes, minus the network.
- **Test data builders:** `ValidPost(id)`, `Broken(id, change)`, a generated decision table (every combination of
  field states), and `MixedSet(total, invalidEvery)` for larger sets. JSON fixtures are inline strings.
- **Oracle:** for mixed data, the expected result is `input.Where(PostValidator.IsValid)`, so the tests prove the
  processor applies exactly the validator's rules, plus explicit expected ids so the test doesn't only mirror the code.
- **Data-driven:** `[TestCase]` / `[TestCaseSource]`, readable names via `SetArgDisplayNames`.
- **Traceability:** `[Description("<ID>")]` with the IDs below.
- **Assertions:** NUnit `Assert.That`, matching the other non-UI tests.

## 4. Test cases

### PostProcessor filtering logic

| ID | Test | Input | Expected |
|---|---|---|---|
| FLT-01 | Nothing is added or changed | 6 posts, 3 invalid | Kept + dropped = 6; every kept item is the same instance as an input item, fields unchanged |
| FLT-02 | Order is kept around invalid posts | Invalid at the start, at the end, and in a run of 3 in the middle | Valid ids in input order |
| FLT-03 | Duplicates are kept (data-driven) | The same post instance twice; two different posts with the same `Id` | Both kept (section 6, C3) |
| FLT-04 | Large data set | 10,000 posts, every 3rd invalid | 6,667 kept, with the expected ids |

### Business rules validation

| ID | Test | Input | Expected |
|---|---|---|---|
| BR-01 | Decision table (data-driven: 48 rows) | One post per combination of `UserId` {1, 0, -1} × `Title` {valid, `null`, `""`, `"  "`} × `Body` {same 4} | Kept only for the one row where all three are valid |
| BR-02 | Decision table in one list | All 48 combinations together | Exactly 1 post kept, the all-valid one |
| BR-03 | `Id` is not a rule (data-driven) | Valid posts with `Id` 0, -1, `int.MaxValue` | Kept |
| BR-04 | Values at the edges of the rules | `UserId` `int.MaxValue`; title `"  Title  "`; body of 10,000 characters | Kept |

### Partial data handling (JSON-fed)

| ID | Test | JSON record (among 1 full valid record) | Expected |
|---|---|---|---|
| PD-01 | Missing required fields are dropped (data-driven) | No `title`; no `body`; no `userId`; `{}`; `"title": null`; `null` item | Only the full record kept |
| PD-02 | Missing `id` is kept | `{ "userId": 1, "title": "t", "body": "b" }` | Kept with `Id` 0 (section 6, C2) |
| PD-03 | Unknown and differently-cased properties | Extra `"tags"`; names `"UserId"`, `"TITLE"`, `"Body"` | Kept |
| PD-04 | Number sent as a string | `"userId": "1"` | Kept with `UserId` 1 |
| PD-05 | One malformed record fails the whole list (data-driven) | `"userId": null`; `"userId": "abc"`; `"userId": 1.5`; `"title": 123` | `JsonException`; no posts returned (section 6, C1) |
| PD-06 | Mostly complete data | 100 records (API shape), 5 with a missing field | 95 kept |

### Mixed valid/invalid data scenarios

| ID | Test | Input | Expected |
|---|---|---|---|
| MIX-01 | API-like data set | 100 posts (ids 1–100, `userId` 1–10), every 10th broken in a different way | 90 kept; ids 10, 20, ... 100 missing |
| MIX-02 | Different ratios (data-driven) | 10 posts with 0, 1, 5, 9, 10 invalid | Kept count = 10 − invalid |
| MIX-03 | Every invalid kind mixed together | One post per invalid kind (8 kinds) shuffled among 8 valid posts | The 8 valid posts, in order |

### Integration of validator with processor

| ID | Test | Input | Expected |
|---|---|---|---|
| INT-01 | Processor result equals the validator's verdict | The BR-02 and MIX-01 data sets | Result equals `input.Where(PostValidator.IsValid)` (same items, same order) |
| INT-02 | Every kept post passes and every dropped post fails | The MIX-03 data set | `IsValid` is true for all kept, false for all dropped |
| INT-03 | Client, processor and validator together without the network | A JSON array in the API's shape with full, partial and invalid records | The expected ids, as typed `Post` objects with all fields filled in |

**Totals:** 18 test cases. With data-driven rows they run as 84 tests (BR-01 alone has 48 rows).

## 5. README traceability

| README requirement | Test cases |
|---|---|
| PostProcessor filtering logic | FLT-01 to FLT-04 |
| Business rules validation | BR-01 to BR-04 |
| Partial data handling | PD-01 to PD-06 |
| Mixed valid/invalid data scenarios | MIX-01 to MIX-03, FLT-02, FLT-04 |
| Integration of validator with processor | INT-01 to INT-03, BR-01, BR-02 |

## 6. Observations and decisions

| ID | Observation | Decision for these tests |
|---|---|---|
| C1 | **One malformed record loses the whole list.** A single wrong type (`"userId": null`, `"abc"`, `1.5`, or `"title": 123`) makes `PublicApiClient` throw `JsonException`, so `GetValidPostsAsync()` returns nothing, not the 99 good posts. Confirmed. | **Decided (2026-11-10): keep the current behaviour.** PD-05 asserts the `JsonException` and this is a known gap. If the client is later changed to skip unreadable records, flip PD-05 to expect the valid records. |
| C2 | A record without `id` is kept with `Id` 0, because there is no rule on `Id`. | PD-02 and BR-03 document it. Matches the unit plan's O2. |
| C3 | Duplicates (same instance or same `Id`) are kept. | FLT-03 documents it. If de-duplication becomes a rule, flip the expectation. |
| C4 | Web JSON defaults accept numbers sent as strings (`"1"`), but not `null`, decimals or text for an `int`. | PD-04 and PD-05 document both sides. |
| C5 | The processor applies exactly the validator's rules; it adds none of its own. | INT-01 to INT-03 lock this in. |

## 7. How this differs from the other test layers

| Topic | Unit (§3) | Mock (§4) | Component (this plan) |
|---|---|---|---|
| One rule, one value | Yes | - | - |
| Client errors, `null`, empty, call counts | - | Yes | - |
| One invalid post of each kind | - | Yes (INV-02) | Only mixed together (MIX-03) |
| Whole data sets, ratios, order, duplicates, size | - | - | Yes |
| Every combination of field states | - | - | Yes (BR-01, BR-02) |
| Partial and malformed JSON through the real client | - | - | Yes (PD) |

## 8. Implementation outline

- Fill in [PostProcessorComponentTests.cs](../tests/ComponentTests/PostProcessorComponentTests.cs): keep the existing
  `Mock<IPublicApiClient>` setup for object-fed tests, and add a small stub `HttpMessageHandler` for JSON-fed tests
  (like CLI-05 in the API tests).
- Keep `[Category("Component")]` and NUnit `Assert.That`; add `[Description("<ID>")]`.
- No production code changes (C1 kept as a known gap).
- Run `dotnet test --filter "Category=Component"`; all tests should pass.

**Status (2026-11-10):** implemented. 18 test cases run as 84 tests; all pass.
