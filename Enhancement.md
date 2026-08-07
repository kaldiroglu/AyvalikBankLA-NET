# Enhancement Walkthrough — Daily Withdrawal Limits

A teaching example: add **per-account, per-calendar-day cumulative withdrawal limits** to the project, then study where the change lands.

This file describes the feature in this codebase (.NET 9 / ASP.NET Core / 3-tier layered). Sibling files in `AyvalikBankHA-JAVA`, `AyvalikBankLA-JAVA`, `AyvalikBankHA-NET`, `AyvalikBankHA-Python`, `AyvalikBankLA-Python` describe the same feature in their respective stacks so the impact can be compared side by side.

---

## The Feature

- Each `Account` carries a nullable `DailyWithdrawalLimit: decimal?`. Null = use a tier-derived default.
- Cumulative withdrawals (direct withdraw + the debit side of transfers) on a single UTC calendar day must not exceed that limit.
- Admin can set/clear the limit per account: `PUT /api/admin/accounts/{id}/daily-limit`.
- Reset at UTC midnight.
- A separate, additive constraint — the existing per-transaction tier caps still apply.

---

## Why this feature is good for teaching

It crosses every layer: model, repository, service, controller, validation. It introduces **state that lives across transactions** ("today's running total"), which is the interesting persistence question. And it sits at the intersection of `Customer`, `Account`, and `Transaction` — three aggregates — which is where the layered/anemic style starts to feel cramped.

---

## Impact on this project — .NET 9 / ASP.NET Core / Layered

### Files to add or modify

| # | Layer | Path | Change |
|---|---|---|---|
| 1 | Model | `Model/Account.cs` | Add `public decimal? DailyWithdrawalLimit { get; set; }` — anemic auto-property, no business method |
| 2 | Repository | `Repository/BankDbContext.cs` | One column map line: `entity.Property(a => a.DailyWithdrawalLimit).HasColumnType("numeric(19,2)").IsRequired(false);` |
| 3 | Service | `Service/AccountService.WithdrawAsync(...)` | **Inline** the LINQ `SumAsync`, the comparison, the `throw new InsufficientFundsException(...)` (or new `DailyLimitExceededException`) into the existing method — interleaved with the existing overdraft / time-deposit / tier-cap branches |
| 4 | Service | `Service/AccountService.TransferAsync(...)` | Same inline insertion on the source-account debit path |
| 5 | Service | `Service/AccountService.SetDailyWithdrawalLimitAsync(...)` *(new)* | Loads account → mutates → `SaveChangesAsync` |
| 6 | Web | `Web/AdminController.cs` | New `PUT /api/admin/accounts/{id}/daily-limit` endpoint + `SetDailyLimitRequest` record |
| 7 | Web | `Web/Dto.cs` | Add `record SetDailyLimitRequest(decimal Amount, Currency Currency)` |
| 8 | Exception | `Exception/BankExceptions.cs` *(optional)* | `DailyLimitExceededException` derived from `InsufficientFundsException` so the global handler still maps it to 422 |
| 9 | Tests | `AyvalikBankLA.Tests/AccountServiceTests.cs` | Extend with at-limit, just-over-limit, and after-midnight-reset cases — **must use the EF Core InMemory fixture** |
| 10 | Tests | (controller tests when added) | New endpoint shape + 422 path |

### Tech-stack-specific notes (.NET)

- **Anemic POCO** — adding `public decimal? DailyWithdrawalLimit { get; set; }` is a one-liner. No method on the entity. The rule lives in the service.
- **EF Core column map** — already in the project's `OnModelCreating` style. One extra `Property(...).HasColumnType(...)` line.
- **LINQ `SumAsync`** inside the service:
  ```csharp
  var startUtc = DateTime.UtcNow.Date;
  var endUtc = startUtc.AddDays(1);
  var withdrawnToday = await _db.Transactions
      .Where(t => t.AccountId == accountId
                  && t.Type == "WITHDRAWAL"
                  && t.Timestamp >= startUtc && t.Timestamp < endUtc)
      .SumAsync(t => t.Amount);
  ```
  This is a single statement wedged into `WithdrawAsync` *between* the overdraft branch and the tier-cap branch.
- **The `WithdrawAsync` method now has four conditional branches**: overdraft, time-deposit-not-matured, tier cap, daily cap. Each was added separately; they accumulate.
- **No DI rewiring needed** — `AccountService` already has `BankDbContext` injected; you just call a new LINQ query on it. **Faster to land than the HA-NET version.**
- **No new use-case interface** — there's no use-case interface concept here; the new admin operation is just a new method on `AccountService` plus a controller action.
- **`IExceptionHandler`** already maps `LimitExceededException → 422`; reuse it (or derive a more specific exception).

### Test impact

- **You cannot write a pure-unit test for the daily-limit rule in this architecture.** The rule is the LINQ `SumAsync` plus the comparison plus the if-check plus the exception, all sitting inside an `async` service method bound to a `DbContext`. To test it, you need the existing **EF Core InMemory** fixture (`AccountServiceTests` uses this pattern) — you cannot mock it cleanly because `SumAsync` is an EF-specific extension method.
- Compare against the HA-NET sibling's `WithdrawalPolicyServiceTests` — pure xUnit + FluentAssertions, no DB. That difference is the cost of the inlined approach.
- Existing `AccountServiceTests` need the seed step extended to insert prior `WITHDRAWAL` transactions so the `SumAsync` returns a non-zero value.

---

## Lesson Plan (apply to all six projects)

1. **Show both diffs side by side.** Count files; count *lines where the actual rule lives*.
2. **Change the rule** — "reset at customer's local midnight, not UTC." In HA you change one method on a domain service + one query in the adapter. In LA you edit a long `WithdrawAsync` that's already doing five other things; the change is wedged between the overdraft branch and the time-deposit-matured branch.
3. **Add a second consumer** — `GET /api/accounts/{id}/today-summary` showing withdrawn-so-far + remaining-limit. In HA: one controller method calling the existing port + policy. In LA: copy the LINQ `SumAsync` + comparison into a new service method.

The moral: **architecture is a bet about which kinds of change are likely.** Layered bets on rules being stable and local — it pays an entanglement tax later. The same feature shows the bet clearly.
