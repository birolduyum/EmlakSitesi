# Emlak Sitesi — Devam Notu (güncel: 03.11.2026)

## Durum
- Plandaki 15 görevin hepsi uygulandı ve ayrı ayrı gözden geçirildi. Ardından tarayıcıda gözle kontrol yapıldı (ana sayfa, detay, galeri, görüntüleyici, yönetici ekranları, mobil), iki görsel hata düzeltildi (detay fotoğraf ızgarası, kur sayfası).
- Bütün projeye son gözden geçirme yapıldı. Sonuç: iki zorunlu şart (ev sahibi bilgisi ziyaretçiye/Saha'ya sızmaz; girişsiz yönetici API'si yok) sağlam. Çıkan bulgular tek bir "son düzeltme turu" (görev 16) olarak UYGULANDI ve gözden geçirildi (API 171, web 415 test geçti; yalnızca "İ/ı içeren etiket adlarında yinelenen etiket denetimi" kısmen açık, düşük etki): kurulum sırlarının örnek değer olmasını engelleme, config'den çıkarılan hesabın silinmesi, Türkçe sayı girişi, eksik filtreler (banyo, bina kat sayısı), giriş hız sınırı, CSP, vb. Rapor: `.superpowers/sdd/emlak/task-16-report.md`.
- Testler (görev 16 öncesi): API 132+, web 352, hepsi geçiyordu.

## Git
- Depo yerelde (`main`), `origin` = private `github.com/birolduyum/EmlakSitesi` (kişisel hesap; Karasoy değil).
- Bütün iş görev görev atomic commit olarak yerel `main` dalında (19 commit). **Push yapıldı (03.11.2026, `origin/main`)**.

## Dosyalar
- Tasarım: `docs/superpowers/specs/2026-11-02-emlak-sitesi-design.md`; plan: `docs/superpowers/plans/2026-11-02-emlak-sitesi.md`
- Çalışma defteri: `.superpowers/sdd/emlak/progress.md` (git'e girmez); görev raporları `task-N-report.md`
- Çalıştırma: `README.md` (`.env` hazırlama, `docker compose up --build`, adresler, roller)

## Çalıştırma kararları
- .NET 8 yerine net10.0 (makinede SDK 10). Node yok → npm `bash scripts/npm.sh ...` ile Docker'da.
- Yapılandırma anahtarları: `Jwt:Key`, `Photos:Path`, `ConnectionStrings:Default`, `Users:{i}:Username/Password/Role`, `Advisor:*`, `Seed:Sample`, `RateLimit:LoginPerMinute`.

## Kullanıcıya açık kalanlar
- Fiyat çubuğu (slider) yapılmadı; yalnızca En Az/En Fazla kutuları (isteğe bağlı sonraki iş).
- Commit/push talimatı.
