using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AyvalikBankLA.Tests;

public class AccountServiceTests : IDisposable
{
    private readonly BankDbContext _db;
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        var opts = new DbContextOptionsBuilder<BankDbContext>()
            .UseInMemoryDatabase(databaseName: $"AyvalikBankLA_{Guid.NewGuid()}")
            .Options;
        _db = new BankDbContext(opts);
        _service = new AccountService(_db, new TransferService());
    }

    public void Dispose() { _db.Dispose(); }

    private async Task<Customer> SeedCustomerAsync(CustomerTier tier = CustomerTier.STANDARD)
    {
        var c = new Customer { Id = Guid.NewGuid(), Name = "X", Email = $"{Guid.NewGuid()}@x.com",
            Role = "CUSTOMER", Tier = tier, CurrentPassword = "h" };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task OpensCheckingWithGivenOverdraftLimit()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 100m);
        a.Type.Should().Be(AccountType.CHECKING);
        a.OverdraftLimit.Should().Be(100m);
        a.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task OpensSavingsWithGivenInterestRate()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateSavingsAccountAsync(c.Id, Currency.EUR, 0.03m);
        a.Type.Should().Be(AccountType.SAVINGS);
        a.InterestRate.Should().Be(0.03m);
    }

    [Fact]
    public async Task OpensTimeDepositWithPrincipalAsBalance()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateTimeDepositAccountAsync(c.Id, Currency.USD, 1000m,
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m);
        a.Type.Should().Be(AccountType.TIME_DEPOSIT);
        a.Balance.Should().Be(1000m);
        a.Matured.Should().Be(false);
    }

    [Fact]
    public async Task DepositOnTimeDepositRejected()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateTimeDepositAccountAsync(c.Id, Currency.USD, 1000m,
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m);
        var act = () => _service.DepositAsync(a.Id, 100m, Currency.USD);
        await act.Should().ThrowAsync<AccountNotOperableException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task CheckingAllowsWithdrawIntoOverdraft()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 100m);
        await _service.DepositAsync(a.Id, 50m, Currency.USD);
        await _service.WithdrawAsync(a.Id, 120m, Currency.USD);
        var loaded = await _service.GetAccountAsync(a.Id);
        loaded.Balance.Should().Be(-70m);
    }

    [Fact]
    public async Task RejectsWithdrawBeyondCheckingOverdraft()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 50m);
        var act = () => _service.WithdrawAsync(a.Id, 60m, Currency.USD);
        await act.Should().ThrowAsync<InsufficientFundsException>().WithMessage("*overdraft*");
    }

    [Fact]
    public async Task RejectsWithdrawAboveStandardCap()
    {
        var c = await SeedCustomerAsync(CustomerTier.STANDARD);
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 100000m);
        await _service.DepositAsync(a.Id, 10000m, Currency.USD);
        var act = () => _service.WithdrawAsync(a.Id, 5001m, Currency.USD);
        await act.Should().ThrowAsync<LimitExceededException>().WithMessage("*STANDARD*");
    }

    [Fact]
    public async Task PremiumHalvesTransferFee()
    {
        var c1 = await SeedCustomerAsync(CustomerTier.PREMIUM);
        var c2 = await SeedCustomerAsync();
        var src = await _service.CreateCheckingAccountAsync(c1.Id, Currency.USD, 0m);
        var tgt = await _service.CreateCheckingAccountAsync(c2.Id, Currency.USD, 0m);
        await _service.DepositAsync(src.Id, 1000m, Currency.USD);
        await _service.SetTransferFeePercentAsync(1.0m);
        await _service.TransferAsync(src.Id, tgt.Id, 200m, Currency.USD);
        var loaded = await _service.GetAccountAsync(src.Id);
        // 200 + (1% * 0.5 * 200 = 1.00) = 201 deducted → 799
        loaded.Balance.Should().Be(799m);
    }

    [Fact]
    public async Task RejectsTransferFromTimeDeposit()
    {
        var c1 = await SeedCustomerAsync();
        var c2 = await SeedCustomerAsync();
        var td = await _service.CreateTimeDepositAccountAsync(c1.Id, Currency.USD, 1000m,
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m);
        var tgt = await _service.CreateCheckingAccountAsync(c2.Id, Currency.USD, 0m);
        var act = () => _service.TransferAsync(td.Id, tgt.Id, 100m, Currency.USD);
        await act.Should().ThrowAsync<AccountNotOperableException>().WithMessage("*transfers*");
    }

    [Fact]
    public async Task AccrueInterestOnSavingsCreditsExpected()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateSavingsAccountAsync(c.Id, Currency.USD, 0.12m); // 1% monthly
        await _service.DepositAsync(a.Id, 1000m, Currency.USD);
        var tx = await _service.AccrueInterestAsync(a.Id, 2026, 4);
        tx.Type.Should().Be(TransactionType.INTEREST);
        tx.Amount.Should().Be(10m);
        var loaded = await _service.GetAccountAsync(a.Id);
        loaded.Balance.Should().Be(1010m);
    }

    [Fact]
    public async Task AccrueOnNonSavingsRejected()
    {
        var c = await SeedCustomerAsync();
        var a = await _service.CreateCheckingAccountAsync(c.Id, Currency.USD, 0m);
        var act = () => _service.AccrueInterestAsync(a.Id, 2026, 4);
        await act.Should().ThrowAsync<AccountNotOperableException>().WithMessage("*savings*");
    }
}
