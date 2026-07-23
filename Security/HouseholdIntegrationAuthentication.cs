using System.Security.Claims;
using System.Text.Encodings.Web;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeastVault.Api.Security;

public static class HouseholdIntegrationDefaults
{
    public const string AuthenticationScheme = "HouseholdIntegration";
    public const string ConnectionIdClaim = "household_connection_id";
    public const string ScopeClaim = "household_scope";
    public const string AccessTokenPrefix = "bvhi_at_";
}

public sealed class HouseholdIntegrationAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public HouseholdIntegrationAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db,
        TimeProvider timeProvider)
        : base(options, logger, encoder)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var rawToken = authorization["Bearer ".Length..].Trim();
        if (!rawToken.StartsWith(HouseholdIntegrationDefaults.AccessTokenPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        if (rawToken.Length > 512)
        {
            return AuthenticateResult.Fail("The integration access token is invalid.");
        }

        var tokenHash = HouseholdIntegrationService.HashCredential(rawToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var accessToken = await _db.HouseholdAccessTokens
            .AsNoTracking()
            .Include(token => token.Connection)
            .ThenInclude(connection => connection.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);

        if (accessToken is null ||
            accessToken.RevokedAt is not null ||
            accessToken.ExpiresAt <= now ||
            accessToken.Connection.Status != HouseholdConnectionStatus.Active)
        {
            return AuthenticateResult.Fail("The integration access token is invalid or expired.");
        }

        var connection = accessToken.Connection;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, connection.UserId.ToString()),
            new(ClaimTypes.Name, connection.User.Username),
            new(HouseholdIntegrationDefaults.ConnectionIdClaim, connection.Id.ToString())
        };
        claims.AddRange(connection.GrantedScopes
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(scope => new Claim(HouseholdIntegrationDefaults.ScopeClaim, scope)));

        await _db.HouseholdConnections
            .Where(item => item.Id == connection.Id && item.Status == HouseholdConnectionStatus.Active)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.LastUsedAt, now)
                .SetProperty(item => item.UpdatedAt, now));

        var identity = new ClaimsIdentity(claims, HouseholdIntegrationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, HouseholdIntegrationDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}

public sealed record HouseholdScopeRequirement(string Scope) : IAuthorizationRequirement;

public sealed class HouseholdScopeAuthorizationHandler
    : AuthorizationHandler<HouseholdScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HouseholdScopeRequirement requirement)
    {
        var integrationIdentity = context.User.Identities.FirstOrDefault(identity =>
            string.Equals(
                identity.AuthenticationType,
                HouseholdIntegrationDefaults.AuthenticationScheme,
                StringComparison.Ordinal));

        if (integrationIdentity is null)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

        if (integrationIdentity.FindAll(HouseholdIntegrationDefaults.ScopeClaim)
            .Any(claim => string.Equals(claim.Value, requirement.Scope, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
