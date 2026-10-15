# Product Resilience Lab — .NET 10

Tek entity (`Product`) üzerinden Clean/Onion mimarisi ve resilience stratejilerini deneyen, controller tabanlı eğitim API'si. PostgreSQL 17, EF Core 10 ve Polly 8 kullanır.

## Mimari

```text
Domain ← Application ← Infrastructure ← Api (composition root)
```

- **Domain:** yalnızca Product entity'si (Id, Name, Price, Stock).
- **Application:** DTO'lar, repository/service sözleşmeleri ve ProductService. EF Core ve Polly bağımlılığı yoktur.
- **Infrastructure:** PostgreSQL DbContext, repository, resilience laboratuvarı. Her denemede ayrı DbContext açılır; hedging aynı context'i paralel kullanmaz.
- **Api:** ProductsController, ResilienceController, ProblemDetails hata eşlemesi. Program.cs yalnızca DI, middleware ve başlatma sorumluluğunu taşır.

## Başlatma

Gerekenler: .NET SDK **10.0.402**, Docker + Compose, smoke testi için Python 3.

```bash
bash scripts/setup.sh
bash scripts/start.sh
```

Betikler depo kökünden bağımsız çalışır. Bulut ortamında SDK `/workspace/.dotnet`, NuGet önbelleği `/workspace/.nuget` altında tutulur. SDK zaten kuruluysa yeniden indirilmez. Paket sürümleri ve transitif bağımlılıklar `packages.lock.json` dosyalarıyla kilitlidir; restore kilitli modda yapılır.

Başlatma PostgreSQL'in hazır olmasını bekler, `--initialize-db` ile eğitim veritabanını `EnsureCreatedAsync` üzerinden oluşturur ve API'yi başlatır. Bu işlem tekrar çalıştırılabilir. Şema değişiklikleri için production migration akışı içermez; veri kaybı yaratabilecek otomatik drop/reset yapılmaz.

Varsayılan geliştirme bağlantısı `Host=127.0.0.1;Port=5432;Database=products;Username=postgres`. `ConnectionStrings__Products` ile değiştirilebilir. Compose yalnızca loopback'e açılır ve yerel eğitim için trust authentication kullanır; production ortamında parola/TLS ve uygun erişim denetimi gerekir. Gerçek sırları dosyalara kaydetmeyin.

`ASPNETCORE_URLS` varsayılan olarak 5080 portunu kullanır. `scripts/env.sh` Development ortamını seçer. Swagger UI `/swagger` yolundadır. Tarayıcıda endpoint seçip **Try it out → Execute** ile CRUD ve resilience senaryolarını deneyebilirsiniz. OpenAPI belgesi `/openapi/v1.json` yolundadır. Her ikisi de yalnızca Development ortamında açılır.

## Product API

| Metot | Yol | Sonuç |
|---|---|---|
| GET | /api/products | Ürün listesi |
| GET | /api/products/{id} | Ürün veya 404 |
| POST | /api/products | 201 ve Location |
| PUT | /api/products/{id} | 204 veya 404 |
| DELETE | /api/products/{id} | 204 veya 404 |

```bash
curl -i -X POST http://127.0.0.1:5080/api/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Klavye","price":1250.50,"stock":10}'
```

Boş/200 karakterden uzun isim, negatif fiyat/stok 400 döndürür. Veritabanında da negatif fiyat/stok için check constraint vardır.

## Resilience politikaları

`GET /api/resilience/{policy}?failures=2&delayMs=0`

`failures` her isteğin ilk N denemesini geçici hatayla sonlandırır (0–100). `delayMs` her denemenin iptal edilebilir gecikmesidir (0–10000). Başarılı deneme gerçek PostgreSQL ürün listesini okur. Sonuçtaki `attempts`, `fallback`, `products` alanları davranışı gözlemlemenizi sağlar. Laboratuvar yalnızca **Development** ortamında erişilebilir.

| policy | Ayar ve deney |
|---|---|
| retry | En fazla 3 tekrar (toplam 4 deneme), 100 ms tabanlı exponential backoff + jitter. `failures=2` → 3 denemede başarı; `failures=10` → 503. |
| circuit-breaker | 10 saniyelik pencerede en az 4 deneme, %50 hata oranında 5 saniye açık devre. 4 kez `failures=1` gönderin, sonra hatasız isteğin bile 503 aldığını görün; 5 saniye sonra başarılı probe devreyi kapatır. |
| timeout | Deneme başına 1 saniye. `delayMs=1500` → 504. Token işleme kadar iletilir. |
| fallback | Geçici hata durumunda boş ürün listesi, `fallback=true`. Gerçek boş listeyle ayırt edilebilir. `failures=1` ile deneyin. |
| hedging | İlk denemeden 100 ms sonra, gerekirse en fazla 2 ek paralel okuma. `failures=1` ile diğer deneme kazanır. Başarısızlıkta da yeni deneme başlatabilir. |
| rate-limit | Süreç genelinde 5 saniyede 3 istek; kuyruk yok. Hızlı 4. istek → 429. |
| bulkhead | Süreç genelinde aynı anda 2 işlem; kuyruk yok. 3 paralel `delayMs=500` isteğinin biri → 429. Polly 8'de concurrency limiter ile uygulanır. |
| cache | Ürün okumasını 10 saniye saklar. İlk yanıt `attempts=1`, hit `attempts=0`. Bu ayrı eğitim cache'i yazmalarda temizlenmez; en fazla TTL kadar eski veri gösterebilir. |
| combined | Dıştan içe **Fallback → Retry → Circuit Breaker → Timeout**. Her retry breaker tarafından sayılır; açık devre retry edilmez, fallback ile boş sonuç döner. |

Retry ve hedging yalnızca okumalarda kullanılır. CRUD yazmaları otomatik tekrar edilmez; idempotency olmadan yazmayı tekrar etmek çift işlem oluşturabilir. Gerçek geçici Npgsql hataları (`IsTransient`) ve simüle edilen hatalar transient olarak ele alınır. İş kuralı, doğrulama ve iptal hataları tekrar edilmez. Politikalar singleton olarak yaşar; devre ve limit durumu istekler arasında korunur, yeniden başlatmada sıfırlanır. Çok instance'lı dağıtık limit değildir.

Cache ve policy composition, Polly 8'de ayrı bir yerleşik cache/PolicyWrap stratejisi değildir: cache IMemoryCache ile, composition pipeline sıralamasıyla gerçekleştirilir. Hedging paralel **okuma** içindir; yükü artırabilir. Fallback sonucu güncel veri garantisi vermez.

## Doğrulama

API çalışırken, yeni başlatılmış laboratuvar durumuyla:

```bash
python3 scripts/smoke.py
```

27 kontrol: gerçek PostgreSQL CRUD, kalıcı güncelleme, veri doğrulama, retry başarı/tükenme, fallback, timeout, hedging, cache miss/hit, rate rejection, eşzamanlı bulkhead rejection, devre açılması ve half-open toparlanması, combined ve bilinmeyen politika. Test kendi ürününü siler. Circuit/rate/cache durumu önceki manuel denemelerden etkilenebileceği için tekrar koşmadan API'yi yeniden başlatın. Test başlangıcında ürün oluşturulamaması ve diğer HTTP hataları testi başarısız kılar.

```bash
docker compose stop  # verileri koruyarak PostgreSQL'i durdurur
```

Uygulama durdurma: API terminalinde Ctrl+C. Veriler named volume içinde kalır.
