# Insurance booking UI test plan

Source of truth: `README.md`, section "1. UI Test Automation (Playwright + Reqnroll)" and its "Automate this Scenario
Flow". Everything below was observed on the live site with Playwright on 2026-11-09 (Chrome, fresh profile). Prices
quoted are examples from that session and will change.

## 1. Overview and scope

**Site:** https://insure.scti.co.nz/ (`BaseUrl` in `tests/appsettings.json`), SCTI travel insurance. This is a live
production insurer.

**Journey as it exists on the site**

| # | README step | What the site does | URL |
|---|---|---|---|
| 1 | Navigate to the page | Home page with the quote form. Trip purpose "Overseas travel" is the default. | `/` |
| 2 | Pick One return journey | Tab "One return journey" (selected by default; "Year-round travel" is the other tab). | `/` |
| 3 | Add destinations, travel dates, travellers and Get plan options | Three fields, each opening a dialog: destinations, travel dates, number of travellers with their ages. The button is **"Get a quote"** (the README's "Get plan options"). It creates a quote and opens the plan options. | `/` |
| 4 | Select One return journey with Select and customise plan | Two plans: "International Comprehensive Single Trip" (labelled "One return journey") and "International Medical Only". Button **"Select and customise plan"** on the first one. | `/our-policies/comprehensive?step=plan-options` |
| 5 | Choose discount, excess, cancellation cover, extra cover, then Next: Traveller details | A customise section expands under the plans. Each change re-prices the plan. | same URL |
| 6 | Complete Traveller Eligibility and privacy declaration | Three Yes/No eligibility questions and two declaration checkboxes. | `?step=traveller-details` |
| 7 | Fill Traveller form | A Travellers section appears on the same page once step 6 is done. | same URL |
| 8 | Add Traveller: 1 adult, 1 dependent, 1 non-dependent | The three travellers chosen in step 3 each get a form (main policyholder, dependent child, non-dependent child). An "Add traveller" button adds more. | same URL |

**Stop point:** all three traveller forms filled with no validation errors, still on `step=traveller-details`.

**Out of scope, and why:** clicking "Next: High-value items" with a complete form, and everything after it (High-value
items, Medical, Summary, payment). The site is a production insurer, so going further would submit personal data and
could create a policy or take a payment. No network call was made while the traveller form was being filled, so
stopping before "Next: High-value items" keeps the fake traveller data in the browser. Validation scenarios may click
"Next: High-value items" only while required fields are still empty, and must then assert the page stayed on
`step=traveller-details`.

**Side effect that is in scope:** "Get a quote" creates a draft quote with a policy number (for example
`SCTI2620582656A/00/01`), and each customisation updates it. Every scenario that gets past the quote form creates one
draft quote in production (four per full run with this plan).

**Requirements coverage**

| README requirement | Where this plan covers it |
|---|---|
| Feature files with Scenario Outline and Examples | Happy-path Scenario Outline (2 rows) and the age-limit Scenario Outline (4 rows), section 5 |
| Page Object Model | Section 3: `BasePage` for shared helpers, one page object per step (`QuoteFormPage`, `PlanOptionsPage`, `TravellerDetailsPage`), private locators |
| Network call capture | Section 4: every request logged per scenario; first-party `/api/` calls asserted by status and request body |
| Dynamic element handling with waits | "Wait / notes" column in section 3, and section 7 |
| Form filling and validation | Happy path fills every form; validation scenarios V1 to V5 use the exact messages found on the site |
| Multi-environment configuration | `BaseUrl`, `Timeout`, `Headless` read through `ConfigurationHelper` (`TEST_ENV` picks `appsettings.{Env}.json`); no URLs in feature files |
| Parallel execution support | Two feature files (happy path, validation), one browser context per scenario, no shared state |
| Comprehensive assertions | Text, attributes (`aria-selected`, checked state, input values), element counts, URL, network bodies, premium arithmetic |
| Error handling and detailed logging | Screenshot and network log attached on failure, console errors recorded, TRX via `dotnet test --logger trx` |
| Test data management | Examples tables, relative dates (`today+30`) and age-based dates of birth (`age:40`), section 2 |
| Technologies | .NET 8, Microsoft.Playwright 1.64.1, Reqnroll 2.4.1 with NUnit 4.0.1, FluentAssertions for non-UI values (network bodies, price sums), Microsoft.Extensions.Configuration. Moq and JSONPlaceholder are not used by the UI tests. |
| Clean code, SOLID, DRY | One helper for relative dates and dates of birth, one network recorder injected through Reqnroll context injection, locators defined once in the page object |

## 2. Test data rules

**Dates**
- Use tokens `today+N`, resolved in New Zealand time (`Pacific/Auckland`) and typed as `dd/MM/yyyy`. A CI agent on
  UTC can otherwise be a day behind the site.
- Departure must be today or later: earlier days are disabled in the calendar and named "Not available ...".
- Return must not be before departure. Typing an earlier return date is silently rejected: the field keeps its
  previous value and no message is shown.
- The travel dates field shows `dd/MM/yyyy - dd/MM/yyyy`. The API sends `yyyy-MM-ddT00:00:00.000Z`.
- Insurance starts on the quote date (today), sent as `StartDateOfInsurance`. Traveller ages are measured on that date.

**Traveller types and ages** (from the travellers dialog and its tooltips)

| Type | Allowed age | Site definition | Message when outside |
|---|---|---|---|
| Adult | 18 to 118 | - | Adult traveller must be aged between 18 and 118 |
| Dependent child | 0 to 20 | "Your children, stepchildren, foster children, and grandchildren who are under 21 years old at the date your insurance starts and are not in full-time employment." | Dependent traveller must be aged 20 or under |
| Non-dependent child | 0 to 17 | "Non-dependent children include children under 18 years old who aren't related to any of the adults your policy covers, such as your child's friend." | Non-dependent traveller must be aged 17 or under |

- The traveller list for the existing step `I add "..." travellers` uses `Type:age` pairs:
  `Adult:40, DependentChild:10, NonDependentChild:12`.
- Each date of birth must give the same age as the quote, or the site shows "Entered age and date of birth do not
  match." Use the token `age:N`, resolved as today (NZ) minus N years minus one month. Avoid 29 February.

**Option values**

| Field | Values | Default |
|---|---|---|
| Destinations | Any country from search or the popular list (Australia, Fiji, Cook Islands, Samoa, Tonga, Japan, Indonesia, Singapore, India, Thailand, United States of America, Canada, Mexico, Brazil, Argentina, France, Germany, Netherlands, United Kingdom, Antarctica, Worldwide). Several allowed, comma-separated in the step. The API uses ISO-3 codes (Japan `JPN`, Australia `AUS`). | none |
| Southern Cross Health Society member (discount) | Yes, No. Required. Yes takes 5% off the base premium. | none |
| Excess | $0, $100, $200 | $200 |
| Cancellation cover | $2,500, $5,000, $10,000, Unlimited | $2,500 |
| Extra cover | Moped or motorbike, Skiing or snowboarding, Cruise, any combination, or None | none |
| Title | Mr., Mrs., Ms., Miss., Master., Mx. | none |
| Eligibility answers that pass | Q1 Yes, Q2 No, Q3 No | none |

**Fictitious traveller data**

| Field | Value |
|---|---|
| First names | Testadult, Testdependent, Testnondependent |
| Last name | Example |
| Title | Mx. |
| Email | test.adult@example.com |
| Mobile | 0210000000 |
| Address | Type `1 Queen Street`, pick "1 Queen Street, Auckland CBD, Auckland, New Zealand" (a public CBD address). The site fills suburb "Auckland CBD", city "Auckland", postcode "1010". |

## 3. Page map

The step definitions call one page object per step of the site: `QuoteFormPage` (home page quote form),
`PlanOptionsPage` (plans and customising) and `TravellerDetailsPage` (eligibility and traveller forms). Keep locators as private `ILocator` properties and
expose actions plus the few locators that steps assert on with `Expect`. `BasePage` holds shared helpers: navigation
from `BaseUrl`, waiting for an API response around an action, and parsing `$1,234.56` prices.

Locator notes that apply throughout:
- Several dialogs each have a "Done" button, so always scope to the open dialog.
- Radio inputs are hidden. Click the label text inside the named radiogroup, then assert the input by id with
  `ToBeCheckedAsync()`.
- Where the accessible name repeats (traveller fields, age fields, "Increment"), the site's stable ids are used
  instead. They are form-model names, not layout.

### 3.1 Home quote form (`/`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Trip purpose "Overseas travel" | `Page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Overseas travel" })` | `OverseasTravelButton` | Default purpose. All five purpose buttons have the accessible name "button", so filter by text. |
| One return journey tab | `Page.GetByRole(AriaRole.Tab, new() { Name = "One return journey" })` | `SelectOneReturnJourney()` | Selected by default. Assert `ToHaveAttributeAsync("aria-selected", "true")`. |
| Destination field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter destination(s)" })` | `FillDestination()` | Click opens the destination dialog. After Done the value is the selection in click order, e.g. `Japan, Australia`. |
| Travel dates field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Select travel dates" })` | `FillTravelDates()` | Click opens the date picker. Value `09/12/2026 - 23/12/2026`. |
| Travellers field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Add travellers" })` | `FillTravellers()` | Click opens the travellers dialog. Value `3 Travellers`. |
| Get a quote | `Page.GetByRole(AriaRole.Button, new() { Name = "Get a quote" })` | `ClickGetPlanOptions()` | With a complete form it sends POST `/api/Quotes/create` and opens `step=plan-options`. |
| Form error | `Page.GetByText("All fields are required.")` | `QuoteFormError` | Shown when any field is empty; stays visible until the next submit. |

### 3.2 Destination dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Select destinations" })` | `DestinationDialog` | Heading "Enter destination". |
| Search box | `DestinationDialog.GetByRole(AriaRole.Textbox, new() { Name = "Destination(s) *" })` | `FillDestination()` | Type with `PressSequentiallyAsync`; matches appear under a "Search results" heading as you type. |
| Result or popular destination | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = destination, Exact = true })` | `FillDestination()` | Wait for the "Search results" heading before clicking. The popular list uses the same role and name. |
| Clear search | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = "clear-input-button" })` | `FillDestination()` | Needed between destinations; the popular list comes back and selected chips stay. |
| Selected chip | text plus an "x" button named "Close" | - | The chip remove buttons share the name "Close" with the dialog's close button, so assert the field value instead. |
| Done | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `FillDestination()` | Closes the dialog and writes the field value. |

