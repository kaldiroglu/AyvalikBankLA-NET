namespace AyvalikBankLA.Api.Model;

public class Account
{
    public Guid Id { get; set; }
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
