// @ts-check
const { test: setup, expect } = require("@playwright/test");
const { ADMIN, ADMIN_STATE, signIn } = require("./accounts");

// Runs before the browser projects - see "setup" in playwright.config.js
// Signing in once keeps the runner under 20 sign ins per minute
setup("sign in as the admin", async ({ page }) => {
  await signIn(page, ADMIN);

  // AdminHome.jsx: wait for the home page so the token is saved before the state is captured
  await expect(
    page.getByRole("heading", { level: 1, name: /^Welcome back/ }),
  ).toBeVisible();

  // The token is in localStorage ("authToken"), which storageState saves
  await page.context().storageState({ path: ADMIN_STATE });
});
