using Emlak.Api.Domain;
using Emlak.Api.Photos;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Data;

/// <summary>Veritabanında hiç ilan yokken (silinmişler dahil) inandırıcı, tamamen kurgusal demo ilanları ve yer tutucu fotoğrafları ekler.</summary>
public static class SampleDataSeeder
{
    private const int PhotoWidth = 800;
    private const int PhotoHeight = 600;

    private static readonly (string Name, TagGroup Group)[] TagCatalog =
    [
        ("Hilton Banyo", TagGroup.Ic), ("Sauna", TagGroup.Ic), ("Gömme Dolap", TagGroup.Ic), ("Seramik Zemin", TagGroup.Ic),
        ("Giyinme Odası", TagGroup.Ic), ("Çelik Kapı", TagGroup.Ic), ("Vestiyer", TagGroup.Ic), ("Çamaşır Odası", TagGroup.Ic),
        ("Klima", TagGroup.Ic),
        ("Kapalı Otopark", TagGroup.Dis), ("Güvenlik", TagGroup.Dis), ("Asansör", TagGroup.Dis), ("Jeneratör", TagGroup.Dis),
        ("Çocuk Parkı", TagGroup.Dis),
        ("Metroya Yakın", TagGroup.Konum), ("Denize Yakın", TagGroup.Konum), ("Okula Yakın", TagGroup.Konum), ("Merkezi", TagGroup.Konum),
    ];

    private sealed record Sample(
        string Title, string? Badge, ListingType Type, decimal Price, Currency Currency, decimal? Deposit, decimal? Aidat,
        string City, string District, string Neighborhood, string Rooms, int Bathrooms, int GrossM2, int NetM2,
        int Floor, int TotalFloors, int BuildingAge, HeatingType Heating, FurnishingStatus Furnishing, UsageStatus Usage,
        DeedStatus Deed, KitchenType Kitchen, bool Balcony, bool Elevator, bool Parking, bool InSite, bool LoanEligible, bool Swap,
        string Description, int PhotoCount, string[] Tags, string OwnerName, string OwnerPhone, string? OwnerNote,
        int DaysAgo, bool Deleted = false);

