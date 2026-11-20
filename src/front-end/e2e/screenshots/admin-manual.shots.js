// @ts-check
const path = require("path");
const { test, expect } = require("@playwright/test");
const { SIGNED_OUT, signIn } = require("../accounts");

// Screenshots for docs/admin/admin-manual.md, taken from the seeded Development data.
// Run with the backend running: npm run docs:screenshots
const OUT = path.resolve(__dirname, "../../../../docs/admin/images");

/** @param {string} name */
const file = (name) => path.join(OUT, `${name}.png`);

// Amber outline on the control a section is about; shows on both the red app bar and white pages
/** @param {import('@playwright/test').Locator} locator */
const highlight = (locator) =>
  locator.evaluate((el) => {
    el.style.outline = "3px solid #ffb300";
    el.style.outlineOffset = "3px";
  });

// Let data load and animations finish, and hide what isn't part of the app:
// the Next.js dev indicator, DevResetButton, the skip link and toasts
/** @param {import('@playwright/test').Page} page */
const settle = async (page) => {
  await page.waitForLoadState("networkidle");
  await page.addStyleTag({
    content: `nextjs-portal, a[href="#main-content"], .MuiSnackbar-root { display: none !important; }`,
  });
  await page
    .getByText("DEV · Reset")
    .evaluateAll((els) =>
      els.forEach((el) =>
        (el.closest("button") ?? el).style.setProperty("display", "none"),
      ),
    );
  await page.waitForTimeout(500);
};

/** @param {import('@playwright/test').Page} page */
const openSessions = async (page) => {
  await page.goto("/staff/dashboard/sessions");
  await expect(
    page.getByRole("button", { name: /^View session/ }).first(),
  ).toBeVisible();
  await settle(page);
};

// A session with more than one bill shows the most in its dialog
/** @param {import('@playwright/test').Page} page */
const openBusiestSession = async (page) => {
  const multiBill = page.getByRole("row").filter({ hasText: / of [2-9]/ });
  const row =
    (await multiBill.count()) > 0
      ? multiBill.first()
      : page.getByRole("row").nth(1);
  await row.getByRole("button", { name: /^View session/ }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText(/^Dining Session #/)).toBeVisible();
  await settle(page);
  return dialog;
};

test("getting around", async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { level: 1, name: /^Welcome back/ }),
  ).toBeVisible();
  await settle(page);
  await highlight(page.getByLabel("Main menu"));
  await highlight(page.getByLabel(/^Working location/));
  await page.screenshot({ path: file("getting-around") });
});

test("sessions list", async ({ page }) => {
  await openSessions(page);
  await page.screenshot({ path: file("sessions-list") });
});

test("session details", async ({ page }) => {
  await openSessions(page);
  const dialog = await openBusiestSession(page);
  await highlight(
    dialog.getByRole("button", { name: /^Add order to/ }).first(),
  );
  await dialog.screenshot({ path: file("session-details") });
});

test("bill summary", async ({ page }) => {
  await openSessions(page);
  const sessionDialog = await openBusiestSession(page);
  await sessionDialog
    .getByRole("button", { name: /^View .* summary$/ })
    .first()
    .click();
  const summary = page.getByRole("dialog").last();
  await expect(summary).toBeVisible();
  await settle(page);
  await summary.screenshot({ path: file("bill-summary") });
});

// The whole MUI text field or select, not just its input
/** @param {import('@playwright/test').Locator} input */
const fieldOf = (input) =>
  input.locator(
    'xpath=ancestor::div[contains(@class,"MuiFormControl-root")][1]',
  );

// The checkbox and its label text
/** @param {import('@playwright/test').Locator} checkbox */
const checkboxRowOf = (checkbox) =>
  checkbox.locator("xpath=ancestor::label[1]");

// Opens each session with more than one open bill until `ready` finds what the shot needs.
// Closes the dialog and tries the next session when it doesn't.
/**
 * @param {import('@playwright/test').Page} page
 * @param {(dialog: import('@playwright/test').Locator) => Promise<boolean>} ready
 */
