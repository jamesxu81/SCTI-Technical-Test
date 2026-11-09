# Senior Test Engineer - Technical Test

## Overview

This is a comprehensive test automation framework demonstrating expertise across multiple testing layers:
- UI Automation (Playwright + C# + Reqnroll)
- API Automation
- Unit Testing
- Mock Testing (Moq)
- Component Testing
- Integration Testing
- Validation Testing

## Project Structure

```
TechnicalTest/
├── src/
│   ├── Api/
│   │   ├── Clients/
│   │   │   ├── IPublicApiClient.cs
│   │   │   └── PublicApiClient.cs
│   │   ├── Models/
│   │   │   └── Post.cs
│   │   ├── Processors/
│   │   │   └── PostProcessor.cs
│   │   └── Validators/
│   │       └── PostValidator.cs
│   └── Ui/
│       ├── Pages/
│       │   ├── BasePage.cs
│       │   └── InsurancePage.cs
│       └── Helpers/
│           └── ConfigurationHelper.cs
│
├── tests/
│   ├── Features/
│   │   └── InsuranceBooking.feature
│   ├── StepDefinitions/
│   │   └── InsuranceBookingSteps.cs
│   ├── UnitTests/
│   │   └── PostValidatorTests.cs
│   ├── ApiTests/
│   │   └── PublicApiClientTests.cs
│   ├── MockTests/
│   │   └── PostProcessorMockTests.cs
│   ├── ComponentTests/
│   │   └── PostProcessorComponentTests.cs
│   ├── IntegrationTests/
│   │   └── PublicApiIntegrationTests.cs
│   ├── ValidationTests/
│   │   └── PostValidationTests.cs
│   └── appsettings.{Environment}.json
│
└── .github/
    └── workflows/
        ├── ui-tests.yml
        └── all-tests.yml
```

##  Getting Started

### Prerequisites
- .NET 8.0 SDK
- PowerShell (for Playwright installation)
- Internet connection (for API integration tests)

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd "TechnicalTest"
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Install Playwright browsers**
   ```powershell
   pwsh tests/bin/Debug/net8.0/playwright.ps1 install
   ```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Tests by Category

**Unit Tests**
```bash
dotnet test --filter "Category=Unit"
```

**API Tests**
```bash
dotnet test --filter "Category=API"
```

**Mock Tests**
```bash
dotnet test --filter "Category=Mock"
```

**Component Tests**
```bash
dotnet test --filter "Category=Component"
```

**Integration Tests**
```bash
dotnet test --filter "Category=Integration"
```

**Validation Tests**
```bash
dotnet test --filter "Category=Validation"
```

**UI Tests**
```bash
dotnet test --filter "Category=UI"
```

### Run Tests on Specific Environment

**Dev Environment**
```bash
$env:TEST_ENV="Dev"
dotnet test --filter "Category=UI"
```

##  Expectation- Test Coverage

### 1. UI Test Automation (Playwright + Reqnroll)
-  Feature files with Scenario Outline and Examples
-  Page Object Model (POM) implementation
-  Network call capture
-  Dynamic element handling with waits
-  Form filling and validation
-  Multi-environment configuration
-  Parallel execution support
-  Comprehensive assertions

**Automate this Scenario Flow :**
- Navigate to a page https://insure.scti.co.nz/
- Pick One return journey
- Add destinations, travel dates, travellers and Get plan options
- Select One return jounery by hitting Select and customise plan
- Choose discount, excess, cancellation cover, and add extra cover as per your wish and hit Next: Traveller details
- complete Travellers Eligibilty and privacy declaration
- Fill Traveller form 
- Add Traveller, 1 Adult, 1 dependent, 1 non dependent

### 2. API Automation Tests
-  GET requests validation
-  POST requests validation
-  Schema validation
-  Status code validation
-  Positive and negative scenarios
-  Count validation
-  Invalid ID handling (404)

**API Endpoints:**
- `GET /posts` - Returns 100 posts with schema validation
- `GET /posts/{id}` - Returns specific post or null
- `POST /posts` - Creates new post and validates response

### 3. Unit Tests
-  PostValidator logic testing
-  Null checks
-  Field validations (UserId, Title, Body)
-  Edge cases
-  Boundary value testing
-  Multiple invalid fields handling

### 4. Mock Tests (Moq)
-  Simulating API success
-  Simulating API failures
-  Simulating empty lists
-  Simulating thrown exceptions
-  Simulating invalid posts
-  Verifying mock interactions

### 5. Component Tests
-  PostProcessor filtering logic
-  Business rules validation
-  Partial data handling
-  Mixed valid/invalid data scenarios
-  Integration of validator with processor

### 6. Integration Tests
-  Real HTTP client usage
-  Real API calls to jsonplaceholder.typicode.com
-  End-to-end flow validation
-  Data consistency checks
-  Multiple valid IDs testing

### 7. Validation Testing
-  Required fields validation
-  Title/Body constraints
-  UserId constraints
-  Boundary values
-  Special characters handling
-  Very long strings handling
-  Whitespace validation

## Configuration

### Environment-Specific Settings

**appsettings.json** (Default)
```json
{
  "BaseUrl": "https://insure.scti.co.nz/",
  "Timeout": 30000,
  "Headless": true
}
```
##  CI/CD Pipelines

### Add Multi-Environment UI Tests Pipeline
- Runs UI tests on Dev, QA, and Prod environments
- Manual trigger with environment selection
- Parallel execution across environments
- Test result artifacts upload

### Add All Tests Pipeline
- Runs all test categories in parallel
- Triggered on push/PR
- Separate jobs for each test type
- Individual test result uploads

## Technologies Used

- **Framework**: .NET 8.0
- **UI Automation**: Playwright 1.41.0
- **BDD Framework**: Reqnroll 2.0.3
- **Test Framework**: NUnit 4.0.1
- **Mocking**: Moq 4.20.70
- **Assertions**: FluentAssertions 6.12.0
- **Configuration**: Microsoft.Extensions.Configuration
- **API**: JSONPlaceholder (typicode.com)

##  Design Patterns

### Page Object Model (POM)
- `BasePage`: Abstract base with common functionality
- `InsurancePage`: Specific page implementation with locators and actions
- Separation of page structure from test logic

### Dependency Injection -API
- Interface-based design (IPublicApiClient)
- Easy mocking and testing
- Loose coupling

### Repository Pattern
- API client abstraction
- Centralized data access logic

## Key Features

### Implemented Features 
1. **Parallel Execution** - Tests can run concurrently
2. **Network Call Capture** - All HTTP requests logged
3. **Dynamic Wait Strategies** - Intelligent element waiting
4. **Multi-Environment Support** - Dev, QA, Prod configurations
5. **Comprehensive Assertions** - Text, attributes, elements
6. **Error Handling** - Graceful failure management
7. **Clean Architecture** - Separation of concerns
8. **Test Data Management** - Scenario outlines with examples
9. **CI/CD Ready** - GitHub Actions workflows
10. **Detailed Logging** - TRX test result reports

##  Test Execution Report

All tests are designed to:
-  Execute independently
-  Clean up resources properly
-  Provide clear failure messages
-  Support parallel execution
-  Be maintainable and scalable

## Contributing

This project should demonstrates enterprise-level test automation practices:
- Clean code principles
- SOLID principles
- DRY (Don't Repeat Yourself)
- Meaningful naming conventions
- Proper abstraction layers

## Support

For questions or issues, please refer to the technical assessment documentation.

---

**Author**: Senior Test Automation Engineer Candidate  
**Version**: 1.0.0  
**Last Updated**: 2024


--- 
## Non-Functional 

**Record below Journey**
**Automate this Scenario Flow :**
- Navigate to a page https://insure.scti.co.nz/
- Pick One return journey
- Add destinations, travel dates, travellers and Get plan options
- Select One return jounery by hitting Select and customise plan
- Choose discount, excess, cancellation cover, and add extra cover as per your wish and hit Next: Traveller details
- complete Travellers Eligibilty and privacy declaration
- Fill Traveller form 
- Add Traveller, 1 Adult, 1 dependent, 1 non dependent

**Create WLM model for baseline, load and stress test**
**Distributed WLM model**

