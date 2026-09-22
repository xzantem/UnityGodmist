# VS-06 — drużyna vertical slice’a

Cztery postacie dostępne od początku pozostają pod istniejącymi ID. VS-05 jest właścicielem profilu kampanii, 500 złota i wyposażenia L1; VS-07–09 dostarczają komplet startowych umiejętności i drzewek. Ta karta nie oznacza ukończenia ich contentu.

## Dane

Źródło bazowych statystyk, przyrostów i odporności: `../design/Statystyki Postaci.xlsx`, arkusz „Statystyki postaci”, kolumny Wojownik/Zwiadowca/Czarodziej/Paladyn. EOR zapisane w obu istniejących polach odporności na hard CC, zgodnie z dotychczasowym authoringiem Jerzego.

| Postać | Bazowe HP | Atak | Szybkość | Surowiec | Regeneracja |
| --- | --- | --- | --- | --- | --- |
| Jerzy | 300 | 18–36 | 55 | 200 impetu | 0 |
| Wladimir | 375 | 24–32 | 40 | 50 wściekłości | 0 |
| Alison | 250 | 27–36 | 45 | 100 many | 20/t |
| Reimond | 450 | 21–30 | 35 | 100 many | 15/t |

Wartości przed wyposażeniem. Początkowy impet/wściekłość 0, mana pełna. Regeneracja zatwierdzona 2026-09-22. Zachowane dotychczasowe maksimum many 100. Uzupełnione odporności Alison/Reimonda; Wladimir otrzymał statystyki, przyrosty i odporności Wojownika zamiast skopiowanych danych Scouta. Bazowy kryt 0, mnożnik 1x, celność z broni — tak jak istniejący Jerzy; usunięte domyślne bonusy 25%/1.5x/100 z Alison/Reimonda. Reguł magicznych trafień karta nie zmienia.

## Prezentacja i konfiguracja

Zgodnie z decyzją użytkownika wykorzystujemy istniejące sprite’y również w portretach. Alison: `wizzard_f`; Reimond: `knight_f` (rycerz z czerwonym pióropuszem). Wladimir zachowuje `knight_m`, Jerzy swoją obecną grafikę. Podpięte idle/run/hit; docelowa grafika należy do VS-22.

Jawne polecenie autora: `Godmist > Content > Configure VS-06 party`. Konfiguracja już wykonana przez Unity API w izolowanym projekcie; zweryfikowane assety przeniesione do głównego projektu. Nie trzeba uruchamiać polecenia ponownie. Import 18 użytych klatek Alison/Reimonda dostosowany z 100 do 16 px/unit, jak istniejące postacie; zachowane GUID-y i sprite ID. Brak zmian runtime, schematu save, ID i scen. Istniejące zapisy zachowują zapisany stan postaci; do odbioru nowych wartości użyć nowej kampanii.

## Weryfikacja

37/37 EditMode PASS: VerticalSlicePartyTests, NewCampaignTests, CharacterLevelGrowthSystemTests, ResourceRuleSystemTests (filtr obejmował też PartyGroupTests). Test integracyjny sprawdza każdą postać: lider, odłączenie/dołączenie do grupy, zdjęcie/założenie broni, awans i roundtrip zapisu z zachowaniem grup, lidera, poziomu, wyposażenia i regeneracji. Sprawdzone rzeczywiste ticki many oraz odporności i komplet animacji.

Play Mode PASS: start kampanii, cztery widoki, właściwe sprite’y idle/run, cztery portrety, każda postać jako lider oraz regresja celu u strażnika. Kontrola obrazu PASS po wyrównaniu skali importu. Pierwszy test sprite’a wymagał uwzględnienia animacji biegu Alison podczas formowania drużyny.

Końcowy retest po zmianie importu: 10/10 VerticalSlicePartyTests PASS. Self-review zakończony; READY_FOR_SMOKE.

Korekta po smoke: rozmycie Alison/Reimonda wynikało z Bilinear i kompresji. Wszystkie 18 użytych klatek ustawione przez Unity API na Point oraz Uncompressed (Default/Standalone); konfigurator utrwala te ustawienia. Ponowny Play Mode i kontrola obrazu PASS.
