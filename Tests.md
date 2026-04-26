# Tests — Ayvalık Bank LA-NET

**Stack:** xUnit · FluentAssertions · NSubstitute · EF Core InMemory (for service tests)
**Total:** 28 tests · 100% passing
**Run:** `dotnet test`

The split is intentional for layered architecture: stateless services (`PasswordValidationService`, `TransferService`) get pure unit tests; stateful services that touch the DbContext (`AccountService`) get EF Core InMemory tests so the `if (type == X)` dispatch is covered against the actual schema.

---

## Summary by Test Class

| Test class | Tests | Style | Focus |
|---|---:|---|---|
| `PasswordValidationServiceTests` | 6 | pure unit | Length, digit, uppercase, special-char rules |
| `CustomerTierTests` | 3 | pure unit | `CustomerTier` policy data (multiplier + caps) |
| `TransferServiceTests` | 8 | pure unit | Tier-aware `CalculateFee`; per-transaction limit checks |
| `AccountServiceTests` | 11 | EF Core InMemory | Open per type; type-specific behavior; tier interaction |

---

## Coverage by Concern

### `PasswordValidationService`
- `AcceptsValidPassword` — happy path
- `RejectsOutOfRangeLength("Short1!")`, `RejectsOutOfRangeLength("ThisIsWayTooLong1!")`
- `RejectsMissingDigit`, `RejectsMissingUppercase`, `RejectsMissingSpecialCharacter`

### `CustomerTier` (policy data)
- `StandardHasFullFeeAndModestCaps` — 1.0× / 5k caps
- `PremiumHalvesFeeAndRaisesCaps` — 0.5× / 50k transfer / 25k withdrawal
- `PrivateIsFreeAndUnlimited` — 0.0× / null caps

### `TransferService` (fee + limits)
- `SameCustomerTransferIsFreeRegardlessOfTier`
- `StandardTierPaysFullFee`, `PremiumTierPaysHalfFee`, `PrivateTierPaysNoFee`
- `RejectsTransferAboveStandardCap`, `AllowsTransferAtExactlyTheCap`
- `PrivateTierTransferIsUnlimited`
- `RejectsWithdrawalAbovePremiumCap`

### `AccountService` (EF Core InMemory)
- `OpensCheckingWithGivenOverdraftLimit`
- `OpensSavingsWithGivenInterestRate`
- `OpensTimeDepositWithPrincipalAsBalance`
- `DepositOnTimeDepositRejected`
- `RejectsTransferFromTimeDeposit`
- `CheckingAllowsWithdrawIntoOverdraft` — negative balance allowed within overdraft
- `RejectsWithdrawBeyondCheckingOverdraft`
- `RejectsWithdrawAboveStandardCap`
- `PremiumHalvesTransferFee` — cross-customer transfer where source is PREMIUM
- `AccrueInterestOnSavingsCreditsExpected`
- `AccrueOnNonSavingsRejected`

Each `AccountServiceTests` test spins up an EF Core InMemory database via:

```csharp
var options = new DbContextOptionsBuilder<BankDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
using var db = new BankDbContext(options);
```

This means the column maps in `OnModelCreating` (numeric scale, enum-as-string conversion, etc.) are exercised on the same schema the production Postgres adapter sees, and the `if (type == ...)` dispatch in `AccountService` is tested end-to-end through real EF Core save/load cycles.

---

## Known Gaps

- **No controller / web tests.** Controllers, request validation, and `GlobalExceptionHandler` are not exercised. `WebApplicationFactory` integration tests are a planned add.
- **No `CustomerService` tests.** Create / list / delete / change-password / change-tier paths rely on the database, which would benefit from the same InMemory pattern as `AccountServiceTests`.
- **No coverage tooling.** No code coverage report is produced. Adding `dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator` would mirror the Java sibling's JaCoCo report.

---

## How to Run

```bash
dotnet test                                                       # all tests
dotnet test --filter "FullyQualifiedName~AccountServiceTests"     # single class
dotnet test --filter "FullyQualifiedName~AccountServiceTests.PremiumHalvesTransferFee"  # single test
dotnet test --logger "console;verbosity=normal"                   # show each test name
```
