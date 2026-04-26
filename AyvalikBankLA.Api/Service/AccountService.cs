using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankLA.Api.Service;

public class AccountService
{
    private readonly BankDbContext _db;
    private readonly TransferService _transferService;

    public AccountService(BankDbContext db, TransferService transferService)
    {
        _db = db;
        _transferService = transferService;
    }

    public async Task<Account> CreateAccountAsync(Guid ownerId, Currency currency)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == ownerId))
            throw new CustomerNotFoundException($"Customer not found: {ownerId}");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Currency = currency,
            Balance = 0m,
            Status = AccountStatus.ACTIVE
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<Transaction> DepositAsync(Guid accountId, decimal amount, Currency currency)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        RequireActive(account);
        if (account.Currency != currency)
            throw new ArgumentException($"Currency mismatch: expected {account.Currency}");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        account.Balance += amount;
        var tx = await SaveTransactionAsync(accountId, TransactionType.DEPOSIT, amount, currency, "Deposit");
        await _db.SaveChangesAsync();
        return tx;
    }

    public async Task<Transaction> WithdrawAsync(Guid accountId, decimal amount, Currency currency)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        RequireActive(account);
        if (account.Currency != currency)
            throw new ArgumentException($"Currency mismatch: expected {account.Currency}");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        if (account.Balance < amount)
            throw new InsufficientFundsException("Insufficient funds");
        account.Balance -= amount;
        var tx = await SaveTransactionAsync(accountId, TransactionType.WITHDRAWAL, amount, currency, "Withdrawal");
        await _db.SaveChangesAsync();
        return tx;
    }

    public async Task TransferAsync(Guid sourceId, Guid targetId, decimal amount, Currency currency)
    {
        var source = await FindAccountOrThrowAsync(sourceId);
        var target = await FindAccountOrThrowAsync(targetId);
        RequireActive(source);
        RequireActive(target);
        if (source.Currency != currency || target.Currency != currency)
            throw new ArgumentException("Currency mismatch on source or target");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");

        var sameCustomer = source.OwnerId == target.OwnerId;
        var feePercent = await GetFeePercentAsync();
        var fee = _transferService.CalculateFee(amount, sameCustomer, feePercent);
        var totalDebit = amount + fee;
        if (source.Balance < totalDebit)
            throw new InsufficientFundsException("Insufficient funds for transfer including fee");

        source.Balance -= totalDebit;
        target.Balance += amount;
        await SaveTransactionAsync(sourceId, TransactionType.TRANSFER_OUT, amount, currency,
            $"Transfer out to {targetId}" + (fee > 0 ? $" (fee: {fee})" : ""));
        await SaveTransactionAsync(targetId, TransactionType.TRANSFER_IN, amount, currency,
            $"Transfer in from {sourceId}");
        await _db.SaveChangesAsync();
    }

    public async Task<Account> GetAccountAsync(Guid accountId) => await FindAccountOrThrowAsync(accountId);

    public async Task<List<Transaction>> GetTransactionsAsync(Guid accountId)
    {
        await FindAccountOrThrowAsync(accountId);
        return await _db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId).ToListAsync();
    }

    public async Task<List<Account>> ListAccountsAsync(Guid ownerId)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == ownerId))
            throw new CustomerNotFoundException($"Customer not found: {ownerId}");
        return await _db.Accounts.AsNoTracking().Where(a => a.OwnerId == ownerId).ToListAsync();
    }

    public async Task FreezeAccountAsync(Guid accountId)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        if (account.Status != AccountStatus.ACTIVE)
            throw new AccountNotOperableException($"Cannot freeze account with status: {account.Status}");
        account.Status = AccountStatus.FROZEN;
        await _db.SaveChangesAsync();
    }

    public async Task UnfreezeAccountAsync(Guid accountId)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        if (account.Status != AccountStatus.FROZEN)
            throw new AccountNotOperableException($"Account is not frozen: {account.Status}");
        account.Status = AccountStatus.ACTIVE;
        await _db.SaveChangesAsync();
    }

    public async Task CloseAccountAsync(Guid accountId)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        if (account.Status == AccountStatus.CLOSED)
            throw new AccountNotOperableException("Account is already closed");
        account.Status = AccountStatus.CLOSED;
        await _db.SaveChangesAsync();
    }

    public async Task SetTransferFeePercentAsync(decimal feePercent)
    {
        var settings = await _db.Settings.FindAsync("TRANSFER_FEE_PERCENT")
            ?? new Settings { Key = "TRANSFER_FEE_PERCENT" };
        settings.Value = feePercent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_db.Entry(settings).State == EntityState.Detached)
            _db.Settings.Add(settings);
        await _db.SaveChangesAsync();
    }

    private void RequireActive(Account a)
    {
        if (a.Status != AccountStatus.ACTIVE)
            throw new AccountNotOperableException($"Account is not active: {a.Status}");
    }

    private async Task<Account> FindAccountOrThrowAsync(Guid id) =>
        await _db.Accounts.FindAsync(id) ?? throw new AccountNotFoundException($"Account not found: {id}");

    private Task<Transaction> SaveTransactionAsync(Guid accountId, TransactionType type, decimal amount, Currency currency, string desc)
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Currency = currency,
            CreatedAt = DateTimeOffset.UtcNow,
            Description = desc
        };
        _db.Transactions.Add(tx);
        return Task.FromResult(tx);
    }

    private async Task<decimal> GetFeePercentAsync()
    {
        var s = await _db.Settings.FindAsync("TRANSFER_FEE_PERCENT");
        return s == null ? 0m : decimal.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
