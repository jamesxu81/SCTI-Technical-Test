# Insurance booking UI test plan

Source of truth: `README.md`, section "1. UI Test Automation (Playwright + Reqnroll)" and its "Automate this Scenario
Flow". Everything below was observed on the live site with Playwright on 2026-11-09 (Chrome, fresh profile). Prices
quoted are examples from that session and will change.

**Implementation status (2026-11-10):** implemented in `tests/Features/*.feature`, `tests/StepDefinitions/` and
`src/Ui/Pages/`. 11 UI tests (2 happy-path rows and 9 validation tests). Chromium 11/11 and WebKit 11/11 pass;
Firefox 10/11 (see section 7, "Known issues").

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
draft quote in production (four per full run, more if a scenario is retried).

**Requirements coverage**

| README requirement | Where this plan covers it |
|---|---|
| Feature files with Scenario Outline and Examples | Happy-path Scenario Outline (2 rows), the age-limit Scenario Outline (4 rows) and the return-date Scenario Outline (2 rows), section 5 |
| Page Object Model | Section 3: `BasePage` for shared helpers, one page object per step (`QuoteFormPage`, `PlanOptionsPage`, `TravellerDetailsPage`). Locators are defined once in the page object; only those the steps assert on are public |
| Network call capture | Section 4: every response and failed request logged per scenario, with request and response bodies for first-party `/api/` calls; those calls asserted by status and request body |
| Dynamic element handling with waits | "Wait / notes" column in section 3, and section 7 |
| Form filling and validation | Happy path fills every form; validation scenarios V1 to V5 use the exact messages found on the site |
| Multi-environment configuration | `BaseUrl`, `Timeout`, `Headless`, `Browser` read through `ConfigurationHelper`: `appsettings.json`, then `appsettings.{TEST_ENV}.json` (Dev, QA, Prod), then `TEST_*` environment variables; no URLs in feature files |
| Cross-browser | `Browser` setting: `chromium` (default), `firefox` or `webkit` |
| Parallel execution support | Every scenario and Examples row may run in parallel (`[assembly: Parallelizable(ParallelScope.Children)]`, `FixtureLifeCycle.InstancePerTestCase`); each scenario launches its own browser and context; no shared state |
| Comprehensive assertions | Text, attributes (`aria-selected`, checked state, input values), element counts, URL, network bodies, premium arithmetic |
| Error handling and detailed logging | Full-page screenshot on failure; network log for every scenario; both attached to the Reqnroll output and saved to `TestResults/ui/` in the test output folder. First-party API calls with bodies are also written to the test output. TRX or HTML via `dotnet test --logger`. Failed scenarios are retried (2 attempts in total, `NRetrySettings` in `reqnroll.json`) |
| Test data management | Examples tables, relative dates (`today+30`) and age-based dates of birth (`age:40`) resolved by step argument transformations, section 2 |
| Technologies | .NET 8, Microsoft.Playwright 1.64.1, Reqnroll 2.4.1 with NUnit 4.0.1, NUnitRetry.ReqnrollPlugin 2.0.4, FluentAssertions for non-UI values (network bodies, price sums), Microsoft.Extensions.Configuration. Moq and JSONPlaceholder are not used by the UI tests. |
| Clean code, SOLID, DRY | `TestData` and `StepArgumentTransformations` for dates, ages and traveller lists; `UiFormat` for dates and money; one `NetworkRecorder` and the page objects injected through Reqnroll context injection; `QuoteState` shares values between steps; API paths in `QuoteApi` |

## 2. Test data rules

**Dates**
- Use tokens `today+N`, resolved in New Zealand time (`Pacific/Auckland`) and typed as `dd/MM/yyyy`. A CI agent on
  UTC can otherwise be a day behind the site. The browser context also runs with `TimezoneId = "Pacific/Auckland"`
  and `Locale = "en-NZ"`, because the site works out quote dates and ages from the browser clock.
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

- Traveller lists in steps use `Type:age` pairs, e.g. `Adult:40, DependentChild:10, NonDependentChild:12`
  (types are the `TravellerType` enum names). A child-only list shows a different rule, so the child age-limit rows
  also include `Adult:40`.
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
| Title | Mx. (not entered by the tests) |
| Email | test.adult@example.com |
| Mobile | 0210000000 |
| Address | Type `1 Queen Street`, pick "1 Queen Street, Auckland CBD, Auckland, New Zealand" (a public CBD address). The site fills suburb "Auckland CBD", city "Auckland", postcode "1010". |

## 3. Page map

