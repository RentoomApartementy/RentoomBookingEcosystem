# System Wielojęzyczności i Zlokalizowanego Routingu - Pełna Dokumentacja Techniczna (Runtime)

## 1. Wstęp i Architektura
System został zaprojektowany, aby wspierać **33 języki** w sposób dynamiczny, bez potrzeby generowania kodu podczas budowania aplikacji (Zero Build-Time Generation). Architektura opiera się na standardowych mechanizmach .NET 8, zapewniając pełną wydajność i zgodność z SEO.

### Kluczowe cechy:
- **Pure Runtime Architecture:** Wszystkie trasy są rozwiązywane dynamicznie w momencie żądania.
- **Single Source of Truth:** Słownikiem adresów URL są wyłącznie pliki zasobów `PageRoutes.resx`.
- **Zlokalizowane Slugi:** Pełne wsparcie dla tłumaczonych adresów (np. `/pl/apartamenty` vs `/en/apartments`).
- **International SEO:** Natywne wsparcie dla tagów `hreflang` i sitemap.

---

## 2. Rozpoznawanie Języka i Synchronizacja
System wykorzystuje natywny potok lokalizacji ASP.NET Core z autorską synchronizacją dla Blazor Interactive Server.

### Logika Detekcji:
1.  **URL Prefix (Priorytet):** `CustomRequestCultureProvider` w `Program.cs` odczytuje pierwszy segment (np. `/it/`).
2.  **Enterprise Cookie Sync:** `LocalizedRoutingMiddleware` dba o to, by przy każdym żądaniu URL ciasteczko `.AspNetCore.Culture` było zaktualizowane. Jest to **kluczowe dla SignalR**, który nie posiada prefiksu językowego w adresie WebSocket i musi polegać na ciasteczku, aby utrzymać poprawny język sesji.

---

## 3. Dynamiczny Routing (LocalizedRouter)
Zamiast setek dyrektyw `@page`, system używa centralnego punktu wejścia: `LocalizedRouter.razor`.

### Jak to działa?
1.  **Catch-All Route:** Router przechwytuje żądania pasujące do wzorca `/{cultureCode}/{*routeSegments}`.
2.  **Rejestr Mapowania:** Plik `PageMapping.cs` zawiera listę technicznych kluczy stron i odpowiadających im komponentów Razor.
3.  **Rezolucja Sluga:** Serwis `RouteLocalizationService` szuka w plikach `PageRoutes.resx` (dla danej kultury), do którego klucza należy dany slug z adresu URL.
4.  **Dynamic Rendering:** Po dopasowaniu klucza (np. "Contact"), router renderuje odpowiedni komponent za pomocą `<DynamicComponent />`.

---

## 4. Nawigacja (IRouteLocalizationService)
W aplikacji **nigdy** nie używamy "twardych" linków tekstowych.

- **Źle:** `<a href="/pl/kontakt">`
- **Dobrze:** `<NavLink href="@RouteService.GetLocalizedUrl("Contact")">`

Serwis automatycznie pobierze aktualny język, znajdzie odpowiedni slug w pliku `.resx` i wygeneruje poprawny, zlokalizowany adres URL.

---

## 5. Middleware Pipeline (Kolejność ma znaczenie)
Aby system działał poprawnie i nie blokował plików statycznych (CSS/JS), potok w `Program.cs` musi zachować ścisłą kolejność:

1.  `app.UseStaticFiles()` - Najpierw serwujemy fizyczne pliki z dysku.
2.  **`app.UseRouting()`** - Jawną deklarację routingu umieszczamy PO plikach, aby "głodny" router Blazora nie przechwytywał zapytań o assety.
3.  `app.UseRequestLocalization()` - Ustawiamy kulturę żądania.
4.  `app.MapRazorComponents()` - Renderujemy UI.

---

## 6. Wytyczne dla Programistów

### Dodawanie nowej podstrony (Tylko 2 kroki!):
1.  **Zarejestruj stronę w `PageMapping.cs`**:
    Dodaj klucz i typ komponentu, np.: `["Career"] = typeof(CareerPage)`.
2.  **Dodaj slugi do plików `.resx`**:
    W `PageRoutes.resx` (domyślny/EN) dodaj klucz `Career` (np. `careers`).
    W `PageRoutes.pl-PL.resx` dodaj klucz `Career` (np. `kariera`).

### Ścieżki do zasobów (Bevel-Proofing):
Ze względu na wielopoziomowe adresy URL, wszystkie zasoby statyczne **muszą** używać ścieżek bezwzględnych (zaczynających się od `/`).
- **Poprawnie:** `<img src="/assets/logo.png" />`
- **Błędnie:** `<img src="assets/logo.png" />` (spowoduje 404 na podstronach apartamentów).

---

## 7. SEO i Hreflang
Komponent `SeoHreflangs.razor` (wstrzyknięty w `App.razor`) automatycznie generuje tagi `<link rel="alternate" hreflang="..." />` dla wszystkich 33 języków, odpytując `RouteLocalizationService` o slugi dla każdego wspieranego języka. Zapewnia to idealną indeksację w Google.
