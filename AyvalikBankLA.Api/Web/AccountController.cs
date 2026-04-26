using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankLA.Api.Web;

[ApiController]
[Route("api")]
[Authorize(Roles = "CUSTOMER")]
public class AccountController(AccountService accountService) : ControllerBase
{
    [HttpPost("accounts")]
    public async Task<IActionResult> Create([FromQuery] Guid ownerId, [FromBody] CreateAccountRequest req)
    {
        var a = await accountService.CreateAccountAsync(ownerId, req.Currency);
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpGet("customers/{customerId:guid}/accounts")]
    public async Task<IActionResult> List(Guid customerId)
    {
        var accounts = await accountService.ListAccountsAsync(customerId);
        return Ok(accounts.Select(AccountResponse.From));
    }

    [HttpGet("accounts/{accountId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid accountId)
    {
        var a = await accountService.GetAccountAsync(accountId);
        return Ok(BalanceResponse.From(a));
    }

    [HttpPost("accounts/{accountId:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await accountService.DepositAsync(accountId, req.Amount, req.Currency);
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await accountService.WithdrawAsync(accountId, req.Amount, req.Currency);
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid accountId, [FromBody] TransferRequest req)
    {
        await accountService.TransferAsync(accountId, req.TargetAccountId, req.Amount, req.Currency);
        return Ok();
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid accountId)
    {
        var txs = await accountService.GetTransactionsAsync(accountId);
        return Ok(txs.Select(TransactionResponse.From));
    }
}
