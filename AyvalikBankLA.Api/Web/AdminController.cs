using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankLA.Api.Web;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController(CustomerService customerService, AccountService accountService) : ControllerBase
{
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req)
    {
        var c = await customerService.CreateCustomerAsync(req.Name, req.Email, req.Password);
        return StatusCode(201, CustomerResponse.From(c));
    }

    [HttpDelete("customers/{id:guid}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        await customerService.DeleteCustomerAsync(id);
        return NoContent();
    }

    [HttpGet("customers")]
    public async Task<IActionResult> ListCustomers()
    {
        var list = await customerService.ListCustomersAsync();
        return Ok(list.Select(CustomerResponse.From));
    }

    [HttpPut("settings/transfer-fee")]
    public async Task<IActionResult> SetTransferFee([FromBody] SetTransferFeeRequest req)
    {
        await accountService.SetTransferFeePercentAsync(req.FeePercent);
        return Ok();
    }

    [HttpPut("accounts/{id:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid id) { await accountService.FreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(Guid id) { await accountService.UnfreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id) { await accountService.CloseAccountAsync(id); return Ok(); }

    [HttpPut("customers/{id:guid}/tier")]
    public async Task<IActionResult> ChangeTier(Guid id, [FromBody] ChangeCustomerTierRequest req)
    {
        await customerService.ChangeCustomerTierAsync(id, req.Tier);
        return Ok();
    }

    [HttpPut("accounts/{id:guid}/accrue-interest")]
    public async Task<IActionResult> AccrueInterest(Guid id, [FromBody] AccrueInterestRequest req)
    {
        var tx = await accountService.AccrueInterestAsync(id, req.Year, req.Month);
        return Ok(TransactionResponse.From(tx));
    }

    [HttpPut("accounts/{id:guid}/mature")]
    public async Task<IActionResult> Mature(Guid id)
    {
        var tx = await accountService.MatureTimeDepositAsync(id);
        return Ok(TransactionResponse.From(tx));
    }
}
