# Architecture Overview

This page explains how the parts of Sushi Toshi fit together, how sign-in and permissions work, and where to find things in the code. For the tech stack and how to run the project, see the [README](../../../README.md). For the database, see [Database, migrations and seed data](../database.md).

## The big picture

```
Browser (customer phone, staff tablet, admin laptop)
   │
   ▼
Next.js frontend            :3000   src/front-end
   │  /api/* is forwarded by the rewrite in next.config.mjs
   ▼
ASP.NET Core Web API        :5264   src/back-end
   │  Entity Framework Core
   ▼
MariaDB (Docker)            :3307   sushi_toshi_db
```

- **The browser only talks to Next.js.** Frontend code calls the API with relative paths (`/api/...`), and Next.js forwards them to the backend. Because everything is on one origin, there are no CORS problems, and phone testing through ngrok works (see [phone-testing-setup.md](../phone-testing-setup.md)).
- **Server-side code calls the backend directly** at `http://127.0.0.1:5264`. Setting `NEXT_PUBLIC_API_URL` overrides both (see `src/front-end/src/config/api.js`).
- **The backend is a JSON API only.** It renders no pages. In Development it serves Swagger at <http://localhost:5264/swagger>.
- **External services:** SendGrid for emails, Google for "Sign in with Google", and canada-holidays.ca for loading public holidays.

## Main concepts

| Concept                      | What it is                                                                                                   | Entity                                         |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- |
| Location                     | A restaurant branch. Almost everything belongs to one                                                        | `Location`                                     |
| Table / table group          | A physical table, or tables pushed together for one party                                                    | `TableEntity`, `TableGroup`                    |
| Dining session               | One party's visit at a table or table group, from when staff seat them until they close the bill             | `DiningSession`                                |
| Session participant          | A person at that table. Each customer and each guest is their own participant, linked to the one bill they order on (`Bill_Id`) | `SessionParticipant`                           |
| Session order / order item   | One round of ordering and the items in it                                                                    | `SessionOrder`, `OrderItem`                    |
| Bill                         | What one party at the table pays, priced per guest from the location's pricing profile. A session can have several | `Billing` (`bills` table)                      |
| Service request              | "Call server" requests from the table                                                                        | `ServiceRequest`                               |
| Menu / menu item / assignment | Items are grouped into menus. An assignment puts an item on a menu, optionally with a location-specific override | `Menu`, `MenuItem`, `MenuItemAssignment`      |
| Pricing profile / rate       | All-you-can-eat prices per day type (weekday, weekend, holiday), GST, gratuity and leftover fee. A default profile, with optional per-location overrides | `PricingProfile`, `PricingRate` |
| Holiday                      | A public holiday that changes pricing once an admin approves it                                              | `Holiday`                                      |
| Staff location               | A location an admin has assigned a staff member to                                                           | `StaffLocation`                                |

## Sign-in and roles

### How a user signs in

All sign-in endpoints are in `AuthController` under `/api/auth`. Each one returns our own JWT.

| Method              | Endpoint                         | Notes                                                                                                   |
| ------------------- | -------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Email and password  | `POST /api/auth/login`           | Password hashed with BCrypt. Email must be verified                                                     |
| Register            | `POST /api/auth/register`        | Sends a verification email through SendGrid (see [email setup](../../setup/email-sendgrid-setup.md))   |
| Google              | `POST /api/auth/google`          | The frontend sends Google's ID token; the backend checks it and creates a customer the first time (see [google-signin-setup.md](../google-signin-setup.md)) |
| Continue as Guest   | `POST /api/auth/guest`           | Creates a throwaway customer account with no password                                                   |

There are also endpoints for verifying email, resending verification, and forgot/reset password. All of these are rate limited per IP address (see **Rate limits** below).

**The token** contains the user's ID, email and role. How long a token lasts depends on the role: staff 12 hours (`Jwt:StaffExpiresInMinutes`, about a shift, so the dashboard doesn't log them out mid-service), admins 2 hours (`Jwt:AdminExpiresInMinutes`), and customers 2 hours (`Jwt:ExpiresInMinutes`). Guest tokens last 6 hours (`Jwt:GuestExpiresInMinutes`) and carry a `guest` claim. The signing key is `Jwt:Key`. Only `appsettings.Development.json` has one; production sets its own with the `Jwt__Key` environment variable (see [production setup](../../setup/production-setup.md)).

