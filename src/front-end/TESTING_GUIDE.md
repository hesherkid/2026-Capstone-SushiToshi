# Frontend Testing Guide

## Overview

This guide provides instructions and templates for creating unit tests for all React components in the project.

## Testing Stack

- **Jest**: Test runner and assertion library
- **React Testing Library**: Component testing utilities
- **@testing-library/user-event**: User interaction simulation
- **@testing-library/jest-dom**: Additional matchers

## Running Tests

```bash
# Run all tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with coverage
npm run test:coverage

# Run E2E tests (Playwright)
npm run test:e2e
```

## File Structure

Place test files in `__tests__` directories next to the components:

```
src/components/
├── MyComponent.jsx
└── __tests__/
    └── MyComponent.test.jsx
```

## Test Template

### Basic Component Test Template

```javascript
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ComponentName from "../ComponentName";

// Mock dependencies
jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
  usePathname: jest.fn(),
}));

jest.mock("@/hooks/useAuth", () => ({
  useAuth: jest.fn(),
}));

describe("ComponentName", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("Rendering", () => {
    it("renders the component", () => {
      render(<ComponentName />);

      expect(screen.getByText(/expected text/i)).toBeInTheDocument();
    });

    it("renders with props", () => {
      render(<ComponentName prop1="value1" />);

      expect(screen.getByText("value1")).toBeInTheDocument();
    });
  });

  describe("User Interaction", () => {
    it("handles button click", async () => {
      const user = userEvent.setup();
      const mockCallback = jest.fn();

      render(<ComponentName onClick={mockCallback} />);

      const button = screen.getByRole("button");
      await user.click(button);

      expect(mockCallback).toHaveBeenCalledTimes(1);
    });

    it("handles input change", async () => {
      const user = userEvent.setup();
      render(<ComponentName />);

      const input = screen.getByLabelText(/input label/i);
      await user.type(input, "test value");

      expect(input).toHaveValue("test value");
    });
  });

  describe("Conditional Rendering", () => {
    it("shows element when condition is true", () => {
      render(<ComponentName showElement={true} />);

      expect(screen.getByText(/conditional element/i)).toBeInTheDocument();
    });

    it("hides element when condition is false", () => {
      render(<ComponentName showElement={false} />);

      expect(
        screen.queryByText(/conditional element/i),
      ).not.toBeInTheDocument();
    });
  });

  describe("Accessibility", () => {
    it("has proper labels", () => {
      render(<ComponentName />);

      expect(screen.getByLabelText(/label text/i)).toBeInTheDocument();
    });

    it("has proper aria attributes", () => {
      render(<ComponentName />);

      const element = screen.getByRole("button");
      expect(element).toHaveAttribute("aria-label", "expected label");
    });
  });
});
```

### Form Component Test Template

```javascript
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import FormComponent from "../FormComponent";

jest.mock("@/utils/api", () => ({
  submitForm: jest.fn(),
}));

describe("FormComponent", () => {
  const mockSubmit = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("Form Rendering", () => {
    it("renders all form fields", () => {
      render(<FormComponent onSubmit={mockSubmit} />);

      expect(screen.getByLabelText(/field 1/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/field 2/i)).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /submit/i }),
      ).toBeInTheDocument();
    });

    it("fields are initially empty", () => {
      render(<FormComponent onSubmit={mockSubmit} />);

      expect(screen.getByLabelText(/field 1/i)).toHaveValue("");
    });
  });

  describe("Form Validation", () => {
    it("shows validation error for empty required field", async () => {
      const user = userEvent.setup();
      render(<FormComponent onSubmit={mockSubmit} />);

      const submitButton = screen.getByRole("button", { name: /submit/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/field is required/i)).toBeInTheDocument();
      });
    });

    it("validates email format", async () => {
      const user = userEvent.setup();
      render(<FormComponent onSubmit={mockSubmit} />);

      const emailInput = screen.getByLabelText(/email/i);
      await user.type(emailInput, "invalid-email");

      const submitButton = screen.getByRole("button", { name: /submit/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/invalid email/i)).toBeInTheDocument();
      });
    });
  });

  describe("Form Submission", () => {
    it("submits form with valid data", async () => {
      const user = userEvent.setup();
      const { submitForm } = require("@/utils/api");
      submitForm.mockResolvedValue({ success: true });

      render(<FormComponent onSubmit={mockSubmit} />);

      await user.type(screen.getByLabelText(/field 1/i), "value 1");
      await user.type(screen.getByLabelText(/field 2/i), "value 2");
      await user.click(screen.getByRole("button", { name: /submit/i }));

      await waitFor(() => {
        expect(mockSubmit).toHaveBeenCalledWith({
          field1: "value 1",
          field2: "value 2",
        });
      });
    });

    it("shows loading state during submission", async () => {
      const user = userEvent.setup();
      const { submitForm } = require("@/utils/api");
      submitForm.mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 1000)),
      );

      render(<FormComponent onSubmit={mockSubmit} />);

      await user.click(screen.getByRole("button", { name: /submit/i }));

      expect(screen.getByRole("progressbar")).toBeInTheDocument();
    });

    it("handles submission error", async () => {
      const user = userEvent.setup();
      const { submitForm } = require("@/utils/api");
      submitForm.mockRejectedValue(new Error("Submission failed"));

      render(<FormComponent onSubmit={mockSubmit} />);

      await user.click(screen.getByRole("button", { name: /submit/i }));

      await waitFor(() => {
        expect(screen.getByText(/submission failed/i)).toBeInTheDocument();
      });
    });
  });
});
```

