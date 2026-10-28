# Testing Documentation - 2026 C#Capstone - Sushi Toshi

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

This document covers the automated tests for the Sushi Toshi Restaurant Management System: what exists, how to run it, how to add to it, and what CI checks on every pull request.

### Testing Pyramid

- **Unit Tests**: Back-end controllers, services, helpers, DTOs and seeders; front-end components, hooks and utilities
- **Integration Tests**: API endpoints running in the real app pipeline
- **E2E Tests**: Critical user flows in a real browser

### Testing Philosophy

- Tests are isolated and independent
- Tests follow AAA pattern (Arrange, Act, Assert)
- Tests have descriptive names (`MethodName_Scenario_ExpectedResult`)
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
- **Integration Host**: `WebApplicationFactory<Program>` (Microsoft.AspNetCore.Mvc.Testing)
- **Code Coverage**: Coverlet

**Test Projects:**

```
src/
├── back-end.Tests/              # Unit tests
│   ├── Controllers/             # Controller tests (incl. analytics, auth, staff location lock)
│   ├── Services/                # Service tests
│   ├── Helpers/                 # Helper tests (bill calculator, order limits, claims, date ranges)
│   ├── DTO/                     # DTO validation tests
│   └── Seeders/                 # Seed data rule tests
└── back-end.IntegrationTests/   # Integration tests (QR codes, guest login rate limiting)
```

### Frontend Testing Stack

**Framework & Tools:**

- **Unit Testing**: Jest + React Testing Library
- **E2E Testing**: Playwright
- **Component Stories**: Storybook, with story tests run by Vitest in a headless Chromium browser
- **Assertion Library**: @testing-library/jest-dom
- **User Interaction**: @testing-library/user-event
- **Code Coverage**: Istanbul (built into Jest)

**Configuration Files:**

- `src/front-end/jest.config.js` - Jest configuration (timeouts, path aliases, coverage thresholds)
- `src/front-end/jest.setup.js` - Test environment setup (jest-dom, `matchMedia` and other jsdom polyfills)
- `src/front-end/playwright.config.js` - E2E test configuration
- `src/front-end/vitest.config.js` - Storybook story tests
- `src/front-end/.storybook/` - Storybook configuration

**Test Organization:**

Tests live in `__tests__` folders next to the code they test:

```
src/front-end/
├── src/
│   ├── components/
│   │   ├── __tests__/           # Shared layout components (AppBarWithTitle)
│   │   ├── admin/__tests__/     # Admin pages and dialogs (incl. pricing/__tests__)
│   │   ├── analytics/__tests__/ # Analytics reports
│   │   ├── auth/__tests__/      # Login, register, QR login redirect
│   │   ├── bills/__tests__/
│   │   ├── common/__tests__/    # Shared dialogs and management page
│   │   ├── customer/__tests__/
│   │   ├── home/__tests__/
│   │   ├── location/__tests__/
│   │   ├── menu/__tests__/
│   │   ├── staff/               # Order, Session and Table dashboards (each has __tests__)
│   │   └── tags/__tests__/
│   ├── hooks/__tests__/
│   ├── services/__tests__/
│   ├── utils/__tests__/
│   └── stories/                 # Storybook stories
└── e2e/                         # Playwright E2E tests
    └── qr-code-generation.spec.js
```

---

## Current Test Coverage

Test counts change with almost every PR, so they aren't listed here. Run the suites (see [Running Tests](#running-tests)) to see current numbers.

### Backend

**Controllers with tests:**

- AdminQrCodeController
- AuthController (email login, guest login)
- BillController (access, gratuity, price snapshots, bill requests, leftover fees)
- CategoryController and TagController
- CustomerController
- DashboardController and DiningSessionController (session join, active session lookup, menu access)
- HolidayController
- LocationController
- MenuController
- MenuItemViewController
- OrderController (table orders, order limit enforcement)
- PricingController
- ServiceRequestController
- StaffController
- Analytics: BrowsingBehavior, ItemPerformance, OrderTiming and TableTurnover controllers
- Staff location lock, which covers Dashboard, DiningSession, MenuAssignment, Order, ServiceRequest and TableEntity controllers

