# Dokumentacja Techniczna Systemu Lokalizacji i Routingu (Runtime .NET 8)

## 1. Architektura Systemu
System opiera się na architekturze **Pure Runtime**, co oznacza, że trasy nie są generowane podczas budowania aplikacji, lecz rozwiązywane dynamicznie w momencie żądania. Gwarantuje to wysoką elastyczność przy wsparciu 33 języków bez długu technicznego w postaci generowanego kodu.

### Kluczowe komponenty:
1. **Słowniki (.resx):** Pliki `PageRoutes.resx` stanowią jedyne źródło prawdy dla zlokalizowanych slugów (np. `Apartments` -> `apartamenty`).
2. **Rejestr (PageMapping.cs):** Statyczna mapa wiążąca klucze logiczne stron z typami komponentów Blazor i ich szablonami parametrów.
3. **Router (LocalizedRouter.razor):** Centralny punkt procesujący żądania UI, odpowiedzialny za dynamiczne renderowanie i ekstrakcję danych z URL.

---

## 2. Warstwa Middleware (Potok przetwarzania)

### Program.cs
Konfiguracja potoku żądań (Pipeline) wymusza ścisłą kolejność:
- **UseStaticFiles:** Obsługa plików fizycznych.
- **UseRouting:** Inicjalizacja silnika routingu (musi nastąpić po plikach statycznych, aby uniknąć przechwytywania assetów przez router Blazora).
- **UseMiddleware<LocalizedRoutingMiddleware>:** Ustawienie kultury i synchronizacja stanu.
- **UseRequestLocalization:** Standardowy mechanizm .NET ustawiający kulturę wątku.

### LocalizedRoutingMiddleware.cs
Odpowiada za dwa krytyczne procesy:
1. **Detekcja z URL:** Odczytuje prefiks językowy (np. `/pl/`) i ustawia `CultureInfo`.
2. **Enterprise Cookie Sync:** Synchronizuje ciasteczko `.AspNetCore.Culture` z adresem URL. Jest to niezbędne dla stabilności połączeń SignalR (WebSocket), które nie posiadają prefiksu językowego w nagłówkach i polegają na ciasteczku w celu utrzymania poprawnej kultury po stronie serwera.

---

## 3. Warstwa Serwisowa

### RouteLocalizationService.cs
Implementuje logikę biznesową tłumaczenia adresów:
- **TryGetPageKeyFromSlug:** Iteruje po zasobach `.resx` w celu dopasowania sluga (np. "apartamenty") do klucza technicznego ("Apartments"). Posiada wbudowany mechanizm fallbacku do języka polskiego.
- **TryFindPageKeyAnyCulture:** Wyszukuje klucz strony na podstawie sluga w dowolnej ze wspieranych kultur, sprawdzając również zlokalizowane aliasy historyczne/alternatywne (zdefiniowane centralnie w kodzie serwisu, np. `"polozenie-torunia"`). Pozwala to na eliminację twardego kodowania slugów w komponentach UI (takich jak przełącznik języków czy moduł hreflangów).
- **GetLocalizedUrl:** Generuje pełne adresy URL z prefiksami na podstawie klucza i opcjonalnej kultury.

### LocalizedUrlBuilder.cs
Narzędzie typu Fluent API zapewniające type-safe budowanie adresów URL z wieloma parametrami, gwarantując ich poprawną kolejność zgodnie z szablonem zdefiniowanym w `PageMapping`.

---

## 4. Mechanizm Routingu (LocalizedRouter.razor)

Router zastępuje standardowy mechanizm `@page` i procesuje trasy `/{cultureCode}/{*routeSegments}`.

