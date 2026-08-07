using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AyvalikBankLA.Tests;

/// <summary>
/// No threads, no sleeps. A lost update is a stale-read problem, not a timing problem, so two
/// DbContexts committing in a fixed order reproduce it deterministically.
///
/// Mirrors AyvalikBankHA-JAVA Refactorings.md entry 5.
/// </summary>
public class AccountOptimisticLockingTests
{
    private readonly DbContextOptions<BankDbContext> _opts =
        new DbContextOptionsBuilder<BankDbContext>()
            .UseInMemoryDatabase($"AyvalikBankLA_lock_{Guid.NewGuid()}")
            .Options;

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = new BankDbContext(_opts);
        var c = new Customer { Id = Guid.NewGuid(), Name = "X", Email = $"{Guid.NewGuid()}@x.com",
            Role = "CUSTOMER", Tier = CustomerTier.STANDARD, CurrentPassword = "h" };
        var a = new Account { Id = Guid.NewGuid(), OwnerId = c.Id, Currency = Currency.USD,
            Balance = 100m, Status = AccountStatus.ACTIVE, Type = AccountType.CHECKING,
            OverdraftLimit = 0m };
        db.Customers.Add(c);
        db.Accounts.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task New_account_starts_at_version_zero()
    {
        var id = await SeedAccountAsync();
        await using var db = new BankDbContext(_opts);
        (await db.Accounts.FindAsync(id))!.Version.Should().Be(0);
    }

    [Fact]
    public async Task Version_increments_on_each_update()
    {
        var id = await SeedAccountAsync();

        for (long expected = 1; expected <= 2; expected++)
        {
            await using (var db = new BankDbContext(_opts))
            {
                var a = await db.Accounts.FindAsync(id);
                a!.Balance += 1m;
                await db.SaveChangesAsync();
            }
            await using var check = new BankDbContext(_opts);
            (await check.Accounts.FindAsync(id))!.Version.Should().Be(expected);
        }
    }

    [Fact]
    public async Task Second_writer_is_rejected_when_both_loaded_the_same_version()
    {
        var id = await SeedAccountAsync();

        await using var db1 = new BankDbContext(_opts);
        await using var db2 = new BankDbContext(_opts);

        // Both read balance 100 at version 0 — this is the stale read.
        var first = await db1.Accounts.FindAsync(id);
        var second = await db2.Accounts.FindAsync(id);

        first!.Balance = 50m;
        await db1.SaveChangesAsync();

        second!.Balance = 50m;
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
