namespace Emlak.Api.Listings;

/// <summary>
/// Telefon/WhatsApp değerlerinden wa.me bağlantısı üreten TEK yardımcı (varsayılan danışman ve ilan danışmanları aynı kuralı kullanır).
/// Rakamlar uluslararası biçime çevrilir: "00…" → baştaki 00 atılır; "0" ile başlayan 11 haneli numara (Türkiye ulusal biçimi) ve "5" ile başlayan 10 haneli numara (başındaki 0 yazılmamış cep numarası) → "90…".
/// </summary>
public static class WhatsappLink
{
    public const int MinDigits = 7;
    public const int MaxDigits = 15;

    /// <summary>Boşluk, +, parantez ve tire dışında karakter içermeyen ve 7–15 rakamdan oluşan numara mı?</summary>
    public static bool IsPhoneLike(string? value)
    {
        var stripped = Strip(value);
        return stripped.Length is >= MinDigits and <= MaxDigits && stripped.All(char.IsAsciiDigit);
    }

    public static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <summary>
    /// Açık WhatsApp değeri geçerliyse ondan (http/https bağlantı olduğu gibi, numara uluslararası biçimde), yoksa telefondan türetilir.
    /// Geçersizse null. Açık değer işe yaramazsa (rakamsız, bağlantı değil, 7–15 hane dışı) telefona düşülür.
    /// </summary>
    public static string? Build(string? phone, string? whatsapp)
    {
        var configured = whatsapp?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            if (IsHttpUrl(configured)) { return configured; }
            // Açık değer yalnızca çıplak numara (rakam, boşluk, +, parantez, tire) ise kabul edilir; "ftp://…/123" gibi değerlerden rakam avlanmaz.
            if (IsPhoneLike(configured)) { return FromNumber(configured); }
        }

        return FromNumber(phone);
    }

    private static string? FromNumber(string? number)
    {
        var digits = new string(number?.Where(char.IsAsciiDigit).ToArray() ?? []);
        if (digits.StartsWith("00", StringComparison.Ordinal)) { digits = digits[2..]; }
        else if (digits.Length == 11 && digits[0] == '0') { digits = "90" + digits[1..]; }
        else if (digits.Length == 10 && digits[0] == '5') { digits = "90" + digits; } // 5xx xxx xx xx: başındaki 0 yazılmamış Türkiye cep numarası
        return digits.Length is >= MinDigits and <= MaxDigits ? $"https://wa.me/{digits}" : null;
    }

    private static string Strip(string? v)
        => new((v ?? "").Where(c => c is not (' ' or '+' or '(' or ')' or '-')).ToArray());
}
