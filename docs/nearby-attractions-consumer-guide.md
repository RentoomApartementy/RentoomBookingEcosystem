# Pobliskie atrakcje apartamentu — przewodnik konsumenta (RentoomBookingWeb / Api / StayWell)

Ten dokument opisuje, jak w solutionie **RentoomBookingEcosystem** czytać i serwować dane o
**pobliskich atrakcjach apartamentu** ("nearby attractions"), które wytwarza i zapisuje aplikacja
**Rentoom** (osobny projekt). Dane są **read‑only** dla tego solutiona — nie zapisujemy ich tutaj.

Całość celowo powiela istniejącą konwencję integracji **RentoomApp → SocialMedia**
(`SharedClasses\Integrations\RentoomApp\SocialMedia\`). Jeśli w czymś masz wątpliwość — zajrzyj, jak
zrobiono to dla social mediów, i zrób tak samo.

---

## 1. Źródło danych

- **Baza:** `rentoomdb` (Postgres), **schema:** `rentoom`.
- **Connection string key:** `RentoomDbConnectionString` — ten sam, którego już używają wszystkie
  `Rapp*DbContext` (rozwiązywany przez `PostgresConnectionStringProvider.GetPostgresConnectionString(...)`
  do zmiennej `rentoomAppConnectionString` w obu `Program.cs`).
- Dane pochodzą z Google Places i są **odświeżane wyłącznie po stronie aplikacji Rentoom** (ręcznie lub
  zbiorczo). Tutaj tylko je czytamy.

### Tabele

**`rentoom.ApartmentNearbyAttractionsSets`** — 1 wiersz na apartament (ApartmentItem). Metadane odświeżenia.

| Kolumna | Typ | Uwagi |
|---|---|---|
| `ApartmentItemId` | `int` | **PK**. To id *itemu* apartamentu (nie object id). |
| `ObjectId` | `int` | IdoSell object id (to, którym operuje Api/StayWell). |
| `LastRefreshedUtc` | `timestamp` (nullable) | **UTC**. |
| `Latitude` | `double` (nullable) | współrzędne użyte do zapytania. |
| `Longitude` | `double` (nullable) | |
| `LastRefreshStatus` | `text` | `ok` / `no-location` / `no-api-key` / `failed`. |

**`rentoom.ApartmentNearbyAttractions`** — N wierszy na apartament (lista atrakcji).

| Kolumna | Typ | Uwagi |
|---|---|---|
| `Id` | `int` | **PK**, identity. |
| `ApartmentItemId` | `int` | **FK** → `ApartmentNearbyAttractionsSets.ApartmentItemId`. |
| `Name` | `text` | nazwa miejsca. |
| `Category` | `text` | etykieta PL (np. "Restauracja", "Przystanek"). |
| `DistanceMeters` | `int` | odległość w linii prostej (Haversine). |
| `WalkMinutes` | `int` | szacowany czas dojścia (~80 m/min). |
| `Address` | `text` (nullable) | |
| `Rating` | `double` (nullable) | ocena Google (1–5). |
| `GoogleMapsUri` | `text` (nullable) | link do Map Google. |
| `ExternalPlaceId` | `text` (nullable) | place id Google (dedup). |

> **Uwaga o kolumnach:** nazwy są **PascalCase** (encje w Rentoom nie używają `[Column]`), więc encje EF
> po tej stronie też mają PascalCase i **nie** potrzebują `[Column]`. Wymagany jest tylko
> `[Table(..., Schema = "rentoom")]`.

### Znaczenie statusów
- `ok` — dane świeże, `Items` aktualne.
- `no-location` — apartament nie ma współrzędnych; brak świeżych atrakcji (mogą istnieć starsze wiersze).
- `no-api-key` — po stronie Rentoom nie skonfigurowano klucza Google.
- `failed` — ostatnie odświeżenie się nie powiodło (poprzednie dane bywają zachowane).

---

## 2. Konwencja: powielamy integrację SocialMedia 1:1

Pliki‑wzorce (przeczytaj przed implementacją):
- `SharedClasses\Integrations\RentoomApp\SocialMedia\Database\RappSocialMediaDbContext.cs`
- `SharedClasses\Integrations\RentoomApp\SocialMedia\Models\ApartmentItemSocialMedia.cs` (DTO **i** encja w jednym pliku)
- `SharedClasses\Integrations\RentoomApp\SocialMedia\ApartmentSocialMediaService.cs` (konkretny serwis, `IDbContextFactory`, `MapToDto`)
- Rejestracja: `RentoomBookingWeb\Program.cs` (l. 122‑123 i 137), analogicznie w `Api\Program.cs` (l. ~99‑109).
- Konsumpcja: `RentoomBookingWeb\...\ApartmentPage.razor.cs` (l. 57 inject, l. 1057 wywołanie).

Docelowy layout (nowy folder, taki sam układ jak SocialMedia):

```
SharedClasses\Integrations\RentoomApp\NearbyAttractions\
├─ Database\RappNearbyAttractionsDbContext.cs
├─ Models\NearbyAttractions.cs            (DTO + encje w jednym pliku)
└─ ApartmentNearbyAttractionsService.cs   (konkretny serwis, bez interfejsu)
```

---

## 3. DTO + encje EF (`SharedClasses\Integrations\RentoomApp\NearbyAttractions\Models\NearbyAttractions.cs`)

Jeden plik, jeden namespace — jak `ApartmentItemSocialMedia.cs`. DTO są plain‑class, PascalCase, nullable,
**bez atrybutów JSON** (na drucie i tak jest camelCase — patrz §7).

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models
{
    // ---------- DTO (kontrakt dla Web/Api/StayWell) ----------

    public class NearbyAttractionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DistanceMeters { get; set; }
        public int WalkMinutes { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public string? GoogleMapsUri { get; set; }
        public string? ExternalPlaceId { get; set; }
    }

    public class NearbyAttractionsResultDTO
    {
        public int ApartmentItemId { get; set; }
        public List<NearbyAttractionDto> Items { get; set; } = new();
        public DateTime? LastRefreshedUtc { get; set; }   // UTC
        public string Status { get; set; } = string.Empty; // ok / no-location / no-api-key / failed / (pusty = brak danych)
    }

    // ---------- Encje EF (read-only, schema "rentoom") ----------

    [Table("ApartmentNearbyAttractionsSets", Schema = "rentoom")]
    public class ApartmentNearbyAttractionsSet
    {
        [Key]
        public int ApartmentItemId { get; set; }
        public int ObjectId { get; set; }
        public DateTime? LastRefreshedUtc { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string LastRefreshStatus { get; set; } = string.Empty;

        public List<ApartmentNearbyAttraction> Attractions { get; set; } = new();
    }

    [Table("ApartmentNearbyAttractions", Schema = "rentoom")]
    public class ApartmentNearbyAttraction
    {
        [Key]
        public int Id { get; set; }
        public int ApartmentItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DistanceMeters { get; set; }
        public int WalkMinutes { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public string? GoogleMapsUri { get; set; }
        public string? ExternalPlaceId { get; set; }
    }
}
```

---

## 4. DbContext (`...\NearbyAttractions\Database\RappNearbyAttractionsDbContext.cs`)

Jak `RappSocialMediaDbContext`, ale z dwiema tabelami — potrzebny minimalny `OnModelCreating`, żeby
`Include(s => s.Attractions)` działał (relacja 1‑do‑wielu po `ApartmentItemId`).

```csharp
using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Database
{
    public class RappNearbyAttractionsDbContext : DbContext
    {
        public RappNearbyAttractionsDbContext(DbContextOptions<RappNearbyAttractionsDbContext> options)
            : base(options) { }

        public DbSet<ApartmentNearbyAttractionsSet> ApartmentNearbyAttractionsSets { get; set; }
        public DbSet<ApartmentNearbyAttraction> ApartmentNearbyAttractions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApartmentNearbyAttractionsSet>()
                .HasMany(s => s.Attractions)
                .WithOne()
                .HasForeignKey(a => a.ApartmentItemId);
        }
    }
}
```

---

## 5. Serwis (`...\NearbyAttractions\ApartmentNearbyAttractionsService.cs`)

Konkretny serwis bez interfejsu — dokładnie jak `ApartmentSocialMediaService`. Dwie metody wejściowe:
`ApartmentItemId` (dla Web) i `ObjectId` (dla Api).

```csharp
using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions
{
    public class ApartmentNearbyAttractionsService
    {
        private readonly IDbContextFactory<RappNearbyAttractionsDbContext> _dbContextFactory;

        public ApartmentNearbyAttractionsService(IDbContextFactory<RappNearbyAttractionsDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>Web: dokładne dopasowanie po kluczu głównym (ApartmentItemId = apartment.Items[0].Id).</summary>
        public async Task<NearbyAttractionsResultDTO?> GetNearbyAttractionsAsync(int apartmentItemId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var set = await db.ApartmentNearbyAttractionsSets
                .AsNoTracking()
                .Include(s => s.Attractions)
                .FirstOrDefaultAsync(s => s.ApartmentItemId == apartmentItemId, ct);

            return set == null ? null : MapToDto(set);
        }

        /// <summary>Api/StayWell: po IdoSell object id. Przy wielu itemach bierzemy najświeższy set.</summary>
        public async Task<NearbyAttractionsResultDTO?> GetNearbyAttractionsByObjectIdAsync(int objectId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var set = await db.ApartmentNearbyAttractionsSets
                .AsNoTracking()
                .Include(s => s.Attractions)
                .Where(s => s.ObjectId == objectId)
                .OrderByDescending(s => s.LastRefreshedUtc)
                .FirstOrDefaultAsync(ct);

            return set == null ? null : MapToDto(set);
        }

        private static NearbyAttractionsResultDTO MapToDto(ApartmentNearbyAttractionsSet set)
        {
            return new NearbyAttractionsResultDTO
            {
                ApartmentItemId = set.ApartmentItemId,
                LastRefreshedUtc = set.LastRefreshedUtc,
                Status = set.LastRefreshStatus,
                Items = set.Attractions
                    .OrderBy(a => a.DistanceMeters)
                    .Select(a => new NearbyAttractionDto
                    {
                        Name = a.Name,
                        Category = a.Category,
                        DistanceMeters = a.DistanceMeters,
                        WalkMinutes = a.WalkMinutes,
                        Address = a.Address,
                        Rating = a.Rating,
                        GoogleMapsUri = a.GoogleMapsUri,
                        ExternalPlaceId = a.ExternalPlaceId
                    })
                    .ToList()
            };
        }
    }
}
```

---

## 6. RentoomBookingWeb (Blazor Server, `ApartmentPage.razor`)

### 6.1. Rejestracja DI — obok SocialMedia w `RentoomBookingWeb\Program.cs`

Tuż przy istniejącym `AddDbContextFactory<RappSocialMediaDbContext>` (l. 122‑123) i
`AddScoped<ApartmentSocialMediaService>()` (l. 137):

```csharp
builder.Services.AddDbContextFactory<RappNearbyAttractionsDbContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));
// ...
builder.Services.AddScoped<ApartmentNearbyAttractionsService>();
```

### 6.2. Użycie w `ApartmentPage.razor.cs`

Wstrzyknięcie (jak social media, l. 57):
```csharp
[Inject] public ApartmentNearbyAttractionsService NearbyAttractionsService { get; set; } = default!;

private NearbyAttractionsResultDTO? _nearbyAttractions;
```

Pobranie tam, gdzie liczone jest social media (okolice l. 1057 — po object id item używamy `Items[0].Id`):
```csharp
if (_apartment?.Items is { Count: > 0 } && _apartment.Items[0].Id.HasValue)
{
    _nearbyAttractions = await NearbyAttractionsService
        .GetNearbyAttractionsAsync(_apartment.Items[0].Id.Value);
}
```

### 6.3. Render (markup)

Prosty wariant (można też ubrać w kartę wzorem `SharedFrontend\...\UpsellComponents\UpsellTile/UpsellList.razor`):

```razor
@if (_nearbyAttractions?.Items is { Count: > 0 })
{
    <section class="nearby-attractions">
        <h3>Atrakcje w okolicy</h3>
        @foreach (var group in _nearbyAttractions.Items.GroupBy(i => i.Category).OrderBy(g => g.Key))
        {
            <h4>@group.Key</h4>
            <ul>
                @foreach (var item in group.OrderBy(i => i.DistanceMeters))
                {
                    <li>
                        @if (!string.IsNullOrEmpty(item.GoogleMapsUri))
                        {
                            <a href="@item.GoogleMapsUri" target="_blank" rel="noopener">@item.Name</a>
                        }
                        else { <span>@item.Name</span> }
                        <span class="dist">@FormatDistance(item.DistanceMeters) · ~@item.WalkMinutes min pieszo</span>
                    </li>
                }
            </ul>
        }
    </section>
}
```

Opcjonalna normalizacja odległości (spójna z aplikacją Rentoom):
```csharp
private static string FormatDistance(int meters) => meters switch
{
    < 75   => "około 50m",
    < 300  => "około 100m",
    < 750  => "około 500m",
    < 1500 => "około 1km",
    <= 2500 => "około 2km",
    <= 5000 => "więcej niż 2km",
    _      => "więcej niż 5km"
};
```

---

## 7. Api (Azure Functions, isolated worker) → StayWell

### 7.1. Rejestracja DI w `Api\Program.cs`

Obok pozostałych `Rapp*` kontekstów (l. ~99‑109), używając tej samej `rentoomAppConnectionString`:
```csharp
builder.Services.AddDbContextFactory<RappNearbyAttractionsDbContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));
builder.Services.AddScoped<ApartmentNearbyAttractionsService>();
```
`Api\local.settings.json` już zawiera `ConnectionStrings:RentoomDbConnectionString` → `Database=rentoomdb`
(w Azure wstrzykiwane jako `ConnectionStrings__RentoomDbConnectionString`). Nic dodatkowego nie trzeba.

### 7.2. Funkcja HTTP (`Api\Integrations\RentoomApp\NearbyAttractionsApi.cs`)

Wzór: `Api\Integrations\RentoomApp\ArrivalInstructionsApi.cs`. Route spójny z rodziną `apartments/{objectId}`.

```csharp
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions;

namespace RentoomBooking.Api.Integrations.RentoomApp
{
    public class NearbyAttractionsApi
    {
        private readonly ApartmentNearbyAttractionsService _service;
        private readonly ILogger<NearbyAttractionsApi> _logger;

        public NearbyAttractionsApi(ApartmentNearbyAttractionsService service, ILogger<NearbyAttractionsApi> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function("GetNearbyAttractionsForApartment")]
        public async Task<HttpResponseData> GetNearbyAttractions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "apartments/{objectId:int}/nearby-attractions")] HttpRequestData req,
            int objectId)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            if (objectId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("objectId must be a positive integer.", ct);
                return response;
            }

            try
            {
                var result = await _service.GetNearbyAttractionsByObjectIdAsync(objectId, ct)
                             ?? new RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models.NearbyAttractionsResultDTO();

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result), ct); // camelCase (DefaultSettings)
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetNearbyAttractions failed for objectId={ObjectId}", objectId);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.", ct);
                return response;
            }
        }
    }
}
```

> Zwracamy zawsze obiekt `NearbyAttractionsResultDTO` (z pustym `Items` i pustym `Status`, gdy brak setu) —
> StayWell dostaje przewidywalny kształt, bez 404.

### 7.3. StayWell (`StayWell\Services\BackendApi.cs`)

Wzór: `GetArrivalInstructionStepsAsync` / `GetApartmentMediaAsync`. Named client `"FunctionsApi"` ma już
`BaseAddress` zakończony `api/`, więc ścieżka jest względna.

```csharp
public async Task<NearbyAttractionsResultDTO> GetNearbyAttractionsAsync(int objectId)
{
    var result = await _http.GetFromJsonAsync<NearbyAttractionsResultDTO>(
        $"apartments/{objectId}/nearby-attractions", _json);
    return result ?? new NearbyAttractionsResultDTO();
}
```

Opcjonalnie (dane wolnozmienne — jak amenities) można owinąć w `GetOrSetCacheAsync(...)`.

Render w StayWell — analogicznie jak w §6.3 (te same `Items`/`Category`/`DistanceMeters`).

---

## 8. Uwagi / pułapki

- **ObjectId vs ApartmentItemId.** PK tabeli to `ApartmentItemId` (id *itemu*). Web ma go pod
  `_apartment.Items[0].Id` (tak samo pobiera social media) → używa `GetNearbyAttractionsAsync`.
  Api/StayWell operują `objectId` (IdoSell) → używają `GetNearbyAttractionsByObjectIdAsync` (najświeższy set).
- **JSON camelCase po obu stronach.** Api serializuje Newtonsoftem z globalnym `CamelCasePropertyNamesContractResolver`;
  StayWell deserializuje System.Text.Json z `JsonSerializerDefaults.Web`. Dlatego DTO **nie** potrzebują atrybutów JSON.
- **Read‑only.** Nie zapisujemy ani nie odświeżamy tych danych tutaj — robi to aplikacja Rentoom.
  Kolumn/tabel nie migrujemy w tym solutionie (migracje należą do Rentoom).
- **Statusy.** `failed`/`no-location`/`no-api-key` oznaczają brak świeżych danych; UI powinno wtedy albo nie
  renderować sekcji, albo pokazać komunikat. `Items` może być puste także przy `ok` (brak atrakcji w promieniu).
- **Czas.** `LastRefreshedUtc` jest w **UTC** — przy wyświetlaniu użyj `ToLocalTime()`.
- **Bezpieczeństwo.** Connection stringi do `rentoomdb` (z hasłem) są w `appsettings.json`/`local.settings.json`
  w postaci jawnej — warto docelowo przenieść do Key Vault / User Secrets (poza zakresem tej integracji).

---

## 9. Przykładowy payload API

`GET /api/apartments/253/nearby-attractions`:
```json
{
  "apartmentItemId": 1042,
  "lastRefreshedUtc": "2026-07-22T10:15:00Z",
  "status": "ok",
  "items": [
    {
      "name": "Muzeum Okręgowe",
      "category": "Obiekt historyczny",
      "distanceMeters": 340,
      "walkMinutes": 5,
      "address": "ul. Przykładowa 1",
      "rating": 4.6,
      "googleMapsUri": "https://maps.google.com/?cid=...",
      "externalPlaceId": "ChIJ..."
    },
    {
      "name": "Przystanek Rynek",
      "category": "Przystanek",
      "distanceMeters": 120,
      "walkMinutes": 2,
      "address": null,
      "rating": null,
      "googleMapsUri": "https://maps.google.com/?cid=...",
      "externalPlaceId": "ChIJ..."
    }
  ]
}
```

---

## 10. Prompt gotowy do wklejenia (agent AI pracujący w RentoomBookingEcosystem)

> **Zadanie:** Dodaj do solutiona RentoomBookingEcosystem odczyt „pobliskich atrakcji apartamentu” z bazy
> `rentoomdb` (schema `rentoom`) i wystaw je: (a) na stronie `ApartmentPage.razor` w RentoomBookingWeb,
> (b) przez endpoint Azure Functions w projekcie `Api`, konsumowany przez StayWell.
>
> **Trzymaj się DOKŁADNIE konwencji istniejącej integracji SocialMedia** — przeczytaj i naśladuj:
> `SharedClasses\Integrations\RentoomApp\SocialMedia\Database\RappSocialMediaDbContext.cs`,
> `...\SocialMedia\Models\ApartmentItemSocialMedia.cs`, `...\SocialMedia\ApartmentSocialMediaService.cs`,
> ich rejestrację w `RentoomBookingWeb\Program.cs` (linie ~122‑123 i 137) oraz `Api\Program.cs` (~99‑109),
> i sposób użycia w `RentoomBookingWeb\Components\Features\ReservationWorkflow\Pages\ApartmentPage.razor.cs`
> (inject ~l.57, wywołanie ~l.1057).
>
> **Utwórz** (read‑only, connection string `RentoomDbConnectionString` = `rentoomAppConnectionString`):
> 1. `SharedClasses\Integrations\RentoomApp\NearbyAttractions\Models\NearbyAttractions.cs` — DTO
>    `NearbyAttractionDto`, `NearbyAttractionsResultDTO` oraz encje `ApartmentNearbyAttractionsSet`
>    (`[Table("ApartmentNearbyAttractionsSets", Schema="rentoom")]`, `[Key] ApartmentItemId`, nawigacja
>    `Attractions`) i `ApartmentNearbyAttraction` (`[Table("ApartmentNearbyAttractions", Schema="rentoom")]`).
>    Kolumny PascalCase, bez `[Column]`.
> 2. `...\NearbyAttractions\Database\RappNearbyAttractionsDbContext.cs` — dwa `DbSet` + `OnModelCreating`
>    z relacją 1‑do‑wielu set→atrakcje po `ApartmentItemId`.
> 3. `...\NearbyAttractions\ApartmentNearbyAttractionsService.cs` — konkretny serwis (bez interfejsu),
>    `IDbContextFactory<RappNearbyAttractionsDbContext>`, `AsNoTracking().Include(s => s.Attractions)`,
>    metody `GetNearbyAttractionsAsync(int apartmentItemId)` i `GetNearbyAttractionsByObjectIdAsync(int objectId)`
>    (najświeższy set po `LastRefreshedUtc`), prywatne `MapToDto`.
> 4. Rejestracja `AddDbContextFactory<RappNearbyAttractionsDbContext>(o => o.UseNpgsql(rentoomAppConnectionString))`
>    + `AddScoped<ApartmentNearbyAttractionsService>()` w OBU `Program.cs` (Web i Api), obok SocialMedia.
> 5. Web: w `ApartmentPage.razor.cs` wstrzyknij serwis, pobierz po `_apartment.Items[0].Id.Value`, wyrenderuj
>    listę pogrupowaną po `Category` (nazwa + link `GoogleMapsUri` + odległość/czas).
> 6. Api: `Api\Integrations\RentoomApp\NearbyAttractionsApi.cs` — `[Function]` z
>    `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="apartments/{objectId:int}/nearby-attractions")]`,
>    woła `GetNearbyAttractionsByObjectIdAsync`, serializuje `JsonConvert.SerializeObject`, `Content-Type
>    application/json; charset=utf-8`, walidacja `objectId<=0 → BadRequest`, `try/catch → 500`. Zwracaj zawsze
>    `NearbyAttractionsResultDTO` (bez 404).
> 7. StayWell: w `BackendApi.cs` dodaj `GetNearbyAttractionsAsync(int objectId)` na kliencie `"FunctionsApi"`
>    (`GetFromJsonAsync<NearbyAttractionsResultDTO>($"apartments/{objectId}/nearby-attractions", _json)`).
>
> **Kryteria akceptacji:** solution się kompiluje; endpoint zwraca JSON w kształcie z sekcji „Przykładowy
> payload”; strona apartamentu pokazuje listę atrakcji, gdy status = `ok` i `Items` niepuste; brak zapisów do
> bazy (tylko odczyt); żadnych migracji w tym repo.
