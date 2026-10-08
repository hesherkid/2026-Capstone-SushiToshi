# Testing Documentation - 2026 C#Capstone - Sushi Toshi

> **CI Update (2026-10-08):** GitHub Actions workflow rebuilt for this fork. CI now runs on `main` only (PRs + pushes) plus a manual "Run workflow" button. Frontend requires Node.js 20.9+ (CI uses Node 22). E2E tests run in CI after merges to `main` and on manual runs (Chromium only, not a required check). Codecov has been removed. QR code generation uses SkiaSharp (not System.Drawing). See [CI/CD Pipeline](https://claude.ai/chat/ac7329a5-d6e8-4f73-ba5d-6f90f211b858#cicd-pipeline).

> **Recent Update (2025-11-10):** All test compilation errors have been fixed. Backend tests updated to use `User_Oid`/`User_Name` instead of `User_Id` to match the current authentication model. BillController tests updated to include `IPricingService` dependency. All 101 backend tests and 87 frontend tests are now passing.

## Table of Contents

- [Overview](#overview)
- [Testing Infrastructure](#testing-infrastructure)
- [Current Test Coverage](#current-test-coverage)
- [Running Tests](#running-tests)
- [Test Implementation Guide](#test-implementation-guide)
- [CI/CD Pipeline](#cicd-pipeline)
- [Coverage Reports](#coverage-reports)
- [Testing Best Practices](#testing-best-practices)
- [Troubleshooting](#troubleshooting)
- [Resources](#resources)

---

## Overview

This document provides comprehensive documentation for the automated testing infrastructure implemented for the Sushi Toshi Restaurant Management System. The testing framework follows the **testing pyramid** strategy to ensure code quality and reliability.

### Testing Pyramid

- **Unit Tests (70%)**: Back-end services, controllers, and front-end components
- **Integration Tests (20%)**: API endpoints with database interactions
- **E2E Tests (10%)**: Critical user flows and workflows

### Testing Philosophy

- Tests are isolated and independent
- Tests follow AAA pattern (Arrange, Act, Assert)
- Tests have descriptive names (Given_When_Then pattern)
- Tests cover happy path and edge cases
- Tests verify error handling
- Tests include accessibility checks (frontend)
- Tests use proper mocking
- Tests clean up resources properly

---

## Testing Infrastructure

### Backend Testing Stack

**Framework & Tools:**

- **Test Framework**: xUnit
- **Assertion Library**: FluentAssertions
- **Mocking Framework**: Moq
- **In-Memory Database**: Microsoft.EntityFrameworkCore.InMemory
- **Code Coverage**: Coverlet

**Test Projects:**

```
src/
├── back-end.Tests/              # Unit tests
│   ├── Controllers/             # Controller tests
│   ├── Services/                # Service tests
│   ├── Helpers/                 # Helper tests
│   └── DTO/                     # DTO validation tests
└── back-end.IntegrationTests/   # Integration tests
```

### Frontend Testing Stack

**Framework & Tools:**

- **Unit Testing**: Jest + React Testing Library
- **E2E Testing**: Playwright
- **Assertion Library**: @testing-library/jest-dom
- **User Interaction**: @testing-library/user-event
- **Code Coverage**: Istanbul (built into Jest)

**Configuration Files:**

- `src/front-end/jest.config.js` - Jest configuration
- `src/front-end/jest.setup.js` - Test environment setup
- `src/front-end/playwright.config.js` - E2E test configuration

**Test Organization:**

```
src/front-end/
├── src/
│   └── components/
│       ├── admin/
│       │   └── __tests__/       # Admin component tests
│       ├── auth/
│       │   └── __tests__/       # Auth component tests
│       ├── customer/
│       │   └── __tests__/       # Customer component tests
│       └── staff/
│           └── __tests__/       # Staff component tests
└── e2e/                         # E2E tests
    └── qr-code-generation.spec.js
```

---

## Current Test Coverage

### Backend Test Status

#### Implemented Tests ✅

**Controllers (7/19 tested - ~37%)**

1. **AuthControllerTests** (12+ test cases)
   - Login with valid/invalid credentials
   - Registration with validation
   - Email case insensitivity
   - Password hashing verification
   - Last interaction timestamp updates

2. **BillControllerTests** (Comprehensive)
   - Bill creation, retrieval, closure
   - Bill cancellation
   - Authorization checks

3. **AdminQrCodeControllerTests** (12+ test cases)
   - WiFi QR generation
   - Session QR generation
   - Bulk generation
   - Cache management
   - Input validation

4. **CategoryAndTagControllerTests**
   - Category CRUD operations
   - Tag management

5. **LocationControllerTests**
   - Location management
   - Menu assignment

6. **MenuControllerTests**
   - Menu CRUD operations
   - Menu item retrieval

7. **OrderControllerTests**
   - Order creation
   - Status updates

**Services (1/3 tested - ~33%)**

1. **QrGeneratorServiceTests** (15+ test cases)
   - WiFi credential retrieval
   - QR code generation (WiFi & Session)
   - Label rendering
   - URL generation
   - Error handling

#### Tests Needed ⚠️

**Controllers:**

- AnalyticsController
- DashboardController
- DiningSessionController
- MenuItemController
- MenuAssignmentController
- MenuLocationController
- ServiceRequestController
- SessionController
- SessionParticipantController
- StaffController
- TableController
- TableEntityController
- TableGroupController
- TagController

**Services:**

- SendGridEmailServices
- PricingService

### Frontend Test Status

#### Implemented Tests ✅

**Components (3/99 tested - ~3%)**

1. **LoginForm** (25+ test cases)
   - Rendering tests
   - Form interactions
   - Form submission
   - Error handling
   - Navigation
   - Accessibility
   - Validation

2. **RegisterForm** (20+ test cases)
   - Rendering tests
   - Form interactions
   - Password validation
   - Success/error states
   - HTTP error handling (400, 409, 422, 500)
   - Accessibility

3. **Header** (30+ test cases)
   - Role-based rendering (Customer, Staff, Admin)
   - Navigation tests
   - Logout functionality
   - Dropdown menus
   - Click-outside-to-close
   - Accessibility

**E2E Tests:**

1. **QR Code Generation Flow** (Partially implemented)
   - Login page navigation
   - QR code page tests
   - Customer ordering flow
   - Mobile responsiveness

#### Components Needing Tests ⚠️

**High Priority (96 components):**

- SessionDashboard (staff)
- TableDashboard (staff)
- QRCodeManagement (admin)
- StaffManagementPage (admin)
- MenuItemManagement (admin)
- OrderDashboard (customer)
- BillManagement (customer)
- Analytics components (5+ components)
- Menu browsing components
- Form components (ForgotPassword, ResetPassword)

### Coverage Statistics

#### Backend

- **Current Coverage**: ~35% (7 of 22 controllers tested)
- **Target Coverage**: 80%+ overall, 90%+ for services

#### Frontend

- **Current Coverage**: ~3% (3 of 99 components tested)
- **Target Coverage**: 0%+ overall, 90%+ for critical components
- **Coverage Thresholds** (jest.config.js):
  - Branches: 70%
  - Functions: 70%
  - Lines: 70%
  - Statements: 70%

---

## Running Tests

### Backend Tests

#### Prerequisites

- .NET 9.0 SDK
- Windows OS recommended (CI uses Windows). QR code generation uses SkiaSharp; `SkiaSharp.NativeAssets.Linux` is referenced in `back-end.csproj` so it also runs on Linux.
- Integration tests start the real app, which validates `SendGrid:ApiKey` on startup. Locally this comes from user secrets; CI supplies a placeholder value

#### Run All Backend Tests

```bash
# From project root
dotnet test src/back-end.Tests/back-end.Tests.csproj
dotnet test src/back-end.IntegrationTests/back-end.IntegrationTests.csproj
```

#### Run Specific Test File

```bash
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
dotnet test --filter "FullyQualifiedName~QrGeneratorServiceTests"
```

#### Run with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

#### Generate Coverage Report

```bash
cd src/back-end.Tests
dotnet test --collect:"XPlat Code Coverage"
# Coverage report: TestResults/*/coverage.cobertura.xml
```

### Frontend Tests

#### Prerequisites

- Node.js 20.9+ (required by Next.js 16; CI uses Node 22)
- npm

#### Install Dependencies

```bash
cd src/front-end
npm install
```

#### Run Unit Tests

```bash
# Run all tests once
npm test

# Run in watch mode (re-runs on file changes)
npm run test:watch

# Run with coverage report
npm run test:coverage
# Open: coverage/lcov-report/index.html
```

#### Run Specific Test File

```bash
npm test LoginForm.test.jsx
npm test -- --testNamePattern="should render"
```

### End-to-End Tests

> **E2E tests run in CI** after merges to `main` and on manual runs - not on PRs, and they are not a required check. Run them locally before opening a PR that changes critical flows (QR codes, ordering, login).

#### Prerequisites

- Playwright installed
- MariaDB running (via `docker-compose`)
- Backend server running on `http://localhost:5264`
- Frontend server running on `http://localhost:3000`

#### Install Playwright Browsers

```bash
cd src/front-end
npx playwright install --with-deps
```

#### Run E2E Tests

```bash
# Headless mode (faster)
npm run test:e2e

# Headed mode (see the browser)
npm run test:e2e:headed

# UI mode (interactive debugging)
npm run test:e2e:ui

# Specific browser
npx playwright test --project=chromium
```

### Run All Tests (PowerShell Script)

Create `run-all-tests.ps1`:

```powershell
Write-Host "Running Back-End Unit Tests..." -ForegroundColor Green
dotnet test src/back-end.Tests/back-end.Tests.csproj

Write-Host "Running Back-End Integration Tests..." -ForegroundColor Green
dotnet test src/back-end.IntegrationTests/back-end.IntegrationTests.csproj

Write-Host "Running Front-End Tests..." -ForegroundColor Green
cd src/front-end
npm test -- --watchAll=false

Write-Host "All tests completed!" -ForegroundColor Green
```

Then run:

```bash
.\run-all-tests.ps1
```

---

## Test Implementation Guide

### Backend Testing Patterns

#### Controller Test Template

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using back_end.domain;
using back_end.Controllers;

public class YourControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly YourController _controller;

    public YourControllerTests()
    {
        // Arrange: Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Mock dependencies
        var mockLogger = new Mock<ILogger<YourController>>();

        _controller = new YourController(_context, mockLogger.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var testData = new YourEntity { /* ... */ };
        await _context.YourEntities.AddAsync(testData);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.MethodName(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
```

#### Service Test Template

```csharp
public class YourServiceTests
{
    private readonly Mock<IDependency> _mockDependency;
    private readonly YourService _service;

    public YourServiceTests()
    {
        _mockDependency = new Mock<IDependency>();
        _service = new YourService(_mockDependency.Object);
    }

    [Fact]
    public void MethodName_ValidInput_ReturnsExpectedResult()
    {
        // Arrange
        _mockDependency.Setup(x => x.Method()).Returns(value);

        // Act
        var result = _service.MethodName(input);

        // Assert
        result.Should().Be(expectedValue);
        _mockDependency.Verify(x => x.Method(), Times.Once);
    }
}
```

### Frontend Testing Patterns

#### Component Test Template

```jsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import YourComponent from "../YourComponent";

// Mock dependencies
jest.mock("axios");
jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
}));

describe("YourComponent", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("should render component correctly", () => {
    // Arrange & Act
    render(<YourComponent />);

    // Assert
    expect(screen.getByText("Expected Text")).toBeInTheDocument();
  });

  it("should handle user interaction", async () => {
    // Arrange
    const user = userEvent.setup();
    render(<YourComponent />);

    // Act
    await user.click(screen.getByRole("button", { name: /submit/i }));

    // Assert
    await waitFor(() => {
      expect(screen.getByText("Success")).toBeInTheDocument();
    });
  });

  it("should handle API errors", async () => {
    // Arrange
    axios.get.mockRejectedValueOnce(new Error("API Error"));
    render(<YourComponent />);

    // Act
    await user.click(screen.getByRole("button", { name: /load/i }));

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/error/i)).toBeInTheDocument();
    });
  });
});
```

#### Form Component Test Checklist

- [ ] Renders all form fields
- [ ] Shows/hides elements based on state
- [ ] Handles input changes
- [ ] Validates required fields
- [ ] Shows validation errors
- [ ] Submits form with valid data
- [ ] Handles submission errors
- [ ] Shows loading state during submission
- [ ] Disables submit during submission
- [ ] Resets form after successful submission
- [ ] Tests accessibility (labels, ARIA attributes)
- [ ] Tests keyboard navigation

---

## CI/CD Pipeline

### GitHub Actions Workflow

**File**: `.github/workflows/automated-tests.yml`

### Pipeline Jobs

#### 1. Backend - build, unit & integration tests (`windows-latest`) - required

```yaml
- dotnet test on back-end.Tests (restores + builds the API automatically)
- dotnet test on back-end.IntegrationTests (SendGrid__ApiKey set to a placeholder - no email is sent)
```

#### 2. Frontend - lint, unit tests & build (`ubuntu-latest`, Node 22) - required

```yaml
- npm ci (package-lock.json must be committed and in sync)
- Lint (non-blocking - warnings shown in the log but do not fail the job)
- Jest unit tests with coverage (coverage table printed in the log)
- Next.js production build (npm run build)
```

#### 3. E2E tests - Playwright, Chromium (`ubuntu-latest`) - NOT required

```yaml
- Runs only after a PR merges into main, or from "Run workflow" - never on PRs
- Starts MariaDB as a service container (Linux-only feature) on port 3307
- Starts the API on port 5264 and waits until /db-test confirms the database
  (the API applies migrations and seeds the empty database on startup)
- Builds the frontend; Playwright starts it with `npm run start`
- Runs Playwright with Chromium only
- Uploads the "playwright-report" artifact (screenshots of failures)
```

**Not in CI:** Codecov (removed). Results and coverage are shown in each job's log.

### Pipeline Triggers

**Automatic Execution:**

- Pull requests to `main` (backend + frontend jobs)
- Pushes to `main`, i.e. after a PR is merged (backend + frontend + E2E jobs)

**Manual Execution:**

- Navigate to Actions tab in GitHub
- Select "Automated Tests" workflow
- Click "Run workflow", choose the branch, and run (includes E2E)

### Required Checks (branch protection on `main`)

PRs cannot be merged until both of these pass. If a check fails, the PR author fixes it and pushes again:

- `Backend - build, unit & integration tests`
- `Frontend - lint, unit tests & build`

> If a job's `name:` is changed in the workflow file, the required check must be updated in Settings > Branches to match.

### Viewing Results

1. Open the PR and scroll to the checks section, or go to the **Actions** tab
2. Select the workflow run
3. Click a failed job, then the failed step, to read the log
4. For E2E failures, download the "playwright-report" artifact (bottom of the run page), unzip it and open `index.html`

---

## Coverage Reports

### Backend Coverage

**Generate Report:**

```bash
cd src/back-end.Tests
dotnet test --collect:"XPlat Code Coverage" /p:CoverletOutputFormat=cobertura
```

**View Report:**

- Coverage file: `TestResults/*/coverage.cobertura.xml`
- Use tools like ReportGenerator to create HTML reports

**Install ReportGenerator:**

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

**Generate HTML Report:**

```bash
reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Frontend Coverage

**Generate Report:**

```bash
cd src/front-end
npm run test:coverage
```

**View Report:**

- Open `coverage/lcov-report/index.html` in browser
- View summary in terminal output

**Coverage Thresholds** (from jest.config.js):

```json
{
  "branches": 70,
  "functions": 70,
  "lines": 70,
  "statements": 70
}
```

### Coverage Goals

#### Backend

- **Overall**: 80%+
- **Service Layer**: 90%+ (critical business logic)
- **Controllers**: 80%+
- **Critical Paths**: 100%
  - QR code generation
  - Authentication
  - Authorization

#### Frontend

- **Overall**: 70%+
- **Critical Components**: 90%+
  - QRCodeManagement
  - MenuItemManagement
  - BillManagement
  - Authentication forms
- **Hooks/Utilities**: 85%+

---

## Testing Best Practices

### General Principles

1. **Write Tests First** (TDD approach when possible)
2. **Test Behavior, Not Implementation** - Focus on what, not how
3. **Keep Tests Simple** - One assertion per concept
4. **Use Descriptive Names** - Test names should explain the scenario
5. **Isolate Tests** - No dependencies between tests
6. **Mock External Dependencies** - APIs, databases (except integration tests)
7. **Clean Up After Tests** - Dispose resources properly

### Backend Best Practices

1. **Use In-Memory Database** for unit tests

   ```csharp
   var options = new DbContextOptionsBuilder<ApplicationDbContext>()
       .UseInMemoryDatabase(Guid.NewGuid().ToString())
       .Options;
   ```

2. **Use FluentAssertions** for readable assertions

   ```csharp
   result.Should().NotBeNull();
   result.Should().BeOfType<OkObjectResult>();
   statusCode.Should().Be(200);
   ```

3. **Mock Dependencies with Moq**

   ```csharp
   var mockLogger = new Mock<ILogger<Controller>>();
   mockLogger.Verify(x => x.Log(...), Times.Once);
   ```

4. **Use Theory for Data-Driven Tests**
   ```csharp
   [Theory]
   [InlineData("value1")]
   [InlineData("value2")]
   public void Test(string value) { }
   ```

### Frontend Best Practices

1. **Query by Accessibility** - Use semantic queries

   ```jsx
   screen.getByRole("button", { name: /submit/i });
   screen.getByLabelText("Email");
   screen.getByText("Welcome");
   ```

2. **Use userEvent over fireEvent**

   ```jsx
   const user = userEvent.setup();
   await user.click(button);
   await user.type(input, "text");
   ```

3. **Wait for Async Operations**

   ```jsx
   await waitFor(() => {
     expect(screen.getByText("Success")).toBeInTheDocument();
   });
   ```

4. **Test Accessibility**

   ```jsx
   const input = screen.getByLabelText("Email");
   expect(input).toHaveAttribute("type", "email");
   expect(input).toHaveAttribute("aria-required", "true");
   ```

5. **Mock API Calls**
   ```jsx
   axios.get.mockResolvedValueOnce({ data: mockData });
   axios.post.mockRejectedValueOnce(new Error("API Error"));
   ```

---

## Troubleshooting

### Common Issues & Solutions

#### Backend Tests

**Issue**: Tests fail with database errors

```
Solution: Ensure each test uses a unique in-memory database name
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Unique name
    .Options;
```

**Issue**: QR code tests fail on non-Windows

```
Solution: QrGeneratorService uses SkiaSharp. The base SkiaSharp package does not
include the Linux native library.
- Run tests on Windows (this is what CI does), or
- Add the SkiaSharp.NativeAssets.Linux package to back-end.csproj
  (it also requires libfontconfig1 installed on the machine)
```

**Issue**: Integration tests fail in CI with `OptionsValidationException` (SendGrid)

```
Solution: Program.cs validates SendGridSettings on startup and ApiKey is [Required].
- Locally: set the key with user secrets
- CI: the workflow sets SendGrid__ApiKey to a placeholder for the integration test step
```

**Issue**: Mock setup doesn't work

```
Solution: Ensure interface is being mocked, not concrete class
var mock = new Mock<IService>(); // Correct
var mock = new Mock<Service>();  // Won't work for sealed classes
```

#### Frontend Tests

**Issue**: "Cannot find module" errors

```
Solution: Clear Jest cache
npm test -- --clearCache
```

**Issue**: Tests timeout

```
Solution: Increase timeout in jest.config.js
module.exports = {
  testTimeout: 10000, // 10 seconds
};
```

**Issue**: "Not wrapped in act(...)" warnings

```
Solution: Use waitFor for async operations
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});
```

**Issue**: Router mock not working

```
Solution: Mock Next.js router properly
jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: jest.fn(),
    pathname: '/',
  }),
}));
```

#### E2E Tests

**Issue**: E2E tests timeout

```
Solution:
1. Ensure backend is running on http://localhost:5264
2. Ensure frontend is running on http://localhost:3000
3. Increase timeout in playwright.config.js
```

**Issue**: E2E job fails in CI at "Start backend" or "Verify database connection"

```
Solution: Open the "Start backend" step log - the API log is printed there when it fails.
- Check the MariaDB service started (job "Initialize containers" step)
- Check the connection string env var in the e2e-tests job matches the service settings
```

**Issue**: Selectors not found

```
Solution: Use more resilient selectors
page.locator('[data-testid="submit-button"]') // Better
page.locator('button:has-text("Submit")')     // More resilient
```

---

## Resources

### Documentation

- [Full Testing Guide](../TESTING_README.md)
- [Implementation Summary](../TESTING_IMPLEMENTATION_SUMMARY.md)
- [Testing Summary](../TESTING_SUMMARY.md)

### Framework Documentation

#### Backend

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

#### Frontend

- [React Testing Library](https://testing-library.com/docs/react-testing-library/intro/)
- [Jest Documentation](https://jestjs.io/)
- [Testing Library Queries](https://testing-library.com/docs/queries/about)
- [userEvent API](https://testing-library.com/docs/user-event/intro)
- [Common Testing Mistakes](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)

#### E2E Testing

- [Playwright Documentation](https://playwright.dev/)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Playwright Test Generator](https://playwright.dev/docs/codegen)

### Articles & Guides

- [Testing Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html)
- [AAA Pattern](https://docs.microsoft.com/en-us/visualstudio/test/unit-test-basics)
- [TDD with xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)

---

## Summary

### Current Status (Updated: 2025-11-10)

✅ **What's Working:**

- Complete testing infrastructure setup
- **ALL backend tests passing: 101/101 tests (100%)**
  - 7 backend controllers tested (37% of controllers)
  - 1 backend service tested (QrGeneratorService)
  - All compilation errors fixed
  - Authentication & authorization tests passing
  - QR code generation fully tested
- **ALL frontend tests passing: 87/87 tests (100%)**
  - 4 critical frontend components tested (LoginForm, RegisterForm, Header, QRCodeManagement)
  - Accessibility tests included
  - Error handling tests included
- CI/CD pipeline operational
- Code coverage tracking enabled

⚠️ **What's Needed:**

- 15 more backend controllers to test
- 96 more frontend components to test
- Complete E2E test implementation
- Integration tests for critical flows
- Performance/load testing
- Security testing

### Estimated Effort

- **Backend**: ~15-20 hours for remaining controllers
- **Frontend**: ~30-40 hours for remaining components
- **E2E/Integration**: ~10-15 hours
- **CI/CD Enhancements**: ~5-10 hours
- **Total**: ~60-85 hours

### Next Steps

1. **Run existing tests** to establish baseline

   ```bash
   .\run-all-tests.ps1
   ```

2. **Review coverage reports** to identify gaps

   ```bash
   npm run test:coverage  # Frontend
   dotnet test --collect:"XPlat Code Coverage"  # Backend
   ```

3. **Prioritize high-value tests**
   - Critical user flows (ordering, payment)
   - Complex business logic (pricing, analytics)
   - Security-critical paths (auth, authorization)

4. **Implement tests incrementally**
   - One component/controller at a time
   - Run tests frequently
   - Maintain high coverage as you add features

5. **Monitor CI/CD pipeline**
   - Ensure tests pass on every commit
   - Review coverage trends
   - Fix failing tests immediately

---

**Version**: 1.1
**Last Updated**: 2026-10-08
**Status**: Active Development

For questions or issues with testing, please contact the development team or submit an issue on GitHub.