const openSessionWhere = async (page, ready) => {
  const rows = page.getByRole("row").filter({ hasText: / of [2-9]/ });
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    await rows
      .nth(i)
      .getByRole("button", { name: /^View session/ })
      .click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText(/^Dining Session #/)).toBeVisible();
    await settle(page);
    if (await ready(dialog)) return dialog;
    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
  }
  throw new Error("No seeded session has what this screenshot needs");
};

// The seed never puts people on bills or marks a bill as set by a guest, so these shots
// fill that in on the dashboard response, in the browser only. Nothing is saved.
// In each session with two or more open bills, people are shared across the open bills
// (two sample guests if nobody joined), and the first open bill is marked "Started by guest".
// The keys used here, from DashboardSessionsDto, DashboardBillDto and DashboardPersonDto
/** @typedef {{ bill_id: number, status: string, people_on_bill?: number, started_by_guest?: boolean, total_guests?: number, adult_count?: number }} DashboardBill */
/** @typedef {{ participant_id: number, user_name: string, bill_id: number | null, is_guest?: boolean }} DashboardPerson */
/** @typedef {{ bills?: DashboardBill[], people?: DashboardPerson[] }} DashboardSession */
// splittable: gives each open bill at least 2 guests (as adults), so Move can change counts and offer
// "+ New bill" (a bill must keep one guest). Browser only, like the rest of this mock
/**
 * @param {import('@playwright/test').Page} page
 * @param {{ splittable?: boolean }} [options]
 */
const showPeopleOnBills = (page, { splittable = false } = {}) =>
  page.route(/\/api\/dashboard\/sessions/i, async (route) => {
    const response = await route.fetch();
    /** @type {DashboardSession[]} */
    const sessions = await response.json();
    for (const session of Array.isArray(sessions) ? sessions : []) {
      const open = (session.bills ?? []).filter((b) =>
        /open/i.test(String(b.status)),
      );
      if (open.length < 2) continue;
      if (!session.people?.length) {
        session.people = [
          {
            participant_id: -1,
            user_name: "Guest 4F2A",
            bill_id: null,
            is_guest: true,
          },
          {
            participant_id: -2,
            user_name: "Guest 9C1D",
            bill_id: null,
            is_guest: true,
          },
        ];
      }
      session.people.forEach(
        (person, i) => (person.bill_id = open[i % open.length].bill_id),
      );
      for (const bill of open) {
        bill.people_on_bill = session.people.filter(
          (p) => p.bill_id === bill.bill_id,
        ).length;
        const missing = 2 - (bill.total_guests ?? 0);
        if (splittable && missing > 0) {
          bill.adult_count = (bill.adult_count ?? 0) + missing;
          bill.total_guests = 2;
        }
      }
      open[0].started_by_guest = true;
    }
    await route.fulfill({ response, json: sessions });
  });

// A bill a guest started or changed, or with more people than guests
const GUEST_CHECK_CHIP =
  /^(Started by guest|Count changed by guest|\d+ people on \d+-guest bill)$/;

test("checking guest counts", async ({ page }) => {
  await showPeopleOnBills(page);
  await openSessions(page);
  const dialog = await openSessionWhere(
    page,
    async (d) => (await d.getByText(GUEST_CHECK_CHIP).count()) > 0,
  );
  const chip = dialog.getByText(GUEST_CHECK_CHIP).first();
  // The bill card: the nearest ancestor that also holds the bill's Edit Guests button
  const bill = chip.locator(
    'xpath=ancestor::*[.//button[starts-with(@aria-label, "Edit guests on")]][1]',
  );
  await highlight(chip);
  await highlight(bill.getByRole("button", { name: /^Edit guests on/ }));
  await highlight(
    bill.getByRole("button", { name: /^(Lock|Unlock) guests on/ }),
  );
  await bill.screenshot({ path: file("guest-check") });

  await bill.getByRole("button", { name: /^Edit guests on/ }).click();
  const edit = page.getByRole("dialog").last();
  await expect(edit.getByText(/^Edit Guests/)).toBeVisible();
  await settle(page);
  for (const input of await edit.getByRole("spinbutton").all()) {
    await highlight(fieldOf(input));
  }
  await edit.screenshot({ path: file("edit-guests-dialog") });
});