### 3.3 Travel dates dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Date picker" })` | `TravelDatesDialog` | Heading "Select travel dates". |
| Departure date | `TravelDatesDialog.GetByRole(AriaRole.Textbox, new() { Name = "Departure date *" })` | `FillTravelDates()` | `FillAsync("dd/MM/yyyy")`. Typing is more stable than clicking calendar cells. |
| Return date | `TravelDatesDialog.GetByRole(AriaRole.Textbox, new() { Name = "Return date *" })` | `FillTravelDates()`, `ChangeReturnDateAsync()` | A return date less than 2 days after departure shows "Return date must be at least 2 days after the Departure date" and Done keeps the dialog open (1 day after is accepted). |
| Calendar day (fallback) | `TravelDatesDialog.GetByRole(AriaRole.Gridcell, new() { Name = "Choose Wednesday, December 9th, 2026" })` | - | Two months are shown, with "Next Month" and "Previous Month" buttons. Past days are disabled. |
| Done | `TravelDatesDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `FillTravelDates()` | Writes the travel dates field. |

### 3.4 Travellers dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Add number of travellers" })` | `TravellersDialog` | Heading "Add traveller(s)". |
| Increment (adult, dependent, non-dependent) | `TravellersDialog.GetByRole(AriaRole.Button, new() { Name = "Increment" }).Nth(0)` (1 = dependent, 2 = non-dependent) | `FillTravellers()` | The three buttons have the same name, so the index is the only handle. After each click assert the count: `Expect(Page.Locator("#Adult")).ToHaveValueAsync("1")` (`#DependentChild`, `#NonDependentChild`). Decrement is disabled at 0. |
| Age inputs | `Page.Locator("#Adult-age-0")`, `Page.Locator("#DependentChild-age-0")`, `Page.Locator("#NonDependentChild-age-0")` | `FillTravellers()` | Appear only after incrementing; the suffix is the nth traveller of that type. Both child age fields are labelled "Child 1 age *", so use the ids. HTML min/max match section 2. |
| Age errors | `TravellersDialog.GetByText("Adult traveller must be aged between 18 and 118")` (and the two child messages) | `TravellersDialog` | Shown on Done; the dialog stays open. |
| Done | `TravellersDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `FillTravellers()` | Don't use `#add-travellers-overlay-done`: the id is on both the button and its inner span (strict-mode violation). |
| Children travelling alone | `TravellersDialog.GetByRole(AriaRole.Checkbox, new() { Name = "Non-dependent children travelling without adults" })` | - | Not used in this plan. |
| Tooltips | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Tooltip" })` | - | Source of the type definitions in section 2. |

