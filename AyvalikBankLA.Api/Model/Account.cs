namespace AyvalikBankLA.Api.Model;

public class Account
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Currency Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.ACTIVE;
}
