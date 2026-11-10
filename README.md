# Senior Test Engineer - Technical Test

## Overview

A C# / .NET 8 test automation framework that covers seven testing layers:

| # | Layer | Category | Tests | Target |
|---|---|---|---|---|
| 1 | UI automation (Playwright + Reqnroll) | `UI` | 11 scenarios | Live site https://insure.scti.co.nz/ |
| 2 | API automation | `API` | 31 | Live API https://jsonplaceholder.typicode.com |
| 3 | Unit | `Unit` | 52 | `PostValidator` |
| 4 | Mock (Moq) | `Mock` | 36 | `PostProcessor` with a mocked `IPublicApiClient` |
| 5 | Component | `Component` | 84 | `PostProcessor` + `PostValidator` + real `PublicApiClient` on canned HTTP responses |
| 6 | Integration | `Integration` | 34 | Real HTTP calls to the live API, end to end |
| 7 | Validation | `Validation` | 191 | `PostValidator` rules, boundaries and special characters |

All 439 tests pass (428 non-UI + 11 UI on Chromium). Each layer has a test plan in [specs/](specs/).

## Project Structure

```
TechnicalTest/
├── src/                                   # Code under test and the UI page objects
│   ├── Api/
│   │   ├── Clients/
│   │   │   ├── IPublicApiClient.cs        # GetPostsAsync, GetPostAsync, CreatePostAsync
│   │   │   └── PublicApiClient.cs         # HttpClient + System.Text.Json (camelCase)
│   │   ├── Models/Post.cs
│   │   ├── Processors/PostProcessor.cs    # Returns only valid posts
│   │   └── Validators/PostValidator.cs    # UserId > 0, Title and Body not blank
│   └── Ui/
│       ├── Helpers/
│       │   ├── ConfigurationHelper.cs     # appsettings + TEST_ENV + TEST_* overrides
│       │   ├── UiSettings.cs              # BaseUrl, Timeout, Headless, Browser
│       │   ├── QuoteApi.cs                # Site API paths the pages wait for
│       │   └── UiFormat.cs                # Money and date formats used by the site
│       ├── Models/
│       │   ├── QuoteTraveller.cs
│       │   └── TravellerFieldName.cs
│       └── Pages/                         # Page Object Model
│           ├── BasePage.cs
│           ├── QuoteFormPage.cs           # Journey, destinations, dates, travellers
│           ├── PlanOptionsPage.cs         # Select and customise a plan
│           └── TravellerDetailsPage.cs    # Eligibility, declarations, traveller forms
│
├── tests/
│   ├── Features/
│   │   ├── InsuranceBooking.feature             # README journey (Scenario Outline)
│   │   └── InsuranceBookingValidation.feature   # Form rules found on the site
│   ├── StepDefinitions/
│   │   ├── InsuranceBookingSteps.cs
│   │   ├── InsuranceBookingValidationSteps.cs
│   │   └── StepArgumentTransformations.cs
│   ├── Hooks/PlaywrightHooks.cs           # Browser per scenario, failure artifacts
│   ├── Support/
│   │   ├── ApiSettings.cs                 # API base address and logging HttpClient
│   │   ├── HttpLoggingHandler.cs          # Logs API requests and responses
│   │   ├── NetworkRecorder.cs             # Captures browser network traffic
│   │   ├── QuoteState.cs                  # Data shared between steps in a scenario
│   │   └── TestData.cs                    # Relative dates (today+30), ages, lists
│   ├── ApiTests/
│   │   ├── PublicApiClientTests.cs
│   │   └── Schemas/post.schema.json
│   ├── UnitTests/PostValidatorTests.cs
│   ├── MockTests/PostProcessorMockTests.cs
│   ├── ComponentTests/PostProcessorComponentTests.cs
│   ├── IntegrationTests/PublicApiIntegrationTests.cs
│   ├── ValidationTests/PostValidationTests.cs
│   ├── PerfTests/                         # JMeter script, load profiles, journey recording
│   ├── AssemblyInfo.cs                    # Parallel execution settings
│   ├── appsettings.json                   # Defaults
│   ├── appsettings.{Dev,QA,Prod}.json     # Per-environment overrides
│   ├── headed.runsettings                 # Visible browser, 3 workers
│   └── reqnroll.json                      # Retry and HTML report settings
│
├── specs/                                 # Test plans: 1 to 7 per layer, 8 non-functional
└── .github/
    ├── workflows/
    │   ├── all-tests.yml                  # Every category, on push and PR
    │   ├── ui-tests.yml                   # UI tests per environment, manual
    │   └── perf-tests.yml                 # JMeter load profile of your choice, manual
    └── agents/                            # test-planner, test-generator, test-healer (all layers)
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- PowerShell 7 (`pwsh`), used to install the Playwright browsers. If it isn't installed:
  `dotnet tool install --global PowerShell`
- Internet access (the UI, API and Integration tests use live sites)

### Installation

```bash
git clone <repository-url>
cd TechnicalTest
dotnet restore
dotnet build
pwsh tests/bin/Debug/net8.0/playwright.ps1 install
```

Add `--with-deps` to the last command on a fresh Linux machine to install the browsers' system libraries.

## Running Tests

```bash
dotnet test                                   # everything
dotnet test --filter "Category!=UI"           # everything except the browser tests
dotnet test --filter "Category=Unit"          # one layer: Unit, Validation, Mock, Component, API, Integration or UI
```

> Each UI run of the booking journey creates draft quotes on the live site (4 per full UI run).

### UI options

| Goal | Command |
|---|---|
| Watch the browser | `dotnet test --filter "Category=UI" --settings tests/headed.runsettings` |
| Another browser | `TEST_Browser=firefox dotnet test --filter "Category=UI"` (`chromium`, `firefox` or `webkit`) |
| Another environment | `TEST_ENV=QA dotnet test --filter "Category=UI"` |
| One scenario | `dotnet test --filter "Name~TheQuoteFormRequiresEveryField"` |

In PowerShell, set variables first, e.g. `$env:TEST_ENV="QA"`.

## Configuration

Settings are read in this order; later sources win:

1. `tests/appsettings.json`
2. `tests/appsettings.{TEST_ENV}.json` when `TEST_ENV` is set (the run fails if that file is missing)
3. Environment variables prefixed `TEST_`, e.g. `TEST_Headless=false`, `TEST_BaseUrl=https://...`