### 3.5 Plan options (`/our-policies/comprehensive?step=plan-options`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Page loaded | `Expect(Page).ToHaveURLAsync(new Regex("step=plan-options"))` | - | After POST `/api/Quotes/create`. Page title starts "Comprehensive insurance". |
| Trip summary fields | Same three textboxes as 3.1 | - | Destinations are re-sorted alphabetically here: `Australia, Japan`. |
| Update plan options | `Page.GetByRole(AriaRole.Button, new() { Name = "Update plan options" })` | - | Re-quotes after the trip summary changes. Not needed in this plan. |
| Back | `Page.GetByRole(AriaRole.Button, new() { Name = "Back" })` | - | Not used. |
| Plan names | `Page.GetByText("International Comprehensive Single Trip")`, `Page.GetByText("International Medical Only")` | `PlanOptions` | Card labels "One return journey" and "Medical only". |
| Plan price | `Page.Locator("div").Filter(new() { HasText = "International Comprehensive Single Trip" }).GetByText(new Regex(@"^\$[\d,]+\.\d{2}$")).First` | `GetPlanPriceAsync()` | e.g. `$279.03` before customising. No id; use the price pattern. |
| Select and customise plan | `Page.Locator("button#ST")` | `SelectPlan()` | The id is the product code (`ST` single trip, `MO` medical only), the same `ProductCode` the API sends. By role and name, two buttons match. |

### 3.6 Customise plan (same URL, opens after Select)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Section | `Page.GetByText("Changes below only apply to the selected plan and may affect your total premium.")` | `SelectPlan()` | Wait for this after clicking Select. A sticky summary with the chosen plan and its price also appears at the top. |
| Discount Yes or No | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "schsDiscount" }).GetByText("Yes", new() { Exact = true })` | `CustomisePlan()` | Question "Are you a Southern Cross Health Society member? *". State: `Expect(Page.Locator("#schsDiscount-yes")).ToBeCheckedAsync()`. No default. |
| Discount amount | `Page.GetByText(new Regex(@"^-\$[\d,]+\.\d{2}$"))` | `GetDiscountAsync()` | `$0.00` before; `-$13.95` on a $279.03 base (5%). |
| Excess | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "excess" }).GetByText("$100", new() { Exact = true })` | `CustomisePlan()` | Input ids `#excess-0`, `#excess-100`, `#excess-200`. |
| Cancellation cover | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "cancellationCover" }).GetByText("$5,000", new() { Exact = true })` | `CustomisePlan()` | Input ids `#cancellationCover-2500`, `-5000`, `-10000`, `-Unlimited`. |
| Extra cover | `Page.GetByRole(AriaRole.Checkbox, new() { Name = "Skiing or snowboarding" })` (also "Moped or motorbike", "Cruise") | `CustomisePlan()` | `CheckAsync()`. The ids contain spaces (`Skiing or snowboarding ICNZ`), so don't select by id. |
| Premium change rows | `Page.GetByText("Premium change")`, value next to each | `GetPremiumChangesAsync()` | One row each for excess, cancellation cover and extra cover. Updated after each PUT `/api/Quotes/extras`. |
| Next: Traveller details | `Page.GetByRole(AriaRole.Button, new() { Name = "Next: Traveller details" })` | `ClickNextToTravellerDetails()` | Sends PUT `/api/Quotes/select`, then opens `step=traveller-details`. |

