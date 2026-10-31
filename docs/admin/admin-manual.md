# Sushi Toshi Admin Manual

This manual covers everything an admin can do in Sushi Toshi: running service, setting up the restaurant, and reading the analytics.

Staff accounts see only **Dining Sessions**, **Tables** and **Orders**. Everything else in this manual is for admins only.

## Contents

- [Getting around](#getting-around)
- **Service**
  - [Dining Sessions](#dining-sessions)
  - [Tables and Table Groups](#tables-and-table-groups)
  - [Orders](#orders)
- **Restaurant setup**
  - [QR Codes](#qr-codes)
  - [Locations and Menus](#locations-and-menus)
  - [Pricing](#pricing)
  - [Holidays](#holidays)
  - [Menu Items](#menu-items)
  - [Menu Categories](#menu-categories)
  - [Tags](#tags)
  - [Staff](#staff)
  - [Customers](#customers)
- [Analytics](#analytics)

---

## Getting around

- **Menu button** (top left): opens the navigation menu. Service pages are at the top, then the **Management** pages, then **Analytics**. **Logout** is at the bottom of the menu.
- **Location dropdown** (top bar): sets your working location. Dining Sessions, Tables, Orders and the "my location" view in Analytics all follow this choice. Admins can switch to any location; staff can only switch between the locations an admin assigned them.
- **Home page**: shows today's active sessions, tables in use and pending requests for your location, with shortcut cards to every page in this manual.

Most management pages share the same layout: a title with an **Add New …** button, a searchable, sortable table, and action icons at the end of each row. Hover over an icon to see what it does.

---

## Dining Sessions

A dining session is one party's visit, from being seated until they leave. The Dining Sessions page lists every active session at your working location.

At the top are three counts: **Active Sessions**, **Tables In Use** and **Pending Requests**.

Each row shows:

| Column | What it shows |
| --- | --- |
| Session | The session number |
| Table | The table or table group the party is seated at |
| Requests | How many service requests ("call server") are waiting |
| Started | When the party was seated |
| Seated For | How long they have been seated. Turns orange after 90 minutes |
| Guests | The number of guests across all bills |
| Open Bills | Open bills out of total bills, e.g. "1 of 2" |

Rows with waiting service requests are highlighted and sorted to the top. Press **Refresh** to reload the list.

### What you can do

- **New Session**: seat a new party. Choose the location and menu, then pick a table (**Assign Individual Table**) or a table group (**Assign Table Group**). Only tables and groups that are active and free are listed.
- **View session** (eye icon): opens the session's details: its service requests and its bills.
  - **Mark done** (tick icon) clears a service request once it's handled.
  - **View Bill Summary** shows a bill's breakdown: guests by age, add-ons, over-limit charges, leftover fee, tax and total.
- **New bill** (receipt icon): adds a bill to the session. Enter a bill name and the number of adults, seniors, children and toddlers (6 and under) on it. Use more than one bill when guests pay separately. Only available once the session has a table.
- **Close Bill** (in the bill summary): closes a bill so no more orders can go on it. If the guests have asked for their bill, a notice shows the time they asked. Once they ask, guests can't add more orders to that bill, but staff still can.
- **Add leftover fee** (in the bill summary): charges the table for leaving too much food. The fee is for the whole table, split evenly across its open bills and taxed with them. It defaults to the location's leftover fee (see [Pricing](#pricing)) and can be changed or removed. **Add it before closing the bills**: a closed bill can't be changed.
- **End session** (exit icon): ends the visit and frees the table. Only available once every bill in the session is closed.

---

## Tables and Table Groups

The Tables page lists the tables and table groups at your working location. Counts at the top show **Tables Available**, **Tables In session** and **Inactive Tables**. Use the **Tables** and **Table groups** tabs to switch lists, and the status filter to show only Available, In session or Inactive.

A table's status is one of:

- **Available**: active and free to seat
- **In session**: a party is seated there
- **Inactive**: switched off and can't be seated

### Tables

- **New Table**: choose the location, then enter the table number and seat count.
- **Edit table**: change the table number or seat count.
- **Deactivate / Activate**: take a table out of use for a while (for example, it's being repaired) without deleting it. Not available while the table is in session.
- **Remove from group**: take the table out of its table group. Not available while it's in session.
- **Delete table**: remove the table permanently. Admin only, and not available while the table is in session.

Each table needs a QR code that guests scan to join the session. See [QR Codes](#qr-codes).

### Table groups

Table groups seat a large party across several tables. The party is seated at the group, and their orders belong to the group. A group doesn't need its own QR code: guests scan the code on any table in it.

- **New Table Group**: choose the location and enter a group name.
- **Edit group**: rename the group.
- **Add table**: add a table to the group. Only active tables that aren't in a session or already in a group are offered.
- **Deactivate / Activate**: take the group out of use for a while. Not available while the group is in session.
- **Delete group**: delete the group. Its tables go back to being individual tables. Not available while the group is in session.

---

## Orders

The Orders page shows the orders placed at your working location. Counts at the top show **Orders Pending**, **Orders Processing** and **Delivered - Seated Tables** (delivered orders for parties still seated). Tabs split the list by status, with the number of orders in each. The **Past Orders** tab looks up orders from earlier visits.

An order moves through these statuses:

1. **Pending**: placed by the guests and waiting for staff to approve it
2. **Processing**: approved and being prepared
3. **Delivered**: every item has reached the table

Items can also be **Cancelled**.

### What you can do

- **View order** (eye icon): see each item and its status.
- **Approve order**: send a pending order to the kitchen.
- **Edit Order** (pending orders): change item quantities or remove items, then **Save Changes** or **Save & Approve**. A removed item is cancelled and won't be delivered.
- **Mark Delivered** (processing orders): mark one item as delivered from the order details.
- **Mark all delivered**: mark every item in a processing order as delivered.

### Past Orders

Use **Past Orders** to look up what a table ordered on an earlier visit at your working location, for example to answer a question about a bill.

1. Choose a **From** and **To** date. The range can be up to 31 days.
2. Optionally, enter a **Table #** or a **Guest or bill name** to narrow the search.
3. Select **Search**.

Each result is one visit, showing the table, session number, bills, when the first order was placed, and the number of orders, items and total. Select **View table orders** (eye icon) to see every order in the visit, grouped by bill, with each item, its quantity and who ordered it.

---

## QR Codes

The QR Codes page generates the codes printed for each table. There are two kinds:

- **Session QR code**: guests scan it to sign in and join the dining session staff started for that table.
- **WiFi QR code**: guests scan it to join the restaurant's WiFi network. The network name and password for each location are set by your system administrator (see [WiFi configuration](../setup/wifi-configuration.md)).

Generated codes are saved on the server and reused until they are regenerated.

> **Before printing codes for the first time**, make sure the site has been set up for production, or the codes won't open on customers' phones. See [Table QR codes](../setup/production-setup.md#5-table-qr-codes).

### Codes for one table

1. Choose the location and enter the table number.
2. Under **WiFi QR Code** or **Session QR Code**, select **Preview** to check it or **Download** to save it.

### Codes for every table

1. Choose the location.
2. Under **Bulk QR Code Generation**, select **Generate QR Codes for All Tables**.
3. Download the result:
   - **Download as ZIP (Individual Files)**: one image per code.
   - **Download as PDF (16 QR per page)**: ready to print on letter paper (8.5" × 11"), 4 × 4 codes per page at 1.75" each.

---

## Locations and Menus

**Locations & Menus** has two parts: **Location Management** for your restaurant branches, and **Menu Management** for menu types (such as Lunch and Dinner) and which locations offer them.

### Location Management

The list shows each location's address, the **Menus** it offers (**No menus** means guests have nothing to order from yet) and whether it uses **Default** or **Custom** pricing. Select the pricing chip to open that location's pricing.

- **Add New Location** / **Edit**: enter the location's details:
  - Name
  - Address Line 1 and Address Line 2
  - City, Province and **Province code** (for example, AB). Holiday pricing uses the holidays of this province.
  - Postal Code
  - Phone Number
- **Assign Menus**: opens **Manage Menus** for the location. Add or remove each menu type it offers. Changes save straight away.
- **Pricing**: open this location's pricing. See [Location pricing](#location-pricing).
- **Delete**: delete a menu that has never been used. A menu with past sessions or orders can't be deleted, so turn off **Is Active Menu** instead.

### Menu Management

- **Add New Menu Type** / **Edit**: enter the menu's details:
  - Menu Name (for example, Lunch or Dinner)
  - Description
  - Start Time and End Time: when the menu is available
  - **Is Active Menu**: turn the menu on or off
- **Assign Locations**: opens **Manage Locations** for the menu. It lists the **Assigned Locations**; use **Add Location** to add another, or remove one from the list. Changes save straight away.
- **Delete**: delete the menu type. It will no longer be available to guests.

---

## Pricing

Sushi Toshi is priced per guest. Each bill is charged by the number of adults, seniors, children and toddlers on it, at a price that depends on the day: **Weekday**, **Weekend** or **Holiday**.

The **Pricing** page sets the **default pricing**, which every location uses unless it has its own. The page lists any locations that have their own pricing.

A pricing profile has:

- **Prices per guest**: a price for each guest type (Adult, Senior, Child, Toddler) on weekdays, weekends and holidays.
- **When each price applies**: tick the days that get weekend prices. All other days get weekday prices.
  - **Use holiday prices on approved holidays**: when on, approved holidays get holiday prices. See [Holidays](#holidays).
- **Tax and fees**
  - **GST**: the tax rate, as a percentage.
  - **Gratuity**: an automatic tip, as a percentage.
  - **Gratuity from party of**: the party size at which gratuity starts. Enter 0 for no automatic gratuity.
  - **Leftover fee**: the default fee staff can add for leaving too much food (see [Dining Sessions](#dining-sessions)).

Select **Save default pricing** to save. The button is only available once you've changed something, and **Unsaved changes** shows until you save. Open bills switch to the new prices; closed bills keep the prices they were charged.

### Location pricing

To give one location its own prices, open **Locations & Menus** and select **Pricing** on that location's row.

1. Turn on **Custom pricing for this location**. The form starts from the default prices.
2. Change what you need and select **Save location pricing**.

To go back to the default, turn custom pricing off and confirm. The location loses its own prices; closed bills keep theirs.

**Check a date** shows which prices a location will charge on a given day (weekday, weekend or holiday, and the holiday's name), using saved prices and approved holidays.

---

## Holidays

Holiday prices only apply on holidays you have approved. The Holidays page lists holidays for one province and year at a time. Each location uses the holidays of its province code (set in [Location Management](#location-management)).

### Loading holidays

1. Choose the **Province** and **Year**.
2. Tick **Include optional holidays** if you also want holidays that aren't statutory everywhere.
3. Select **Load holidays**. The public holidays are fetched and added as **Pending**. Holidays already in the list aren't added again.

### Approving holidays

1. Filter by **Pending**.
2. Tick the holidays that should get holiday pricing and select **Approve selected**. Tick the rest and select **Reject selected**.

Only **Approved** holidays get holiday prices. A rejected holiday stays in the list so it isn't loaded again as Pending.

When a holiday falls on a weekend and is observed on a weekday (for example, a Saturday Christmas observed on Monday), the **Observed** column shows the observed date.

### Other actions

- **Add Holiday**: add your own holiday, such as a local event, with a date and name. Holidays you add are approved straight away.
- **Edit**: change a holiday's date or name.
- **Delete**: remove a holiday. If you load that year again it comes back as Pending, so to keep a holiday out of pricing for good, **reject** it instead.

---

## Menu Items

The Menu Items page lists every dish with its image, name, category, description and tags.

- **Add New Menu Item** / **Edit**: enter the item's name, description and category, and upload an image (**Change Image**).
- **Manage Tags** (tag icon in the Description & Tags column): add or remove tags on the item, such as Spicy or Vegetarian. See [Tags](#tags).
- **Assign to Menus**: choose which menus offer the item and how. The **Menu Assignments** dialog lists the item's current assignments.
  - An assignment for **All locations (default)** puts the item on that menu everywhere.
  - Choosing a single location adds a **location price** instead: the same menu with its own price, status and limits at that location. Add the default assignment first; the form starts from its values. Select **Add Location Price** to save it.
  - **Edit assignment** (pencil icon) changes an existing assignment. Select **Update Assignment** to save, or **Cancel Edit**.
  - **Remove from menu** (on a default assignment) takes the item off that menu, along with all its location prices. **Reset to default price** (on a location price) removes that location's own values so it uses the default again.

  For each assignment, set:
  - **Location**: one location, or **All locations (default)**.
  - **Menu**: the menu type, such as Lunch or Dinner.
  - **Price**: for an add-on, the price charged every time. For an included item, the price charged only when a guest goes past a per-person limit set to **Charge**. Enter 0 to never charge.
  - **Status**: Available, Unavailable or Seasonal.
  - **Is Add-on Item**: turn on for items that cost extra on top of the buffet price.
  - **Limits per person** (Adult, Senior, Child, Toddler): the most each guest can order of this item during the visit. 0 = unlimited. Senior and child limits can't be more than the adult limit; a toddler limit of 0 uses the child limit.
  - **Per order limit**: the most the whole table can order in one order. 0 = no cap. Orders over this limit are always blocked.
  - **Past per-person limit**: **Block** stops guests ordering more, **Charge the item's price** lets them and bills the item's price for each extra.
- **Delete**: remove the item. It will no longer be available to order.

---

## Menu Categories

Categories group menu items (for example, Nigiri or Rolls) and can limit how much guests order across the whole category.

- **Add New Category** / **Edit**:
  - **Name** and **Description**.
  - **Limits per person** (Adult, Senior, Child, Toddler): the most each guest can order from this category during the visit. 0 = unlimited. Senior and child limits can't be more than the adult limit; a toddler limit of 0 uses the child limit.
  - **Per Order Limit**: the most the whole table can order from this category in one order, all items combined. 0 = no cap.
  - **Past per-person limit**: **Block** stops guests ordering more, **Charge the item's price** bills each item's price for every extra. Items need a price set in their menu assignment for Charge to work.
- **Delete**: delete the category. This can't be undone.

---

## Tags

Tags label menu items with things like dietary needs and allergens (for example, Spicy, Vegan or Gluten-Free), so guests can make informed choices.

- **Add New Tag** / **Edit**: enter the tag name and choose a color. The table shows a preview of how the tag looks.
- **View Items**: see every menu item that has this tag, and remove the tag from an item.
- **Delete**: delete the tag. It's removed from every menu item and this can't be undone.

To add a tag to a menu item, use **Manage Tags** on the [Menu Items](#menu-items) page.

---

## Staff

The Staff Management page lists staff and admin accounts with their name, email, locations, role and status.

- **Create New Staff Account**: enter the email address, a password (at least 8 characters), first and last name, and role:
  - **Staff** can use Dining Sessions, Tables and Orders at the locations you choose under **Locations**. A staff member with no location can't work anywhere until you assign one.
  - **Admin** can use everything, at every location.
- **Edit User**: change the account's details, role or locations. You can't change your own role.
- **Reset Password**: set a new password for the account.
- **Deactivate Account**: stop the person signing in, without deleting their account. You can't deactivate yourself.
- **Reactivate Account**: let a deactivated person sign in again.

Staff accounts can't be deleted. Deactivate them instead.

---

## Customers

Customers create their own accounts when they sign up in the app. The Customer Management page lists them, with counts of **Total Customers**, **Active Customers** and customers **New in 30 Days**.

Each row shows the customer's name, email, whether their email is **Verified**, their status, when they joined and when they were last active.

- **Deactivate customer**: stop the customer signing in until you reactivate them.
- **Reactivate customer**: let a deactivated customer sign in again.
- **Delete customer**: remove the customer's name, email and sign-in details. Their past orders stay in reports. This can't be undone.

---

## Analytics

Analytics has five pages, linked by tabs at the top: **Overview**, **Item performance**, **Browsing behavior**, **Order timing** and **Table turnover**.

### Choosing what to report on

The four report pages (all except Overview) share the same controls:

- **Date range**: Today, Yesterday, the last 7 or last 30 full days before today, or a custom **Date Range** (choose **From** and **To**, then **Show results**).
- **Location**: reports start on your working location. Remove the location chip to see **All locations**, or select **Show my location only** to go back.
- **Category** and **Tag** filters (item, browsing and order timing reports): narrow the report to some menu items. Item reports also have **Find an item**.
- **Export report**: download the report as CSV, download charts as PNG images, or **Print or save as PDF**.

### Overview

How the restaurant is doing **today** at your location, at a glance:

- **Seatings**: tables and guests seated, parties still seated, parties seated longer than the time limit, and the busiest seating hour.
- **Order status**: average time from pending to processing (waiting for staff) and from processing to completed (kitchen and delivery).
- **Item performance**: today's top and bottom sellers.
- **Browsing behavior**: today's most and least viewed items.

### Item performance

Which items sell, and which don't:

- Units sold per item, with each item's status and category
- Top 5 and bottom 5 sellers
- **Add-on items**: how many units were ordered as add-ons and their share of the item's sales

### Browsing behavior

What guests look at on the menu, and whether they order it:

- **Most viewed** and **Least viewed** items (excluding items with no views)
- **Low conversion with high views**: items guests look at often but rarely order. Set what counts as "high views" with **High views starts at**.
- **Views of unavailable items**: items guests looked at while they were unavailable

### Order timing

How quickly orders move, and when the restaurant is busy:

- **Visits**: average visit length, time from being seated to the first order, the gap between orders, and orders per table
- **Orders by hour**: a chart of when orders are placed
- **Order status**: average time from pending to processing (waiting for staff) and from processing to completed (kitchen and delivery), plus the 5 slowest and 5 fastest items

### Table turnover

How quickly tables are used and freed up:

- **Parties seated**, **Guests seated**, and parties seated longer than the time limit
- **Busiest time**: seatings by hour, measured by parties or by guests
- **Visit length by party size**: the average length of finished visits. Party size comes from the guest counts on each party's bills; parties with no bill are shown as unknown size.
- **Time between parties**: for single tables and table groups, the time from one party leaving to the next being seated at the same table on the same day. Long gaps count as the table sitting empty and aren't included.
