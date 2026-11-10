# Integration Test Plan: client, processor and validator against the live API

Covers README "6. Integration Tests". Written 2026-11-10. Facts in section 2 were checked against the live API on
that date.

## 1. Scope

Integration tests run our own classes together, wired the way the application wires them, against the real
JSONPlaceholder API: `HttpClient` → `PublicApiClient` → `PostProcessor` → `PostValidator`. They answer: with real
data over a real connection, do the parts give the right end result, and does the data agree across calls?

| In scope | Out of scope |
|---|---|
| The whole chain with a real `HttpClient` and the configured base URL | Status codes, headers, raw JSON and schema (API tests, README §2) |
| `PublicApiClient` and `PostProcessor` results with live data | Error handling with simulated failures (mock tests, README §4) |
| Consistency between the list, single-post and create calls | Filtering rules and partial data (component tests, README §5) |
| Many valid ids, including user and range boundaries | `PUT`, `PATCH`, `DELETE`, other resources; load (separate WLM work) |
| A real network failure (unresolvable host) through the chain | |

- **System under test:** [PublicApiClient.cs](../src/Api/Clients/PublicApiClient.cs),
  [PostProcessor.cs](../src/Api/Processors/PostProcessor.cs),
  [PostValidator.cs](../src/Api/Validators/PostValidator.cs), and the live API.
- **Test file:** [PublicApiIntegrationTests.cs](../tests/IntegrationTests/PublicApiIntegrationTests.cs), the file the
  README names. It has the setup (`ApiSettings.CreateHttpClient()`, `PublicApiClient`) and no tests yet.
- **Category:** `[Category("Integration")]`, run with `dotnet test --filter "Category=Integration"`.
- **Environment:** `ApiBaseUrl` from `tests/appsettings*.json` (`TEST_ENV`, overridable with `TEST_ApiBaseUrl`).

## 2. Live data facts (2026-11-10)

| Fact | Value |
|---|---|
| Posts from `GET /posts` | 100, ids 1–100 in order, `userId` 1–10 with 10 posts each |
| Valid under `PostValidator` | All 100 |
| Fields per post | Exactly `userId`, `id`, `title`, `body` |
| Title length | 12 to 79 characters, all titles unique |
| Body length | 102 to 225 characters; every body contains a line break (`\n`) |
| `GET /posts/{id}` vs the same item in `GET /posts` | Identical (ids 1, 25, 50, 75, 100 checked) |
| Two `GET /posts` calls | Identical |
| `POST /posts` | Echoes the post with `id` 101; nothing is stored (`GET /posts/101` is 404) |

## 3. Test design

- **Real wiring:** every test uses `ApiSettings.CreateHttpClient()` (real `HttpClientHandler`, configured base
  address, request and response logging) and real `PublicApiClient`, `PostProcessor` and `PostValidator` instances.
  No mocks or stubs, except the deliberate bad host in NEG-01.
- **Typed comparisons:** results are compared as `Post` objects field by field (a small `AssertSamePost` helper), not
  as raw JSON. That is what distinguishes these tests from the API tests.
- **Ids chosen as boundaries:** 1 and 100 (range ends), 10 and 11 (last post of user 1, first of user 2), 50 and 51
  (middle), 91 (first post of the last user), plus 25 and 75.
