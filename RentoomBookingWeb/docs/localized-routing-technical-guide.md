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

### SeoHreflangs.razor
Komponent wstrzyknięty centralnie w `App.razor`. Dla każdego żądania generuje komplet 33 tagów `<link rel="alternate" hreflang="..." />`. Dzięki architekturze Runtime, potrafi wygenerować zlokalizowane linki (np. zamiana `apartamenty` na `apartments`) dla dowolnie głębokich adresów z parametrami.

---

## 6. Procedura dodawania nowej podstrony
1. **PageMapping.cs:** Dodać wpis do `KeyToComponent` definiując klucz, typ klasy oraz opcjonalny szablon parametrów (np. `"{Id}/{Slug}"`).
2. **PageRoutes.resx:** Dodać klucz i jego tłumaczenie (slug) w odpowiednich plikach językowych.
3. **Komponent:** Upewnić się, że właściwości oznaczone jako `[Parameter]` odpowiadają nazwom użytym w szablonie trasy.
