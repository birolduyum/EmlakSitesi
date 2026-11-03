# Emlak Sitesi

Satılık ve kiralık konut ilanlarının listelendiği bir emlak sitesi. Ziyaretçiler ilanları süzer ve inceler; yönetici ve saha hesapları ilan, fotoğraf, kur ve ev gösterme kayıtlarını yönetir. Arayüz Türkçedir. Ziyaretçi ana sayfası varsayılan olarak **Tümü** (satılık ve kiralık birlikte) listeler; tür, süzgeç panelinden seçilir.

- **web**: Vue 3 (Vite), nginx ile sunulur
- **api**: C# / ASP.NET Core (.NET 10), Entity Framework Core, JWT ile giriş
- **db**: PostgreSQL 16

## Gereksinimler

Yalnızca Docker (Docker Compose dahil). Başka bir şey kurmanız gerekmez.

## Hızlı başlangıç

Windows (Komut İstemi / PowerShell):

```bat
copy .env.example .env
```

Linux / macOS:

```bash
cp .env.example .env
```

`.env` dosyasındaki değerleri kendinize göre değiştirin:

| Anahtar | Anlamı |
| --- | --- |
| `POSTGRES_PASSWORD` | Veritabanı parolası. Zorunludur; boşsa `docker compose` hata verir. |
| `JWT_KEY` | Oturum anahtarı, **en az 32 karakter**, tahmin edilemez olmalı. Zorunludur. |
| `Users__0..2__Username / Password / Role` | Üç hesap. 0 ve 1 `Admin`, 2 `Field` (saha) rolündedir. Parolalar veritabanında yalnızca hash olarak saklanır. Hesap yönetim ekranı yoktur; hesaplar buradan tanımlanır. |
| `Advisor__Name / Phone / WhatsApp` | **Varsayılan danışman**: ilana özel danışman atanmamış ilanlarda görünen ad ve telefonlar (bkz. "Danışmanlar"). `Advisor__WhatsApp` boşsa WhatsApp bağlantısı `Advisor__Phone` rakamlarından (`https://wa.me/<rakamlar>`, 7-15 hane) türetilir; ikisi de yoksa düğmeler `tel:` yedeğine döner. WhatsApp'ta doğrudan sesli arama başlatan bir bağlantı yoktur: "WhatsApp'tan Ara" yalnızca sohbeti açar, arama simgesine ziyaretçi dokunur. |
| `Seed__Sample` | Varsayılan `true`: veritabanında hiç ilan yokken örnek ilanlar ve yer tutucu fotoğraflar eklenir. **Üretimde `false` yapın** (`.env` içine `Seed__Sample=false` yazın). |
| `RateLimit__LoginPerMinute` | İsteğe bağlı. Giriş denemesi sınırı: istemci IP'si başına dakikada en çok bu kadar deneme (varsayılan `10`). Aşılırsa API `429` ve "Çok fazla deneme. Bir dakika sonra tekrar deneyin." döner. |

**Örnek değerler geçerli değildir.** `.env.example` içindeki `degistir-beni` içeren değerler (`JWT_KEY` ve tüm `Users__*__Password` parolaları) örnektir: bunlarla API **başlamaz** ve "hâlâ örnek değer; .env dosyasında değiştirin" hatasıyla durur. Ayrıca `JWT_KEY` 32 karakterden kısa olamaz ve `Role` yalnızca `Admin` ya da `Field` olabilir. İlk başlatma bu yüzden `.env` düzenlenene kadar başarısız olur; `docker compose logs api` hatanın hangi anahtar için olduğunu söyler (değeri yazmaz). Bir hesabı `.env`'den kaldırırsanız API bir sonraki başlatmada o hesabı veritabanından da siler (giriş yapamaz); açık oturum jetonu ise süresi (12 saat) dolana kadar geçerli kalır.

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
| API (yalnızca bu makineden) | http://localhost:8080 |

Site ve yönetim ekranı yalnızca 8081 üzerinden kullanılır; nginx `/api/` ve `/photos/` isteklerini API'ye iletir. 8080 portu yalnızca geliştirme ve sağlık kontrolü içindir ve **yalnızca loopback'e (`127.0.0.1:8080`) yayınlanır**: ağdaki başka makineler API'ye doğrudan erişemez. Web kapsayıcısı API'ye compose ağı üzerinden (`api:8080`) ulaşır; 8080'in dışarıya açılmasına gerek yoktur. Dışarıya yalnızca 8081 açılmalıdır.

## Roller ve yetkiler

| İşlem | Ziyaretçi | Saha (`Field`) | Yönetici (`Admin`) |
| --- | :---: | :---: | :---: |
| İlanları listeleme/görme (ev sahibi bilgisi olmadan) | evet | evet | evet |
| Ev sahibi adı, telefonu ve notunu görme | hayır | hayır | evet |
| İlan ekleme, düzenleme, silme, geri alma | hayır | hayır | evet |
| Fotoğraf yükleme, silme, sıralama | hayır | hayır | evet |
| Kur (USD/EUR) belirleme | hayır | hayır | evet |
| Danışmanları yönetme (ekleme, düzenleme, pasifleştirme) ve ilana atama | hayır | hayır | evet |
| Ev gösterme kaydı alma ve kapatma | hayır | evet | evet |
| Gösterme kayıtlarını ve raporunu görme | hayır | hayır | evet |

