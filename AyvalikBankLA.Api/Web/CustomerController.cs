using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AyvalikBankLA.Api.Web;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "CUSTOMER")]
public class CustomerController(CustomerService customerService) : ControllerBase
{
    /// <summary>
    /// The authenticated customer's id, from the ClaimTypes.NameIdentifier claim BasicAuthHandler
    /// sets. Authorization must never trust an id supplied by the caller in a route or query string.
    /// </summary>
    private Guid CallerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req)
    {
        await customerService.ChangePasswordAsync(CallerId, id, req.NewPassword);
        return Ok();
    }
}
