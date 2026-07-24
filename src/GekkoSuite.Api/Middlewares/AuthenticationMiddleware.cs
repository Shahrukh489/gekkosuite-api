using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Middlewares;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Gates every non-anonymous endpoint: the request must carry a valid token (401 if not) AND the user
    /// must still be active in the DB (401 if not — a token stays valid until it expires, so a disabled
    /// account must be caught here). Anonymous endpoints are skipped even when a token is attached
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IUserService userService)
    {
        // check if its a public api with [AllowAnonymous]
        var isAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (!isAnonymous)
        {
            // a protected endpoint requires a valid, authenticated token
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // check if user is active in database exists
            var userId = context.User.FindFirst("userId")?.Value;
            var organizationId = context.User.FindFirst("organizationId")?.Value;
            if (userId is null || organizationId is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var user = await userService.GetUserByIdAsync(Guid.Parse(organizationId!), Guid.Parse(userId!));
            if (user is null || !user.IsActive)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        // continue to next middleware if user is authenticated succesfully
        await _next(context);
    }
}
