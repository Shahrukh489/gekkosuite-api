using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Policies;

public class FeatureHandler : AuthorizationHandler<FeatureRequirement>
{
    private readonly IOrganizationService _organizationService;

    public FeatureHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    /// <summary>
    /// Authorizes the request against the feature gate: the org's effective features must include the
    /// required feature at the required scope. A failure here is a 402 (paid capability not enabled).
    /// </summary>
    /// <param name="context">The authorization context, carrying the validated user.</param>
    /// <param name="requirement">The feature code and scope the endpoint requires.</param>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        var organizationId = context.User.GetOrganizationId();

        // @TODO: check the org's effective features (union of ACTIVE/TRIALING subscriptions' offerings)
        // include requirement.Feature at requirement.Scope; fail otherwise. For now, always allow.
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
