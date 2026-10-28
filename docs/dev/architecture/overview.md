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
| Session participant          | A person at that table. Each customer and each guest is their own participant                                | `SessionParticipant`                           |
| Session order / order item   | One round of ordering and the items in it                                                                    | `SessionOrder`, `OrderItem`                    |
| Bill                         | What a party pays, priced from the location's pricing profile                                                | `Bill`                                         |
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
| Continue as Guest   | `POST /api/auth/guest`           | Creates a throwaway customer account with no password. Rate limited per IP address                      |

There are also endpoints for verifying email, resending verification, and forgot/reset password.

**The token** contains the user's ID, email and role. Regular tokens last `Jwt:ExpiresInMinutes` (120). Guest tokens last 6 hours (`Jwt:GuestExpiresInMinutes`) and carry a `guest` claim. The signing key is `Jwt:Key`, which must be replaced in production (see [production setup](../../setup/production-setup.md)).

**In the browser**, the token is stored in `localStorage` (`authToken`). The shared axios instance in `src/front-end/src/config/api.js` adds it to every request as `Authorization: Bearer ...`. On a 401 it clears the token and sends the user to `/auth/login`. Calls that must not send a token, such as before sign-in, use `config/publicApi.js`.

### Roles

| Role       | Can                                                                                                     |
| ---------- | ------------------------------------------------------------------------------------------------------- |
| `Customer` | Browse the menu, order, call a server and see the bill, but only for the table they're seated at. Guests are customers too |
| `Staff`    | Run the floor: sessions, tables, orders and service requests, limited to their current location        |
| `Admin`    | Everything staff can do at any location, plus menus, pricing, holidays, staff, customers, QR codes and analytics |

The `UserRoles` enum also has `Employee`, but nothing uses it.

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

### Customers are limited to their table

Customers aren't limited by location. Instead, they can only see the session they're seated in. `Helpers/SessionAccess.cs` checks this for bills and service requests, so changing an ID in the URL can't show another table's bill.

## The customer flow: from QR code to ordering

1. **Staff start a session** for a table from the Sessions dashboard. Customers can't create sessions.
2. **The customer scans the table's QR code.** It links to `/auth/login?locationId=1&tableNumber=4`. The URL comes from `QRCodeSettings:SessionPageUrl`, and `QrGeneratorService` generates the images (see the [QR code service README](../../../src/back-end/Services/QrCode/README.md)).
3. **The login page saves the table** in `localStorage` (`utils/tableVisit.js`) and forgets any previous visit's session, bill and cart.
4. **The customer signs in, registers, or continues as a guest.**
5. **The frontend joins them to the table** with `POST /api/DiningSession/join`. If the table is in a group, they join the group's session. If staff haven't started the table yet, they're asked to get staff to start it. Joining a new table leaves any table they were at before.
6. **They're sent to the menu** (`/menu/full-menu`). Orders, service requests and the bill all use that session.

Old QR codes that link to `/join` or `/start-session` still work. Those pages forward to `/auth/login` (`components/auth/QrLoginRedirect.jsx`).

## Where things live

### Backend (`src/back-end`)

| Folder / file          | Contents                                                                                                   |
| ---------------------- | ---------------------------------------------------------------------------------------------------------- |
| `Program.cs`           | Startup: services, JWT, policies, rate limits, CORS, Swagger, and database migration and seeding on start |
| `Controllers/`         | API endpoints, one controller per area. `Analytics/` has the four analytics report controllers             |
| `Services/`            | Logic shared between controllers: pricing, holidays, location access, auth emails, QR codes (`QrCode/`), analytics queries (`Analytics/`) |
| `Helpers/`             | Small pure rules, mostly static: bill calculation, order limits, leftover fees, guest accounts, claims, session access |
| `DTO/`                 | Request and response shapes                                                                                |
| `domain/Entities/`     | EF Core entity classes (the database tables)                                                               |
| `domain/Dbcontexts/`   | `ApplicationDbContext`                                                                                     |
| `domain/Seeders/`      | Development seed data                                                                                      |
| `Migrations/`          | EF Core migrations                                                                                         |
| `Configurations/`      | Typed settings classes, validated on startup (SendGrid, auth emails, holiday API)                          |
| `storage/qrcodes/`     | Generated QR images (gitignored)                                                                           |

Most controllers use `ApplicationDbContext` directly. There's no repository layer. When logic is needed by more than one controller, or is complex enough to test on its own, it moves to `Services/` (with an interface, registered in `Program.cs`) or, for pure calculations, to `Helpers/`.

**Rate limits** (`Program.cs`): endpoints opt in with `[EnableRateLimiting(...)]`. Only two do today: guest sign-in (per IP address) and menu item view tracking (per token). `Program.cs` also defines `api` and `auth` policies, but no endpoint uses them yet, so login and register aren't rate limited.

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
| Customers | `/menu/full-menu`, `/menu/review`, `/dashboard/orders`, `/dashboard/bills`, `/dashboard/call-server`, `/thank-you` |
| Staff     | `/staff/dashboard/sessions`, `/staff/dashboard/tables`, `/staff/dashboard/orders`                         |
| Admins    | `/admin/*` (staff, customers, menu items, categories, pricing, holidays, QR codes), `/location/menu-location`, `/tags/tag-management`, `/analytics/*` |

The staff dashboards (`components/staff/Order`, `SessionDashboard`, `TableDashboard`) share one structure. Each has an `index.jsx` entry, a `context/` provider holding its state, `hooks/` for data loading and actions, and `components/` for columns and dialogs. Follow the same pattern for a new dashboard.

## Adding a feature: where the pieces go

A typical feature touches these places, in this order:

1. **Entity and migration**, if the data model changes (see [database.md](../database.md#changing-the-schema))
2. **DTOs** for the request and response
3. **Controller endpoint**, with the right `[Authorize]` attribute and, for location data, `ILocationAccessService`
4. **Service or helper**, if the logic is shared or worth testing on its own
5. **Backend tests** in `src/back-end.Tests` (see [testing.md](../testing.md))
6. **Component** in `components/<area>/`, calling the API through `@/config/api`
7. **Page** in `app/`, with a role check if it isn't public
8. **Frontend tests** in a `__tests__` folder next to the component (see [frontend-testing.md](../frontend-testing.md))