**Services with tests:** QrGeneratorService, LocationAccessService, PricingService (restaurant time), and the four analytics services through their controller tests.

**Other unit tests:** BillCalculator, OrderLimitRules, ClaimsHelpers, analytics date ranges, DTO validation, and seeder rules.

**Integration tests:** QR code endpoints and guest login rate limiting.

**Not yet tested:**

- Controllers: MenuItemController, MenuLocationController, SessionController, SessionParticipantController, TableController, TableGroupController. MenuAssignmentController and TableEntityController are only covered by the staff location lock tests.
- Services: AuthTokenService, HolidayService, CanadaHolidaysClient, SendGridEmailService

### Frontend

**Covered areas:**

- Auth: LoginForm, RegisterForm, QrLoginRedirect
- Admin: categories, customers, holidays, menu items, menu assignment, QR codes, staff, tags, pricing profiles
- Analytics: nav and all four reports
- Staff: Order, Session and Table dashboards, plus the bill summary, new bill and leftover fee dialogs
- Customer: bill dialog, side and bottom nav, service request form
- Menu: item card, cart line, quantity stepper, review order button, item view tracking
- Common: AppDialog, ConfirmDialog, ManagementPage, AppBarWithTitle
- Hooks: useAutoRefresh, useOrderActions
- Utils and services: auth, serverMessage, tableVisit, tableSession

**Not yet tested (highest value first):**

- Auth: ForgotPasswordForm, ResetPasswordForm, VerifyEmailForm
- Location management: LocationManagement, LocationForm, MenuManagement, MenuForm, MenuAssignmentForm, AddLocationToMenu
- Staff dialogs: NewSessionDialog, SessionDetailDialog, AddTableDialog, OrderDetailDialog, and the TableDashboard dialogs
- Admin: MenuItemForm, PasswordChangeDialog, DefaultPricing
- Analytics: AnalyticsOverview, ReportExport
- Hooks: useAuth, useItemViewTracker

### E2E

One spec, `e2e/qr-code-generation.spec.js`. It covers the login page, admin navigation to QR codes, the customer ordering flow and mobile layout.

### Coverage thresholds

`jest.config.js` enforces a low baseline so the build fails if coverage drops:

| Metric     | Threshold |
| ---------- | --------- |
| Branches   | 3%        |
| Functions  | 2%        |
| Lines      | 3%        |
| Statements | 3%        |

