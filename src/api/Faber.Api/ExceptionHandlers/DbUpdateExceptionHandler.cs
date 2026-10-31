using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Faber.Api.ExceptionHandlers;

public class DbUpdateExceptionHandler(ILogger<DbUpdateExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException dbUpdateException)
        {
            return false;
        }

        logger.LogError(dbUpdateException, "Database update exception at {Path}", httpContext.Request.Path.Value);

        var (status, detail) = dbUpdateException switch
        {
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "The resource was modified by another request."),
            _ => (HttpStatusCode.InternalServerError, "A database error occurred.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)status,
            Title = "Database Error",
            Detail = detail
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}