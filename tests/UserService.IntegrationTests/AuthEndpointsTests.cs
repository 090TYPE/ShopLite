using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using UserService.Data;
using Xunit;

namespace UserService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AuthEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private UserApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new UserApiFactory(postgres.ConnectionString);
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private record Registration(string Email, string Password, string Name);

    [Fact]
    public async Task Register_creates_user_and_persists_it()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("ada@example.com", "s3cret!", "Ada"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        db.Users.Should().ContainSingle(u => u.Email == "ada@example.com");
    }

    [Fact]
    public async Task Register_never_stores_the_raw_password()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("grace@example.com", "s3cret!", "Grace"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var user = db.Users.Single(u => u.Email == "grace@example.com");

        user.PasswordHash.Should().NotBe("s3cret!");
        user.PasswordHash.Should().StartWith("$2");  // BCrypt
    }

    [Fact]
    public async Task Register_with_duplicate_email_conflicts()
    {
        var payload = new Registration("dup@example.com", "s3cret!", "Dup");
        await _client.PostAsJsonAsync("/api/auth/register", payload);

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("login@example.com", "s3cret!", "Login"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "login@example.com", Password = "s3cret!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["token"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("wrong@example.com", "s3cret!", "Wrong"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "wrong@example.com", Password = "not-it" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unknown_email_is_unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "ghost@example.com", Password = "s3cret!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
