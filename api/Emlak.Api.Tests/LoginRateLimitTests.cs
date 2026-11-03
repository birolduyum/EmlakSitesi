using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Emlak.Api.Tests;

/// <summary>Giriş ucu hız sınırı sınanırken kullanılan fabrika: dakikada 3 deneme.</summary>
public class ThrottledApiFactory : ApiFactory
{
    public const int Limit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimit:LoginPerMinute", Limit.ToString());
    }
}

public class LoginRateLimitTests : IClassFixture<ThrottledApiFactory>
{
    private readonly ThrottledApiFactory _factory;

    public LoginRateLimitTests(ThrottledApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> TryLoginAsync(HttpClient client, string ip, string password = "yanlis")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username = "admin1", password }),
        };
        req.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(req);
    }

    [Fact]
    public async Task FourthAttemptInWindow_Gets429_WithJsonBodyAndRetryAfter()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        for (var i = 0; i < ThrottledApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await TryLoginAsync(client, "203.0.113.10")).StatusCode);
        }

        var blocked = await TryLoginAsync(client, "203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        Assert.Contains("Çok fazla deneme. Bir dakika sonra tekrar deneyin.", await blocked.Content.ReadAsStringAsync());
        // Doğru parola da sınırdayken engellenir.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await TryLoginAsync(client, "203.0.113.10", ApiFactory.TestPassword)).StatusCode);
    }

    [Fact]
    public async Task DifferentForwardedIp_IsCountedSeparately()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        for (var i = 0; i < ThrottledApiFactory.Limit + 1; i++) { await TryLoginAsync(client, "203.0.113.20"); }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await TryLoginAsync(client, "203.0.113.20")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TryLoginAsync(client, "203.0.113.21", ApiFactory.TestPassword)).StatusCode);
    }

    [Fact]
    public async Task OtherEndpoints_AreNotThrottled()
    {
        _ = _factory.Server;
        var client = _factory.CreateClient();
        for (var i = 0; i < ThrottledApiFactory.Limit + 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }
    }
}