| Setting | Default | Used for |
|---|---|---|
| `BaseUrl` | `https://insure.scti.co.nz/` | UI tests |
| `ApiBaseUrl` | `https://jsonplaceholder.typicode.com` | API and Integration tests |
| `Timeout` | `30000` (ms) | Playwright actions, navigation and `Expect` assertions |
| `Headless` | `true` | Hide or show the browser |
| `Browser` | `chromium` | `chromium`, `firefox` or `webkit` |

The Dev, QA and Prod files currently point at the same live URLs, because only one public environment exists. Change
`BaseUrl` / `ApiBaseUrl` in those files when separate environments are available.

## Test Coverage

### 1. UI Test Automation (Playwright + Reqnroll)
Plan: [specs/1-ui-test-automation.plan.md](specs/1-ui-test-automation.plan.md)

**Booking journey** (`InsuranceBooking.feature`, a Scenario Outline with 2 example rows):
navigate to the site → pick One return journey → add destinations, dates and travellers → Get plan options → Select and
customise a plan → choose discount, excess, cancellation cover and extra cover → Next: Traveller details → complete
eligibility and privacy declarations → fill in 1 adult, 1 dependent and 1 non-dependent traveller.

Besides the screens, the steps check:
- the quote request the site sends (destinations, traveller ages)
- the plan price equals the base price plus each premium change
- the plan update request (discount, excess, cancellation cover, extra cover)

**Validation** (`InsuranceBookingValidation.feature`, 9 scenarios): required fields, traveller age limits, return date
at least 2 days after departure, eligibility and declarations, date of birth mismatch prompt, adding, changing and
removing a traveller.

How it works:
- Page objects expose locators and actions; step definitions never use `IPage` directly.
- Waits use Playwright auto-waiting, web-first `Expect(...)` assertions and waits for the site's API responses. There
  are no fixed sleeps.
- Dates in the examples are relative (`today+30`) and ages become dates of birth, so the data never goes stale. The
  browser runs in the `Pacific/Auckland` time zone with the `en-NZ` locale, like the site's users.

### 2. API Automation Tests
Plan: [specs/2-api-automation-tests.plan.md](specs/2-api-automation-tests.plan.md)

`GET /posts` (count 100, JSON schema, unique ids, filtering by user), `GET /posts/{id}` (valid ids; out-of-range and
non-numeric ids return 404), `POST /posts` (201, echoed fields, new id). Positive and negative cases, status codes and schema checks
against [post.schema.json](tests/ApiTests/Schemas/post.schema.json).

### 3. Unit Tests
Plan: [specs/3-unit-tests.plan.md](specs/3-unit-tests.plan.md)

`PostValidator`: null post, each field rule, boundaries (`UserId` 0, 1, negative, `int.MaxValue`), blank and
whitespace text, several invalid fields at once.

### 4. Mock Tests (Moq)
Plan: [specs/4-mock-tests.plan.md](specs/4-mock-tests.plan.md)

