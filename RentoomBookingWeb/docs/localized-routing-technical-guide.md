# Techniczna Dokumentacja Systemu Lokalizacji i SEO (Localized Routing)

Ten dokument opisuje architekturę, mechanizmy automatyzacji oraz logikę biznesową systemu lokalizacji opartego na prefiksach URL (np. `/en/apartments`) w projekcie RentoomBookingWeb.

---

## 1. Architektura Systemu

System opiera się na trzech filarach:
1.  **Native Routing (Blazor)**: Każda strona posiada fizycznie zdefiniowane trasy `@page` dla wszystkich 33 obsługiwanych języków.
2.  **Automatyzacja (Python)**: Skrypt generujący trasy na podstawie plików `.resx`, eliminujący ryzyko ludzkiego błędu.
3.  **Middleware (ASP.NET Core)**: Odpowiada za detekcję języka z adresu URL, ustawienie kultury wątku oraz synchronizację z ciasteczkiem `.AspNetCore.Culture`.

---

## 2. Analiza Komponentów (Plik po Pliku)

### 2.1. Skrypt Generujący: `route_generator.py`
**Lokalizacja:** `RentoomBookingWeb/Services/Localization/route_generator.py`

Jest to "mózg" systemu. Skrypt wykonuje następujące zadania:
*   **Skanowanie Zasobów**: Przeszukuje katalog `Resources` i mapuje klucze (np. `ApartmentsText`) na przetłumaczone wartości we wszystkich 33 językach.
*   **Slugyfikacja**: Przekształca nazwy (np. "O Toruniu") na bezpieczne URL-e (`o-toruniu`) przy użyciu normalizacji NFKD (usuwanie polskich znaków, diakrytyków, zamiana spacji na myślniki).
*   **Obsługa Ambiguity (Niejednoznaczności)**: Jeśli dla danego języka brakuje tłumaczenia nazwy strony, skrypt automatycznie używa polskiego odpowiednika (np. `/be/apartamenty`). Zapobiega to sytuacji, w której wiele stron miałoby tę samą trasę (np. samo `/be`), co powodowałoby błąd Blazora.
*   **Patchowanie Razor**: Wyszukuje markery `@* [SEO_ROUTES] *@` w plikach `.razor` i wstrzykuje tam aktualną listę tras.
*   **Generowanie Rejestru**: Tworzy plik `LocalizedRouteRegistry.cs`.

### 2.2. Rejestr Tras: `LocalizedRouteRegistry.cs`
**Lokalizacja:** `RentoomBookingWeb/Services/Localization/LocalizedRouteRegistry.cs`

Statyczna klasa C# zawierająca słownik `PageSlugs`. Jest to "mapa drogowa" aplikacji, która pozwala systemowi dowiedzieć się, że np. strona o kluczu `"Apartments"` w języku angielskim to `"apartments"`, a w niemieckim `"wohnungen"`.

### 2.3. Middleware: `LocalizedRoutingMiddleware.cs`
**Lokalizacja:** `RentoomBookingWeb/Services/Localization/LocalizedRoutingMiddleware.cs`

Middleware uruchamiany na samym początku potoku (pipeline) w `Program.cs`.
*   **Priorytet URL**: Sprawdza pierwszy segment ścieżki. Jeśli znajdzie kod języka (np. `en`, `pl`, `de`), natychmiast ustawia `CultureInfo.CurrentCulture`.
*   **Synchronizacja Ciasteczka**: Po wykryciu języka z URL, aktualizuje ciasteczko `.AspNetCore.Culture`. Dzięki temu, jeśli użytkownik przejdzie na stronę bez prefiksu, system zapamięta jego ostatni wybór.
*   **Ochrona Plików**: Posiada logikę pomijania ścieżek zawierających kropki (pliki statyczne) oraz ścieżek technicznych (`_blazor`, `api`), co zapobiega błędom 404/405 dla zasobów.

### 2.4. Serwis: `RouteLocalizationService.cs`
**Lokalizacja:** `RentoomBookingWeb/Services/Localization/RouteLocalizationService.cs`

Serwis wstrzykiwany do komponentów (`@inject IRouteLocalizationService`).
*   **`GetLocalizedUrl(pageKey, culture)`**: Zwraca pełny URL dla danej strony w wybranym języku. Używany do budowania linków w menu i tagów hreflang.
*   **`TryGetPageKeyFromSlug(slug, culture)`**: Odwraca proces – na podstawie sluga z paska adresu mówi systemowi, na której stronie (kluczu) aktualnie znajduje się użytkownik. Kluczowe dla poprawnego działania przełącznika języków.

---

## 3. Komponenty UI

### 3.1. Przełącznik Języków: `ChangeGlobal.razor`
Logika przełączania:
1. Pobiera aktualny slug z adresu URL.
2. Pyta `RouteLocalizationService`, jakiej stronie odpowiada ten slug.
3. Jeśli znajdzie stronę (np. "Apartments"), generuje nowy URL w docelowym języku (np. `/de/wohnungen`).
4. Wykonuje `Navigation.NavigateTo(..., forceLoad: true)`, aby całkowicie przeładować stan aplikacji pod nową kulturę.

### 3.2. Tagi SEO: `SeoHreflangs.razor`
Komponent umieszczony w sekcji `<head>`. Dla każdej podstrony generuje 33 tagi `<link rel="alternate" hreflang="..." />`. Jest to krytyczne dla Google, aby poprawnie indeksować każdą wersję językową osobno.

---

## 4. Utrzymanie (How-to)

### Jak dodać nową stronę do systemu lokalizacji?
1.  Stwórz komponent w katalogu `Pages`.
2.  Dodaj na górze pliku markery:
    ```razor
    @page "/twoja-domyslna-trasa"
    @* [SEO_ROUTES] *@
    @* [SEO_ROUTES_END] *@
    ```
3.  Otwórz `route_generator.py` i dodaj nową stronę do słownika `PAGE_MAPPINGS`. Musisz podać `file_prefix` (nazwa zasobu .resx) oraz `res_key` (klucz z zasobu, który ma służyć jako nazwa w URL).
4.  Uruchom skrypt: `python3 route_generator.py`.

### Jak dodać nowy język?
1.  Dodaj nowy plik `.resx` w katalogu `Resources` (np. `HomePage.fr.resx`).
2.  Uruchom skrypt `route_generator.py`. System automatycznie wykryje nowy kod języka (`fr`) i doda trasy do wszystkich komponentów.

---

## 5. Rozwiązywanie problemów

*   **Błąd "Ambiguous routes"**: Oznacza, że dwie różne strony mają tę samą trasę. Skrypt `route_generator.py` zapobiega temu poprzez fallback do polskich nazw, jeśli tłumaczenie jest puste.
*   **Strona zwraca 404 po przełączeniu języka**: Sprawdź, czy w `LocalizedRouteRegistry.cs` znajduje się poprawny slug dla tego języka i czy plik `.razor` został poprawnie spatchowany przez skrypt.
*   **Zasoby (obrazy/CSS) nie ładują się**: Upewnij się, że w `LocalizedRoutingMiddleware.cs` ścieżka do zasobu nie jest błędnie interpretowana jako kod języka. Logika `path.Contains('.')` powinna to chronić.
