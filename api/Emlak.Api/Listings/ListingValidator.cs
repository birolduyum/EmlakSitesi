namespace Emlak.Api.Listings;

public static class ListingValidator
{
    private const decimal MaxMoney = 9_999_999_999_999m; // numeric(18,2) taşmasın

    /// <summary>Hata iletilerini döner; boş dizi = geçerli. Uzunluk sınırları veritabanı sütunlarıyla aynıdır.</summary>
    public static string[] Validate(AdminListingInput i)
    {
        var e = new List<string>();

        Required(e, i.Title, "Başlık", 200);
        Required(e, i.City, "İl", 100);
        Required(e, i.District, "İlçe", 100);
        Required(e, i.Rooms, "Oda sayısı", 20);
        Required(e, i.OwnerName, "Ev sahibi adı", 200);
        Required(e, i.OwnerPhone, "Ev sahibi telefonu", 50);
        MaxLen(e, i.Neighborhood, "Mahalle", 100);
        MaxLen(e, i.Badge, "Rozet", 50);

        if (i.Price <= 0 || i.Price > MaxMoney)
        {
            e.Add("Fiyat sıfırdan büyük olmalıdır.");
        }

        if (i.Type == Domain.ListingType.Satilik)
        {
            if (i.Deposit is not null) { e.Add("Depozito yalnızca kiralık ilanda girilebilir."); }
            if (i.Aidat is not null) { e.Add("Aidat yalnızca kiralık ilanda girilebilir."); }
        }
        else
        {
            if (i.Deposit is < 0 or > MaxMoney) { e.Add("Depozito geçersiz."); }
            if (i.Aidat is < 0 or > MaxMoney) { e.Add("Aidat geçersiz."); }
        }

        if (i.Bathrooms < 0) { e.Add("Banyo sayısı negatif olamaz."); }
        if (i.GrossM2 < 0) { e.Add("Brüt m² negatif olamaz."); }
        if (i.NetM2 < 0) { e.Add("Net m² negatif olamaz."); }
        if (i.Floor < -5) { e.Add("Bulunduğu kat -5'ten küçük olamaz (bodrum katlar kabul edilir)."); }
        if (i.TotalFloors < 0) { e.Add("Toplam kat negatif olamaz."); }
        if (i.BuildingAge < 0) { e.Add("Bina yaşı negatif olamaz."); }
        if (i.NetM2 > i.GrossM2) { e.Add("Net m², brüt m²'den büyük olamaz."); }

        if (!Enum.IsDefined(i.Type) || !Enum.IsDefined(i.Currency) || !Enum.IsDefined(i.Heating) || !Enum.IsDefined(i.Furnishing)
            || !Enum.IsDefined(i.Usage) || !Enum.IsDefined(i.Deed) || !Enum.IsDefined(i.Kitchen))
        {
            e.Add("Geçersiz seçenek değeri.");
        }

        return e.ToArray();
    }

    private static void Required(List<string> e, string? v, string label, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) { e.Add($"{label} boş olamaz."); }
        else { MaxLen(e, v, label, max); }
    }

    private static void MaxLen(List<string> e, string? v, string label, int max)
    {
        if (v is not null && v.Trim().Length > max) { e.Add($"{label} en çok {max} karakter olabilir."); }
    }
}
