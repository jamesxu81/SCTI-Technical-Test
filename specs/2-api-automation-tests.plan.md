# API Automation Test Plan: JSONPlaceholder `/posts`

Covers README "2. API Automation Tests". Facts below were checked against the live API on 2026-11-10.

## 1. Scope

| In scope | Out of scope |
|---|---|
| `GET /posts`, `GET /posts/{id}`, `POST /posts` | `PUT`, `PATCH`, `DELETE`, other resources (`/comments`, `/users`) |
| Status codes, response headers, JSON schema, counts, values, negative inputs | Performance and load (separate WLM work) |
| `PublicApiClient` behaviour against the real API | `PostProcessor`/`PostValidator` logic (covered by unit, mock and component tests) |

- **System under test:** [PublicApiClient.cs](../src/Api/Clients/PublicApiClient.cs) and the API it calls.
- **Environment:** `ApiBaseUrl` from `tests/appsettings*.json`, selected with `TEST_ENV` and overridable with `TEST_ApiBaseUrl` (see [ApiSettings.cs](../tests/Support/ApiSettings.cs)).
- **Category:** `[Category("API")]`, run with `dotnet test --filter "Category=API"`.

## 2. Observed API behaviour

| Request | Status | Response |
|---|---|---|
| `GET /posts` | 200, `application/json; charset=utf-8` | Array of 100 posts, ids 1–100 in order, `userId` 1–10 (10 posts each) |
| `GET /posts/1` | 200 | `{ "userId": 1, "id": 1, "title": "sunt aut facere…", "body": "quia et suscipit…" }` |
| `GET /posts/100` | 200 | `userId` 10, `id` 100 |
| `GET /posts/101`, `/0`, `/-1`, `/9999`, `/abc` | 404 | `{}` |
| `GET /posts?userId=1` | 200 | 10 posts |
| `GET /posts?userId=999` | 200 | `[]` |
| `POST /posts` with `{userId, title, body}` | 201, `application/json` | Echoes the body plus `"id": 101` |
| `POST /posts` with an `id` in the body (e.g. `0`) | 201 | `id` is replaced with 101 |
| `POST /posts` with `{}` | 201 | `{ "id": 101 }` (no server-side validation) |
| `POST /posts` with malformed JSON | 500 | Plain-text `SyntaxError…` stack trace |
| `GET /posts/101` after a POST | 404 | Creation is faked; nothing is stored |

Post schema: `userId` integer ≥ 1, `id` integer ≥ 1, `title` non-empty string, `body` non-empty string. No other properties.

## 3. Defect found while planning (fixed)

**D1: `PublicApiClient` returned empty posts.** It deserialized with default `System.Text.Json` options, which match property names case-sensitively. The API sends camelCase (`userId`, `title`), but `Post` has PascalCase properties, so every field stayed at its default. A check printed `Id=0 UserId=0 Title='' Body.Length=0` for post 1. The original sample test passed only because it checked the list wasn't empty.

- **Fix (done):** `PublicApiClient` now uses `JsonSerializerDefaults.Web` (case-insensitive, camelCase) for reading responses and for the POST request body.
- **Regression tests:** CLI-01, CLI-02 and CLI-04 fail without the fix. The HTTP contract tests (GET-*, ID-*, POST-*) read raw JSON, so they don't depend on it.

## 4. Test design

Two layers, because `PublicApiClient` hides status codes (`GetPostAsync` returns `null` for any failure) and raw JSON:

1. **HTTP contract tests (GET-, ID-, POST-):** use the configured `HttpClient` directly to assert status, headers, raw JSON and schema.
2. **Client tests (CLI-, short for client):** use `PublicApiClient` to assert the typed results the rest of the code relies on.

**Schema validation:** [post.schema.json](../tests/ApiTests/Schemas/post.schema.json) (JSON Schema draft 2020-12), validated with the `JsonSchema.Net` package. It requires `userId`, `id`, `title` and `body`, rejects extra properties, and needs integers ≥ 1 and non-empty strings. A failure message lists each broken rule and the offending JSON.

**Assertions:** NUnit `Assert.That`, matching the existing non-UI tests. `Assert.Multiple` is used where several fields are checked at once.

**Traceability:** each test has `[Description("<ID>")]` with the ID from section 5, shown in Test Explorer and TRX/HTML reports.

