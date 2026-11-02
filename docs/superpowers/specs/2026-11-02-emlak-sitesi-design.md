# Emlak Sitesi — Tasarım

Tarih: 2026-11-02. Bu proje başka hiçbir projeyle (LawAstCrm dahil) ilişkili değildir; sıfırdan yeni bir projedir.

## Amaç

Bir emlakçı için, evlerin kartlar halinde gösterildiği, fotoğrafları büyük görülebilen, her evin ayrıntılı sayfası olan bir ilan sitesi. Yönetici, evlerin künyesini ve ev sahiplerinin bilgilerini girip düzenleyip silebilir. Görsel ve davranış örneği olarak emlakjet.com ilan sayfaları alınmıştır (sahibi referans ekran görüntüleri verdi).

## Hedefler

1. Ziyaretçi sitesi: ilan kartları, filtreleme, ilan detay sayfası, fotoğraf galerisi, büyük fotoğraf görüntüleyici.
2. Yönetici sitesi: kullanıcı adı ve parolayla giriş; ilan ekleme, düzenleme, silme; ev sahibi bilgisi; fotoğraf yönetimi; filtreleme.
3. Her şey Docker üzerinde: REST API kapsayıcısı, Postgres kapsayıcısı, Vue ön yüz kapsayıcısı; `docker compose up` ile ayağa kalkar.

## Hedef dışı

- Arsa ilanları (kullanıcı: "şimdilik olmasın"; ileride eklenebilir, veri modeli buna kapıyı kapatmamalı ama şimdi yapılmaz).
- Sokak görünümü, drone görünümü, harita, "Kaydet", "Paylaş", reklam bantları, çok kullanıcılı/rol sistemi, ödeme, ziyaretçi kaydı. (Bunlar referans görsellerde var ama istenmedi.)
- Ziyaretçiden gelen mesajların sistemde saklanması: bu tasarımda yok (aşağıda "Danışman kutusu").

## Başarısızlık sayılacak durumlar

- Ev sahibinin adı/telefonu ziyaretçi sitesinde veya ziyaretçi API'sinde görünürse.
- Yönetici API'sine girişsiz erişilebilirse.
- Kart üzerinde fotoğraf noktaları fotoğraf sayısıyla uyuşmazsa.

## Ziyaretçi sitesi

### Ana sayfa (kullanıcı kararı: emlakjet liste sayfası gibi)

Kullanıcı bir emlakjet liste sayfası görseli verdi ve "ana sayfayı böyle yapalım" dedi. Ana sayfa bu liste sayfasıdır: solda filtre paneli (aşağıda "Filtre paneli"), sağda sonuçlar. Sonuçlar varsayılan olarak satır halinde listelenir: solda fotoğraf, ortada başlık, "il, ilçe", "3+2 · 220 m² · 2. Kat · tarih" satırı ve fiyat, sağda danışman kutusu ("Ara" düğmesiyle). Üstte arama kutusu ("kelime, semt veya ilan no ile ara"), ekmek kırıntısı, "N ilan bulundu", ve sıralama seçimi bulunur. İlanlar alt alta satırlar olarak listelenir (kullanıcı kararı: "alt alta olsun"); kart ızgarası ve görünüm düğmeleri yoktur. Alınmayanlar: Harita, EmlakZeka, Kaydet/Paylaş, favori kalbi, yapay zekâlı süzgeç düğmeleri, "İlan Ver", "Değerini Öğren", "Hesap Oluştur".

İlan fotoğrafı (satırda da kartta da): fotoğraf sayısı kadar küçük çubuk/nokta; fare hangisinin üzerine gelirse o fotoğraf gösterilir. Tıklayınca detay sayfası açılır. Her ilan satırının ayrıntıları (ilk anlatımdaki kart tarifi bu satıra uygulanır):

