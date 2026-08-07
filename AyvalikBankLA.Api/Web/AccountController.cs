using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AyvalikBankLA.Api.Web;

[ApiController]
[Route("api")]
[Authorize(Roles = "CUSTOMER")]
public class AccountController(AccountService accountService) : ControllerBase
{
    /// <summary>
    /// The authenticated customer's id, from the ClaimTypes.NameIdentifier claim BasicAuthHandler
    /// sets. Authorization must never trust an id supplied by the caller in a route or query string.
    /// </summary>
    private Guid CallerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("accounts/checking")]
    public async Task<IActionResult> CreateChecking([FromBody] CreateCheckingAccountRequest req)
    {
        var a = await accountService.CreateCheckingAccountAsync(CallerId, req.Currency, req.OverdraftLimit);
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpPost("accounts/savings")]
    public async Task<IActionResult> CreateSavings([FromBody] CreateSavingsAccountRequest req)
    {
        var a = await accountService.CreateSavingsAccountAsync(CallerId, req.Currency, req.AnnualInterestRate);
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpPost("accounts/time-deposit")]
    public async Task<IActionResult> CreateTimeDeposit([FromBody] CreateTimeDepositAccountRequest req)
    {
        var a = await accountService.CreateTimeDepositAccountAsync(
            CallerId, req.Currency, req.Principal, req.MaturityDate, req.AnnualInterestRate);
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpGet("customers/{customerId:guid}/accounts")]
    public async Task<IActionResult> List(Guid customerId)
    {
        var accounts = await accountService.ListAccountsAsync(CallerId, customerId);
        return Ok(accounts.Select(AccountResponse.From));
    }

    [HttpGet("accounts/{accountId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid accountId)
    {
        var a = await accountService.GetAccountAsync(CallerId, accountId);
        return Ok(BalanceResponse.From(a));
    }

    [HttpPost("accounts/{accountId:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await accountService.DepositAsync(CallerId, accountId, req.Amount, req.Currency);
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await accountService.WithdrawAsync(CallerId, accountId, req.Amount, req.Currency);
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid accountId, [FromBody] TransferRequest req)
    {
        await accountService.TransferAsync(CallerId, accountId, req.TargetAccountId, req.Amount, req.Currency);
        return Ok();
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid accountId)
    {
        var txs = await accountService.GetTransactionsAsync(CallerId, accountId);
        return Ok(txs.Select(TransactionResponse.From));
    }
}