### Proces rozwiązywania trasy:
1. **Dopasowanie klucza:** Wykorzystuje `RouteLocalizationService` do identyfikacji strony.
2. **Parsowanie szablonu (MapParametersFromTemplate):** Pobiera definicję trasy z `PageMapping` (np. `{Id}/{Slug}`). Rozbija segmenty URL i przypisuje je do parametrów na podstawie nazwy. Obsługuje automatyczną konwersję typu `int` dla parametrów o nazwie `Id`.
3. **Inteligentna Iniekcja (Refleksja):** Router za pomocą refleksji sprawdza publiczne właściwości (`[Parameter]`) docelowego komponentu. Wstrzykuje tylko te parametry, które istnieją w kodzie C#, co zapobiega błędom `InvalidOperationException` przy dodatkowych parametrach QueryString.
4. **Obsługa Dual-Support:** System równolegle parsuje segmenty ścieżki (Pretty URLs) oraz QueryString. Parametry z URL mają priorytet, a dane z QueryString stanowią uzupełnienie (fallback).
5. **Renderowanie:** Wykorzystuje `<DynamicComponent />` do załadowania właściwej strony z przekazanym słownikiem parametrów.

---

## 5. SEO i Internationalization

### 5.1 SeoHreflangs.razor
Komponent wstrzyknięty centralnie w `App.razor`. Dla każdego żądania generuje komplet 33 tagów `<link rel="alternate" hreflang="..." />`. Dzięki architekturze Runtime, system potrafi wygenerować zlokalizowane linki (np. zamiana `apartamenty` na `apartments`) dla dowolnie głębokich adresów z parametrami.

### 5.2 Dynamiczne Breadcrumbs (BreadcrumbJsonLd.razor)
Komponent generuje strukturę JSON-LD `BreadcrumbList` dla wyszukiwarek:
* **Dynamiczna Strona Główna**: Zamiast twardo kodować `/` dla pierwszego elementu ("Strona główna"), system dynamicznie odpytuje `RouteService.GetLocalizedUrl("Home")`. Zapobiega to przekierowaniom 302 z `/` do `/pl` lub innego języka domyślnego, co chroni przed błędami przekierowania w Google Search Console.
* **Bezpieczeństwo Ścieżek Absolutnych (Fix macOS/Linux)**: Metoda pomocnicza `ToAbsoluteUrl` weryfikuje poprawność schematów sieciowych. W systemach Unixowych (np. macOS, Linux) ścieżki zaczynające się od `/` (np. `/bs`) są rozpoznawane przez `Uri.TryCreate` jako absolutne ścieżki do plików w systemie operacyjnym (schemat `file:///`). Zabezpieczono to warunkiem, że wykryty URI musi mieć schemat `http` lub `https`.
* **Zachowanie znaku `@` w Serializacji**: Serializator C# usuwa znak `@` z nazw właściwości obiektów anonimowych (np. `@context` staje się `"context"`). Aby zachować zgodność ze specyfikacją Schema.org, komponent tworzy metadane przy użyciu słownika `Dictionary<string, object>`, co pozwala na zachowanie kluczy `"@context"`, `"@type"` oraz `"@id"`.

### 5.3 Spójność Hreflang w Sitemap (SitemapController.cs)
Aby wyeliminować ostrzeżenia Google Search Console o niedopasowaniu hreflangów, kody języków w `SitemapController.cs` zostały ujednolicone z kodami renderowanymi w HTML. Sitemap generuje dwuliterowe kody języków ISO (np. `pl`, `en`, `cs` zamiast pełnych nazw kultur `pl-PL`, `en-US`, `cs-CZ`) poprzez operację podziału ciągu: `cult.Split('-')[0].ToLowerInvariant()`.

### 5.4 Ograniczenie Indeksowania w robots.txt (Program.cs)
W środowisku produkcyjnym endpoint `/robots.txt` dynamicznie wyklucza z indeksowania ścieżki transakcyjne oraz testowe bramki płatności:
```txt
Disallow: /rezerwuj/
Disallow: /tpay-mock/
```
W środowiskach deweloperskich (`!Environment.IsProduction()`) robots.txt wyklucza całą domenę (`Disallow: /`), dodatkowo wstrzykując tag `<meta name="robots" content="noindex, nofollow..." />` w `App.razor`.

---