### 3.7 Traveller details: eligibility and declaration (`?step=traveller-details`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Page loaded | `Expect(Page).ToHaveURLAsync(new Regex("step=traveller-details"))` | - | Heading text "Traveller details". |
| Q1 citizens or residents, eligible for public health, returning to NZ | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "CitizenOrLivingInNZ" }).GetByText("Yes", new() { Exact = true })` | `CompleteTravellerEligibility()` | State `Page.Locator("[id='CitizenOrLivingInNZ-yes']")`. "No" shows the ineligibility message. |
| Q2 already left NZ | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "JourneyStarted NZ" }).GetByText("No", new() { Exact = true })` | `CompleteTravellerEligibility()` | The id contains a space: `Page.Locator("[id='JourneyStarted NZ-no']")`. |
| Q3 cover refused, claim declined or policy voided for fraud | `Page.GetByRole(AriaRole.Radiogroup, new() { Name = "Fraud NZ" }).GetByText("No", new() { Exact = true })` | `CompleteTravellerEligibility()` | State `Page.Locator("[id='Fraud NZ-no']")`. |
| Ineligibility message | `Page.GetByText("Sorry, you don't appear to meet our minimum eligibility criteria for this type of travel insurance.")` | - | Shown as soon as Q1 is "No". |
| Declaration 1 | `Page.GetByRole(AriaRole.Checkbox, new() { Name = "I confirm that I have read, understood, and agree" })` | `CompleteTravellerEligibility()` | Id `#confirm-understood`. |
| Declaration 2 | `Page.GetByRole(AriaRole.Checkbox, new() { Name = "I authorise SCTI to collect, use, and share personal information" })` | `CompleteTravellerEligibility()` | Id `#authorise-collect-information`. |
| Required message | `Page.GetByText("This field is required")` | - | Three for empty eligibility answers. |
| Declaration message | `Page.GetByText("You must agree to the declaration to continue.")` | - | Two when both boxes are unticked. |
| Next: High-value items | `Page.GetByRole(AriaRole.Button, new() { Name = "Next: High-value items" })` | `TryContinueToHighValueItems()` | Validation only. Never click it with a complete form (out of scope). |
| Back: Plan options | `Page.GetByRole(AriaRole.Button, new() { Name = "Back: Plan options" })` | - | Not used. |
| Total premium | `Page.GetByText("Total premium")` and the value next to it | `GetTotalPremiumAsync()` | Equals the customised plan price. |
| Progress steps | Travel info, Plan options, Traveller details, High-value items, Medical, Summary | - | Shown beside the form. |

### 3.8 Travellers form (same page)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Traveller headings | `Page.GetByText("Main policyholder - Adult 1, aged 40")`, `Page.GetByText("Dependent child 1, aged 10")`, `Page.GetByText("Non-dependent child 1, aged 12")` | `AddTraveller()` | The section was not on the page before eligibility and declarations were completed, and was there after. Wait for the policyholder heading. Text is built from the quote ages. |
| Traveller fields | `Page.Locator("[id='travellers[0].firstName']")`, pattern `travellers[{index}].{field}` with fields `title`, `firstName`, `lastName`, `dob`, `mobilePhone`, `emailAddress`, `streetAddress`, `suburb`, `city`, `postcode` | `AddTraveller()` | Labels such as "First name *" repeat for every traveller, so role and label locators are ambiguous. Index 0 is the policyholder, then travellers in quote order (dependent 1, non-dependent 2). Children only have `firstName`, `lastName`, `dob`. |
| Title | Click `Page.Locator("[id='travellers[0].title']")`, then `Page.GetByRole(AriaRole.Button, new() { Name = "Mx.", Exact = true })` | `AddTraveller()` | Custom dropdown; the options are buttons. |
| Date of birth | `Page.Locator("[id='travellers[1].dob']")`, `FillAsync("dd/MM/yyyy")`, then press Tab | `AddTraveller()` | Focus opens a calendar; Tab closes it. A wrong age shows the mismatch message below. |
| Date of birth mismatch | `Page.GetByText("Entered age and date of birth do not match.")`, buttons `Page.GetByRole(AriaRole.Button, new() { Name = "Re-enter" })` and `Page.GetByRole(AriaRole.Button, new() { Name = "Update quote" })` | `ChooseDobPromptOptionAsync()` | Prompt "Date of birth changed". "Re-enter" keeps the price and reopens the calendar. "Update quote" re-prices (not used). |
| Mobile phone | `Page.Locator("[id='travellers[0].mobilePhone']")` | `FillPolicyholderContactAsync()` | Number input. |
| Email | `Page.Locator("[id='travellers[0].emailAddress']")` | `FillPolicyholderContactAsync()` | |
| Street address | `Page.Locator("[id='travellers[0].streetAddress']")`, `PressSequentiallyAsync("1 Queen Street")`, then `Page.GetByRole(AriaRole.Button, new() { Name = "1 Queen Street, Auckland CBD, Auckland, New Zealand" })` | `FillPolicyholderContactAsync()` | Suggestions come from Google Places. Choosing one fills suburb, city and postcode; assert `Expect(Page.Locator("[id='travellers[0].postcode']")).ToHaveValueAsync("1010")`. |
| Traveller type | Radio ids `travellers[{index}].type.Adult`, `.DependentChild`, `.NonDependentChild`; click the label text, e.g. "Dependent child (20 and under)", inside that traveller's block | `SetTravellerTypeAsync()` | Pre-selected from the quote. A change re-renders the block a moment later: heading and fields change (adults have title, phone, email and an address choice; children only names and date of birth). |
| Add traveller | `Page.GetByRole(AriaRole.Button, new() { Name = "Add traveller" })` | `AddAnotherTravellerAsync()` | Adds a block headed "Adult N" with the address choice "Same as main policyholder" or "Search for an address". |
| Remove traveller | `Page.GetByText("Remove", new() { Exact = true }).Nth(index)` | `RemoveTravellerAsync()` | A link-like element without a role. Removes at once, no confirmation; premium unchanged. |