// Each Move button's row (its parent) shows the person's bill, or "Not on a bill yet"
/** @param {import('@playwright/test').Locator} d */
const onABill = (d) =>
  d
    .getByRole("button", { name: /^Move / })
    .locator("xpath=..")
    .filter({ hasNotText: "Not on a bill yet" });

test("moving people between bills", async ({ page }) => {
  await showPeopleOnBills(page, { splittable: true });
  await openSessions(page);
  // Someone already on a bill, so every option in the dialog shows
  const dialog = await openSessionWhere(
    page,
    async (d) => (await onABill(d).count()) > 0,
  );
  await onABill(dialog)
    .first()
    .getByRole("button", { name: /^Move / })
    .click();

  const move = page.getByRole("dialog").last();
  await move.getByLabel("Move to").click();
  // An existing bill; "+ New bill" has its own shot
  await page
    .getByRole("option")
    .filter({ hasNotText: /^\+ New bill/ })
    .first()
    .click();
  // The label only reads this way when the counts can change, so don't wait for it otherwise
  const changeCounts = move.getByRole("checkbox", {
    name: "Change both bills' guest counts",
  });
  if ((await changeCounts.count()) > 0 && (await changeCounts.isEnabled()))
    await changeCounts.check();
  await settle(page);

  await highlight(fieldOf(move.getByLabel("Move to")));
  for (const checkbox of await move.getByRole("checkbox").all()) {
    await highlight(checkboxRowOf(checkbox));
  }
  for (const label of ["Kind of guest", "How many"]) {
    const input = move.getByLabel(label);
    if (await input.count()) await highlight(fieldOf(input));
  }
  await move.screenshot({ path: file("move-person-dialog") });
});

test("splitting a bill", async ({ page }) => {
  await showPeopleOnBills(page, { splittable: true });
  await openSessions(page);
  const dialog = await openSessionWhere(
    page,
    async (d) => (await onABill(d).count()) > 0,
  );
  await onABill(dialog)
    .first()
    .getByRole("button", { name: /^Move / })
    .click();

  const move = page.getByRole("dialog").last();
  await move.getByLabel("Move to").click();
  await page.getByRole("option", { name: /^\+ New bill:/ }).click();
  const preview = move
    .getByRole("alert")
    .filter({ hasText: /starts with \d+ guests?\./ });
  await expect(preview).toBeVisible();
  await settle(page);

  await highlight(fieldOf(move.getByLabel("Move to")));
  await highlight(
    checkboxRowOf(
      move.getByRole("checkbox", { name: /count for the new bill$/ }),
    ),
  );
  await highlight(preview);
  await move.screenshot({ path: file("split-bill-dialog") });
});

test("merging bills", async ({ page }) => {
  await openSessions(page);
  const dialog = await openBusiestSession(page);
  await dialog
    .getByRole("button", { name: /^Merge .* into another bill$/ })
    .first()
    .click();

  const merge = page.getByRole("dialog").last();
  await merge.getByLabel("Merge into").click();
  await page.getByRole("option").first().click();
  await expect(merge.getByRole("alert").first()).toBeVisible();
  await settle(page);
  await highlight(fieldOf(merge.getByLabel("Merge into")));
  await merge.screenshot({ path: file("merge-bill-dialog") });
});

test("orders dashboard", async ({ page }) => {
  await page.goto("/staff/dashboard/orders");
  await expect(page.getByText("Orders Pending")).toBeVisible();
  await settle(page);
  await page.screenshot({ path: file("orders-dashboard") });
});

test("menu assignment dialog", async ({ page }) => {
  await page.goto("/admin/menu-items");
  const assign = page.getByRole("button", { name: /^Assign to Menus/ }).first();
  await expect(assign).toBeVisible();
  await settle(page);
  await assign.click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await settle(page);
  await dialog.screenshot({ path: file("menu-assignment-dialog") });
});

test("pricing form", async ({ page }) => {
  // Tall enough for the whole form, so the sticky save bar sits at the bottom
  await page.setViewportSize({ width: 1280, height: 1150 });
  await page.goto("/admin/pricing");
  await expect(
    page.getByRole("heading", { name: "Default Pricing" }),
  ).toBeVisible();
  await settle(page);
  await page.screenshot({ path: file("pricing-form") });
});

