# System Inteligentnych Opisów AI (Rentoom Booking)

System pozwala na dynamiczne zastępowanie standardowych opisów z IdoBooking bogatymi treściami generowanymi przez AI (H1, opisy, FAQ, Highlights) pobieranymi z zewnętrznej bazy danych `Rentoom App`.

## 1. Architektura Danych

### Baza Danych (SharedClasses)
- **DbContext:** `RappDescriptionsDbContext` (korzysta z `RentoomDbConnectionString`).
- **Encje i Mapowanie Tabel (`SharedClasses/Integrations/RentoomApp/Descriptions/Models/DescriptionEntities.cs`):**
    - `ApartmentDescriptionSet` -> Tabela: **`rentoom.ApartmentDescriptionSets`**
    - `ApartmentDescriptionVariant` -> Tabela: **`rentoom.ApartmentDescriptionVariants`**
    - `ApartmentDescriptionVariantChannel` -> Tabela: **`rentoom.ApartmentDescriptionVariantChannels`**
    - `ApartmentDescriptionFaq` -> Tabela: **`rentoom.ApartmentDescriptionFaqItems`**
    - `ApartmentDescriptionHighlight` -> Tabela: **`rentoom.ApartmentDescriptionHighlights`**
    - `ApartmentDescriptionSeoPhrase` -> Tabela: **`rentoom.ApartmentDescriptionSeoPhrases`**
    - `ApartmentDescriptionCoverage` -> Tabela: **`rentoom.ApartmentDescriptionCoverage`**

### Serwis Logiki (`SharedClasses/Services/Descriptions/ApartmentAiDescriptionService.cs`)
Serwis `IApartmentAiDescriptionService` realizuje 3-krokową logikę wyboru treści:
1.  **Znalezienie aktywnego zestawu:** Szuka rekordów w `ApartmentDescriptionSets` dla danego `ApartmentId`, gdzie `IsActive = true`.
2.  **Identyfikacja wariantu kanału:** Sprawdza w tabeli `VariantChannels`, który `VariantType` (np. `mainVariant`) jest przypisany do kanału `rentoomPl` dla wersji źródłowej (`TranslationStatus = 'source'`).
3.  **Pobranie treści:** Pobiera rekord z `Variants` dla wybranego typu wariantu i aktualnego języka użytkownika (np. `pl`, `en`). Jeśli brak tłumaczenia, system automatycznie próbuje pobrać wersję źródłową. Pobiera również FAQ (z kolekcji `FaqItems`) i Highlights dla tego samego zestawu i języka.

## 2. Integracja z UI

### Strona Apartamentu (`ApartmentPage.razor`)
System używa warunkowego renderowania. Jeśli obiekt `_aiDescription` jest wypełniony, strona wyświetla nowoczesny układ AI, w przeciwnym razie następuje powrót do danych z IdoBooking:
```razor
@if (_aiDescription != null) {
    <ApartmentAiDescription Data="_aiDescription" ... />
} else {
    <ApartmentDescription ... /> @* Fallback do IdoBooking *@
}
```

### Logika Inicjalizacji (`ApartmentPage.razor.cs`)
Wstrzykiwany serwis używa aliasu **`AiDescriptionService`** (aby uniknąć konfliktów nazw z klasą serwisu). Logowanie debugowe widoczne w konsoli serwera:
- `[AI-Description] SUCCESS: Found AI description...` -> Dane załadowane poprawnie.
- `[AI-Description] INFO: No AI description found...` -> Brak danych w bazie AI, aktywny fallback.
- `[AI-Description] ERROR...` -> Problem z połączeniem lub schematem bazy.

## 3. Komponenty Prezentacji
Zlokalizowane w `RentoomBookingWeb/Components/Features/ReservationWorkflow/Components/ApartmentPage/`:
- `ApartmentAiDescription.razor`: Kontener główny i obsługa SEO (MetaTitle/Description).
- `AiHighlightsGrid.razor`: Wizualna prezentacja kluczowych cech (pills).
- `AiFaqSection.razor`: Sekcja pytań i odpowiedzi w formie akordeonu.

## 4. Troubleshooting (Najczęstsze problemy)
1.  **Błąd 42P01 (Relation does not exist):** Oznacza błędne mapowanie nazwy tabeli. Wszystkie tabele (poza FAQ i Coverage) powinny być w liczbie mnogiej. FAQ musi mapować na `ApartmentDescriptionFaqItems`.
2.  **Brak logów w przeglądarce:** Logi `Console.WriteLine` w Blazor Server pojawiają się tylko w konsoli terminala serwera.
3.  **Fallback mimo istnienia danych:** Sprawdź, czy w tabeli `ApartmentDescriptionVariantChannels` rekord dla kanału `rentoomPl` ma `IsEnabled = true`.
4.  **Konflikt nazw (CS0120):** Przy wstrzykiwaniu serwisu zawsze używaj nazwy właściwości innej niż nazwa typu (np. `AiDescriptionService`).
