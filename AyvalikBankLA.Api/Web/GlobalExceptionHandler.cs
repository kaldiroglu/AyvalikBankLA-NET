using Microsoft.EntityFrameworkCore;
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
            // Two operations modified the same account concurrently and the second one lost.
            // The detail is fixed rather than ex.Message, which names the entity and key.
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (0, "")
        };
        if (status == 0) return false;
        var detail = ex is DbUpdateConcurrencyException
            ? "The account was modified by another operation. Please retry."
            : ex.Message;
        var pd = new ProblemDetails { Status = status, Title = title, Detail = detail };
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(pd, cancellationToken: ct);
        return true;
    }
}