// The seed has no pending holidays, so this shows the whole list for the year
test("holidays", async ({ page }) => {
  await page.goto("/admin/holidays");
  await expect(page.getByRole("button", { name: "All" })).toBeVisible();
  await page.getByRole("button", { name: "All" }).click();
  await settle(page);
  await page.screenshot({ path: file("holidays") });
});

// Locally the server sees every request from one address, so the real dialog would only show its
// "can't tell phones apart" warning. This fills in what an admin on a restaurant's Wi-Fi sees, in the
// browser only, with a documentation address (203.0.113.0/24) so no real address ends up in the image.
// The keys are from WifiCheckResponseDto
test("Wi-Fi check dialog", async ({ page }) => {
  await page.route(/\/api\/location\/\d+\/wifi-check/i, (route) =>
    route.fulfill({
      json: {
        allowed_networks: [],
        your_address: "203.0.113.10",
        your_network: "203.0.113.10/32",
        can_see_client_addresses: true,
      },
    }),
  );
  await page.goto("/location/menu-location");
  const open = page.getByRole("button", { name: /^Wi-Fi check/ }).first();
  await expect(open).toBeVisible();
  await settle(page);
  await open.click();
  const dialog = page.getByRole("dialog");
  await expect(
    dialog.getByRole("button", { name: /^Add this network/ }),
  ).toBeVisible();
  await settle(page);
  await highlight(dialog.getByRole("button", { name: /^Add this network/ }));
  await dialog.screenshot({ path: file("wifi-check-dialog") });
});

// Bulk generation rewrites the saved QR images in the local backend's storage/qrcodes
test("bulk QR codes", async ({ page }) => {
  await page.goto("/admin/qr-codes");
  await page.getByLabel("Location", { exact: true }).click();
  await page.getByRole("option").first().click();
  const generate = page.getByRole("button", {
    name: /^Generate QR Codes for All/,
  });
  await expect(generate).toBeEnabled();
  await generate.click();
  await expect(
    page.getByRole("button", { name: /Download as PDF/ }),
  ).toBeVisible({ timeout: 60_000 });
  await settle(page);
  const bulk = page.locator(".MuiPaper-root", {
    has: page.getByRole("heading", { name: "Bulk QR Code Generation" }),
  });
  await bulk.screenshot({ path: file("qr-bulk") });
});

// ---- Free ice cream at the bill ----
// The seed never has a bill that said yes to ice cream, so these shots fill it in on the dashboard
// response, in the browser only, like showPeopleOnBills. Nothing is sent or saved.
// In the first session with two or more open bills, the first open bill asked for the bill 5 minutes
// ago and said yes (with 4 guests, so a few flavours fit), and its "Ready for bill" service request is
// added to the pending requests, worded as RequestBill writes it. sent: it's already had its ice cream
// instead. nobody: no bill said yes (the table asked a server in person), so only the smaller button
// by the Bills heading shows.
// The keys are from DashboardBillDto and ServiceRequestResponseDto
/**
 * @param {import('@playwright/test').Page} page
 * @param {{ sent?: boolean, nobody?: boolean }} [options]
 */
const showIceCreamYes = async (page, { sent = false, nobody = false } = {}) => {
  // The two load together: the request waits for the session it belongs to
  /** @type {(request: object | null) => void} */
  let resolveRequest = () => {};
  const askedRequest = new Promise((resolve) => (resolveRequest = resolve));

  await page.route(/\/api\/dashboard\/sessions/i, async (route) => {
    const response = await route.fetch();
    const sessions = await response.json();
    let request = null;
    for (const session of Array.isArray(sessions) ? sessions : []) {
      const open = (session.bills ?? []).filter((b) => /open/i.test(String(b.status)));
      for (const bill of session.bills ?? []) {
        bill.wants_ice_cream = false;
        bill.ice_cream_sent_at = null;
      }
      if (open.length < 2 || nobody || request) continue;
      const askedAt = new Date(Date.now() - 5 * 60_000).toISOString();
      const bill = open[0];
      bill.requested_at = askedAt;
      bill.wants_ice_cream = true;
      bill.ice_cream_sent_at = sent ? new Date(Date.now() - 2 * 60_000).toISOString() : null;
      if ((bill.total_guests ?? 0) < 4) {
        bill.adult_count = (bill.adult_count ?? 0) + (4 - (bill.total_guests ?? 0));
        bill.total_guests = 4;
      }
      if (!sent) {
        request = {
          request_id: -100,
          session_id: session.session_id,
          table_id: 0,
          requested_by: 0,
          claimed_by: null,
          notes: `Ready for bill: ${bill.bill_name} (wants free ice cream)`,
          status: "Pending",
          created_at: askedAt,
          claimed_at: null,
          completed_at: null,
          table_number: session.table_numbers?.[0] ?? 0,
        };
      }
    }
    resolveRequest(request);
    await route.fulfill({ response, json: sessions });
  });

  await page.route(/\/api\/servicerequest\/pending/i, async (route) => {
    const response = await route.fetch();
    const pending = await response.json();
    const request = await askedRequest;
    await route.fulfill({
      response,
      json: request ? [request, ...(Array.isArray(pending) ? pending : [])] : pending,
    });
  });
};

