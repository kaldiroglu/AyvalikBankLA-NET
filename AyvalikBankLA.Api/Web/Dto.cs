using System.ComponentModel.DataAnnotations;
using AyvalikBankLA.Api.Model;

namespace AyvalikBankLA.Api.Web.Dto;

// Requests
public record CreateCustomerRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ChangePasswordRequest([Required] string NewPassword);

public record CreateAccountRequest([Required] Currency Currency);

public record MoneyOperationRequest(
    [Required, Range(typeof(decimal), "0.01", "999999999")] decimal Amount,
    [Required] Currency Currency);

public record TransferRequest(
    [Required] Guid TargetAccountId,
    [Required, Range(typeof(decimal), "0.01", "999999999")] decimal Amount,
    [Required] Currency Currency);

public record SetTransferFeeRequest(
    [Required, Range(typeof(decimal), "0", "100")] decimal FeePercent);

// Responses
public record CustomerResponse(Guid Id, string Name, string Email, string Role)
{
    public static CustomerResponse From(Customer c) => new(c.Id, c.Name, c.Email, c.Role);
}

public record AccountResponse(Guid Id, Guid OwnerId, string Currency, decimal Balance, string Status)
{
    public static AccountResponse From(Account a) =>
        new(a.Id, a.OwnerId, a.Currency.ToString(), a.Balance, a.Status.ToString());
}

public record BalanceResponse(decimal Amount, string Currency)
{
    public static BalanceResponse From(Account a) =>
        new(a.Balance, a.Currency.ToString());
}

public record TransactionResponse(
    Guid Id, Guid AccountId, string Type, decimal Amount, string Currency,
    DateTimeOffset CreatedAt, string Description)
{
    public static TransactionResponse From(Transaction t) =>
        new(t.Id, t.AccountId, t.Type.ToString(), t.Amount, t.Currency.ToString(), t.CreatedAt, t.Description);
}
