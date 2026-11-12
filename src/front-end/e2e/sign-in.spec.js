// @ts-check
const { test, expect } = require("@playwright/test");
const { ADMIN, SIGNED_OUT, signIn } = require("./accounts");

// Here everyone starts signed out
test.use({ storageState: SIGNED_OUT });

// What a tables session QR code opens - see QRGeneratorService.cs
const TABLE_QR_URL = "/auth/login?locationId=1&tableNumber=5";

/** @param {import('@playwright/test').Page} page */
const continueAsGuest = (page) =>
  page.getByRole("button", { name: "Continue as Guest" });

test.describe("Sign-in page", () => {
  test("shows the sign-in form", async ({ page }) => {
    await page.goto("/auth/login");

    await expect(
      page.getByRole("heading", { name: "Start your order" }),
    ).toBeVisible();
    await expect(page.getByLabel("Email Address")).toBeVisible();
    await expect(page.getByLabel(/^Password/)).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Sign In", exact: true }),
    ).toBeEnabled();
  });

  test("only offers guest sign-in from a table's QR code", async ({ page }) => {
    await page.goto("/auth/login");

    await expect(continueAsGuest(page)).toBeDisabled();
    await expect(
      page.getByText("To continue as a guest, scan the QR code on your table."),
    ).toBeVisible();
  });

  test("explains a failed sign-in and stays on the page", async ({ page }) => {
    await signIn(page, {
      email: "nobody@example.com",
      password: "fake-password",
    });

    await expect(page.locator(".MuiAlert-root")).toBeVisible();
    await expect(page.locator(".MuiAlert-root")).not.toBeEmpty();
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test("signs the admin in and opens the admin home page", async ({ page }) => {
    await signIn(page, ADMIN);

    await expect(
      page.getByRole("heading", { level: 1, name: /^Welcome back/ }),
    ).toBeVisible();
    expect(new URL(page.url()).pathname).toBe("/");
  });
});

test.describe("Table QR code landing", () => {
  test("opens sign-in with guest sign-in available for that table", async ({
    page,
  }) => {
    await page.goto(TABLE_QR_URL);

    await expect(
      page.getByRole("heading", { name: "Start your order" }),
    ).toBeVisible();
    await expect(continueAsGuest(page)).toBeEnabled();
    await expect(
      page.getByText("To continue as a guest, scan the QR code on your table."),
    ).toBeHidden();
  });

  test("keeps the table in the address", async ({ page }) => {
    await page.goto(TABLE_QR_URL);

    // toHaveURL keeps checking, page may rewrite the address after loading
    await expect(page).toHaveURL(/[?&]locationId=1(&|$)/);
    await expect(page).toHaveURL(/[?&]tableNumber=5(&|$)/);
  });

  test("keeps the table when the customer goes to Sign Up", async ({
    page,
  }) => {
    await page.goto(TABLE_QR_URL);
    await page.getByRole("link", { name: "Sign Up" }).click();

    await expect(page).toHaveURL(/\/auth\/register\?/);
    await expect(page).toHaveURL(/[?&]locationId=1(&|$)/);
    await expect(page).toHaveURL(/[?&]tableNumber=5(&|$)/);
  });
});
