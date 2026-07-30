using Microsoft.AspNetCore.Authorization;

namespace GekkoSuite.Api.Middlewares;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Custom middleware that runs after ClaimsTransformation to validation additional checks
    /// Such as missing OrganizationId 
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ILogger<AuthenticationMiddleware> logger)
    {
        // check if its a public api with [AllowAnonymous]
        var isAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        // add additional checks to protected endpoints
        if (!isAnonymous)
        {
            var route = $"{context.Request.Method} {context.Request.Path}";

            // requires a valid, authenticated token
            if (context.User.Identity?.IsAuthenticated != true)
            {
                logger.LogDebug("Auth gate: 401 on {Route} — no authenticated token.", route);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (context.User.FindFirst("userId") is null)
            {
                logger.LogDebug("Auth gate: 401 on {Route} — authenticated but no userId claim", route);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // @TODO: double check this.
            // verify the claims transformation added an organizationId 
            if (context.User.FindFirst("organizationId") is null)
            {
                logger.LogDebug("Auth gate: 401 on {Route} — authenticated but no organizationId claim.", route);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            logger.LogDebug("Auth gate: passed on {Route} for org {OrganizationId}.", route, context.User.FindFirst("organizationId")!.Value);
        }

        // continue to next middleware if user is authenticated succesfully
        await _next(context);
    }
}