- **No hard-coded content** except where the API is known to be fixed (post 1's title start); prefer comparing one
  call with another.
- **Parallel:** each test has its own `HttpClient`; all calls are reads or fake creates, so tests run in parallel.
- **Traceability:** `[Description("<ID>")]` with the IDs below.
- **Assertions:** NUnit `Assert.That`, matching the other non-UI tests.

## 4. Test cases

### Real HTTP client usage

| ID | Test | Action | Expected |
|---|---|---|---|
| HTTP-01 | The configured client reaches the API | `GetPostsAsync()` through `ApiSettings.CreateHttpClient()` | Returns posts; the client's `BaseAddress` equals the configured `ApiBaseUrl` with a trailing `/` |
| HTTP-02 | One client serves several calls | `GetPostsAsync()`, `GetPostAsync(1)`, `CreatePostAsync(...)` on the same `HttpClient` | All three succeed |

### Real API calls

| ID | Test | Action | Expected |
|---|---|---|---|
| API-01 | Live list through the client | `GetPostsAsync()` | 100 posts, every field filled in (guards the camelCase fix D1 against live data) |
| API-02 | Live single post through the client | `GetPostAsync(1)` | `Id` 1, `UserId` 1, title starts "sunt aut facere", body not empty |
| API-03 | Live create through the client | `CreatePostAsync(new Post { UserId = 1, Title, Body })` | Returned post has `Id` 101 and the same `UserId`, `Title`, `Body` |

### End-to-end flow validation

| ID | Test | Action | Expected |
|---|---|---|---|
| E2E-01 | Live data through the processor | `new PostProcessor(client).GetValidPostsAsync()` | 100 valid posts, ids 1–100 in order (all live posts are valid) |
| E2E-02 | Processor keeps exactly what the validator accepts | `GetPostsAsync()` and `GetValidPostsAsync()` | Processor result equals `posts.Where(PostValidator.IsValid)` |
| E2E-03 | Create, then validate | `CreatePostAsync(post)`, then `PostValidator.IsValid(created)` | `true` |
| E2E-04 | Create is not persisted | `CreatePostAsync(post)`, then `GetPostAsync(101)` | `null` (the client's "not found" result) |
| E2E-05 | Unknown id through the chain | `GetPostAsync(9999)` | `null`, no exception |

### Data consistency checks

| ID | Test | Action | Expected |
|---|---|---|---|
| CON-01 | List and single post agree (data-driven over the MID ids) | `GetPostsAsync()` and `GetPostAsync(id)` | Same `UserId`, `Id`, `Title`, `Body` |
| CON-02 | Repeated calls agree | `GetPostsAsync()` twice | Identical lists, field by field |
| CON-03 | Processor output agrees with the raw list | `GetValidPostsAsync()` and `GetPostsAsync()` | Each processed post equals the post with the same id in the list |
| CON-04 | Data shape is consistent | `GetPostsAsync()` | Ids unique and 1–100; each `userId` 1–10 has 10 posts; titles unique; text kept exactly, including line breaks in bodies |
| CON-05 | Created post echoes the request | `CreatePostAsync(post)` with title and body containing Unicode and a line break | Returned `Title` and `Body` equal the request exactly |

### Multiple valid IDs testing

| ID | Test | Action | Expected |
|---|---|---|---|
| MID-01 | Valid ids at boundaries (data-driven: 1, 10, 11, 25, 50, 51, 75, 91, 100) | `GetPostAsync(id)` | Not `null`; `Id` equals `id`; `UserId` equals `(id - 1) / 10 + 1`; title and body not empty; passes `PostValidator` |
| MID-02 | Every id agrees with the list | `GetPostAsync(id)` for all 100 ids, at most 10 requests at a time | All 100 equal the list items |

### Negative integration

| ID | Test | Action | Expected |
|---|---|---|---|
| NEG-01 | Real network failure through the chain | `PostProcessor` over a `PublicApiClient` whose `HttpClient` points at `https://api.invalid/` (the `.invalid` domain never resolves) | `HttpRequestException` reaches the caller |

**Totals:** 18 test cases. With data-driven rows (CON-01 and MID-01 use the same 9 ids) they run as 34 tests, making
about 150 HTTP requests per run (101 of them from MID-02).

## 5. README traceability

| README requirement | Test cases |
|---|---|
| Real HTTP client usage | HTTP-01, HTTP-02, NEG-01 (and every test uses the real client) |
| Real API calls to jsonplaceholder.typicode.com | API-01 to API-03 |
| End-to-end flow validation | E2E-01 to E2E-05 |
| Data consistency checks | CON-01 to CON-05, MID-02 |
| Multiple valid IDs testing | MID-01, MID-02, CON-01 |

## 6. Overlap with the API tests (README §2)

| Topic | API tests | Integration tests (this plan) |
|---|---|---|
| Status codes, headers, schema, raw JSON | Yes | No |
| One client method at a time against the live API | CLI-01 to CLI-04 | API-01 to API-03, kept short; they set up the chain tests |
| `PostProcessor` and `PostValidator` with live data | No | Yes (E2E) |
| Consistency across calls | Post 1 only, raw JSON (ID-03) | Many ids, typed `Post` objects (CON, MID) |
| Real network failure | No (CLI-05 uses a stub) | Yes (NEG-01) |

## 7. Risks and decisions

| ID | Risk or observation | Decision |
|---|---|---|
| I1 | The API is public and can be slow, rate-limited or down; failures there aren't product defects. | Keep the `Integration` category separate in CI. No retries for now (same as the API tests); add NUnit `[Retry(2)]` if it becomes flaky. |
| I2 | MID-02 makes 100 requests. | Limit to 10 at a time. If it's too slow or noisy, mark it `[Explicit]` so it only runs on request. |
| I3 | Live content could change (it hasn't in years). | Only API-02 checks fixed content (post 1's title start); everything else compares one call with another or checks shape. |
| I4 | The request/response log is long for list calls. | Accepted; it's in each test's own output. |
| I5 | Writes are faked (`id` 101, nothing stored). | E2E-04 documents it; no test depends on created data being stored. |
| I6 | NEG-01 depends on DNS failing for `.invalid`, which is reserved for that purpose (RFC 2606). | Reliable without network access to a real failing server. |

## 8. Implementation outline

- Fill in [PublicApiIntegrationTests.cs](../tests/IntegrationTests/PublicApiIntegrationTests.cs) using the existing
  setup; add a `PostProcessor` built on the same client, an `AssertSamePost` helper, and `[TestCase]` rows for MID-01.
- Keep `[Category("Integration")]` and NUnit `Assert.That`; add `[Description("<ID>")]`.
- No production code changes.
- Run `dotnet test --filter "Category=Integration"`; all tests should pass while the API is reachable.

**Status (2026-11-10):** implemented. 18 test cases run as 34 tests; all pass against the live API (MID-02 takes
about 2 s).