    private static readonly Sample[] Samples =
    [
        new("Şişli Merkezde Yeni Binada 3+1 Daire", "Yeni", ListingType.Satilik, 14_500_000m, Currency.TRY, null, null,
            "İstanbul", "Şişli", "Mecidiyeköy", "3+1", 2, 145, 120, 6, 12, 2, HeatingType.Kombi, FurnishingStatus.Esyasiz, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Amerikan, true, true, true, false, true, false,
            "Metroya yürüme mesafesinde, iki yıllık binada geniş bir daire. Ebeveyn banyosu ve giyinme odası mevcuttur. Kapalı otoparkı vardır.",
            6, ["Hilton Banyo", "Giyinme Odası", "Kapalı Otopark", "Metroya Yakın"], "Ahmet Yılmaz", "0532 000 00 01", "Hafta içi akşam aranmalı.", 2),
        new("Karşıyaka'da Balkonlu 2+1 Kiralık Daire", "Balkonlu", ListingType.Kiralik, 28_000m, Currency.TRY, 56_000m, 750m,
            "İzmir", "Karşıyaka", "Bostanlı", "2+1", 1, 105, 90, 3, 5, 8, HeatingType.Dogalgaz, FurnishingStatus.Esyasiz, UsageStatus.KiraciOturuyor,
            DeedStatus.KatMulkiyeti, KitchenType.Kapali, true, true, false, false, false, false,
            "Bostanlı vapur iskelesine kısa mesafede, geniş balkonlu bir daire. Okul ve çarşıya yakındır.",
            4, ["Çelik Kapı", "Gömme Dolap", "Okula Yakın"], "Ayşe Demir", "0532 000 00 02", null, 5),
        new("Çankaya'da Site İçinde 4+1 Geniş Daire", "Site İçi", ListingType.Satilik, 22_000_000m, Currency.TRY, null, null,
            "Ankara", "Çankaya", "Çayyolu", "4+1", 2, 210, 180, 8, 14, 5, HeatingType.Merkezi, FurnishingStatus.Esyasiz, UsageStatus.MalSahibiOturuyor,
            DeedStatus.KatMulkiyeti, KitchenType.Amerikan, true, true, true, true, true, true,
            "Güvenlikli ve çocuk parklı sitede, şehir manzaralı geniş bir daire. Sauna ve kapalı otopark site imkanlarındadır. Krediye uygundur.",
            8, ["Sauna", "Güvenlik", "Çocuk Parkı", "Asansör", "Kapalı Otopark", "Merkezi"], "Mehmet Kaya", "0532 000 00 03", "Takas düşünülüyor.", 9),
        new("Muratpaşa'da Eşyalı 1+1 Kiralık Stüdyo", null, ListingType.Kiralik, 650m, Currency.USD, 1_300m, 40m,
            "Antalya", "Muratpaşa", "Konyaaltı Caddesi", "1+1", 1, 65, 55, 4, 7, 4, HeatingType.Klima, FurnishingStatus.Esyali, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Amerikan, true, true, false, true, false, false,
            "Denize kısa mesafede, tüm eşyaları yeni bir daire. Yaz ve kış kullanıma uygundur.",
            1, ["Klima", "Denize Yakın", "Asansör"], "Elif Çelik", "0532 000 00 04", null, 13),
        new("Bodrum'da Şöminili Havuzlu 3+2 Villa", "Şöminili", ListingType.Satilik, 420_000m, Currency.EUR, null, null,
            "Muğla", "Bodrum", "Yalıkavak", "3+2", 3, 260, 220, 0, 2, 3, HeatingType.YerdenIsitma, FurnishingStatus.Esyali, UsageStatus.Bos,
            DeedStatus.MustakilTapu, KitchenType.Amerikan, true, false, true, false, false, false,
            "Deniz manzaralı, bahçeli ve özel havuzlu müstakil villa. Salonda şömine, yerden ısıtma ve jeneratör bulunur. Marinaya beş dakikadır.",
            7, ["Jeneratör", "Denize Yakın", "Çamaşır Odası", "Giyinme Odası"], "Murat Aydın", "0532 000 00 05", "Sadece randevuyla gösterilir.", 17),
        new("Beşiktaş'ta Boğaza Yakın 3+1 Kiralık Daire", null, ListingType.Kiralik, 60_000m, Currency.TRY, 120_000m, 2_500m,
            "İstanbul", "Beşiktaş", "Levent", "3+1", 2, 150, 125, 9, 15, 6, HeatingType.Merkezi, FurnishingStatus.Esyali, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Amerikan, true, true, true, true, false, false,
            "Güvenlikli sitede, metroya yürüme mesafesinde eşyalı bir daire. Kapalı otopark ve çocuk parkı site içindedir.",
            5, ["Güvenlik", "Kapalı Otopark", "Metroya Yakın", "Klima"], "Zeynep Arslan", "0532 000 00 06", null, 22),
        new("Nilüfer'de Fırsat 2+1 Daire", null, ListingType.Satilik, 5_450_000m, Currency.TRY, null, null,
            "Bursa", "Nilüfer", "Görükle", "2+1", 1, 95, 80, 2, 4, 12, HeatingType.Kombi, FurnishingStatus.Esyasiz, UsageStatus.KiraciOturuyor,
            DeedStatus.KatMulkiyeti, KitchenType.Kapali, true, false, false, false, true, false,
            "Üniversiteye yakın, kiracılı yatırımlık bir daire. Ulaşım ve market gibi ihtiyaçlar yürüme mesafesindedir.",
            0, ["Seramik Zemin", "Okula Yakın"], "Hasan Öztürk", "0532 000 00 07", "Kiracı sözleşmesi Haziran'da bitiyor.", 30),
        new("Bornova'da Ferah 3+1 Daire", "Yeni", ListingType.Satilik, 185_000m, Currency.USD, null, null,
            "İzmir", "Bornova", "Kazımdirik", "3+1", 2, 140, 115, 5, 8, 1, HeatingType.Kombi, FurnishingStatus.Esyasiz, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Amerikan, true, true, true, false, true, true,
            "Yeni teslim edilmiş binada, ebeveyn banyolu ve vestiyerli bir daire. Takas düşünülebilir.",
            3, ["Vestiyer", "Hilton Banyo", "Asansör"], "Fatma Şahin", "0532 000 00 08", null, 41),
        new("Kadıköy'de Moda'ya Yakın 2+1 Kiralık Daire", null, ListingType.Kiralik, 900m, Currency.EUR, 1_800m, 1_200m,
            "İstanbul", "Kadıköy", "Moda", "2+1", 1, 100, 85, 3, 6, 25, HeatingType.Kombi, FurnishingStatus.Esyasiz, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Kapali, false, false, false, false, false, false,
            "Moda sahiline beş dakika mesafede, eski bir binada tadilatlı daire. Çarşı, vapur ve metrobüs hattı yakındır.",
            4, ["Merkezi", "Denize Yakın", "Gömme Dolap"], "Can Polat", "0532 000 00 09", null, 50),
        new("Tepebaşı'nda 3+1 Daire", null, ListingType.Satilik, 6_900_000m, Currency.TRY, null, null,
            "Eskişehir", "Tepebaşı", "Batıkent", "3+1", 1, 130, 110, 4, 6, 10, HeatingType.Dogalgaz, FurnishingStatus.Esyasiz, UsageStatus.Bos,
            DeedStatus.KatMulkiyeti, KitchenType.Kapali, true, true, false, false, true, false,
            "Kampüse yakın, bakımlı bir daire. İlan yayından kaldırılmıştır.",
            3, ["Çelik Kapı"], "Selin Koç", "0532 000 00 10", null, 58, Deleted: true),
    ];

    // Kurgusal danışmanlar; numaralar ev sahiplerinin numaralarından (0532 000 00 xx) bilerek farklıdır.
    private static readonly (string Name, string Phone, string? WhatsApp)[] AdvisorCatalog =
    [
        ("Ayşe Kaya", "+90 532 000 10 01", null),
        ("Mehmet Demir", "+90 532 000 10 02", "https://wa.me/905320001002"),
        ("Zeynep Yalçın", "0 532 000 10 03", null),
    ];

