using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AyvalikBankLA.Tests;

/// <summary>
/// Any authenticated customer could previously operate on any account given its id, and set any
/// other customer's password. UnauthorizedAccessException was mapped to 403 but never thrown by
/// production code. Mirrors AyvalikBankHA-JAVA Refactorings.md entry 3.
/// </summary>
public class AccountOwnershipTests : IDisposable
{
    private readonly BankDbContext _db;
    private readonly AccountService _service;

    public AccountOwnershipTests()
    {
        var opts = new DbContextOptionsBuilder<BankDbContext>()
            .UseInMemoryDatabase(databaseName: $"AyvalikBankLA_own_{Guid.NewGuid()}")
            .Options;
        _db = new BankDbContext(opts);
        _service = new AccountService(_db, new TransferService());
    }

    public void Dispose() => _db.Dispose();

    private async Task<Customer> SeedCustomerAsync()
    {
        var c = new Customer { Id = Guid.NewGuid(), Name = "X", Email = $"{Guid.NewGuid()}@x.com",
            Role = "CUSTOMER", Tier = CustomerTier.STANDARD, CurrentPassword = "h" };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task Deposit_into_another_customers_account_is_rejected()
    {
        var owner = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);

        var act = () => _service.DepositAsync(Guid.NewGuid(), a.Id, 100m, Currency.USD);

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Withdrawal_from_another_customers_account_is_rejected()
    {
        var owner = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);

        var act = () => _service.WithdrawAsync(Guid.NewGuid(), a.Id, 10m, Currency.USD);

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Transfer_out_of_another_customers_account_is_rejected()
    {
        var owner = await SeedCustomerAsync();
        var intruder = await SeedCustomerAsync();
        var src = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);
        var tgt = await _service.CreateCheckingAccountAsync(intruder.Id, Currency.USD, 0m);

        var act = () => _service.TransferAsync(intruder.Id, src.Id, tgt.Id, 10m, Currency.USD);

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Reading_another_customers_account_is_rejected()
    {
        var owner = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);

        var act = () => _service.GetAccountAsync(Guid.NewGuid(), a.Id);

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Reading_another_customers_transactions_is_rejected()
    {
        var owner = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);

        var act = () => _service.GetTransactionsAsync(Guid.NewGuid(), a.Id);

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Listing_another_customers_accounts_is_rejected()
    {
        var act = () => _service.ListAccountsAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<Api.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task The_transfer_target_is_deliberately_not_ownership_checked()
    {
        var sender = await SeedCustomerAsync();
        var recipient = await SeedCustomerAsync();
        var src = await _service.CreateCheckingAccountAsync(sender.Id, Currency.USD, 0m);
        var tgt = await _service.CreateCheckingAccountAsync(recipient.Id, Currency.USD, 0m);
        await _service.DepositAsync(sender.Id, src.Id, 500m, Currency.USD);

        await _service.TransferAsync(sender.Id, src.Id, tgt.Id, 100m, Currency.USD);

        var loaded = await _service.GetAccountAsync(recipient.Id, tgt.Id);
        loaded.Balance.Should().Be(100m);
    }
}
