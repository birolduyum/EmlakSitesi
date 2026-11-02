# Emlak Sitesi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Docker üzerinde çalışan (Vue 3 + ASP.NET Core + PostgreSQL) bir emlak ilan sitesi: ziyaretçi liste/detay/galeri, rol bazlı yönetici paneli, yumuşak silme, gösterme kaydı ve kurla çevrilen fiyat filtresi.

**Architecture:** Tek ASP.NET Core Minimal API projesi (EF Core + Npgsql) ve tek Vue 3 SPA (`/admin` altında yönetici sayfaları). nginx kapsayıcısı SPA'yı sunar, `/api` ve `/photos` isteklerini API'ye yönlendirir. Fotoğraflar API kapsayıcısında bir volume klasöründe durur. Filtre mantığı tek bir sorgu servisindedir; ziyaretçi ve yönetici uç noktaları aynı servisi kullanır.

**Tech Stack:** .NET 10 (makinedeki SDK 10.0.402; plandaki ".NET 8" yerine, bkz. Global Constraints), EF Core 10 + Npgsql, xUnit + `Microsoft.AspNetCore.Mvc.Testing` + Testcontainers.PostgreSql, JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`), Vue 3 + Vite + vue-router + Vitest, PostgreSQL 16, nginx, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-11-02-emlak-sitesi-design.md`

## Global Constraints

- Arayüz Türkçe; sayılar binlik ayırıcı nokta ile (`22.000.000 ₺`); para birimleri `₺`, `$`, `€`; kiralık fiyat `15.000 ₺ / ay`.
- "emlakjet" adı/logosu/görseli hiçbir yerde kullanılmaz; yeşil/beyaz sade stil.
- Ev sahibi bilgisi ziyaretçi API cevabında ve Saha rolüne verilen cevapta hiç bulunmaz (alan yok, `null` da değil).
- Yönetici/Saha uç noktaları girişsiz 401 verir; yetkisiz rol 403 verir; yetki API'de zorlanır.
- Silme yalnızca yumuşak silmedir (kalıcı silme yok); fotoğraf dosyaları ve ev sahibi kaydı silinmez.
- İlan oluşturma/silme/geri alma/güncelleme için kim + ne zaman kaydedilir (kullanıcı adı, UTC zaman).
- Üç hesap: 2 × `Admin`, 1 × `Field`; hesaplar `.env`'den okunur, parola yalnızca hash olarak saklanır; hesap yönetim ekranı yok.
- Fotoğraf: jpg/png/webp, dosya başına en çok 10 MB, ilan başına en çok 40; fotoğrafsız ilan geçerlidir.
- Konut dışı ilan türü (arsa), harita, favori, kaydet/paylaş, ziyaretçi mesajı saklama yok.
- Yalnızca yerel `docker compose up`; HTTPS yok. Sırlar `.env`'de, `.env` git'e girmez.
- Commit yapılmaz; kullanıcı "commit at" diyene kadar hiçbir adım `git commit` çalıştırmaz (kullanıcının kalıcı kuralı).
- Çalıştırma kararı (yürütücü, 2026-11-02): makinede .NET SDK **10.0.402** var → hedef `net10.0`, EF Core/Npgsql 10.x, Docker imajları `mcr.microsoft.com/dotnet/sdk:10.0` ve `aspnet:10.0`; "dotnet 8" geçen her yer bu şekilde okunur. Makinede **Node/npm yok** → bütün `npm` komutları `bash scripts/npm.sh <npm argümanları>` ile (Docker'da `node:22` çalıştırır, `web/` klasörünü bağlar); plandaki `npm --prefix web X` = `bash scripts/npm.sh X`. Geliştirme sunucusu yerine tarayıcı doğrulaması `docker compose up --build web api db` ile `http://localhost:8081` üzerinden yapılır. Proje klasörü git deposu değildir ve yapılmaz.

## Review Focus

Spec'in söylemediği ama kullanıcıyı ısıracak girdiler (her biri sahibi task'ta testle sabitlenir):

1. Fotoğrafsız ve tek fotoğraflı ilan: kartta yer tutucu görsel, nokta/çubuk yok (Task 11).
2. Kur kaydı eksik ya da sıfır/negatif girilirse fiyat filtresi bozulmamalı: kur doğrulaması reddeder, tohum kuru her zaman vardır (Task 4, 8).
3. Silinmiş ilanın kimliği ziyaretçi uç noktasından doğrudan istenirse 404; gösterme sayısı da sızmaz (Task 5).
4. Aynı kullanıcı aynı ilan için açık (iade edilmemiş) gösterme kaydıyla ikinci kez "anahtarı aldım" derse 409 (Task 8).
5. Arama kutusuna `%`, `_`, `'` gibi karakterler girilince sorgu bozulmaz, joker gibi davranmaz (Task 4).
6. Geçersiz/aşırı büyük/yanlış türde fotoğraf yüklemesi 400 verir, ilan değişmez (Task 7).

---

## Dosya yapısı

```
EmlakSitesi/
  docker-compose.yml        .env.example      .gitignore
  api/
    Dockerfile
    Emlak.Api/              Program.cs, Domain/, Data/, Auth/, Listings/, Showings/, Photos/, Rates/
    Emlak.Api.Tests/
  web/
    Dockerfile  nginx.conf  package.json  vite.config.js
    src/ main.js, router.js, api.js, format.js, components/, views/, admin/
    src/__tests__/
```