Raise these as coverage grows. The long-term target is 70% for each metric (see [Coverage Goals](#coverage-goals)).

---

## Running Tests

### Backend Tests

#### Prerequisites

- .NET 9.0 SDK
- Runs on Windows, macOS and Linux. CI runs backend tests on Windows. QR code generation uses SkiaSharp, and `SkiaSharp.NativeAssets.Linux` is referenced in `back-end.csproj` for Linux.
- Integration tests start the real app, which validates `SendGrid:ApiKey` on startup. Locally this comes from user secrets; CI supplies a placeholder value.

#### Run All Backend Tests

```bash
# From project root
dotnet test src/back-end.Tests/back-end.Tests.csproj
dotnet test src/back-end.IntegrationTests/back-end.IntegrationTests.csproj
```

#### Run Specific Test File

```bash
dotnet test src/back-end.Tests --filter "FullyQualifiedName~EmailLoginTests"
dotnet test src/back-end.Tests --filter "FullyQualifiedName~QrGeneratorServiceTests"
```

#### Run with Detailed Output

```bash
dotnet test src/back-end.Tests --logger "console;verbosity=detailed"
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

#### Storybook

```bash
# Browse component stories at http://localhost:6006
npm run storybook

# Run the story tests (headless Chromium)
npx vitest --project=storybook
```

Story tests are not run in CI.

### End-to-End Tests

> **E2E tests run in CI** on every PR and are a required check. Run them locally before opening a PR that changes a tested flow.

#### Prerequisites

- MariaDB running (via `docker-compose`)
- Backend server running on `http://localhost:5264`
- Playwright browsers installed (below)

Playwright starts the frontend itself (`npm run dev` locally, `npm run start` in CI), or reuses one already running on `http://localhost:3000`.

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

# Chromium only (what CI runs)
npx playwright test --project=chromium
```

### Run All Tests (PowerShell Script)

`run-all-tests.ps1` in the project root runs the backend unit tests, backend integration tests and frontend unit tests in order, then prints a pass/fail summary. It does not run E2E tests.

```powershell
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

Controllers that filter by the signed-in staff member's location take an `ILocationAccessService`. Most tests pass a real `new LocationAccessService(_context)` so the location rules are exercised too. See `StaffLocationLockTests.cs` for examples.

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

#### Integration Test Template

```csharp
public class YourIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public YourIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Endpoint_Scenario_ExpectedResult()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/your-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

See `QrCodeIntegrationTests.cs` and `GuestLoginRateLimitTests.cs` for complete examples.

### Frontend Testing Patterns

#### Component Test Template

Components call the backend through the shared axios instance in `@/config/api`, so tests mock that module rather than `axios` itself.

```jsx
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import YourComponent from "@/components/area/YourComponent";
import api from "@/config/api";
import { useAuth } from "@/hooks/useAuth";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

jest.mock("next/navigation", () => ({
  useRouter: jest.fn(() => ({ push: jest.fn() })),
}));

describe("YourComponent", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAuth.mockReturnValue({ userRole: "admin", userEmail: "admin@example.com" });
  });

  it("should render component correctly", async () => {
    // Arrange
    api.get.mockResolvedValueOnce({ data: [] });

    // Act
    render(<YourComponent />);

    // Assert
    expect(await screen.findByText("Expected Text")).toBeInTheDocument();
  });

  it("should handle user interaction", async () => {
    // Arrange
    const user = userEvent.setup();
    api.get.mockResolvedValue({ data: [] });
    api.post.mockResolvedValueOnce({ data: {} });
    render(<YourComponent />);

    // Act
    await user.click(screen.getByRole("button", { name: /submit/i }));

    // Assert
    await waitFor(() => {
      expect(api.post).toHaveBeenCalled();
    });
  });

  it("should handle API errors", async () => {
    // Arrange
    api.get.mockRejectedValueOnce(new Error("API Error"));

    // Act
    render(<YourComponent />);

    // Assert
    expect(await screen.findByText(/error/i)).toBeInTheDocument();
  });
});
```

Components that use the MUI `DataGrid` need a little extra setup in jsdom (virtualization off, a `ResizeObserver` stub). Copy it from `src/components/admin/__tests__/CustomerManagement.test.jsx`.

See [Frontend Testing Guide](frontend-testing.md) for more templates and patterns.

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

A new push to a branch or PR cancels its older, still-running workflow run.

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

#### 3. E2E tests - Playwright, Chromium (`ubuntu-latest`) - required

```yaml
- Runs after the backend and frontend jobs pass
- Starts MariaDB as a service container on port 3307
- Installs libfontconfig1 (needed by SkiaSharp for QR codes)
- Starts the API on port 5264 and waits until /db-test confirms the database
  (the API applies migrations and seeds the empty database on startup)