**Logging:** `ApiSettings.CreateHttpClient()` adds [HttpLoggingHandler.cs](../tests/Support/HttpLoggingHandler.cs), which writes every call (method, URL, status, request and response bodies as indented JSON) to the test's output. CLI-05 uses a stub handler, so it isn't logged.

**Parallel:** all tests are read-only or call a fake create, so they run in parallel (assembly-level `Parallelizable(ParallelScope.Children)`). Each test gets its own fixture instance and `HttpClient` (`InstancePerTestCase`). Measured: 31 tests in about 0.6 s in parallel versus 4 s with one worker.

**Size:** 24 test cases, 31 test runs (ID-01 has 3 rows, ID-04 has 4, POST-04 has 3).

## 5. Test cases

### GET /posts

| ID | Type | Test | Expected | Test method |
|---|---|---|---|---|
| GET-01 | Positive, status | Get all posts | 200, `Content-Type` is `application/json` | `GetPosts_Returns200WithJsonContentType` |
| GET-02 | Count | Count posts | 200, a JSON array of exactly 100 | `GetPosts_ReturnsExactly100Posts` |
| GET-03 | Schema | Validate every item against the post schema | All 100 valid; no missing, extra or empty fields | `GetPosts_EveryPostMatchesSchema` |
| GET-04 | Data | Ids are unique and run 1–100 in order | `[1..100]` | `GetPosts_IdsAreUniqueAndOrderedFrom1To100` |
| GET-05 | Count | Posts per user | 10 users (1–10), 10 posts each | `GetPosts_EachOf10UsersHas10Posts` |
| GET-06 | Positive, filter | `GET /posts?userId=1` | 200, 10 posts, all `userId` 1 | `GetPosts_FilteredByUserId_ReturnsOnlyThatUsersPosts` |
| GET-07 | Negative, filter | `GET /posts?userId=999` | 200, empty array | `GetPosts_FilteredByUnknownUserId_ReturnsEmptyArray` |

### GET /posts/{id}

| ID | Type | Test | Expected | Test method |
|---|---|---|---|---|
| ID-01 | Positive, status, schema (data-driven: 1, 50, 100) | Get by valid id | 200, schema valid, `id` equals the requested id | `GetPostById_WithValidId_Returns200AndMatchingPost` |
| ID-02 | Data | Post 1 content | 200, `userId` 1, title starts "sunt aut facere" | `GetPostById_Post1_HasExpectedContent` |
| ID-03 | Consistency | Post 1 from `/posts/1` equals item 1 from `/posts` | Same JSON (deep compare) | `GetPostById_MatchesSamePostInList` |
| ID-04 | Negative, 404 (data-driven: 101, 0, -1, 9999) | Get by id out of range | 404, body is an empty object `{}` | `GetPostById_WithOutOfRangeId_Returns404AndEmptyObject` |
| ID-05 | Negative, 404 | Get by non-numeric id `abc` | 404 | `GetPostById_WithNonNumericId_Returns404` |

### POST /posts

| ID | Type | Test | Expected | Test method |
|---|---|---|---|---|
| POST-01 | Positive, status | Create a post with userId, title and body | 201, `application/json` | `CreatePost_Returns201WithJsonContentType` |
| POST-02 | Response | Response echoes the request | Same `userId`, `title`, `body`; `id` is 101 | `CreatePost_EchoesRequestWithNewId` |
| POST-03 | Schema | Response matches the post schema | Valid | `CreatePost_ResponseMatchesSchema` |
| POST-04 | Data (data-driven: 3 rows) | Special characters (quotes, `<>&`, newline, tab, backslash); Unicode ("Kia ora – ñ 漢字 😀", Māori macrons); 5,000-character body | 201, `title` and `body` echoed unchanged | `CreatePost_WithUnusualContent_EchoesItUnchanged` |
| POST-05 | Negative | Empty object `{}` | 201, response has only `id`, and fails the post schema; documents that the API doesn't validate input | `CreatePost_WithEmptyObject_IsAcceptedWithoutValidation` |
| POST-06 | Negative, status | Malformed JSON | 500 | `CreatePost_WithMalformedJson_Returns500` |
| POST-07 | Negative, non-persistence | Create (201), then `GET /posts/101` | 404; documents that JSONPlaceholder fakes writes | `CreatePost_IsNotPersisted` |

