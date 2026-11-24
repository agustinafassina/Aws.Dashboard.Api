using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Aws.Api.Middleware;
using Aws.Models.Configuration;
using Aws.Repository;
using Aws.Services;

const string AuthScheme = "Auth0App1";

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// When true (default), every endpoint requires a valid JWT unless marked [AllowAnonymous].
var requireAuthentication = configuration.GetValue("Auth:RequireAuthentication", true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the JWT access token (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// AWS SDK (uses default credential chain: env vars, profile, IAM role)
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
builder.Services.AddAWSService<Amazon.EC2.IAmazonEC2>();
builder.Services.AddAWSService<Amazon.RDS.IAmazonRDS>();
builder.Services.AddAWSService<Amazon.IdentityManagement.IAmazonIdentityManagementService>();
builder.Services.AddAWSService<Amazon.CostExplorer.IAmazonCostExplorer>();
builder.Services.AddAWSService<Amazon.Inspector2.IAmazonInspector2>();
builder.Services.AddAWSService<Amazon.Lambda.IAmazonLambda>();
builder.Services.AddAWSService<Amazon.SQS.IAmazonSQS>();
builder.Services.AddAWSService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>();
builder.Services.AddAWSService<Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2>();
builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
builder.Services.AddAWSService<Amazon.WAFV2.IAmazonWAFV2>();
builder.Services.AddAWSService<Amazon.CloudWatch.IAmazonCloudWatch>();
builder.Services.AddAWSService<Amazon.CloudWatchLogs.IAmazonCloudWatchLogs>();
builder.Services.AddAWSService<Amazon.CertificateManager.IAmazonCertificateManager>();
builder.Services.AddAWSService<Amazon.ElasticLoadBalancingV2.IAmazonElasticLoadBalancingV2>();
builder.Services.AddAWSService<Amazon.ElasticLoadBalancing.IAmazonElasticLoadBalancing>();
builder.Services.AddAWSService<Amazon.ECR.IAmazonECR>();
builder.Services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
builder.Services.Configure<Ec2Options>(configuration.GetSection(Ec2Options.SectionName));
builder.Services.Configure<RdsOptions>(configuration.GetSection(RdsOptions.SectionName));
builder.Services.Configure<IamOptions>(configuration.GetSection(IamOptions.SectionName));
builder.Services.Configure<CostOptions>(configuration.GetSection(CostOptions.SectionName));
builder.Services.Configure<InspectorOptions>(configuration.GetSection(InspectorOptions.SectionName));

// Dependency Injection: Repositories (then Services)
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();

// CORS — explicit origins only (* is ignored; incompatible with credentialed browser requests)
var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowedOrigins = configuredOrigins
    .Where(o => !string.IsNullOrWhiteSpace(o) && !string.Equals(o, "*", StringComparison.Ordinal))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

if (builder.Environment.IsDevelopment())
{
    foreach (var devOrigin in new[] { "http://localhost:3000", "http://127.0.0.1:3000" })
    {
        if (!allowedOrigins.Contains(devOrigin, StringComparer.OrdinalIgnoreCase))
            allowedOrigins.Add(devOrigin);
    }
}

if (allowedOrigins.Count == 0)
    allowedOrigins.Add("http://localhost:3000");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthScheme;
})
.AddJwtBearer(AuthScheme, options =>
{
    options.Audience = configuration["Auth0App1:Audience"] ?? Environment.GetEnvironmentVariable("Auth0App1.Audience");
    options.Authority = configuration["Auth0App1:Issuer"] ?? Environment.GetEnvironmentVariable("Auth0App1.Issuer");
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["Auth0App1:Issuer"] ?? Environment.GetEnvironmentVariable("Auth0App1.Issuer")
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var exception = context.Exception;
            if (exception is SecurityTokenExpiredException)
                throw new SecurityTokenExpiredException("Token has expired", exception);
            if (exception is SecurityTokenInvalidSignatureException)
                throw new SecurityTokenInvalidSignatureException("Invalid token signature", exception);
            if (exception is SecurityTokenValidationException)
                throw new SecurityTokenValidationException("Token validation failed", exception);
            throw new UnauthorizedAccessException("Authentication failed", exception);
        },
        OnChallenge = context =>
        {
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
                throw new UnauthorizedAccessException("Authorization header is missing");
            context.HandleResponse();
            throw new UnauthorizedAccessException("Authentication challenge failed");
        }
    };
});

// Authorization — secure by default: all endpoints require an authenticated user.
builder.Services.AddAuthorization(options =>
{
    if (requireAuthentication)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder(AuthScheme)
            .RequireAuthenticatedUser()
            .Build();
    }
});

if (!requireAuthentication && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Auth:RequireAuthentication=false is only allowed in the Development environment.");

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (!requireAuthentication)
    app.Logger.LogWarning("Authentication is DISABLED (Auth:RequireAuthentication=false). Do not use this setting outside local development.");

app.Run();
