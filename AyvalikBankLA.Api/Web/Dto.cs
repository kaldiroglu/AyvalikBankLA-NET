using System.ComponentModel.DataAnnotations;
using AyvalikBankLA.Api.Model;

namespace AyvalikBankLA.Api.Web.Dto;

// Requests
public record CreateCustomerRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ChangePasswordRequest([Required] string NewPassword);

public record CreateCheckingAccountRequest(
    [Required] Currency Currency,
    decimal? OverdraftLimit);

public record CreateSavingsAccountRequest(
    [Required] Currency Currency,
    [Required, Range(typeof(decimal), "0", "10", ParseLimitsInInvariantCulture = true)] decimal AnnualInterestRate);

public record CreateTimeDepositAccountRequest(
    [Required] Currency Currency,
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Principal,
    [Required] DateOnly MaturityDate,
    [Required, Range(typeof(decimal), "0", "10", ParseLimitsInInvariantCulture = true)] decimal AnnualInterestRate);

public record AccrueInterestRequest(
    [Required, Range(2000, 2100)] int Year,
    [Required, Range(1, 12)] int Month);

public record MoneyOperationRequest(
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [Required] Currency Currency);

public record TransferRequest(
    [Required] Guid TargetAccountId,
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [Required] Currency Currency);

public record SetTransferFeeRequest(
    [Required, Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)] decimal FeePercent);

public record ChangeCustomerTierRequest([Required] CustomerTier Tier);

// Responses
public record CustomerResponse(Guid Id, string Name, string Email, string Role, string Tier)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.Name, c.Email, c.Role, c.Tier.ToString());
}

public record AccountResponse(
    Guid Id, Guid OwnerId, string Currency, decimal Balance, string Status, string Type,
    decimal? OverdraftLimit, decimal? InterestRate, DateOnly? LastAccrualDate,
    decimal? Principal, DateOnly? OpenedOn, DateOnly? MaturityDate, bool? Matured)
{
    public static AccountResponse From(Account a) =>
        new(a.Id, a.OwnerId, a.Currency.ToString(), a.Balance, a.Status.ToString(), a.Type.ToString(),
            a.OverdraftLimit, a.InterestRate, a.LastAccrualDate,
            a.Principal, a.OpenedOn, a.MaturityDate, a.Matured);
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
