# Naming Conventions

This page lists the naming rules for the backend and the API, and explains why some of them aren't the usual C# and ASP.NET defaults. For the short version, see [JSON and query parameter names](architecture/overview.md#backend-src-back-end) in the architecture overview.

## The rules

| What | Style | Example |
| --- | --- | --- |
| C# classes, methods and DTO properties | PascalCase | `GetBills`, `BillResponse.AdultCount` |
| C# parameters and local variables | camelCase | `sessionId` |
| Backend files | Named after the class they hold | `BillController.cs` |
| JSON keys in request and response bodies | snake_case | `adult_count` |
| API query parameters | snake_case | `?location_id=1` |
| Page URLs (QR codes, login links) | camelCase | `/auth/login?locationId=1&tableNumber=4` |
| EF entity properties | Match the database column | `Bill_Id`, `table_number` |

JSON keys come from the C# property name: `ApiJsonOptions` sets the `SnakeCaseLower` policy, so `AdultCount` is sent and read as `adult_count`. Only add `[JsonPropertyName]` when a key differs from its property name, such as `UserEmail` sent as `email`. Query parameters don't go through that policy, so each multi-word one names itself: `[FromQuery(Name = "location_id")] int? locationId`.

## Why some names don't follow C# conventions

The project began in Python and was ported to C#. Python uses snake_case, and many names kept that style through the port.

The controllers, services, helpers and DTOs now use standard C# naming. Two things deliberately don't:

- **The JSON keys are snake_case** (see below). This is a choice about what the API sends. The C# behind it is standard.
- **EF entity properties keep their original names**, like `Bill_Id`, `First_name` and `table_number`. EF uses the property name as the column name, and almost none of these properties have a `[Column]` mapping. Renaming them would mean adding a mapping to every renamed property or migrating the database. Users wouldn't see any difference, and the risk wasn't worth it with the time left. No endpoint returns an entity directly; responses are DTOs or objects built for the response. So entity names don't leak into the JSON.

## Why the JSON keys are snake_case

ASP.NET's default is camelCase (`locationId`), which also matches JavaScript naming. The API uses snake_case instead (`location_id`), for these reasons:

- **Most of the API already used it.** Most keys were already snake_case, so only the inconsistent ones had to change. Switching to camelCase would have changed nearly every key, and rewritten a large amount of front-end code, tests and mocks that already worked.
- **Less risk.** A missed rename in JavaScript fails silently: the page just shows a blank. Changing fewer keys meant fewer chances for that, in fewer and smaller PRs that didn't block other front-end work.
- **One style everywhere.** Request bodies, responses and query strings all use the same style.
- **It's a common API style.** GitHub's API, for example, uses snake_case.

Either style fixes the real problem, which was the same field arriving under different names from different endpoints (`location_Id`, `location_id`, `locationId`). 

The trade-off is that front-end code mixes styles a little, as in `const billName = bill.bill_name`.

## How the names are checked

- **`DtoJsonKeysTests`** compares every DTO's JSON keys with `src/back-end.Tests/Dtos/DtoJsonKeys.txt`, so renaming a C# property can't quietly change a key. When a key change is intended, regenerate the file with `UPDATE_DTO_JSON_KEYS=1 dotnet test --filter DtoJsonKeysTests` and commit it with the change.
- **`QueryParameterNameTests`** checks that every query parameter is snake_case.
- **In Development, the backend rejects unknown names** with a 400 that names them. That covers body keys a DTO doesn't have (`Json:RejectUnknownKeys`) and query parameters an action doesn't take (`Query:RejectUnknownParameters`).
- **In development, the frontend warns about missing keys.** Reading a key an API response doesn't have prints an `[api]` warning in the browser console (`src/front-end/src/utils/missingKeyWarnings.js`).