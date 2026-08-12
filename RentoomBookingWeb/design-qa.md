# Design QA — `/pl/wspolpraca/v3`

## Zakres

- Implementacja: `http://127.0.0.1:5088/pl/wspolpraca/v3`
- Widoki pełne: ultrawide `2203 × 1339`, desktop `1440 × 900`, mobile `390 × 844`
- Dodatkowa kontrola ultrawide: `3440 × 1440`
- Dodatkowe breakpointy: `767/768px` oraz `959/960px`
- Stan: język polski, V3 włączone, zdjęcie `photo_1`

## Materiał referencyjny i dowody

- Hero — referencja: `C:\Users\macie\AppData\Local\Temp\codex-clipboard-ab29a489-cc14-4962-981a-5555ada04d5e.png`
- Lokalna przewaga — referencja: `C:\Users\macie\AppData\Local\Temp\codex-clipboard-9f66ee7b-90b5-495f-b76f-8cdf932d2de0.png`
- Ultrawide — zgłoszenie źródłowe: `C:\Users\macie\AppData\Local\Temp\codex-clipboard-655b33c7-6b55-4faa-8d69-7f4390207b3c.png` (`2203 × 1339`)
- Ultrawide po poprawce: `.design-qa/wspolpraca-v3-ultrawide-2203x1339-top-after.png` (`2203 × 1339`, CSS viewport `2203 × 1339`, density `1`)
- Skupiony kadr sekcji po poprawce: `.design-qa/wspolpraca-v3-ultrawide-2203x1339-after.png`
- Desktop: `.design-qa/wspolpraca-v3-desktop-1440x900-final2.png`
- Mobile: `.design-qa/wspolpraca-v3-mobile-390x844-final.png`
- Tabela kosztów: `.design-qa/wspolpraca-v3-cost-table-final.png`
- Porównanie modeli: `.design-qa/wspolpraca-v3-models-table-final.png`
- Lokalna przewaga: `.design-qa/wspolpraca-v3-local-advantage-framed.png`

## Wyniki wizualne

- Hero zachowuje układ zdjęcie + biały gradient + tekst; na mobile zdjęcie pozostaje widoczne pod treścią.
- H1 ma `60.48px` przy `1440px` i `40.95px` przy `390px`.
- Przy `1440px` przyciski hero są w jednym rzędzie, a kalkulator ma dwie kolumny: opcje po lewej, wynik po prawej.
- Przy `390px` przyciski i kalkulator są pionowe; stałe CTA jest pełną dolną belką i nie powoduje poziomego scrolla.
- Przy `768px` CTA przełącza się z belki na pigułkę, a przy `960px` kalkulator z jednej kolumny na dwie. Reguła mobilnego hero kończy się jawnie na `767px`.
- Obie tabele używają wspólnego arkusza, tej samej szerokości, paddingów, separatorów i typografii. W obu pierwszą kolumną danych jest zielony Rentoom.
- Sekcja „Lokalna przewaga” zachowuje istniejące tło i slider; treść oraz oba pogrubienia odpowiadają referencji.
- Na ekranach `2203px` i `3440px` szerokość siatki „Co robimy” pozostaje stała na `1180px`, a każda z trzech kolumn ma około `393px`; tekst nie zwija się do wąskich słupków.
- Przy `3440px` hero i statystyki zachowują odpowiednio bezpieczne szerokości treści oraz `1240px`, a wszystkie sekcje V3 liczą marginesy od rzeczywistego kontenera strony.

## Interakcje i technika

- Mobilne stałe CTA otwiera istniejący formularz; zamknięcie działa.
- Zmiana lokalizacji w kalkulatorze aktualizuje przedział wypłaty (`4 840–8 867 zł` → `3 622–7 211 zł`).
- Link hero pozostaje pod bieżącym adresem V3 i przewija do `#coop-calculator`.
- Czysta sesja przeglądarki: brak ostrzeżeń i błędów konsoli.
- Brak poziomego scrolla w sprawdzonych widokach.
- Build `RentoomBookingWeb`: 0 ostrzeżeń, 0 błędów.

## Historia poprawek

- Naprawiono link kalkulatora, który wcześniej gubił suffix V3.
- Usunięto zawijanie przycisku kalkulatora na desktopie.
- Wyrównano próg mobilnego hero z dolną belką CTA do `767px`.
- Poprawiono kadry obu tabel i potwierdzono wspólny kontrakt wizualny.
- `[P1]` Na viewportach szerszych od ograniczonego do `1920px` kontenera, boczne odstępy były liczone od `100vw`, przez co szerokość siatki spadała z `1180px` do `897px`, a kolumny z około `393px` do `299px`. Zamieniono obliczenia `100vw` na `100%` we wszystkich sekcjach V3 korzystających z tego wzorca. Ponowny pomiar na `2203px` i `3440px` oraz wskazane wyżej zrzuty potwierdzają pełną czytelność.

## Wynik

final result: passed

Nie znaleziono otwartych rozbieżności P0, P1 ani P2 względem zatwierdzonego planu i dostarczonych referencji.
