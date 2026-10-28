# Database, Migrations and Seed Data

## Overview

The app uses MariaDB, run locally in Docker, with Entity Framework Core (Pomelo MySQL provider) as the ORM.

- **Schema** lives in C# entity classes in `src/back-end/domain/Entities/`, configured in `src/back-end/domain/Dbcontexts/ApplicationContext.cs` (`ApplicationDbContext`).
- **Migrations** live in `src/back-end/Migrations/`.
- **Seed data** comes from the seeders in `src/back-end/domain/Seeders/`, run by `DatabaseSeeder`.
- **ERD**: [architecture/erd.drawio](architecture/erd.drawio). It doesn't yet include the tables added in October 2026 (menu item views, pricing, holidays, staff locations), so check the entity classes for the current schema.

## The local database

`docker-compose.yml` in the repo root starts MariaDB:

| Setting  | Value                                        |
| -------- | -------------------------------------------- |
| Host     | `localhost`, port `3307` (container's 3306)  |
| Database | `sushi_toshi_db`                             |
| User     | `app_user` / `app_password`                  |
| Root     | `root` / `root`                              |
| Data     | Docker volume `mariadb_data`                 |

The backend's connection string is `ConnectionStrings:MySqlConnection` in `src/back-end/appsettings.json`, and it matches these values. To check the backend can reach the database, open <http://localhost:5264/db-test> while it's running.

```bash
# From the repo root
docker compose up -d     # start
docker compose down      # stop (keeps the data)
docker compose down -v   # stop and DELETE all data (removes the volume)
```

## What happens when the backend starts

You don't need to run `dotnet ef database update` yourself. Every time the backend starts, `Program.cs`:

1. **Checks it can connect.** If it can't, it logs `Cannot connect to database` and carries on without a database. Every request that needs one will then fail.
2. **Drops the database, if asked.** This happens only in Development, and only when `Seeding:ResetOnStartup` is `true` (see [Resetting the database](#resetting-the-database)).
3. **Applies any pending migrations** (`Database.MigrateAsync()`).
4. **Checks default pricing exists.** The `LocationPricingAndHolidays` migration inserts the default pricing profile and its rates. If any are missing, startup logs an error and skips seeding, because every bill would be wrong.
5. **Seeds the data:**
   - **No menu items in the database:** runs the full seed (see [Seed data](#seed-data)).
   - **Otherwise:** only re-adds any missing seeded user accounts, matched by email. Everything else is left alone.

Errors during these steps are logged (`An error occurred during database initialization`), but they don't stop the backend from starting. If the app behaves oddly after a pull, scroll up in the backend console and look for that line.

## Seed data

The seed gives you realistic data to work with straight away:

- Two locations in Alberta: **North Side Branch** and **South Side Branch**
- Tables and table groups for each location
- Categories, tags, menus and menu items, assigned to both locations
- Past and currently open dining sessions, with participants, orders, bills and service requests
- Menu item views (browsing data) so the analytics pages have something to show

Seeders run in dependency order (locations → menus and items → tables and groups → users → sessions and everything that hangs off them). `DatabaseSeeder.SeedDatabase()` lists the order. If you add a seeder, register it in `ServiceCollectionExtensions.cs` and call it from `SeedDatabase()` at the right point.

Not seeded: holidays are not loaded from the holiday API. An admin loads them from the **Holidays** page, then approves them.

### Seeded accounts

All seeded accounts are active and have a confirmed email. They're created by `UserSeeder.cs`.

| Role     | Email                         | Password         | Notes                                                                         |
| -------- | ----------------------------- | ---------------- | ----------------------------------------------------------------------------- |
| Admin    | `admin.user@sushitoshi.ca`    | `AdminPass123!`  | Can work at any location                                                      |
| Staff    | `sarah.jones@sushitoshi.ca`   | `StaffPass123!`  | Assigned to location 1                                                        |
| Staff    | `john.smith@sushitoshi.ca`    | `StaffPass123!`  | Assigned to location 2                                                        |
| Staff    | `michael.chen@sushitoshi.ca`  | `StaffPass123!`  | Assigned to location 2                                                        |
| Customer | `emma.wilson@email.com`       | `Customer123!`   | One of 10 customers with order history (see `UserSeeder.cs` for the rest)     |
| Customer | `noah.clarke@email.com`       | `Customer123!`   | No sessions, orders or bills. Use it to try the new-customer flow from scratch |
| Customer | `guestemail@email.com`        | `GuestUser!`     | Old shared guest account                                                      |

"Continue as Guest" doesn't use any of these. It creates a new throwaway account each time, with an email ending in `@guest.sushitoshi.invalid` and no password (see `Helpers/GuestAccounts.cs`).

> **These passwords are in the source code and the accounts are recreated on every startup, including in production.** See the known issue under [Default accounts](../setup/production-setup.md#4-default-accounts-security) in the production setup guide.

### Seeding settings

| Setting                   | Default | What it does                                                                                     |
| ------------------------- | ------- | ------------------------------------------------------------------------------------------------ |
| `Seeding:ResetOnStartup`  | `false` | Development only. When `true`, drops the whole database on startup, then migrates and seeds fresh |
| `Seeding:Enabled`         | `true`  | **Not read by the code yet.** Seeding always runs; changing this does nothing                    |

`userSeedData.csv` in the Seeders folder is loaded by `DatabaseSeeder` but its data isn't used. The seeded users come from `UserSeeder.cs`.

## Resetting the database

To get a fresh, freshly seeded database, use one of these. Each one deletes **all** local data, including anything you created in the app.

**Option 1: reset on startup.** Turn it on with user secrets so it isn't committed. From `src/back-end`:

```bash
dotnet user-secrets set "Seeding:ResetOnStartup" "true"
dotnet run --launch-profile http
# Once it has started, turn it off again, or every restart wipes the database
dotnet user-secrets remove "Seeding:ResetOnStartup"
```

**Option 2: drop it with the EF tool.** From `src/back-end`, run `dotnet ef database drop`, then start the backend.

**Option 3: delete the Docker volume.** From the repo root, run `docker compose down -v` then `docker compose up -d`, then start the backend.

Reset after pulling a change to the seeders. Because the full seed only runs on an empty database, a seeder change does nothing to a database that already has data.

## Changing the schema

### Add a migration

1. Change or add the entity class in `domain/Entities/`. If it's a new table, add a `DbSet` and any configuration (keys, indexes, relationships) to `ApplicationDbContext`.
2. Stop the backend if it's running. The migration command builds the project, and on Windows a running backend locks the build output.
3. From `src/back-end`, create the migration:

   ```bash
   dotnet ef migrations add AddSomethingDescriptive
   ```

4. **Read the generated file** in `Migrations/` before you commit it. Check it only changes what you meant to change, and that `Down()` reverses it.
5. Start the backend. It applies the migration automatically.
6. Commit the migration's `.cs` and `.Designer.cs` files together with the updated `ApplicationDbContextModelSnapshot.cs`.

You need the EF CLI for this. It's installed in the README's prerequisites (`dotnet tool install --global dotnet-ef --version 9.*`).

### Data a migration must insert

If the app can't work without some rows, insert them in the migration with `migrationBuilder.InsertData(...)`, not in a seeder. Seeders only run on an empty database, so existing databases (including production) would never get the rows. The default pricing in `LocationPricingAndHolidays` works this way.

### Undo a migration

- **Not pushed yet:** run `dotnet ef database update <PreviousMigrationName>` to roll your database back, then `dotnet ef migrations remove` to delete the files.
- **Already on `main`:** don't remove or edit it, because teammates' databases have already applied it. Add a new migration that makes the change you want.

### Two people added migrations at the same time

If both branches add a migration, the second one to merge will usually have a merge conflict in `ApplicationDbContextModelSnapshot.cs`. To fix it:

1. Merge `main` into your branch and keep `main`'s version of the snapshot.
2. Delete your migration's two files.
3. Run `dotnet ef migrations add <YourMigrationName>` again, so it's generated on top of `main`'s latest migration.

## Troubleshooting

| Symptom                                                           | Cause                                                                 | Fix                                                                                                       |
| ----------------------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Backend logs `Cannot connect to database`                         | MariaDB isn't running, or isn't on port 3307                          | `docker compose up -d` from the repo root, then restart the backend                                       |
| `/db-test` returns a 500 with `Access denied`                     | Connection string doesn't match the container's user or password      | Compare `ConnectionStrings:MySqlConnection` (and any user secret overriding it) with `docker-compose.yml` |
| Log shows `Default pricing is missing ... prices`                 | The `LocationPricingAndHolidays` migration didn't apply               | Check the earlier log lines for a migration error. Reset the database if it's only local data             |
| Error like `Unknown column` or `Table ... doesn't exist`          | A migration failed part-way, or someone edited an applied migration   | Read the startup log for the migration error. For local data, reset the database                          |
| New seed data from a pull doesn't appear                          | The full seed only runs on an empty database                          | [Reset the database](#resetting-the-database)                                                             |
| `dotnet ef migrations add` fails with a file lock or build error  | The backend is running and locks the build output                     | Stop the backend and run the command again                                                                |
| `dotnet ef: command not found`                                    | EF CLI isn't installed, or the terminal was opened before installing  | Install it (see README prerequisites), then open a new terminal                                           |
| A staff account sees "Ask an admin to assign you a location"      | That staff member has no current location                             | An admin assigns one on the **Staff** page                                                                |
