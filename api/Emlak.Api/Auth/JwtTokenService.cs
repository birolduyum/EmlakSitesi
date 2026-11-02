using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Emlak.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Emlak.Api.Auth;

public class JwtTokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration config) => _key = AuthExtensions.GetSigningKey(config);

    public string CreateToken(AppUser user)
    {
        var claims = new[]
        {
            new Claim("name", user.Username),
            new Claim("role", user.Role.ToString()),
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
