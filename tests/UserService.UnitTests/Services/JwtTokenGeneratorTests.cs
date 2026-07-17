using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.UnitTests.Services;

public class JwtTokenGeneratorTests
{
    private const string Key = "super-secret-key-for-shoplite-at-least-32-chars!!";

    private static JwtTokenGenerator CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key,
                ["Jwt:Issuer"] = "ShopLite",
                ["Jwt:Audience"] = "ShopLite"
            })
            .Build();

        return new JwtTokenGenerator(config);
    }

    private static User SampleUser() => new()
    {
        Email = "ada@example.com",
        Name = "Ada",
        PasswordHash = "irrelevant"
    };

    [Fact]
    public void Generated_token_carries_user_id_as_subject()
    {
        var user = SampleUser();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(user));

        token.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
    }

    [Fact]
    public void Generated_token_carries_email_and_name()
    {
        var user = SampleUser();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(user));

        token.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email && c.Value == "ada@example.com");
        token.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Ada");
    }

    [Fact]
    public void Generated_token_uses_configured_issuer_and_audience()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.Issuer.Should().Be("ShopLite");
        token.Audiences.Should().Contain("ShopLite");
    }

    [Fact]
    public void Generated_token_expires_in_about_24_hours()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Generated_token_is_signed_with_hmac_sha256()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.SignatureAlgorithm.Should().Be("HS256");
    }
}
