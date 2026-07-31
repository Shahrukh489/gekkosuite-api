using System.Text;

using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

using GekkoSuite.Api.Configurations;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Policies;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;

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

builder.Services.AddOpenApi();

// Setup cors from appsettings configs
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
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Suppress the framework's automatic RFC-9110 ProblemDetails body on error responses (e.g. 401);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = true;
});


// Connect to database
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("No Db connection string");

builder.Services.AddNpgsqlDataSource(connectionString);

// Register App Services
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IStoreRepository, StoreRepository>();
builder.Services.AddSingleton<IRoleRepository, RoleRepository>();
builder.Services.AddSingleton<IOfferingRepository, OfferingRepository>();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IStoreService, StoreService>();
builder.Services.AddSingleton<IRoleService, RoleService>();
builder.Services.AddSingleton<IOfferingService, OfferingService>();

// Custom Claims Transformation that enriches auth context
// Use this to add organizationId to claims for the given userId from the token
builder.Services.AddSingleton<IClaimsTransformation, OrganizationClaimsTransformation>();

// Custom Authorization Handlers
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, FeatureHandler>();

// JWT signing settings
var jwtOptions = new JwtOptions
{
    Secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("No JWT secret. Set JWT_SECRET or Jwt:Secret."),
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "gekkosuite-api",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "gekkosuite-client",
    AccessTokenLifetimeMinutes = 60
};

// Fail fast if the signing secret is too weak. 
// HS256 needs a key at least as long as its output (256 bits = 32 bytes);
if (Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
{
    throw new InvalidOperationException("JWT secret must be at least 32 bytes for HS256.");
}

builder.Services.AddSingleton(jwtOptions);

// Validate the bearer JWT on every request
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
                // the token carries only userId, validate its a Guid
                var userId = context.Principal?.FindFirst("userId")?.Value;

                if (!Guid.TryParse(userId, out _))
                {
                    context.Fail("Missing or invalid userId claim.");
                }

                return Task.CompletedTask;
            }
        };
    });

// Default authorization fallback policy which requires auth for endpoints with no authorization attributes
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// Global exception handler for any errors not caught in controller layer
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(feature?.Error, "Handling Global Unhandled exception.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Task.CompletedTask;
    });
});


// add open-api spec in development only with no auth
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseRouting();

// Add CORS middleware before authentication so preflight OPTIONS requests are answered before auth can reject them.
if (allowedOrigins.Length > 0)
{
    app.UseCors(CorsPolicyName);
}

// enable JWT authentication validation
app.UseAuthentication();

// add custom middleware to run after token auth validation and custom claims transformations
app.UseMiddleware<GekkoSuite.Api.Middlewares.AuthenticationMiddleware>();
app.UseAuthorization();
app.MapControllers();

// print the bound addresses once the server is actually listening
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Server started successfully on {string.Join(", ", app.Urls)}");
});

app.Run();
