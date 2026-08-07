using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankLA.Api.Service;

public class AccountService
{
    private const int MonthsPerYear = 12;
    private readonly BankDbContext _db;
    private readonly TransferService _transferService;

    public AccountService(BankDbContext db, TransferService transferService)
    {
        _db = db;
        _transferService = transferService;
    }

    // ── Account opening (one method per type) ─────────────────────────────

    public async Task<Account> CreateCheckingAccountAsync(Guid callerId, Currency currency, decimal? overdraftLimit)
    {
        await RequireCustomerExistsAsync(callerId);
        var od = overdraftLimit ?? 0m;
        if (od < 0) throw new ArgumentException("Overdraft limit cannot be negative");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = callerId,
            Currency = currency,
            Balance = 0m,
            Status = AccountStatus.ACTIVE,
            Type = AccountType.CHECKING,
            OverdraftLimit = od
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<Account> CreateSavingsAccountAsync(Guid callerId, Currency currency, decimal annualInterestRate)
    {
        await RequireCustomerExistsAsync(callerId);
        if (annualInterestRate < 0) throw new ArgumentException("Annual interest rate must be non-negative");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = callerId,
            Currency = currency,
            Balance = 0m,
            Status = AccountStatus.ACTIVE,
            Type = AccountType.SAVINGS,
            InterestRate = annualInterestRate
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<Account> CreateTimeDepositAccountAsync(Guid callerId, Currency currency,
        decimal principal, DateOnly maturityDate, decimal annualInterestRate)
    {
        await RequireCustomerExistsAsync(callerId);
        if (principal <= 0) throw new ArgumentException("Principal must be positive");
        if (annualInterestRate < 0) throw new ArgumentException("Annual interest rate must be non-negative");
        var openedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        if (maturityDate <= openedOn) throw new ArgumentException("Maturity date must be after today");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = callerId,
            Currency = currency,
            Balance = principal,
            Status = AccountStatus.ACTIVE,
            Type = AccountType.TIME_DEPOSIT,
            Principal = principal,
            OpenedOn = openedOn,
            MaturityDate = maturityDate,
            InterestRate = annualInterestRate,
            Matured = false
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    // ── Account operations ────────────────────────────────────────────────

    public async Task<Transaction> DepositAsync(Guid callerId, Guid accountId, decimal amount, Currency currency)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        RequireOwner(account, callerId);
        RequireActive(account);
        if (account.Type == AccountType.TIME_DEPOSIT)
            throw new AccountNotOperableException("Time deposit principal is locked — further deposits are not allowed");
        if (account.Currency != currency)
            throw new ArgumentException($"Currency mismatch: expected {account.Currency}");
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        account.Balance += amount;
        var tx = SaveTransaction(accountId, TransactionType.DEPOSIT, amount, currency, "Deposit");
        await _db.SaveChangesAsync();
        return tx;
    }

    public async Task<Transaction> WithdrawAsync(Guid callerId, Guid accountId, decimal amount, Currency currency)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        RequireOwner(account, callerId);
        RequireActive(account);
        if (account.Currency != currency)
            throw new ArgumentException($"Currency mismatch: expected {account.Currency}");
        if (amount <= 0) throw new ArgumentException("Amount must be positive");

        var owner = await FindCustomerOrThrowAsync(account.OwnerId);
        _transferService.RequireWithdrawalWithinLimit(amount, owner.Tier);

        if (account.Type == AccountType.TIME_DEPOSIT && account.Matured != true)
            throw new AccountNotOperableException("Time deposit has not matured");

        var projected = account.Balance - amount;
        if (account.Type == AccountType.CHECKING)
        {
            var floor = -(account.OverdraftLimit ?? 0m);
            if (projected < floor)
            {
                if ((account.OverdraftLimit ?? 0m) == 0m)
                    throw new InsufficientFundsException("Insufficient funds");
                throw new InsufficientFundsException("Withdrawal exceeds overdraft limit");
            }
        }
        else if (projected < 0m)
        {
            throw new InsufficientFundsException("Insufficient funds");
        }

        account.Balance = projected;
        var tx = SaveTransaction(accountId, TransactionType.WITHDRAWAL, amount, currency, "Withdrawal");
        await _db.SaveChangesAsync();
        return tx;
    }

    public async Task TransferAsync(Guid callerId, Guid sourceId, Guid targetId, decimal amount, Currency currency)
    {
        var source = await FindAccountOrThrowAsync(sourceId);
        RequireOwner(source, callerId);
        // The TARGET is deliberately NOT ownership-checked: sending money to another
        // customer is the entire point of a transfer.
        var target = await FindAccountOrThrowAsync(targetId);
        RequireActive(source);
        RequireActive(target);
        if (source.Type == AccountType.TIME_DEPOSIT)
            throw new AccountNotOperableException("Time deposit accounts do not support transfers");
        if (source.Currency != currency || target.Currency != currency)
            throw new ArgumentException("Currency mismatch on source or target");
        if (amount <= 0) throw new ArgumentException("Amount must be positive");

        var sourceOwner = await FindCustomerOrThrowAsync(source.OwnerId);
        _transferService.RequireTransferWithinLimit(amount, sourceOwner.Tier);

        var sameCustomer = source.OwnerId == target.OwnerId;
        var feePercent = await GetFeePercentAsync();
        var fee = _transferService.CalculateFee(amount, sameCustomer, feePercent, sourceOwner.Tier);
        var totalDebit = amount + fee;

        var projected = source.Balance - totalDebit;
        if (source.Type == AccountType.CHECKING)
        {
            var floor = -(source.OverdraftLimit ?? 0m);
            if (projected < floor)
                throw new InsufficientFundsException("Insufficient funds for transfer including fee");
        }
        else if (projected < 0m)
        {
            throw new InsufficientFundsException("Insufficient funds for transfer including fee");
        }

        source.Balance = projected;
        target.Balance += amount;

        var outDesc = $"Transfer out to {targetId}" + (fee > 0 ? $" (fee: {fee})" : "");
        SaveTransaction(sourceId, TransactionType.TRANSFER_OUT, amount, currency, outDesc);
        SaveTransaction(targetId, TransactionType.TRANSFER_IN, amount, currency, $"Transfer in from {sourceId}");
        await _db.SaveChangesAsync();
    }

    // ── Savings: monthly interest accrual ────────────────────────────────

    public async Task<Transaction> AccrueInterestAsync(Guid accountId, int year, int month)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        if (account.Type != AccountType.SAVINGS)
            throw new AccountNotOperableException("Account is not a savings account");
        if (account.Status == AccountStatus.CLOSED)
            throw new AccountNotOperableException("Cannot accrue interest on a closed account");

        var firstOfNextMonth = new DateOnly(year, month, 1).AddMonths(1);
        if (account.LastAccrualDate is { } last && firstOfNextMonth <= last)
            throw new AccountNotOperableException($"Interest already accrued for or after {year:D4}-{month:D2}");

        var monthlyRate = (account.InterestRate ?? 0m) / MonthsPerYear;
        var interest = Math.Round(account.Balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
        account.Balance += interest;
        account.LastAccrualDate = firstOfNextMonth;

        var tx = SaveTransaction(accountId, TransactionType.INTEREST, interest, account.Currency,
            $"Interest accrual for {year:D4}-{month:D2}");
        await _db.SaveChangesAsync();
        return tx;
    }

    // ── Time deposit: maturation ─────────────────────────────────────────

    public async Task<Transaction> MatureTimeDepositAsync(Guid accountId)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        if (account.Type != AccountType.TIME_DEPOSIT)
            throw new AccountNotOperableException("Account is not a time deposit");
        if (account.Status == AccountStatus.CLOSED)
            throw new AccountNotOperableException("Cannot mature a closed account");
        if (account.Matured == true)
            throw new AccountNotOperableException("Account is already matured");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (account.MaturityDate is null || today < account.MaturityDate)
            throw new AccountNotOperableException("Maturity date not yet reached");

        var months = MonthsBetween(account.OpenedOn!.Value, account.MaturityDate!.Value);
        var years = (decimal)months / MonthsPerYear;
        var interest = Math.Round((account.Principal ?? 0m) * (account.InterestRate ?? 0m) * years,
            2, MidpointRounding.AwayFromZero);
        account.Balance += interest;
        account.Matured = true;

        var tx = SaveTransaction(accountId, TransactionType.INTEREST, interest, account.Currency,
            "Maturity interest credit");
        await _db.SaveChangesAsync();
        return tx;
    }

