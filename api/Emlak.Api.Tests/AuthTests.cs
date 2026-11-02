using System.Net;
using System.Net.Http.Json;
using Emlak.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    private sealed record LoginResponse(string Token, string Username, string Role);

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokenAndRole()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin1", password = ApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal("admin1", body.Username);
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin1", password = "yanlis-parola" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401WithSameBody()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin1", password = "yanlis-parola" });
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "yok-boyle-biri", password = "yanlis-parola" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownUser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaffEndpoint_WithoutToken_Returns401()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/_test/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsField_Returns403()
    {
        await _factory.ResetAsync();
        var field = await _factory.LoginAsync("saha1");
        var admin = await _factory.LoginAsync("admin1");

        Assert.Equal(HttpStatusCode.Forbidden, (await field.GetAsync("/api/_test/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await field.GetAsync("/api/_test/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/_test/admin")).StatusCode);
    }

    [Fact]
    public async Task Passwords_AreStoredHashed()
    {
        await _factory.ResetAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.ToListAsync();

        Assert.Equal(3, users.Count);
        Assert.All(users, u =>
        {
            Assert.NotEqual(ApiFactory.TestPassword, u.PasswordHash);
            Assert.DoesNotContain(ApiFactory.TestPassword, u.PasswordHash);
        });
    }
}
