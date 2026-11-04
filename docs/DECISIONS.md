# MiniDPP — Mimari kararlar ve teknik mülakat notları

## 60 saniyelik anlatım

> Bu ödev için kasıtlı olarak basit bir mimari seçtim: tek ASP.NET Core MVC (.NET 10) projesi, hem Razor ekranlarını hem de API endpoint'lerini sunuyor. CSV satırlarını ayrı bir servis okuyup doğruluyor; geçerli satırlar EF Core aracılığıyla SQLite'a kaydediliyor. GTIN üzerinden HTML pasaport, schema.org/Product biçiminde JSON-LD ve o HTML adresini açan PNG QR kodu üretiliyor. Karbon bilgisi varsayılan olarak gizli, query parametresiyle sadece demo için açılıyor. Bonus olarak ürün güncellenirken eski veriler sürüm numarası ve UTC tarih ile korunuyor.

## Veri akışı

1. Kullanıcı `POST /import` (HTML formu) veya `POST /api/import` (multipart/form-data) ile CSV gönderir.
2. `TextFieldParser` CSV'yi (tırnak içi alanları destekleyerek) okur, fiziksel satır numarasını takip eder; başlık 1. satırdır.
3. `CsvRowValidator` boş alan, rakamsal yüzde, aralık, toplam %100, karbon ve GS1 GTIN-14 kontrolünü yapar.
4. Hatalı satır atlanır ve açıklaması rapora eklenir. Diğer satırlar kaydedilir.
5. `CsvImportService` yeni model/batch oluşturur veya değişmiş kayıtları günceller. Değişiklik öncesi snapshot saklanır.
6. `DppService` GTIN'e göre modeli, ilgili son batch'i getirir.
7. `DppController` HTML, JSON-LD veya QR PNG döndürür.

## Veritabanı ilişkileri

```text
ProductModels
  Id PK, Gtin UNIQUE TEXT(14), Name, CottonPercent, ElastanePercent, CurrentVersion
  ├── 1:N ProductBatches
  │     Id PK, ProductModelId FK, BatchNo, ProductionPlace,
  │     CarbonKgCo2e, UpdatedAtUtc
  │     UNIQUE(ProductModelId, BatchNo)
  └── 1:N ProductVersions  [BONUS]
        Id PK, ProductModelId FK, VersionNumber, UpdatedAtUtc, SnapshotJson
        UNIQUE(ProductModelId, VersionNumber)
```

**GTIN neden string?** Kimliktir, matematiksel sayı değildir. 14 hanesi ve baştaki sıfırı korunmalı.

**Model ve batch neden ayrı?** Aynı model farklı üretim partileriyle tekrar kullanılabilir; gereksiz veri tekrarını ve tutarsızlığı önler.

**Repository Pattern neden yok?** EF Core DbContext zaten takip ve unit-of-work sağlar. Bu küçük projede ek katman kodu uzatırdı. Servisler iş kurallarını bir arada tutar.

**Ayrı API + MVC + React neden yok?** Ödev tek çalışan uygulama ve basit arayüz kabul ediyor. Ayrı servis/deploy, HTTP roundtrip ve UI build sürecinin bu kapsamda faydası az.

**SQLite ve EnsureCreated neden?** `dotnet run` ile anında çalışsın diye. Uzun ömürlü ürünlerde şema migrasyonları ve daha ayrıntılı veri geçişi gerekir.

**TextFieldParser neden?** CSV'nin tırnaklı alanlarını doğru ayrıştırır; üçüncü parti CSV kütüphanesine gerek kalmaz.

## Bonus sürümleme kararı

- İlk kez eklenen ürün: `CurrentVersion = 1`, geçmişi boş.
- Model adı, elyaf ya da batch verisi değişirse güncel kayıt üzerine yazılmadan **önce** modelin ve bütün batch'lerin JSON snapshot'ı alınır.
- `ProductVersions` tablosuna eski sürüm numarası, değişim anı UTC ve snapshot yazılır; `CurrentVersion` artırılır.
- Değişmeyen CSV satırının yeniden yüklenmesi bir sürüm oluşturmaz.
- Bir modelin çok batch'i olabilir; `/dpp/{gtin}` yolunda batch tanımlayıcısı olmadığından en son içe aktarılan veya değiştirilen batch gösterilir. Gerçek sistemde lot/seri kimliği URL'ye eklenmelidir.
- Snapshot bonus özelliği, ayrı servis ve karmaşık event sourcing yerine tek tablo + güncelleme işlemi içerisinde uygulanır.

