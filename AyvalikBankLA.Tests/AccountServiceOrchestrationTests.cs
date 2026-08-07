using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AyvalikBankLA.Tests;

/// <summary>
/// Orchestration coverage that AccountServiceTests did not have: repository lookups, status
/// transitions, transfer fees and not-found handling. Ported from AyvalikBankLA-JAVA's
/// AccountServiceTest.
/// </summary>
public class AccountServiceOrchestrationTests : IDisposable
{
    private readonly BankDbContext _db;
    private readonly AccountService _service;

    public AccountServiceOrchestrationTests()
    {
        var opts = new DbContextOptionsBuilder<BankDbContext>()
            .UseInMemoryDatabase($"AyvalikBankLA_orch_{Guid.NewGuid()}")
            .Options;
        _db = new BankDbContext(opts);
        _service = new AccountService(_db, new TransferService());
    }

    public void Dispose() => _db.Dispose();

    private async Task<Customer> SeedCustomerAsync(CustomerTier tier = CustomerTier.STANDARD)
    {
        var c = new Customer { Id = Guid.NewGuid(), Name = "X", Email = $"{Guid.NewGuid()}@x.com",
            Role = "CUSTOMER", Tier = tier, CurrentPassword = "h" };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    private async Task<(Customer owner, Account account)> FundedAsync(decimal amount, CustomerTier tier = CustomerTier.STANDARD)
    {
        var c = await SeedCustomerAsync(tier);
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 0m);
        if (amount > 0) await _service.DepositAsync(c.Id, a.Id, amount, Currency.USD);
        return (c, a);
    }

    [Fact]
    public async Task Creating_an_account_for_a_missing_customer_is_not_found()
    {
        var act = () => _service.CreateCheckingAccountAsync(Guid.NewGuid(), Currency.USD, 0m);

        await act.Should().ThrowAsync<CustomerNotFoundException>();
    }

    [Fact]
    public async Task Deposit_credits_the_account()
    {
        var (owner, a) = await FundedAsync(0m);

        await _service.DepositAsync(owner.Id, a.Id, 200m, Currency.USD);

        (await _service.GetAccountAsync(owner.Id, a.Id)).Balance.Should().Be(200m);
    }

    [Fact]
    public async Task Deposit_into_a_missing_account_is_not_found()
    {
        var act = () => _service.DepositAsync(Guid.NewGuid(), Guid.NewGuid(), 10m, Currency.USD);

        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact]
    public async Task Withdrawal_beyond_the_balance_is_rejected()
    {
        var (owner, a) = await FundedAsync(100m);

        var act = () => _service.WithdrawAsync(owner.Id, a.Id, 500m, Currency.USD);

        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task Transfer_between_one_customers_own_accounts_is_free()
    {
        var (owner, src) = await FundedAsync(500m);
        var tgt = await _service.CreateCheckingAccountAsync(owner.Id, Currency.USD, 0m);

        await _service.TransferAsync(owner.Id, src.Id, tgt.Id, 200m, Currency.USD);

        (await _service.GetAccountAsync(owner.Id, src.Id)).Balance.Should().Be(300m);
        (await _service.GetAccountAsync(owner.Id, tgt.Id)).Balance.Should().Be(200m);
    }

    [Fact]
    public async Task Transfer_between_different_customers_deducts_the_fee()
    {
        var (sender, src) = await FundedAsync(1000m);
        var recipient = await SeedCustomerAsync();
        var tgt = await _service.CreateCheckingAccountAsync(recipient.Id, Currency.USD, 0m);
        await _service.SetTransferFeePercentAsync(1.0m);

        await _service.TransferAsync(sender.Id, src.Id, tgt.Id, 200m, Currency.USD);

        (await _service.GetAccountAsync(sender.Id, src.Id)).Balance.Should().Be(798m);
        (await _service.GetAccountAsync(recipient.Id, tgt.Id)).Balance.Should().Be(200m);
    }

    [Fact]
    public async Task Transfer_above_the_standard_cap_is_rejected()
    {
        var (sender, src) = await FundedAsync(10000m);
        var recipient = await SeedCustomerAsync();
        var tgt = await _service.CreateCheckingAccountAsync(recipient.Id, Currency.USD, 0m);

        var act = () => _service.TransferAsync(sender.Id, src.Id, tgt.Id, 5001m, Currency.USD);

        await act.Should().ThrowAsync<LimitExceededException>();
    }

    [Fact]
    public async Task Freezes_then_unfreezes_an_account()
    {
        var (owner, a) = await FundedAsync(100m);

        await _service.FreezeAccountAsync(a.Id);
        (await _service.GetAccountAsync(owner.Id, a.Id)).Status.Should().Be(AccountStatus.FROZEN);

        await _service.UnfreezeAccountAsync(a.Id);
        (await _service.GetAccountAsync(owner.Id, a.Id)).Status.Should().Be(AccountStatus.ACTIVE);
    }

    [Fact]
    public async Task Closes_an_account()
    {
        var (owner, a) = await FundedAsync(0m);

        await _service.CloseAccountAsync(a.Id);

        (await _service.GetAccountAsync(owner.Id, a.Id)).Status.Should().Be(AccountStatus.CLOSED);
    }

    [Fact]
    public async Task Freezing_a_closed_account_is_not_operable()
    {
        var (_, a) = await FundedAsync(0m);
        await _service.CloseAccountAsync(a.Id);

        var act = () => _service.FreezeAccountAsync(a.Id);

        await act.Should().ThrowAsync<AccountNotOperableException>();
    }

    [Fact]
    public async Task Freezing_a_missing_account_is_not_found()
    {
        var act = () => _service.FreezeAccountAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact]
    public async Task Maturing_a_non_time_deposit_is_rejected()
    {
        var (_, a) = await FundedAsync(0m);

        var act = () => _service.MatureTimeDepositAsync(a.Id);

        // NOTE: this repo raises AccountNotOperableException where the Java implementations raise
        // InvalidAccountOperationException. Both map to HTTP 422, so the shared contract suite
        // cannot see the difference. Reconciling the exception vocabulary is separate work.
        await act.Should().ThrowAsync<AccountNotOperableException>();
    }

    [Fact]
    public async Task Withdrawal_from_an_unmatured_time_deposit_is_rejected()
    {
        var c = await SeedCustomerAsync();
        var td = await _service.CreateTimeDepositAccountAsync(
            c.Id, Currency.USD, 1000m, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m);

        var act = () => _service.WithdrawAsync(c.Id, td.Id, 100m, Currency.USD);

        await act.Should().ThrowAsync<AccountNotOperableException>();
    }
}