## 4. Network calls

Record every request per scenario (method, URL, status) with `Page.Request`, `Page.Response` and
`Page.RequestFailed`, write it to the Reqnroll output, and assert only first-party `/api/` calls. Wrap triggering
actions in `Page.RunAndWaitForResponseAsync(...)` and read request bodies with `IRequest.PostDataJSON()`. Response
bodies were not visible during exploration, so assert the status and the request body.

| Trigger | Method | URL pattern | Expected status | Assert? |
|---|---|---|---|---|
| Load `BaseUrl` | GET | `/api/config` | 200 | Yes: the app has loaded |
| Get a quote with a complete form | POST | `/api/Quotes/create` | 200 | Yes: body (below) matches destinations, dates and ages |
| Get a quote with an empty form | - | no `/api/Quotes/create` | - | Yes: no request is sent |
| Each discount, excess, cancellation cover or extra cover change | PUT | `/api/Quotes/extras` | 200 | Yes: one call per change; the last body has the final choices |
| Next: Traveller details | PUT | `/api/Quotes/select` | 200 | Yes: body repeats the choices |
| Filling the traveller form | - | no first-party call | - | Yes: no traveller data leaves the browser |
| Typing the street address | GET | `maps.googleapis.com/...` | 200 | No: third party |
| Analytics, ads and webchat (Google Analytics, DoubleClick, Taboola, Facebook, pxf.io, ambithub, `/cdn-cgi/rum`) | various | - | - | No: log only; optionally block with `Page.RouteAsync` to cut noise |

Request bodies seen during exploration:

```json
// POST /api/Quotes/create
{"ProductCode":"ST","BaseCountry":"NZL","StartDateOfJourney":"2026-12-09T00:00:00.000Z",
 "StartDateOfInsurance":"2026-11-09T00:00:00.000Z","EndDate":"2026-12-23T00:00:00.000Z",
 "Destinations":["JPN","AUS"],"AdultsAge":[40],"NonDependentChildAges":[12],"DependentChildAges":[10],"ChildAges":[]}

// PUT /api/Quotes/extras (after the last change)
{"QuoteId":"6af1463c8d3e90aa392e257b","PolicyNumber":"SCTI2620582656A/00/01","Excess":"100",
 "CancellationCover":"5000","ProductCode":"ST","SchsDiscount":true,"MopedCover":false,"CruiseCover":false,"SkiCover":true}

// PUT /api/Quotes/select
{"ProductCode":"ST","QuoteId":"6af1463c8d3e90aa392e257b","PolicyNumber":"SCTI2620582656A/00/01","Excess":"100",
 "CancellationCover":"5000","IsSchsMember":true,"MopedCover":false,"CruiseCover":false,"SkiCover":true}
```

The same first-party calls (config, create, extras, select) are the transactions to record for the README's
non-functional workload model.

## 5. Scenarios

Two feature files, so NUnit can run them in parallel (Reqnroll makes each feature an NUnit fixture).

