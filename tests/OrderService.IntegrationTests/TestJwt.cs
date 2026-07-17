using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OrderService.IntegrationTests;

public static class TestJwt
{
    // Совпадает с Jwt:Key в OrderService/appsettings.json.
    private const string Key = "super-secret-key-for-shoplite-at-least-32-chars!!";

    public static string ForUser(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ShopLite",
            audience: "ShopLite",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
