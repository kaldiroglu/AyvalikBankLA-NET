using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankLA.Api.Web;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "CUSTOMER")]
public class CustomerController(CustomerService customerService) : ControllerBase
{
    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req)
    {
        await customerService.ChangePasswordAsync(id, req.NewPassword);
        return Ok();
    }
}
