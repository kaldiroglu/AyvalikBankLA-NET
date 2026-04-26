# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Ayvalık Bank LA-NET** — .NET 9 / ASP.NET Core port of `AyvalikBankLA1` (the Java/Spring Boot layered project). Identical use cases, same 3-tier / anemic-model / fat-service style.

## Commands

```bash
docker compose up -d                         # Postgres on port 5433
dotnet build
dotnet test
dotnet run --project AyvalikBankLA.Api
```

## Architecture

Classic 3-Tier Layered. Direct dependencies: Controller → Service → DbContext.

```
Web/            — controllers, DTOs, GlobalExceptionHandler
Service/        — CustomerService, AccountService, PasswordValidationService, TransferService
Repository/     — BankDbContext (EF Core)
Model/          — anemic POCO entities + enums
Exception/      — typed exception classes
Config/         — BasicAuthHandler, AdminSeeder
```

## Key Decisions (preserved from the Java sibling)

- **Anemic model.** Entities have auto-properties only. No business methods.
- **Business logic in services.** Status guards, balance checks, fee calc, type-specific behavior — all in `AccountService` / `CustomerService`.
- **No repository abstraction.** Services hold `BankDbContext` directly. The .NET-idiomatic equivalent of Spring Data — no `IRepository<T>` ceremony.
- **DTO `From(entity)` factory methods** mirror the Java `from(Entity)` pattern.
- **`decimal` for money** instead of Java's `BigDecimal` (no precision ceremony needed).
- **Single `Account` table with `Type` discriminator + nullable type-specific columns** (`OverdraftLimit`, `InterestRate`, `LastAccrualDate`, `Principal`, `OpenedOn`, `MaturityDate`, `Matured`). `AccountService` dispatches behavior with `if (type == ...)` — preserving the layered/anemic style.
- **Customer tiers** as a `CustomerTier` enum + extension-method policy data: `FeeMultiplier()` (1.0×/0.5×/0.0×) and per-transaction caps (5k/50k/unlimited transfer; 5k/25k/unlimited withdrawal). Same-customer transfers are always free.

## Default Admin

`admin@ayvalikbank.dev` / `Admin@123!` (seeded by `AdminSeeder` on first startup, with `Tier = STANDARD`)
