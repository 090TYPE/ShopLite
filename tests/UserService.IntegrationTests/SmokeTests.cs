using FluentAssertions;
using Xunit;

namespace UserService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class SmokeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Application_starts_and_serves_swagger()
    {
        await using var factory = new UserApiFactory(postgres.ConnectionString);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