    /// <summary>İlan tablosu tamamen boşsa örnek veriyi ekler; aksi halde hiçbir şey yapmaz.</summary>
    public static async Task SeedAsync(AppDbContext db, PhotoStorage storage, CancellationToken ct = default)
    {
        if (await db.Listings.AnyAsync(ct)) { return; }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var tags = await EnsureTagsAsync(db, ct);
        var now = DateTimeOffset.UtcNow;
        var photoNo = 0;
        var advisors = AdvisorCatalog
            .Select(a => new Advisor { Name = a.Name, Phone = a.Phone, WhatsApp = a.WhatsApp, IsActive = true, CreatedAt = now, CreatedBy = "admin1" })
            .ToList();
        db.Advisors.AddRange(advisors);
        await db.SaveChangesAsync(ct);
        var saved = new List<Listing>();

        for (var i = 0; i < Samples.Length; i++)
        {
            var s = Samples[i];
            var listing = new Listing
            {
                Title = s.Title, Badge = s.Badge, Type = s.Type, Price = s.Price, Currency = s.Currency, Deposit = s.Deposit, Aidat = s.Aidat,
                City = s.City, District = s.District, Neighborhood = s.Neighborhood, Rooms = s.Rooms, Bathrooms = s.Bathrooms,
                GrossM2 = s.GrossM2, NetM2 = s.NetM2, Floor = s.Floor, TotalFloors = s.TotalFloors, BuildingAge = s.BuildingAge,
                Heating = s.Heating, Furnishing = s.Furnishing, Usage = s.Usage, Deed = s.Deed, Kitchen = s.Kitchen,
                Balcony = s.Balcony, Elevator = s.Elevator, Parking = s.Parking, InSite = s.InSite, LoanEligible = s.LoanEligible, Swap = s.Swap,
                Description = s.Description,
                CreatedAt = now.AddDays(-s.DaysAgo).AddHours(-i),
                CreatedBy = i % 2 == 0 ? "admin1" : "admin2",
                // Sırayla danışmanlar; her dördüncü ilanın danışmanı yok (sitedeki varsayılan danışman görünür).
                Advisor = i % (advisors.Count + 1) < advisors.Count ? advisors[i % (advisors.Count + 1)] : null,
                Owner = new Owner { Name = s.OwnerName, Phone = s.OwnerPhone, Note = s.OwnerNote },
                Tags = s.Tags.Select(n => tags[n]).ToList(),
            };
            if (s.Deleted)
            {
                listing.IsDeleted = true;
                listing.DeletedBy = "admin1";
                listing.DeletedAt = now.AddDays(-3);
            }

            db.Listings.Add(listing);
            await db.SaveChangesAsync(ct); // fotoğraf yolu ilan kimliğini içerdiği için önce ilan yazılır

            for (var p = 0; p < s.PhotoCount; p++)
            {
                var bytes = PlaceholderImage.Create(++photoNo, PhotoWidth, PhotoHeight);
                var path = await storage.SaveBytesAsync(listing.Id, bytes, ct);
                listing.Photos.Add(new Photo { FilePath = path, SortOrder = p });
            }

            saved.Add(listing);
        }

        // Gösterme kayıtları: silinmemiş ilanlarda; ilk Saha kullanıcısı adına (yoksa ilk Yönetici). Kullanıcı yoksa kayıt eklenmez:
        // var olmayan bir kullanıcıya ait açık kayıt bırakılmaz. Users tablosu örnek veriden önce tohumlanır.
        var fieldUser = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Field).OrderBy(u => u.Id).Select(u => u.Username).FirstOrDefaultAsync(ct)
            ?? await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Admin).OrderBy(u => u.Id).Select(u => u.Username).FirstOrDefaultAsync(ct);
        var live = saved.Where(l => !l.IsDeleted).ToList();
        if (fieldUser is not null)
        {
            db.Showings.AddRange(
                new Showing { ListingId = live[0].Id, Username = fieldUser, TakenAt = DateTimeOffset.UtcNow.AddDays(-1), ReturnedAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(2), Note = "Alıcı adayı daireyi beğendi, fiyatı soracak." },
                new Showing { ListingId = live[2].Id, Username = fieldUser, TakenAt = DateTimeOffset.UtcNow.AddDays(-4), ReturnedAt = DateTimeOffset.UtcNow.AddDays(-4).AddHours(1), Note = "Site yönetimi anahtar teslimini onayladı." },
                new Showing { ListingId = live[4].Id, Username = fieldUser, TakenAt = DateTimeOffset.UtcNow.AddHours(-3) });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<Dictionary<string, FeatureTag>> EnsureTagsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.FeatureTags.ToListAsync(ct);
        foreach (var (name, group) in TagCatalog)
        {
            if (!existing.Any(t => t.Name == name && t.Group == group))
            {
                var tag = new FeatureTag { Name = name, Group = group };
                db.FeatureTags.Add(tag);
                existing.Add(tag);
            }
        }

        await db.SaveChangesAsync(ct);
        return existing.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
    }
}
