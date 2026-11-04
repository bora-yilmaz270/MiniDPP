# MiniDPP

ESG Next mini Dijital Ürün Pasaportu (DPP) teknik değerlendirme uygulaması.

**Teknoloji:** ASP.NET Core MVC (.NET 10), Razor Views, API Controller, Entity Framework Core, SQLite, QRCoder ve xUnit.

## Kurulum ve çalıştırma

Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

Depo kökünden:

```bash
dotnet restore MiniDPP.slnx
dotnet run --project MiniDPP.Web --urls http://localhost:5090
```

Tarayıcı: **http://localhost:5090**. SQLite veritabanı (`minidpp.db`) ilk çalıştırmada otomatik oluşturulur. Hesap, bulut servisi, React veya ek veritabanı kurulumu gerekmiyor.

> Küçük bir assessment olduğu için EF Core `EnsureCreated` kullanıldı. Canlı üretim ortamında şema değişiklikleri EF Core migrations ile yönetilmelidir.

## CSV içe aktarma

Ana sayfadaki formu kullanın veya API üzerinden gönderin:

```bash
curl -F "file=@samples/urunler_duzeltilmis_test.csv" http://localhost:5090/api/import
```

İstenen CSV başlığı:

```csv
gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e
```

**Örnek CSV sonucu:** 3 yeni kayıt, 2 hatalı satır.

- **4. satır:** GTIN boş.
- **5. satır:** pamuk (%95) + elastan (%3) = %98.

Başlık **1. satır** kabul edilir. Bir satır hatalıysa diğer geçerli satırlar kaydedilmeye devam eder. GTIN baştaki sıfırları koruyan **string** olarak saklanır; GS1 kontrol basamağı kontrol edilir. Yüzdeler 0–100 aralığında ve toplam %100 olmalıdır. Karbon negatif olamaz. Boş model adı, batch numarası ve üretim yeri kabul edilmez. CSV parser tırnak içindeki alanları destekler.

**Önemli:** GTIN'in baştaki sıfırlarının kaybolmaması için örnek CSV dosyasını Excel'de açıp kaydetmeyin.

Aynı veriler tekrar içe aktarıldığında kayıtlar çoğaltılmaz. Bir kayıt değişmişse güncellenir ve önceki sürüm saklanır.

## Ürün pasaportu ve örnek adresler

CSV'yi içe aktardıktan sonra:

| Çıktı | URL |
|---|---|
| HTML DPP | http://localhost:5090/dpp/08690000000012 |
| JSON-LD (schema.org/Product) | http://localhost:5090/dpp/08690000000012.jsonld |
| PNG QR kodu | http://localhost:5090/dpp/08690000000012/qr.png |
| Yetkili demo HTML | http://localhost:5090/dpp/08690000000012?rol=yetkili |
| Yetkili demo JSON-LD | http://localhost:5090/dpp/08690000000012.jsonld?rol=yetkili |

Tüketici (varsayılan) görünümünde `karbon_kg_co2e` **HTML ve JSON-LD çıktılarının ikisinde de bulunmaz**. `rol=yetkili` parametresiyle her iki çıktıda görünür. QR kodu daima genel tüketici HTML adresine yönlendirir.

**Güvenlik notu:** `?rol=yetkili` yalnızca ödevde istenen görünürlük ayrımı demosudur, gerçek kimlik doğrulama / yetkilendirme değildir.

## Veritabanı ve bonus

- `ProductModels`: Benzersiz GTIN, model adı, pamuk/elastan oranları, güncel sürüm.
- `ProductBatches`: Modele bağlı 1:N üretim partileri, üretim yeri, karbon değeri.
- `ProductVersions` (**bonus**): Model FK, sürüm numarası, UTC güncelleme tarihi ve **önceki hâlin JSON snapshot'ı**.

Bir GTIN için birden fazla batch varsa, ödevin `/dpp/{gtin}` formatında batch anahtarı olmadığı için en son içe aktarılan/güncellenen batch gösterilir (demo tasarım varsayımı). Bir model veya batch değiştiğinde önceki tüm model/batch verileri snapshot olarak saklanır; yeni sürüm numarası artırılır. Aynı kayıt tekrar yüklenirse yeni sürüm oluşmaz.

## Testler

```bash
dotnet test MiniDPP.slnx
```

Testler; GTIN-14 kontrol basamağını, alan doğrulamalarını, **3 doğru / 2 hatalı** CSV sonucunu ve satır numaralarını, tekrarlı importu, model/batch ilişkisini, geçmiş sürümleri, HTML/JSON-LD rol ayrımını ve 404 sonuçlarını içerir. GitHub Actions her push ve PR'da build/test çalıştırır.

## Teknik mülakat notları

Mimari gerekçeleri, alternatifler, veri akışı, örnek demo akışı ve olası sorular: **[docs/DECISIONS.md](docs/DECISIONS.md)**.

Bu uygulama teknik değerlendirme demosudur; gerçek DPP servisi olarak kullanılmamalıdır.