**In the browser**, the token is stored in `localStorage` (`authToken`). The shared axios instance in `src/front-end/src/config/api.js` adds it to every request as `Authorization: Bearer ...`. On a 401 it clears the token and sends the user to `/auth/login`. Calls that must not send a token, such as before sign-in, use `config/publicApi.js`.

### Guest account cleanup

"Continue as Guest" creates an account per phone, so many are never used to order.
`GuestCleanupWorker` runs `GuestCleanup` daily at `GuestCleanup:RunAtHourUtc` (default 10:00 UTC,
3–4 AM Alberta time) and removes guest accounts that:

- are older than `GuestCleanup:MinAgeHours` (default 24, must be longer than the guest token)
- have no orders and no service requests
- aren't seated at a table that's still open

Their `session_participant` rows are kept with `User_Id` set to null, so table history and
head counts don't change. Set `GuestCleanup:Enabled` to `false` to turn it off.


### Roles

| Role       | Can                                                                                                     |
| ---------- | ------------------------------------------------------------------------------------------------------- |
| `Customer` | Browse the menu, order, call a server and see the bill, but only for the table they're seated at. Guests are customers too |
| `Staff`    | Run the floor: sessions, tables, orders and service requests, limited to their current location        |
| `Admin`    | Everything staff can do at any location, plus menus, pricing, holidays, staff, customers, QR codes and analytics |

The `UserRoles` enum also has `Employee`. **Nothing is defined for it at this time.** It has no authorization policy, no endpoints and no pages, and no account can be given it: `StaffController` only accepts `Staff` or `Admin`, and sign-up and guest accounts are always `Customer`. It's kept only because `users.Role` is stored as a number (`Admin = 0`, `Employee = 1`, `Customer = 2`, `Staff = 3`). Removing it would shift those numbers and give existing accounts the wrong role. The numbers are fixed in `enums.cs` and checked by `UserRolesTests`.


**In the backend**, controllers use `[Authorize]` for any signed-in user, or one of two policies defined in `Program.cs`:

- `staffOnly`: Staff or Admin
- `adminOnly`: Admin only

**In the frontend**, each protected page reads the role with the `useAuth` hook and redirects if it's wrong (for example, `src/app/admin/staff/page.jsx`). There's no route middleware. These checks only control what the user sees. The backend is what actually enforces access, so every new endpoint needs the right `[Authorize]` attribute.

### Staff are limited to their location

Staff work at one location at a time. Admins can work at any location.

- **Assigned locations:** an admin assigns staff to one or more locations on the **Staff** page. These are stored in the `StaffLocation` table.
- **Current location:** `User.Location_id` is the one they're working at now. Staff switch between their assigned locations from the app bar, which calls `PUT /api/staff/update-user-location/{id}`.
- **The current location is read from the database on every request,** not from the token. A switch, or an admin changing it, applies on the next request without signing in again.

`ILocationAccessService` (`Services/LocationAccessService.cs`) is how controllers apply this. Inject it into any controller that returns or changes location data:

| Method                     | Use it when                                                                                                       |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `ResolveLocationAsync`     | The endpoint lists things for a location. Staff always get their current location, whatever they asked for. Admins get what they asked for, or all locations |
| `CanAccessLocationAsync`   | The endpoint acts on one thing by ID (a table, session, order). Check the thing's location before returning or changing it |
| `CanSwitchToLocationAsync` | A user is changing their current location                                                                         |

Return the standard 403 responses from `Helpers/LocationResults.cs` so the frontend shows consistent messages. `StaffLocationLockTests.cs` shows the expected behaviour for each controller.

### Customers are limited to their table and their bill

Customers aren't limited by location. Instead, they can only see the session they're seated in, and within it only the bill they're on. `Helpers/SessionAccess.cs` has the checks, so changing an ID in the URL can't show another table's bill or another party's:

