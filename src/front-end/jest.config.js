const nextJest = require("next/jest");

const createJestConfig = nextJest({
  // Provide the path to your Next.js app to load next.config.js and .env files in your test environment
  dir: "./",
});

// Add any custom config to be passed to Jest
const customJestConfig = {
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js"],
  testEnvironment: "jest-environment-jsdom",
  // userEvent and MUI tests are slow under --coverage on busy machines (Windows, CI):
  // allow more than Jest's default 5 s per test, and leave CPU for each worker
  testTimeout: 30000,
  maxWorkers: "50%",
  moduleNameMapper: {
    "^@/(.*)$": "<rootDir>/src/$1",
    "^@/components/(.*)$": "<rootDir>/src/components/$1",
    "^@/config/(.*)$": "<rootDir>/src/config/$1",
  },
  testMatch: ["**/__tests__/**/*.[jt]s?(x)", "**/?(*.)+(test).[jt]s?(x)"],
  testPathIgnorePatterns: ["/node_modules/", "/e2e/", "/.next/"],
  collectCoverageFrom: [
    "src/**/*.{js,jsx,ts,tsx}",
    "!src/**/*.d.ts",
    "!src/**/_*.{js,jsx,ts,tsx}",
    "!src/**/*.stories.{js,jsx,ts,tsx}",
  ],
  // Coverage thresholds: Currently set to baseline levels (3-4%)
  // 4 of 99+ components are tested (LoginForm, RegisterForm, Header, QRCodeManagement)
  // All 87 tests pass successfully
  // TODO: Gradually increase thresholds as more tests are added
  // Target: 70% for all metrics (see TESTING.md for roadmap)
  coverageThreshold: {
    global: {
      branches: 3,
      functions: 2,
      lines: 3,
      statements: 3,
    },
  },
};

// createJestConfig is exported this way to ensure that next/jest can load the Next.js config which is async
module.exports = createJestConfig(customJestConfig);
