# System Wielojęzyczności i Zlokalizowanego Routingu - Pełna Dokumentacja Techniczna

## 1. Wstęp i Architektura
System został zaprojektowany, aby przekształcić Rentoom Booking w globalną platformę wspierającą **33 języki**. Wykorzystuje on natywne mechanizmy Blazor Server, zintegrowane z potokiem lokalizacji ASP.NET Core oraz autorskim systemem automatycznego generowania tras SEO.

### Kluczowe cechy:
- **Prefixy językowe:** Każdy język posiada swój 2-literowy kod w adresie URL (np. `/en/`, `/it/`, `/pl/`).
- **Zlokalizowane Slugi:** Adresy URL są tłumaczone (np. `/pl/wspolpraca` vs `/en/cooperation`).
- **International SEO:** Pełne wsparcie dla tagów `hreflang` i zintegrowany system sitemap.

---

## 2. Rozpoznawanie Języka (Culture Detection)
Logika znajduje się w `Program.cs` i `LocalizedRoutingMiddleware.cs`. System używa 4 poziomów detekcji o następujących priorytetach:

1.  **URL Segment (Highest):** Specjalny `CustomRequestCultureProvider` sprawdza pierwszy segment ścieżki. Jeśli wykryje `/it/`, wymusza język włoski dla całego żądania, ignorując inne ustawienia.
2.  **Cookie:** Standardowe ciasteczko `.AspNetCore.Culture` przechowuje ostatni wybór użytkownika.
3.  **Bot Detection:** System automatycznie wykrywa roboty indeksujące (Google, Bing). Jeśli wejdą na domenę główną bez prefiksu, otrzymują wersję `pl-PL`, co zapewnia stabilną bazę do indeksacji.
4.  **Browser Settings:** Fallback do ustawień przeglądarki użytkownika.

**Synchronizacja Sesji:** `LocalizedRoutingMiddleware` dba o to, by przy każdym żądaniu z prefiksem językowym ciasteczko `.AspNetCore.Culture` zostało zaktualizowane. Jest to krytyczne dla Blazor Server, aby nawiązane połączenie WebSocket (Circuit) wiedziało, w jakim języku renderować komponenty.

---

## 3. Generator Tras (route_generator.py)
Ponieważ Blazor wymaga statycznych dyrektyw `@page`, proces ten jest zautomatyzowany skryptem Python (`RentoomBookingWeb/Services/Localization/route_generator.py`).

### Logika "Hierarchicznego Fallbacku":
Podczas generowania adresów dla 33 języków, skrypt stosuje bezpieczną kaskadę:
1.  Pobierz tłumaczenie z pliku `.resx` (np. `CooperationText`).
2.  Jeśli brak tłumaczenia -> użyj wersji polskiej.
3.  Jeśli wersja polska jest pusta -> użyj klucza technicznego strony (np. `Cooperation`).
*Dzięki temu unikamy konfliktów (każda strona ma unikalny adres URL) i zapobiegamy błędom "Ambiguous Route" w Blazorze.*

### Mapowanie polskich znaków:
Funkcja `to_slug` posiada wbudowaną mapę znaków diakrytycznych. Litery takie jak `ł` są zamieniane na `l`, a `ą` na `a` (zamiast myślników). Zapewnia to czyste, profesjonalne adresy URL (np. `/pl/wspolpraca` zamiast `/pl/wspo-praca`).

---

## 4. Nawigacja i Rejestr (IRouteLocalizationService)
Centralnym punktem nawigacji jest `IRouteLocalizationService`. 

### LocalizedRouteRegistry.cs
Ten plik jest generowany automatycznie przez skrypt Python. Zawiera on gigantyczną mapę `Dictionary<string, Dictionary<string, string>>`, która przechowuje wszystkie możliwe kombinacje `KluczStrony -> Język -> Slug`.

### Zasady nawigacji:
W komponentach **nigdy** nie używamy twardych ścieżek. 
- **Źle:** `<a href="/kontakt">`
- **Dobrze:** `<a href="@RouteService.GetLocalizedUrl("Contact")">`

Usługa automatycznie wykryje bieżący język, pobierze odpowiedni slug z rejestru i doklei prefiks językowy.

---

## 5. Międzynarodowe SEO i System Sitemap
System map witryny został przebudowany na strukturę **Sitemap Index**, co jest standardem dla dużych serwisów.

### Struktura:
1.  **`/sitemap.xml` (Indeks):** Główny plik wskazujący na 33 pod-mapy: `/pl/sitemap.xml`, `/en/sitemap.xml`, itd.
2.  **`/{culture}/sitemap.xml` (Mapy Językowe):** Dynamicznie generowane pliki XML zawierające linki dla konkretnego kraju.

### Wsparcie Hreflang (Cross-Linking):
Każdy link w sitemapie językowej (np. do apartamentu po włosku) posiada zestaw tagów `<xhtml:link rel="alternate" hreflang="..." href="..." />`. 
Informują one Google: "Strona włoska to odpowiednik strony polskiej pod adresem X". 
**Efekt:** Google Search Console poprawnie łączy wersje językowe, eliminuje duplikaty i wyświetla użytkownikom linki w ich ojczystym języku.

---

## 6. Wytyczne techniczne dla programistów

### Ścieżki do zasobów (CSS/JS/Obrazy)
Wprowadzenie wielopoziomowych URL-i (np. `/pl/apartamenty/123/nazwa`) wymusza stosowanie **ścieżek bezwzględnych**.
- **Zawsze zaczynaj od `/`**: `<script src="/js/script.js">` zamiast `<script src="js/script.js">`.
- Bez `/` na początku, przeglądarka będzie szukać skryptu relatywnie do aktualnej podstrony (np. w `/pl/apartamenty/js/...`), co zakończy się błędem 404.

### Dodawanie nowej podstrony do systemu:
1.  Stwórz plik `.razor` i dodaj znaczniki routingu SEO:
    ```razor
    @page "/trasa-bazowa"
    @* [SEO_ROUTES] *@
    @* [SEO_ROUTES_END] *@
    ```
2.  W `route_generator.py` dodaj nową stronę do słownika `PAGE_MAPPINGS`.
3.  Upewnij się, że w plikach `.resx` (np. `SharedResources.resx`) istnieje klucz użyty w konfiguracji.
4.  Uruchom generator: `python3 route_generator.py`.
5.  Przebuduj projekt: `dotnet build`.

### Dynamiczne parametry (np. ApartmentPage)
Przełącznik języków (`ChangeGlobal.razor`) został zaprogramowany tak, aby obsługiwać złożone adresy. Przy zmianie języka:
1.  Identyfikuje klucz strony na podstawie bieżącego sluga.
2.  Podmienia główny slug na wersję w nowym języku.
3.  **Zachowuje pozostałe segmenty URL** (takie jak ID apartamentu czy daty rezerwacji) oraz parametry QueryString.

---

## 7. Rozwiązywanie Problemów
- **Błąd "Could not find createRipple":** Sprawdź, czy import w `Button.razor` używa ścieżki bezwzględnej `/js/buttonHelper.js`.
- **Język nie zmienia się po kliknięciu:** Upewnij się, że `LocalizedRoutingMiddleware` jest zarejestrowany w `Program.cs` **przed** `app.UseRequestLocalization()`.
- **Sitemapa nie pokazuje zmian:** Wyczyść cache serwera i upewnij się, że `route_generator.py` został uruchomiony poprawnie (sprawdź zawartość `LocalizedRouteRegistry.cs`).
