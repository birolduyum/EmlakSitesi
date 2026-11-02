using Microsoft.Extensions.Configuration;

namespace Emlak.Api.Photos;

/// <summary>Fotoğraf dosyalarının diskteki kökü. Dosya adı her zaman sunucuda üretilir; istemci adı kullanılmaz.</summary>
public class PhotoStorage
{
    public const string DefaultPath = "/data/photos";

    public PhotoStorage(IConfiguration config)
    {
        var configured = config["Photos:Path"];
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? DefaultPath : configured);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Mutlak kök klasör.</summary>
    public string Root { get; }

    /// <summary>Dosyayı "{listingId}/{Guid:N}.{ext}" olarak kaydeder (ext tespit edilen türden) ve görece yolu döner.</summary>
    public async Task<string> SaveAsync(int listingId, Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct)
    {
        await using var source = file.OpenReadStream();
        var ext = await PhotoValidator.DetectExtensionAsync(source, ct)
                  ?? throw new InvalidDataException("Desteklenmeyen dosya türü.");

        var relative = $"{listingId}/{Guid.NewGuid():N}.{ext}";
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        try
        {
            await using var target = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await source.CopyToAsync(target, ct);
        }
        catch
        {
            TryDeleteFile(full);
            throw;
        }

        return relative;
    }

    /// <summary>Dosyayı siler; yoksa hata vermez. Kök dışına çıkan yol reddedilir.</summary>
    public void Delete(string relativePath) => TryDeleteFile(Resolve(relativePath));

    /// <summary>Görece yolu mutlak yola çevirir; sonuç kökün altında kalmıyorsa reddeder.</summary>
    public string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        }

        return full;
    }

    private static void TryDeleteFile(string full)
    {
        try
        {
            File.Delete(full);
        }
        catch (IOException)
        {
            // Dosya silinemedi (örn. kullanımda); kayıt yine de doğru kalır.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