    private static int MonthsBetween(DateOnly start, DateOnly end) =>
        (end.Year - start.Year) * 12 + (end.Month - start.Month);

    // ── Read-only queries ─────────────────────────────────────────────────

    public async Task<Account> GetAccountAsync(Guid callerId, Guid accountId)
    {
        var account = await FindAccountOrThrowAsync(accountId);
        RequireOwner(account, callerId);
        return account;
    }

    public async Task<List<Transaction>> GetTransactionsAsync(Guid callerId, Guid accountId)
    {
        RequireOwner(await FindAccountOrThrowAsync(accountId), callerId);
        await FindAccountOrThrowAsync(accountId);
        return await _db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId).ToListAsync();
    }

    public async Task<List<Account>> ListAccountsAsync(Guid callerId, Guid ownerId)
    {
        RequireSelf(ownerId, callerId);
        await RequireCustomerExistsAsync(ownerId);
        return await _db.Accounts.AsNoTracking().Where(a => a.OwnerId == ownerId).ToListAsync();
    }

    // ── Status transitions ────────────────────────────────────────────────

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

    // ── Settings ──────────────────────────────────────────────────────────

    // Security: the caller must own the account.
    // Mirrors AyvalikBankHA-JAVA Refactorings.md entry 3.
    private static void RequireOwner(Account account, Guid callerId)
    {
        if (account.OwnerId != callerId)
            throw new AyvalikBankLA.Api.Exception.UnauthorizedAccessException("Account does not belong to the caller");
    }

    private static void RequireSelf(Guid subject, Guid callerId)
    {
        if (subject != callerId)
            throw new AyvalikBankLA.Api.Exception.UnauthorizedAccessException("Callers may only act on their own customer record");
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

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RequireActive(Account a)
    {
        if (a.Status != AccountStatus.ACTIVE)
            throw new AccountNotOperableException($"Account is not active: {a.Status}");
    }

    private async Task RequireCustomerExistsAsync(Guid id)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == id))
            throw new CustomerNotFoundException($"Customer not found: {id}");
    }

    private async Task<Account> FindAccountOrThrowAsync(Guid id) =>
        await _db.Accounts.FindAsync(id) ?? throw new AccountNotFoundException($"Account not found: {id}");

    private async Task<Customer> FindCustomerOrThrowAsync(Guid id) =>
        await _db.Customers.FindAsync(id) ?? throw new CustomerNotFoundException($"Customer not found: {id}");

    private Transaction SaveTransaction(Guid accountId, TransactionType type, decimal amount, Currency currency, string desc)
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
        return tx;
    }

    private async Task<decimal> GetFeePercentAsync()
    {
        var s = await _db.Settings.FindAsync("TRANSFER_FEE_PERCENT");
        return s == null ? 0m : decimal.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
