namespace Emlak.Api.Auth;

/// <summary>
/// Örnek (.env.example) değerlerle uygulamanın başlamasını engeller. Hata iletileri yalnızca yapılandırma anahtarını söyler, değeri asla.
/// </summary>
public static class SecretGuard
{
    public const string PlaceholderMarker = "degistir-beni";
    public const int MinJwtKeyLength = 32;

    public static bool IsPlaceholder(string? value)
        => value is not null && value.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase);

    public static void ValidateJwtKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Jwt:Key ayarlı değil; .env dosyasında JWT_KEY tanımlayın.");
        }

        if (IsPlaceholder(key))
        {
            throw new InvalidOperationException("Jwt:Key hâlâ örnek değer; .env dosyasında değiştirin.");
        }

        if (key.Length < MinJwtKeyLength)
        {
            throw new InvalidOperationException($"Jwt:Key en az {MinJwtKeyLength} karakter olmalı; .env dosyasında değiştirin.");
        }
    }

    public static void ValidateUserPassword(string configKey, string? password)
    {
        if (IsPlaceholder(password))
        {
            throw new InvalidOperationException($"{configKey} hâlâ örnek değer; .env dosyasında değiştirin.");
        }
    }
}