// The flavours a new restaurant starts with (the FreeIceCreamDefaults migration), so the shots don't
// depend on what's in the local database. Keys from IceCreamFlavourOptionDto
/** @param {import('@playwright/test').Page} page */
const showDefaultFlavours = (page) =>
  page.route(/\/api\/bill\/ice-cream-flavours/i, (route) =>
    route.fulfill({
      json: ["Strawberry", "Chocolate", "Mango", "Vanilla"].map((flavour, i) => ({
        item_id: 9001 + i,
        name: `${flavour} Ice Cream`,
      })),
    }),
  );

/** @param {import('@playwright/test').Page} page */
const openIceCreamSession = async (page) => {
  await openSessions(page);
  return openSessionWhere(
    page,
    async (d) => (await d.getByText("Table wants ice cream").count()) > 0,
  );
};

test("free ice cream strip", async ({ page }) => {
  await showIceCreamYes(page);
  const dialog = await openIceCreamSession(page);
  // The guest's request, the bill they asked on, and the button to send it
  // The request's card: the nearest ancestor that also holds its Mark done button
  await highlight(
    dialog
      .getByText(/\(wants free ice cream\)$/)
      .locator('xpath=ancestor::*[.//button[@aria-label="Mark request done"]][1]'),
  );
  await highlight(dialog.getByText("Asked for bill").first());
  await highlight(dialog.getByRole("button", { name: "Free ice cream for the table" }));
  await dialog.screenshot({ path: file("free-ice-cream-strip") });
});

test("free ice cream dialog", async ({ page }) => {
  await showIceCreamYes(page);
  await showDefaultFlavours(page);
  const dialog = await openIceCreamSession(page);
  await dialog.getByRole("button", { name: "Free ice cream for the table" }).click();

  const picker = page.getByRole("dialog", { name: /^Free ice cream for/ });
  await expect(picker.getByRole("button", { name: /^Add one Mango/ })).toHaveAttribute("aria-disabled", "false");
  await picker.getByRole("button", { name: /^Add one Mango/ }).click();
  await picker.getByRole("button", { name: /^Add one Mango/ }).click();
  await picker.getByRole("button", { name: /^Add one Vanilla/ }).click();
  await picker.getByRole("button", { name: /^Add one Strawberry/ }).click();
  await settle(page);

  await highlight(picker.locator("fieldset"));
  await highlight(picker.getByRole("button", { name: /to kitchen$/ }));
  await picker.screenshot({ path: file("free-ice-cream-dialog") });
});

test("free ice cream sent", async ({ page }) => {
  await showIceCreamYes(page, { sent: true });
  await openSessions(page);
  const dialog = await openSessionWhere(
    page,
    async (d) => (await d.getByText(/^Ice cream sent/).count()) > 0,
  );
  const chip = dialog.getByText(/^Ice cream sent/).first();
  // The bill card, like the guest-check shot
  const bill = chip.locator(
    'xpath=ancestor::*[.//button[starts-with(@aria-label, "View ")]][1]',
  );
  await highlight(chip);
  await bill.screenshot({ path: file("free-ice-cream-sent") });
});