Her `Listings/`, `Showings/` vb. klasör kendi endpoint dosyasını, servisini ve DTO'larını taşır; `Program.cs` yalnızca kayıt/yapılandırma yapar.

---

### Task 1: İskelet ve Docker

**Files:**
- Create: `docker-compose.yml`, `.env.example`, `.gitignore`, `api/Dockerfile`, `api/Emlak.Api/Emlak.Api.csproj`, `api/Emlak.Api/Program.cs`, `api/Emlak.Api.Tests/Emlak.Api.Tests.csproj`, `api/Emlak.Api.Tests/ApiFactory.cs`

**Interfaces:**
- Produces: `GET /health` → 200 `"ok"`; `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` — Testcontainers PostgreSQL başlatır, bağlantı dizesini `ConnectionStrings:Default` olarak verir, `Task ResetAsync()` tabloları temizler; `public partial class Program {}`.
- Compose servisleri: `db` (postgres:16, volume `dbdata`, healthcheck `pg_isready`), `api` (`depends_on: db healthy`, volume `photos` → `/data/photos`), `web` (Task 15'te eklenir).
- `.env.example` anahtarları: `POSTGRES_PASSWORD`, `JWT_KEY` (≥32 karakter), `Users__0__Username/Password/Role` … `Users__2__…` (Role: `Admin`,`Admin`,`Field`), `Advisor__Name`, `Advisor__Phone`, `Advisor__WhatsApp`.

- [ ] **Step 1: `dotnet --version`, `docker --version`, `node --version` ile araçları doğrula.** .NET 8 SDK yoksa dur ve kullanıcıya bildir.
- [ ] **Step 2: Test yaz** `HealthTests.Health_ReturnsOk` — `ApiFactory` ile `GET /health` → 200, gövde `ok`. Çalıştır: `dotnet test api/Emlak.Api.Tests --filter HealthTests` → FAIL (proje yok/endpoint yok).
- [ ] **Step 3: İskeleti oluştur** (csproj'lar, `Program.cs` içinde `app.MapGet("/health", () => "ok")`, `ApiFactory`, Dockerfile çok aşamalı, compose, `.env.example`, `.gitignore` → `.env`, `bin/`, `obj/`, `node_modules/`).
- [ ] **Step 4: Çalıştır** `dotnet test api/Emlak.Api.Tests --filter HealthTests` → PASS. Sonra `docker compose up -d db api` ve `curl http://localhost:8080/health` → `ok` (api portu 8080).

---

### Task 2: Alan modeli ve EF Core göçü

**Files:**
- Create: `api/Emlak.Api/Domain/Enums.cs`, `Domain/Listing.cs`, `Domain/Owner.cs`, `Domain/Photo.cs`, `Domain/FeatureTag.cs`, `Domain/Showing.cs`, `Domain/CurrencyRate.cs`, `Domain/AppUser.cs`, `Data/AppDbContext.cs`, `Data/Migrations/*`
- Test: `api/Emlak.Api.Tests/DataTests.cs`

**Interfaces:**
- Produces enum'lar (Türkçe değerleri builder seçer, tek dosyada): `ListingType {Satilik, Kiralik}`, `Currency {TRY, USD, EUR}`, `HeatingType`, `FurnishingStatus`, `UsageStatus`, `DeedStatus`, `KitchenType`, `TagGroup {Ic, Dis, Konum}`, `UserRole {Admin, Field}`.
- Produces `Listing`: `int Id` (ilan numarası olarak da gösterilir), `string Title`, `string? Badge`, `ListingType Type`, `decimal Price`, `Currency Currency`, `decimal? Deposit`, `decimal? Aidat`, `string City, District, Neighborhood`, `string Rooms` (örn. `"3+2"`), `int Bathrooms, GrossM2, NetM2, Floor, TotalFloors, BuildingAge`, `HeatingType Heating`, `FurnishingStatus Furnishing`, `UsageStatus Usage`, `DeedStatus Deed`, `KitchenType Kitchen`, `bool Balcony, Elevator, Parking, InSite, LoanEligible, Swap`, `string Description`, `DateTime CreatedAt`, `string CreatedBy`, `DateTime? UpdatedAt`, `string? UpdatedBy`, `bool IsDeleted`, `DateTime? DeletedAt`, `string? DeletedBy`, `DateTime? RestoredAt`, `string? RestoredBy`, `Owner Owner` (1:1, zorunlu), `List<Photo> Photos`, `List<FeatureTag> Tags` (çoka çok).
- `Owner { int Id; int ListingId; string Name; string Phone; string? Note }`, `Photo { int Id; int ListingId; string FilePath; int SortOrder }`, `FeatureTag { int Id; string Name; TagGroup Group }`, `Showing { int Id; int ListingId; string Username; DateTime TakenAt; DateTime? ReturnedAt; string? Note }`, `CurrencyRate { Currency Currency (PK, USD/EUR); decimal TryPerUnit; DateTime UpdatedAt; string UpdatedBy }`, `AppUser { int Id; string Username (unique); string PasswordHash; UserRole Role }`.
- `AppDbContext(DbContextOptions<AppDbContext>)` + `DbSet` her tür için; `Listing` üzerinde global sorgu filtresi **yok** (silinenler yönetici filtresi için görünür kalmalı, filtre servisi açıkça uygular).
- Ayrı varlık tohumu: `CurrencyRate` USD ve EUR için yer tutucu değerlerle (USD=35, EUR=38; yönetici ayar ekranından değiştirir).

- [ ] **Step 1: Test yaz** `DataTests.Listing_WithOwnerPhotosAndTags_RoundTrips` (kaydet/oku, `Owner.Name`, `Photos.Count`, `Tags.Count` aynı gelir) ve `DataTests.CurrencyRates_AreSeeded` (USD ve EUR satırı var, `TryPerUnit > 0`). Çalıştır → FAIL.
- [ ] **Step 2: Modeli, `AppDbContext`'i yaz; `dotnet ef migrations add Initial`; `Program.cs` başlangıçta `Database.Migrate()` çağırır.**
- [ ] **Step 3: Çalıştır** `dotnet test api/Emlak.Api.Tests --filter DataTests` → PASS.

---

### Task 3: Giriş, JWT ve roller

**Files:**
- Create: `api/Emlak.Api/Auth/AuthEndpoints.cs`, `Auth/UserSeeder.cs`, `Auth/JwtTokenService.cs`
- Modify: `Program.cs` (kimlik doğrulama, yetkilendirme, `UserSeeder`)
- Test: `api/Emlak.Api.Tests/AuthTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `AppUser`.
- Produces: `POST /api/auth/login` gövde `{ username, password }` → 200 `{ token, username, role }` ya da 401 (hangi alanın yanlış olduğu söylenmez). Politikalar: `"AdminOnly"` (rol `Admin`), `"Staff"` (rol `Admin` veya `Field`). JWT talepleri: `name` = kullanıcı adı, `role`. Ömrü 12 saat.
- `UserSeeder.SeedAsync(AppDbContext, IConfiguration, IPasswordHasher<AppUser>)` — `Users:{i}:Username/Password/Role` yapılandırmasından başlangıçta oluşturur/günceller; parolayı yalnızca hash olarak yazar.
- Test yardımcıları (`ApiFactory`'ye): `Task<HttpClient> LoginAsync(string username)` — test hesapları (`admin1`, `admin2`, `saha1`) fabrikanın yapılandırmasıyla tanımlanır.

- [ ] **Step 1: Testler yaz** `AuthTests`: `Login_WithCorrectPassword_ReturnsTokenAndRole`, `Login_WithWrongPassword_Returns401`, `Login_UnknownUser_Returns401WithSameBody`, `StaffEndpoint_WithoutToken_Returns401`, `AdminEndpoint_AsField_Returns403`, `Passwords_AreStoredHashed` (veritabanındaki `PasswordHash` düz parolaya eşit değil). Test için geçici iki uç nokta: `GET /api/_test/staff` (`Staff`), `GET /api/_test/admin` (`AdminOnly`) yalnızca `Testing` ortamında kaydedilir. Çalıştır → FAIL.
- [ ] **Step 2: Uygula** (`PasswordHasher<AppUser>`, `JwtBearer`, imzalama anahtarı `Jwt:Key`).
- [ ] **Step 3: Çalıştır** `--filter AuthTests` → PASS.

---

### Task 4: Filtre/sorgu servisi

**Files:**
- Create: `api/Emlak.Api/Listings/ListingQuery.cs`, `Listings/ListingSearchService.cs`
- Test: `api/Emlak.Api.Tests/ListingSearchTests.cs`

**Interfaces:**
- Produces `record ListingQuery(ListingType Type = Satilik, string? City, string? District, string? Neighborhood, Currency? Currency, decimal? MinPriceTry, decimal? MaxPriceTry, int? MinM2, int? MaxM2, string[]? Rooms, int? MaxBuildingAge, int? Floor, HeatingType? Heating, FurnishingStatus? Furnishing, bool? Balcony, bool? Elevator, bool? Parking, bool? InSite, bool? LoanEligible, bool? Swap, string? Text, ListingSort Sort = Newest, int Page = 1, int PageSize = 20, ListingStatus Status = Active, string? OwnerText = null)`; `enum ListingSort {Newest, PriceAsc, PriceDesc}`; `enum ListingStatus {Active, Deleted, All}`.
- Produces `ListingSearchService.SearchAsync(ListingQuery q, bool includeOwnerSearch, CancellationToken ct) → Task<(List<Listing> Items, int Total)>` — `Photos` ve `Owner` dahil yüklenir; kapsam kuralları: `Status` yalnızca yönetici çağrılarında Active dışı olabilir (çağıran zorlar); `Type` zorunlu bir ayırıcıdır; fiyat aralığı ve fiyat sıralaması `Price × (Currency==TRY ? 1 : kur)` ile ₺'ye çevrilerek yapılır (kur `CurrencyRate` tablosundan sorguya katılır); konum filtreleri fiyat/para biriminden bağımsızdır; `Text` ILIKE ile başlık/il/ilçe/mahalle/ilan numarasında arar, `%`, `_`, `\` kaçışlanır; `OwnerText` yalnızca `includeOwnerSearch` true iken ev sahibi ad/telefonunda arar.
- `ListingSearchService.GetFilterOptionsAsync(ListingType type, CancellationToken ct) → Task<FilterOptions>` — `FilterOptions(string[] Cities, Dictionary<string,string[]> DistrictsByCity, Dictionary<string,int> RoomCounts)` (yalnızca aktif ilanlardan).

- [ ] **Step 1: Testler yaz** (hepsi `ListingSearchTests`, gerçek Postgres): `PriceFilter_ConvertsUsdAndEurWithConfiguredRate` (kur USD=40; 100.000 $ ilan, `MinPriceTry=3.900.000, MaxPriceTry=4.100.000` ile gelir, ₺'lik 4.000.000'lukla birlikte), `PriceFilter_DoesNotMixSaleAndRent` (aynı aralıkta kiralık ilan satılık sorguda gelmez), `LocationFilter_ReturnsAllCurrencies`, `CurrencyFilter_ReturnsOnlyThatCurrency`, `Sort_PriceAsc_UsesTryConvertedPrice`, `Text_WithPercentAndQuote_DoesNotActAsWildcardOrThrow` (başlığı `"A%B"` olan ve `"AxB"` olan ilan; metin `"A%B"` yalnızca birincisini getirir; `"'"` istisna atmaz), `Status_Active_ExcludesDeleted_DeletedShowsOnlyDeleted_AllShowsBoth`, `OwnerText_OnlyWorksWhenAllowed`, `FilterOptions_CountsActiveOnly`. Çalıştır → FAIL.
- [ ] **Step 2: Uygula** (`IQueryable` üzerinde birleşik filtre; ₺ çevirisi için `CurrencyRate` ile `Join` ya da iki kur değerini önceden okuyup `CASE` ifadesi; sayfalama `Skip/Take`).
- [ ] **Step 3: Çalıştır** `--filter ListingSearchTests` → PASS.

---

### Task 5: Ziyaretçi API'si

**Files:**
- Create: `api/Emlak.Api/Listings/PublicListingEndpoints.cs`, `Listings/PublicDtos.cs`
- Test: `api/Emlak.Api.Tests/PublicApiTests.cs`

**Interfaces:**
- Consumes: `ListingSearchService`, `Showings` sayısı (Task 8'in tablosu; bu task'ta `Showing` sayımı doğrudan `AppDbContext` ile yapılır).
- Produces: `GET /api/listings` (sorgu dizesi `ListingQuery` alanlarının adlarıyla; `Status`/`OwnerText` yok sayılır) → `PagedResult<PublicListingSummary>(Items, Total, Page, PageSize)`; `GET /api/listings/{id}` → `PublicListingDetail` ya da 404 (silinmiş dahil); `GET /api/listings/filters?type=Satilik` → `FilterOptions`; `GET /api/tags` → `FeatureTag[]`; `GET /api/site` → `{ advisorName, advisorPhone, whatsappUrl }` (`Advisor:*` yapılandırması).
- `PublicListingSummary(int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency, string City, string District, string Rooms, int GrossM2, int Floor, DateTime CreatedAt, string[] PhotoUrls)`. `PublicListingDetail` = özet alanlar + künyenin tamamı + `Tags` + `Description` + `ShowingCount`. **Hiçbirinde ev sahibi, silinme/oluşturan alanları yoktur.**

- [ ] **Step 1: Testler yaz** `PublicApiTests`: `Detail_Json_ContainsNoOwnerFields` (ham JSON'da `owner`, `phone` benzeri alan adı yok — sahibin telefonu metni hiçbir yerde geçmez), `List_Json_ContainsNoOwnerFields`, `DeletedListing_ById_Returns404`, `DeletedListing_NotInList`, `Detail_ShowsShowingCount` (2 gösterme kaydı → `ShowingCount == 2`; silinmiş ilan 404 olduğu için sayı sızmaz), `ListWithoutPhotos_HasEmptyPhotoUrls`, `Filters_Endpoint_ReturnsCityAndRoomCounts`. Çalıştır → FAIL.
- [ ] **Step 2: Uygula** (DTO eşlemesi tek yerde; `PhotoUrls` = `/photos/{FilePath}`, `SortOrder`'a göre).
- [ ] **Step 3: Çalıştır** `--filter PublicApiTests` → PASS.

---

### Task 6: Yönetici ilan API'si (CRUD, yumuşak silme, kayıt alanları)

**Files:**
- Create: `api/Emlak.Api/Listings/AdminListingEndpoints.cs`, `Listings/AdminDtos.cs`, `Listings/ListingValidator.cs`
- Test: `api/Emlak.Api.Tests/AdminListingTests.cs`

**Interfaces:**
- Consumes: Task 3 politikaları, Task 4 servis.
- Produces (hepsi `Staff`, yazma uçları `AdminOnly`): `GET /api/admin/listings` (`ListingQuery` + `Status` + `OwnerText`; `OwnerText` Field için yok sayılır), `GET /api/admin/listings/{id}` → `AdminListingDetail` (silinmiş olanlar dahil; `Owner` yalnızca rol `Admin` ise dolu, Field için alan hiç yok); `POST /api/admin/listings` (`AdminOnly`) → 201 + detay; `PUT /api/admin/listings/{id}`; `DELETE /api/admin/listings/{id}` → 204 (yumuşak: `IsDeleted=true`, `DeletedAt`, `DeletedBy`); `POST /api/admin/listings/{id}/restore` → 204; `POST /api/admin/tags` `{name, group}` → 201.
- `AdminListingInput` (oluşturma/güncelleme gövdesi): `Listing` alanlarının düzenlenebilir hepsi + `OwnerName`, `OwnerPhone`, `OwnerNote`, `TagIds`. `ListingValidator.Validate(AdminListingInput) → string[] errors` (başlık dolu, fiyat > 0, `Deposit/Aidat` yalnızca `Kiralik` iken kabul, `NetM2 ≤ GrossM2`, ev sahibi adı/telefonu dolu); hata varsa 400 `{ errors }`.
- Kayıt alanları: oluşturmada `CreatedAt/CreatedBy`, güncellemede `UpdatedAt/UpdatedBy`, silmede/geri almada ilgili çiftler, kullanıcı adı JWT `name` talebinden.

- [ ] **Step 1: Testler yaz** `AdminListingTests`: `Create_AsAdmin_SetsCreatedByAndOwner`, `Create_AsField_Returns403`, `Update_SetsUpdatedBy`, `Delete_IsSoft_ListingStillInDbWithDeletedBy`, `Delete_ThenVisitorList_DoesNotShow`, `Delete_ThenAdminListStatusDeleted_Shows`, `Restore_ClearsFlagAndSetsRestoredBy`, `AdminDetail_AsField_HasNoOwnerField` (ham JSON), `AdminDetail_AsAdmin_HasOwner`, `Create_InvalidInput_Returns400WithErrors` (fiyat 0, satılıkta depozito), `Anonymous_Returns401ForEveryAdminRoute` (tüm `/api/admin/*` rotalarını döngüyle dener). Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `--filter AdminListingTests` → PASS.

---

### Task 7: Fotoğraf yönetimi

**Files:**
- Create: `api/Emlak.Api/Photos/PhotoEndpoints.cs`, `Photos/PhotoStorage.cs`
- Modify: `Program.cs` (`/photos` statik dosya sunumu, `Photos:Path` varsayılan `/data/photos`)
- Test: `api/Emlak.Api.Tests/PhotoTests.cs`

**Interfaces:**
- Produces (`AdminOnly`): `POST /api/admin/listings/{id}/photos` (multipart, `files[]`) → 201 + `Photo[]`; `DELETE /api/admin/listings/{id}/photos/{photoId}` → 204 (kaydı ve dosyayı siler; fotoğraf için kalıcı silme serbesttir, yumuşak silme yalnızca ilan içindir); `PUT /api/admin/listings/{id}/photos/order` gövde `int[] photoIds` → 204 (`SortOrder` yeniden yazılır; ilk fotoğraf kapaktır).
- `PhotoStorage.SaveAsync(int listingId, IFormFile file, CancellationToken) → Task<string>` (görece dosya yolu), `Delete(string relativePath)`; içerik türü ve ilk baytlardan (jpg/png/webp imzası) tür doğrulanır, uzantıya güvenilmez.

- [ ] **Step 1: Testler yaz** `PhotoTests`: `Upload_ValidJpeg_AppearsInPublicDetailInOrder`, `Upload_TextFileRenamedToJpg_Returns400`, `Upload_Over10MB_Returns400`, `Upload_41stPhoto_Returns400AndListingUnchanged`, `Reorder_ChangesPublicOrder`, `Delete_RemovesFileAndRecord`, `Upload_AsField_Returns403`. Dosya kökü testlerde geçici klasördür. Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `--filter PhotoTests` → PASS.

---

### Task 8: Gösterme kaydı, rapor ve kur ayarı

**Files:**
- Create: `api/Emlak.Api/Showings/ShowingEndpoints.cs`, `Rates/RateEndpoints.cs`
- Test: `api/Emlak.Api.Tests/ShowingTests.cs`, `api/Emlak.Api.Tests/RateTests.cs`

**Interfaces:**
- Produces (`Staff`): `POST /api/showings` `{ listingId }` → 201 `ShowingDto(int Id, int ListingId, string ListingTitle, string Username, DateTime TakenAt, DateTime? ReturnedAt, string? Note)`; silinmiş/olmayan ilan → 404; aynı kullanıcının aynı ilanda açık kaydı varsa 409. `POST /api/showings/{id}/close` `{ note? }` → 204 (yalnızca kaydı açan kullanıcı ya da `Admin` kapatabilir, `ReturnedAt` = şimdi). `GET /api/showings/mine` → kullanıcının açık kayıtları.
- Produces (`AdminOnly`): `GET /api/admin/showings?listingId&username&from&to` → `ShowingDto[]` (tarih aralığı `TakenAt`'e uygulanır); `GET /api/admin/rates` → `RateDto[] { currency, tryPerUnit, updatedAt, updatedBy }`; `PUT /api/admin/rates` `{ usd, eur }` → 204 (`> 0` zorunlu, aksi 400; `UpdatedBy` kayda yazılır).

- [ ] **Step 1: Testler yaz** `ShowingTests`: `Take_AsField_CreatesRecordWithUsernameAndTime`, `Take_AsAdmin_Works`, `Take_SameListingTwiceWhileOpen_Returns409`, `Take_DeletedListing_Returns404`, `Close_SetsReturnedAtAndNote`, `Close_ByOtherField_Returns403`, `Report_FiltersByListingUserAndDateRange`, `Report_AsField_Returns403`. `RateTests`: `PutRates_AsAdmin_UpdatesAndRecordsUser`, `PutRates_ZeroOrNegative_Returns400AndKeepsOld`, `PutRates_AsField_Returns403`, `NewRate_ChangesPriceFilterResult` (Task 4 filtresi yeni kurla farklı sonuç verir). Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `--filter "ShowingTests|RateTests"` → PASS.

---

### Task 9: Örnek veri

**Files:**
- Create: `api/Emlak.Api/Data/SampleDataSeeder.cs`, `api/Emlak.Api/Data/sample-photos/` (6–8 kendi ürettiğimiz yer tutucu jpg: düz renk + "Fotoğraf N" yazısı; telifli görsel kullanılmaz)
- Test: `api/Emlak.Api.Tests/SampleDataTests.cs`

**Interfaces:**
- Produces: `SampleDataSeeder.SeedAsync(AppDbContext, PhotoStorage)` — yalnızca `Listings` boşsa ve `Seed:Sample=true` ise çalışır; 8–10 ilan: karışık satılık/kiralık, ₺/$/€, farklı il/ilçe, bir tanesi fotoğrafsız, bir tanesi tek fotoğraflı, bir tanesi silinmiş, birkaçında gösterme kaydı, birkaç etiket (`Ic/Dis/Konum`).

- [ ] **Step 1: Test yaz** `SampleDataTests.Seed_CreatesVariedListings_AndIsIdempotent` (iki kez çalıştır, ilan sayısı aynı; hem `Satilik` hem `Kiralik`, üç para birimi, en az bir fotoğrafsız ve bir silinmiş ilan). Çalıştır → FAIL.
- [ ] **Step 2: Uygula**, compose'ta `Seed__Sample=true` ortam değişkeni.
- [ ] **Step 3: Çalıştır** `--filter SampleDataTests` → PASS. `dotnet test api/Emlak.Api.Tests` (tümü) → PASS.

---

### Task 10: Web iskeleti, biçimlendirme ve API istemcisi

**Files:**
- Create: `web/package.json`, `web/vite.config.js` (dev proxy `/api`,`/photos` → `http://localhost:8080`), `web/src/main.js`, `web/src/router.js`, `web/src/api.js`, `web/src/format.js`, `web/src/styles.css`, `web/src/__tests__/format.test.js`
- Test: `web/src/__tests__/format.test.js`

**Interfaces:**
- Produces `format.js`: `formatPrice(price: number, currency: 'TRY'|'USD'|'EUR', type: 'Satilik'|'Kiralik') → string` (`"22.000.000 ₺"`, `"450.000 $"`, `"15.000 ₺ / ay"`), `formatArea(m2) → "220 m²"`, `formatDate(iso) → "23.08.2026"`, `specLine(listing) → "3+2 · 220 m² · 2. Kat · 23.08.2026"`.
- Produces `api.js`: `api.get(path, params?)`, `api.post/put/delete(path, body?)`, `api.upload(path, files)`; JWT `localStorage`'dan (anahtar `emlak_token`) `Authorization: Bearer` olarak eklenir; 401 cevabında token silinir ve `/admin/giris`'e gider; localStorage erişimi try/catch içindedir.
- `router.js` rotaları: `/` (Liste), `/ilan/:id` (Detay), `/ilan/:id/fotograflar` (Galeri), `/admin/giris`, `/admin` (yönetici liste), `/admin/ilan/yeni`, `/admin/ilan/:id`, `/admin/gosterim`, `/admin/rapor`, `/admin/kur`; `/admin/*` yönlendirici koruması token yoksa girişe yollar.

- [ ] **Step 1: Testler yaz** `format.test.js`: `formatPrice(22000000,'TRY','Satilik') === '22.000.000 ₺'`, `formatPrice(450000,'USD','Satilik') === '450.000 $'`, `formatPrice(15000,'TRY','Kiralik') === '15.000 ₺ / ay'`, `formatPrice(1500000,'EUR','Satilik') === '1.500.000 €'`, `specLine` örneği. Çalıştır: `npm --prefix web test` → FAIL.
- [ ] **Step 2: İskeleti ve `format.js`'i yaz; sayı biçimlendirme `Intl.NumberFormat('tr-TR')`.**
- [ ] **Step 3: Çalıştır** `npm --prefix web test` → PASS; `npm --prefix web run build` → hatasız.

---

### Task 11: Ana sayfa — filtre paneli ve ilan satırları

**Files:**
- Create: `web/src/views/ListingsView.vue`, `web/src/components/FilterPanel.vue`, `web/src/components/ListingRow.vue`, `web/src/components/PhotoStrip.vue`, `web/src/components/AdvisorBox.vue`, `web/src/placeholder.svg`
- Test: `web/src/__tests__/PhotoStrip.test.js`, `web/src/__tests__/FilterPanel.test.js`

**Interfaces:**
- Consumes: `GET /api/listings`, `/api/listings/filters`, `/api/site`.
- Produces `PhotoStrip` — prop `photos: string[]`; her fotoğraf için bir nokta/çubuk (`data-testid="dot"`); fare noktanın üstüne gelince (`mouseenter`) o fotoğraf gösterilir; fare bileşenden çıkınca (`mouseleave`) ilkine döner; `photos.length === 0` → yer tutucu görsel ve nokta yok; `photos.length === 1` → nokta yok. Dış öğe tıklanınca `click` olayını yukarı bırakır.
- Produces `FilterPanel` — `v-model` `query` nesnesi (alan adları `ListingQuery` ile aynı, camelCase); Satılık/Kiralık seçimi en üstte; il (+ ilçe, mahalle), para birimi, fiyat En Az/En Fazla (₺), oda sayısı onay kutuları (adetle), m², bina yaşı, kat, ısıtma, eşya, evet/hayır özellikleri; "Temizle" düğmesi. Değişiklik `update:modelValue` yayar; sayfa 250 ms bekleyip yeniden sorgular.
- `ListingsView`: üstte arama kutusu ("Kelime, semt veya ilan no ile ara"), başlık ("Satılık Daire"/"Kiralık Daire") ve `"{total} ilan bulundu"`, sıralama seçimi (En yeni / Fiyat artan / Fiyat azalan), alt alta `ListingRow`'lar, sayfalama; sorgu URL parametrelerinde tutulur (geri tuşu ve paylaşım çalışır).
- `ListingRow`: solda `PhotoStrip` (etiket üst köşede), ortada başlık, `"{city}, {district}"`, `specLine`, `formatPrice`; sağda `AdvisorBox` (`Ara` → `tel:`). Satır tıklanınca `/ilan/:id`.

- [ ] **Step 1: Testler yaz** `PhotoStrip.test.js`: `renders_one_dot_per_photo` (5 foto → 5 `dot`), `hover_on_third_dot_shows_third_photo`, `mouseleave_resets_to_first`, `zero_photos_shows_placeholder_and_no_dots`, `one_photo_has_no_dots`. `FilterPanel.test.js`: `switching_to_kiralik_emits_type_kiralik`, `typing_price_emits_numeric_min_max`, `clear_resets_to_defaults`. Çalıştır → FAIL.
- [ ] **Step 2: Bileşenleri ve sayfayı uygula.**
- [ ] **Step 3: Çalıştır** `npm --prefix web test` → PASS. Sonra `docker compose up -d db api` + `npm --prefix web run dev`; tarayıcıda `http://localhost:5173/` aç: tohum ilanlar alt alta görünür, nokta üstüne gelince fotoğraf değişir, filtreler sonuç sayısını değiştirir. Gözleneni raporla.

---

### Task 12: Detay, galeri ve büyük fotoğraf görüntüleyici

**Files:**
- Create: `web/src/views/DetailView.vue`, `web/src/views/GalleryView.vue`, `web/src/components/Lightbox.vue`, `web/src/components/SpecGrid.vue`
- Test: `web/src/__tests__/Lightbox.test.js`

**Interfaces:**
- Consumes: `GET /api/listings/{id}`, `/api/site`.
- Produces `Lightbox` — props `photos: string[]`, `startIndex: number`; olaylar `close`; siyah zemin, ortada fotoğraf, ileri/geri okları (ilk fotoğrafta geri, sonda ileri pasif), `←`/`→` klavye, `Esc` kapatır, üstte `Geri` ve `Ara` düğmeleri.
- `DetailView`: üstte bir büyük + dört küçük fotoğraf ve "N fotoğrafın tümünü gör" (→ `/ilan/:id/fotograflar`); fotoğraf tıklayınca `Lightbox`; başlık, adres, `formatPrice`; özet kutuları (oda, banyo, brüt/net, kat, kat sayısı, bina yaşı); künye satırları; kiralıkta depozito ve aidat satırları (ilanın para biriminde); İç/Dış/Konum sekmeleri; açıklama; sağda danışman kutusu (`Ara`, `Mesaj Gönder` → WhatsApp bağlantısı); `"Bu ilan {n} kez gösterildi"` (n = 0 ise satır yok). Ev sahibi/yönetici alanı hiç gösterilmez.
- `GalleryView`: fotoğraflar alt alta (çoğu iki sütun, her 5. fotoğraf tam genişlik), sağda sabit özet (fiyat, başlık, oda/m², adres, `Ara`), `İlana Dön`; fotoğraf tıklayınca `Lightbox`.

- [ ] **Step 1: Testler yaz** `Lightbox.test.js`: `arrow_right_moves_to_next_photo`, `left_disabled_on_first_and_right_disabled_on_last`, `keyboard_arrows_navigate`, `escape_emits_close`. Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `npm --prefix web test` → PASS; tarayıcıda: ilan satırına tıkla → detay, "tümünü gör" → galeri, fotoğrafa tıkla → ok tuşlarıyla gezilen görüntüleyici; kiralık örnek ilanda depozito/aidat görünür. Gözleneni raporla.

---

### Task 13: Yönetici girişi, liste ve ilan formu

**Files:**
- Create: `web/src/admin/LoginView.vue`, `web/src/admin/AdminListView.vue`, `web/src/admin/ListingFormView.vue`, `web/src/admin/PhotoManager.vue`, `web/src/admin/auth.js`, `web/src/admin/AdminLayout.vue`
- Test: `web/src/__tests__/auth.test.js`, `web/src/__tests__/AdminListView.test.js`

**Interfaces:**
- Produces `auth.js`: `login(username, password) → Promise<{username, role}>`, `logout()`, `currentUser() → {username, role} | null`, `isAdmin() → boolean` (token ve kullanıcı localStorage'da, try/catch içinde).
- `LoginView`: kullanıcı adı + parola, hatada "Kullanıcı adı veya parola hatalı".
- `AdminLayout`: üst çubukta kullanıcı adı, rol, menü (İlanlar, Gösterme, Rapor ve Kur yalnızca Admin için), çıkış.
- `AdminListView`: ziyaretçi sayfasıyla aynı satır görünümü ve aynı `FilterPanel` + ek: durum seçimi (Aktif / Silinenler / Hepsi), ev sahibi adı/telefon araması (yalnızca Admin); silinen satırlar soluk ve "Silindi — {deletedBy}, {tarih}" etiketli; Admin için "Yeni ilan", satırda "Düzenle", "Sil" (onay sorar), silinende "Geri al"; Field rolünde bu düğmeler hiç çizilmez.
- `ListingFormView`: tüm künye alanları (seçmeli alanlar enum listelerinden), ev sahibi bölümü, etiket seçimi (+ yeni etiket ekleme), kiralıkta depozito/aidat görünür; doğrulama hataları (`400 { errors }`) alanın altında gösterilir; Field rolü bu rotaya girerse listeye yönlendirilir; ayrıntı sayfasında oluşturan/silen/güncelleyen kim-ne zaman bilgisi görünür; `PhotoManager` yükleme, silme, sürükle/ok ile sıralama.

- [ ] **Step 1: Testler yaz** `auth.test.js`: `login_stores_token_and_role`, `logout_clears_storage`, `works_when_localStorage_throws` (erişim hatasında istisna fırlatmaz). `AdminListView.test.js`: `field_role_sees_no_edit_delete_buttons`, `admin_sees_restore_on_deleted_row`, `status_filter_sends_status_param`. Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `npm --prefix web test` → PASS; tarayıcıda Admin ile giriş: yeni ilan ekle (fotoğraflı), düzenle, sil, "Silinenler" filtresinde gör, geri al; ziyaretçi sayfasında silinenin görünmediğini doğrula; Field hesabıyla giriş: düzenleme düğmesi yok ve ev sahibi bilgisi görünmez. Gözleneni raporla.

---

### Task 14: Gösterme kaydı, rapor ve kur ekranları

**Files:**
- Create: `web/src/admin/ShowingView.vue`, `web/src/admin/ReportView.vue`, `web/src/admin/RatesView.vue`
- Test: `web/src/__tests__/RatesView.test.js`

**Interfaces:**
- Consumes: `/api/showings*`, `/api/admin/showings`, `/api/admin/rates`.
- `ShowingView` (Admin ve Field): ilan arama/seçme, "Anahtarı aldım / gösterime gidiyorum" düğmesi → `POST /api/showings`; altında kullanıcının açık kayıtları, her birinde not kutusu ve "Gösterdim / anahtarı iade ettim" düğmesi → `close`; 409'da "Bu ilan için zaten açık bir kaydınız var" gösterilir.
- `ReportView` (Admin): tablo — ilan, kim, alındığı zaman, iade zamanı, not; ilan / kullanıcı / tarih aralığı süzgeçleri; toplam kayıt sayısı.
- `RatesView` (Admin): `1 $ = [ ] ₺`, `1 € = [ ] ₺`, son güncelleme + kim; kaydetme; `≤ 0` istemci tarafında da engellenir.

- [ ] **Step 1: Testler yaz** `RatesView.test.js`: `rejects_zero_and_negative_before_calling_api`, `shows_last_updated_by`. Çalıştır → FAIL.
- [ ] **Step 2: Uygula.**
- [ ] **Step 3: Çalıştır** `npm --prefix web test` → PASS; tarayıcıda: Field ile gösterme aç/kapat; Admin ile raporda gör; kuru değiştir ve ana sayfada fiyat filtresinin sonucunun değiştiğini gör. Gözleneni raporla.

---

### Task 15: Web kapsayıcısı ve uçtan uca doğrulama

**Files:**
- Create: `web/Dockerfile` (node ile `vite build`, nginx ile sunum), `web/nginx.conf`
- Modify: `docker-compose.yml` (`web` servisi, port 8081 → 80, `depends_on: api`), `README.md` (kurulum: `.env` kopyala, `docker compose up --build`, adresler, test hesapları nasıl tanımlanır)

**Interfaces:**
- `nginx.conf`: SPA için `try_files $uri /index.html`; `/api/` ve `/photos/` → `http://api:8080`; `client_max_body_size 50m`.

- [ ] **Step 1: Uygula** ve `docker compose down -v && docker compose up --build -d`.
- [ ] **Step 2: Doğrula** (çıktıyı raporla): `curl http://localhost:8081/api/listings` JSON döner; tarayıcıda `http://localhost:8081/` ana sayfa tohum ilanlarla açılır; `/admin/giris` ile giriş çalışır; fotoğraf yükleyip ziyaretçi sayfasında görüntüle; `docker compose restart api` sonrası ilanlar ve fotoğraflar duruyor (volume).
- [ ] **Step 3: Tüm testler:** `dotnet test api/Emlak.Api.Tests` ve `npm --prefix web test` → PASS.
- [ ] **Step 4: Spec'in "Başarısızlık sayılacak durumları"nı elle son kez kontrol et:** (a) ziyaretçi sayfası ve `/api/listings/{id}` yanıtında ev sahibi adı/telefonu yok; (b) girişsiz `curl http://localhost:8081/api/admin/listings` → 401; (c) kartta nokta sayısı = fotoğraf sayısı. Sonuçları raporla.

---

## Öz değerlendirme (yazarken yapıldı)

- **Spec kapsamı:** liste/filtre/satır/noktalar (T4,5,11), detay/galeri/görüntüleyici (T12), yönetici CRUD + yumuşak silme + kim/ne zaman (T6,13), ev sahibi (T6, gizlilik T5/6), fotoğraf (T7,13), roller (T3,6,8), gösterme kaydı/rapor/sayı (T5,8,14), kur (T4,8,14), satılık/kiralık + depozito/aidat (T2,4,6,11,12), Docker (T1,15), örnek veri (T9). Boşluk görülmedi.
- **Tutarlılık:** `ListingQuery`, `ListingSearchService`, `ShowingDto`, `format.js` adları task'lar arasında aynı.
- **Orantı:** Task'lar imza + test adı + beklenen sonuç içerir; gövdeler yazılmadı.