- Builds the frontend; Playwright starts it with `npm run start`
- Runs Playwright with Chromium only
- Uploads the "playwright-report" artifact (screenshots of failures), even when tests fail
```

**Not in CI:** Codecov (removed) and Storybook story tests. Results and coverage are shown in each job's log.

### Pipeline Triggers

**Automatic Execution** (all three jobs):

- Pull requests to `main`
- Pushes to `main`, i.e. after a PR is merged

**Manual Execution:**

- Navigate to Actions tab in GitHub
- Select "Automated Tests" workflow
- Click "Run workflow", choose the branch, and run

### Required Checks (branch protection on `main`)

PRs cannot be merged until all of these pass. If a check fails, the PR author fixes it and pushes again:

- `Backend - build, unit & integration tests`
- `Frontend - lint, unit tests & build`
- `E2E tests - Playwright, Chromium`

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
dotnet test --collect:"XPlat Code Coverage"
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

Coverage is collected from `src/**`, excluding `*.stories.*` files. The thresholds that fail the run are listed under [Coverage thresholds](#coverage-thresholds).

### Coverage Goals

#### Backend

- **Overall**: 80%+
- **Service Layer**: 90%+ (critical business logic)
- **Controllers**: 80%+
- **Critical Paths**: 100%
  - QR code generation
  - Authentication
  - Authorization and staff location access
  - Bill calculation

#### Frontend

- **Overall**: 70%+
- **Critical Components**: 90%+
  - QRCodeManagement
  - MenuItemManagement
  - Staff dashboards (Order, Session, Table)
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

5. **Mock API Calls** through `@/config/api`
   ```jsx
   api.get.mockResolvedValueOnce({ data: mockData });
   api.post.mockRejectedValueOnce(new Error("API Error"));
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

**Issue**: QR code tests fail on Linux

```
Solution: QrGeneratorService uses SkiaSharp. back-end.csproj already references
SkiaSharp.NativeAssets.Linux, but it also needs the font library installed:
sudo apt-get install -y libfontconfig1
```

**Issue**: Integration tests fail with `OptionsValidationException` (SendGrid)

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

**Issue**: Tests time out

```
Solution: jest.config.js already allows 30 seconds per test (testTimeout: 30000),
because userEvent and MUI tests are slow under --coverage. If a test still times
out, it is usually waiting on an API mock that was never set up
(e.g. api.get returning undefined).
```

**Issue**: "Not wrapped in act(...)" warnings

```
Solution: Use waitFor or findBy for async operations
expect(await screen.findByText('Loaded')).toBeInTheDocument();
```

**Issue**: Router mock not working

```
Solution: Mock Next.js router properly
jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: jest.fn(),
  }),
  usePathname: () => '/',
}));
```

#### E2E Tests

**Issue**: E2E tests time out

```
Solution:
1. Ensure backend is running on http://localhost:5264
2. Ensure MariaDB is running and the backend can reach it (http://localhost:5264/db-test)
3. Increase timeout in playwright.config.js
```

**Issue**: E2E job fails in CI at "Start backend"

```
Solution: Open the "Start backend" step log - the API log is printed there when it fails.
- Check the MariaDB service started (job "Initialize containers" step)
- Check the connection string env var in the e2e-tests job matches the service settings
```

**Issue**: Selectors not found

```
Solution: Prefer role-based locators, which survive markup changes
page.getByRole('button', { name: 'Sign In', exact: true })
page.getByRole('heading', { name: /start your order/i })
```

---

## Resources

### Documentation

- [Frontend Testing Guide](frontend-testing.md)

### Framework Documentation

#### Backend

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

#### Frontend

- [React Testing Library](https://testing-library.com/docs/react-testing-library/intro/)
- [Jest Documentation](https://jestjs.io/)
- [Testing Library Queries](https://testing-library.com/docs/queries/about)
- [userEvent API](https://testing-library.com/docs/user-event/intro)
- [Storybook Vitest Addon](https://storybook.js.org/docs/writing-tests/integrations/vitest-addon)
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

## Next Steps

1. **Fill the gaps** listed under [Not yet tested](#current-test-coverage), starting with the untested backend controllers and the auth forms.
2. **Add E2E flows** for staff (open a session, take an order, close a bill) and for customers ordering from a QR code.
3. **Raise the Jest coverage thresholds** in `jest.config.js` as coverage grows, so it can't slip back.
4. **Keep this document current** when tests, CI jobs or required checks change.

---

**Version**: 1.2
**Last Updated**: 2026-10-27
**Status**: Active Development

For questions or issues with testing, please contact the development team or submit an issue on GitHub.
