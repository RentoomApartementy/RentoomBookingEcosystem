# Implementacja publicznego bloku Bloga: ApartmentsListing

Zaimplementuj w `RentoomBookingWeb` publiczny renderer bloku Bloga `ApartmentsListing` oraz integrację z publiczną ofertą IdoBooking. Nie zmieniaj kontraktu danych zapisywanego przez RentoomApp.

## Kontrakt bloku Bloga

- `BlogPostBlock.PropsJson` dla typu `ApartmentsListing` jest surową tablicą `int[]`, np. `[256,341]`.
- Pusta tablica `[]` oznacza wszystkie aktywne apartamenty, a nie pustą listę wyników.
- Lista nie zawiera zdjęć ani cen. Publiczny konsument ma pobierać bieżące dane apartamentów i ofert.
- Dodaj obsługę `ApartmentsListing` w publicznym rendererze `Components/Features/Blog/Pages/BlogPostPage.razor`.

## Reużywalna karuzela apartamentów

Bazuj na istniejącym `Components/Features/Home/Components/ApartmentsSection.razor`, ale nie uzależniaj renderera Bloga od `IApartmentsViewModel`, jego paginacji ani infinite scrolla.

1. Wydziel z `ApartmentsSection` reużywalną warstwę prezentacyjną karuzeli, np. `ApartmentsCarousel`.
2. Karuzela musi przyjmować listę `Apartments` jako parametr i renderować istniejące karty `Components/Features/Apartments/Components/Apartment.razor` wewnątrz `SimpleCarousel`.
3. Zachowaj autoscroll i nawigację `SimpleCarousel`; korzystaj z istniejących parametrów komponentu zamiast tworzyć drugi mechanizm przewijania.
4. `ApartmentsSection` strony głównej ma przekazywać do tej karuzeli `ViewModel.Items` i nadal obsługiwać własne ładowanie oraz infinite scroll.
5. Nowy komponent dla bloku Bloga ma odczytać ID z `PropsJson`, wybrać odpowiednie apartamenty z publicznej listy aktywnych obiektów i przekazać je do tej samej karuzeli. Dla `[]` przekazuje wszystkie aktywne apartamenty.
6. Zachowaj kolejność ID z `PropsJson` dla listy jawnie wybranej. Dla `[]` użyj istniejącej domyślnej kolejności listy publicznej.

## Publiczna oferta IdoBooking

Rozszerz istniejący `IIdoOfferService` oraz `IdoOfferService` w `SharedClasses/Services/IdoBooking/OfferService.cs` o metodę pobierania jednej publicznej oferty per `ApartmentId`.

- Wywołanie IdoBooking jest wykonywane tylko na serwerze przez istniejący `IIdoBookingConnectService`.
- Użyj `POST` do endpointu `public/offer/34/json` z ciałem JSON:

```json
{
  "offerId": 256
}
```

- `offerId` jest identyfikatorem apartamentu (`ApartmentId`/`objectId`).
- Dodaj modele odpowiedzi tylko dla potrzeb bloku: `result.images[0].url`, `result.minimalPrice`, `result.currency` oraz `result.errors`.
- Nie traktuj odpowiedzi z `result.errors` jako dostępnej oferty. Błąd pojedynczego apartamentu nie może zatrzymać renderowania innych kart.
- Cache'uj skuteczną odpowiedź per `ApartmentId` przez 10 minut po stronie serwera. Ogranicz równoległe pobieranie przy długiej liście apartamentów.
- Nie wywołuj IdoBooking w `Apartment.razor` ani z kodu JavaScript/przeglądarki.

## Ceny i zachowanie karty Apartment

Rozszerz istniejącą kartę `Apartment`, aby przyjmowała dane fallbacku z publicznej oferty, np. cenę minimalną i walutę. Dane są przekazywane przez komponent nadrzędny karuzeli.

1. Oferta dla aktualnie wybranych dat (`PricingOffer`) ma zawsze priorytet i zachowuje obecny format oraz CTA rezerwacji.
2. Jeśli brak `PricingOffer` dla dat, ale istnieje publiczna oferta, pokaż tekst `od XXX zł` z `minimalPrice` i `currency`.
3. Przy takim fallbacku zachowaj komunikat o braku oferty/dostępności dla wybranych dat.
4. Zamiast zablokowanego CTA renderuj aktywny przycisk `Szczegóły`, prowadzący do strony apartamentu bez przekazywania niedostępnych dat i bez uruchamiania rezerwacji.
5. Jeśli nie ma ani oferty dla dat, ani danych `public/offer`, zachowaj obecny fallback karty.
6. Rozszerz telemetrykę kliknięć o rozróżnienie ceny datowej i fallbacku publicznego, bez zmieniania istniejących zdarzeń dla standardowej oferty.

## Testy i kryteria akceptacji

- Deserializacja publicznej oferty poprawnie mapuje pierwsze zdjęcie, cenę minimalną, walutę i odpowiedź z błędem.
- `[]` renderuje wszystkie aktywne apartamenty; `[id...]` wyłącznie wskazane, w kolejności z JSON.
- Cena dla dat ma pierwszeństwo przed ceną z `public/offer`.
- Brak oferty dla dat wraz z publiczną ceną pokazuje `od XXX zł`, komunikat o niedostępności i aktywne `Szczegóły`.
- Brak danych `public/offer` zachowuje dotychczasowe zachowanie braku oferty.
- Publiczne oferty są cache'owane na 10 minut, a uszkodzona odpowiedź jednego apartamentu nie blokuje pozostałych kart.
- Zbuduj `RentoomBookingWeb` i uruchom istniejące testy rozwiązania.
