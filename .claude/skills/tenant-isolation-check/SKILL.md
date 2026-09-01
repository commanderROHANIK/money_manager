---
name: tenant-isolation-check
description: Verify tenant isolation for a new or changed data model in Money Manager — the property this app's own tests call "the one defect this product cannot ship with." Use after adding or changing a model, DbSet, or controller that touches user-owned data.
allowed-tools: Read, Grep, Glob
---

Check every model/entity touched by the change against all five:

1. The model implements `IOwnedByUser` (`MoneyManager.Api/Models/IOwnedByUser.cs`) if it holds data that belongs to one user.
2. Its DbSet has `ConfigureOwnership<T>(modelBuilder)` called on it in `MoneyManager.Api/Data/MoneyManagerDbContext.cs` (`OnModelCreating`) — this is what applies the global query filter and cascade-on-user-delete. A DbSet without this call is NOT tenant-isolated even though the model implements the interface. This is the single easiest step to forget.
3. Every controller action that fetches a single row uses `FirstOrDefaultAsync(x => x.Id == id)`, never `FindAsync` — `Find` can return a change-tracked instance without querying, bypassing the query filter.
4. No action attaches a client-constructed entity directly (`Entry(e).State = Modified`). Updates must load the existing row through the filtered DbSet, then copy fields from the request DTO onto it — see the `Apply(request, entity)` pattern in any existing controller (e.g. `StocksController.cs`).
5. `MoneyManager.Api.Tests/TenantIsolationTests.cs`'s `Every_owned_entity_type_is_filtered_not_just_properties` test seeds and asserts every owned DbSet — add a seed line and an `Assert.Empty(asBob.<NewDbSet>.ToList())` line for any entity missing from it.

Report each item as pass/fail with the file:line you checked. If anything fails, fix it before calling the change done — don't just report and stop.
