using Emlak.Api.Listings;

namespace Emlak.Api.Advisors;

public static class AdvisorValidator
{
    public const int MaxName = 100;
    public const int MaxPhone = 30;
    public const int MaxWhatsApp = 200;

    /// <summary>Hata iletilerini döner; boş dizi = geçerli.</summary>
    public static string[] Validate(AdvisorInput i)
    {
        var e = new List<string>();

        var name = i.Name?.Trim() ?? "";
        if (name.Length == 0) { e.Add("Ad boş olamaz."); }
        else if (name.Length > MaxName) { e.Add($"Ad en çok {MaxName} karakter olabilir."); }

        var phone = i.Phone?.Trim() ?? "";
        if (phone.Length == 0) { e.Add("Telefon boş olamaz."); }
        else if (phone.Length > MaxPhone) { e.Add($"Telefon en çok {MaxPhone} karakter olabilir."); }
        else if (!WhatsappLink.IsPhoneLike(phone)) { e.Add("Telefon geçersiz: boşluk, +, parantez ve tire dışında yalnızca rakam içermeli ve 7-15 rakamdan oluşmalıdır."); }

        var whatsapp = i.WhatsApp?.Trim() ?? "";
        if (whatsapp.Length > MaxWhatsApp) { e.Add($"WhatsApp en çok {MaxWhatsApp} karakter olabilir."); }
        else if (whatsapp.Length > 0 && !WhatsappLink.IsHttpUrl(whatsapp) && !WhatsappLink.IsPhoneLike(whatsapp))
        {
            e.Add("WhatsApp geçersiz: http(s) bağlantısı ya da 7-15 rakamlı bir numara olmalıdır.");
        }

        return e.ToArray();
    }
}