| Method                | Use it when                                                                                              |
| --------------------- | -------------------------------------------------------------------------------------------------------- |
| `CanViewSessionAsync` | The endpoint is about the whole table, e.g. service requests. Staff pass if they can see the location   |
| `CanUseBillAsync`     | The endpoint reads, orders onto or requests one bill. Staff pass for any bill at a table they can see; customers only for their own bill |
| `MyBillIdAsync`       | You need the bill a seated customer is on (null until they pick one), e.g. to filter a list to it        |

Other parties' bills only ever show a customer their name and party size (`GET /api/sessionparticipant/{session_id}/bill`), so they can pick which one to join.

## The customer flow: from QR code to ordering

1. **Staff start a session** for a table from the Sessions dashboard. Customers can't create sessions.
2. **The customer scans the table's QR code.** It links to `/auth/login?locationId=1&tableNumber=4`. The URL comes from `QRCodeSettings:SessionPageUrl`, and `QrGeneratorService` generates the images (see the [QR code service README](../../../src/back-end/Services/QrCode/README.md)).
3. **The login page saves the table** in `localStorage` (`utils/tableVisit.js`) and forgets any previous visit's session, bill and cart.
4. **The customer signs in, registers, or continues as a guest.**
5. **The frontend joins them to the table** with `POST /api/DiningSession/join`. If the table is in a group, they join the group's session. If staff haven't started the table yet, they're asked to get staff to start it. Joining a new table leaves any table they were at before.
6. **They're sent to the menu** (`/menu/full-menu`). Orders, service requests and the bill all use that session.
7. **They pick their bill.** A guest who isn't on a bill yet sees `components/menu/BillChoiceDialog.jsx` and either joins one of the table's open bills or starts one for their party, then checks its guest count. They can't order until they're on a bill. If they rescan after their bill was closed or cancelled, `join` takes them off it so they pick again.

## Bills and who's on them

A table can have several bills, one per party paying separately. Each `SessionParticipant` has a nullable `Bill_Id` for the bill they order on and can see. Deleting a bill sets it back to null.

**Guests** manage their own link through `SessionParticipantController`. `TableOrderContext` loads it on the menu pages and the orders page.

| Endpoint                                              | What it does                                                                                         |
| ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `GET /api/sessionparticipant/{session_id}/bill`       | Their bill (with counts by guest type) and the table's open bills (name and party size only)         |
| `PUT /api/sessionparticipant/{session_id}/bill`       | Join an open bill                                                                                    |
| `PUT /api/sessionparticipant/{session_id}/bill/guests` | Change their own bill's guest counts                                                                 |
| `POST /api/bill/create-bill/{session_id}`             | Start a bill. A guest is put on it, and it's marked `Started_By_Guest`                               |

**Staff** manage everyone's from the Dining Session dialog (`components/staff/SessionDashboard`):

| Endpoint                                                                   | What it does                                                                      |
| -------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `PUT /api/bill/guest-counts/{bill_id}`                                     | Edit Guests. Clears both "by guest" flags                                         |
| `PUT /api/bill/lock-guest-counts/{bill_id}`                                | Lock or unlock the counts (`Counts_Locked`). Locking clears both "by guest" flags |
| `PUT /api/bill/merge/{bill_id}`                                            | Move people, non-cancelled orders, leftover fee and counts to another open bill, then cancel this one |
| `PUT /api/sessionparticipant/{session_id}/participants/{participant_id}/bill` | Move someone, optionally with their orders (`move_orders`) and guests from the old bill's count (`guest_type`, `guest_count`) |

`GET /api/dashboard/sessions` sends each bill's counts, flags and `People_On_Bill`, the table's `Seat_Count`, and the `People` at the table, so the dashboard can show who's on which bill and which counts need checking.

The rules live in `Helpers/BillLinks.cs`:

- **Leaving a bill** (`CantLeaveReasonAsync`): a guest can switch bills until they've ordered on their current open bill, or staff have locked its counts. After that only staff can move them.
- **Joining a bill** (`CantJoinReason`): only open bills at their table that haven't asked for the bill.
- **Guest counts** (`GuestCountProblemAsync`): every bill needs at least one guest. A guest can't take the table's open bills past its seats (`TableCapacity`, summed across a table group), but can always lower their count. Staff can go past the seats; the frontend warns them first.
- **Flags staff should check:** `Started_By_Guest` and `Counts_Changed_By_Guest` on `Billing`, plus more people on a bill than its guest count. The sessions table shows a **Check guests** chip for any of these (`billNeedsGuestCheck` in `SessionColumns.jsx`).