Yetki kontrolü API'de yapılır: girişsiz istek 401, yetkisiz rol 403 alır. İlan silme yalnızca yumuşak silmedir; kayıt ve fotoğraf dosyaları silinmez, yönetici silinen ilanı geri alabilir.

## Danışmanlar

Yönetim menüsündeki **Danışmanlar** ekranından (yalnızca Yönetici) danışman eklenir, düzenlenir, pasifleştirilir/etkinleştirilir. Danışman silinmez: ilanlar ona bağlı olduğundan yalnızca pasifleştirilir; pasif danışman mevcut ilanlarında görünmeye devam eder ama yeni bir ilana atanamaz. İlan formundaki **Danışman** bölümünden her ilana bir danışman seçilir; seçilmezse `.env` içindeki `Advisor__*` ayarlarıyla belirlenen **varsayılan danışman** görünür. Sitede liste satırı, ilan sayfası, fotoğraf galerisi ve büyük fotoğraf görünümü o ilanın danışmanını gösterir. Telefon numaraları Türkiye ulusal biçiminde (`0 5xx …`), başındaki 0 olmadan (`5xx …`) ya da uluslararası biçimde (`+90 5xx …`) yazılabilir; WhatsApp bağlantısı her zaman uluslararası biçimde (`https://wa.me/905…`) kurulur, görünen telefon yazıldığı gibi kalır. Yönetim ilan listesi danışmana göre süzülebilir ("İlanlarını gör" bağlantısı da bunu kullanır).

**Gizlilik:** danışmanın adı, telefonu ve WhatsApp bağlantısı ziyaretçiye açıktır (zaten ilanda gösterilmek içindir). Ev sahibi bilgileri ise ziyaretçi ve saha rolüne giden yanıtlarda hiç bulunmaz. Örnek veri (`Seed__Sample`) üç kurgusal danışman ve her dördüncü ilanda danışmansız (varsayılan) bir ilan içerir.

## Veriler: nerede durur, yedek ve sıfırlama

- Veritabanı `dbdata` volume'unda, fotoğraflar `photos` volume'unda saklanır. `docker compose down`, `restart` veya yeniden `up --build` verileri silmez.
- Yedek (veritabanı): `docker compose exec db pg_dump -U emlak emlak > yedek.sql`
- Yedek (fotoğraflar): fotoğraf volume'unu geçerli klasöre arşivleyin (volume adındaki ön ek klasör adınıza göre değişebilir: `docker volume ls`).

  Windows (Komut İstemi):

  ```bat
  docker run --rm -v emlaksitesi_photos:/data -v "%cd%":/yedek alpine tar czf /yedek/photos.tar.gz -C /data .
  ```

  Linux / macOS:

  ```bash
  docker run --rm -v emlaksitesi_photos:/data -v "$(pwd)":/yedek alpine tar czf /yedek/photos.tar.gz -C /data .
  ```

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

- Giriş ucunda (`POST /api/auth/login`) istemci IP'si başına dakikada 10 deneme sınırı vardır (`RateLimit__LoginPerMinute` ile değiştirilir). API nginx'in arkasında çalıştığı için gerçek istemci IP'si `X-Forwarded-For` başlığından okunur; bu yüzden API'ye nginx'i atlayarak doğrudan erişim loopback ile sınırlıdır.
- Web, nginx üzerinden İçerik Güvenlik Politikası (CSP) başlığıyla sunulur: yalnızca kendi kaynağından betik/stil/görsel yüklenir.
- HTTPS bu projenin kapsamı dışındadır; internete açılacaksa TLS'i ters vekil sağlamalıdır.
- Sırlar (parolalar, `JWT_KEY`) yalnızca `.env` dosyasında durur; `.env` git'e eklenmez. Gerçek parolaları depoya koymayın.
- Ev sahibi bilgisi ziyaretçi ve saha rolüne giden yanıtlarda hiç bulunmaz (alan yoktur).

## Bilinen sınırlamalar

Bilerek kabul edilmiş, şimdilik değiştirilmeyen sınırlamalar:

- Fiyat çubuğu (slider) yoktur; fiyat yalnızca "En Az" ve "En Fazla" kutularıyla süzülür.
- Silinen ilanların fotoğrafları diskte dosya olarak kalır (tahmin edilemez adlarla saklanır; silme yalnızca yumuşak silmedir).
- Aynı kullanıcı ve aynı ilan için tek açık gösterme kaydı kontrolü uygulama düzeyindedir; çok nadir bir çift istekte iki açık kayıt oluşabilir.
- 12 saatlik oturumda rol ya da hesap değişikliği, jetonun süresi dolana kadar geçerli olmaz: eski jeton süresi bitene kadar eski rolle çalışmaya devam eder.
- Kat filtresi bodrum (negatif) katları aramaz.
- Kimlik doğrulama jetonu tarayıcının `localStorage` alanında tutulur (CSP ile risk azaltılmıştır, ortadan kalkmaz).
- HTTPS bu projede yoktur; halka açık kullanımda TLS'i sağlayan bir ters vekil gerekir.
