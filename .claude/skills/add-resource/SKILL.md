---
name: add-resource
description: Scaffold a new user-owned resource in Money Manager (model + DbSet + migration + controller), following the project's established CRUD pattern. Invoke with /add-resource when adding a new kind of owned entity, the way Stocks/Loans/BankAccounts already exist.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
---

Given a resource name (e.g. "Bond"), scaffold it in the exact shape every existing resource uses — `MoneyManager.Api/Controllers/StocksController.cs` and `MoneyManager.Api/Models/Stock.cs` are the reference pair, there is no service layer to add:

1. **Model** in `MoneyManager.Api/Models/<Name>.cs` — implements `IOwnedByUser` (gives it `Id` and `UserId`), plus whatever fields the resource needs.
2. **DbSet** — add `public DbSet<<Name>> <Name>s { get; set; }` to `MoneyManagerDbContext`, and add `ConfigureOwnership<<Name>>(modelBuilder);` next to the other `ConfigureOwnership<T>` calls in `OnModelCreating`. Skipping this line is the most common way to accidentally ship a non-isolated resource — do not skip it.
3. **Migration** — `dotnet ef migrations add Add<Name>s --project MoneyManager.Api --startup-project MoneyManager.Api`, then apply it with `dotnet ef database update --project MoneyManager.Api --startup-project MoneyManager.Api`.
4. **Controller** in `MoneyManager.Api/Controllers/<Name>sController.cs` — `[ApiController] [Authorize] [Route("api/[controller]")]`, GetAll/GetById/Create/Update/Delete using `FirstOrDefaultAsync` (never `FindAsync`), a private static `Apply(<Name>Request request, <Name> entity)` mapper, and a `record <Name>Request(...)` DTO declared in the same file.
5. Run the `tenant-isolation-check` skill against the new resource before considering this done — in particular, extend `Every_owned_entity_type_is_filtered_not_just_properties` in `TenantIsolationTests.cs` to cover it.
6. Run `dotnet test` from the repo root to confirm nothing broke.