**Customer orders are per bill.** `OrderController` only lets a customer place, add to or view orders on their own bill, and `GET /api/order/session/{sessionId}` returns only their bill's orders. On `/dashboard/orders`, "All orders" means everyone on their bill. The My orders / All orders tabs are hidden when nobody else is on it.

**Concurrent changes:** anything that changes a bill's orders or counts takes a row lock first with `Helpers/BillLock.cs` (`SELECT ... FOR UPDATE` in a transaction), so an order can't land on a bill while staff close, merge or recount it. Lock several bills with the `params` overload, which locks in ID order to avoid deadlocks. Call it before reading anything. The in-memory test database has no locks, so it returns null there.

**Cancelling a bill** (`POST /api/bill/cancel-bill/{bill_id}`) is refused once any of its orders are being made or served, since those still have to be paid for. Cancelled bills aren't counted in guest totals.

## Where things live

### Backend (`src/back-end`)

| Folder / file          | Contents                                                                                                   |
| ---------------------- | ---------------------------------------------------------------------------------------------------------- |
| `Program.cs`           | Startup: services, JWT, policies, rate limits, CORS, Swagger, and database migration and seeding on start |
| `Controllers/`         | API endpoints, one controller per area. `Analytics/` has the four analytics report controllers             |
| `Services/`            | Logic shared between controllers: pricing, holidays, location access, auth emails, QR codes (`QrCode/`), analytics queries (`Analytics/`) |
| `Helpers/`             | Small pure rules, mostly static: bill calculation, order limits, leftover fees, guest accounts, claims, session access, bill links and bill locks |
| `Dtos/`                | Request and response shapes                                                                                |
| `domain/Entities/`     | EF Core entity classes (the database tables)                                                               |
| `domain/Dbcontexts/`   | `ApplicationDbContext`                                                                                     |
| `domain/Seeders/`      | Development seed data                                                                                      |
| `Migrations/`          | EF Core migrations                                                                                         |
| `Configurations/`      | Typed settings classes, validated on startup (SendGrid, auth emails, holiday API)                          |
| `storage/qrcodes/`     | Generated QR images (gitignored)                                                                           |

Most controllers use `ApplicationDbContext` directly. There's no repository layer. When logic is needed by more than one controller, or is complex enough to test on its own, it moves to `Services/` (with an interface, registered in `Program.cs`) or, for pure calculations, to `Helpers/`.

**Rate limits** (`Program.cs`): endpoints opt in with `[EnableRateLimiting(...)]`. There's no site-wide limit. Over a limit, the backend answers `429 Too Many Requests` straight away.

| Policy | Endpoints | Limit | Counted per |
| --- | --- | --- | --- |
| Guest sign-in (`GuestAccounts`) | `guest` | 30 a minute | IP address |
| Sign-in (`AuthRateLimits.SignInPolicy`) | `login`, `google`, `verify-email`, `reset-password` | 20 a minute, shared by these endpoints | IP address |
| Email (`AuthRateLimits.EmailPolicy`) | `register`, `forgot-password`, `resend-verification` | 5 a minute, shared by these endpoints | IP address |
| Menu item views (`ViewTrackingRules`) | `POST /api/menu-item-views` | `ViewTrackingRules.RateLimitPerMinute` | Sign-in token (IP address without one) |

