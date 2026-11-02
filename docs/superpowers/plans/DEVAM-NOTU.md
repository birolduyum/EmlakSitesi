# Emlak Sitesi — Devam Notu (2026-11-02 akşamı)

Yarın buradan devam edilir. Yöntem: `superpowers:subagent-driven-development` (görev başına uygulayıcı ajan + gözden geçirici ajan, gerekirse düzeltme turu).

## Dosyalar
- Tasarım: `docs/superpowers/specs/2026-11-02-emlak-sitesi-design.md` (onaylı)
- Plan: `docs/superpowers/plans/2026-11-02-emlak-sitesi.md` (15 görev; başındaki "Global Constraints" çalıştırma kararlarını da içerir)
- Çalışma defteri (ledger): `.superpowers/sdd/emlak/progress.md` — rulings, ertelenen küçük bulgular (minor), görev durumları. **Önce bunu oku.**
- Görev özetleri: `.superpowers/sdd/emlak/task-N-brief.md`, uygulayıcı raporları `task-N-report.md`, `global.md` (genel kısıtlar)
- Yardımcı betikler: `.superpowers/sdd/emlak/snap.sh before|after N` ve `pack.sh N [temel-snap]` (git yok; önce/sonra anlık kopya + `git diff --no-index` ile gözden geçirme diff'i üretir), `scripts/npm.sh` (Node makinede yok; npm komutları Docker'da `node:22` ile).

## Durum
- Görev 1–7: TAMAM (uygulama + gözden geçirme; düzeltme turları dahil). API testleri 93/93 geçiyordu (görev 7 sonu).
- **Görev 8 (gösterme kaydı, rapor, kur ayarı): UYGULANDI** (123/123 test geçti, 0 uyarı; rapor `.superpowers/sdd/emlak/task-8-report.md`). **Gözden geçirme BEKLİYOR**; diff hazır: `.superpowers/sdd/emlak/task-8-review.diff`. Yarın ilk iş: gözden geçirici ajan gönder (model `sonnet`, görev 6/7'deki istemin aynısı; odak: Staff/AdminOnly yetkileri, kapatma yetkisi (açan kişi ya da Admin), tarih sınırları, kur doğrulaması, boş `name` claim). Uygulayıcının notu: aynı kullanıcı/ilan için "tek açık kayıt" kontrolü uygulama kodunda; eşzamanlı çift istekte iki açık kayıt oluşabilir (kısmi benzersiz indeks göç gerektirir, ertelendi).
- Kalan: Görev 9 (örnek veri), 10–14 (Vue arayüz), 15 (web kapsayıcısı + uçtan uca), sonra bütün işin son gözden geçirmesi.

## Kararlar (kısa)
- .NET 8 yerine **net10.0** (makinede SDK 10.0.402). Node yok → npm Docker'da.
- Git deposu 03.11.2026 açıldı (`main`, origin = private `github.com/birolduyum/EmlakSitesi`, kişisel hesap; Karasoy değil). **Hâlâ commit yok, push yok**: kullanıcı "commit at" / "push et" diyene kadar beklenir (kullanıcı kuralı). İlk commit mesajı için kullanıcı talimatı beklenir.
- Yapılandırma anahtarları: `Jwt:Key`, `Photos:Path`, `ConnectionStrings:Default`, `Users:{i}:Username/Password/Role`, `Advisor:Name/Phone/WhatsApp`.
- API sözleşmesi: enum'lar ad olarak (`"Satilik"`, `"Kiralik"`, `"TRY"/"USD"/"EUR"`). `ListingQuery.Type` boş olursa ziyaretçide Satılık, yönetici/saha listesinde ikisi birden. Yönetici listesi `status=Active|Deleted|All` kabul eder.
- Her uç noktada açık `RequireAuthorization`/`AllowAnonymous` zorunlu (fallback policy yok).

## Plan dışı ertelenenler (ledger'da ayrıntı)
Giriş hız sınırı (halka açmadan önce), `ValidAlgorithms` sabitleme, bazı test boşlukları, `SearchAsync` her zaman Owner yükler, vb. Son gözden geçirme bunları değerlendirecek.

## Kullanıcıya yarın sorulacaklar
- (Çözüldü: git deposu açıldı, repo GitHub'da private ve boş.)
- Uçtan uca doğrulamada kullanılacak 3 hesabın kullanıcı adı/parolası `.env`'e yazılacak (kullanıcı belirler; sohbette tekrar edilmez).
