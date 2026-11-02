namespace Emlak.Api.Photos;

/// <summary>Yüklenen dosyanın türünü yalnızca ilk baytlarından (imza) saptar; ad ve Content-Type güvenilmez.</summary>
public static class PhotoValidator
{
    public const long MaxFileBytes = 10L * 1024 * 1024;
    public const int MaxPhotosPerListing = 40;

    /// <summary>Okunması gereken en az bayt sayısı (WebP için 12).</summary>
    public const int HeaderLength = 12;

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Riff => "RIFF"u8;
    private static ReadOnlySpan<byte> WebP => "WEBP"u8;

    /// <summary>"jpg", "png" veya "webp"; tanınmayan içerik için null.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Jpeg)) { return "jpg"; }
        if (header.StartsWith(Png)) { return "png"; }
        if (header.Length >= 12 && header.StartsWith(Riff) && header.Slice(8, 4).SequenceEqual(WebP)) { return "webp"; }
        return null;
    }

    /// <summary>Dosya akışının ilk baytlarını okuyup türü saptar; akışın konumunu başa döndürür.</summary>
    public static async Task<string?> DetectExtensionAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[HeaderLength];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0) { break; }
            read += n;
        }

        if (stream.CanSeek) { stream.Position = 0; }
        return DetectExtension(buffer.AsSpan(0, read));
    }

    /// <summary>Tek dosya için hata iletisi; geçerliyse null.</summary>
    public static async Task<string?> ValidateAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct)
    {
        var label = DisplayName(file);
        if (file.Length == 0) { return $"{label}: dosya boş."; }
        if (file.Length > MaxFileBytes) { return $"{label}: dosya 10 MB sınırını aşıyor."; }

        await using var stream = file.OpenReadStream();
        return await DetectExtensionAsync(stream, ct) is null
            ? $"{label}: yalnızca jpg, png veya webp resimleri yüklenebilir."
            : null;
    }

    // Yalnızca hata iletisinde gösterilir; yola hiç girmez. Uzunluk ve denetim karakterleri sınırlanır.
    private static string DisplayName(Microsoft.AspNetCore.Http.IFormFile file)
    {
        var name = Path.GetFileName((file.FileName ?? "").Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (name.Length == 0) { return "Dosya"; }
        return name.Length > 60 ? name[..60] : name;
    }
}
