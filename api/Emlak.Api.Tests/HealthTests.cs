using System.Net;
using Xunit;

namespace Emlak.Api.Tests;

public class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }
}