The step definitions call one page object per step of the site: `QuoteFormPage` (home page quote form),
`PlanOptionsPage` (plans and customising) and `TravellerDetailsPage` (eligibility and traveller forms). Locators are
properties defined once in the page object: private unless a step asserts on them with `Expect`. `BasePage` holds
shared helpers: `GotoBaseUrlAsync()`, `RunAndWaitForApiResponseAsync()`, `Message(text)` for any validation message,
`RadioGroup()`, `RadioInput()` and `CheckRadioAsync()`, and the `Amount` regex for prices such as `$1,234.56`.
`UiFormat` formats dates and parses money. The "Page-object member" column names the real members.

Locator notes that apply throughout:
- Several dialogs each have a "Done" button, so always scope to the open dialog.
- Radio inputs are hidden. `CheckRadioAsync()` clicks the label text inside the named radiogroup, then asserts the
  input by id with `ToBeCheckedAsync()`.
- Where the accessible name repeats (traveller fields, age fields, "Increment"), the site's stable ids are used
  instead. They are form-model names, not layout.
- The three quote form fields are clicked near their left edge (`Position X=5, Y=10`): in WebKit the field icon covers
  the rest of the input and intercepts a centre click.

### 3.1 Home quote form (`/`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Trip purpose "Overseas travel" | `Page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Overseas travel" })` | - | Default purpose, not touched by the tests. All five purpose buttons have the accessible name "button". |
| One return journey tab | `Page.GetByRole(AriaRole.Tab, new() { Name = "One return journey" })` | `JourneyTab(name)`, `SelectJourneyAsync(name)` | Selected by default. The step asserts `ToHaveAttributeAsync("aria-selected", "true")`. |
| Destination field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter destination(s)" })` | `DestinationInput`, `FillDestinationsAsync(names)` | Click opens the destination dialog. After Done the value is the selection in click order, e.g. `Japan, Australia`. |
| Travel dates field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Select travel dates" })` | `TravelDatesInput`, `FillTravelDatesAsync(departure, return)` | Click opens the date picker. Value `09/12/2026 - 23/12/2026`. |
| Travellers field | `Page.GetByRole(AriaRole.Textbox, new() { Name = "Add travellers" })` | `TravellersInput`, `FillTravellersAsync(travellers)` | Click opens the travellers dialog. Value `3 Travellers`. |
| Get a quote | `Page.GetByRole(AriaRole.Button, new() { Name = "Get a quote" })` | `ClickGetPlanOptionsAsync()` (private `GetAQuoteButton`) | `NavigateAsync()` waits for this button after loading `BaseUrl`. With a complete form it sends POST `/api/Quotes/create` and opens `step=plan-options`. |
| Form error | `Page.GetByText("All fields are required.", new() { Exact = true })` | `Message(text)` | Shown when any field is empty; stays visible until the next submit. |

### 3.2 Destination dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Select destinations" })` | `DestinationDialog` (private) | Heading "Enter destination". |
| Search box | `DestinationDialog.GetByRole(AriaRole.Textbox, new() { Name = "Destination(s) *" })` | `FillDestinationsAsync()` | Cleared with `ClearAsync()`, then typed with `PressSequentiallyAsync`; matches appear under a "Search results" heading as you type. |
| Result or popular destination | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = destination, Exact = true })` | `FillDestinationsAsync()` | The click auto-waits for the result. The popular list uses the same role and name. |
| Clear search | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = "clear-input-button" })` | - | Not used; `ClearAsync()` on the search box does the same. Selected chips stay. |
| Selected chip | text plus an "x" button named "Close" | - | The chip remove buttons share the name "Close" with the dialog's close button, so the step asserts the field value instead. |
| Done | `DestinationDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `FillDestinationsAsync()` | Closes the dialog and writes the field value; the page object waits for the dialog to be hidden. |

### 3.3 Travel dates dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Date picker" })` | `TravelDatesDialog` | Heading "Select travel dates". |
| Departure date | `TravelDatesDialog.GetByRole(AriaRole.Textbox, new() { Name = "Departure date *" })` | `FillTravelDatesAsync()` | `FillAsync("dd/MM/yyyy")`. Typing is more stable than clicking calendar cells. |
| Return date | `TravelDatesDialog.GetByRole(AriaRole.Textbox, new() { Name = "Return date *" })` | `FillTravelDatesAsync()`, `ChangeReturnDateAsync(date)` | A return date less than 2 days after departure shows "Return date must be at least 2 days after the Departure date" and Done keeps the dialog open (1 day after is accepted). `ChangeReturnDateAsync` clears the field, types the digits with `PressSequentiallyAsync` (the mask adds the slashes), presses Tab, then clicks Done without waiting for the dialog to close: Firefox silently reverts a pasted invalid date. |
| Date error | `TravelDatesDialog.GetByText(text, new() { Exact = true })` | `TravelDatesError(text)` | |
| Calendar day (fallback) | `TravelDatesDialog.GetByRole(AriaRole.Gridcell, new() { Name = "Choose Wednesday, December 9th, 2026" })` | - | Not used. Two months are shown, with "Next Month" and "Previous Month" buttons. Past days are disabled. |
| Done | `TravelDatesDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `FillTravelDatesAsync()` | Writes the travel dates field; the page object waits for the dialog to be hidden. |

### 3.4 Travellers dialog

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Dialog | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Add number of travellers" })` | `TravellersDialog` | Heading "Add traveller(s)". |
| Increment (adult, dependent, non-dependent) | `TravellersDialog.GetByRole(AriaRole.Button, new() { Name = "Increment" }).Nth((int)type)` | `EnterTravellersAsync()` | The three buttons have the same name, so the row index is the only handle; the `TravellerType` enum order (Adult 0, DependentChild 1, NonDependentChild 2) matches the rows. Decrement is disabled at 0. |
| Age inputs | `TravellersDialog.Locator("#Adult-age-0")`, `#DependentChild-age-0`, `#NonDependentChild-age-0` | `EnterTravellersAsync()` | Appear only after incrementing (the fill auto-waits); the suffix is the nth traveller of that type. Both child age fields are labelled "Child 1 age *", so use the ids. HTML min/max match section 2. |
| Age errors | `TravellersDialog.GetByText(text, new() { Exact = true })` | `TravellersError(text)` | Shown on Done; the dialog stays open. |
| Done | `TravellersDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true })` | `EnterTravellersAsync()` clicks it; `FillTravellersAsync()` also waits for the dialog to close | Don't use `#add-travellers-overlay-done`: the id is on both the button and its inner span (strict-mode violation). |
| Children travelling alone | `TravellersDialog.GetByRole(AriaRole.Checkbox, new() { Name = "Non-dependent children travelling without adults" })` | - | Not used. |
| Tooltips | `Page.GetByRole(AriaRole.Dialog, new() { Name = "Tooltip" })` | - | Not used; source of the type definitions in section 2. |