### Context Provider Test Template

```javascript
import { render, screen } from "@testing-library/react";
import { ContextProvider, useContextName } from "../ContextProvider";

// Test component that consumes the context
const TestComponent = () => {
  const { value, setValue } = useContextName();
  return (
    <div>
      <span>{value}</span>
      <button onClick={() => setValue("new value")}>Update</button>
    </div>
  );
};

describe("ContextProvider", () => {
  it("provides context values to children", () => {
    render(
      <ContextProvider>
        <TestComponent />
      </ContextProvider>,
    );

    expect(screen.getByText(/initial value/i)).toBeInTheDocument();
  });

  it("updates context value", async () => {
    const user = userEvent.setup();
    render(
      <ContextProvider>
        <TestComponent />
      </ContextProvider>,
    );

    await user.click(screen.getByRole("button"));

    expect(screen.getByText("new value")).toBeInTheDocument();
  });
});
```

## Common Testing Patterns

### Testing Async Operations

```javascript
it("handles async data fetching", async () => {
  const mockData = { id: 1, name: "Test" };
  global.fetch = jest.fn(() =>
    Promise.resolve({
      json: () => Promise.resolve(mockData),
    }),
  );

  render(<Component />);

  await waitFor(() => {
    expect(screen.getByText("Test")).toBeInTheDocument();
  });
});
```

### Testing Error Boundaries

```javascript
it("renders error fallback on error", () => {
  const ThrowError = () => {
    throw new Error("Test error");
  };

  render(
    <ErrorBoundary>
      <ThrowError />
    </ErrorBoundary>,
  );

  expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
});
```

### Testing with Router

```javascript
import { useRouter } from "next/navigation";

jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
}));

it("navigates on button click", async () => {
  const mockPush = jest.fn();
  useRouter.mockReturnValue({ push: mockPush });

  render(<Component />);

  await userEvent.click(screen.getByRole("button"));

  expect(mockPush).toHaveBeenCalledWith("/expected-route");
});
```

### Testing Material-UI Components

```javascript
it("opens dialog", async () => {
  render(<ComponentWithDialog />);

  await userEvent.click(screen.getByRole("button", { name: /open/i }));

  expect(screen.getByRole("dialog")).toBeInTheDocument();
});

it("selects from dropdown", async () => {
  render(<ComponentWithSelect />);

  const select = screen.getByLabelText(/select label/i);
  await userEvent.click(select);

  const option = await screen.findByRole("option", { name: /option 1/i });
  await userEvent.click(option);

  expect(select).toHaveTextContent("Option 1");
});
```

## Best Practices

1. **Test Behavior, Not Implementation**
   - Focus on what the user sees and does
   - Avoid testing internal state directly
   - Use screen queries that match user experience

2. **Use Semantic Queries**

   ```javascript
   // Good - accessible and user-centric
   screen.getByRole("button", { name: /submit/i });
   screen.getByLabelText(/email/i);
   screen.getByText(/welcome/i);

   // Avoid - implementation details
   container.querySelector(".submit-button");
   getByTestId("submit-btn");
   ```

3. **Async Testing**
   - Always use `waitFor` for async operations
   - Use `findBy` queries for elements that appear asynchronously
   - Don't use `act()` manually (React Testing Library handles it)

4. **Clean Up**
   - Tests automatically clean up after each test
   - Clear mocks in `beforeEach`
   - Reset module state when needed

5. **Accessibility**
   - Test with screen readers in mind
   - Verify ARIA attributes
   - Check keyboard navigation

## Coverage Goals

Aim for the following coverage thresholds (configured in jest.config.js):

- **Branches**: 70%
- **Functions**: 70%
- **Lines**: 70%
- **Statements**: 70%

## Component Test Checklist

For each component, ensure you test:

- [ ] Component renders without crashing
- [ ] Component renders with props
- [ ] User interactions (clicks, inputs, etc.)
- [ ] Conditional rendering
- [ ] Loading states
- [ ] Error states
- [ ] Form validation (if applicable)
- [ ] Form submission (if applicable)
- [ ] Accessibility features
- [ ] Navigation/routing
- [ ] API calls (mocked)
- [ ] Edge cases

## Common Matchers

```javascript
// Presence
expect(element).toBeInTheDocument();
expect(element).toBeVisible();
expect(element).toBeNull();

// Text
expect(element).toHaveTextContent("text");
expect(element).toHaveValue("value");

// Attributes
expect(element).toHaveAttribute("href", "/path");
expect(element).toHaveClass("className");
expect(element).toBeDisabled();
expect(element).toBeEnabled();

// Form
expect(input).toHaveValue("value");
expect(checkbox).toBeChecked();

// Negation
expect(element).not.toBeInTheDocument();
```

## Debugging Tests

```javascript
// Print component HTML
const { debug } = render(<Component />);
debug();

// Print specific element
debug(screen.getByRole("button"));

// Log available queries
screen.logTestingPlaygroundURL();

// Check what's rendered
screen.getByRole(""); // Will list all available roles
```

## Examples

See these test files for complete examples:

- `src/components/auth/__tests__/LoginForm.test.jsx`
- `src/components/auth/__tests__/RegisterForm.test.jsx`
- `src/components/__tests__/Header.test.jsx`

## Resources

- [React Testing Library Docs](https://testing-library.com/docs/react-testing-library/intro/)
- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Testing Library Cheatsheet](https://testing-library.com/docs/react-testing-library/cheatsheet)
- [Common Mistakes](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
