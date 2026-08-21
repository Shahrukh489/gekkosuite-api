using System.Text;

using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

using GekkoSuite.Api.Auth;
using GekkoSuite.Api.Configurations;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Policies;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// single-line console logs with a short timestamp (default logger)
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

// map json_agg columns onto entity collections (e.g. a role's permissions)
SqlMapper.AddTypeHandler(new JsonTypeHandler<List<PermissionEntity>>());
SqlMapper.AddTypeHandler(new JsonTypeHandler<List<MembershipEntity>>());
SqlMapper.AddTypeHandler(new JsonTypeHandler<List<SubscriptionEntity>>());
SqlMapper.AddTypeHandler(new JsonTypeHandler<List<OrderProductEntity>>());

builder.Services.AddOpenApi();

// CORS is opt-in via config: Cors:AllowedOrigins (a JSON array). Absent/empty — e.g. production — means
// no policy is registered and no cross-origin requests are allowed.
const string CorsPolicyName = "ConfiguredOrigins";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyName, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

// serialize enums as their names (e.g. "ORGANIZATION"), not their underlying integer
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Suppress the framework's automatic RFC-9110 ProblemDetails body on error responses (e.g. 401);
// endpoints return their own responses, so a bare status code goes out with an empty body.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = true;
});


// DB Connection
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "No Postgres connection string. Set POSTGRES_CONNECTION_STRING or ConnectionStrings:Postgres.");

builder.Services.AddNpgsqlDataSource(connectionString);

// Register App Services
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IStoreRepository, StoreRepository>();
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();
builder.Services.AddSingleton<IRoleRepository, RoleRepository>();
builder.Services.AddSingleton<IOfferingRepository, OfferingRepository>();
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IStoreService, StoreService>();
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICustomerService, CustomerService>();
builder.Services.AddSingleton<IRoleService, RoleService>();
builder.Services.AddSingleton<IOfferingService, OfferingService>();
builder.Services.AddSingleton<IOrderService, OrderService>();

// Enriches the authenticated principal with the caller's organizationId (resolved from the token's userId).
builder.Services.AddSingleton<IClaimsTransformation, OrganizationClaimsTransformation>();

// Authorization: the provider turns a HasPermission policy name into a requirement, the handler evaluates it.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, FeatureHandler>();

// JWT signing settings
var jwtOptions = new JwtOptions
{
    Secret =
        Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("No JWT secret. Set JWT_SECRET or Jwt:Secret."),
    Issuer =
        Environment.GetEnvironmentVariable("JWT_ISSUER")
        ?? builder.Configuration["Jwt:Issuer"]
        ?? "gekkosuite-api",
    Audience =
        Environment.GetEnvironmentVariable("JWT_AUDIENCE")
        ?? builder.Configuration["Jwt:Audience"]
        ?? "gekkosuite-clients",
    AccessTokenLifetimeMinutes = 60
};

// Fail fast if the signing secret is too weak. HS256 needs a key at least as long as its output (256 bits = 32 bytes);
if (Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
{
    throw new InvalidOperationException("JWT secret must be at least 32 bytes for HS256.");
}

builder.Services.AddSingleton(jwtOptions);

// Validate the bearer JWT on every request: pinned HS256, verified signature, and matching issuer, audience, and expiry.
// Anything else is rejected with 401.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // check the token's signature (reject tampered/forged tokens)
            ValidateIssuerSigningKey = true,
            // the key the signature is verified against
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            // only accept HS256 — rejects 'none' and RS/HS-confusion tokens
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            // require the token's 'iss' claim to match
            ValidateIssuer = true,
            // the issuer value the token must carry
            ValidIssuer = jwtOptions.Issuer,
            // require the token's 'aud' claim to match
            ValidateAudience = true,
            // the audience value the token must carry
            ValidAudience = jwtOptions.Audience,
            // reject expired tokens (checks the 'exp' claim)
            ValidateLifetime = true,
            // allowed clock drift for exp/nbf — tightened from the 5-minute default
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // after the standard checks pass, require our custom claims to be present and well-formed
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // the token carries only userId (the org is resolved later by the claims transformation);
                // reject if it is missing or not a valid GUID
                var userId = context.Principal?.FindFirst("userId")?.Value;

                if (!Guid.TryParse(userId, out _))
                {
                    context.Fail("Missing or invalid userId claim.");
                }

                return Task.CompletedTask;
            }
        };
    });

// Deny by default: an endpoint with no explicit policy will use this FallBack policy 
// which requires an authenticated user
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Top of the pipeline: catch every unhandled exception 
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(feature?.Error, "Handling Global Unhandled exception.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Task.CompletedTask;
    });
});

if (app.Environment.IsDevelopment())
{
    // exempt from the deny-by-default FallbackPolicy so the spec is reachable without a token
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseRouting();

// CORS runs before authentication so preflight OPTIONS requests are answered before auth can reject them.
// Only mapped when origins were configured (registered above, before the app was built).
if (allowedOrigins.Length > 0)
{
    app.UseCors(CorsPolicyName);
}

app.UseAuthentication();
app.UseMiddleware<GekkoSuite.Api.Middlewares.AuthenticationMiddleware>();
app.UseAuthorization();
app.MapControllers();

// print the bound addresses once the server is actually listening
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Server started successfully on {string.Join(", ", app.Urls)}");
});

app.Run();
