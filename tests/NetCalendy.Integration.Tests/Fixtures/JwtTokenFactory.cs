using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NetCalendy.Integration.Tests.Fixtures;

public static class JwtTokenFactory
{
    private const string Key = "REPLACE_WITH_A_SECRET_KEY_AT_LEAST_32_CHARS";
    private const string Issuer = "netcalendy";
    private const string Audience = "netcalendy-api";

    public static string GenerateToken(Guid userId, Guid tenantId, string tenantSlug = "test-org")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("tenant_slug", tenantSlug),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