`PostProcessor` with a mocked `IPublicApiClient`: success, failures and exceptions, empty and invalid lists, and
`Verify` of the calls made.

### 5. Component Tests
Plan: [specs/5-component-tests.plan.md](specs/5-component-tests.plan.md)

`PostProcessor`, `PostValidator` and the real `PublicApiClient` together, fed canned HTTP responses: filtering, business
rules, partial and malformed data, mixed valid and invalid posts.

### 6. Integration Tests
Plan: [specs/6-integration-tests.plan.md](specs/6-integration-tests.plan.md)

Real `HttpClient` calls to the live API: end-to-end flow, data consistency between the list and single-post endpoints,
several valid ids, and network failure handling.

### 7. Validation Tests
Plan: [specs/7-validation-tests.plan.md](specs/7-validation-tests.plan.md)

Required fields, `Title` / `Body` / `UserId` constraints, boundary values, special characters, Unicode, very long
strings and every kind of whitespace.

### Known issues
Found while testing. The tests assert the current behaviour so they fail if it changes; see the plans for details.

| Id | Issue | Status |
|---|---|---|
| D1 | `PublicApiClient` returned empty posts (case-sensitive JSON) | Fixed |
| M1 | A `null` post list makes `PostProcessor` throw `ArgumentNullException` | Kept as a known gap |
| C1 | One malformed record makes the whole list fail with `JsonException` | Kept as a known gap |
| V1 | A title or body made only of invisible characters (e.g. zero-width space) passes validation | Kept as a known gap |
| UI | Firefox never shows the "Date of birth changed" prompt, so 1 of 11 UI scenarios fails there | Open |

## Reports and Logs

