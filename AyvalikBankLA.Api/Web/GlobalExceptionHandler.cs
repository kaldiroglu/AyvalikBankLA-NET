using AyvalikBankLA.Api.Exception;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankLA.Api.Web;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, System.Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            CustomerNotFoundException => (StatusCodes.Status404NotFound, "Customer Not Found"),
            AccountNotFoundException => (StatusCodes.Status404NotFound, "Account Not Found"),
            InsufficientFundsException => (StatusCodes.Status422UnprocessableEntity, "Insufficient Funds"),
            AccountNotOperableException => (StatusCodes.Status422UnprocessableEntity, "Account Not Operable"),
            LimitExceededException => (StatusCodes.Status422UnprocessableEntity, "Limit Exceeded"),
            InvalidPasswordException => (StatusCodes.Status400BadRequest, "Invalid Password"),
            PasswordReusedException => (StatusCodes.Status409Conflict, "Password Reused"),
            AyvalikBankLA.Api.Exception.UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (0, "")
        };
        if (status == 0) return false;
        var pd = new ProblemDetails { Status = status, Title = title, Detail = ex.Message };
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(pd, cancellationToken: ct);
        return true;
    }
}
