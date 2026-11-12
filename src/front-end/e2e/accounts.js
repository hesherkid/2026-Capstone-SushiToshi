// Accounts and sign in shared by E2E tests
// Seeded in development and used for E2E tests
// Override with E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD to test against another DB.
const ADMIN = {
  email: process.env.E2E_ADMIN_EMAIL || "admin.user@sushitoshi.ca",
  password: process.env.E2E_ADMIN_PASSWORD || "AdminPass123!",
};

// auth.setup.js saves the admin's signed-in browser state added to git-ignore
const ADMIN_STATE = "playwright/.auth/admin.json";

// Signed-out browser state, for tests that must not start signed in
const SIGNED_OUT = { cookies: [], origins: [] };

/**
 * Sign in through the login form - see components/auth/LoginForm.jsx.
 * @param {import('@playwright/test').Page} page
 * @param {{ email: string, password: string }} account
 */
async function signIn(page, { email, password }) {
  await page.goto("/auth/login");
  await page.getByLabel("Email Address").fill(email);
  // A regex so the "Show password" button isn't matched too
  await page.getByLabel(/^Password/).fill(password);
  await page.getByRole("button", { name: "Sign In", exact: true }).click();
}

module.exports = { ADMIN, ADMIN_STATE, SIGNED_OUT, signIn };
