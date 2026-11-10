# Mock Test Plan: `PostProcessor` with a mocked `IPublicApiClient`

Covers README "4. Mock Tests (Moq)". Written 2026-11-10 against the current code. Behaviour marked "confirmed" was
checked by running it.

## 1. Scope

| In scope | Out of scope |
|---|---|
| How `PostProcessor.GetValidPostsAsync()` reacts to every kind of client response: success, HTTP failure, empty, null, thrown exceptions, invalid posts | Filtering business rules in depth and partial or mixed data sets (component tests, README §5) |
| How `PostProcessor` uses the client: which methods, how often, in which situations | `PostValidator` rule by rule (unit tests, README §3) |
| In-memory only: Moq stands in for the client; no network | The real `PublicApiClient` and API (API and integration tests, README §2 and §6) |

- **System under test:** [PostProcessor.cs](../src/Api/Processors/PostProcessor.cs), which calls
  `IPublicApiClient.GetPostsAsync()` and keeps the posts that pass `PostValidator.IsValid`.
- **Mocked dependency:** [IPublicApiClient.cs](../src/Api/Clients/IPublicApiClient.cs) (`GetPostsAsync`,
  `GetPostAsync`, `CreatePostAsync`).
- **Test file:** [PostProcessorMockTests.cs](../tests/MockTests/PostProcessorMockTests.cs), the file the README names.
- **Category:** `[Category("Mock")]`, run with `dotnet test --filter "Category=Mock"`.

## 2. Behaviour under test

```csharp
public async Task<List<Post>> GetValidPostsAsync()
{
    var posts = await _client.GetPostsAsync();
    return posts.Where(PostValidator.IsValid).ToList();
}
```

| Client behaviour (simulated with Moq) | Processor result | Source |
|---|---|---|
| Returns posts | A new list with only the valid posts, same instances, same order | Code |
| Returns an empty list | An empty list | Code |
| Returns `null` (also what an unconfigured loose mock returns for `Task<List<Post>>`) | Throws `ArgumentNullException` (parameter `source`) | Confirmed |
| Returns a list containing a `null` item | The `null` item is dropped; no exception | Confirmed |
| `ThrowsAsync(...)` | The same exception propagates, unwrapped | Code |
| `Throws(...)` (synchronous throw) | Surfaces through the returned task (the task is faulted), not when the method is called | Confirmed |

The real client throws `HttpRequestException` for any non-success status (`EnsureSuccessStatusCode`), so that is how
an "API failure" is simulated. There is no retry, caching or error wrapping in `PostProcessor`.

## 3. Test design

- **Fresh mock per test:** `Mock<IPublicApiClient>` and `PostProcessor` are created in `[SetUp]`. With
  `InstancePerTestCase` each test has its own, so tests run in parallel safely.
- **Moq features used:** `Setup(...).ReturnsAsync`, `ThrowsAsync`, `Throws`, `SetupSequence` (fail then succeed),
  `Verify(..., Times.*)`, `VerifyNoOtherCalls()`, `MockBehavior.Strict` (one test).
- **Test data:** a `ValidPost(id)` helper; invalid posts start from a valid one with one field broken, so each row
  shows which rule removed it.
- **Data-driven:** `[TestCase]` / `[TestCaseSource]` for status codes, exception types and invalid post kinds.
- **Traceability:** `[Description("<ID>")]` with the IDs below, as in the API and unit tests.
- **Assertions:** NUnit `Assert.That`, `Assert.ThrowsAsync`, matching the existing tests.

## 4. Test cases

### Simulating API success

| ID | Test | Mock setup | Expected | Status |
|---|---|---|---|---|
| SUC-01 | Valid posts are returned | `ReturnsAsync` 2 valid posts | Both returned, in the same order | Exists (order not yet checked) |
| SUC-02 | Posts are passed through unchanged | `ReturnsAsync` 2 valid posts | Result items are the same instances (`Is.SameAs`) with unchanged fields | New |
| SUC-03 | The source list isn't changed | `ReturnsAsync` a mixed list | Result is a new list; the client's list still has all its items | New |
| SUC-04 | A full page of posts | `ReturnsAsync` 100 valid posts, like the real API | 100 returned | New |

### Simulating API failures

| ID | Test | Mock setup | Expected | Status |
|---|---|---|---|---|
| FAIL-01 | HTTP failure propagates | `ThrowsAsync(new HttpRequestException("Network error"))` | `HttpRequestException`, the same instance and message | Exists (instance and message not checked) |
| FAIL-02 | Status code is preserved (data-driven: 404, 429, 500, 503) | `ThrowsAsync(new HttpRequestException(msg, null, status))` | Caught exception has the same `StatusCode` | New |
| FAIL-03 | A failure isn't remembered | `SetupSequence`: first call throws `HttpRequestException`, second returns valid posts | First call throws; second call returns the posts | New |

### Simulating empty lists

| ID | Test | Mock setup | Expected | Status |
|---|---|---|---|---|
| EMP-01 | Empty list | `ReturnsAsync(new List<Post>())` | Empty list, not `null` | Exists (not-null not checked) |
| EMP-02 | Every post is invalid | `ReturnsAsync` 3 invalid posts | Empty list | New |
| EMP-03 | `null` list (data-driven: explicit `ReturnsAsync(null)`, unconfigured loose mock) | as named | `ArgumentNullException` today | New; documents a gap (section 6, M1) |