```gherkin
# tests/Features/InsuranceBooking.feature
@UI
Feature: Insurance Booking Journey
  As a New Zealand resident travelling overseas
  I want to quote and customise a one return journey policy
  So that I can enter my travellers' details before buying

  Scenario Outline: Quote, customise and add 1 adult, 1 dependent and 1 non-dependent traveller
    Given I navigate to the insurance portal
    When I select "One return journey" journey
    And I add destination "<destinations>"
    And I add travel dates "<departure>" and "<return>"
    And I add "Adult:<adult age>, DependentChild:<dependent age>, NonDependentChild:<non-dependent age>" travellers
    And I click Get plan options
    Then I should see plan options available
    And the quote request should contain destinations "<destination codes>" and ages "<adult age>, <dependent age>, <non-dependent age>"
    When I select a plan and customise it
    And I choose discount "<discount>", excess "<excess>", cancellation cover "<cancellation cover>", and extra cover "<extra cover>"
    Then the plan price should equal the base price plus the premium changes
    And the latest plan update request should contain discount "<discount>", excess "<excess>", cancellation cover "<cancellation cover>" and extra cover "<extra cover>"
    When I click Next to Traveller details
    Then I should see traveller eligibility section
    When I complete traveller eligibility and privacy declaration
    And I add adult traveller with firstname "Testadult", lastname "Example", and DOB "age:<adult age>"
    And I enter contact details for the main policyholder with email "test.adult@example.com", mobile "0210000000" and address "1 Queen Street, Auckland CBD, Auckland, New Zealand"
    And I add dependent traveller with firstname "Testdependent", lastname "Example", and DOB "age:<dependent age>"
    And I add non-dependent traveller with firstname "Testnondependent", lastname "Example", and DOB "age:<non-dependent age>"
    Then I should see all travellers added successfully

    Examples:
      | destinations     | destination codes | departure | return   | adult age | dependent age | non-dependent age | discount | excess | cancellation cover | extra cover            |
      | Japan, Australia | JPN, AUS          | today+30  | today+44 | 40        | 10            | 12                | Yes      | $100   | $5,000             | Skiing or snowboarding |
      | Australia        | AUS               | today+60  | today+70 | 65        | 20            | 17                | No       | $0     | Unlimited          | Cruise                 |
```

```gherkin
# tests/Features/InsuranceBookingValidation.feature
@UI
Feature: Insurance Booking Journey - validation
  Form rules found on the live site. No scenario goes past the Traveller details step.

  Background:
    Given I navigate to the insurance portal

  Scenario: V1 The quote form requires every field
    When I click Get plan options
    Then I should see the quote form error "All fields are required."
    And no quote should be requested

  Scenario Outline: V2 Traveller ages outside the allowed range are rejected
    When I try to add "<traveller>" travellers
    Then I should see the traveller error "<message>"
    And the travellers selector should stay open

    Examples:
      | traveller            | message                                          |
      | Adult:17             | Adult traveller must be aged between 18 and 118  |
      | Adult:119            | Adult traveller must be aged between 18 and 118  |
      | DependentChild:21    | Dependent traveller must be aged 20 or under     |
      | NonDependentChild:18 | Non-dependent traveller must be aged 17 or under |

  Scenario Outline: V3 The return date must be at least 2 days after the departure date
    When I add travel dates "today+30" and "today+44"
    Then the travel dates should be "today+30" to "today+44"
    When I change the return date to "<return>"
    Then I should see the travel dates error "Return date must be at least 2 days after the Departure date"
    And the travel dates selector should stay open

    Examples:
      | return   |
      | today+20 |
      | today+30 |

  Scenario: V4 Traveller details enforce eligibility, the declarations and a matching date of birth
    Given I have a quote for "Japan" from "today+30" to "today+44" for "Adult:40, DependentChild:10, NonDependentChild:12"
    When I select a plan and customise it
    And I choose discount "No", excess "$200", cancellation cover "$2,500", and extra cover "None"
    And I click Next to Traveller details
    And I continue without answering the eligibility questions or declarations
    Then I should see 3 "This field is required" messages
    And I should see 2 "You must agree to the declaration to continue." messages
    And I should still be on the traveller details step
    When I answer "No" to whether all travellers are New Zealand residents
    Then I should see the eligibility message "Sorry, you don't appear to meet our minimum eligibility criteria for this type of travel insurance."
    When I complete traveller eligibility and privacy declaration
    And I enter date of birth "age:30" for the main policyholder
    Then I should see the date of birth error "Entered age and date of birth do not match."
    And I should be offered "Re-enter" or "Update quote"
    When I choose "Re-enter"
    Then the total premium should be unchanged

  Scenario: V5 A traveller added on Traveller details can change type and be removed
    Given I have a quote for "Australia" from "today+30" to "today+44" for "Adult:40, DependentChild:10, NonDependentChild:12"
    When I select a plan and customise it
    And I choose discount "No", excess "$200", cancellation cover "$2,500", and extra cover "None"
    And I click Next to Traveller details
    And I complete traveller eligibility and privacy declaration
    And I add another traveller
    Then I should see a traveller form headed "Adult 2"
    When I change the new traveller's type to "Dependent child (20 and under)"
    Then I should see a traveller form headed "Dependent child 2"
    And the "Dependent child 2" form should only ask for first name, last name and date of birth
    When I remove the new traveller
    Then I should see 3 traveller forms
    And the total premium should be unchanged
```

**Expected results**

Happy path, each row:
- The One return journey tab has `aria-selected="true"`. The destination field shows the destinations in click order,
  the travel dates field shows `dd/MM/yyyy - dd/MM/yyyy` for the resolved dates, and the travellers field shows
  `3 Travellers`.
- "Get a quote" sends POST `/api/Quotes/create` (200) whose body has `ProductCode` "ST", `BaseCountry` "NZL",
  `Destinations` equal to the codes column, `StartDateOfJourney` and `EndDate` equal to the travel dates,
  `StartDateOfInsurance` equal to today, and the three age lists equal to the ages columns. The page moves to
  `step=plan-options`, both plan names are visible, and `button#ST` is visible.
- After Select, the customise section is visible. Each of the four choices sends one PUT `/api/Quotes/extras` (200).
  The last body has `SchsDiscount` true for Yes or false for No, `Excess` "100" or "0", `CancellationCover` "5000" or
  "Unlimited", and only the chosen cover flag true (`SkiCover` or `CruiseCover`).
