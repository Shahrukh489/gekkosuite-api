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
    /// Gates every non-anonymous endpoint: the request must carry a valid token AND resolve to a live, active
    /// organization (401 otherwise — a token stays valid until it expires, so a disabled/deleted account must
    /// be caught here). The organizationId claim is added by OrganizationClaimsTransformation during
    /// authentication; its absence on an authenticated request means the user has no live org, so we reject.
    /// Anonymous endpoints are skipped even when a token is attached.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ILogger<AuthenticationMiddleware> logger)
    {
        // check if its a public api with [AllowAnonymous]
        var isAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (!isAnonymous)
        {
            var route = $"{context.Request.Method} {context.Request.Path}";

            // a protected endpoint requires a valid, authenticated token
            if (context.User.Identity?.IsAuthenticated != true)
            {
                logger.LogDebug("Auth gate: 401 on {Route} — no authenticated token.", route);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // the claims transformation resolves the caller's org (filtering is_active/is_deleted) and adds
            // it as a claim; no claim = the user didn't resolve to a live, active org, so reject
            if (context.User.FindFirst("organizationId") is null)
            {
                logger.LogDebug("Auth gate: 401 on {Route} — authenticated but no organizationId claim (user has no live, active org).", route);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            logger.LogDebug("Auth gate: passed on {Route} for org {OrganizationId}.", route, context.User.FindFirst("organizationId")!.Value);
        }

        // continue to next middleware if user is authenticated succesfully
        await _next(context);
    }
}
