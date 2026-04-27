# Architecture — Ayvalık Bank LA-NET

A .NET 9 / ASP.NET Core port of `AyvalikBankLA-JAVA`, organized as a **Classic 3-Tier Layered Architecture**. Anemic entities, fat services, no repository abstraction.

---

## Dependency Graph

```
Web (controllers + DTOs)
   │
   ▼
Service (CustomerService, AccountService, TransferService, PasswordValidationService)
   │
   ▼
Repository (BankDbContext) — direct EF Core dependency, no IRepository<T>
   │
   ▼
Model (POCO entities)
```

Direct, top-down dependencies. Controllers know about services; services know about the DbContext; the DbContext knows about entities.

---

## Project Layout

```
AyvalikBankLA.Api/
  Model/                        — POCO entities + enums
    Customer, Account, Transaction, Settings (entities)
    AccountStatus, AccountType, CustomerTier, TransactionType, Currency (enums)
  Repository/
    BankDbContext.cs            — EF Core DbContext + OnModelCreating column maps
  Service/
    CustomerService.cs          — create/delete/list/change-password/change-tier
    AccountService.cs           — open per type, deposit/withdraw/transfer,
                                  accrue interest, mature time deposit,
                                  freeze/unfreeze/close, type-aware dispatch
    TransferService.cs          — fee calc + per-transaction limit checks
    PasswordValidationService   — length + char-class rules
  Web/
    AccountController, AdminController, CustomerController
    Dto.cs                      — request records + response records (with From())
    GlobalExceptionHandler.cs   — IExceptionHandler → ProblemDetails
  Exception/
    BankExceptions.cs           — typed exception classes
  Config/
    BasicAuthHandler.cs         — custom AuthenticationHandler<>
    AdminSeeder.cs              — seeds admin@ayvalikbank.dev on startup
  Program.cs                    — DI + middleware wiring
AyvalikBankLA.Tests/
  *.cs                          — xUnit tests (28 total)
```

---

## Key Design Decisions

### Anemic model

Entities have public auto-properties only — no business methods.

```csharp
public class Account
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Currency Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; }
    public AccountType Type { get; set; }
    public decimal? OverdraftLimit { get; set; }
    public decimal? InterestRate { get; set; }
    public DateOnly? LastAccrualDate { get; set; }
    public decimal? Principal { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public bool? Matured { get; set; }
}
```

### Fat services with type dispatch

`AccountService` uses `if (account.Type == AccountType.X)` to dispatch behavior. This preserves the layered/anemic style — no polymorphism in the model:

```csharp
public async Task<Transaction> WithdrawAsync(Guid accountId, decimal amount, Currency currency)
{
    var account = await _db.Accounts.FindAsync(accountId);
    EnsureOperable(account);
    EnsureSameCurrency(account, currency);

    if (account.Type == AccountType.TIME_DEPOSIT)
    {
        if (account.Matured != true)
            throw new InvalidAccountOperationException("Time deposit not matured");
    }

    var owner = await _db.Customers.FindAsync(account.OwnerId);
    _transferService.RequireWithdrawalWithinLimit(amount, owner.Tier);

    if (account.Type == AccountType.CHECKING)
    {
        var floor = -(account.OverdraftLimit ?? 0m);
        if (account.Balance - amount < floor) throw new InsufficientFundsException(...);
    }
    else
    {
        if (account.Balance < amount) throw new InsufficientFundsException(...);
    }

    account.Balance -= amount;
    /* ... */
}
```

### No repository abstraction

Services hold `BankDbContext` directly. The .NET-idiomatic equivalent of Spring Data — no `IRepository<T>` ceremony, no Unit-of-Work wrapper. EF Core is the persistence layer, full stop.

### Single-table inheritance for accounts

One `accounts` table with a `Type` discriminator and seven nullable type-specific columns (`OverdraftLimit`, `InterestRate`, `LastAccrualDate`, `Principal`, `OpenedOn`, `MaturityDate`, `Matured`). EF Core column maps live in `BankDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<Account>(b =>
{
    b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
    b.Property(x => x.OverdraftLimit).HasColumnType("numeric(19,2)");
    b.Property(x => x.InterestRate).HasColumnType("numeric(19,2)");
    /* ... */
});
```

### `CustomerTier` with extension-method policy data

