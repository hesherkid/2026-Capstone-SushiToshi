// @ts-check
const { defineConfig, devices } = require("@playwright/test");
const { ADMIN_STATE } = require("./e2e/accounts");

// Takes the admin manual's screenshots (npm run docs:screenshots). Not part of the E2E suite:
// the *.shots.js files don't match the main config's *.spec.js pattern.
module.exports = defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  reporter: "list",
  use: {
    baseURL: "http://localhost:3000",
    viewport: { width: 1280, height: 800 },
  },
  projects: [
    {
      name: "setup",
      testMatch: /auth\.setup\.js/,
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "screenshots",
      testMatch: /\.shots\.js/,
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1280, height: 800 },
        storageState: ADMIN_STATE,
      },
      dependencies: ["setup"],
    },
  ],
  webServer: {
    command: "npm run dev",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 120 * 1000,
  },
});
