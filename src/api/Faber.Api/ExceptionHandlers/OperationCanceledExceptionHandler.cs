using Microsoft.AspNetCore.Diagnostics;

namespace Faber.Api.ExceptionHandlers;

public class OperationCanceledExceptionHandler(ILogger<OperationCanceledExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not OperationCanceledException)
        {
            return ValueTask.FromResult(false);
        }

        logger.LogDebug("Request was cancelled at {Path}", httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = 499; // Client Closed Request

        return ValueTask.FromResult(true);
    }
}