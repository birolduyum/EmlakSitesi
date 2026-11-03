# Emlak Sitesi

Satılık ve kiralık konut ilanlarının listelendiği bir emlak sitesi. Ziyaretçiler ilanları süzer ve inceler; yönetici ve saha hesapları ilan, fotoğraf, kur ve ev gösterme kayıtlarını yönetir. Arayüz Türkçedir.

- **web**: Vue 3 (Vite), nginx ile sunulur
- **api**: C# / ASP.NET Core (.NET 10), Entity Framework Core, JWT ile giriş
- **db**: PostgreSQL 16

## Gereksinimler

Yalnızca Docker (Docker Compose dahil). Başka bir şey kurmanız gerekmez.

## Hızlı başlangıç

```bash
copy .env.example .env        # Linux/macOS: cp .env.example .env
```

`.env` dosyasındaki değerleri kendinize göre değiştirin:

| Anahtar | Anlamı |
| --- | --- |
| `POSTGRES_PASSWORD` | Veritabanı parolası. Zorunludur; boşsa `docker compose` hata verir. |
| `JWT_KEY` | Oturum anahtarı, **en az 32 karakter**, tahmin edilemez olmalı. Zorunludur. |
| `Users__0..2__Username / Password / Role` | Üç hesap. 0 ve 1 `Admin`, 2 `Field` (saha) rolündedir. Parolalar veritabanında yalnızca hash olarak saklanır. Hesap yönetim ekranı yoktur; hesaplar buradan tanımlanır. |
| `Advisor__Name / Phone / WhatsApp` | İlan sayfalarındaki danışman kutusunda görünen ad ve telefonlar. |
| `Seed__Sample` | Varsayılan `true`: veritabanında hiç ilan yokken örnek ilanlar ve yer tutucu fotoğraflar eklenir. **Üretimde `false` yapın** (`.env` içine `Seed__Sample=false` yazın). |

Sonra başlatın:

```bash
docker compose up --build
```

Arka planda çalıştırmak için `docker compose up --build -d`.

## Adresler

| Ne | Adres |
| --- | --- |
| Site | http://localhost:8081 |
| Yönetim girişi | http://localhost:8081/admin/giris |
| API | http://localhost:8080 |

Site ve yönetim ekranı yalnızca 8081 üzerinden kullanılır; nginx `/api/` ve `/photos/` isteklerini API'ye iletir. 8080 portu geliştirme ve sağlık kontrolü içindir; **gerçek bir sunucuda yalnızca 8081 dışarıya açılmalıdır** (8080'i `docker-compose.yml` içinde kaldırın).

## Roller ve yetkiler

| İşlem | Ziyaretçi | Saha (`Field`) | Yönetici (`Admin`) |
| --- | :---: | :---: | :---: |
| İlanları listeleme/görme (ev sahibi bilgisi olmadan) | evet | evet | evet |
| Ev sahibi adı, telefonu ve notunu görme | hayır | hayır | evet |
| İlan ekleme, düzenleme, silme, geri alma | hayır | hayır | evet |
| Fotoğraf yükleme, silme, sıralama | hayır | hayır | evet |
| Kur (USD/EUR) belirleme | hayır | hayır | evet |
| Ev gösterme kaydı alma ve kapatma | hayır | evet | evet |
| Gösterme kayıtlarını ve raporunu görme | hayır | hayır | evet |

Yetki kontrolü API'de yapılır: girişsiz istek 401, yetkisiz rol 403 alır. İlan silme yalnızca yumuşak silmedir; kayıt ve fotoğraf dosyaları silinmez, yönetici silinen ilanı geri alabilir.

## Veriler: nerede durur, yedek ve sıfırlama

- Veritabanı `dbdata` volume'unda, fotoğraflar `photos` volume'unda saklanır. `docker compose down`, `restart` veya yeniden `up --build` verileri silmez.
- Yedek (veritabanı): `docker compose exec db pg_dump -U emlak emlak > yedek.sql`
- Yedek (fotoğraflar): `docker run --rm -v emlaksitesi_photos:/data -v "%cd%":/yedek alpine tar czf /yedek/photos.tar.gz -C /data .` (volume adındaki ön ek klasör adınıza göre değişebilir: `docker volume ls`)
- **Uyarı: `docker compose down -v` veritabanını ve tüm fotoğrafları KALICI olarak siler.** Yalnızca baştan başlamak istediğinizde kullanın.
- Not: PostgreSQL parolayı yalnızca ilk kurulumda uygular. `POSTGRES_PASSWORD` değerini sonradan değiştirirseniz API bağlanamaz; ya veritabanı parolasını da değiştirin ya da (veri kaybını göze alarak) `down -v` yapın.

## Testler

```bash
dotnet test api/Emlak.Api.Tests   # Docker gerekir (Testcontainers ile geçici PostgreSQL açar)
cd web && npm test                # Node kuruluysa
```

Node kurulu değilse web testleri Docker ile çalışır:

```bash
bash scripts/npm.sh test
```

## Proje yapısı

```
api/Emlak.Api/          API: Auth, Listings, Photos, Rates, Showings, Data (EF Core, geçişler, örnek veri)
api/Emlak.Api.Tests/    API testleri
web/                    Vue uygulaması (src/), Dockerfile ve nginx.conf
scripts/npm.sh          Node olmadan npm komutlarını Docker'da çalıştırır
docker-compose.yml      db + api + web
.env.example            Yapılandırma şablonu (.env git'e girmez)
```

## Güvenlik notları

- Giriş ekranında henüz deneme sınırı (rate limiting) yoktur. Herkese açık bir ağa **doğrudan açmayın**; önüne istek sınırlayan bir ters vekil (reverse proxy) koyun.
- HTTPS bu projenin kapsamı dışındadır; internete açılacaksa TLS'i ters vekil sağlamalıdır.
- Sırlar (parolalar, `JWT_KEY`) yalnızca `.env` dosyasında durur; `.env` git'e eklenmez. Gerçek parolaları depoya koymayın.
- Ev sahibi bilgisi ziyaretçi ve saha rolüne giden yanıtlarda hiç bulunmaz (alan yoktur).
