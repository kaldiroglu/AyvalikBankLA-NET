namespace AyvalikBankLA.Api.Model;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Description { get; set; } = "";
}
