# System Zlokalizowanego Routingu - Pełna Dokumentacja Techniczna

## 1. Architektura Systemu
System opiera się na natywnym routingu Blazor, wspieranym przez zewnętrzny generator tras oraz niestandardowy potok detekcji kultury w ASP.NET Core. Obsługuje 33 języki z unikalnymi prefiksami URL (np. `/en/`, `/it/`) oraz zlokalizowanymi slugami.

---

## 2. Detekcja Kultury (Culture Pipeline)
Logika detekcji znajduje się w `Program.cs` i działa jako zestaw dostawców (`RequestCultureProviders`) o określonym priorytecie:

### A. URL Prefix Provider (Priorytet najwyższy)
Jest to `CustomRequestCultureProvider`, który analizuje pierwszy segment ścieżki (np. `parts[0]`).
- Jeśli segment pasuje do wspieranego języka (np. `it` lub `it-IT`), wymusza tę kulturę dla całego żądania.
- Dzięki temu wejście na `/it/apartamenty-torun` zawsze ustawi język włoski, ignorując ciasteczka czy ustawienia przeglądarki.

### B. Cookie Provider
Standardowy `CookieRequestCultureProvider`. Przechowuje preferencje użytkownika w ciasteczku `.AspNetCore.Culture`.

### C. Bot Detection
Dostawca wykrywający roboty (Googlebot itp.). Jeśli robot wejdzie na stronę bez prefiksu, system wymusza kulturę polską (`pl-PL`), aby uniknąć indeksowania wersji "pustych" lub mieszanych.

### D. Accept-Language Header
Ostatni etap – dopasowanie języka do ustawień przeglądarki użytkownika.

---

## 3. Automatyzacja Tras (route_generator.py)
Blazor wymaga statycznych dyrektyw `@page` w plikach `.razor`. Aby nie wpisywać ręcznie 33 tras dla każdej strony, używamy skryptu w Pythonie.

### Proces generowania:
1.  **Ekstrakcja:** Skrypt czyta pliki `.resx` z folderu `/Resources`. Szuka kluczy zdefiniowanych w `PAGE_MAPPINGS` (np. `Home_PageTitle`).
2.  **Slugyfikacja:** Tekst z zasobów jest czyszczony z polskich znaków, zamieniany na małe litery i łączony myślnikami (np. "Wszystkie Apartamenty" -> "wszystkie-apartamenty").
3.  **Hierarchia Fallbacków (Kluczowe dla SEO):**
    - Próba pobrania przetłumaczonego sluga.
    - Jeśli brak -> pobranie polskiego sluga.
    - Jeśli brak -> użycie klucza strony (np. `AllApartments`).
    *To zapobiega konfliktom tras (każda strona musi mieć unikalny adres).*
4.  **Registry:** Tworzony jest plik `LocalizedRouteRegistry.cs`, który zawiera mapę `Język -> Slug`. Służy on do generowania linków wewnątrz aplikacji.
5.  **Patching:** Skrypt wstrzykuje dyrektywy `@page` do plików `.razor` pomiędzy znaczniki `@* [SEO_ROUTES] *@`.

---

## 4. Nawigacja Wewnętrzna (IRouteLocalizationService)
Nigdy nie używamy twardych linków typu `<a href="/kontakt">`. Zamiast tego wstrzykujemy usługę:

```csharp
@inject IRouteLocalizationService RouteService
<a href="@RouteService.GetLocalizedUrl("Contact")">Kontakt</a>
```

### Metody:
- `GetLocalizedUrl(pageKey, culture)`: Zwraca pełny zlokalizowany URL (np. `/en/contact-us`).
- `TryGetPageKeyFromSlug(slug, culture)`: Odwraca proces – na podstawie sluga znajduje klucz strony (używane przy przełączaniu języka).

---

## 5. Przełącznik Języków (ChangeGlobal.razor)
Logika przełączania jest "inteligentna" – nie tylko zmienia prefix, ale też zachowuje parametry:
1.  Rozpoznaje, na jakiej stronie jest użytkownik (używając `TryGetPageKeyFromSlug`).
2.  Znajduje odpowiedni slug w nowym języku.
3.  Dokleja pozostałą część ścieżki (np. ID apartamentu, daty rezerwacji) oraz QueryString.
4.  Wykonuje `NavigateTo` z `forceLoad: true`, aby odświeżyć stan serwera.

---

## 6. Rozwiązywanie Problemów i Rozbudowa

### Jak dodać nową stronę do systemu?
1.  W pliku `.razor` dodaj blok:
    ```razor
    @page "/twoja-trasa-bazowa"
    @* [SEO_ROUTES] *@
    @* [SEO_ROUTES_END] *@
    ```
2.  W `route_generator.py` dodaj wpis do `PAGE_MAPPINGS`:
    ```python
    'NowaStrona': {
        'file_prefix': 'NazwaPlikuResx', 
        'res_key': 'KluczTytulu',
        'file_path': '../../Droga/Do/Pliku.razor',
        'params': '' # opcjonalnie parametry typu /{Id:int}
    }
    ```
3.  Uruchom skrypt: `python3 route_generator.py`.
4.  Zbuduj projekt: `dotnet build`.

### Middleware (LocalizedRoutingMiddleware.cs)
Pełni rolę strażnika:
- **Ignoruje pliki:** Jeśli ścieżka zawiera kropkę (`.js`, `.css`, `.png`), middleware natychmiast przepuszcza żądanie dalej, omijając logikę lokalizacji.
- **Trasy techniczne:** Blokuje próby lokalizowania tras `/api`, `/_blazor` czy `/swagger`.

---

## 7. SEO i Indeksacja
System jest zoptymalizowany pod kątem wytycznych Google dla witryn wielojęzycznych:
- **Hreflang-ready:** Każda wersja ma swój unikalny adres URL.
- **Zlokalizowane Slugi:** Zwiększają trafność w lokalnych wynikach wyszukiwania.
- **Canonical Tags:** Skrypt generuje unikalne ścieżki, co zapobiega problemom z *duplicate content*.
- **SSR (Server-Side Rendering):** Roboty indeksujące otrzymują w pełni wyrenderowany kod HTML z tekstem w odpowiednim języku.