- The plan price equals the base price, plus the discount (negative, about 5% of base when Yes, $0.00 when No), plus
  the three premium changes. Example from exploration: 279.03 - 13.95 + 34.49 + 33.97 + 116.00 = $449.54.
- "Next: Traveller details" sends PUT `/api/Quotes/select` (200) and opens `step=traveller-details`. The three
  eligibility radiogroups are visible.
- After eligibility and declarations, the forms are headed "Main policyholder - Adult 1, aged <adult age>",
  "Dependent child 1, aged <dependent age>" and "Non-dependent child 1, aged <non-dependent age>". All fields hold the
  values entered; the postcode is `1010`. No "This field is required" or "do not match" message is shown.
- The total premium equals the customised plan price. No first-party request was made after `/api/Quotes/select`.
  The page is still `step=traveller-details`.

Validation:
- V1: "All fields are required." is visible, no POST `/api/Quotes/create` was captured, and the URL is still `/`.
- V2: the matching message is shown under the age field, and the "Add number of travellers" dialog stays open after
  Done. Ages 17, 21 and 18 were tried during exploration; the `Adult:119` row follows from the 18-to-118 range in the
  message.
- V3: the error "Return date must be at least 2 days after the Departure date" shows and the date picker stays open.
- V4: three "This field is required" and two "You must agree to the declaration to continue." messages show, and the
  URL stays `step=traveller-details`. Q1 "No" shows the ineligibility message. A date of birth for age 30 against a
  quoted 40 shows "Entered age and date of birth do not match." with "Re-enter" and "Update quote". After "Re-enter"
  the total premium equals the value recorded when the page opened.
- V5: "Add traveller" adds a block headed "Adult 2" with title, name, date of birth, mobile, email and address
  choice fields. Choosing "Dependent child (20 and under)" changes the heading to "Dependent child 2" with only first
  name, last name and date of birth. "Remove" brings the page back to three traveller forms, and the total premium is
  unchanged.

## 6. Step mapping

Existing bindings are in `tests/StepDefinitions/InsuranceBookingSteps.cs`; page methods are on `QuoteFormPage`, `PlanOptionsPage` and `TravellerDetailsPage`.

| Step text | Existing binding or NEW | Page-object method |
|---|---|---|
| `I navigate to the insurance portal` | Existing | `NavigateAsync()`: go to `BaseUrl` from config, wait for GET `/api/config` |
| `I select "(.*)" journey` | Existing (the binding ignores its argument; pass it through) | `SelectOneReturnJourney()` |
| `I add destination "(.*)"` | Existing (accept a comma-separated list) | `FillDestination(string)` |
| `I add travel dates "(.*)" and "(.*)"` | Existing (resolve `today+N`) | `FillTravelDates(string, string)` |
| `I add "(.*)" travellers` | Existing (argument is now a `Type:age` list) | `FillTravellers(string)` |
| `I click Get plan options` | Existing | `ClickGetPlanOptions()`: clicks "Get a quote" only |
| `I should see plan options available` | Existing (replace the `button:has-text('Select')` check with `Expect` on the URL, plan names and `#ST`; record the base price) | `PlanOptions`, `GetPlanPriceAsync()` |
| `the quote request should contain destinations "(.*)" and ages "(.*)"` | NEW | none: network recorder |
| `I select a plan and customise it` | Existing | `SelectPlan()` |
| `I choose discount "(.*)", excess "(.*)", cancellation cover "(.*)", and extra cover "(.*)"` | Existing (pass the raw values instead of converting to Yes/No booleans) | `CustomisePlan(string, string, string, string)` |
| `the plan price should equal the base price plus the premium changes` | NEW | `GetPlanPriceAsync()`, `GetDiscountAsync()`, `GetPremiumChangesAsync()` |
| `the latest plan update request should contain discount "(.*)", excess "(.*)", cancellation cover "(.*)" and extra cover "(.*)"` | NEW | none: network recorder |
| `I click Next to Traveller details` | Existing (currently does nothing) | `ClickNextToTravellerDetails()` (NEW method); records the total premium |
| `I should see traveller eligibility section` | Existing (replace `input[name='eligibility']` with the `CitizenOrLivingInNZ` radiogroup and the URL) | `EligibilitySection` |
| `I complete traveller eligibility and privacy declaration` | Existing | `CompleteTravellerEligibility()` |
| `I add adult traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Existing (resolve `age:N`) | `AddTraveller("Adult", ...)` |
| `I enter contact details for the main policyholder with email "(.*)", mobile "(.*)" and address "(.*)"` | NEW | `FillPolicyholderContactAsync(string, string, string)` |
| `I add dependent traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Existing | `AddTraveller("Dependent", ...)` |
| `I add non-dependent traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Existing | `AddTraveller("Non-Dependent", ...)` |
| `I should see all travellers added successfully` | Existing (replace the "network count > 0" check with the expected results in section 5) | `TravellerHeadings`, `GetTotalPremiumAsync()` |
| `I should see the quote form error "(.*)"` | NEW | `QuoteFormError` |
| `no quote should be requested` | NEW | none: network recorder |
| `I try to add "(.*)" travellers` | NEW | `FillTravellers(string)` without waiting for the dialog to close |
| `I should see the traveller error "(.*)"` | NEW | `TravellersDialog` |
| `the travellers selector should stay open` | NEW | `TravellersDialog` |
| `I change the return date to "(.*)"` | NEW | `ChangeReturnDateAsync(string)` |
| `the travel dates should be "(.*)" to "(.*)"` | NEW | `TravelDatesValue` |
| `I have a quote for "(.*)" from "(.*)" to "(.*)" for "(.*)"` | NEW (composes the existing quote steps) | `SelectOneReturnJourney()`, `FillDestination()`, `FillTravelDates()`, `FillTravellers()`, `ClickGetPlanOptions()` |
| `I continue without answering the eligibility questions or declarations` | NEW | `TryContinueToHighValueItems()` |
| `I should see (\d+) "(.*)" messages` | NEW | none: `Page.GetByText(text)` count |
| `I should still be on the traveller details step` | NEW | none: URL check |
| `I answer "(.*)" to whether all travellers are New Zealand residents` | NEW | `AnswerResidencyQuestionAsync(string)` |
| `I should see the eligibility message "(.*)"` | NEW | none: `Page.GetByText(text)` |
| `I enter date of birth "(.*)" for the main policyholder` | NEW | `SetTravellerDobAsync(0, string)` |
| `I should see the date of birth error "(.*)"` | NEW | none: `Page.GetByText(text)` |
| `I should be offered "(.*)" or "(.*)"` | NEW | `DobPromptButton(string)` |
| `I choose "(.*)"` | NEW | `ChooseDobPromptOptionAsync(string)` |
| `the total premium should be unchanged` | NEW | `GetTotalPremiumAsync()` |
| `I add another traveller` | NEW | `AddAnotherTravellerAsync()` |
| `I should see a traveller form headed "(.*)"` | NEW | `TravellerHeading(string)` |
| `I change the new traveller's type to "(.*)"` | NEW | `SetTravellerTypeAsync(int, string)` |
| `the "(.*)" form should only ask for first name, last name and date of birth` | NEW | `TravellerFields(int)` |
| `I remove the new traveller` | NEW | `RemoveTravellerAsync(int)` |
| `I should see (\d+) traveller forms` | NEW | `TravellerHeadings` count |

