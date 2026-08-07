using System.ComponentModel.DataAnnotations;
namespace AyvalikBankLA.Api.Model;

public class Account
{
    public Guid Id { get; set; }

    /// <summary>
    /// Optimistic-lock token. Incremented by the persistence layer on every write and included in
    /// the UPDATE's WHERE clause, so a write based on a stale read affects no rows and EF Core
    /// raises DbUpdateConcurrencyException.
    ///
    /// Without it two concurrent withdrawals both read the same balance, both write their own
    /// result, and one silently disappears while both transaction rows persist.
    /// Mirrors AyvalikBankHA-JAVA Refactorings.md entry 5.
    /// </summary>
    [ConcurrencyCheck]
    public long Version { get; set; }

    public Guid OwnerId { get; set; }
    public Currency Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.ACTIVE;
    public AccountType Type { get; set; } = AccountType.CHECKING;

    // Checking-specific
    public decimal? OverdraftLimit { get; set; }

    // Savings-specific
    public decimal? InterestRate { get; set; }
    public DateOnly? LastAccrualDate { get; set; }

    // Time-deposit-specific
    public decimal? Principal { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public bool? Matured { get; set; }
}
