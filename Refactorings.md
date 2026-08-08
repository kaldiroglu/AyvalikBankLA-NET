# Refactorings

Claude Opus 5 (1M context) — created 2026-08-08

A log of significant refactorings applied to Ayvalık Bank LA-NET.

For further enquiry please contact Akin Kaldiroglu at akin@kaldiroglu.dev

**Relationship to the other implementations.** This repository is one of six: hexagonal and layered,
in Java, .NET and Python. Five refactorings were designed in `AyvalikBankHA-JAVA`; **only two apply
here** — see *Refactorings that do not apply* at the end, which is the most interesting part of this
file. All six are held to one HTTP contract by `AyvalikBankContractTests`.

---

## Entry 1 — Ownership authorization: a rule that could not be said

**Baseline:** `0c3f9dd` · **Commit:** `7016520`

### The symptom

Any authenticated customer could operate on any other customer's data:

- `ChangePassword(Guid id, ...)` took its target from the route, gated only by
  `[Authorize(Roles = "CUSTOMER")]`. **Any customer could set any other customer's password, then
  log in as them.**
- Given an account id, any customer could deposit to it, withdraw from it, transfer out of it, and
  read its balance and transaction history.
- `CreateChecking([FromQuery] Guid ownerId, ...)` let a customer open accounts owned by anyone.

### The tell

`UnauthorizedAccessException` existed and `GlobalExceptionHandler` mapped it to 403. **No production
code threw it.**

> **An exception nothing throws is a rule nothing enforces.** Across the six repositories this
> two-minute grep found three real holes.

### The root cause

No service method took the caller, so "the caller must own this account" was **inexpressible** — not
merely unenforced. A signature declares what an operation is permitted to consider.

### What made this port easy

`BasicAuthHandler` already put the customer id in `ClaimTypes.NameIdentifier` and nothing read it.
No new infrastructure was needed — the identity was already in every request, unused.

| Situation | Technique |
|---|---|
| The resource *is* the caller's | **Delete the parameter** — `?ownerId=` is gone |
| The route names a customer | **Require self** |
| The route names an account | **Require ownership** |

**Prefer deleting a parameter to validating it.** A validated parameter must be validated everywhere,
forever; a deleted one is gone.

### The transfer asymmetry

The caller must own the **source** only. The target is deliberately unchecked — sending money to
other people is the product — and a test pins that so the obvious hardening fails loudly.

### A C#-specific hazard

The project's own `UnauthorizedAccessException` collides with `System.UnauthorizedAccessException`,
so throw sites must be fully qualified.

---

## Entry 2 — Optimistic locking

**Baseline:** `7016520` · **Commit:** `e776c22`

### The symptom

Two concurrent withdrawals of 50 from a balance of 100 both read 100 and both wrote 50. The balance
ended at **50** where it should be **0**, with **both** `Transaction` rows written — money created
from nothing, ledger contradicting the account.

### Why this port was straightforward

The layered service mutates tracked entities on the `DbContext` directly, so a `[ConcurrencyCheck]`
token closes the hole. Compare `AyvalikBankHA-NET`, whose adapter read with `AsNoTracking()` — there,
the token would have incremented forever while never detecting a conflict, and the **read** path had
to change first.

> **An ORM can only protect a row you actually loaded.** The mapping layer that buys the hexagonal
> repository its independence is exactly what put that claim at risk.

### Where the increment lives

In `BankDbContext.SaveChangesAsync`, not in each service method:

```csharp
foreach (var entry in ChangeTracker.Entries<Account>())
    if (entry.State == EntityState.Modified)
        entry.Entity.Version++;
```

`AccountService` writes accounts from a dozen places. **A token one of them forgets to bump is a
guard that silently does nothing** — the same failure family as the dead exception in entry 1.

### The test needs no threads

Two `DbContext` instances committing in a fixed order reproduce the bug deterministically. **A lost
update is a stale-read problem, not a timing problem.** `DbUpdateConcurrencyException` maps to **409
Conflict** with a fixed message rather than EF's, which names the entity and key.

---

