namespace Emlak.Api.Photos;

/// <summary>Kayıtlı dosya yolundan herkese açık fotoğraf adresi: tek yerde üretilir (ziyaretçi, yönetici ve yükleme cevapları aynı çıktıyı verir).</summary>
public static class PhotoUrl
{
    public static string For(string filePath) => "/photos/" + filePath.Replace('\\', '/').TrimStart('/');
}
