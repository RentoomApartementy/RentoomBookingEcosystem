# System Wielojęzyczności i Zlokalizowanego Routingu - Pełna Dokumentacja Techniczna

## 1. Wstęp i Architektura
System został zaprojektowany, aby przekształcić Rentoom Booking w globalną platformę wspierającą **33 języki**. Wykorzystuje on natywne mechanizmy Blazor Server, zintegrowane z potokiem lokalizacji ASP.NET Core oraz autorskim systemem automatycznego generowania tras SEO.

### Kluczowe cechy:
- **Prefixy językowe:** Każdy język posiada swój 2-literowy kod w adresie URL (np. `/en/`, `/it/`, `/pl/`).
- **Zlokalizowane Slugi:** Adresy URL są tłumaczone (np. `/pl/wspolpraca` vs `/en/cooperation`).
- **International SEO:** Pełne wsparcie dla tagów `hreflang` i zintegrowany system sitemap.

---

## 2. Rozpoznawanie Języka (Culture Detection)
Logika znajduje się w `Program.cs` i `LocalizedRoutingMiddleware.cs`. System wykorzystuje natywny potok lokalizacji ASP.NET Core, oparty na wbudowanych mechanizmach `IRequestCultureProvider`, zachowując pełną zgodność z architekturą platformy .NET.

Priorytety detekcji:
1.  **URL Segment (Highest):** Autorski `CustomRequestCultureProvider` sprawdza pierwszy segment ścieżki. Jeśli wykryje `/it/`, wymusza język włoski dla całego żądania, gwarantując, że zawartość odpowiada adresowi URL (kluczowe dla SEO).
2.  **Cookie:** Standardowy `CookieRequestCultureProvider` sprawdza ciasteczko `.AspNetCore.Culture`. Jest to mechanizm wymagany przez Blazor Server do utrzymania stanu wybranego języka w ramach aktywnego połączenia WebSocket (SignalR).
3.  **Bot Detection:** Automatyczna detekcja robotów indeksujących (Google, Bing). Wejścia na domenę główną bez prefiksu serwują wersję `pl-PL`, zapewniając bazę do indeksacji.
4.  **Browser Settings:** Fallback do nagłówka `Accept-Language` przeglądarki.
5.  **Global Fallback:** Jeśli żaden z powyższych warunków nie jest spełniony lub brakuje tłumaczenia, aplikacja domyślnie korzysta z języka angielskiego (`en-US`), zdefiniowanego w `supported-languages.json`.

**Synchronizacja Sesji:** `LocalizedRoutingMiddleware` dba o to, by przy każdym żądaniu z prefiksem językowym ciasteczko `.AspNetCore.Culture` zostało zaktualizowane. Zapewnia to spójność między tym, co widzi przeglądarka (URL), a tym, co pamięta serwer (Cookie).

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

## 5. Międzynarodowe SEO i System Hreflang
System został zaprojektowany z myślą o globalnym pozycjonowaniu (International SEO), co wymaga precyzyjnego zarządzania mapami witryn oraz tagami w sekcji `<head>`.

### Struktura Sitemap (Sitemap Index):
1.  **`/sitemap.xml` (Indeks):** Główny plik wskazujący na 33 pod-mapy: `/pl/sitemap.xml`, `/en/sitemap.xml`, itd.
2.  **`/{culture}/sitemap.xml` (Mapy Językowe):** Dynamicznie generowane pliki XML zawierające linki dla konkretnego kraju. Linki wewnątrz tych map zawierają wbudowane atrybuty `hreflang` do cross-linkowania.

### Dynamiczne tagi Hreflang w HTML (`SeoHreflangs.razor`):
Aby zapobiec problemom z "Duplicate Content" i pomóc wyszukiwarkom w serwowaniu odpowiedniej wersji językowej, system implementuje tagi `<link rel="alternate" hreflang="..." />` w sekcji `<head>` każdej podstrony.