- İlanlar alt alta satırlar olarak listelenir.
- Satır: solda fotoğraf, fotoğrafın üst köşesinde küçük bir etiket (örn. "Yeni", "Şöminili"; yöneticinin ilana yazdığı kısa metin, isteğe bağlı). Altında başlık, "ilçe, il", "3+2 · 220 m² · 2. Kat · ilan tarihi" satırı ve fiyat.
- Fotoğrafın altında/içinde fotoğraf sayısı kadar küçük çubuk veya nokta bulunur. Fare hangisinin üzerine gelirse kart o sıradaki fotoğrafı gösterir; fare karttan çıkınca ilk fotoğrafa döner (builder'ın kararı).
- Karta tıklanınca ilanın detay sayfası açılır.
- Üstte süzgeç düğmeleri (aşağıda "Filtreleme"). Konut/Arsa sekmesi yoktur: şimdilik yalnızca konut (ev) ilanları vardır.

### Filtre paneli

Ana sayfanın solundaki paneldir. Benim anladığım: solda filtre paneli (İl seçimi, fiyat çubuğu ve "En Az / En Fazla" kutuları, oda sayısı onay kutuları ve yanında adetleri, metrekare, binanın yaşı, bulunduğu kat, binanın kat sayısı, ısıtma tipi, banyo sayısı); üstte "N ilan bulundu"; sağda sonuç listesi. Görseldeki yapay zekâ ("Kira Geliri", "Geri Dönüş Süresi"), "Kaydet/Paylaş" ve favori kalbi alınmaz.
### Detay sayfası

- Üstte bir büyük fotoğraf ve yanında dört küçük fotoğraf; "N fotoğrafın tümünü gör" düğmesi.
- Başlık, adres (mahalle, ilçe, il) ve fiyat.
- Özet kutuları: oda sayısı, banyo sayısı, brüt/net m², bulunduğu kat, kat sayısı, bina yaşı.
- Satır satır künye: ilan numarası, güncelleme tarihi, kategori, ısıtma tipi, otopark, kullanım durumu, krediye uygunluk, site içinde, tapu durumu, takas, asansör, mutfak, eşya durumu, balkon.
- "İç / Dış / Konum Özellikleri" sekmeleri; her sekmede işaretli özellik etiketleri.
- İlan açıklaması.
- Danışman kutusu (sağda): emlakçı/danışman adı ve telefon; "Ara" (tel: bağlantısı) ve "Mesaj Gönder" düğmeleri. Karar (builder'a bırakıldı, bkz. aşağıda): "Mesaj Gönder" şimdilik e-posta/WhatsApp bağlantısı açar, sistemde mesaj saklanmaz. Danışman bilgisi tek bir site ayarıdır (ev sahibi bilgisi değildir).

### Fotoğraf galerisi

- "Tümünü gör" ile tam sayfa galeri açılır: fotoğraflar alt alta, çoğu iki sütun, bazıları tam genişlik. Sağda fiyat, başlık, oda/m², adres ve "Ara" düğmesi sabit durur. Üstte "İlana Dön".

### Büyük fotoğraf görüntüleyici

- Galeride veya detay sayfasında bir fotoğrafa tıklayınca açılır: siyah zeminde fotoğraf ortada, iki yanda ileri/geri okları, üstte "Geri" ve "Ara" düğmeleri. Fotoğraflar arasında gezilir (klavye okları da çalışır — builder'ın kararı).

## Filtreleme

Ziyaretçi sitesinde ve yönetici sitesinde bulunur; ev künyesindeki her alana göre süzer:
- Satılık / Kiralık (kullanıcı kararı; hem ziyaretçi hem yönetici listesinde)
- il/ilçe/mahalle: para biriminden ve fiyattan bağımsız çalışır; yalnızca konum seçilince ilan hangi para biriminde olursa olsun hepsi gelir (kullanıcı kararı)
- para birimi (₺, $, €) ayrı bir filtredir; seçilince yalnızca o birimdeki ilanlar gelir (kullanıcı kararı)
- fiyat aralığı (En Az / En Fazla): ₺ cinsinden girilir (tahminim) ve ilanın fiyatı ayarlanan kurla ₺'ye çevrilerek karşılaştırılır; para birimi filtresi seçilmese de her birimdeki ilan aralığa göre gelir (kullanıcı kararı); m² aralığı
- oda sayısı, bina yaşı
- kat, ısıtma, eşya durumu
- evet/hayır özellikleri (balkon, asansör, otopark, site içi, krediye uygun, takas vb.)
- serbest metinle arama
- sıralama: en yeni, fiyata göre artan/azalan

Yönetici sitesinde ek olarak ev sahibinin adına ve telefonuna göre arama.

## Yönetici sitesi

- Giriş: kullanıcı adı ve parola. Üç yönetici hesabı olur (kullanıcı istedi; önce tek hesap düşünülmüştü). Hesaplar iki roldedir (bkz. "Yetkiler ve gösterme kaydı"); kurulumda `.env`/ilk çalıştırma ile tanımlanır ve hesap ekleme/silme ekranı yoktur (benim kararım; kullanıcı istemezse eklenmez). Bu yüzden "kim ekledi/sildi" kaydı gerçekten üç kişiyi ayırt eder. Parola veritabanında yalnızca şifrelenmiş (hash) olarak saklanır. *(Kullanıcı: kullanıcı adı ve parola gerekli dedi; "tek hesap" benim kararım.)*
- Ana ekran ziyaretçi ana sayfası gibi kart listesidir; kart tıklanınca detay sayfası gibi açılır ve alanlar düzenlenebilir.
- İlan ekleme, düzenleme, silme (silmeden önce onay sorulur; silme yumuşak silmedir, bkz. "Ek kararlar").
- Ev künyesinin tüm alanları girilir (yukarıdaki detay sayfası alanlarının hepsi).
- Fotoğraf yönetimi: yükleme, silme, sıra değiştirme; ilk fotoğraf kapak olur.
- Ev sahibi bilgisi (ad, telefon, isteğe bağlı not) her ilanla birlikte girilir ve düzenlenir. Yalnızca yönetici sitesinde ve yönetici API'sinde görünür.
- Yönetici sitesinde de filtreleme vardır.

## Teknik tercihler (sahibi "C# ya da Node, sen seç" dedi; aşağıdakiler benim kararım)

- Ön yüz: Vue 3 (Vite).
- API: C# / ASP.NET Core (.NET), Entity Framework Core, JWT ile yönetici girişi.
- Veritabanı: PostgreSQL, kendi Docker kapsayıcısında, veri kalıcı volume'da.
- Fotoğraflar: sunucuda volume'daki bir klasörde, veritabanında yalnızca dosya yolu ve sıra.
- Başlangıçta birkaç örnek ev (sahte veri ve yer tutucu fotoğraf) yüklenir.
- Kurulum: `docker compose up` ile üç kapsayıcı (web, api, db).
- Yönetici API ile ziyaretçi API'si ayrı uç noktalardır; ev sahibi bilgisi ziyaretçi uç noktalarının cevabında hiç bulunmaz.

## Yetkiler ve gösterme kaydı (kullanıcı istedi)

**Roller**
- Yönetici rolü (iki hesap): ilan ekleme, düzenleme, silme, geri alma, listeleme, görme; gösterme kayıtlarını ve raporunu görme.
- Saha rolü (üçüncü hesap): yalnızca ilanları listeleme ve görme, ve gösterme kaydı oluşturma. İlan ekleyemez, düzenleyemez, silemez. Ev sahibi bilgisini görüp göremeyeceği söylenmedi; tahminim: göremez (gizli). Yetki API tarafında da zorlanır, yalnızca düğmeleri gizlemek yetmez.

**Gösterme kaydı**
- Saha kullanıcısı bir evin anahtarını aldığında ilanı seçip "Anahtarı aldım / gösterime gidiyorum" der; sistem kim, hangi ilan, hangi tarih ve saat olduğunu kaydeder. Tahminim: dönüşte "Gösterdim" ile kaydı kapatır ve anahtar iade zamanı da tutulur; isteğe bağlı kısa not yazabilir. Kaç kişiye gösterildiği girilmez: her kayıt bir gösterme sayılır (kullanıcı kararı).
- Yönetici rolü de gösterme kaydı oluşturabilir (tahmin).
- Rapor (yönetici): hangi ev, kim tarafından, hangi tarihte gösterildi; ilan, kullanıcı ve tarih aralığına göre süzülür. Her ilanın gösterme sayısı ilan detayında görünür.
- Ziyaretçi sitesinde ilan detayında "Bu ilan N kişi tarafından görüldü" gibi bir sayı gösterilir (kullanıcı istedi). Sayı, ilana ait gösterme kayıtlarının sayısıdır; her kayıt bir kez sayılır (kullanıcı kararı). Metin buna uygun yazılır: "Bu ilan N kez gösterildi". Silinen ilanın sayısı görünmez, çünkü ilan hiç görünmez. Ziyaretçi API'si yalnızca sayıyı verir; kimin gösterdiği ve notlar yönetici/saha API'sinde kalır.

## Satılık ve kiralık (kullanıcı istedi: "ikisi de olacak")

- Her ilan Satılık ya da Kiralık'tır. Ana sayfanın filtre panelinde en üstte Satılık/Kiralık seçimi bulunur (görseldeki gibi); sayfa başlığı ve "N ilan bulundu" seçime göre değişir (örn. "Satılık Daire", "Kiralık Daire").
- Kiralık ilanda fiyat aylık kiradır ve "/ ay" ile gösterilir (tahminim). Kur çevirisi ve fiyat filtresi aynı şekilde çalışır; satılık ve kiralık fiyatlar aynı aralıkta karışmasın diye fiyat aralığı seçili türe (Satılık/Kiralık) uygulanır (benim kararım).
- Kiralık ilanda depozito ve aidat alanları da girilir ve detay sayfasında gösterilir (kullanıcı kararı). Tutarlar ilanın para biriminde girilir (tahminim).
- Konut/Arsa ayrımı yine yoktur; yalnızca konut (ev) ilanları vardır.

## Ek kararlar (uzman kontrolünden sonra; hepsi benim kararım)

- Evet/hayır künye alanları (balkon, asansör, otopark, site içi, krediye uygun, takas) sabit sütunlardır. "İç / Dış / Konum Özellikleri" etiketleri ise yöneticinin yönettiği bir etiket listesidir; yönetici yeni etiket ekleyebilir ve ilana işaretleyebilir.
- Isıtma, eşya durumu, kullanım durumu, tapu durumu, mutfak tipi gibi seçmeli alanlar sabit değer listeleridir (Türkçe değerler builder belirler); filtreler ve yönetici formu bu listeleri kullanır.
- İl, ilçe ve mahalle serbest metindir; il/ilçe filtresi veritabanındaki mevcut değerlerden doldurulur.
- Ev sahibi, ilanla birlikte tutulan tek bir kayıttır (bir ilana bir ev sahibi).
- Silme yumuşak silmedir (soft delete; kullanıcı istedi): ilan "silindi" diye işaretlenir ve silinme tarihi tutulur; ilan, ev sahibi kaydı ve fotoğraf dosyaları gerçekte silinmez. Silinen ilan ziyaretçi sitesinde ve ziyaretçi API'sinde hiç görünmez. Yönetici listesinde varsayılan olarak silinenler gizlidir; yönetici filtreyle (Aktif / Silinenler / Hepsi) silinenleri görebilir. Silinen ilanı geri alma düğmesi eklenmesi benim tahminimdir (kullanıcı istemedi; kalıcı silme yoktur).
- Fotoğraf: jpg/png/webp, dosya başına en çok 10 MB, ilan başına en çok 40 adet. Fotoğrafsız ilan kaydedilebilir; kartta yer tutucu görsel gösterilir. Tek fotoğraflı kartta çubuk/nokta gösterilmez.
- Kayıt tutma (kullanıcı istedi): ilan eklenirken ve silinirken kimin yaptığı ve ne zaman yaptığı kaydedilir. İlan kaydında "oluşturan, oluşturma zamanı, silen, silinme zamanı" alanları tutulur ve yönetici detay sayfasında görünür. "Kim" giriş yapan yöneticinin kullanıcı adıdır (üç yönetici hesabı vardır). Tahminim (kullanıcı söylemedi): düzenlemelerde de "son güncelleyen, son güncelleme zamanı" tutulur. Geri alma yapılırsa o da kim/ne zaman olarak kaydedilir.
- Yönetici ve ziyaretçi sitesi aynı Vue uygulamasıdır; yönetici sayfaları `/admin` altındadır. Kart ve detay bileşenleri paylaşılır.
- Yalnızca yerel `docker compose up` hedeflenir; HTTPS/alan adı kapsam dışıdır. JWT anahtarı ve üç yöneticinin kullanıcı adı/parolası `.env` dosyasından okunur.
- Danışman adı, telefonu ve "Mesaj Gönder" bağlantısı (WhatsApp) `.env`/yapılandırmadan gelir; ayar ekranı yoktur.
- Test: "Başarısızlık sayılacak durumlar"daki iki gizlilik kuralı (ev sahibi bilgisi ziyaretçi API'sinde yok, yönetici API'si girişsiz 401 verir) otomatik testle doğrulanır; arayüz davranışları elle kontrol edilir.

## Builder'a bırakılanlar

- Veritabanı şeması, API uç nokta adları, klasör yapısı, Vue bileşen ayrımı.
- Görsel stil: referans görsellerin yeşil/beyaz sade düzeni; birebir kopya değil, logo/marka kullanılmaz ("emlakjet" adı ve logosu kullanılmaz).
- Sayfalama ve fotoğraf boyutlandırma ayrıntıları.
- Türkçe arayüz; binlik ayırıcı nokta.
- Para birimleri ₺, $ ve € (kullanıcı istedi). Yönetici her ilanda fiyatı ve para birimini seçer; ilan o birimle gösterilir (örn. 22.000.000 ₺, 450.000 $). Kullanıcı kararı: bir kur belirlenir (1 $ = ? ₺, 1 € = ? ₺); fiyat filtresi ve fiyata göre sıralama bu kurla ₺'ye çevrilerek yapılır, böylece her para biriminden ilan aynı fiyat aralığında gelir. İlan kartında ve detayda fiyat ilanın kendi birimiyle gösterilir; çeviri yalnızca filtre/sıralamada kullanılır. Tahminim (kullanıcı yalnızca "bir kur belirleriz" dedi): kurlar yönetici rolünün erişebildiği bir ayar ekranından elle girilir; kur değişince filtre ve sıralama yeni kurla çalışır, ilanın kendi fiyatı değişmez; kurun son güncelleme zamanı ve kimin değiştirdiği kaydedilir. Konum filtresi fiyat ve para biriminden bağımsızdır (bkz. "Filtreleme").