```csharp
public enum CustomerTier { STANDARD, PREMIUM, PRIVATE }

public static class CustomerTierExtensions
{
    public static decimal FeeMultiplier(this CustomerTier t) => t switch
    {
        CustomerTier.STANDARD => 1.00m,
        CustomerTier.PREMIUM  => 0.50m,
        CustomerTier.PRIVATE  => 0.00m,
        _ => 1.00m
    };
    public static decimal? MaxPerTransfer(this CustomerTier t) => /* 5k/50k/null */;
    public static decimal? MaxPerWithdrawal(this CustomerTier t) => /* 5k/25k/null */;
}
```

### DTOs with `From(entity)` factory methods

Each response record exposes `static From(entity)` — mirrors the Java sibling's `from(Entity)` pattern and keeps the mapping next to the DTO definition.

### Cross-cutting

- **Authentication** — `BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>` (the `idunno.Authentication.Basic` package is incompatible with `net9.0`). Credentials read from the `customers` table.
- **Error handling** — `GlobalExceptionHandler : IExceptionHandler` (.NET 8+ idiom) maps domain exceptions to `ProblemDetails`: 404 `not-found`, 401 `invalid-credentials`, 422 `invalid-account-operation` / `limit-exceeded` / `insufficient-funds`, 400 fallback.
- **Composition root** — `Program.cs`. EF Core, services, auth, exception handler, controllers all wired here.

---

## Request Flow

### `POST /api/accounts/checking?ownerId={id}`

```
HTTP request
  → AccountController.CreateChecking
      → AccountService.CreateCheckingAsync(ownerId, currency, overdraftLimit)
          → BankDbContext.Customers.FindAsync(ownerId)
          → new Account { Type = CHECKING, OverdraftLimit = ..., ... }
          → BankDbContext.Accounts.AddAsync(...)
          → BankDbContext.SaveChangesAsync()
      ← AccountResponse.From(account)
HTTP 201 Created + JSON
```

### `POST /api/accounts/{id}/transfer` (cross-customer, with fee)

```
HTTP request
  → AccountController.Transfer
      → AccountService.TransferAsync(sourceId, targetId, amount, currency)
          → load source, target, sourceOwner, settings
          → TransferService.RequireTransferWithinLimit(amount, sourceOwner.Tier)
          → fee = TransferService.CalculateFee(amount, sameCustomer, feePct, tier)
          → debit source by (amount + fee), credit target by amount
          → record TRANSFER_OUT and TRANSFER_IN transactions
          → SaveChangesAsync
HTTP 200 OK
```

---

## Tech Stack

| Concern          | Technology                                 |
|------------------|--------------------------------------------|
| Runtime          | .NET 9                                     |
| Web              | ASP.NET Core 9 Web API                     |
| Persistence      | EF Core 9 + Npgsql (PostgreSQL)            |
| Auth             | Custom `AuthenticationHandler<>` (Basic)   |
| Validation       | DataAnnotations on request records         |
| Testing          | xUnit · FluentAssertions · NSubstitute     |
| Password hashing | BCrypt.Net-Next                            |
| Local infra      | Docker Compose (Postgres on `5433`)        |

---

## Comparison to the Java Sibling (LA1)

| Aspect | Java LA1 | .NET LA-NET |
|---|---|---|
| Entity style | Anemic JPA `@Entity` | Anemic POCO + EF Core column maps |
| Repository | Spring Data `JpaRepository<T, ID>` | None — `DbContext` directly in service |
| Service | `@Service` + `@Transactional` | Plain class, scoped DI; transaction = single `SaveChangesAsync` |
| Controller | `@RestController` + `@RequestMapping` | `[ApiController]` + `[Route]` + `[HttpPost]` etc. |
| Account type dispatch | `if (account.getType() == ...)` | `if (account.Type == ...)` |
| Tier policy | enum methods on `CustomerTier` | extension methods on `CustomerTier` |
| Auth | Spring Security HTTP Basic | `AuthenticationHandler<>` HTTP Basic |
| Error handling | `@ControllerAdvice` | `IExceptionHandler` (.NET 8+) |
| Money | `BigDecimal` + `Currency` enum | `decimal` + `Currency` enum |

The two .NET projects (HA-NET and LA-NET) deliberately share many surface-level decisions (auth handler, exception handler shape, DTO factory pattern) so the architectural contrast — rich domain + ports vs. anemic + fat-service + direct DbContext — is the primary axis of difference.