### 3.5 Plan options (`/our-policies/comprehensive?step=plan-options`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Page loaded | `Page.WaitForURLAsync(new Regex("step=plan-options"))`, then `button#ST` | `WaitForLoadAsync()` | After POST `/api/Quotes/create`. The step also asserts the URL with `Expect(Page).ToHaveURLAsync`. Page title starts "Comprehensive insurance". |
| Trip summary fields | Same three textboxes as 3.1 | - | Destinations are re-sorted alphabetically here: `Australia, Japan`. |
| Update plan options | `Page.GetByRole(AriaRole.Button, new() { Name = "Update plan options" })` | - | Not used. Re-quotes after the trip summary changes. |
| Back | `Page.GetByRole(AriaRole.Button, new() { Name = "Back" })` | - | Not used. |
| Plan names | `Page.GetByText(name, new() { Exact = true }).Locator("visible=true").First` | `PlanName(name)`, constant `OneReturnJourneyPlan` | "International Comprehensive Single Trip" and "International Medical Only" (card labels "One return journey" and "Medical only"). The names are repeated in hidden markup, so only the visible one counts. |
| Plan price | `Page.GetByText(OneReturnJourneyPlan, new() { Exact = true }).Locator("..").GetByText(Amount).Locator("visible=true")` | `GetPlanPriceAsync()` | e.g. `$279.03` before customising. No id; the price pattern next to the plan name. After customising, a sticky summary repeats the name without a price, so it doesn't match. |
| Select and customise plan | `Page.Locator("button#ST")` | `SelectPlanButton`, `SelectPlanAsync()` | The id is the product code (`ST` single trip, `MO` medical only), the same `ProductCode` the API sends. By role and name, two buttons match. |

### 3.6 Customise plan (same URL, opens after Select)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Section | `Page.GetByText("Changes below only apply to the selected plan")` | `CustomiseSection` (private), waited for in `SelectPlanAsync()` | A sticky summary with the chosen plan and its price also appears at the top. |
| Discount Yes or No | `RadioGroup("schsDiscount").GetByText("Yes", new() { Exact = true })`, state `[id='schsDiscount-yes']` | `CustomisePlanAsync()` | Question "Are you a Southern Cross Health Society member? *". No default. |
| Discount amount | `Page.GetByText("Discount", new() { Exact = true }).Locator("..").GetByText(Amount)` | `GetDiscountAsync()` | Only shown when the discount applies (`-$13.95` on a $279.03 base, 5%); `GetDiscountAsync()` returns 0 when it isn't there. |
| Excess | `RadioGroup("excess").GetByText("$100", new() { Exact = true })` | `CustomisePlanAsync()` | Input ids `#excess-0`, `#excess-100`, `#excess-200`. |
| Cancellation cover | `RadioGroup("cancellationCover").GetByText("$5,000", new() { Exact = true })` | `CustomisePlanAsync()` | Input ids `#cancellationCover-2500`, `-5000`, `-10000`, `-Unlimited`. |
| Extra cover | `Page.GetByRole(AriaRole.Checkbox, new() { Name = cover, Exact = true })` | `CustomisePlanAsync()` | `CheckAsync()`. Names "Moped or motorbike", "Skiing or snowboarding", "Cruise". The ids contain spaces (`Skiing or snowboarding ICNZ`), so don't select by id. |
| Premium change rows | `Page.GetByText("Premium change", new() { Exact = true }).Locator("..").GetByText(Amount)` | `GetPremiumChangesAsync()` | Rows in order: excess (`Nth(0)`), cancellation cover (`Nth(1)`), extra cover (`Nth(2)`). Updated after each PUT `/api/Quotes/extras`. |
| Next: Traveller details | `Page.GetByRole(AriaRole.Button, new() { Name = "Next: Traveller details" })` | `ClickNextToTravellerDetailsAsync()` | Waits for PUT `/api/Quotes/select` around the click, then for `step=traveller-details`. |

