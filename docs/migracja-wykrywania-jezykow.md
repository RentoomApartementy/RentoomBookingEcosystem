# Migracja do adresów URL z prefiksem językowym (SEO)

## Opis zmian
W celu poprawy pozycjonowania (SEO) oraz umożliwienia wyszukiwarkom (np. Google) indeksowania różnych wersji językowych strony, wprowadzono strukturę adresów URL z prefiksem językowym: `/pl/` oraz `/en/`.

Dzięki tej zmianie, każda podstrona posiada unikalny adres dla każdego języka (np. `/pl/apartamenty` oraz `/en/apartamenty`), co jest zgodne z najlepszymi praktykami SEO.

## Kluczowe elementy techniczne

### 1. Rozpoznawanie języka z URL
W pliku `Program.cs` skonfigurowano `RequestLocalizationOptions` tak, aby `RouteDataRequestCultureProvider` był głównym dostawcą informacji o kulturze.
Aplikacja teraz automatycznie ustawia język na podstawie pierwszego segmentu ścieżki URL.

### 2. Nowy pomocnik nawigacji: `NavigationHelper.cs`
Utworzono klasę `RentoomBookingWeb.Helpers.NavigationHelper`, która zawiera metody:
- `GetLocalizedUrl(path)`: Dokleja aktualny kod języka (`pl` lub `en`) do podanej ścieżki.
- `RemoveCultureFromPath(path)`: Usuwa prefiks językowy ze ścieżki (użyteczne przy przełączaniu języków).

### 3. Zmiany w komponentach (Routing)
Wszystkie strony w folderze `Components/Features/` otrzymały dodatkowe dyrektywy `@page`, obsługujące parametr `{Culture}`. 
Przykład (`Apartments.razor`):
```razor
@page "/apartamenty"
@page "/{Culture}/apartamenty"
```
Dzięki zachowaniu starych ścieżek, dotychczasowe linki (np. z reklam czy e-maili) nadal działają.

### 4. Przełącznik języków (`ChangeGlobal.razor`)
Zmodyfikowano mechanizm przełączania języków. Zamiast używać parametrów w query string (`?culture=...`), komponent teraz przekierowuje użytkownika na odpowiedni adres URL z prefiksem, zachowując przy tym resztę ścieżki oraz parametry wyszukiwania.

### 5. Linki wewnętrzne
- **Menu główne (`Menu.razor`)**: Wszystkie linki są teraz generowane dynamicznie przez `NavigationHelper`, co gwarantuje, że użytkownik pozostanie w wybranej wersji językowej podczas nawigacji.
- **Wyszukiwarka (`SearchBar.razor`)**: Po kliknięciu "Szukaj", użytkownik jest przekierowywany na `/pl/apartamenty` lub `/en/apartamenty` z zachowaniem filtrów.

### 6. Mapa strony (`SitemapController.cs`)
Plik `sitemap.xml` generuje teraz linki dla wszystkich obsługiwanych języków. Google widzi teraz kompletną listę stron w wersji polskiej i angielskiej, co znacznie poprawi widoczność międzynarodową.

### 7. Integracja z Tpay (`TpayOpenApiGateway.cs`)
Zaktualizowano budowanie linków powrotnych (`SuccessUrl` oraz `ErrorUrl`). Po dokonaniu płatności, Tpay przekieruje użytkownika z powrotem na stronę w tym samym języku, w którym rozpoczął proces rezerwacji.

## Lista zmodyfikowanych plików
- `RentoomBookingWeb/Program.cs`
- `RentoomBookingWeb/Helpers/NavigationHelper.cs` (Nowy)
- `RentoomBookingWeb/Components/Shared/ChangeGlobal.razor`
- `RentoomBookingWeb/Components/Layout/Menu.razor`
- `RentoomBookingWeb/Components/Shared/SearchBar.razor`
- `RentoomBookingWeb/Controllers/SitemapController.cs`
- `SharedClasses/Integrations/Tpay/TpayOpenApiGateway.cs`
- Wszystkie pliki `.razor` stron (m.in. `Home.razor`, `Apartments.razor`, `ApartmentPage.razor`, `Contact.razor`, `Cooperation.razor`, `Statute.razor`, `Summary.razor`, `Payment.razor`).

## Uwagi dla programistów
Przy dodawaniu nowych stron lub linków, zaleca się używanie `NavigationHelper.GetLocalizedUrl("/sciezka")`, aby automatycznie wspierać wielojęzyczność w adresach URL.