### PublicApiClient behaviour

| ID | Type | Test | Expected | Test method |
|---|---|---|---|---|
| CLI-01 | Positive, count | `GetPostsAsync()` | 100 posts, every `Post` fully populated (guards D1) | `GetPostsAsync_Returns100FullyPopulatedPosts` |
| CLI-02 | Positive | `GetPostAsync(1)` | `Post` with id 1, `userId` 1, title starts "sunt aut facere", non-empty body | `GetPostAsync_WithValidId_ReturnsPopulatedPost` |
| CLI-03 | Negative, 404 | `GetPostAsync(9999)` | `null` | `GetPostAsync_WithUnknownId_ReturnsNull` |
| CLI-04 | Positive | `CreatePostAsync(post)` | Returned `Post` has id 101 and the same `userId`, `title`, `body` | `CreatePostAsync_ReturnsCreatedPost` |
| CLI-05 | Negative | Server error, using a stub `HttpMessageHandler` that returns 500 (no network) | `GetPostsAsync` and `CreatePostAsync` throw `HttpRequestException`; `GetPostAsync` returns `null` | `Client_WhenServerReturns500_ThrowsOrReturnsNull` |

## 6. README traceability

| README requirement | Test cases |
|---|---|
| GET requests validation | GET-01–07, ID-01–05, CLI-01–03 |
| POST requests validation | POST-01–07, CLI-04 |
| Schema validation | GET-03, ID-01, POST-03, POST-05 |
| Status code validation | 200: GET-01–07, ID-01–03. 201: POST-01, POST-04, POST-05, POST-07. 404: ID-04, ID-05, POST-07. 500: POST-06, CLI-05 |
| Positive and negative scenarios | Positive: GET-01–06, ID-01–03, POST-01–04, CLI-01, CLI-02, CLI-04. Negative: GET-07, ID-04, ID-05, POST-05–07, CLI-03, CLI-05 |
| Count validation | GET-02, GET-05, GET-06, GET-07, CLI-01 |
| Invalid ID handling (404) | ID-04, ID-05, CLI-03 |
| `GET /posts` returns 100 posts with schema validation | GET-02, GET-03 |
| `GET /posts/{id}` returns specific post or null | ID-01, CLI-02, CLI-03 |
| `POST /posts` creates new post and validates response | POST-01–03, CLI-04 |

## 7. Implementation

| File | Role |
|---|---|
| [PublicApiClient.cs](../src/Api/Clients/PublicApiClient.cs) | D1 fix: `JsonSerializerDefaults.Web` for reading and writing |
| [PublicApiClientTests.cs](../tests/ApiTests/PublicApiClientTests.cs) | All cases in section 5, in one `#region` per endpoint plus one for the client. `[TestCase]` and `[TestCaseSource]` for data-driven rows |
| [post.schema.json](../tests/ApiTests/Schemas/post.schema.json) | Post schema, copied to the build output (`ApiTests/Schemas/*.json` in the test project) |
| [ApiSettings.cs](../tests/Support/ApiSettings.cs) | `BaseAddress` from `ApiBaseUrl`; `CreateHttpClient()` with logging |
| [HttpLoggingHandler.cs](../tests/Support/HttpLoggingHandler.cs) | Logs request and response bodies to the test output |
| [TechnicalTest.Tests.csproj](../tests/TechnicalTest.Tests.csproj) | `JsonSchema.Net` 9.6.0 |

**Run and report:**

```bash
dotnet test --filter "Category=API" --logger "html;LogFileName=api-report.html" --logger "trx;LogFileName=api.trx"
```

Reports go to `tests/TestResults/` (git-ignored) and include each test's request and response log.

**Status:** all 31 test runs pass against the live API (2026-11-10).

## 8. Risks

- **Third-party service:** JSONPlaceholder is public and can be slow, rate-limited or down. Failures there aren't product defects. Keep the API category separate in CI. Retries are not added yet; NUnit `[Retry(2)]` is an option if the service becomes flaky.
- **Fake writes:** POST never persists and always returns id 101. Tests must not depend on created data.
- **Environment files:** Dev, QA and Prod all point at the same public API for now.