## Entry 3 — Two API defects the contract suite found

**Baseline:** `e776c22` · **Commit:** `23f088c`

`AyvalikBankContractTests` is a black-box HTTP suite run against all six implementations. Neither
defect below could have been caught by any test in this repository — because **there are no
controller tests here**; the service tests never construct an HTTP request.

### Money movement was broken on any comma-decimal locale

```csharp
[Range(typeof(decimal), "0.01", "999999999")]
```

`RangeAttribute` parses its string bounds using the **current culture**. On the author's machine
(`en_TR`, decimal separator `,`) `"0.01"` failed to parse and **every deposit, withdrawal and
transfer returned 400** with `"0.01 is not a valid value for Decimal"`.

Not an edge case — the core operation of a bank did not work. It would have passed in most CI
(`en-US`) and failed for every user in Europe. Six attributes were affected;
`ParseLimitsInInvariantCulture = true` fixes them.

> **Bounds written in source should never be locale-sensitive.**

### Enums had to be sent as numbers

`System.Text.Json` deserializes enums numerically unless `JsonStringEnumConverter` is registered, so
this API wanted `{"currency": 0}` while the Java and Python implementations use `{"currency": "USD"}`.
Responses already emitted strings — the API was asymmetric with itself.

---

## Entry 4 — One exception-vocabulary divergence

**Commit:** `c810199`

While porting the missing orchestration tests, one rule turned out to be classified differently here
than in `AyvalikBankLA-Python`: "time deposit has not matured" and five related state rules. This
repository and `AyvalikBankLA-JAVA` agreed on `AccountNotOperableException`; Python was the outlier
and was aligned to match.

**Both map to HTTP 422, so the shared contract suite could not see the difference.** That is the
honest limit of a black-box suite: it proves the six agree on *what clients observe*, not on *how
they reason internally*. Two implementations can be contract-identical and still disagree about what
kind of failure just occurred — which matters the moment someone catches a specific type, or reads
the code to learn the domain.

The two layers are complementary, and neither would have found the other's bugs.

---

## Refactorings that do not apply here — and why

Three of the five refactorings from `AyvalikBankHA-JAVA` were deliberately **not** ported.

### `TransactionAmount` (HA entry 1)

It wraps a `Money` value object. **This repository has no `Money`** — amounts are raw `decimal`
passed alongside a separate `Currency`. Introducing it would mean first introducing `Money`, moving
the layered design toward the rich domain model the hexagonal repositories exist to contrast with.

### Actor-shaped ports (HA entry 2)

Layered architecture has no ports. Controllers call services directly.

### A refusal vocabulary (HA entry 4)

**Zero catch blocks in `AccountService`. Zero raw `InvalidOperationException`. Zero message
matching.**

`AyvalikBankHA-NET` needed that refactoring badly — its application layer decided the HTTP status by
running `e.Message.Contains("frozen")`, so rewording a domain message silently changed the response.
That defect exists *because* hexagonal separates domain from application and the refusal must be
translated across the seam. A layered service throws the mapped exception directly. No seam, no
defect.

### The conclusion worth teaching

**Three of the five refactorings are artifacts of the hexagonal boundary.** The layered
implementation is not behind; it is structurally incapable of those defects, and pays for it
elsewhere — an anemic model and logic concentrated in services.

That trade is the point of keeping both architectures, and it shows more clearly in what *didn't*
need fixing than in what did.

---

## Deliberate non-goals

- **`Customer` has the same lost-update exposure** as `Account`.
- **No retry-on-conflict.** A 409 tells the client to retry.
- **`ChangePassword` does not verify the current password.** Defensible under HTTP Basic; not once
  sessions arrive.
- **No controller tests.** The web layer is covered by `AyvalikBankContractTests`, which found entry
  3 and needs a running instance.

## Discussion questions

1. Entry 3's culture bug passed every test here. What class of defect can *only* be found by a test
   that speaks HTTP?
2. Entry 4 was invisible to the contract suite. What would catch that class of divergence?
3. Entry 2 puts the version bump in `SaveChangesAsync`. Argue for putting it in each service method.