test("free with the bill category", async ({ page }) => {
  // Tall enough for the whole dialog, so the switch at the bottom shows
  await page.setViewportSize({ width: 1280, height: 1100 });
  await page.goto("/admin/menu-categories");
  await expect(page.getByRole("row").nth(1)).toBeVisible();
  // It's on a later page of the table
  await page.getByRole("searchbox").fill("Free Ice Cream");
  const row = page.getByRole("row").filter({ hasText: "Free Ice Cream" }).first();
  await expect(row).toBeVisible();
  await settle(page);
  await row.getByRole("button", { name: "Edit Free Ice Cream" }).click();

  const dialog = page.getByRole("dialog");
  const freeSwitch = dialog.getByRole("switch", { name: "Free with the bill" });
  await expect(freeSwitch).toBeChecked();
  await settle(page);
  await highlight(freeSwitch.locator('xpath=ancestor::div[contains(@class,"MuiFormControl-root")][1]'));
  await dialog.screenshot({ path: file("free-with-bill-category") });
});

// Filled in but never saved
test("adding a free ice cream flavour", async ({ page }) => {
  await page.goto("/admin/menu-items");
  const add = page.getByRole("button", { name: "Add New Menu Item" });
  await expect(add).toBeVisible();
  await settle(page);
  await add.click();

  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/^Name/).fill("Pistachio Ice Cream");
  await dialog.getByLabel("Description").fill("Free with the bill");
  await dialog.getByLabel(/^Category/).click();
  await page.getByRole("option", { name: "Free Ice Cream" }).click();
  // Nothing focused, so the field's focus colour doesn't look like an error
  await page.evaluate(() => /** @type {HTMLElement | null} */ (document.activeElement)?.blur());
  await settle(page);
  await highlight(fieldOf(dialog.getByLabel(/^Category/)));
  await dialog.screenshot({ path: file("free-ice-cream-flavour-item") });
});

// What the guest sees when they ask for the bill, on a phone. Signs in as the seeded customer who's
// seated with a bill (docs/dev/database.md). If they already asked for it, the summary is changed in
// the browser so the button shows. Stops at the question: nothing is sent
const SEATED_CUSTOMER = { email: "emma.wilson@email.com", password: "Customer123!" };

test.describe("guest", () => {
  test.use({ storageState: SIGNED_OUT, viewport: { width: 390, height: 844 } });

  test("free ice cream question", async ({ page }) => {
    await page.route(/\/api\/bill\/summary\/\d+/i, async (route) => {
      const response = await route.fetch();
      const summary = await response.json();
      await route.fulfill({ response, json: { ...summary, requested_at: null, status: "Open" } });
    });
    await signIn(page, SEATED_CUSTOMER);
    await page.waitForURL((url) => !url.pathname.startsWith("/auth"));
    await page.goto("/dashboard/bills");
    const ready = page.getByRole("button", { name: "Ready for bill" });
    await expect(ready).toBeEnabled({ timeout: 15_000 });
    await settle(page);
    await ready.click();

    const confirm = page.getByRole("dialog");
    await confirm.getByRole("button", { name: "Yes, please" }).click();
    await expect(confirm.getByText(/Your server will ask for your flavour/)).toBeVisible();
    await settle(page);
    await highlight(confirm.getByRole("group", { name: "Would you like free ice cream?" }));
    await confirm.screenshot({ path: file("free-ice-cream-guest-question") });
  });
});

// No bill said yes on a phone: staff can still send ice cream for a table that asked in person
test("free ice cream without an app request", async ({ page }) => {
  await showIceCreamYes(page, { nobody: true });
  await openSessions(page);
  const dialog = await openBusiestSession(page);
  const button = dialog.getByRole("button", { name: "Free ice cream for the table" });
  await expect(button).toBeVisible();
  await expect(dialog.getByText("Table wants ice cream")).toHaveCount(0);
  await highlight(button);
  await dialog.screenshot({ path: file("free-ice-cream-staff-button") });

  // The picker starts with nobody ticked
  await showDefaultFlavours(page);
  await button.click();
  const picker = page.getByRole("dialog", { name: /^Free ice cream for/ });
  await expect(picker.getByRole("button", { name: /^Add one Mango/ })).toBeVisible();
  await settle(page);
  await highlight(picker.locator("fieldset"));
  await picker.screenshot({ path: file("free-ice-cream-staff-dialog") });
});