## 7. Risks

**Flaky areas and how to wait for them**
- Price recalculation is asynchronous. Wrap each option click in `RunAndWaitForResponseAsync` for PUT
  `/api/Quotes/extras`, then assert the new price with `Expect(...).ToHaveTextAsync(...)`. Never read a price straight
  after a click.
- Quick successive radio clicks can be lost: one of three fast label clicks did not register during exploration.
  After each radio click, assert the hidden input with `ToBeCheckedAsync()` before the next one.
- Sections appear after a choice: the customise section, the age inputs, the travellers form, and a re-rendered
  traveller block after a type change (an immediate DOM read still showed the old type). Wait on the heading or text
  with `Expect`, not on a fixed delay.
- Destination search and the address field return results as you type. Use `PressSequentiallyAsync` and wait for the
  result button. Address suggestions come from Google Places, which can be slow or rate-limited, so give that
  assertion a longer timeout.
- Prices change with rates and promotions. Assert the price format and the sum, never a fixed amount.
- Banners, such as the dismissible "Important information regarding cover for travel affected by Middle East conflict",
  can change. Don't depend on them.

**Locator risks**
- Repeated names: "Done" in three dialogs (scope to the dialog), "Increment" three times (index plus a value
  assertion), "Child 1 age *" twice and per-traveller labels (use ids), "Select and customise plan" twice (`#ST`),
  "Yes" and "No" (scope to the radiogroup), "Remove" (index).
- Some ids contain spaces (`JourneyStarted NZ-no`, `Skiing or snowboarding ICNZ`). Use `[id='...']` or role
  locators.

**Environment and data**
- Production only. Each scenario that gets a quote creates a draft quote with a policy number. Keep the suite small,
  don't run it in loops, and limit parallel workers. Dev and QA URLs for `appsettings.Dev.json` and
  `appsettings.QA.json` are unknown; ask for a test environment before enabling them.
- Compute "today" in `Pacific/Auckland` and dates of birth from today, or a UTC CI agent can be a day out and break
  the age match.
- Parallel runs: enable NUnit fixture-level parallelism (`[assembly: Parallelizable(ParallelScope.Fixtures)]`) and
  give each scenario its own browser context. Scenario Outline rows inside one feature run one after another.
- Third-party analytics, ads and webchat create most of the traffic. Log it, but filter assertions to first-party
  `/api/`. Blocking it with `Page.RouteAsync` makes runs faster and quieter.

**Observations from exploration (possible site defects to raise)**
- The "frequently asked questions" link in the destination dialog points to
  `https://https://scti.co.nz/about/understanding-travel-warnings` (scheme repeated).
- The id `add-travellers-overlay-done` is used on both the Done button and its inner span.
- The five trip purpose buttons all have the accessible name "button"; the clear-search button is named
  "clear-input-button"; destination chip remove buttons are named "Close", like the dialog's close button.
- A mobile number of `123` produced no message when "Next: High-value items" was clicked, while other required fields
  did. Confirm whether the phone number is validated later.
- The API names the discount flag `SchsDiscount` in `/api/Quotes/extras` but `IsSchsMember` in `/api/Quotes/select`.
- The README labels differ from the site: "Get plan options" is "Get a quote" (and "Update plan options" on the plan
  page), and "Select" is "Select and customise plan".