Diners on the restaurant Wi-Fi share one IP address, so the per-IP limits are set to allow a busy room. Behind a proxy, they only work per client with forwarded headers turned on (see [Behind a reverse proxy](../../setup/production-setup.md#behind-a-reverse-proxy)).

**JSON and query parameter names** are snake_case. [Naming conventions](../naming-conventions.md) explains why, and why some C# names aren't standard:

- **JSON keys** come from the C# property name. `ApiJsonOptions` sets the `SnakeCaseLower` policy, so `TableNumber` is sent and read as `table_number`. Only add `[JsonPropertyName]` when the key differs from the property name, such as `UserEmail` sent as `email`. Enums are sent as their names (`"Pending"`).
- **Query parameters** don't go through the JSON policy. Give each multi-word one its name: `[FromQuery(Name = "location_id")] int? locationId`.
- **In Development, a wrong name gets a 400** that names it, instead of quietly binding to 0 or null. That covers body keys a DTO doesn't have (`Json:RejectUnknownKeys`) and query parameters an action doesn't take (`Query:RejectUnknownParameters`).
- **Tests guard both.** `DtoJsonKeysTests` compares every DTO's keys with `DtoJsonKeys.txt`. When a key change is intended, regenerate it with `UPDATE_DTO_JSON_KEYS=1 dotnet test --filter DtoJsonKeysTests`. `QueryParameterNameTests` checks that every query parameter is snake_case.
- **Page URLs aren't API names.** The QR code link `/auth/login?locationId=…&tableNumber=…` keeps camelCase (see `QrGeneratorService`).

### Frontend (`src/front-end/src`)

The frontend is Next.js with the App Router, React 19 and MUI. All pages are client components that call the API.

| Folder        | Contents                                                                                                   |
| ------------- | ---------------------------------------------------------------------------------------------------------- |
| `app/`        | Routes. Each folder with a `page.jsx` is a URL, e.g. `app/staff/dashboard/sessions/page.jsx` → `/staff/dashboard/sessions` |
| `components/` | UI, grouped by area (`admin/`, `staff/`, `customer/`, `menu/`, `analytics/`, `auth/`, `common/` ...). Pages stay thin and render these |
| `config/`     | `api.js` (signed-in axios instance), `publicApi.js` (no token), `auth.js` (JWT decoding)                    |
| `hooks/`      | Shared hooks, e.g. `useAuth`, `useAutoRefresh`, `useOrderActions`                                          |
| `contexts/`   | React contexts shared across pages (menu, cart/orders, notifications)                                      |
| `services/`   | API helpers used by several components (orders, pricing, holidays, table session)                         |
| `utils/`      | Small helpers (auth token storage, table visit, server error messages)                                     |
| `stories/`    | Storybook stories                                                                                          |
| `styles/`     | Global CSS and CSS modules                                                                                 |

**Main routes:**

| Area      | Routes                                                                                                    |
| --------- | --------------------------------------------------------------------------------------------------------- |
| Everyone  | `/` (home, different for each role), `/auth/*` (login, register, verify email, forgot/reset password)    |
| Customers | `/menu/full-menu`, `/menu/review`, `/dashboard/orders`, `/dashboard/bills`, `/dashboard/call-server`, `/thank-you`; with an account only: `/menu/browse` (the menu before being seated, no ordering), `/visits` |
| Staff     | `/staff/dashboard/sessions`, `/staff/dashboard/tables`, `/staff/dashboard/orders`                         |
| Admins    | `/admin/*` (staff, customers, menu items, categories, pricing, holidays, QR codes), `/location/menu-location`, `/tags/tag-management`, `/analytics/*` |

The staff dashboards (`components/staff/Order`, `SessionDashboard`, `TableDashboard`) share one structure. Each has an `index.jsx` entry, a `context/` provider holding its state, `hooks/` for data loading and actions, and `components/` for columns and dialogs. Follow the same pattern for a new dashboard.

## Adding a feature: where the pieces go

A typical feature touches these places, in this order:

1. **Entity and migration**, if the data model changes (see [database.md](../database.md#changing-the-schema))
2. **DTOs** for the request and response, with snake_case keys and query parameters (see [JSON and query parameter names](#backend-src-back-end))
3. **Controller endpoint**, with the right `[Authorize]` attribute and, for location data, `ILocationAccessService`
4. **Service or helper**, if the logic is shared or worth testing on its own
5. **Backend tests** in `src/back-end.Tests` (see [testing.md](../testing.md))
6. **Component** in `components/<area>/`, calling the API through `@/config/api`
7. **Page** in `app/`, with a role check if it isn't public
8. **Frontend tests** in a `__tests__` folder next to the component (see [frontend-testing.md](../frontend-testing.md))