### 3.7 Traveller details: eligibility and declaration (`?step=traveller-details`)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Page loaded | `Expect(Page).ToHaveURLAsync(new Regex("step=traveller-details"))` | - | Asserted by the step. Heading text "Traveller details". |
| Q1 citizens or residents, eligible for public health, returning to NZ | `RadioGroup("CitizenOrLivingInNZ").GetByText("Yes", new() { Exact = true })`, state `[id='CitizenOrLivingInNZ-yes']` | `EligibilityQuestion(name)`, `CompleteTravellerEligibilityAsync()`, `AnswerEligibilityQuestionAsync(name, answer)` | "No" shows the ineligibility message. |
| Q2 already left NZ | `RadioGroup("JourneyStarted NZ")`, state `[id='JourneyStarted NZ-no']` | `CompleteTravellerEligibilityAsync()` | The name and id contain a space. |
| Q3 cover refused, claim declined or policy voided for fraud | `RadioGroup("Fraud NZ")`, state `[id='Fraud NZ-no']` | `CompleteTravellerEligibilityAsync()` | |
| Ineligibility message | `Message("Sorry, you don't appear to meet our minimum eligibility criteria for this type of travel insurance.")` | `Message(text)` | Shown as soon as Q1 is "No". |
| Declaration 1 | `Page.GetByRole(AriaRole.Checkbox, new() { Name = "I confirm that I have read, understood, and agree" })` | `ConfirmDeclaration` (private) | Id `#confirm-understood`. |
| Declaration 2 | `Page.GetByRole(AriaRole.Checkbox, new() { Name = "I authorise SCTI to collect, use, and share personal information" })` | `AuthoriseDeclaration` (private) | Id `#authorise-collect-information`. `CompleteTravellerEligibilityAsync()` then waits for the policyholder heading. |
| Required message | `Message("This field is required")` | `Message(text)` | Three for empty eligibility answers. |
| Declaration message | `Message("You must agree to the declaration to continue.")` | `Message(text)` | Two when both boxes are unticked. |
| Next: High-value items | `Page.GetByRole(AriaRole.Button, new() { Name = "Next: High-value items" })` | `TryContinueToHighValueItemsAsync()` | Validation only. Throws instead of clicking if the traveller form is showing, so a complete form is never sent. |
| Back: Plan options | `Page.GetByRole(AriaRole.Button, new() { Name = "Back: Plan options" })` | - | Not used. |
| Total premium | `Page.GetByText("Total premium", new() { Exact = true }).Locator("..").GetByText(Amount)` | `TotalPremium` | Equals the customised plan price. Recorded when the page opens. |
| Progress steps | Travel info, Plan options, Traveller details, High-value items, Medical, Summary | - | Shown beside the form; not asserted. |

### 3.8 Travellers form (same page)

