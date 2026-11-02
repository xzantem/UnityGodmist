# Audyt wymagań i połączeń drzewek — 2026-11-02

**Wynik: COMPLETE — nie znaleziono kolejnych błędnych połączeń.**

Sprawdzono wszystkie 10 aktualnych drzewek: 240 węzłów i 131 wymagań. Zakres obejmuje produkcyjne T1–T5, a dla Paladyna aktualne T1–T4. Nie uwzględnia przyszłych, jeszcze niezaimplementowanych drzew ani tierów.

| Drzewko | Węzły | Połączenia |
|---|---:|---:|
| Dowódca | 25 | 14 |
| Awanturnik | 25 | 14 |
| Zbrojny | 25 | 10 |
| Skrytobójca | 25 | 16 |
| Tancerz Ostrzy | 25 | 14 |
| Strażnik | 25 | 14 |
| Czarnoksiężnik | 25 | 11 |
| Mag Ognia | 25 | 13 |
| Kapłan | 20 | 13 |
| Krzyżowiec | 20 | 12 |

## Dane i uzasadnienie wymagań

- Porównano 160 istniejących węzłów z `design/content_pass_1.xlsx`, arkusz Drzewka, oraz 55 nowych z `design/content_pass_2.xlsx`, arkusz Drzewka.
- Strażnik: 25 węzłów porównano z zatwierdzonym `Tools/ContentPass2/guardian-content.json`, z zachowaniem zamiany kolumn Refleksu i Czujności. Stary projekt z arkusza nie zastępuje zaakceptowanego redesignu.
- Sprawny Odzysk celowo nie wymaga Siły Naparu. Jest zatwierdzonym zamiennikiem Mocnej Wątroby: `VS07_SCOUT_CONTENT.md:19`.
- Zachowano wcześniejszą poprawkę Zbrojnego: Cios Rękojeścią → Odebranie Żywiołu; Odporność Nabyta pozostaje niezależna.
- Sprawdzono zgodność bieżących danych authoringu Zbrojnego, Zwiadowcy T5, Maga T5 i Strażnika z assetami.
- Przeczytano opisy wszystkich węzłów pod kątem właściwego bazowego skilla, zależności pośrednich i umiejętności znanych od początku. Nie dodawano nowych zależności wyłącznie dlatego, że pasyw i skill korzystają z podobnej mechaniki.

## Weryfikacja

- Unikalne ID i pozycje, istniejący rodzice w tym samym drzewku, prawidłowe minimalne rangi: PASS.
- Brak cykli, połączeń do samego siebie i wymagań z wyższego tieru: PASS.
- Runtime Unity: legalny zakup każdego z 240 węzłów: PASS.
- Runtime Unity: usunięcie wymaganego poziomu każdego ze 131 rodziców blokuje zakup odpowiedniego węzła i wskazuje brakujący talent: PASS. Test uwzględnia wszystkie rodzice w wymaganiach łączonych AND, zachowując odblokowany próg SP.
- Testy rysowania połączeń po zmianie rozmiaru okna, progresji oraz poprawionego Zbrojnego: 15/15 PASS.
- UI rysuje połączenia po `NodeId`, nie po nazwie lub numerze kolumny; `nodeRectsById` wskazuje faktyczne karty wybranego drzewka.

## Pozostałość w arkuszu

`design/content_pass_2.xlsx`, Drzewka!E27:E28 nadal zawierają stary, przesunięty wpis Zbrojnego. Jest to ten sam wcześniej zgłoszony błąd. Gra i dane authoringu są poprawione; arkusz nie był edytowany w ramach tego audytu.

## Dowody i zakres zmian

`Tools/ContentPass2/talent-requirements-audit-validation.json`; szczegółowy kontrakt źródeł i uruchomienia Unity: `../LocalHandoffs/talent-requirements-audit-20261102/`.

Audyt nie zmienia runtime, assetów ani arkuszy. Nie wymaga dodatkowego wiring. Nie wykonano commit/push.