- **Implementacja:** Za renderowanie odpowiada komponent `SeoHreflangs.razor`. Aby zagwarantować najwyższy priorytet i uniknąć nadpisywania przez bloki `<HeadContent>` na poszczególnych podstronach, komponent ten został zaimplementowany centralnie w pliku `App.razor`.
- **Obsługa Deep Linków:** Komponent dynamicznie mapuje pełne adresy URL (włącznie z parametrami QueryString i identyfikatorami zasobów, np. `/pl/apartamenty/123/nazwa`), zapewniając, że tagi `hreflang` zawsze prowadzą do dokładnego odpowiednika strony w innym języku.

### Logika podwójnego Fallbacku (x-default vs System Default):
System obsługuje dwa niezależne mechanizmy "języka domyślnego", dostosowane do wymagań wyszukiwarek i użytkowników:
1.  **System Fallback (`en-US`):** Zdefiniowany w `supported-languages.json`. Używany przez aplikację, gdy brakuje tłumaczenia w plikach `.resx`. Zapewnia to, że interfejs użytkownika pozostanie zrozumiały (po angielsku) zamiast wyświetlać puste klucze.
2.  **SEO Fallback (`x-default`):** W komponencie `SeoHreflangs.razor` tag `x-default` jest na sztywno przypisany do wersji polskiej (`pl-PL`). Informuje to roboty indeksujące (np. Googlebot), że w przypadku braku dedykowanej wersji dla danego regionu, globalnym "oryginałem" serwisu jest strona polska. Rozdzielenie tych logik pozwala chronić pozycję macierzystego rynku w wynikach wyszukiwania, nie psując doświadczenia użytkowników z innych krajów.

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

### Social Media Pretty URLs (New Feature)
The system now supports clean, sharable URLs for the apartment listing page with pre-selected dates and guests. This is specifically designed for social media campaigns.

- **Pattern:** `/{culture}/{slug}/{StartDate}/{EndDate}/{Adults?}/{Children?}`
- **Example:** `/pl/apartamenty/2026-06-01/2026-06-15/2`
- **Technical Logic:** 
    - The `Apartments.razor` component detects these route parameters and automatically triggers a search.
    - SEO metadata (Open Graph) dynamically updates its title and description to include the selected dates, providing a rich preview in Facebook, Messenger, and Instagram.

---

## 7. Rozwiązywanie Problemów
- **Błąd "Could not find createRipple":** Sprawdź, czy import w `Button.razor` używa ścieżki bezwzględnej `/js/buttonHelper.js`.
- **Język nie zmienia się po kliknięciu:** Upewnij się, że `LocalizedRoutingMiddleware` jest zarejestrowany w `Program.cs` **przed** `app.UseRequestLocalization()`.
- **Sitemapa nie pokazuje zmian:** Wyczyść cache serwera i upewnij się, że `route_generator.py` został uruchomiony poprawnie (sprawdź zawartość `LocalizedRouteRegistry.cs`).

---

## 8. Planowana Migracja: C# Route Generator (BuildTool)
Zgodnie z decyzją architektoniczną, obecny skrypt Python zostanie zastąpiony natywnym narzędziem .NET, aby ujednolicić stos technologiczny i poprawić skalowalność.

### Architektura Docelowa:
1.  **Projekt:** `Tools/RouteGenerator` (Aplikacja konsolowa .NET 8).
2.  **Konfiguracja:** `RentoomBookingWeb/routes-config.json` – zawiera mapowania stron i ścieżki do komponentów.
3.  **Source of Truth:** Narzędzie będzie pobierać listę aktywnych języków bezpośrednio z `SharedFrontend/Localization/supported-languages.json`.
4.  **Automatyzacja:** Wykorzystanie MSBuild `PreBuildEvent` w projekcie Web, co zapewni automatyczne generowanie tras przy każdej kompilacji (lokalnie oraz na CI/CD).

### Korzyści:
- Brak zależności od Pythona w środowisku deweloperskim i CI.
- Pełna integracja z `$(MSBuildThisFileDirectory)`, eliminująca problemy ze ścieżkami relatywnymi.
- Możliwość łatwego debugowania generatora w Visual Studio/Rider.
- Walidacja poprawności routingu na etapie kompilacji (Build Fail przy błędach konfiguracji).
