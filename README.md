# Ayvalık Bank LA-NET

A banking application built as a learning project to demonstrate **Classic 3-Tier Layered Architecture** in **.NET 9 / ASP.NET Core**. .NET counterpart to `AyvalikBankLA1` (Java/Spring Boot).

## Tech Stack

| Concern | Technology |
|---------|-----------|
| Runtime | .NET 9 |
| Framework | ASP.NET Core 9 (Web API) |
| Persistence | EF Core 9 + Npgsql (PostgreSQL) |
| Security | Custom Basic Auth handler |
| Validation | DataAnnotations |
| Testing | xUnit · FluentAssertions · NSubstitute |
| Password hashing | BCrypt.Net-Next |
| Infrastructure | Docker Compose (PostgreSQL on port 5433) |

## Quick Start

```bash
docker compose up -d
dotnet run --project AyvalikBankLA.Api
```

Default admin: `admin@ayvalikbank.dev` / `Admin@123!` (seeded on first startup)

## Project Layout

```
AyvalikBankLA.Api/
  Model/         — anemic entity classes + enums
  Repository/    — EF Core BankDbContext
  Service/       — fat services with all business logic
  Web/           — controllers + DTOs + GlobalExceptionHandler
  Config/        — BasicAuthHandler, AdminSeeder
  Exception/     — typed exceptions
  Program.cs     — DI + middleware wiring
AyvalikBankLA.Tests/
  *.cs           — xUnit tests
```

## Architectural notes

- **Anemic entities** — `Customer`, `Account`, `Transaction` are plain POCOs with auto-properties only
- **Fat services** — `CustomerService` and `AccountService` own all business logic, including type-specific dispatch (`if (type == ...)`)
- **Single `Account` table with `Type` discriminator** + nullable type-specific columns (overdraft, interest rate, principal, maturity date, etc.) — preserves the layered/anemic style
- **Customer tiers** (`STANDARD / PREMIUM / PRIVATE`) on `Customer`, with extension-method policy data: `FeeMultiplier` (1.0×/0.5×/0.0×) and per-transaction caps
- **No repository abstraction** — services depend on `BankDbContext` directly (Spring Data equivalent in .NET style)
- **`decimal` for money** — no `BigDecimal` ceremony like the Java sibling

## Endpoints

| Method | Path | Role |
|---|---|---|
| POST | `/api/admin/customers` | ADMIN |
| DELETE | `/api/admin/customers/{id}` | ADMIN |
| GET | `/api/admin/customers` | ADMIN |
| PUT | `/api/admin/settings/transfer-fee` | ADMIN |
| PUT | `/api/admin/accounts/{id}/freeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/unfreeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/close` | ADMIN |
| PUT | `/api/admin/customers/{id}/tier` | ADMIN |
| PUT | `/api/admin/accounts/{id}/accrue-interest` | ADMIN |
| PUT | `/api/admin/accounts/{id}/mature` | ADMIN |
| PUT | `/api/customers/{id}/password` | CUSTOMER |
| POST | `/api/accounts/checking?ownerId=` | CUSTOMER |
| POST | `/api/accounts/savings?ownerId=` | CUSTOMER |
| POST | `/api/accounts/time-deposit?ownerId=` | CUSTOMER |
| GET | `/api/customers/{id}/accounts` | CUSTOMER |
| GET | `/api/accounts/{id}/balance` | CUSTOMER |
| POST | `/api/accounts/{id}/deposit` | CUSTOMER |
| POST | `/api/accounts/{id}/withdraw` | CUSTOMER |
| POST | `/api/accounts/{id}/transfer` | CUSTOMER |
| GET | `/api/accounts/{id}/transactions` | CUSTOMER |

## Test coverage

28 unit tests (xUnit + FluentAssertions, EF Core InMemory for service tests), covering:
- TransferService tier-aware fees and per-transaction limits
- CustomerTier policy data
- AccountService: opens per type, time-deposit deposit/transfer rejection, checking overdraft happy + cap rejection, tier-cap rejection, premium fee discount, savings monthly accrual
