using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace Emlak.Api.Auth;

/// <summary>POST /api/auth/login için istemci IP'sine göre sabit pencereli sınır (RateLimit:LoginPerMinute, varsayılan 10).</summary>
public static class LoginRateLimit
{
    public const string PolicyName = "login";
    public const int DefaultPerMinute = 10;

    public static int ReadLimit(IConfiguration config)
        => int.TryParse(config["RateLimit:LoginPerMinute"], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : DefaultPerMinute;

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration config)
    {
        var limit = ReadLimit(config);

        // API nginx'in arkasında çalışır: gerçek istemci IP'si X-Forwarded-For'dan okunur. Docker ağı güvenilir kabul edildiği için
        // bilinen ağ/vekil listeleri boşaltılır (varsayılan yalnızca loopback'e güvenir). API portu yalnızca loopback'e açıktır;
        // dışarıdan doğrudan erişilemediği için başlığı sahtelemek mümkün değildir.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PolicyName, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Çok fazla deneme. Bir dakika sonra tekrar deneyin." }, ct);
            };
        });

        return services;
    }
}
