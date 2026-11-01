using ErrorOr;
using Faber.Modules.Auth.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account;

/// <summary>What every account endpoint shares: getting the user's own access token and answering errors.</summary>
public static class AccountAccess
{
    /// <summary>
    /// The access token of the web session, which <c>SessionRefresher</c> keeps fresh. It never leaves the
    /// server. A request without a session (a bearer call, for instance) has none and gets an error.
    /// </summary>
    public static async Task<ErrorOr<string>> GetAccessTokenAsync(this HttpContext context)
    {
        var token = await context.GetTokenAsync(AuthSchemes.Session, "access_token");

        return string.IsNullOrEmpty(token)
            ? Error.Unauthorized("Account.Unauthorized", "There is no signed-in session.")
            : token;
    }

    /// <summary>
    /// A ProblemDetails answer for an error: Keycloak's field errors become a validation problem, so the
    /// form can show each message beside its field.
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => ToValidationProblem(error),
            ErrorType.Unauthorized => TypedResults.Problem(error.Description, statusCode: StatusCodes.Status401Unauthorized),
            ErrorType.Forbidden => TypedResults.Problem(error.Description, statusCode: StatusCodes.Status403Forbidden),
            ErrorType.Conflict => TypedResults.Problem(error.Description, statusCode: StatusCodes.Status409Conflict),
            ErrorType.NotFound => TypedResults.Problem(error.Description, statusCode: StatusCodes.Status404NotFound),
            _ => TypedResults.Problem(error.Description, statusCode: StatusCodes.Status502BadGateway)
        };
    }

    private static ProblemHttpResult ToValidationProblem(Error error)
    {
        if (error.Metadata is not { Count: > 0 } fields)
        {
            return TypedResults.Problem(error.Description, statusCode: StatusCodes.Status400BadRequest);
        }

        var errors = fields.ToDictionary(
            field => field.Key,
            field => new[] { field.Value?.ToString() ?? error.Description },
            StringComparer.Ordinal);

        return TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = error.Description
        });
    }
}