## 6. Procedura dodawania nowej podstrony
1. **PageMapping.cs:** Dodać wpis do `KeyToComponent` definiując klucz, typ klasy oraz opcjonalny szablon parametrów (np. `"{Id}/{Slug}"`).
2. **PageRoutes.resx:** Dodać klucz i jego tłumaczenie (slug) w odpowiednich plikach językowych.
3. **Komponent:** Upewnić się, że właściwości oznaczone jako `[Parameter]` odpowiadają nazwom użytym w szablonie trasy.

---

## 7. Przyszłe Rozszerzenia (Deep Nested Routing)
Architektura została zaprojektowana z myślą o łatwym rozszerzaniu o głęboko zagnieżdżone trasy (np. system blogowy: `/blog/nazwa-posta/komentarze/strona/2`).

### Strategia "Zlokalizowanych Stałych" (Localized Constants):
Jeśli w przyszłości zajdzie potrzeba obsługi linków, w których środkowe segmenty również muszą być tłumaczone (np. słowo `komentarze` -> `comments`), należy:

1.  **Słownik (.resx):** Dodać techniczne słowo kluczowe (np. `CommentsKey`) i jego tłumaczenia we wszystkich językach.
2.  **Szablon (PageMapping.cs):** Zastosować specjalną notację dla stałych fragmentów, np. używając wykrzyknika:
    `["BlogComments"] = new(typeof(BlogPage), "{Slug}/!CommentsKey/{PageNumber}")`
3.  **Rozszerzenie Routera:** Zmodyfikować metodę `MapParametersFromTemplate` w `LocalizedRouter.razor`, aby rozpoznawała prefiks `!`. Zamiast przypisywać segment do parametru, router powinien:
    *   Pobrać zlokalizowany slug dla klucza `CommentsKey`.
    *   Porównać go z bieżącym segmentem w URL.
    *   Zwrócić 404, jeśli segment w URL nie pasuje do tłumaczenia w aktualnym języku.

Dzięki temu system pozostanie generyczny i umożliwi tworzenie nieskończenie złożonych, w pełni zlokalizowanych struktur URL bez zmiany podstawowej architektury.

---

## 8. Obsługa znaków narodowych (non-ASCII) i zapobieganie Double-Encoding

System w pełni wspiera znaki narodowe (np. polskie znaki diakrytyczne `ą, ć, ń, ż`, cyrylicę, znaki greckie) w strukturach adresów URL. Ze względu na specyfikację protokołu HTTP oraz serwera Kestrel, wdrożono dedykowane zabezpieczenia w warstwie routingu i przekierowań:

### Zabezpieczenie nagłówka Location (Program.cs)
Serwer Kestrel zabrania umieszczania surowych znaków nie-ASCII (np. greckiego `Ο` - `0x039F`) w nagłówkach odpowiedzi HTTP, co przy bezpośrednim przekierowaniu przez `Results.LocalRedirect` wywoływało błąd `InvalidOperationException`.
- **Rozwiązanie:** W `/culture/set` wdrożono helper `EscapeRelativeUrl`, który automatycznie koduje ścieżkę (path) i parametry (query string) do bezpiecznego formatu ASCII (percent-encoding, np. `%CE%9F`), zachowując przy tym prawidłową strukturę adresową i ukośniki `/`.

### Rozwiązanie problemu Double-Encoding (ChangeGlobal.razor / SeoHreflangs.razor)
Metoda Blazora `Navigation.ToBaseRelativePath` zwraca relatywną ścieżkę w postaci zakodowanej (URL-encoded), podczas gdy słowniki `.resx` przechowują slugi odkodowane. Różnica ta uniemożliwiała poprawne dopasowanie klucza strony i prowadziła do awaryjnego podwójnego zakodowania adresu (np. `%25D0` zamiast `%D0`), co skutkowało błędami 404.
- **Rozwiązanie:** W komponentach `ChangeGlobal.razor` oraz `SeoHreflangs.razor` ścieżka wejściowa jest odkodowywana przed jakąkolwiek analizą za pomocą `Uri.UnescapeDataString(Navigation.ToBaseRelativePath(...))`. Zapewnia to poprawne porównanie ciągów tekstowych z bazą zasobów oraz generowanie pojedynczo zakodowanych, prawidłowych adresów URL.