| Element | Locator (C#) | Page-object member | Wait / notes |
|---|---|---|---|
| Traveller headings | e.g. `Page.GetByText("Main policyholder - Adult 1, aged 40", new() { Exact = true })`, `"Dependent child 1, aged 10"`, `"Non-dependent child 1, aged 12"` | `TravellerHeading(text)` | The section appears only after eligibility and declarations are completed. Text is built from the quote ages. |
| Traveller forms | `Page.Locator("[id$='].firstName']")` | `TravellerForms` | One per traveller; used for counts. |
| Traveller fields | `Page.Locator("[id='travellers[0].firstName']")`, pattern `travellers[{index}].{field}` with fields `title`, `firstName`, `lastName`, `dob`, `mobilePhone`, `emailAddress`, `streetAddress`, `suburb`, `city`, `postcode` | `TravellerField(index, field)`, `AddTravellerAsync(type, first, last, dob)` | Labels such as "First name *" repeat for every traveller, so role and label locators are ambiguous. Index 0 is the policyholder, then travellers in quote order. `AddTravellerAsync` fills the first empty form of the requested type (index 0 is always the adult policyholder) and returns its index. Children only have `firstName`, `lastName`, `dob`. |
| Title | Click `[id='travellers[0].title']`, then `Page.GetByRole(AriaRole.Button, new() { Name = "Mx.", Exact = true })` | - | Not used. Custom dropdown; the options are buttons. |
| Date of birth | `TravellerField(index, "dob")`, `FillAsync("dd/MM/yyyy")`, then press Tab | `EnterDateOfBirthAsync(index, date)` | Focus opens a calendar; Tab closes it. A wrong age shows the mismatch message below. |
| Date of birth mismatch | `Message("Entered age and date of birth do not match.")`, buttons "Re-enter" and "Update quote" | `DateOfBirthPromptButton(name)`, `ChooseDateOfBirthPromptOptionAsync(name)` | Prompt "Date of birth changed". "Re-enter" keeps the price and reopens the calendar. "Update quote" re-prices (not used). Not shown in Firefox (section 7). |
| Mobile phone, email | `TravellerField(0, "mobilePhone")`, `TravellerField(0, "emailAddress")` | `FillPolicyholderContactAsync(email, mobile, address)` | |
| Street address | `TravellerField(0, "streetAddress")`, `PressSequentiallyAsync("1 Queen Street")`, then `Page.GetByRole(AriaRole.Button, new() { Name = "1 Queen Street, Auckland CBD, Auckland, New Zealand", Exact = true })` | `FillPolicyholderContactAsync()` | Suggestions come from Google Places, which loads after the travellers section appears, so the page object first waits for `window.google.maps.places`. Choosing one fills suburb, city and postcode; it then asserts the postcode isn't empty. |
| Traveller type | Radio `[id='travellers[{index}].type.DependentChild']` (also `.Adult`, `.NonDependentChild`); click the `label` that contains it | `SetTravellerTypeAsync(index, type)` | Pre-selected from the quote. Asserts the radio is checked. A change re-renders the block: heading and fields change (adults have title, phone, email and an address choice; children only names and date of birth). |
| Add traveller | `Page.GetByRole(AriaRole.Button, new() { Name = "Add traveller" })` | `AddAnotherTravellerAsync()` | Adds a block headed "Adult N" with the address choice "Same as main policyholder" or "Search for an address". Waits for the new `firstName` field and returns the new index. |
| Remove traveller | `Page.GetByText("Remove", new() { Exact = true }).Nth(index - 1)` | `RemoveTravellerAsync(index)` | A link-like element without a role. The policyholder has no Remove link, so link n belongs to traveller n + 1. Removes at once, no confirmation; waits for the form to detach. Premium unchanged. |

## 4. Network calls

`NetworkRecorder` (`tests/Support/NetworkRecorder.cs`) listens to `Page.Response` and `Page.RequestFailed` for every
scenario and records method, URL, status and request body. For first-party `/api/` calls on the site's host it also
reads the response body. After each scenario the hooks write the full list to
`TestResults/ui/<scenario>-<id>.network.log` (bodies only for first-party API calls, pretty-printed JSON), attach it
to the Reqnroll output, and write the first-party calls with bodies to the test output. Steps assert only first-party
calls, by status and request body (parsed with `JsonDocument`); paths are in `QuoteApi`. `BasePage.RunAndWaitForApiResponseAsync()`
wraps an action and waits for a given API response.

| Trigger | Method | URL pattern | Expected status | Assert? |
|---|---|---|---|---|
| Load `BaseUrl` | GET | `/api/config` | 200 | No: logged only. `NavigateAsync()` waits for the "Get a quote" button instead |
| Get a quote with a complete form | POST | `/api/Quotes/create` | 200 | Yes: exactly one call, 200, body (below) matches destinations, dates and ages |
| Get a quote with an empty form | - | no `/api/Quotes/create` | - | Yes: no request is sent and the URL is still `BaseUrl` |
| Each discount, excess, cancellation cover or extra cover change | PUT | `/api/Quotes/extras` | 200 | Yes: at least one call, all 200; the last body has the final choices |
| Next: Traveller details | PUT | `/api/Quotes/select` | 200 | Yes: `ClickNextToTravellerDetailsAsync()` waits for it; the final step asserts exactly one call with 200 |
| Filling the traveller form | - | no first-party call | - | Yes: no first-party `/api/` call after `/api/Quotes/select` |
| Typing the street address | GET | `maps.googleapis.com/...` | 200 | No: third party |
| Analytics, ads and webchat (Google Analytics, DoubleClick, Taboola, Facebook, pxf.io, ambithub, `/cdn-cgi/rum`) | various | - | - | No: logged without bodies. Not blocked (`Page.RouteAsync` would cut the noise if needed) |

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

Two feature files: happy path and validation. Every scenario and Examples row is its own NUnit test and may run in
parallel with the others. V1 to V5 are labels used in this plan only; the scenario titles don't carry them.

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
    And the quote request should contain destinations "<destination codes>" and travellers "Adult:<adult age>, DependentChild:<dependent age>, NonDependentChild:<non-dependent age>"
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

  Scenario: The quote form requires every field
    When I click Get plan options
    Then I should see the quote form error "All fields are required."
    And no quote should be requested

  Scenario Outline: Traveller ages outside the allowed range are rejected
    When I try to add "<traveller>" travellers
    Then I should see the traveller error "<message>"
    And the travellers selector should stay open

    # Children need an adult on the policy, otherwise a different rule is shown.
    Examples:
      | traveller                      | message                                          |
      | Adult:17                       | Adult traveller must be aged between 18 and 118  |
      | Adult:119                      | Adult traveller must be aged between 18 and 118  |
      | Adult:40, DependentChild:21    | Dependent traveller must be aged 20 or under     |
      | Adult:40, NonDependentChild:18 | Non-dependent traveller must be aged 17 or under |

  Scenario Outline: The return date must be at least 2 days after the departure date
    When I add travel dates "today+30" and "today+44"
    Then the travel dates should be "today+30" to "today+44"
    When I change the return date to "<return>"
    Then I should see the travel dates error "Return date must be at least 2 days after the Departure date"
    And the travel dates selector should stay open

    Examples:
      | return   |
      | today+20 |
      | today+30 |

  Scenario: Traveller details enforce eligibility, the declarations and a matching date of birth
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
    When I choose "Re-enter" in the date of birth prompt
    Then the total premium should be unchanged

  Scenario: A traveller added on Traveller details can change type and be removed
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
- "Get a quote" sends exactly one POST `/api/Quotes/create` (200) whose body has `ProductCode` "ST", `BaseCountry` "NZL",
  `Destinations` equal to the codes column, `StartDateOfJourney` and `EndDate` equal to the travel dates,
  `StartDateOfInsurance` equal to today (NZ), and `AdultsAge`, `DependentChildAges` and `NonDependentChildAges` equal
  to the ages columns. The page moves to `step=plan-options`, both plan names are visible, `button#ST` is visible, and
  the base price is positive (recorded for the sum check).
- After Select, the customise section is visible. Options already at the chosen value (default excess $200 and
  cancellation cover $2,500) aren't clicked; each real change waits for its amount to update. Every PUT
  `/api/Quotes/extras` returned 200, and the last body has `ProductCode` "ST", `SchsDiscount` true for Yes or false for
  No, `Excess` "100" or "0", `CancellationCover` "5000" or "Unlimited", and only the chosen cover flag true
  (`MopedCover`, `SkiCover` or `CruiseCover`).
- The plan price equals the base price, plus the discount (negative, about 5% of base when Yes, 0 when No), plus
  the three premium changes. Example from exploration: 279.03 - 13.95 + 34.49 + 33.97 + 116.00 = $449.54.
- "Next: Traveller details" sends PUT `/api/Quotes/select` and opens `step=traveller-details`. The three
  eligibility radiogroups are visible. The total premium shown is recorded.
- After eligibility and declarations there is one form per traveller, headed "Main policyholder - Adult 1, aged <adult
  age>", "Dependent child 1, aged <dependent age>" and "Non-dependent child 1, aged <non-dependent age>". First name,
  last name and date of birth hold the values entered; the policyholder's email and mobile hold the values entered,
  and city and postcode are filled from the address (`Auckland`, `1010` observed). No "This field is required" or
  "do not match" message is shown.
- The total premium equals the customised plan price. Exactly one PUT `/api/Quotes/select` (200) was made and no
  first-party request after it. The page is still `step=traveller-details`.

Validation:
- V1: "All fields are required." is visible, no POST `/api/Quotes/create` was captured, and the URL is still `BaseUrl`.
- V2: the matching message is shown in the travellers dialog, and the dialog stays open after Done. Ages 17, 21 and
  18 were tried during exploration; the `Adult:119` row follows from the 18-to-118 range in the message.
- V3: after the travel dates field shows the first pair, changing the return date shows "Return date must be at least
  2 days after the Departure date" and the date picker stays open.
- V4: three "This field is required" and two "You must agree to the declaration to continue." messages show, and the
  URL stays `step=traveller-details`. Q1 "No" shows the ineligibility message. A date of birth for age 30 against a
  quoted 40 shows "Entered age and date of birth do not match." with "Re-enter" and "Update quote" buttons. After
  "Re-enter" the total premium equals the value recorded when the page opened.
- V5: "Add traveller" adds a form headed "Adult 2". Choosing "Dependent child (20 and under)" changes the heading to
  "Dependent child 2"; its first name, last name and date of birth fields are visible and it has no title, mobile or
  email field. "Remove" brings the page back to three traveller forms, and the total premium is unchanged.

## 6. Step mapping

Bindings live in two files: `InsuranceBookingSteps.cs` (happy path, "Main" below) and
`InsuranceBookingValidationSteps.cs` ("Validation"), both in `tests/StepDefinitions/`. `StepArgumentTransformations.cs`
turns `today+N` and `age:N` into `DateOnly` and `Type:age` lists into `IReadOnlyList<QuoteTraveller>`. Steps share
values (dates, travellers, prices, the added traveller's index) through `QuoteState`.

| Step text | Binding file | Page-object members used |
|---|---|---|
| `I navigate to the insurance portal` | Main | `QuoteFormPage.NavigateAsync()`: go to `BaseUrl`, wait for "Get a quote" |
| `I select "(.*)" journey` | Main | `SelectJourneyAsync(name)`, asserts `JourneyTab(name)` is `aria-selected` |
| `I add destination "(.*)"` | Main | `FillDestinationsAsync(names)`, asserts `DestinationInput` value |
| `I add travel dates "(.*)" and "(.*)"` | Main | `FillTravelDatesAsync(departure, return)`, asserts `TravelDatesInput` value |
| `I add "(.*)" travellers` | Main | `FillTravellersAsync(travellers)`, asserts `TravellersInput` is "N Travellers" |
| `I click Get plan options` | Main | `ClickGetPlanOptionsAsync()`: clicks "Get a quote" |
| `I should see plan options available` | Main | `PlanOptionsPage.WaitForLoadAsync()`, `PlanName(name)`, `SelectPlanButton`, `GetPlanPriceAsync()` (records the base price) |
| `the quote request should contain destinations "(.*)" and travellers "(.*)"` | Main | none: `NetworkRecorder.ApiCalls("POST", QuoteApi.Create)` |
| `I select a plan and customise it` | Main | `SelectPlanAsync()` |
| `I choose discount "(.*)", excess "(.*)", cancellation cover "(.*)", and extra cover "(.*)"` | Main | `CustomisePlanAsync(discount, excess, cancellationCover, covers)`; "None" means no extra cover |
| `the plan price should equal the base price plus the premium changes` | Main | `GetPlanPriceAsync()`, `GetDiscountAsync()`, `GetPremiumChangesAsync()` (records the customised price) |
| `the latest plan update request should contain discount "(.*)", excess "(.*)", cancellation cover "(.*)" and extra cover "(.*)"` | Main | none: `NetworkRecorder.ApiCalls("PUT", QuoteApi.Extras)` |
| `I click Next to Traveller details` | Main | `ClickNextToTravellerDetailsAsync()`, then records `TravellerDetailsPage.TotalPremium` |
| `I should see traveller eligibility section` | Main | `EligibilityQuestion(name)` for the three questions, plus the URL |
| `I complete traveller eligibility and privacy declaration` | Main | `CompleteTravellerEligibilityAsync()` |
| `I add adult traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Main | `AddTravellerAsync(TravellerType.Adult, ...)` |
| `I enter contact details for the main policyholder with email "(.*)", mobile "(.*)" and address "(.*)"` | Main | `FillPolicyholderContactAsync(email, mobile, address)` |
| `I add dependent traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Main | `AddTravellerAsync(TravellerType.DependentChild, ...)` |
| `I add non-dependent traveller with firstname "(.*)", lastname "(.*)", and DOB "(.*)"` | Main | `AddTravellerAsync(TravellerType.NonDependentChild, ...)` |
| `I should see all travellers added successfully` | Main | `TravellerForms`, `TravellerHeading(text)`, `TravellerField(index, field)`, `Message(text)`, `TotalPremium`; `NetworkRecorder` for `/api/Quotes/select` and later calls |
| `I have a quote for "(.*)" from "(.*)" to "(.*)" for "(.*)"` | Validation | `SelectJourneyAsync`, `FillDestinationsAsync`, `FillTravelDatesAsync`, `FillTravellersAsync`, `ClickGetPlanOptionsAsync`, `WaitForLoadAsync` |
| `I should see the quote form error "(.*)"` | Validation | `QuoteFormPage.Message(text)` |
| `no quote should be requested` | Validation | none: URL equals `BaseUrl`, `NetworkRecorder` has no create call |
| `I try to add "(.*)" travellers` | Validation | `EnterTravellersAsync(travellers)`: clicks Done without waiting for the dialog to close |
| `I should see the traveller error "(.*)"` | Validation | `TravellersError(text)` |
| `the travellers selector should stay open` | Validation | `TravellersDialog` |
| `I change the return date to "(.*)"` | Validation | `ChangeReturnDateAsync(date)` |
| `I should see the travel dates error "(.*)"` | Validation | `TravelDatesError(text)` |
| `the travel dates selector should stay open` | Validation | `TravelDatesDialog` |
| `the travel dates should be "(.*)" to "(.*)"` | Validation | `TravelDatesInput` |
| `I continue without answering the eligibility questions or declarations` | Validation | `TryContinueToHighValueItemsAsync()` |
| `I should see (\d+) "(.*)" messages` | Validation | `TravellerDetailsPage.Message(text)` count |
| `I should still be on the traveller details step` | Validation | none: URL check |
| `I answer "(.*)" to whether all travellers are New Zealand residents` | Validation | `AnswerEligibilityQuestionAsync("CitizenOrLivingInNZ", answer)` |
| `I should see the eligibility message "(.*)"` | Validation | `Message(text)` |
| `I enter date of birth "(.*)" for the main policyholder` | Validation | `EnterDateOfBirthAsync(0, date)` |
| `I should see the date of birth error "(.*)"` | Validation | `Message(text)` |
| `I should be offered "(.*)" or "(.*)"` | Validation | `DateOfBirthPromptButton(name)` |
| `I choose "(.*)" in the date of birth prompt` | Validation | `ChooseDateOfBirthPromptOptionAsync(name)` |
| `the total premium should be unchanged` | Validation | `TotalPremium` against the value recorded on arrival |
| `I add another traveller` | Validation | `AddAnotherTravellerAsync()` (records the new index) |
| `I should see a traveller form headed "(.*)"` | Validation | `TravellerHeading(text)` |
| `I change the new traveller's type to "(.*)"` | Validation | `SetTravellerTypeAsync(index, type)` |
| `the "(.*)" form should only ask for first name, last name and date of birth` | Validation | `TravellerHeading(text)`, `TravellerField(index, field)` |
| `I remove the new traveller` | Validation | `RemoveTravellerAsync(index)` |
| `I should see (\d+) traveller forms` | Validation | `TravellerForms` count |

## 7. Risks

**Flaky areas and how to wait for them**
- Price recalculation is asynchronous. `CustomisePlanAsync()` reads the affected amount before each change and waits
  with `Expect(amount).Not.ToHaveTextAsync(before)` until the server re-prices it, so the updates stay in order.
  Options already at the chosen value are skipped. Prices are never read straight after a click.
- Quick successive radio clicks can be lost: one of three fast label clicks did not register during exploration.
  `CheckRadioAsync()` asserts the hidden input with `ToBeCheckedAsync()` after each click.
- Sections appear after a choice: the customise section, the age inputs, the travellers form, and a re-rendered
  traveller block after a type change. The page objects wait on the section text, heading or field (`WaitForAsync`,
  `Expect`), never on a fixed delay.
- Destination search and the address field return results as you type. Both use `PressSequentiallyAsync` and click
  the result button, which auto-waits. Address suggestions come from Google Places, which loads late; the page object
  waits for `window.google.maps.places` before typing.
- All actions, navigations and `Expect` assertions use the `Timeout` setting (30 s by default).
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
  don't run it in loops, and limit parallel workers. `appsettings.Dev.json`, `appsettings.QA.json` and
  `appsettings.Prod.json` exist, but all point at the production URL until test environments are known.
- Compute "today" in `Pacific/Auckland` and dates of birth from today, or a UTC CI agent can be a day out and break
  the age match. The browser context uses the same time zone.
- Parallel runs: `[assembly: Parallelizable(ParallelScope.Children)]` with `FixtureLifeCycle.InstancePerTestCase`
  (Reqnroll 2.4.1) runs scenarios and Examples rows in parallel; each scenario launches its own browser, context and
  page objects. Cap the number of browsers with `NUnit.NumberOfTestWorkers` (3 in `tests/headed.runsettings`).
- Retries: a failed scenario is run again once (`NRetrySettings.maxRetries: 2` in `reqnroll.json`). A retried
  scenario creates another draft quote.
- Third-party analytics, ads and webchat create most of the traffic. They are logged without bodies; assertions are
  filtered to first-party `/api/`. They are not blocked.

**Cross-browser** (`Browser` setting or `TEST_Browser`)
- WebKit: the quote form field icons intercept centre clicks, so the fields are clicked near their left edge.
- Firefox: a pasted invalid return date is silently reverted, so `ChangeReturnDateAsync()` types the digits and
  presses Tab.

**Known issues**
- Firefox: in V4 the "Date of birth changed" prompt ("Re-enter", "Update quote") never appears after a mismatched date
  of birth, so that scenario fails (10/11 on Firefox). The date of birth input sits inside a `<button>` on the site,
  which Firefox handles differently. Not yet decided: keep failing, skip on Firefox, or assert only the message.

**Running**
- Headless by default. Headed in VS Code: `tests/headed.runsettings` sets `TEST_Headless=false`.
- `dotnet test --filter "Category=UI"`; add `TEST_ENV=QA`, `TEST_Browser=firefox` or `TEST_BaseUrl=...` to
  override settings.

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