### Simulating thrown exceptions

| ID | Test | Mock setup | Expected | Status |
|---|---|---|---|---|
| EXC-01 | Non-HTTP exceptions propagate unwrapped (data-driven) | `ThrowsAsync` `TaskCanceledException` (timeout), `OperationCanceledException`, `JsonException` (bad response body), `InvalidOperationException` | Same type and same instance; not wrapped in `AggregateException` | New |
| EXC-02 | Synchronous throw from the client | `Throws(new InvalidOperationException())` | The call returns a faulted task; awaiting it throws the same exception | New |

### Simulating invalid posts

| ID | Test | Mock setup | Expected | Status |
|---|---|---|---|---|
| INV-01 | Mixed valid and invalid posts | 1 valid, invalid `UserId`, empty `Title`, empty `Body` | Only the valid post | Exists |
| INV-02 | Each kind of invalid post is dropped (data-driven: `UserId` 0, -1; `Title` `null`, `""`, `"  "`; `Body` `null`, `""`, `"  "`) | 1 valid post plus the invalid one | Only the valid post | Partly exists (null title, whitespace title and body as two tests); merge |
| INV-03 | `null` item in the list | `[null, valid]` | Only the valid post; no exception | New |
| INV-04 | Valid posts keep their order around invalid ones | `[valid 1, invalid, valid 3, invalid, valid 5]` | `[1, 3, 5]` in that order | New |

### Verifying mock interactions

| ID | Test | Action | Verification | Status |
|---|---|---|---|---|
| VER-01 | The client is called once | `GetValidPostsAsync()` once | `Verify(c => c.GetPostsAsync(), Times.Once)` | Exists |
| VER-02 | No other client method is used | `GetValidPostsAsync()` once | VER-01 check, then `VerifyNoOtherCalls()`; `GetPostAsync` and `CreatePostAsync` never called | New |
| VER-03 | Strict mock | `MockBehavior.Strict` with only `GetPostsAsync` set up | Succeeds; any unplanned call would throw | New |
| VER-04 | No caching | `GetValidPostsAsync()` twice | `Times.Exactly(2)` | New |
| VER-05 | No retry on failure | `ThrowsAsync(HttpRequestException)`, call once | Exception, and `Times.Once` | New |
| VER-06 | The constructor doesn't call the client | Create `PostProcessor` only | `VerifyNoOtherCalls()` | New |

**Totals:** 22 test cases (16 new, 6 existing or extended). With data-driven rows they run as about 36 tests. The
current file has 7 tests.

## 5. README traceability

| README requirement | Test cases |
|---|---|
| Simulating API success | SUC-01 to SUC-04 |
| Simulating API failures | FAIL-01 to FAIL-03, VER-05 |
| Simulating empty lists | EMP-01 to EMP-03 |
| Simulating thrown exceptions | EXC-01, EXC-02, FAIL-01 |
| Simulating invalid posts | INV-01 to INV-04, EMP-02 |
| Verifying mock interactions | VER-01 to VER-06, FAIL-03 (sequence) |

## 6. Observations and decisions

| ID | Observation | Decision for these tests |
|---|---|---|
| M1 | A `null` list makes `GetValidPostsAsync()` throw `ArgumentNullException`. The real client can return `null`: for a response body of `null`, `JsonSerializer.Deserialize<List<Post>>` returns `null` and the `!` hides it. | **Decided (2026-11-10): keep the current behaviour.** EMP-03 asserts the `ArgumentNullException` and this is a known gap. If `PostProcessor` is later changed to treat `null` as an empty list, flip EMP-03 to expect an empty list. |
| M2 | No retry, caching or error wrapping: every failure reaches the caller as the original exception. | FAIL-01 to FAIL-03, EXC-01, VER-04 and VER-05 document it. |
| M3 | `PostProcessor` only uses `GetPostsAsync`. `GetPostAsync` and `CreatePostAsync` exist on the interface but aren't used by it. | VER-02 and VER-03 lock this in; a new call would make them fail on purpose. |
| M4 | A synchronous throw from the client still surfaces through the task, because `GetValidPostsAsync` is `async`. | EXC-02 documents it. |

## 7. Overlap with the component tests (README §5)

[PostProcessorComponentTests.cs](../tests/ComponentTests/PostProcessorComponentTests.cs) also mocks the client but
has no tests yet. To avoid duplication:

| Topic | Mock tests (this plan) | Component tests (README §5) |
|---|---|---|
| Client success, failure, empty, null, exceptions | Yes | No |
| How the client is called | Yes | No |
| Invalid posts | One of each kind is dropped (INV-01 to INV-04) | Business rules in depth, partial data, larger mixed data sets |

## 8. Implementation outline

- Rewrite [PostProcessorMockTests.cs](../tests/MockTests/PostProcessorMockTests.cs): add the `ValidPost(id)` helper,
  merge the three single-kind invalid tests into INV-02, extend the existing tests as noted, and add the new cases.
- Keep `[Category("Mock")]`, Moq 4.20.70 and NUnit `Assert.That`; add `[Description("<ID>")]`.
- No production code changes (M1 kept as a known gap).
- Run `dotnet test --filter "Category=Mock"`; all tests should pass.

**Status (2026-11-10):** implemented. 22 test cases run as 36 tests; all pass.