## Görünürlük ve güvenlik

HTML ve JSON-LD, aynı `ProductPassport` DTO'sunu ve aynı `DppService.CanSeeCarbon` rol kontrolünü kullanır. Varsayılan tüketici çıktısında karbon alanı hiç üretilmez, sadece CSS ile gizlenmez. `?rol=yetkili` her ikisinde de görünürlüğü açar. Query parametresi **kimlik doğrulama değildir**; gerçek gizli veri için sunucu tarafı yetki kontrolleri zorunludur.

## Beş dakikalık demo

1. `dotnet run --project MiniDPP.Web --urls http://localhost:5090`; ana sayfayı göster.
2. Örnek CSV'yi yükle. **4. satır:** GTIN boş. **5. satır:** elyaf %98. Kalan 3 ürün kaydedildi.
3. Ürün kartından `/dpp/08690000000012` HTML pasaportunu aç.
4. JSON-LD'nin schema.org/Product yapısını ve PNG QR kodunu göster. QR herkese açık HTML sayfasına yönlendirir.
5. `?rol=yetkili` parametresiyle HTML ve JSON-LD'de karbonun belirdiğini göster.
6. Aynı CSV'yi tekrar yükle: kayıtlar değişmedi. Bir satırın karbon değerini değiştirip yeniden yükle: güncelleme + eski snapshot.

## Mülakatta gelebilecek sorular

**Bir CSV satırı hatalıysa?** Gerekçesi ve gerçek CSV satır numarası raporlanır, başka geçerli satırların kayıtları durmaz.

**GS1 nasıl kontrol ediliyor?** İlk 13 haneye sağdan başlayarak 3 ve 1 ağırlıkları dönüşümlü uygulanır; 14. hane toplamı 10'un katına tamamlayan kontrol basamağıdır.

**Neden decimal?** Elyaf yüzdeleri ve %100 toplamını ondalık kayan nokta hatası olmadan karşılaştırmak için.

**Aynı CSV tekrar yüklenirse?** Unique GTIN ve (ModelId, BatchNo) kombinasyonu var; içerik eşitse no-op, değiştiyse geçmiş sürüm oluşur.

**Bir modelin iki batch'i olursa?** Model aynı kalır, batch sayısı artar. Mevcut GTIN URL'si son güncellenen batch'i seçer; üretim sisteminde daha ayrıntılı pasaport kimliği gerekir.

**MVC sayfası neden API'yi HTTP üzerinden çağırmıyor?** İkisi de tek process içinde, aynı servisleri DI ile çağırıyor. Fazladan HTTP isteği gereksiz.

**Canlı küçük değişiklik?** Yeni CSV alanı için DTO + doğrulama + entity + HTML/JSON-LD mapping + test güncellenebilir. Alternatif: yeni bir validasyon kuralı ekleyip testini yazmak.

**Production'da ne farklı olur?** Gerçek authentication/authorization, EF migrations, güvenilir audit identity, transactional import/idempotency, loglama, background jobs, tenant sınırları, yayınlanan immutable DPP versiyonları.

## Commit ilerleme notu

1. .NET 10 MVC solution ve EF Core SQLite ilişkisel model.
2. CSV validation ve kısmi import, DPP HTML/JSON-LD/QR endpoint'leri.
3. Razor ekranları ve ekteki örnek CSV.
4. API upload, otomatik testler, GitHub Actions CI.
5. Karar notları ve README (bu commit).

**Teslim tarihi notu:** Başvuru e-postasında 5 Kasım 2026 23:59, PDF'de 10 Kasım 2026 23:59 yazıyor. Emin olunana kadar daha erken tarih esas alınmalı.
