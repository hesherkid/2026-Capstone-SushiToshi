// @ts-check
const { test, expect } = require("@playwright/test");

// Admin tests start signed in as an admin user
const LOCATIONS = ["North Side Branch", "South Side Branch"];
const TABLES_PER_LOCATION = 34;

/** @param {import('@playwright/test').Page} page */
const pageHeading = (page) =>
  page.getByRole("heading", { name: "QR Code Management" });

// Select location and table panel. Scoped so that the app bar's own location dropdown does not interfere.
/** @param {import('@playwright/test').Page} page */
const tablePicker = (page) =>
  page.locator(".MuiPaper-root", {
    has: page.getByRole("heading", { name: "Individual QR Code Generation" }),
  });

// Wifi and Session cards each get there own preview and download buttons
/**
 * @param {import('@playwright/test').Page} page
 * @param {"WiFi QR Code" | "Session QR Code"} title
 */
const qrCard = (page, title) =>
  page.locator(".MuiCard-root", {
    has: page.getByRole("heading", { name: title }),
  });

// Each worker uses its own table, so two browsers running at once never regenerate the same QR code simultaneously.
const ourTable = () => String(test.info().parallelIndex + 1);

test.describe("QR code management", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/admin/qr-codes");
    await expect(pageHeading(page)).toBeVisible();
  });

  test("lets admin select either location and shows its tables", async ({
    page,
  }) => {
    const location = tablePicker(page).getByRole("combobox");
    await expect(location).toHaveText(new RegExp(LOCATIONS.join("|")));

    await location.click();
    for (const locationName of LOCATIONS) {
      await expect(
        page.getByRole("option", { name: locationName }),
      ).toBeVisible();
    }
    await page.getByRole("option", { name: LOCATIONS[1] }).click();

    await expect(location).toHaveText(LOCATIONS[1]);
    await expect(
      page.getByText(`${TABLES_PER_LOCATION} tables found at ${LOCATIONS[1]}`),
    ).toBeVisible();
  });

  test("needs a table number before a code can be previewed or downloaded", async ({
    page,
  }) => {
    const cards = [
      qrCard(page, "WiFi QR Code"),
      qrCard(page, "Session QR Code"),
    ];

    for (const card of cards) {
      await expect(
        card.getByRole("button", { name: "Preview" }),
      ).toBeDisabled();
      await expect(
        card.getByRole("button", { name: "Download" }),
      ).toBeDisabled();
    }

    await tablePicker(page).getByLabel("Table Number").fill(ourTable());

    for (const card of cards) {
      await expect(card.getByRole("button", { name: "Preview" })).toBeEnabled();
      await expect(
        card.getByRole("button", { name: "Download" }),
      ).toBeEnabled();
      await expect(card).toContainText(`Table: ${ourTable()}`);
    }
  });

  test("previews a table's session QR code, then closes it", async ({
    page,
  }) => {
    await tablePicker(page).getByLabel("Table Number").fill(ourTable());
    await qrCard(page, "Session QR Code")
      .getByRole("button", { name: "Preview" })
      .click();

    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();

    // The alt text is the dialog title, "Session QR Code - Location 1, Table 5"
    const image = dialog.getByRole("img", {
      name: new RegExp(
        `^Session QR Code - Location \\d+, Table ${ourTable()}$`,
      ),
    });
    await expect(image).toBeVisible();
    // The decode actual API PNG is loaded
    await expect
      .poll(() =>
        image.evaluate(
          (img) => /** @type {HTMLImageElement} */ (img).naturalWidth,
        ),
      )
      .toBeGreaterThan(0);

    await dialog
      .getByRole("button")
      .filter({ hasText: /^Close$/ })
      .click();
    await expect(dialog).toBeHidden();
  });

  test("downloads a table's WiFi QR code as a PNG", async ({ page }) => {
    await tablePicker(page).getByLabel("Table Number").fill(ourTable());

    const downloadStarted = page.waitForEvent("download");
    await qrCard(page, "WiFi QR Code")
      .getByRole("button", { name: "Download" })
      .click();
    const download = await downloadStarted;

    // QRCodeManagement.jsx names it `${type}_L${location}_T${table}.png`
    expect(download.suggestedFilename()).toMatch(
      new RegExp(`^wifi_L\\d+_T${ourTable()}\\.png$`),
    );
    await expect(
      page.getByText("WiFi QR code downloaded successfully!"),
    ).toBeVisible();
  });
});

test.describe("QR code management on a phone", () => {
  // Runs in the Mobile Chrome and Mobile Safari projects only
  test.skip(({ isMobile }) => !isMobile, "Mobile projects only");

  test("fits the screen without sideways scrolling", async ({ page }) => {
    await page.goto("/admin/qr-codes");
    await expect(pageHeading(page)).toBeVisible();

    const overflow = await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    );
    expect(overflow, "page is wider than the screen").toBeLessThanOrEqual(0);

    await expect(
      qrCard(page, "Session QR Code").getByRole("button", { name: "Preview" }),
    ).toBeVisible();
  });
});