| Output | When | Where (local run) |
|---|---|---|
| Console results | Every run | Terminal |
| TRX / HTML results | When requested, e.g. `--logger trx --logger html` | `TestResults/` (or `--results-directory`) |
| Reqnroll HTML report | Every UI run | `tests/bin/Debug/net8.0/TestResults/ui/reqnroll-report.html` |
| Network log | Every UI scenario (bodies only for the site's own `/api/` calls) | `tests/bin/Debug/net8.0/TestResults/ui/*.network.log` |
| Screenshot, Playwright trace, video | Failed UI scenarios only | `tests/bin/Debug/net8.0/TestResults/ui/*.png`, `*.trace.zip`, `*.webm` |
| API request / response log | API and Integration tests | Test output |

Open a trace with `pwsh tests/bin/Debug/net8.0/playwright.ps1 show-trace <file>` or at https://trace.playwright.dev.

Failed UI scenarios are retried up to 2 times (`NRetrySettings` in `reqnroll.json`).

## CI/CD Pipelines

All three workflows run on `ubuntu-24.04` and keep results as build artifacts (14 days; 30 for performance runs).

### All Tests ([all-tests.yml](.github/workflows/all-tests.yml))
- Runs on push to `main`, on pull requests and manually; changes to Markdown or `specs/` only don't trigger it.
- One parallel job per category: Unit, Validation, Mock, Component, API, Integration, UI.
- Uploads TRX, HTML and (for UI) the Reqnroll report and failure artifacts per category.
- Publishes a test summary check with `dorny/test-reporter`.

### UI Tests, multi-environment ([ui-tests.yml](.github/workflows/ui-tests.yml))
- Manual trigger with two choices: environment (All, Dev, QA, Prod) and browser (chromium, firefox, webkit).
- "All" runs Dev, QA and Prod in parallel; each uses its GitHub environment, so protection rules such as an approval
  before Prod apply.
- Uploads results per environment and browser and publishes a test summary.

### Performance Tests, JMeter ([perf-tests.yml](.github/workflows/perf-tests.yml))
- **Trigger:** manual only. Choose the load profile (smoke, baseline, load, stress), the target host and the allowed
  error %.
- **Target guard:** the run stops if baseline, load or stress has no host, and it never runs them against the live
  site. Only one performance run happens at a time.
- **Setup:** it installs Java 21 and a checksum-verified, cached JMeter 5.6.3, then runs the profile.
- **Results:**
  - Posts a response-time table per transaction to the run summary.
  - Fails if the error rate is over the limit.
  - Uploads the JTL, `jmeter.log` and HTML report.
- **Approval:** add required reviewers to the `perf` environment in the repo settings so each run is approved first.

## Technologies Used

- **Framework**: .NET 8.0
- **UI automation**: Microsoft.Playwright 1.64.1
- **BDD**: Reqnroll 3.3.4 (NUnit), NUnitRetry.ReqnrollPlugin 2.1.0
- **Test framework**: NUnit 4.0.1
- **Mocking**: Moq 4.20.70
- **Assertions**: FluentAssertions 6.12.0, JsonSchema.Net 9.6.0
- **Configuration**: Microsoft.Extensions.Configuration (JSON + environment variables)
- **CI**: GitHub Actions
- **API under test**: JSONPlaceholder (typicode.com)

## Design Patterns

### Page Object Model
- `BasePage`: shared helpers (radio groups, messages, waiting for a site API response).
- `QuoteFormPage`, `PlanOptionsPage`, `TravellerDetailsPage`: one class per step of the journey, each with its locators,
  actions and an `ExpectOpenAsync()` check.
- Step definitions describe behaviour; the page objects own every selector.

### Dependency Injection
- UI: `PlaywrightHooks` registers the page, page objects and `NetworkRecorder` in Reqnroll's container for each
  scenario; step classes receive them through their constructors.
- API: `PostProcessor` depends on `IPublicApiClient`, so mock tests swap in Moq and component tests use the real client
  with canned HTTP responses.

### Repository Pattern
- `PublicApiClient` is the single place that knows the `/posts` endpoints and JSON format; callers work with `Post`
  objects.

## Key Features

1. **Parallel execution**: NUnit runs tests in parallel (`ParallelScope.Children`, a new fixture instance per test);
   each UI scenario has its own browser.
2. **Network call capture**: every browser request is logged per scenario; API calls are logged with request and
   response.
3. **Dynamic waits**: Playwright auto-waiting and web-first assertions; no fixed sleeps.
4. **Multi-environment**: Dev, QA and Prod settings, chosen with `TEST_ENV` locally or in CI.
5. **Assertions**: text, attributes, element counts, prices, captured requests, JSON schema.
6. **Error handling**: failure artifacts are saved even when the browser misbehaves; failed UI scenarios are retried.
7. **Separation of concerns**: page objects and API code in `src`, tests and test support in `tests`.
8. **Test data management**: Scenario Outlines with Examples, relative dates, NUnit `TestCase` / `TestCaseSource`.
9. **CI/CD**: two GitHub Actions workflows with artifacts and test summaries.
10. **Reports**: TRX, HTML, Reqnroll HTML report, screenshots, traces, videos and network logs.

## Test Plans and AI Agents

- [specs/](specs/) holds one plan per layer, with the cases, data and status of each.
- [.github/agents/](.github/agents/) holds three agents for every test layer (UI, API, unit, mock, component,
  integration, validation and JMeter performance). They also hold what has been learned about the SCTI site and this
  repo.
  - **test-planner** studies the requirement, code, live site or API and writes a plan in `specs/`.
  - **test-generator** implements a plan following the repo's conventions, then builds and runs the tests.
  - **test-healer** finds the root cause of failing tests and fixes them, or records a known issue.

## Non-Functional (JMeter)

Plan: [specs/8-non-functional-performance.plan.md](specs/8-non-functional-performance.plan.md). It covers how the
journey is recorded, the workload model, the baseline, load and stress profiles, distributed load generation, targets,
and the rules for testing a live site.

Script: [tests/PerfTests/jmeter/booking-journey.jmx](tests/PerfTests/jmeter/booking-journey.jmx). The load profile is
chosen at run time, from `tests/PerfTests/jmeter`:

```bash
brew install jmeter    # JMeter 5.6.3 + OpenJDK
jmeter -n -t booking-journey.jmx -q profiles/smoke.properties -l results/smoke.jtl -e -o results/smoke-report
jmeter -n -t booking-journey.jmx -q profiles/load.properties -Jhost=<approved host> -l results/load.jtl -e -o results/load-report
```

Only `smoke` (1 journey) may run against the live site. Baseline, load and stress need a test environment or SCTI's
approval; without `-Jhost` they go to a host that never resolves.

In CI, run **Performance Tests (JMeter)** ([perf-tests.yml](.github/workflows/perf-tests.yml)) from the Actions tab:
- Choose the profile and target host when you start it.
- It posts a response-time table to the run summary and uploads the JTL, log and HTML report as an artifact.

The README's original requirements:

**Record the journey below**
- Navigate to https://insure.scti.co.nz/
- Pick One return journey
- Add destinations, travel dates, travellers and Get plan options
- Select a plan with Select and customise plan
- Choose discount, excess, cancellation cover and extra cover, then Next: Traveller details
- Complete traveller eligibility and the privacy declaration
- Fill in the traveller form
- Add travellers: 1 adult, 1 dependent, 1 non-dependent

**Create a WLM model for baseline, load and stress tests**
**Distributed WLM model**

---

**Author**: Senior Test Automation Engineer Candidate - James Xu
**Version**: 2.0.0
**Last Updated**: November 2026
