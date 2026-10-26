# VS-01 — katalog contentu pierwszego slice’a

Stan inspekcji: 2026-09-22. Katalog zakresu i pokrycia, nie deklaracja ukończenia contentu.
Źródła: `../design/content_pass_1.xlsx` (8 arkuszy), `../design/Wyposażenie.xlsx` / Arcydzieła!A48:I51, aktualne definicje w `Assets/_Project/ScriptableObjects`, późniejsze decyzje użytkownika w `BALANCE_DECISIONS_PENDING.md`.
Arkusze i serialized assety pozostawiono bez zmian. Nowsze decyzje rozmowy mają pierwszeństwo przed starszymi komórkami.

## Zamknięty zakres

- Cztery postacie dostępne od początku: Jerzy (Scout), Wladimir (Wojownik), Alison (Czarodziejka), Reimond (Paladyn).
- Osiem drzewek, pierwsze cztery tiery: 160 węzłów oraz osiem pasywów drzewka. Pięć startowych skilli każdej klasy, łącznie 20.
- Content poziomów 1–20, limit postaci nadal 50. Wyposażenie bazowych poziomów 5 i 15, materiały poziomów 1 i 11. Materiały przeciwników poziomu 21 wykluczone.
- Siedem zwykłych przeciwników i Duch Umarłego Króla (warianty wypraw L10/L20). Elity to osobne typy; Szkielet Dowódca pozostaje przykładem przyszłego rozszerzenia, nie ósmym mobem tego passu.
- Katakumby, bez podestów/balkonów. Użytkownik zatwierdził 16 produkcyjnych szablonów. Roboczy podział wykonawczy: 1 startowy, 9 zwykłych, 3 opcjonalne, 2 korytarze, 1 finałowy.
- Wszystkie nazwane warianty materiałów i części T1/T2 z arkusza. Źródła z innych theme dostępne przez rotację NPC, bez gwarancji stocku; nie dodajemy innych lochów.
- Biżuteria: cztery sloty × osiem metali, 11 klejnotów. Pula 53 efektów galdurytów z arkusza jest konkretną listą passu; nie zastępować jej kolejną arbitralną redukcją do procentowego celu 50–70%.
- Consumables wynikają ze składników i kolumny „Siła (M)”; kolumna „Siła” opisuje późniejsze efekty T2, poza passem. Poziom składnika i tier siły efektu nie są tym samym.
- Specjalne nagrody bossa: wykorzystać cztery istniejące unikatowe definicje, zweryfikować warianty L10/L20.
- Koniec slice’a: nowa gra → przygotowanie czterech postaci → wyprawa/misja → opcjonalny boss → powrót/nagroda → rozwój → kolejna wyprawa. Nie narzucać jednego poziomu na wyprawę.

## Pokrycie i właściciele

| Karta | Istniejąca baza | Pozostaje |
| --- | --- | --- |
| VS-02/03 | Runtime klas, zasobów, skillów i pasywów | Review pod niżej wymienione konkretne mechaniki, następnie tylko potrzebne zmiany |
| VS-04/05 | Obecny scene/save flow i Arungard | Produkcyjny start, menu, startowe zasoby i ekwipunek |
| VS-06 | 4 klasy, CD_Jerzy, CD_Wladimir_Test, CD_Mage, CD_Paladin | Wladimir ma pustą ClassData; nazwy Alison/Reimond, role, statystyki, prezentacja i startowy loadout |
| VS-07 | ST_Scout_Assassin, ST_Scout_BladeDancer | Porównać wszystkie 40 węzłów z katalogiem; BladeDancer zawiera kopie/testy, nie gotowe drzewko. Guardian poza passem |
| VS-08 | CCD_Warrior, generyczne skille | Dowódca i Awanturnik, 40 węzłów i startowe skille |
| VS-09 | CCD_Mage/Paladin, generyczne skille | Czarnoksiężnik, Mag Ognia, Kapłan, Krzyżowiec — 80 węzłów i startowe skille |
| VS-10 | 27 assetów części wyposażenia, materiały i receptury | Pokrycie 192 wpisów części T1/T2 i 182 materiałów L1/L11 z list poniżej; istniejące ID zachować |
| VS-11 | 4 sloty biżuterii, Topaz/Amber/Obsidian, 4 unikaty; 6 typów efektów galdurytów w enumie | Pozostałe klejnoty, metale/efekty, rozszerzenie galdurytów, walidacja unikatów |
| VS-12 | CID_Potion/Bomb/ReturnScroll/Bandage/Antidote/DampTowel, runtime alchemii | Składniki i katalizatory, źródła i receptury, zgodne nazwy/opisy |
| VS-13 | Dostawy kowala i cenniki usług po zmianach VS-18 | Spięcie produkcyjnych ofert/receptur z pełnym katalogiem |
| VS-14 | 20 assetów pokojów łącznie z demo/wysokościami, 2 layout profiles | Wybrać/uzupełnić 16 produkcyjnych szablonów, katalog i definicję Katakumb |
| VS-15/16 | CD_Skeleton_Test, CD_Enemy_Test, CD_Boss_Test; 3 testowe encountery; 4 loot tables | Siedem mobów + boss, ich skille, różne składy, tematyczne lootbagi i warianty poziomów |
| VS-17 | Jeden quest i runtime nagród XP | Powtarzalne misje i ich przepływ |
| VS-18 | Wdrożone pakiety mechanik balansu | Oczekuje na gotowy content i końcową integrację; obecnie nie jest aktywną kartą |
| VS-22 | Istniejąca prezentacja/testowe grafiki | Uzupełnić brakujące docelowe portrety, ikony i podstawową prezentację |

Liczby istniejących assetów obejmują testy i nie oznaczają gotowości produkcyjnej. Dopasowanie nazwy w tabelach jest tylko kandydatem do reuse, nie weryfikacją wartości, referencji katalogu czy działania. Brak dopasowania nazwy nie dowodzi braku równoważnej mechaniki.

## Rozbieżności źródeł — obowiązkowe przy authoringu

1. Ektoplazma, Pazur ghula i pozostałe materiały przeciwników L21: poza passem, mimo obecności w opisach dropu mobów. Królewska ektoplazma to osobna nagroda bossa w przedziale 11–20, nie zwykła Ektoplazma. Nie ma osobnego wpisu w arkuszu Materiały — parametry i zastosowanie doprecyzować w VS-11/16.
2. Galduryty z toreb: pierwszy tier L11, nie widniejące w starszym arkuszu L20.
3. Koścista torba A: wspólna kategoria 80%, równy wybór alkoholu/prochu, czyli 40% na każdy. Zachować właściwy poziom alkoholu/prochu.
4. Torba bossa D: 30% łącznie, równy wybór Bursztynu i Obsydianu; 15% na każdy, Obsydian 1–2.
5. Kategorie lootbagów losują po jednym entry, nie jedną sztukę. Możliwe powtórzenie materiału między kategoriami (przykład użytkownika z rudą w dwóch kategoriach); stare dopiski „inny niż B/C” nie są aktualną zasadą. Arkusz ma też dwie kategorie E — nadać unikalne klucze techniczne bez zgubienia obu dropów.
6. Pierścienie: celność 2/3 i kryt 0.06x/0.08x; kolczyki maksymalnego surowca +10/12%. Starsze komórki nie mogą nadpisać decyzji.
7. Uzdrowienie: 30 MP, 120% średniego ataku. Agresywny Ogień: 50% rodzica, niezależnie na cel, do trzech pokoleń. Pole Osądu: jedno na mapie, nowe zastępuje stare.
8. Zastosować pozostałe doprecyzowania drzewek i mechanik z rejestru decyzji (m.in. brak stackowania bez jawnej reguły, rzeczywista utrata HP, wspólna tolerancja CC, PA w setnych, penetracja def/(1+pen)).
9. Szanse zwykłych mobów na poszczególne torby nie były ustalone. To decyzja VS-15, nie powód do uznania obecnych testowych tabel za produkcyjne. W opisie bossa Torba z Brońmi występuje dwa razy; sprawdzić intencję przed authoringiem tabeli.
10. Arkusz główny wyposażenia zawiera też późniejsze poziomy do symulacji; nie włączać poziomu 45 do passu 1–20. Warianty startowego sprzętu i możliwość wyposażenia na L1 doprecyzować w VS-05/06.

## Zależności mechaniczne do punktowego review VS-02

- Drzewka: zmiany istniejących skilli, warunki/rangi, koszt ofensywnych skilli, narastająca Złość i pamięć pochodzenia podpaleń.
- Pole Osądu: pojedynczy obszar, zastępowanie, efekty końca/przedostatniej tury. Zweryfikować istniejące aura/delayed effects zamiast zakładać pełne pokrycie.
- Przeciwnicy: niewidzialność/materializacja, warunkowe zachowania AI, absorpcja obrażeń sąsiadów, skill z ruchem i obrażeniami.
- Boss: przywołania z limitem oraz pięć run na progach HP 75/50/25%, deaktywacja przez wejście i detonacja po dwóch turach. Istniejące summon/pułapki to baza, nie dowód obsługi tej sekwencji.
- Galduryty: enum ma GeneralDamagePercent, CriticalChancePercent, AccuracyPercent, MaxHealthPercent, PhysicalDefensePercent, SpeedPercent. Pozostałe efekty tabeli wymagają sprawdzenia runtime i authoringu; „przy ataku” raz na użycie skilla, domyślnie 3 tury i DoT 60%.
- Lootbagi: niezależne kategorie, zakresy poziomów, generowane bronie/pancerze/galduryty i unikat bossa. Zachować istniejący system zamiast nowego właściciela nagród.
- Start gry: stabilne ID, cztery postacie, brak zależności od testowego składu Scouta; kompatybilność zapisów przy rozszerzaniu enumów/definicji.

## Kolejność pracy

VS-01 (ten katalog) → VS-02 (review gotowości pod konkretną listę) → VS-03 (tylko wymagany refaktor) → VS-04/05/06 (start i drużyna) → VS-07/08/09 (klasy) → VS-10/11/12/13 (przedmioty i gospodarka) → VS-14/15/16/17 (Katakumby, boss, misje) → VS-22 i końcowa VS-18.
Prezentację wykonywać razem z contentem. VS-19–21 poza zakresem. Szczegóły UI, składów encounterów, misji i konkretnych pokojów rozstrzygać w kartach właścicielskich, nie otwierać ponownie zatwierdzonej listy klas/mobów.

## Startowe skille — VS-06–09

| Postać | Skill | Koszt z arkusza | Źródło | Kandydat reuse |
| --- | --- | --- | --- | --- |
| Wladimir | Cięcie | 0 PW, 60% PA | Startowe skille!A2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wladimir | Kopnięcie | 20 PW, 40% PA | Startowe skille!B2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wladimir | Okrzyk Wojenny | 30 PW, 35% PA | Startowe skille!C2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wladimir | Prawy Sierpowy | 35 PW, 60% PA | Startowe skille!D2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wladimir | Bandaż Polowy | 40 PW, 65% PA | Startowe skille!E2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jerzy | Rzut Sztyletem | 0 PP, 60% PA, 1sz./- | Startowe skille!F2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jerzy | Wymiana | 20 PP, 20% PA | Startowe skille!G2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jerzy | Chmura Dymu | 65 PP, 30% PA | Startowe skille!H2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jerzy | Linka z Hakiem | 25 PP, 25% PA; odzysk sztyletu: 20 PP, 20% PA | Decyzja użytkownika 2026-10-09 (zastępuje Startowe skille!I2) | Przeciwnik: 80% przyciągnięcia o 3 pola; własny sztylet: odzysk bez losowania |
| Jerzy | Celny Rzut | 50 PP, 70% PA, 1sz./ | Startowe skille!J2 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Alison | Kula Energii | 5 MP, 75% PA | Startowe skille!A11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Alison | Kula Ognia | 40 MP, 75% PA | Startowe skille!B11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Alison | Skupienie | 0 MP, 85% PA | Startowe skille!C11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Alison | Magiczna Tarcza | 40 MP, 90% PA | Startowe skille!D11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Alison | Męczące Czary | Toggle | Startowe skille!E11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Reimond | Wyrok | 0 MP, 60% PA | Startowe skille!F11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Reimond | Miażdżący Cios | 30 MP, 70% PA | Startowe skille!G11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Reimond | Uzdrowienie | 30 MP, 40% PA | Startowe skille!H11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Reimond | Święta Transfuzja | 60 MP, 65% PA | Startowe skille!I11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Reimond | Tarcza Odbicia | 40 MP, 40% AP | Startowe skille!J11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |

Dodatkowe analogi: Rzut Sztyletem → SD_KnifeToss (obecnie „Rzut nożem”, wartości różne); Okrzyk Wojenny → SD_BattleCry; Uzdrowienie → SD_Heal; Magiczna Tarcza → SD_Shield. Nie tworzyć duplikatów wyłącznie z powodu różnicy nazw.

## Osiem drzewek — imienna lista węzłów i rang

### Dowódca

Pasyw drzewka: +0.3% Szansy krytycznej. Ulepszone bronie zadają przeciwnikowi 0.002% Nieuchronnych obrażeń za każdy poziom ulepszenia

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Rozkaz Unicestwienia [45 PW, 70% PA] | [90%] Zwiększa obrażenia zadawane celowi przez wszystkich sojuszników o 20% na 3 tury | Celność: -; Cel: Jeden wróg; Zasięg: 4 | Drzewka!A2:G2 |
| T1:2 | Oznaczenie [2] | Zwiększa szansę na obrażenia krytyczne przeciwko celowi objętemu Rozkazem Unicestwienia o 50%/100%[p] | Wymaga: T1:1 | Drzewka!A3:G3 |
| T1:3 | Zdarcie Pancerza [1] | Zwiększa penetrację fizyczną o 25% |  | Drzewka!A4:G4 |
| T1:4 | Przebijanie Pancerza [2] | Każde obrażenia są zwiększone o 0.15%/0.3% za każdy aktualny procent zdrowia przeciwnika | Wymaga: T1:3 | Drzewka!A5:G5 |
| T1:5 | Mistrzostwo Broni [3] | Zwiększa szansę krytyczną o 5%/10%/15%[p] |  | Drzewka!A6:G6 |
| T2:1 | Rozbicie [3] | Zwiększa bonus obrażeń Rozkazu Unicestwienia o 5%/10%/15% | Wymaga: T1:1 | Drzewka!A7:G7 |
| T2:2 | Unieruchomienie [40 PW, 70% PA] | Zadaje losowe obrażenia fizyczne, [90%] unieruchomienie na 2 tury | Celność: 71; Cel: Jeden wróg; Zasięg: 1 | Drzewka!A8:G8 |
| T2:3 | Połamane Kości [1] | Zwiększa czas trwania Unieruchomienia o 2 tury | Wymaga: T2:2 | Drzewka!A9:G9 |
| T2:4 | Przebicie [1] | Zwiększa obrażenia krytyczne o 20% |  | Drzewka!A10:G10 |
| T2:5 | Głębokie Rany [3] | Obrażenia krytyczne mają 90% szansy na wyrządzenie krwawienia 45%/60%/75% na 3 tury | Wymaga: T1:5, T2:4 | Drzewka!A11:G11 |
| T3:1 | Flankowanie [2] | Zmniejsza obronę celu objętego Rozkazem Unicestwienia o 5%/10% za każdego przyległego sojusznika | Wymaga: T2:1 | Drzewka!A12:G12 |
| T3:2 | Przygwożdżenie [2] | Unieruchomienie zmniejsza unik przeciwnika o 12/20 | Wymaga: T2:2 | Drzewka!A13:G13 |
| T3:3 | Trauma [2] | [85%] Ciosy krytyczne zwiększają obrażenia DoT na przeciwniku o 30%/60% na 5 tur |  | Drzewka!A14:G14 |
| T3:4 | Rozkaz Ochrony [35 PW, 70% PA] | Zmniejsza przyjmowane obrażenia celu o 30% i o dodatkowe 10% za każdego przyległego sojusznika na 3 tury | Cel: Jeden sojusznik; Zasięg: 4 | Drzewka!A15:G15 |
| T3:5 | Odnowienie Sił [3] | Daje 5%/10%/15% szansy na uniknięcie ogłuszenia i odnowę 10% maksymalnego zdrowia |  | Drzewka!A16:G16 |
| T4:1 | Rozkaz Przegrupowania [30 PW, 70% PA] | Przyśpiesza ruch celu do przodu o 100% | Cel: Jeden sojusznik; Zasięg: 3 | Drzewka!A17:G17 |
| T4:2 | Strategia [1] | Rozkaz Przegrupowania daje wszystkim sojusznikom 2 darmowe kroki w każdej turze przez 3 tury | Wymaga: T4:1 | Drzewka!A18:G18 |
| T4:3 | Dobitka [3] | Cięcie ma 3%/6%/9% na niezużycie punktów akcji |  | Drzewka!A19:G19 |
| T4:4 | Riposta [1] | Przeciwnicy częściej obierają za cel osobę objętą Rozkazem Ochrony, szansa zwiększa się za każdą przyległą osobę. Zwraca 30% obrażeń przedmitygacyjnych do atakującego | Wymaga: T3:4 | Drzewka!A20:G20 |
| T4:5 | Wściekłe Powstanie [2] | Odnowienie Sił zadaje przeciwnikowi, który chciał wywołać ogłuszenie obrażenia magiczne równe 3%/7% jego maksymalnego zdrowia ze zwiększoną szansą na cios krytyczny o 10%. | Wymaga: T3:5 | Drzewka!A21:G21 |

### Awanturnik

Pasyw drzewka: +1% Maksymalnej wściekłości. 2% Zużytej wściekłości jest przekształcane na zdrowie

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Krwawiące Zniszczenie [40 PW, 65% PA] | Zadaje najmniejsze obrażenia fizyczne, [75%] krwawienie 90% na 4 tury | Celność: 75; Cel: Jeden wróg; Zasięg: 1 | Drzewka!A33:G33 |
| T1:2 | Wyrządzanie Ran [2] | Każdy posiadany punkt wściekłości zwiększa szansę na krwawienie Krwawiącego Zniszczenia o 0.05%/0.1% | Wymaga: T1:1 | Drzewka!A34:G34 |
| T1:3 | Pragnienie Zemsty [3] | Przyjmowanie obrażeń generuje 1/2/3 wściekłości za każde utracone 5% maksymalnego zdrowia | Wymaga: T1:4 | Drzewka!A35:G35 |
| T1:4 | Wściekłość [1] | Zwiększa ilość wściekłości generowanej przez Cięcie o 5 i zmniejsza efekt kary za niedobór wściekłości o 50% |  | Drzewka!A36:G36 |
| T1:5 | Wściekłe Uderzenia [3] | Zmniejsza koszt wściekłości umiejętności ofensywnych o 10%/20%/30% | Wymaga: T1:4 | Drzewka!A37:G37 |
| T2:1 | Obszerniejsza Rana [2] | Zwiększa obrażenia krwawienia Krwawiącego Zniszczenia o 15%/30% | Wymaga: T1:1 | Drzewka!A38:G38 |
| T2:2 | Zemsta [2] | Każdy posiadany punkt wściekłości zwiększa szansę na obrażenia krytyczne o 0.05%/0.1% | Wymaga: T1:2 | Drzewka!A39:G39 |
| T2:3 | Wyzwolić Wściekłość [3] | Zwiększa maksymalną ilość punktów wściekłości o 10/20/30 |  | Drzewka!A40:G40 |
| T2:4 | Krzyk [100% PW, 60% PA] | [90%] Ogłusza na 1 turę, lub na 3 tury, jeśli zużyje co najmniej 50 wściekłości | Cel: Jeden wróg; Zasięg: 3 | Drzewka!A41:G41 |
| T2:5 | Kolczasta Zbroja [1] | Zwiększa atak o 0.1 za każdy punkt obrony fizycznej |  | Drzewka!A42:G42 |
| T3:1 | Młyniec [40 PW, 20% PA] | Zadaje 25% losowych obrażeń 3 razy | Celność: 79; Cel: Obszarowy na wrogów; Zasięg: 1 | Drzewka!A43:G43 |
| T3:2 | Do Trzech Razy Sztuka [2] | Co trzecie Cięcie z rzędu na tym samym celu ma zwiększone obrażenia o 0.25%/0.5% za każdy posiadany punkt wściekłości | Wymaga: T2:2 | Drzewka!A44:G44 |
| T3:3 | Amok [3] | Ciosy krytyczne zwiększają maksymalną wściekłość o 20/30/40 na 5 tur | Wymaga: T2:3 | Drzewka!A45:G45 |
| T3:4 | Wykorzystanie Nieświadomości [2] | Ataki na celach ogłuszonych generują 50%/100% więcej wściekłości | Wymaga: T2:4 | Drzewka!A46:G46 |
| T3:5 | Agresywna Natura [1] | Pasywnie generuje 3 wściekłości na turę |  | Drzewka!A47:G47 |
| T4:1 | Centrum Pożogi [2] | Każdy cios zadany przez Młyniec ma 15% szansy na wyrządzenie podpalenia 30%/50% na 2 tury | Wymaga: T3:1 | Drzewka!A48:G48 |
| T4:2 | Moment Siły [2] | Zwiększa ilość ciosów zadawanych przez Młyniec o 1/2 | Wymaga: T4:1 | Drzewka!A49:G49 |
| T4:3 | Adrenalina [1] | Zwiększa szybkość o 4 za każde 25 punktów wściekłości |  | Drzewka!A50:G50 |
| T4:4 | Pragnienie Śmierci [Toggle] | Zużywa 25% maksymalnej lub 20 wściekłości (większe) na turę, zwiększa zadawane i przyjmowane obrażenia o 50%, każde śmiertelne obrażenia są negowane, kończy efekt gdy braknie wściekłości | Cel: Rzucający | Drzewka!A51:G51 |
| T4:5 | Desperacja [2] | Zwiększa bonus zadawanych i przyjmowanych obrażeń Pragnienia Śmierci o 25%/50% | Wymaga: T4:4 | Drzewka!A52:G52 |

### Skrytobójca

Pasyw drzewka: +0.2% Szansy krytycznej. +0.5% Obrażeń w walce wręcz

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Cios w Plecy [2] | Zwiększa obrażenia zadawane od tyłu o 10%/20% |  | Drzewka!A64:G64 |
| T1:2 | Rykoszet [60 PP, 70% PA, 1sz.] | Zadaje losowe obrażenia fizyczne, każde kolejne trafienie ma 10% większą szansę krytyczną | Celność: 81; Cel: Ciąg wrogów [3]; Zasięg: b4w2 | Drzewka!A65:G65 |
| T1:3 | Dalekie Widzenie [2] | Zwiększa zasięg bezwzględny i względny Rykoszetu o 1/3 | Wymaga: T1:2 | Drzewka!A66:G66 |
| T1:4 | Krytyczny Punkt [3] | Zwiększa obrażenia krytyczne zyskane ze Słabego Punktu o 5%/10%/15% | Wymaga: T1:5 | Drzewka!A67:G67 |
| T1:5 | Słaby Punkt [80 PP, 55% PA] | [95%] Zwiększa szansę krytyczną o 10% i obrażenia krytyczne o 20% na 5 tur wobec wroga | Cel: Jeden wróg; Zasięg: 8 | Drzewka!A68:G68 |
| T2:1 | Podcięcie Nóg [2] | Atakowanie przeciwników od tyłu po poruszaniu się ma 25%/50% szansy na ogłuszenie na 1 turę | Wymaga: T1:1 | Drzewka!A69:G69 |
| T2:2 | Siła Rzutu [2] | Każdy kolejny przeciwnik trafiony Rykoszetem otrzymuje 10%/20% więcej obrażeń | Wymaga: T1:2 | Drzewka!A70:G70 |
| T2:3 | Odbicie Sztyletu [1] | Zwiększa ilość celów trafianych przez Rykoszet o 2 | Wymaga: T2:2 | Drzewka!A71:G71 |
| T2:4 | Gwałtowne Uderzenia [2] | Zwiększa szansę krytyczną w walce wręcz o 5%/10% |  | Drzewka!A72:G72 |
| T2:5 | Dogłębna Analiza [2] | Ciosy krytyczne podczas działania Słabego Punktu dają 20%/30% penetracji fizycznej wobec każdego przeciwnika na 3 tury | Wymaga: T1:5 | Drzewka!A73:G73 |
| T3:1 | Atak w Biegu [2] | Przechodzenie obok przeciwników zadaje im 25%/50% losowych obrażeń fizycznych z celnością 77 (raz na turę) | Wymaga: T2:1 | Drzewka!A74:G74 |
| T3:2 | Zaskoczenie [2] | Atak wyprowadzony podczas trwania Wojownika Cieni ma 30%/60% większe obrażenia | Wymaga: T3:3 | Drzewka!A75:G75 |
| T3:3 | Wojownik Cieni [50PP, 70% PA] | Daje niewidzialność na 3 tury, Szarża 1 | Cel: Rzucający | Drzewka!A76:G76 |
| T3:4 | Styl Karambitowy [3] | Zwiększa obrażenia w walce wręcz o 25%/60%/100% | Wymaga: T2:4 | Drzewka!A77:G77 |
| T3:5 | Podcięcie Gardła [3] | Daje 4%/7%/10% szansy na natychmiastowe zabicie ciosem wręcz przeciwnika, którego zabiłaby pięciokrotność zadanych obrażeń. Bossy zamiast tego otrzymują 100% obrażeń więcej | Wymaga: T3:4 | Drzewka!A78:G78 |
| T4:1 | Celny Wzrok [2] | Zwiększa szansę na Ostry Nóż o 5%/10% | Wymaga: T4:2 | Drzewka!A79:G79 |
| T4:2 | Ostry Nóż [2] | Rzucone noże mają 15% szansy na wbicie się w przeciwnika. Kliknięcie wyjmuje go rzucając [90%] krwawienie 60%/90% na 3 tury |  | Drzewka!A80:G80 |
| T4:3 | Lekki Krok [2] | Zmniejsza wymagania ruchowe podczas trwania Wojownika Cieni o 1/3 punkty akcji | Wymaga: T3:3 | Drzewka!A81:G81 |
| T4:4 | Trafny Cios [1] | Zwiększa czas trwania ogłuszenia Uderzenia Głowicą do 2 tur | Wymaga: T4:5 | Drzewka!A82:G82 |
| T4:5 | Uderzenie Głowicą [2] | Daje 15%/30% na ogłuszenie przeciwnika na 1 turę, jeśli Podcięcie Gardła nie powiedzie się | Wymaga: T3:5 | Drzewka!A83:G83 |

### Tancerz Ostrzy

Pasyw drzewka: +1% Szans na niezużycie sztyletu. +1% Siły zatrucia

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Wirusy Osłabiające [2] | Zatruty Sztylet zmniejsza odporność na zatrucie o 15%/30% na 3 tury | Wymaga: T1:2 | Drzewka!A95:G95 |
| T1:2 | Zatruty Sztylet [25 PP, 65% PA, 1sz./] | Zadaje najmniejsze obrażenia fizyczne, [65%] zatrucie 90% na 3 tury | Celność: 81; Cel: Jeden wróg; Zasięg: 4/1 | Drzewka!A96:G96 |
| T1:3 | Jad [3] | Zwiększa obrażenia zatrucia Zatrutego Sztyletu o 10%/20%/30% | Wymaga: T1:2 | Drzewka!A97:G97 |
| T1:4 | Silna Zniewaga [2] | Zwiększa bonus obrażeń za każdy punkt Złości o 0.05%/0.1% | Wymaga: T1:5 | Drzewka!A98:G98 |
| T1:5 | Złość [1] | Zadawanie obrażeń nakłada na przeciwnika 10 punktów Złości (max 100), każdy punkt zwiększa otrzymywane obrażenia o 0.15%  |  | Drzewka!A99:G99 |
| T2:1 | Rzut w Kolano [3] | Rzut Sztyletem ma 10%/20%/30% na spowolnienie przeciwnika o 15 na 3 tury |  | Drzewka!A100:G100 |
| T2:2 | Kombo [2] | Daje 5%/10% szansy na podwojenie Rzutu Sztyletem |  | Drzewka!A101:G101 |
| T2:3 | Szybkie Ciosy [30 PP, 70% PA, 3sz./] | Zadaje 50% losowych obrażeń fizycznych 3 razy, każdy cios ma 35% na podwójne obrażenia | Celność: 74; Cel: Jeden wróg; Zasięg: 4/1 | Drzewka!A102:G102 |
| T2:4 | Seria Ciosów [2] | Daje 50% szansy na wyprowadzenie maksymalnie 1/2 dodatkowych ciosów przez Szybkie Ciosy | Wymaga: T2:3 | Drzewka!A103:G103 |
| T2:5 | Zemsta [1] | Otrzymywanie obrażeń od przeciwników nakłada na nich 10 punktów Złości | Wymaga: T1:5 | Drzewka!A104:G104 |
| T3:1 | Tonik Pobudzający [65 PP, 40% PA] | Przyspieszenie o 20 na 5 tur, po tym spowolnienie o 10 na 3 tury | Cel: Rzucajacy | Drzewka!A105:G105 |
| T3:2 | Mistrz Kombo [2] | Kombo narzuca [80%] krwawienie 100%/120% na 3 tury | Wymaga: T2:2 | Drzewka!A106:G106 |
| T3:3 | Finezyjne Pchnięcia [2] | Zwiększa szanse na podwójne obrażenia Szybkich Ciosów o 10%/20% | Wymaga: T2:3 | Drzewka!A107:G107 |
| T3:4 | Taniec Zagłady [2] | Zwiększa maksymalną ilość dodatkowych ciosów z Serii Ciosów o 1/2 | Wymaga: T2:4 | Drzewka!A108:G108 |
| T3:5 | Kontrola Psychiczna [3] | Zwiększa przyrost Złości o 1/3/5 | Wymaga: T2:5 | Drzewka!A109:G109 |
| T4:1 | Siła Naparu [1] | Zwiększa przyspieszenie Toniku Pobudzającego o 10 i spowolnienie o 5 | Wymaga: T3:1 | Drzewka!A110:G110 |
| T4:2 | Sprawny Odzysk [1] | Zwiększa zniżkę odzysku własnych sztyletów Linką z Hakiem z 20% do 60%: 10 PP i 10% PA | Brak wymagania konkretnego węzła; standardowy próg T4 | Decyzja użytkownika 2026-10-09; zastępuje Mocną Wątrobę; ID efficient_recovery, migracja starego ID przy wczytywaniu |
| T4:3 | Celny Rzut [2] | Zwiększa obrażenia o 2%/4% za każde pole odległości od celu |  | Drzewka!A112:G112 |
| T4:4 | Mistrz Trucizny [2] | Zwiększa siłę wszystkich zatruć o 10%/20%[p] |  | Drzewka!A113:G113 |
| T4:5 | Zapasowe Sztylety [2] | Zwiększa maksymalną ilość sztyletów o 1/2 |  | Drzewka!A114:G114 |

### Czarnoksiężnik

Pasyw drzewka: -1.5% Zużycia many. +1% Obrażeń jeśli otrzymuje je tylko jeden przeciwnik

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Rozproszenie [3] | Zwiększa obrażenia Kuli Energii o 10%/17.5%/25% |  | Drzewka!A126:G126 |
| T1:2 | Mistrzostwo Podstaw [1] | Zmniejsza koszt many wszystkich umiejętności o 5 |  | Drzewka!A127:G127 |
| T1:3 | Defensywna Moc [3] | Zwiększa zdrowie o 0.1/0.3/0.5 za każdy punkt ataku |  | Drzewka!A128:G128 |
| T1:4 | Nadludzkie Poświęcenie [3] | Zwiększa efekt pozytywny oraz negatywny Ofiary do 35%/40%/50% | Wymaga: T1:5 | Drzewka!A129:G129 |
| T1:5 | Ofiara [Toggle] | Zwiększa obrażenia zadawane i przyjmowane o 30% |  | Drzewka!A130:G130 |
| T2:1 | Magiczna Oszczędność [1] | Zmniejsza koszty many o 20% poniżej 50% zdrowia |  | Drzewka!A131:G131 |
| T2:2 | Oszczędzanie Energii [2] | Zwiększa regenerację many o 1%/2% za każdy brakujący % many | Wymaga: T1:2 | Drzewka!A132:G132 |
| T2:3 | Skupienie Magii [35 PM, 40% PA] | Przez następne 3 tury zadajesz zawsze największe obrażenia; | Cel: Rzucający | Drzewka!A133:G133 |
| T2:4 | Nauka Spokoju [2] | Zmniejsza negatywny efekt Ofiary o 10%/20% | Wymaga: T1:4 | Drzewka!A134:G134 |
| T2:5 | Poświęcenie [40 PM, 40% PA] | Zmniejsza obrażenia wybranego sojusznika o 20%; zwiększając swoje obrażenia o 20% na 3 tury; | Cel: Jeden sojusznik, Rzucający; Zasięg: 8 | Drzewka!A135:G135 |
| T3:1 | Poczucie Energii [2] | Każdy przeciwnik odległy o maksymalnie 5 pól daje regenerację zdrowia 0.5%/1% na turę, maksymalnie do 1.5%/3% |  | Drzewka!A136:G136 |
| T3:2 | Moc poza Granice [2] | Pozwala na regenerację many 15%/30% poza maksymalną ilość | Wymaga: T2:2 | Drzewka!A137:G137 |
| T3:3 | Mistrzowska Koncentracja [1] | Zwiększa czas trwania Skupienia Magii do 5 tur | Wymaga: T2:3 | Drzewka!A138:G138 |
| T3:4 | Trening Pamięciowy [2] | Zwiększa ilość maksymalnych umiejętności o 1/2 |  | Drzewka!A139:G139 |
| T3:5 | Masowe Poświęcenie [2] | Możesz użyć Poświęcenia na 2/3 sojusznikach | Wymaga: T2:5 | Drzewka!A140:G140 |
| T4:1 | Adaptacja Energetyczna [2] | Otrzymanie obrażeń za co najmniej 20% maksymalnego zdrowia zmniejsza obrażenia 3 następnych ataków od tego Celu o 10%/20% obrażeń zadanych podczas pierwotnego ciosu |  | Drzewka!A141:G141 |
| T4:2 | Wyładowanie Energii [3] | Jeśli masz powyżej maksymalnej ilości many, obrażenia następnego ataku mają szansę na bycie ciosem krytycznym. Szansa wynosi 0.2%/0.6%/1% za każdy nadmierny punkt many i wszystkie są zużywane | Wymaga: T3:2 | Drzewka!A142:G142 |
| T4:3 | Roztrojenie Energii [35 PM, 70% PA] | Zadaje 70% losowych obrażeń magicznych | Cel: Linia prosto i 2 na ukos; Zasięg: 8 | Drzewka!A143:G143 |
| T4:4 | Oszczędność Mocy [2] | Zwiększa obrażenia Roztrojenia Energii do 85%/100% | Wymaga: T4:3 | Drzewka!A144:G144 |
| T4:5 | Rytuał [50 PM, 30% PA] | Zadaje wszystkim członkom drużyny 10% ich maksymalnego zdrowia w zamian za regenerację 30% w ciągu 5 tur; | Cel: Wszyscy sojusznicy, Rzucający | Drzewka!A145:G145 |

### Mag Ognia

Pasyw drzewka: +2% Zadawanych obrażeń od ognia. +1% Odporności na ogień

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Paląca Aura [2] | Zwiększa obrażenia Ognistej Aury o 15%/30% | Wymaga: T1:2 | Drzewka!A157:G157 |
| T1:2 | Ognista Aura [65 PM, 45% PA] | Tworzy aurę ognia o zasięgu 1 wokół celu z [50%] podpalenie 90% | Cel: Jeden sojusznik; Zasięg: 6 | Drzewka!A158:G158 |
| T1:3 | Podstawy Piromanii [2] | Zwiększa szansę na podpalenie od Ognistej Aury i Kuli Ognia o 10%/25% | Wymaga: T1:2 | Drzewka!A159:G159 |
| T1:4 | Rozpraszający Ogień [2] | Podpaleni przeciwnicy zadają o 10%/20% mniej obrażeń |  | Drzewka!A160:G160 |
| T1:5 | Wypalanie Ran [2] | Podpalanie przeciwników zwiększa zadawane im obrażenia o 10%/20% |  | Drzewka!A161:G161 |
| T2:1 | Ognista Adrenalina [1] | Zwiększa szybkość podczas bycia podpalonym o 15 | Wymaga: T2:2 | Drzewka!A162:G162 |
| T2:2 | Ognista Ochrona [2] | Zwiększa obronę fizyczną oraz magiczną podczas bycia podpalonym o 20%/50% |  | Drzewka!A163:G163 |
| T2:3 | Agresywny Ogień [1] | Podpalenia mają 50% na zduplikowanie się na przeciwnikach w zasięgu 1 od podpalonego z 50% siły pierwotnego podpalenia, maksymalnie do trzech pokoleń kopii |  | Drzewka!A164:G164 |
| T2:4 | Żywienie Ogniem [2] | Każde gasnące podpalenie na polu bitwy przywraca 10%/20% zadanych obrażeń jako zdrowie |  | Drzewka!A165:G165 |
| T2:5 | Arkana Ognia [3] | Zwiększa obrażenia wszystkich podpaleń o 10%/20%/30% |  | Drzewka!A166:G166 |
| T3:1 | Odporność na Ogień [1] | Zmniejsza obrażenia otrzymywane od podpaleń o 25% i samopodpaleń o 50% | Wymaga: T3:2 | Drzewka!A167:G167 |
| T3:2 | Ognista Ofiara [3] | Podpalanie przeciwników daje 15%/30%/50% szansy na samopodpalenie (niepomniejszona przez odporność na ogień) | Wymaga: T2:2 | Drzewka!A168:G168 |
| T3:3 | Daleka Ekspansja [1] | Zwiększa zasięg Agresywnego Ognia do 3 | Wymaga: T2:3 | Drzewka!A169:G169 |
| T3:4 | Sangwinizm [2] | Jeśli podpalenie zadaje mniej niż 10% maksymalnego zdrowia przeciwnika na turę, jego siła zwiększa się o 15%/30% (może po tym nałożyć się Choleryzm jeśli siła wzrośnie powyżej 20% maksymalnego zdrowia) | Wymaga: T3:5 | Drzewka!A170:G170 |
| T3:5 | Choleryzm [2] | Jeśli podpalenie zadaje więcej niż 10% maksymalnego zdrowia przeciwnika na turę, ma 10%/25% szansy na ogłuszenie na 2 tury w każdej turze | Wymaga: T2:5 | Drzewka!A171:G171 |
| T4:1 | Ognista Strzała [40 PM, 60% PA] | Zadaje losowe obrażenia magiczne z penetracją magiczną 30%, [70%] podpalenie 90% na 2 tury, zmniejsza obrażenia o 15% na 4 tury | Cel: Linia wrogów; Zasięg: 6 | Drzewka!A172:G172 |
| T4:2 | Przeszycie [2] | Zwiększa magiczną penetrację Ognistej Strzały do 50%/70% | Wymaga: T4:1 | Drzewka!A173:G173 |
| T4:3 | Przewodnictwo Ognia [2] | Podpaleni przeciwnicy otrzymują 7%/15% dodatkowych obrażeń nieuchronnych |  | Drzewka!A174:G174 |
| T4:4 | Odkrycie Słabości [2] | Podpaleni przeciwnicy mają 15%/40% na otrzymanie obrażeń krytycznych | Wymaga: T4:3 | Drzewka!A175:G175 |
| T4:5 | Podniszczanie [2] | Atakowanie przeciwników zmniejsza ich odporność na ogień o 5%/10% do końca bitwy, nakłada się do 5 razy |  | Drzewka!A176:G176 |

### Kapłan

Pasyw drzewka: +1% Siły leczenia. +1% uleczonych obrażeń jest też przywracane Rzucającemu

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Grupowe Leczenie [35 MP, 60% PA] | 50% Leczenie | Cel: Wszyscy sojusznicy | Drzewka!A188:G188 |
| T1:2 | Siła Leczenia [2] | Zwiększa siłę Uzdrowienia oraz Grupowego Leczenia o 15%/30% | Wymaga: T1:1 | Drzewka!A189:G189 |
| T1:3 | Aura Leczenia [2] | Zapewnia 7%/15% leczenie przyległym sojusznikom w każdej turze |  | Drzewka!A190:G190 |
| T1:4 | Łańcuch Leczenia [1] | Każdy kolejny przyległy sojusznik przedłuża obszar działania Aury Leczenia do siebie | Wymaga: T1:3 | Drzewka!A191:G191 |
| T1:5 | Święte Artefakty [1] | Obiekty stworzone przez gracza (sztylety, runy, obiekty natury, etc.) liczone są do Łańcucha Leczenia | Wymaga: T1:4 | Drzewka!A192:G192 |
| T2:1 | Zapał Bitewny [1] | Grupowe Leczenie zwiększa szybkość o 10 na jedną turą, a potem o 5 na jedną turę | Wymaga: T1:1 | Drzewka!A193:G193 |
| T2:2 | Wszechstronna Łaska [3] | Grupowe Leczenie zwiększa obronę fizyczną i magiczną i unik na 2 tury o 2%/5%/10% | Wymaga: T2:1 | Drzewka!A194:G194 |
| T2:3 | Niespodziewana Łaska [3] | Daje szansę na leczenie krytyczne umiejętnościom leczniczym równą 50%/100%/150% szansy krytycznej |  | Drzewka!A195:G195 |
| T2:4 | Krytyczne Skupienie [2] | Leczenia obejmujące tylko jedną osobę mają 50%/100% większą szansę na leczenie krytyczne | Wymaga: T2:3, T2:5 | Drzewka!A196:G196 |
| T2:5 | Skupienie Uzdrowiciela [2] | Leczenia obejmujące tylko jedną osobę mają zwiększoną siłę o 15%/30% |  | Drzewka!A197:G197 |
| T3:1 | Pośpiech [3] | Przywraca 15%/25%/40% zdrowia odnawianego przez Świętą Regenerację od razu po użyciu | Wymaga: T3:2 | Drzewka!A198:G198 |
| T3:2 | Święta Regeneracja [50 PM, 55% PA] | Regeneruje 40% zdrowia sojusznika przez 4 tury | Cel: Jeden sojusznik; Zasięg: 8 | Drzewka!A199:G199 |
| T3:3 | Namaszczony Wojownik [2] | Każdy leczniczy cios krytyczny zwiększa obrażenia uleczonego celu o 10%/20% na 2 tury | Wymaga: T2:3 | Drzewka!A200:G200 |
| T3:4 | Wytrwały Uzdrowiciel [3] | Zmniejsza koszt punktów akcji Uzdrowienia o 25%/50%/100%, jeśli cel otrzymał obrażenia większe niż 20% maksymalnego zdrowia w jednej turze. Użycie Uzdrowienia na tym celu zdejmuje zniżkę. |  | Drzewka!A201:G201 |
| T3:5 | Pokrzepienie [1] | Leczenia obejmujące tylko jedną osobę przyśpieszają jej akcję o 20% |  | Drzewka!A202:G202 |
| T4:1 | Trwały Efekt [2] | Święta Regeneracja po upłynięciu czasu trwania leczy 25% początkowej wartości przez kolejne 4/2 tury | Wymaga: T3:1 | Drzewka!A203:G203 |
| T4:2 | Zbawienie [1] | Rzucający otrzymuje dodatkowo 50% odnowionego surowca przez Łaskę | Wymaga: T4:3 | Drzewka!A204:G204 |
| T4:3 | Kolekcja Łask [2] | Otrzymywanie i zadawanie obrażeń pod działaniem Błogosławieństwa daje ładunek Łaski (max 6), po upłynięciu czasu trwania, cel regeneruje 4/8 surowca za każdy ładunek | Wymaga: T4:4 | Drzewka!A205:G205 |
| T4:4 | Błogosławieństwo [40 PM, 40% PA] | Zwiększa zadawane obrażenia o 10%, zmniejsza przyjmowane obrażenia o 10% na 4 tury | Cel: Jeden sojusznik; Zasięg: b7w3 | Drzewka!A206:G206 |
| T4:5 | Silne Błogosławieństwo [2] | Wzmacnia efekt Błogosławieństwa do 15%/20% | Wymaga: T4:4 | Drzewka!A207:G207 |

### Krzyżowiec

Pasyw drzewka: +1% Obrażeń przeciwko Demonom i Nieumarłym. —

| Pozycja | Nazwa / rangi lub koszt aktywnego | Działanie z arkusza | Wymaganie / dodatkowe dane | Źródło |
| --- | --- | --- | --- | --- |
| T1:1 | Pole Osądu [35 MP, 60% PA] | Tworzy na 4 tury obszar, w którym przeciwnicy są spowolnieni o 5 i otrzymują 50% losowych obrażeń magicznych co turę | Cel: Wybrane pole, kwadrat 2; Zasięg: 4 | Drzewka!A219:G219 |
| T1:2 | Zasięg Osądu [2] | Zwiększa obszar Pola Osądu o 1/2 | Wymaga: T1:1 | Drzewka!A220:G220 |
| T1:3 | Grabarz [2] | Zwiększa obrażenia zadawane Nieumarłym o 15%/30% |  | Drzewka!A221:G221 |
| T1:4 | Nienawiść Przeklętych [1] | Atakowanie Nieumarłych i Demonów ma 50% szansy na nałożenie prowokacji na 1 turę | Wymaga: T1:3, T1:5 | Drzewka!A222:G222 |
| T1:5 | Egzorcysta [2] | Zwiększa obrażenia zadawane Demonom o 15%/30% |  | Drzewka!A223:G223 |
| T2:1 | Wydanie Osądu [3] | Po ukończeniu czasu działania Pole Osądu zadaje przeciwnikom w nim 50/100/150% losowych obrażeń magicznych | Wymaga: T1:1 | Drzewka!A224:G224 |
| T2:2 | Oślepiające Światło [60 MP, 75% PA] | Zadaje 60% losowych obrażeń magicznych, [85%] Celność -15 3t | Cel: Ciąg wrogów [3]; Zasięg: b5w2 | Drzewka!A225:G225 |
| T2:3 | Odsłonięcie Słabości [2] | Przeciwnicy objęci Oślepiającym Światłem otrzymują 25%/50% więcej obrażeń z następnego ataku | Wymaga: T2:2 | Drzewka!A226:G226 |
| T2:4 | Przewaga [1] | Zwiększa obrażenia zadawane przeciwnikom o mniejszej ilości maksymalnego zdrowia o 1% za każde 2% różnicy (max 50%) |  | Drzewka!A227:G227 |
| T2:5 | Srogi Wyrok [2] | Wyrok ma 20%/40% szansy na odepchnięcie przeciwnika o 1 pole |  | Drzewka!A228:G228 |
| T3:1 | Wyczerpanie [2] | Pole Osądu w przedostatniej turze działania ogłusza na 1/2 tury | Wymaga: T2:1 | Drzewka!A229:G229 |
| T3:2 | Zaślepienie [1] | Oślepiające Światło ma 90% szansy na ogłuszenie na 3 tury, każdy kolejny cel ma 20% mniejszą szansę na zostanie ogłuszonym | Wymaga: T2:2 | Drzewka!A230:G230 |
| T3:3 | Nękanie [3] | Zwiększa maksymalną ilość Udręczenia do 6/8/10 | Wymaga: T3:4 | Drzewka!A231:G231 |
| T3:4 | Udręczenie [1] | Ataki nakładają Udręczenie, które zmniejsza obronę o 2% i unik o 2, do 4 razy |  | Drzewka!A232:G232 |
| T3:5 | Surowy Sędzia [1] | Ciosy krytyczne zwiększają szansę na Srogi Wyrok o 40% i zwiększają siłę odepchnięcia o 1 | Wymaga: T2:5 | Drzewka!A233:G233 |
| T4:1 | Fala Uziemienia [65 MP, 75% PA] | Przyciąga o 1 w kierunku środku obszaru działania | Cel: Wybrane pole, kwadrat 5; Zasięg: 6 | Drzewka!A234:G234 |
| T4:2 | Fala Zniszczenia [1] | Zwiększa siłę przyciągnięcia Fali Uziemienia o 1 | Wymaga: T4:1 | Drzewka!A235:G235 |
| T4:3 | Kopanie Leżącego [3] | Zwiększa obrażenia zadawane ogłuszonym przeciwnikom o 15%/30%/50% |  | Drzewka!A236:G236 |
| T4:4 | Zadręczenie [3] | Nałożenie co najmniej 4 Udręczenia sprawia że przeciwnik otrzymuje dodatkowe obrażenia nieuchronne o wartości 30%/60%/100% obrażeń które zmitygowałaby zabrana obrona | Wymaga: T3:4 | Drzewka!A237:G237 |
| T4:5 | Kontrola [2] | Zwiększa szansę na cios krytyczny o 50%/100%[p] jeśli przeciwnik ma mniej aktualnego zdrowia | Wymaga: T3:5 | Drzewka!A238:G238 |

## Przeciwnicy — VS-15/16

| Nazwa | Trzy umiejętności (pełne parametry w źródle) | Źródło | Stan |
| --- | --- | --- | --- |
| Szkielet Miecznik | Kościany Miecz; Cięcie Krzyżowe; Kościany Zapał | Przeciwnicy!X14:AB14 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Umarlak | Maniakalne Ugryzienie; Wymiociny; Zapach Krwi | Przeciwnicy!X15:AB15 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Upiór | Nastraszenie; Dematerializacja; Materializacja | Przeciwnicy!X16:AB16 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Szkielet Okultysta | Starożytna Inkantacja; Wyssanie Życia; Starożytny Bicz | Przeciwnicy!X17:AB17 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Szkielet Łucznik | Łuk z Kości; Podpalona Strzała; Kołczan Zawiści | Przeciwnicy!X18:AB18 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Ghul | Rozszarpanie; Dziki Ryk; Dezorientujący Skok | Przeciwnicy!X19:AB19 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Szkielet Obrońca | Kościana Maczuga; Ferwor Bitewny; Kościany Obrońca | Przeciwnicy!X20:AB20 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |
| Duch Umarłego Króla | Królewskie Cięcie; Straż Przyboczna; Zemsta Zaświatów | Przeciwnicy!X21:AB21 | Do authoringu; CD_Skeleton_Test/CD_Boss_Test to analogi |

Statystyki źródłowe: Przeciwnicy!A2:V9. Boss nie jest jednym z siedmiu zwykłych mobów.

## Materiały — VS-10/12/13

| Nazwa | Poziom | Receptura / źródło w arkuszu | Komórka | Kandydat reuse |
| --- | --- | --- | --- | --- |
| Ruda żelaza | 1 | — | Materiały!A2 | `Items/Materials/Smithing/ID_IronOre.asset` |
| Ruda niklu | 1 | — | Materiały!A3 | `Items/Materials/Smithing/ID_NickelOre.asset` |
| Ruda miedzi | 1 | — | Materiały!A4 | `Items/Materials/Smithing/ID_CopperOre.asset` |
| Ruda cyny | 1 | — | Materiały!A5 | `Items/Materials/Smithing/ID_TinOre.asset` |
| Węgiel | 11 | — | Materiały!A6 | `Items/Materials/Smithing/ID_Coal.asset` |
| Chrom | 11 | — | Materiały!A7 | `Items/Materials/Smithing/ID_Chromium.asset` |
| Odłamek meteorytu | 11 | — | Materiały!A8 | `Items/Materials/Smithing/ID_MeteoriteShard.asset` |
| Kawałek cynku | 11 | — | Materiały!A9 | `Items/Materials/Smithing/ID_ZincPiece.asset` |
| Sztabka żelaza | 1 | 2x Ruda żelaza -> 1x Sztabka żelaza | Materiały!A10 | `Items/Materials/Smithing/CID_IronIngot.asset` |
| Sztabka niklu | 1 | 2x Ruda niklu -> 1x Sztabka niklu | Materiały!A11 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka miedzi | 1 | 2x Ruda miedzi -> 1x Sztabka miedzi | Materiały!A12 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka cyny | 1 | 2x Ruda cyny -> 1x Sztabka cyny | Materiały!A13 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka stali | 11 | 2x Ruda żelaza + 1x Węgiel -> 1x Sztabka stali | Materiały!A14 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka inwaru | 11 | 1x Ruda żelaza + 1x Ruda niklu + 1x Chrom -> 1x Sztabka inwaru | Materiały!A15 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka żelaza meteorytowego | 11 | 2x Ruda żelaza + 1x Odłamek meteorytu -> 1x Sztabka żelaza meteorytowego | Materiały!A16 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sztabka brązu | 11 | 2x Ruda miedzi + 1x Ruda cyny + 1x Kawałek cynku -> 1x Sztabka brązu | Materiały!A17 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka żelaza | 1 | 6x Ruda żelaza -> 1x Wielka sztabka żelaza | Materiały!A18 | `Items/Materials/Smithing/CID_HugeIronIngot.asset` |
| Wielka sztabka niklu | 1 | 6x Ruda niklu -> 1x Wielka sztabka niklu | Materiały!A19 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka miedzi | 1 | 6x Ruda miedzi -> 1x Wielka sztabka miedzi | Materiały!A20 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka cyny | 1 | 6x Ruda cyny -> 1x Wielka sztabka cyny | Materiały!A21 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka stali | 11 | 6x Ruda żelaza + 3x Węgiel -> 1x Wielka sztabka stali | Materiały!A22 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka inwaru | 11 | 3x Ruda żelaza + 3x Ruda niklu + 3x Chrom -> 1x Wielka sztabka inwaru | Materiały!A23 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka żelaza meteorytowego | 11 | 6x Ruda żelaza + 3x Odłamek meteorytu -> 1x Wielka sztabka żelaza meteorytowego | Materiały!A24 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wielka sztabka brązu | 11 | 9x Ruda miedzi + 3x Ruda cyny + 3x Kawałek cynku -> 1x Wielka sztabka brązu | Materiały!A25 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowane żelazo | 1 | 1x Sztabka żelaza -> 2x Sproszkowane żelazo | Materiały!A26 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowany nikiel | 1 | 1x Sztabka niklu -> 2x Sproszkowany nikiel | Materiały!A27 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowana miedź | 1 | 1x Sztabka miedzi -> 2x Sproszkowana miedź | Materiały!A28 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowana cyna | 1 | 1x Sztabka cyny -> 2x Sproszkowana cyna | Materiały!A29 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowana stal | 11 | 1x Sztabka stali -> 2x Sproszkowana stal | Materiały!A30 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowany inwar | 11 | 1x Sztabka inwaru -> 2x Sproszkowany inwar | Materiały!A31 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowane żelazo meteorytowe | 11 | 1x Sztabka żelaza meteorytowego -> 2x Sproszkowane żelazo meteorytowe | Materiały!A32 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sproszkowany brąz | 11 | 1x Sztabka brązu -> 2x Sproszkowany brąz | Materiały!A33 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Żelazny łańcuch | 1 | 1x Sztabka żelaza -> 1x Żelazny łańcuch | Materiały!A34 | `Items/Materials/Smithing/CID_IronChain.asset` |
| Niklowy łańcuch | 1 | 1x Sztabka niklu -> 1x Niklowy łańcuch | Materiały!A35 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Miedziany łańcuch | 1 | 1x Sztabka miedzi -> 1x Miedziany łańcuch | Materiały!A36 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cynowy łańcuch | 1 | 1x Sztabka cyny -> 1x Cynowy łańcuch | Materiały!A37 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Stalowy łańcuch | 11 | 1x Sztabka stali -> 1x Stalowy łańcuch | Materiały!A38 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Inwarowy łańcuch | 11 | 1x Sztabka inwaru -> 1x Inwarowy łańcuch | Materiały!A39 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Meteorytowy łańcuch | 11 | 1x Sztabka żelaza meteorytowego -> 1x Meteorytowy łańcuch | Materiały!A40 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Brązowy łańcuch | 11 | 1x Sztabka brązu -> 1x Brązowy łańcuch | Materiały!A41 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Żelazna blacha | 1 | 2x Sztabka żelaza -> 1x Żelazna blacha | Materiały!A42 | `Items/Materials/Smithing/CID_IronPlate.asset` |
| Niklowa blacha | 1 | 2x Sztabka niklu -> 1x Niklowa blacha | Materiały!A43 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Miedziana blacha | 1 | 2x Sztabka miedzi -> 1x Miedziana blacha | Materiały!A44 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cynowa blacha | 1 | 2x Sztabka cyny -> 1x Cynowa blacha | Materiały!A45 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Stalowa blacha | 11 | 2x Sztabka stali -> 1x Stalowa blacha | Materiały!A46 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Inwarowa blacha | 11 | 2x Sztabka inwaru -> 1x Inwarowa blacha | Materiały!A47 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Meteorytowa blacha | 11 | 2x Sztabka meteorytowego żelaza -> 1x Meteorytowa blacha | Materiały!A48 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Brązowa blacha | 11 | 2x Sztabka brązu -> 1x Brązowa blacha | Materiały!A49 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Skóra kozy | 1 | Kowale, Koścista torba zasobów, Pustynna torba zasobów | Materiały!A50 | `Items/Materials/Smithing/ID_GoatSkin.asset` |
| Skóra dzikiego psa | 1 | Kowale, Morska torba zasobów, Leśna torba zasobów | Materiały!A51 | `Items/Materials/Smithing/ID_WildDogSkin.asset` |
| Skóra królika | 1 | Kowale, Górska torba zasobów, Bagienna torba zasobów | Materiały!A52 | `Items/Materials/Smithing/ID_RabbitSkin.asset` |
| Skóra lisa | 1 | Kowale, Demoniczna torba zasobów, Świątynna torba zasobów | Materiały!A53 | `Items/Materials/Smithing/ID_FoxSkin.asset` |
| Skóra tygrysa | 11 | Kowale, Demoniczna torba zasobów, Bagienna torba zasobów | Materiały!A54 | `Items/Materials/Smithing/ID_TigerSkin.asset` |
| Skóra jelenia | 11 | Kowale, Koścista torba zasobów, Leśna torba zasobów | Materiały!A55 | `Items/Materials/Smithing/ID_DeerSkin.asset` |
| Skóra dzika | 11 | Kowale, Morska torba zasobów, Pustynna torba zasobów | Materiały!A56 | `Items/Materials/Smithing/ID_BoarSkin.asset` |
| Skóra warana | 11 | Kowale, Świątynna torba zasobów, Górska torba zasobów | Materiały!A57 | `Items/Materials/Smithing/ID_MonitorSkin.asset` |
| Wyprawiona skóra kozy | 1 | 1x Skóra kozy -> 1x Wyprawiona skóra kozy | Materiały!A58 | `Items/Materials/Smithing/CID_GoatHide.asset` |
| Wyprawiona skóra dzikiego psa | 1 | 2x Skóra dzikiego psa -> 1x Wyprawiona skóra dzikiego psa | Materiały!A59 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra królika | 1 | 3x Skóra królika -> 1x Wyprawiona skóra królika | Materiały!A60 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra lisa | 1 | 2x Skóra lisa -> 1x Wyprawiona skóra lisa | Materiały!A61 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra tygrysa | 11 | 1x Skóra tygrysa -> 3x Wyprawiona skóra tygrysa | Materiały!A62 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra jelenia | 11 | 1x Skóra jelenia -> 3x Wyprawiona skóra jelenia | Materiały!A63 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra dzika | 11 | 1x Skóra dzika -> 2x Wyprawiona skóra dzika | Materiały!A64 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wyprawiona skóra warana | 11 | 1x Skóra warana -> 1x Wyprawiona skóra warana | Materiały!A65 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry kozy | 1 | 1x Wyprawiona skóra kozy -> 2x Paski ze skóry kozy | Materiały!A66 | `Items/Materials/Smithing/CID_GoatHideStrips.asset` |
| Paski ze skóry dzikiego psa | 1 | 1x Wyprawiona skóra dzikiego psa -> 2x Paski ze skóry dzikiego psa | Materiały!A67 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry królika | 1 | 1x Wyprawiona skóra królika -> 2x Paski ze skóry królika | Materiały!A68 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry lisa | 1 | 1x Wyprawiona skóra lisa -> 2x Paski ze skóry lisa | Materiały!A69 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry tygrysa | 11 | 1x Wyprawiona skóra tygrysa -> 2x Paski ze skóry tygrysa | Materiały!A70 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry jelenia | 11 | 1x Wyprawiona skóra jelenia -> 2x Paski ze skóry jelenia | Materiały!A71 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry dzika | 11 | 1x Wyprawiona skóra dzika -> 2x Paski ze skóry dzika | Materiały!A72 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Paski ze skóry warana | 11 | 1x Wyprawiona skóra warana -> 2x Paski ze skóry warana | Materiały!A73 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Drewno modrzewiowe | 1 | Kowale, Koścista torba zasobów, Pustynna torba zasobów | Materiały!A74 | `Items/Materials/Smithing/ID_LarchWood.asset` |
| Drewno lipowe | 1 | Kowale, Morska torba zasobów, Leśna torba zasobów | Materiały!A75 | `Items/Materials/Smithing/ID_LindenWood.asset` |
| Drewno topolowe | 1 | Kowale, Górska torba zasobów, Bagienna torba zasobów | Materiały!A76 | `Items/Materials/Smithing/ID_PoplarWood.asset` |
| Drewno olchowe | 1 | Kowale, Demoniczna torba zasobów, Świątynna torba zasobów | Materiały!A77 | `Items/Materials/Smithing/ID_AlderWood.asset` |
| Drewno głogowe | 11 | Kowale, Demoniczna torba zasobów, Bagienna torba zasobów | Materiały!A78 | `Items/Materials/Smithing/ID_HawthornWood.asset` |
| Drewno brzozowe | 11 | Kowale, Koścista torba zasobów, Leśna torba zasobów | Materiały!A79 | `Items/Materials/Smithing/ID_BirchWood.asset` |
| Drewno cyprysowe | 11 | Kowale, Morska torba zasobów, Pustynna torba zasobów | Materiały!A80 | `Items/Materials/Smithing/ID_CypressWood.asset` |
| Drewno cedrowe | 11 | Kowale, Świątynna torba zasobów, Górska torba zasobów | Materiały!A81 | `Items/Materials/Smithing/ID_CedarWood.asset` |
| Modrzewiowa deska | 1 | 1x Drewno modrzewiowe -> 2x Modrzewiowa deska | Materiały!A82 | `Items/Materials/Smithing/CID_LarchPlank.asset` |
| Lipowa deska | 1 | 1x Drewno lipowe -> 2x Lipowa deska | Materiały!A83 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Topolowa deska | 1 | 1x Drewno topolowe -> 2x Topolowa deska | Materiały!A84 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Olchowa deska | 1 | 1x Drewno olchowe -> 2x Olchowa deska | Materiały!A85 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Głogowa deska | 11 | 1x Drewno głógowe -> 2x Głógowa deska | Materiały!A86 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Brzozowa deska | 11 | 1x Drewno brzozowe -> 2x Brzozowa deska | Materiały!A87 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cyprysowa deska | 11 | 1x Drewno cyprysowe -> 2x Cyprysowa deska | Materiały!A88 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cedrowa deska | 11 | 1x Drewno cedrowe -> 2x Cedrowa deska | Materiały!A89 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Modrzewiowy patyk | 1 | 3x Modrzewiowa deska -> 8x Modrzewiowy patyk | Materiały!A90 | `Items/Materials/Smithing/CID_LarchStick.asset` |
| Lipowy patyk | 1 | 3x Lipowa deska -> 8x Lipowy patyk | Materiały!A91 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Topolowy patyk | 1 | 3x Topolowa deska -> 8x Topolowy patyk | Materiały!A92 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Olchowy patyk | 1 | 3x Olchowa deska -> 8x Olchowy patyk | Materiały!A93 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Głogowy patyk | 11 | 3x Głógowa deska -> 8x Głógowy patyk | Materiały!A94 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Brzozowy patyk | 11 | 3x Brzozowa deska -> 8x Brzozowy patyk | Materiały!A95 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cyprysowy patyk | 11 | 3x Cyprysowa deska -> 8x Cyprysowy patyk | Materiały!A96 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cedrowy patyk | 11 | 3x Cedrowa deska -> 8x Cedrowy patyk | Materiały!A97 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Słaba igła do nici | 1 | 1x Sztabka żelaza -> 3x Słaba igła od nici / 1x Sztabka niklu -> 3x Słaba igła od nici / 1x Sztabka miedzi -> 3x Słaba igła od nici / 1x Sztabka cyny -> 3x Słaba igła od nici | Materiały!A98 | `Items/Materials/Smithing/CID_WeakThreadNeedle.asset` |
| Igła do nici | 11 | 1x Sztabka stali -> 3x Igła od nici / 1x Sztabka inwaru -> 3x Igła od nici / 1x Sztabka żelaza meteorytowego -> 3x Igła od nici / 1x Sztabka brązu -> 3x Igła od nici | Materiały!A99 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Włókno bawełny | 1 | Kowale, Koścista torba zasobów, Pustynna torba zasobów | Materiały!A100 | `Items/Materials/Smithing/ID_CottonFibre.asset` |
| Włókno bukominy | 1 | Kowale, Morska torba zasobów, Leśna torba zasobów | Materiały!A101 | `Items/Materials/Smithing/ID_BukominaFibre.asset` |
| Włókno koziej wełny | 1 | Kowale, Górska torba zasobów, Bagienna torba zasobów | Materiały!A102 | `Items/Materials/Smithing/ID_GoatWoolFibre.asset` |
| Włókno juty | 1 | Kowale, Demoniczna torba zasobów, Świątynna torba zasobów | Materiały!A103 | `Items/Materials/Smithing/ID_JuteFibre.asset` |
| Włókno owczej wełny | 11 | Kowale, Demoniczna torba zasobów, Bagienna torba zasobów | Materiały!A104 | `Items/Materials/Smithing/ID_SheepWoolFibre.asset` |
| Włókno palmy | 11 | Kowale, Koścista torba zasobów, Leśna torba zasobów | Materiały!A105 | `Items/Materials/Smithing/ID_PalmFibre.asset` |
| Włókno lamiej wełny | 11 | Kowale, Morska torba zasobów, Pustynna torba zasobów | Materiały!A106 | `Items/Materials/Smithing/ID_LlamaWoolFibre.asset` |
| Włókno wasminy | 11 | Kowale, Świątynna torba zasobów, Górska torba zasobów | Materiały!A107 | `Items/Materials/Smithing/ID_WasminaFibre.asset` |
| Bawełniana nitka | 1 | 1x Włókno bawełny -> 2x Bawełniana nitka | Materiały!A108 | `Items/Materials/Smithing/CID_CottonThread.asset` |
| Bukominowa nitka | 1 | 1x Włókno bukominy -> 2x Bukominowa nitka | Materiały!A109 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Nitka z koziej wełny | 1 | 1x Włókno koziej wełny -> 2x Nitka z koziej wełny | Materiały!A110 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jutowa nitka | 1 | 1x Włókno juty -> 2x Jutowa nitka | Materiały!A111 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Nitka z owczej wełny | 11 | 1x Włókno owczej wełny -> 2x Nitka z owczej wełny | Materiały!A112 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Palmowa nitka | 11 | 1x Włókno palmy -> 2x Palmowa nitka | Materiały!A113 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Nitka z lamiej wełny | 11 | 1x Włókno lamiej wełny -> 2x Nitka z lamiej wełny | Materiały!A114 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wasminowa nitka | 11 | 1x Włókno wasminy -> 2x Wasminowa nitka | Materiały!A115 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Bawełniana tkanina | 1 | 8x Bawełniana nitka + 1x Słaba igła od nici -> 3x Bawełniana tkanina | Materiały!A116 | `Items/Materials/Smithing/CID_CottonFabric.asset` |
| Bukominowa tkanina | 1 | 8x Bukominowa nitka + 1x Słaba igła od nici -> 3x Bukominowa tkanina | Materiały!A117 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Tkanina z koziej wełny | 1 | 8x Nitka z koziej wełny + 1x Słaba igła od nici -> 3x Tkanina z koziej wełny | Materiały!A118 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Jutowa tkanina | 1 | 8x Jutowa nitka + 1x Słaba igła od nici -> 3x Jutowa tkanina | Materiały!A119 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Tkanina z owczej wełny | 11 | 8x Nitka z owczej wełny + 1x Igła od nici -> 3x Tkanina z owczej wełny | Materiały!A120 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Palmowa tkanina | 11 | 8x Palmowa nitka + 1x Igła od nici -> 3x Palmowa tkanina | Materiały!A121 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Tkanina z lamiej wełny | 11 | 8x Nitka z lamiej wełny + 1x Igła od nici -> 3x Tkanina z lamiej wełny | Materiały!A122 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wasminowa tkanina | 11 | 8x Wasminowa nitka + 1x Igła od nici -> 3x Wasminowa tkanina | Materiały!A123 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Kość szkieleta | 1 | 1-2 Szkielet Miecznik (10%), 1-2 Szkielet Łucznik (10%), 1-2 Szkielet Okultysta (10%), 1-2 Szkielet Obrońca (10%), Koścista torba zasobów | Materiały!A124 | `Items/Materials/Alchemy/ID_SkeletonBone.asset` |
| Mózg umarlaka | 1 | 1 Umarlak (30%), 1 Ghul (30%), Koścista torba zasobów | Materiały!A125 | `Items/Materials/Alchemy/ID_UndeadBrain.asset` |
| Ucho elfa | 1 | 1-2 Elfi Łucznik (15%), 1-2 Elfi Łupieżca (15%), 1-2 Elfi Druid (15%), Leśna torba zasobów | Materiały!A126 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Róg demona | 1 | 1 Demoniczny Zbrojny (10%), 1 Demoniczny Czarownik (10%), 1 Demoniczny Bandyta (10%), 1 Demoniczny Iluzjonista (10%), Demoniczna torba zasobów | Materiały!A127 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Oko pirata | 1 | 1 Pirat Kombatant (10%), 1 Pirat Harpuniarz (10%), 1 Pirat Konsyliarz (10%), 1 Pirat Kanonier (10%), Morska torba zasobów | Materiały!A128 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Pióro ptaka | 1 | 1-3 Piaszczysty Feniks (20%), 1-3 Papuga (20%), Morska torba zasobów | Materiały!A129 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Owoc kaktusa | 1 | 1-2 Awazar Buławnik (10%), 1-2 Awazar Łucznik (10%), 1-2 Awazar Medyk (10%), 1-2 Awazar Weteran (10%), Pustynna torba zasobów | Materiały!A130 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Spaczone serce | 1 | 1 Kapłan Malada (8%), 1 Czarny Rycerz Malada (8%), 1 Plugawiciel Malada (8%), 1 Strażnik Świątynny (8%), 1 Nekromanta Malada (8%), Świątynna torba zasobów | Materiały!A131 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Skruszona czaszka | 1 | 1 Włócznik Skruszonej Czaszki (20%), 1 Szaman Skruszonej Czaszki (20%), Górska torba zasobów | Materiały!A132 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Nerka trolla | 1 | 1 Troll Górski (40%), Górska torba zasobów | Materiały!A133 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Żyjący liść | 1 | 1-3 Mięsożerna Roślina (15%), 1-3 Pasożytnicza Roślina (15%), 1-3 Plująca Roślina (15%), Bagienna torba zasobów | Materiały!A134 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Kieł aligatora | 1 | 1-3 Aligator (20%), Bagienna torba zasobów | Materiały!A135 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Purchawka | 1 | Katakumby | Materiały!A148 | `Items/Materials/Alchemy/ID_Puffball.asset` |
| Kosarz pospolity | 1 | Katakumby | Materiały!A149 | `Items/Materials/Alchemy/ID_CommonReaper.asset` |
| Szyszka | 1 | Lasy | Materiały!A150 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Trawa dusicielska | 1 | Elfie Ruiny | Materiały!A151 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Wodorosty | 1 | Zatoki | Materiały!A152 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Kwitnąca wiśnia | 1 | Zatoki | Materiały!A153 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Aloe meda | 1 | Pustynie | Materiały!A154 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Pnącza alzif | 1 | Świątynie | Materiały!A155 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Kwiat byliny | 1 | Góry | Materiały!A156 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Dmuchawiec tregandzki | 1 | Góry | Materiały!A157 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Sitowie | 1 | Bagna | Materiały!A158 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Mucha zwyczajna | 1 | Bagna | Materiały!A159 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Zwykły | 1 | 1x Słaby alkohol + 1x Kosarz pospolity + 1x Kwitnąca wiśnia + 1x Kwiat byliny -> 1x | Materiały!A160 | `Items/Materials/Alchemy/CID_HerbalPowderNormal.asset` |
| Proch ziołowy: Grudkowaty | 1 | 1x Słaby alkohol + 1x Purchawka + 1x Szyszka + 1x Sitowie -> 1x | Materiały!A161 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Lepki | 1 | 1x Słaby alkohol + 1x Aloe meda + 1x Mucha zwyczajna + 1x Wodorosty -> 1x | Materiały!A162 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Luźny | 1 | 1x Słaby alkohol + 1x Pnącza alzif + 1x Dmuchawiec tregandzki + 1x Trawa dusicielska -> 1x | Materiały!A163 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Żółty | 11 | 2x Alkohol + 1x Kosarz pospolity + 1x Mucha zwyczajna + Dmuchawiec tregandzki -> 1x | Materiały!A164 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Zielony | 11 | 2x Alkohol + 1x Wodorosty + 1x Aloe meda + 1x Trawa dusicielska -> 1x | Materiały!A165 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Różowy | 11 | 2x Alkohol + 1x Kwitnąca wiśnia + 1x Pnącza alzif + 1x Kwiat byliny -> 1x | Materiały!A166 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Proch ziołowy: Pomarańczowy | 11 | 2x Alkohol + 1x Sitowie + 1x Szyszka + 1x Purchawka -> 1x | Materiały!A167 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Słaby alkohol | 1 | Wszystkie torby zasobów | Materiały!A168 | `Items/Materials/Alchemy/ID_AlcoholWeak.asset` |
| Alkohol | 11 | Wszystkie torby zasobów | Materiały!A169 | `Items/Materials/Alchemy/ID_AlcoholNormal.asset` |
| Słaby proch strzelniczy | 1 | Wszystkie torby zasobów | Materiały!A170 | `Items/Materials/Alchemy/ID_GunpowderWeak.asset` |
| Proch strzelniczy | 11 | Wszystkie torby zasobów | Materiały!A171 | `Items/Materials/Alchemy/ID_GunpowderNormal.asset` |
| Lity wapień | 11 | Wszystkie torby zasobów | Materiały!A172 | `Items/Materials/Runeforging/ID_SolidLimestone.asset` |
| Wapienny kamień galdurytu | 11 | 1x Lity wapień -> 2x Wapienny kamień galdurytu | Materiały!A173 | `Items/Materials/Runeforging/CID_LimestoneGalduryteStone.asset` |
| Granat | 11 | Koścista torba zasobów | Materiały!A174 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Szafir | 11 | Morska torba zasobów | Materiały!A175 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Ametyst | 11 | Świątynna torba zasobów | Materiały!A176 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cytryn | 11 | Pustynna torba zasobów | Materiały!A177 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Szmaragd | 11 | Leśna torba zasobów | Materiały!A178 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Onyks | 11 | Demoniczna torba zasobów | Materiały!A179 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Rubin | 11 | Bagienna torba zasobów | Materiały!A180 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Diament | 11 | Górska torba zasobów | Materiały!A181 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Topaz | 11 | Leśna torba zasobów, Demoniczna torba zasobów, Bagienna torba zasobów, Górska torba zasobów | Materiały!A182 | `Items/Materials/Runeforging/ID_Topaz.asset` |
| Bursztyn | 11 | Koścista torba zasobów, Morska torba zasobów, Pustynna torba zasobów, Świątynna torba zasobów | Materiały!A183 | `Items/Materials/Runeforging/ID_Amber.asset` |
| Czerwony proszek | 11 | 1x Granat -> 2x Czerwony proszek | Materiały!A184 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Niebieski proszek | 11 | 1x Szafir -> 2x Niebieski proszek | Materiały!A185 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Purpurowy proszek | 11 | 1x Ametyst -> 2x Purpurowy proszek | Materiały!A186 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cytrynowy proszek | 11 | 1x Cytryn -> 2x Cytrynowy proszek | Materiały!A187 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Szmaragdowy proszek | 11 | 1x Szmaragd -> 2x Szmaragdowy proszek | Materiały!A188 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Czarny proszek | 11 | 1x Onyks -> 2x Czarny proszek | Materiały!A189 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Różowy proszek | 11 | 1x Rubin -> 2x Różowy proszek | Materiały!A190 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Biały proszek | 11 | 1x Diament -> 2x Biały proszek | Materiały!A191 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Złoty proszek | 11 | 1x Topaz -> 2x Złoty proszek | Materiały!A192 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Bursztynowy proszek | 11 | 1x Bursztyn -> 2x Bursztynowy proszek | Materiały!A193 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Koścista torba zasobów | 1 | Potwory w katakumbach | Materiały!A194 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Torba Ducha Umarłego Króla | 1 | Duch Umarłego Króla | Materiały!A195 | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |

Poza passem (L21): Ektoplazma, Pazur ghula, Kieł wilka, Pozawymiarowa macka, Błyszczący piasek, Płetwa rekina, Żywioł wiatru, Starożytna skóra, Histyna, Pióro ruka, Ssawka komara, Płuco topielca.

## Części wyposażenia — VS-10

| Klasa | Przedmiot | Część | Materiał | Bazowy poziom | Źródło statystyk/kosztu |
| --- | --- | --- | --- | --- | --- |
| Wojownik | Długi Miecz | Głownia | Sztabka żelaza | 5 | Wyposażenie!A2:J2 |
| Wojownik | Kolczuga | Napierśnik | Sztabka żelaza | 5 | Wyposażenie!K2:S2 |
| Wojownik | Długi Miecz | Głownia | Sztabka niklu | 5 | Wyposażenie!A3:J3 |
| Wojownik | Kolczuga | Napierśnik | Sztabka niklu | 5 | Wyposażenie!K3:S3 |
| Wojownik | Długi Miecz | Głownia | Sztabka miedzi | 5 | Wyposażenie!A4:J4 |
| Wojownik | Kolczuga | Napierśnik | Sztabka miedzi | 5 | Wyposażenie!K4:S4 |
| Wojownik | Długi Miecz | Głownia | Sztabka cyny | 5 | Wyposażenie!A5:J5 |
| Wojownik | Kolczuga | Napierśnik | Sztabka cyny | 5 | Wyposażenie!K5:S5 |
| Wojownik | Długi Miecz | Głownia | Sztabka stali | 15 | Wyposażenie!A6:J6 |
| Wojownik | Kolczuga | Napierśnik | Sztabka stali | 15 | Wyposażenie!K6:S6 |
| Wojownik | Długi Miecz | Głownia | Sztabka inwaru | 15 | Wyposażenie!A7:J7 |
| Wojownik | Kolczuga | Napierśnik | Sztabka inwaru | 15 | Wyposażenie!K7:S7 |
| Wojownik | Długi Miecz | Głownia | Sztabka żelaza meteorytowego | 15 | Wyposażenie!A8:J8 |
| Wojownik | Kolczuga | Napierśnik | Sztabka żelaza meteorytowego | 15 | Wyposażenie!K8:S8 |
| Wojownik | Długi Miecz | Głownia | Sztabka brązu | 15 | Wyposażenie!A9:J9 |
| Wojownik | Kolczuga | Napierśnik | Sztabka brązu | 15 | Wyposażenie!K9:S9 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra kozy | 5 | Wyposażenie!A10:J10 |
| Wojownik | Kolczuga | Kolcza | Żelazny łańcuch | 5 | Wyposażenie!K10:S10 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!A11:J11 |
| Wojownik | Kolczuga | Kolcza | Niklowy łańcuch | 5 | Wyposażenie!K11:S11 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra królika | 5 | Wyposażenie!A12:J12 |
| Wojownik | Kolczuga | Kolcza | Miedziany łańcuch | 5 | Wyposażenie!K12:S12 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra lisa | 5 | Wyposażenie!A13:J13 |
| Wojownik | Kolczuga | Kolcza | Cynowy łańcuch | 5 | Wyposażenie!K13:S13 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra tygrysa | 15 | Wyposażenie!A14:J14 |
| Wojownik | Kolczuga | Kolcza | Stalowy łańcuch | 15 | Wyposażenie!K14:S14 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra jelenia | 15 | Wyposażenie!A15:J15 |
| Wojownik | Kolczuga | Kolcza | Inwarowy łańcuch | 15 | Wyposażenie!K15:S15 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra dzika | 15 | Wyposażenie!A16:J16 |
| Wojownik | Kolczuga | Kolcza | Meteorytowy łańcuch | 15 | Wyposażenie!K16:S16 |
| Wojownik | Długi Miecz | Jelec | Wyprawiona skóra warana | 15 | Wyposażenie!A17:J17 |
| Wojownik | Kolczuga | Kolcza | Brązowy łańcuch | 15 | Wyposażenie!K17:S17 |
| Wojownik | Długi Miecz | Rękojeść | Modrzewiowy patyk | 5 | Wyposażenie!A18:J18 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra kozy | 5 | Wyposażenie!K18:S18 |
| Wojownik | Długi Miecz | Rękojeść | Lipowy patyk | 5 | Wyposażenie!A19:J19 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!K19:S19 |
| Wojownik | Długi Miecz | Rękojeść | Topolowy patyk | 5 | Wyposażenie!A20:J20 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra królika | 5 | Wyposażenie!K20:S20 |
| Wojownik | Długi Miecz | Rękojeść | Olchowy patyk | 5 | Wyposażenie!A21:J21 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra lisa | 5 | Wyposażenie!K21:S21 |
| Wojownik | Długi Miecz | Rękojeść | Głógowy patyk | 15 | Wyposażenie!A22:J22 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra tygrysa | 15 | Wyposażenie!K22:S22 |
| Wojownik | Długi Miecz | Rękojeść | Brzozowy patyk | 15 | Wyposażenie!A23:J23 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra jelenia | 15 | Wyposażenie!K23:S23 |
| Wojownik | Długi Miecz | Rękojeść | Cyprysowy patyk | 15 | Wyposażenie!A24:J24 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra dzika | 15 | Wyposażenie!K24:S24 |
| Wojownik | Długi Miecz | Rękojeść | Ostrokrzewowy patyk | 15 | Wyposażenie!A25:J25 |
| Wojownik | Kolczuga | Skóra | Wyprawiona skóra warana | 15 | Wyposażenie!K25:S25 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka żelaza | 5 | Wyposażenie!A26:J26 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra kozy | 5 | Wyposażenie!K26:S26 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka niklu | 5 | Wyposażenie!A27:J27 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!K27:S27 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka miedzi | 5 | Wyposażenie!A28:J28 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra królika | 5 | Wyposażenie!K28:S28 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka cyny | 5 | Wyposażenie!A29:J29 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra lisa | 5 | Wyposażenie!K29:S29 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka stali | 15 | Wyposażenie!A30:J30 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra tygrysa | 15 | Wyposażenie!K30:S30 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka inwaru | 15 | Wyposażenie!A31:J31 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra jelenia | 15 | Wyposażenie!K31:S31 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka żelaza meteorytowego | 15 | Wyposażenie!A32:J32 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra dzika | 15 | Wyposażenie!K32:S32 |
| Zwiadowca | Miecz i Sztylet | Głownia | Sztabka brązu | 15 | Wyposażenie!A33:J33 |
| Zwiadowca | Tunika | Materiał | Wyprawiona skóra warana | 15 | Wyposażenie!K33:S33 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra kozy | 5 | Wyposażenie!A34:J34 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry kozy | 5 | Wyposażenie!K34:S34 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!A35:J35 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry dzikiego psa | 5 | Wyposażenie!K35:S35 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra królika | 5 | Wyposażenie!A36:J36 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry królika | 5 | Wyposażenie!K36:S36 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra lisa | 5 | Wyposażenie!A37:J37 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry lisa | 5 | Wyposażenie!K37:S37 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra tygrysa | 15 | Wyposażenie!A38:J38 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry tygrysa | 15 | Wyposażenie!K38:S38 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra jelenia | 15 | Wyposażenie!A39:J39 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry jelenia | 15 | Wyposażenie!K39:S39 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra dzika | 15 | Wyposażenie!A40:J40 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry dzika | 15 | Wyposażenie!K40:S40 |
| Zwiadowca | Miecz i Sztylet | Jelec | Wyprawiona skóra warana | 15 | Wyposażenie!A41:J41 |
| Zwiadowca | Tunika | Rzemienie | Paski ze skóry warana | 15 | Wyposażenie!K41:S41 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Modrzewiowy patyk | 5 | Wyposażenie!A42:J42 |
| Zwiadowca | Tunika | Umocnienia | Sztabka żelaza | 5 | Wyposażenie!K42:S42 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Lipowy patyk | 5 | Wyposażenie!A43:J43 |
| Zwiadowca | Tunika | Umocnienia | Sztabka niklu | 5 | Wyposażenie!K43:S43 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Topolowy patyk | 5 | Wyposażenie!A44:J44 |
| Zwiadowca | Tunika | Umocnienia | Sztabka miedzi | 5 | Wyposażenie!K44:S44 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Olchowy patyk | 5 | Wyposażenie!A45:J45 |
| Zwiadowca | Tunika | Umocnienia | Sztabka cyny | 5 | Wyposażenie!K45:S45 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Głógowy patyk | 15 | Wyposażenie!A46:J46 |
| Zwiadowca | Tunika | Umocnienia | Sztabka stali | 15 | Wyposażenie!K46:S46 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Brzozowy patyk | 15 | Wyposażenie!A47:J47 |
| Zwiadowca | Tunika | Umocnienia | Sztabka inwaru | 15 | Wyposażenie!K47:S47 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Cyprysowy patyk | 15 | Wyposażenie!A48:J48 |
| Zwiadowca | Tunika | Umocnienia | Sztabka żelaza meteorytowego | 15 | Wyposażenie!K48:S48 |
| Zwiadowca | Miecz i Sztylet | Rękojeść | Ostrokrzewowy patyk | 15 | Wyposażenie!A49:J49 |
| Zwiadowca | Tunika | Umocnienia | Sztabka brązu | 15 | Wyposażenie!K49:S49 |
| Czarodziej | Kostur | Katalizator | Sztabka żelaza | 5 | Wyposażenie!A50:J50 |
| Czarodziej | Szata | Materiał | Bawełniana tkanina | 5 | Wyposażenie!K50:S50 |
| Czarodziej | Kostur | Katalizator | Sztabka niklu | 5 | Wyposażenie!A51:J51 |
| Czarodziej | Szata | Materiał | Bukominowa tkanina | 5 | Wyposażenie!K51:S51 |
| Czarodziej | Kostur | Katalizator | Sztabka miedzi | 5 | Wyposażenie!A52:J52 |
| Czarodziej | Szata | Materiał | Tkanina z koziej wełny | 5 | Wyposażenie!K52:S52 |
| Czarodziej | Kostur | Katalizator | Sztabka cyny | 5 | Wyposażenie!A53:J53 |
| Czarodziej | Szata | Materiał | Jutowa tkanina | 5 | Wyposażenie!K53:S53 |
| Czarodziej | Kostur | Katalizator | Sztabka stali | 15 | Wyposażenie!A54:J54 |
| Czarodziej | Szata | Materiał | Tkanina z owczej wełny | 15 | Wyposażenie!K54:S54 |
| Czarodziej | Kostur | Katalizator | Sztabka inwaru | 15 | Wyposażenie!A55:J55 |
| Czarodziej | Szata | Materiał | Palmowa tkanina | 15 | Wyposażenie!K55:S55 |
| Czarodziej | Kostur | Katalizator | Sztabka żelaza meteorytowego | 15 | Wyposażenie!A56:J56 |
| Czarodziej | Szata | Materiał | Tkanina z lamiej wełny | 15 | Wyposażenie!K56:S56 |
| Czarodziej | Kostur | Katalizator | Sztabka brązu | 15 | Wyposażenie!A57:J57 |
| Czarodziej | Szata | Materiał | Wasminowa tkanina | 15 | Wyposażenie!K57:S57 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra kozy | 5 | Wyposażenie!A58:J58 |
| Czarodziej | Szata | Pas | Paski ze skóry kozy | 5 | Wyposażenie!K58:S58 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!A59:J59 |
| Czarodziej | Szata | Pas | Paski ze skóry dzikiego psa | 5 | Wyposażenie!K59:S59 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra królika | 5 | Wyposażenie!A60:J60 |
| Czarodziej | Szata | Pas | Paski ze skóry królika | 5 | Wyposażenie!K60:S60 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra lisa | 5 | Wyposażenie!A61:J61 |
| Czarodziej | Szata | Pas | Paski ze skóry lisa | 5 | Wyposażenie!K61:S61 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra tygrysa | 15 | Wyposażenie!A62:J62 |
| Czarodziej | Szata | Pas | Paski ze skóry tygrysa | 15 | Wyposażenie!K62:S62 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra jelenia | 15 | Wyposażenie!A63:J63 |
| Czarodziej | Szata | Pas | Paski ze skóry jelenia | 15 | Wyposażenie!K63:S63 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra dzika | 15 | Wyposażenie!A64:J64 |
| Czarodziej | Szata | Pas | Paski ze skóry dzika | 15 | Wyposażenie!K64:S64 |
| Czarodziej | Kostur | Osłona | Wyprawiona skóra warana | 15 | Wyposażenie!A65:J65 |
| Czarodziej | Szata | Pas | Paski ze skóry warana | 15 | Wyposażenie!K65:S65 |
| Czarodziej | Kostur | Trzon | Modrzewiowy patyk | 5 | Wyposażenie!A66:J66 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra kozy | 5 | Wyposażenie!K66:S66 |
| Czarodziej | Kostur | Trzon | Lipowy patyk | 5 | Wyposażenie!A67:J67 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!K67:S67 |
| Czarodziej | Kostur | Trzon | Topolowy patyk | 5 | Wyposażenie!A68:J68 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra królika | 5 | Wyposażenie!K68:S68 |
| Czarodziej | Kostur | Trzon | Olchowy patyk | 5 | Wyposażenie!A69:J69 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra lisa | 5 | Wyposażenie!K69:S69 |
| Czarodziej | Kostur | Trzon | Głógowy patyk | 15 | Wyposażenie!A70:J70 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra tygrysa | 15 | Wyposażenie!K70:S70 |
| Czarodziej | Kostur | Trzon | Brzozowy patyk | 15 | Wyposażenie!A71:J71 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra jelenia | 15 | Wyposażenie!K71:S71 |
| Czarodziej | Kostur | Trzon | Cyprysowy patyk | 15 | Wyposażenie!A72:J72 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra dzika | 15 | Wyposażenie!K72:S72 |
| Czarodziej | Kostur | Trzon | Ostrokrzewowy patyk | 15 | Wyposażenie!A73:J73 |
| Czarodziej | Szata | Koszula | Wyprawiona skóra warana | 15 | Wyposażenie!K73:S73 |
| Paladyn | Młot | Głownia | Wielka sztabka żelaza | 5 | Wyposażenie!A74:J74 |
| Paladyn | Kirys | Napierśnik | Żelazna blacha | 5 | Wyposażenie!K74:S74 |
| Paladyn | Młot | Głownia | Wielka sztabka niklu | 5 | Wyposażenie!A75:J75 |
| Paladyn | Kirys | Napierśnik | Niklowa blacha | 5 | Wyposażenie!K75:S75 |
| Paladyn | Młot | Głownia | Wielka sztabka miedzi | 5 | Wyposażenie!A76:J76 |
| Paladyn | Kirys | Napierśnik | Miedziana blacha | 5 | Wyposażenie!K76:S76 |
| Paladyn | Młot | Głownia | Wielka sztabka cyny | 5 | Wyposażenie!A77:J77 |
| Paladyn | Kirys | Napierśnik | Cynowa blacha | 5 | Wyposażenie!K77:S77 |
| Paladyn | Młot | Głownia | Wielka sztabka stali | 15 | Wyposażenie!A78:J78 |
| Paladyn | Kirys | Napierśnik | Stalowa blacha | 15 | Wyposażenie!K78:S78 |
| Paladyn | Młot | Głownia | Wielka sztabka inwaru | 15 | Wyposażenie!A79:J79 |
| Paladyn | Kirys | Napierśnik | Inwarowa blacha | 15 | Wyposażenie!K79:S79 |
| Paladyn | Młot | Głownia | Wielka sztabka żelaza meteorytowego | 15 | Wyposażenie!A80:J80 |
| Paladyn | Kirys | Napierśnik | Meteorytowa blacha | 15 | Wyposażenie!K80:S80 |
| Paladyn | Młot | Głownia | Wielka sztabka brązu | 15 | Wyposażenie!A81:J81 |
| Paladyn | Kirys | Napierśnik | Brązowa blacha | 15 | Wyposażenie!K81:S81 |
| Paladyn | Młot | Owicie | Wyprawiona skóra kozy | 5 | Wyposażenie!A82:J82 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry kozy | 5 | Wyposażenie!K82:S82 |
| Paladyn | Młot | Owicie | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!A83:J83 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry dzikiego psa | 5 | Wyposażenie!K83:S83 |
| Paladyn | Młot | Owicie | Wyprawiona skóra królika | 5 | Wyposażenie!A84:J84 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry królika | 5 | Wyposażenie!K84:S84 |
| Paladyn | Młot | Owicie | Wyprawiona skóra lisa | 5 | Wyposażenie!A85:J85 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry lisa | 5 | Wyposażenie!K85:S85 |
| Paladyn | Młot | Owicie | Wyprawiona skóra tygrysa | 15 | Wyposażenie!A86:J86 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry tygrysa | 15 | Wyposażenie!K86:S86 |
| Paladyn | Młot | Owicie | Wyprawiona skóra jelenia | 15 | Wyposażenie!A87:J87 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry jelenia | 15 | Wyposażenie!K87:S87 |
| Paladyn | Młot | Owicie | Wyprawiona skóra dzika | 15 | Wyposażenie!A88:J88 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry dzika | 15 | Wyposażenie!K88:S88 |
| Paladyn | Młot | Owicie | Wyprawiona skóra warana | 15 | Wyposażenie!A89:J89 |
| Paladyn | Kirys | Rzemienie | Paski ze skóry warana | 15 | Wyposażenie!K89:S89 |
| Paladyn | Młot | Rękojeść | Modrzewiowy patyk | 5 | Wyposażenie!A90:J90 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra kozy | 5 | Wyposażenie!K90:S90 |
| Paladyn | Młot | Rękojeść | Lipowy patyk | 5 | Wyposażenie!A91:J91 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra dzikiego psa | 5 | Wyposażenie!K91:S91 |
| Paladyn | Młot | Rękojeść | Topolowy patyk | 5 | Wyposażenie!A92:J92 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra królika | 5 | Wyposażenie!K92:S92 |
| Paladyn | Młot | Rękojeść | Olchowy patyk | 5 | Wyposażenie!A93:J93 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra lisa | 5 | Wyposażenie!K93:S93 |
| Paladyn | Młot | Rękojeść | Głógowy patyk | 15 | Wyposażenie!A94:J94 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra tygrysa | 15 | Wyposażenie!K94:S94 |
| Paladyn | Młot | Rękojeść | Brzozowy patyk | 15 | Wyposażenie!A95:J95 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra jelenia | 15 | Wyposażenie!K95:S95 |
| Paladyn | Młot | Rękojeść | Cyprysowy patyk | 15 | Wyposażenie!A96:J96 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra dzika | 15 | Wyposażenie!K96:S96 |
| Paladyn | Młot | Rękojeść | Ostrokrzewowy patyk | 15 | Wyposażenie!A97:J97 |
| Paladyn | Kirys | Naplecznik | Wyprawiona skóra warana | 15 | Wyposażenie!K97:S97 |

Poniżej indeks istniejących części; porównywać klasę, Kind, materiał i LevelBracket, nie tylko nazwę. Wszystkie wymagają porównania parametrów z powyższą listą.

| ID | Poziom | Asset |
| --- | --- | --- |
| part.mage.armor.base.common.goat_leather | 5 | Equipment/ArmorParts/EPD_MageArmorBase_GoatLeather.asset |
| part.mage.armor.binder.common.goat_leather | 5 | Equipment/ArmorParts/EPD_MageArmorBinder_GoatLeather.asset |
| part.mage.armor.plate.common.cotton | 5 | Equipment/ArmorParts/EPD_MageArmorPlate_Cotton.asset |
| part.paladin.armor.base.common.goat_leather | 5 | Equipment/ArmorParts/EPD_PaladinArmorBase_GoatLeather.asset |
| part.paladin.armor.binder.common.goat_leather | 5 | Equipment/ArmorParts/EPD_PaladinArmorBinder_GoatLeather.asset |
| part.paladin.armor.plate.common.iron | 5 | Equipment/ArmorParts/EPD_PaladinArmorPlate_Iron.asset |
| part.scout.armor.base.common.iron | 5 | Equipment/ArmorParts/EPD_ScoutArmorBase_Iron.asset |
| part.scout.armor.binder.common.goat_leather | 5 | Equipment/ArmorParts/EPD_ScoutArmorBinder_GoatLeather.asset |
| part.scout.armor.plate.common.goat_leathe | 5 | Equipment/ArmorParts/EPD_ScoutArmorPlate_GoatLeather.asset |
| part.warrior.armor.base.common.goat_leather | 5 | Equipment/ArmorParts/EPD_WarriorArmorBase_GoatLeather.asset |
| part.warrior.armor.binder.common.iron | 5 | Equipment/ArmorParts/EPD_WarriorArmorBinder_Iron.asset |
| part.warrior.armor.plate.common.iron | 5 | Equipment/ArmorParts/EPD_WarriorArmorPlate_Iron.asset |
| part.mage.weapon.binder.common.goat_leather | 5 | Equipment/WeaponParts/EPD_MageWeaponBinder_GoatLeather.asset |
| part.mage.weapon.handle.common.larch | 5 | Equipment/WeaponParts/EPD_MageWeaponHandle_Larch.asset |
| part.mage.weapon.head.common.iron | 5 | Equipment/WeaponParts/EPD_MageWeaponHead_Iron.asset |
| part.paladin.weapon.binder.common.goat_leather | 5 | Equipment/WeaponParts/EPD_PaladinWeaponBinder_GoatLeather.asset |
| part.paladin.weapon.handle.common.larch | 5 | Equipment/WeaponParts/EPD_PaladinWeaponHandle_Larch.asset |
| part.paladin.weapon.head.common.iron | 5 | Equipment/WeaponParts/EPD_PaladinWeaponHead_Iron.asset |
| part.scout.weapon.binder.goat_leather | 5 | Equipment/WeaponParts/EPD_ScoutWeaponBinder_GoatLeather.asset |
| part.scout.weapon.binder.tiger_leather | 15 | Equipment/WeaponParts/EPD_ScoutWeaponBinder_GoatLeather_Copy.asset |
| part.scout.weapon.handle.larch | 5 | Equipment/WeaponParts/EPD_ScoutWeaponHandle_Larch.asset |
| part.scout.weapon.handle.hawthorn | 15 | Equipment/WeaponParts/EPD_ScoutWeaponHandle_Larch_Copy.asset |
| part.scout.weapon.head.iron | 5 | Equipment/WeaponParts/EPD_ScoutWeaponHead_Iron.asset |
| part.scout.weapon.head.steel | 15 | Equipment/WeaponParts/EPD_ScoutWeaponHead_Iron_Copy.asset |
| part.warrior.weapon.binder.common.goat_leather | 5 | Equipment/WeaponParts/EPD_WarriorWeaponBinder_GoatLeather.asset |
| part.warrior.weapon.handle.common.larch | 5 | Equipment/WeaponParts/EPD_WarriorWeaponHandle_Larch.asset |
| part.warrior.weapon.head.common.iron | 5 | Equipment/WeaponParts/EPD_WarriorWeaponHead_Iron.asset |

## Consumables — VS-12

| Rodzaj | Efekt | Siła T1 | Składnik M1 | Składnik M2 | Czas | Źródło |
| --- | --- | --- | --- | --- | --- | --- |
| Mikstura | Odnowa zdrowia | 0.15 | Purchawka | Kość szkieleta | - | Consumable!A3:F3 |
| Mikstura | Regeneracja zdrowia | 0.25 | Kosarz pospolity | Mózg umarlaka | 10 | Consumable!A4:F4 |
| Mikstura | Odnowa surowca | 0.2 | Szyszka | Ucho elfa | - | Consumable!A5:F5 |
| Mikstura | Regeneracja surowca | 0.3 | Trawa dusicielska | Róg demona | 10 | Consumable!A6:F6 |
| Mikstura | Zwiększenie maksymalnego surowca | 0.1 | Wodorosty | Oko pirata | 10 | Consumable!A7:F7 |
| Mikstura | Zwiększenie obrażeń | 0.05 | Kwitnąca wiśnia | Pióro ptaka | 8 | Consumable!A8:F8 |
| Mikstura | Zwiększenie obrony | 0.1 | Aloe meda | Owoc kaktusa | 8 | Consumable!A9:F9 |
| Mikstura | Odporności | 0.1 | Pnącza alzif | Znak świątyni | 8 | Consumable!A10:F10 |
| Mikstura | Zwiększenie szybkości | 0.15 | Kwiat byliny | Złamana maska | 8 | Consumable!A11:F11 |
| Mikstura | Szansa krytyczna | 0.05 | Dmuchawiec tregandzki | Nerka trolla | 8 | Consumable!A12:F12 |
| Mikstura | Unik | 10 | Sitowie | Żyjący liść | 8 | Consumable!A13:F13 |
| Mikstura | Zwiększenie punktów akcji | 0.1 | Mucha zwyczajna | Kieł aligatora | 8 | Consumable!A14:F14 |
| Bomba | Obrażenia fizyczne | 0.15 | Purchawka | Kość szkieleta | - | Consumable!A17:F17 |
| Bomba | Obrażenia magiczne | 0.15 | Kosarz pospolity | Mózg umarlaka | - | Consumable!A18:F18 |
| Bomba | Krwawienie | 0.1 | Szyszka | Ucho elfa | 4 | Consumable!A19:F19 |
| Bomba | Zatrucie | 0.1 | Trawa dusicielska | Róg demona | 4 | Consumable!A20:F20 |
| Bomba | Podpalenie | 0.1 | Wodorosty | Oko pirata | 3 | Consumable!A21:F21 |
| Bomba | Ogłuszenie | 0.6 | Kwitnąca wiśnia | Pióro ptaka | 2 | Consumable!A22:F22 |
| Bomba | Osłabienie obrony fizycznej | 0.1 | Aloe meda | Owoc kaktusa | 3 | Consumable!A23:F23 |
| Bomba | Osłabienie obrony magicznej | 0.1 | Pnącza alzif | Znak świątyni | 3 | Consumable!A24:F24 |
| Bomba | Osłabienie uniku | 0.1 | Kwiat byliny | Złamana maska | 3 | Consumable!A25:F25 |
| Bomba | Osłabienie celności | 5 | Dmuchawiec tregandzki | Nerka trolla | 3 | Consumable!A26:F26 |
| Bomba | Spowolnienie | 2 | Sitowie | Żyjący liść | 3 | Consumable!A27:F27 |
| Bomba | Zamrożenie | 0.55 | Mucha zwyczajna | Kieł aligatora | 2 | Consumable!A28:F28 |

Tabela podaje pary z arkusza; używać tylko składników L1/L11 z listy materiałów, nie dodawać M2 L21. Bomby bezpośrednie stosują zatwierdzoną wspólną krzywą poziomu (cel ~40% ataku dla jednego składnika i ~60% dla dwóch), nie stary mnożnik broni. Regeneracje są sumą rozłożoną na tury.

| Rodzaj | Katalizator T1 | Efekt | Wartość |
| --- | --- | --- | --- |
| Mikstura | Proch ziołowy: Zwykły | Wydłużenie czasu działania | +1t |
| Mikstura | Proch ziołowy: Grudkowaty | Zwiększenie efektu | 0.1 |
| Mikstura | Proch ziołowy: Lepki | Skondensowanie czasu działania | -1t |
| Mikstura | Proch ziołowy: Luźny | Ilość ładunków | 1 |
| Bomba | Proch ziołowy: Zwykły | Wydłużenie czasu działania | +1t |
| Bomba | Proch ziołowy: Grudkowaty | Zwiększenie efektu | 0.1 |
| Bomba | Proch ziołowy: Lepki | Zwiększenie zasięgu | 1 |
| Bomba | Proch ziołowy: Luźny | Ilość ładunków | 1 |

| Przedmiot | Receptura | Poziom | Działanie |
| --- | --- | --- | --- |
| Bandaż | 1x Alkohol + 2x Dowolna tkanina -> 1x Bandaż | 11 | Zatrzymuje krwawienie i zwiększa odporność na krwawienie o 30% na 3 tury |
| Antidotum | 1x Alkohol + 4x Dowolne zioło + 4x Dowolne zioło -> 1x Antidotum | 11 | Zatrzymuje truciznę i zwiększa odporność na truciznę o 30% na 3 tury |
| Sucha Szmatka | 4x Dowolna tkanina -> 1x Sucha szmatka | 11 | Zatrzymuje podpalenie i zwiększa odporność na ogień o 30% na 3 tury |

Zachować zwój powrotu i zatwierdzone zestawy obozowe jako elementy pętli; obóz nie jest recepturą alchemiczną.

## Biżuteria, klejnoty i galduryty — VS-11

| Metal | Poziom | Pierścień | Naszyjnik | Bransoleta | Kolczyki |
| --- | --- | --- | --- | --- | --- |
| Żelazo | 10 | 2 siły ataku | 30 punktów życia | 2% odporności na krwawienie | 4 siły leczenia |
| Nikiel | 10 | 1% szansy na kryt. | 6 obrony fizycznej | 2% odporności na zatrucie | 5% generacji surowca |
| Miedź | 10 | 2 celności | 5 obrony magicznej | 2% odporności na ogień | 3 maksymalnej many |
| Cyna | 10 | 0.03x obrażeń kryt. | 1 uniku | 1% odporności na ogłuszenie | 2 szybkości |
| Stal | 20 | 9 siły ataku | 125 punktów życia | 3% odporności na krwawienie | 18 siły leczenia |
| Inwar | 20 | 1.5% szansy na kryt. | 30 obrony fizycznej | 3% odporności na zatrucie | 7% generacji surowca |
| Żelazo meteorytowe | 20 | 2 celności | 25 obrony magicznej | 3% odporności na ogień | 4 maksymalnej many |
| Brąz | 20 | 0.035x obrażeń kryt. | 2 uniku | 2% odporności na ogłuszenie | 4 szybkości |

Wartości w powyższej tabeli są odczytem starszych komórek; poprawki pierścieni/kolczyków wskazano w sekcji rozbieżności i obowiązują przy authoringu.

| Klejnot | Efekt | Źródło docelowe / NPC dla obcych theme | Kandydat reuse |
| --- | --- | --- | --- |
| Granat | 3% obrażeń przeciwko nieumarłym | Katakumby | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Szafir | 2.5% obrażeń przeciwko bestiom | Morze | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Ametyst | 2.5% obrażeń w zamkniętym terenie | Świątynia | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Cytryn | 2% obrażeń w ciepłym terenie | Pustynia | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Szmaragd | 1.5% obrażeń przeciwko humanoidom | Las | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Onyks | 3% obrażeń przeciwko demonom | Kuźnia | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Rubin | 2.5% obrażeń w otwartym terenie | Bagno | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Diament | 3% obrażeń w zimnym terenie | Góry | Brak dopasowania nazwy; sprawdzić analog przed utworzeniem |
| Topaz | 1% zdobywanego doświadczenia | Las, Kuźnia, Bagno, Góry | `Items/Materials/Runeforging/ID_Topaz.asset` |
| Bursztyn | 1.5% zdobywanego złota | Katakumby, Morze, Świątynia, Pustynia | `Items/Materials/Runeforging/ID_Amber.asset` |
| Obsydian | 3.5% obrażeń w katakumbach | Katakumby (Bossy) | `Items/Materials/Runeforging/ID_Obsidian.asset` |

| Cel | Efekt | D/C/B/A/S | Proszek | Źródło |
| --- | --- | --- | --- | --- |
| Na broń | Bonus obrażeń ogólny | 2%/2.6%/3.2%/3.8%/5% | Czerwony | Biżuteria i Galduryty!N2:Q2 |
| Na broń | Bonus obrażeń fizycznych | 3%/3.6%/4.2%/4.8%/6% | Czerwony | Biżuteria i Galduryty!N3:Q3 |
| Na broń | Bonus obrażeń magicznych | 3%/3.6%/4.2%/4.8%/6% | Czerwony | Biżuteria i Galduryty!N4:Q4 |
| Na broń | Bonus szansy na obrażenia krytyczne | 1%/1.4%/1.8%/2.2%/3% | Niebieski | Biżuteria i Galduryty!N5:Q5 |
| Na broń | Bonus obrażeń krytycznych | 0.02x/0.026x/0.032x/0.038x/0.05x | Niebieski | Biżuteria i Galduryty!N6:Q6 |
| Na broń | Bonus szansy na trafienie [p] | 1%/1.4%/1.8%/2.2%/3% | Purpurowy | Biżuteria i Galduryty!N7:Q7 |
| Na broń | Bonus szansy na EOR [p] | 1%/1.4%/1.8%/2.2%/3% | Purpurowy | Biżuteria i Galduryty!N8:Q8 |
| Na broń | Bonus szansy na osłabienie [p] | 1%/1.4%/1.8%/2.2%/3% | Purpurowy | Biżuteria i Galduryty!N9:Q9 |
| Na broń | Bonus szansy na efekt DoT [p] | 1%/1.4%/1.8%/2.2%/3% | Purpurowy | Biżuteria i Galduryty!N10:Q10 |
| Na broń | Szansa na krwawienie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Cytrynowy | Biżuteria i Galduryty!N11:Q11 |
| Na broń | Szansa na zatrucie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Cytrynowy | Biżuteria i Galduryty!N12:Q12 |
| Na broń | Szansa na podpalenie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Cytrynowy | Biżuteria i Galduryty!N13:Q13 |
| Na broń | Przywrócenie zdrowia przy ataku | 1.5%/2%/2.5%/3%/4% (zadanych obrażeń) | Zielony | Biżuteria i Galduryty!N14:Q14 |
| Na broń | Przywrócenie surowca przy ataku | 1.5%/2%/2.5%/3%/4% (maksymalnego) | Zielony | Biżuteria i Galduryty!N15:Q15 |
| Na broń | Przyspieszenie ruchu przy ataku (+AV) | 2/2.5/3/3.5/4.5 | Zielony | Biżuteria i Galduryty!N16:Q16 |
| Na broń | Bonus szansy na zdobycie przedmiotu [p] | 3%/3.6%/4.2%/4.8%/6% | Czarny | Biżuteria i Galduryty!N17:Q17 |
| Na broń | Bonus generowanego surowca | 4%/4.8%/5.6%/6.4%/8% | Czarny | Biżuteria i Galduryty!N18:Q18 |
| Na broń | Bonus obrażeń przeciwko nieumarłym | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N19:Q19 |
| Na broń | Bonus obrażeń przeciwko ludziom | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N20:Q20 |
| Na broń | Bonus obrażeń przeciwko bestiom | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N21:Q21 |
| Na broń | Bonus obrażeń przeciwko demonom | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N22:Q22 |
| Na broń | Szansa na ogłuszenie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Biały | Biżuteria i Galduryty!N23:Q23 |
| Na broń | Szansa na zamrożenie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Biały | Biżuteria i Galduryty!N24:Q24 |
| Na broń | Szansa na spowolnienie przy ataku | 0.75%/1%/1.25%/1.5%/2% | Biały | Biżuteria i Galduryty!N25:Q25 |
| Na broń | Bonus zdobywanego doświadczenia | 2%/2.6%/3.2%/3.8%/5% | Złoty | Biżuteria i Galduryty!N26:Q26 |
| Na broń | Bonus zdobywanego złota | 2%/2.6%/3.2%/3.8%/5% | Złoty | Biżuteria i Galduryty!N27:Q27 |
| Na broń | Penetracja obrony fizycznej | 3%/3.6%/4.2%/4.8%/6% | Pomarańczowy | Biżuteria i Galduryty!N28:Q28 |
| Na broń | Penetracja obrony magicznej | 3%/3.6%/4.2%/4.8%/6% | Pomarańczowy | Biżuteria i Galduryty!N29:Q29 |
| Na pancerz | Bonus maksymalnego zdrowia | 2%/2.6%/3.2%/3.8%/5% | Czerwony | Biżuteria i Galduryty!N30:Q30 |
| Na pancerz | Bonus uniku | 3%/3.6%/4.2%/4.8%/6% | Czerwony | Biżuteria i Galduryty!N31:Q31 |
| Na pancerz | Bonus obrony fizycznej | 3%/3.6%/4.2%/4.8%/6% | Niebieski | Biżuteria i Galduryty!N32:Q32 |
| Na pancerz | Bonus obrony magicznej | 3%/3.6%/4.2%/4.8%/6% | Niebieski | Biżuteria i Galduryty!N33:Q33 |
| Na pancerz | Bonus obrony ogólnej | 2%/2.6%/3.2%/3.8%/5% | Niebieski | Biżuteria i Galduryty!N34:Q34 |
| Na pancerz | Bonus szybkości | 2%/2.6%/3.2%/3.8%/5% | Purpurowy | Biżuteria i Galduryty!N35:Q35 |
| Na pancerz | Bonus otrzymywanych punktów akcji | 2%/2.6%/3.2%/3.8%/5% | Purpurowy | Biżuteria i Galduryty!N36:Q36 |
| Na pancerz | Bonus odporności na DoT | 1.5%/2%/2.5%/3%/4% | Cytrynowy | Biżuteria i Galduryty!N37:Q37 |
| Na pancerz | Bonus odporności na EOR | 1.5%/2%/2.5%/3%/4% | Cytrynowy | Biżuteria i Galduryty!N38:Q38 |
| Na pancerz | Bonus odporności na osłabienia | 1.5%/2%/2.5%/3%/4% | Cytrynowy | Biżuteria i Galduryty!N39:Q39 |
| Na pancerz | Bonus wszystkich odporności | 1%/1.4%/1.8%/2.2%/3% | Cytrynowy | Biżuteria i Galduryty!N40:Q40 |
| Na pancerz | Zmniejszone przyjmowane obrażenia fizyczne | 2%/2.6%/3.2%/3.8%/5% | Zielony | Biżuteria i Galduryty!N41:Q41 |
| Na pancerz | Zmniejszone przyjmowane obrażenia magiczne | 2%/2.6%/3.2%/3.8%/5% | Zielony | Biżuteria i Galduryty!N42:Q42 |
| Na pancerz | Zmniejszone przyjmowane obrażenia ogólne | 1.5%/2%/2.5%/3%/4% | Zielony | Biżuteria i Galduryty!N43:Q43 |
| Na pancerz | Bonus szansy na zdobycie przedmiotu [p] | 3%/3.6%/4.2%/4.8%/6% | Czarny | Biżuteria i Galduryty!N44:Q44 |
| Na pancerz | Bonus maksymalnego surowca | 3%/3.6%/4.2%/4.8%/6% | Czarny | Biżuteria i Galduryty!N45:Q45 |
| Na pancerz | Zmniejszone obrażenia od krwawienia | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N46:Q46 |
| Na pancerz | Zmniejszone obrażenia od zatrucia | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N47:Q47 |
| Na pancerz | Zmniejszone obrażenia od ognia | 4%/4.8%/5.6%/6.4%/8% | Różowy | Biżuteria i Galduryty!N48:Q48 |
| Na pancerz | Zmniejszone zużycie surowca | 2%/2.6%/3.2%/3.8%/5% | Biały | Biżuteria i Galduryty!N49:Q49 |
| Na pancerz | Szansa na nieotrzymanie ciosu krytycznego | 3%/3.6%/4.2%/4.8%/6% | Biały | Biżuteria i Galduryty!N50:Q50 |
| Na pancerz | Bonus zdobywanego złota | 2%/2.6%/3.2%/3.8%/5% | Złoty | Biżuteria i Galduryty!N51:Q51 |
| Na pancerz | Bonus zdobywanego doświadczenia | 2%/2.6%/3.2%/3.8%/5% | Złoty | Biżuteria i Galduryty!N52:Q52 |
| Na pancerz | Pasywne przywracanie zdrowia | 0.4%/0.52%/0.64%/0.76%/1% (maksymalnego) | Pomarańczowy | Biżuteria i Galduryty!N53:Q53 |
| Na pancerz | Pasywne przywracanie surowca | 0.5%/0.8%/1.1%/1.4%/2% (maksymalnego) | Pomarańczowy | Biżuteria i Galduryty!N54:Q54 |

## Unikaty Ducha Umarłego Króla — VS-11/16

| Nazwa | Klasa | Pierwszy wariant efektu | Źródło wariantów | Istniejący asset |
| --- | --- | --- | --- | --- |
| Miecz Legionu Szkieletów | Wojownik | 30% szansy podczas ataku na stworzenie ducha, który duplikuje atak z obrażeniami zmniejszonymi o 40% | Wyposażenie.xlsx / Arcydzieła!A48:I48 | `Equipment/BossUnique/EID_BossDeadKing_WarriorSword.asset` |
| Niematerialny Sztylet | Zwiadowca | 45% na niezużycie sztyletu po ataku | Wyposażenie.xlsx / Arcydzieła!A49:I49 | `Equipment/BossUnique/EID_BossDeadKing_ScoutDagger.asset` |
| Kostur Umarłych | Czarodziej | 5% szansy na przywołanie losowego Szkieleta o maksymalnym zdrowiu zmniejszonym o 75% | Wyposażenie.xlsx / Arcydzieła!A50:I50 | `Equipment/BossUnique/EID_BossDeadKing_MageStaff.asset` |
| Zbroja Umarłego Króla | Paladyn | 10% szansy na całkowite pochłonięcie obrażeń magicznych | Wyposażenie.xlsx / Arcydzieła!A51:I51 | `Equipment/BossUnique/EID_BossDeadKing_PaladinArmor.asset` |

Nie przenosić całego późniejszego skalowania unikatów do slice’a; VS-11/16 mają dobrać istniejący model do L10/L20.

## Lootbagi — VS-15/16

| Torba | Kategoria | L1–10: zapis źródłowy | L11–20: zapis źródłowy | Źródło |
| --- | --- | --- | --- | --- |
| Koścista torba zasobów | A) | 80% jeden z: Słaby alkohol (1-2) | 80% jeden z: Alkohol (1-2) | Lootbagi!A4:E4 |
| Koścista torba zasobów | B) | 100% jeden z: Ruda żelaza (3-5), Skóra kozy (1-3), Drewno modrzewiowe (1-2), Włókno bawełny (3-4), Czerwony pył energetyczny (1-2) | 100% jeden z: Ruda żelaza (3-5), Skóra jelenia (1-2), Drewno brzozowe (1-2), Włókno palmy (3-4), Żółty pył energetyczny (1-2) | Lootbagi!A5:E5 |
| Koścista torba zasobów | C) | 70% jeden z: Ruda żelaza (3-5), Skóra kozy (1-3), Drewno modrzewiowe (1-2), Włókno bawełny (3-4), Czerwony pył energetyczny (1-2); inny niż B | 70% jeden z: Ruda żelaza (3-5), Skóra jelenia (1-2), Drewno brzozowe (1-2), Włókno palmy (3-4), Żółty pył energetyczny (1-2); inny niż B | Lootbagi!A6:E6 |
| Koścista torba zasobów | D) | 30% jeden z: Ruda żelaza (3-5), Skóra kozy (1-3), Drewno modrzewiowe (1-2), Włókno bawełny (3-4), Czerwony pył energetyczny (1-2); inny niż B i C | 30% jeden z: Ruda żelaza (3-5), Skóra jelenia (1-2), Drewno brzozowe (1-2), Włókno palmy (3-4), Żółty pył energetyczny (1-2); inny niż B i C | Lootbagi!A7:E7 |
| Koścista torba zasobów | E) | - | 80% jeden z: Węgiel (2-3) | Lootbagi!A8:E8 |
| Koścista torba zasobów | E) | 100% jeden z: Kosarz pospolity (1-3), Purchawka (1-3) | 100% jeden z: Kosarz pospolity (1-3), Purchawka (1-3) | Lootbagi!A9:E9 |
| Koścista torba zasobów | F) | 40% jeden z: Kosarz pospolity (1-3), Purchawka (1-3), inny niż E | 40% jeden z: Kosarz pospolity (1-3), Purchawka (1-3), inny niż E | Lootbagi!A10:E10 |
| Koścista torba zasobów | G) | - | 60% jeden z: Lity wapień (1-2) | Lootbagi!A11:E11 |
| Koścista torba zasobów | H) | - | 30% jeden z: Granat (1-2) | Lootbagi!A12:E12 |
| Koścista torba zasobów | I) | - | 15% jeden z: Bursztyn (1) | Lootbagi!A13:E13 |
| Koścista torba zasobów | J) | 40% jeden z: Kość szkieleta (1-2), Mózg umarlaka (1-2) | 40% jeden z: Kość szkieleta (1-2), Mózg umarlaka (1-2) | Lootbagi!A14:E14 |
| Koścista torba zasobów | K) | - | — | Lootbagi!A15:E15 |
| Torba z Brońmi | A) | 100% jeden z: Losowa Słaba Broń na poziom 5 (1), Losowa Broń na poziom 5 (1), Losowa Doskonała Broń na poziom 5 (1), | 100% jeden z: Losowa Słaba Broń na poziom 15 (1), Losowa Broń na poziom 15 (1), Losowa Doskonała Broń na poziom 15 (1), | Lootbagi!A16:E16 |
| Torba z Brońmi | B) | 100% jeden z: Losowa Słaba Broń na poziom 5 (1), Losowa Broń na poziom 5 (1), Losowa Doskonała Broń na poziom 5 (1), | 100% jeden z: Losowa Słaba Broń na poziom 15 (1), Losowa Broń na poziom 15 (1), Losowa Doskonała Broń na poziom 15 (1), | Lootbagi!A17:E17 |
| Torba z Brońmi | C) | 80% jeden z: Losowa Słaba Broń na poziom 5 (1), Losowa Broń na poziom 5 (1), Losowa Doskonała Broń na poziom 5 (1), | 80% jeden z: Losowa Słaba Broń na poziom 15 (1), Losowa Broń na poziom 15 (1), Losowa Doskonała Broń na poziom 15 (1), | Lootbagi!A18:E18 |
| Torba z Brońmi | D) | 50% jeden z: Losowa Słaba Broń na poziom 5 (1), Losowa Broń na poziom 5 (1), Losowa Doskonała Broń na poziom 5 (1), | 50% jeden z: Losowa Słaba Broń na poziom 15 (1), Losowa Broń na poziom 15 (1), Losowa Doskonała Broń na poziom 15 (1), | Lootbagi!A19:E19 |
| Torba z Brońmi | E) | 25% jeden z: Losowa Słaba Broń na poziom 5 (1), Losowa Broń na poziom 5 (1), Losowa Doskonała Broń na poziom 5 (1), | 25% jeden z: Losowa Słaba Broń na poziom 15 (1), Losowa Broń na poziom 15 (1), Losowa Doskonała Broń na poziom 15 (1), | Lootbagi!A20:E20 |
| Torba z Pancerzami | A) | 100% jeden z: Losowy Słaby Pancerz na poziom 5 (1), Losowy Pancerz na poziom 5 (1), Losowy Doskonały Pancerz na poziom 5 (1), | 100% jeden z: Losowy Słaby Pancerz na poziom 15 (1), Losowy Pancerz na poziom 15 (1), Losowy Doskonały Pancerz na poziom 15 (1), | Lootbagi!A21:E21 |
| Torba z Pancerzami | B) | 100% jeden z: Losowy Słaby Pancerz na poziom 5 (1), Losowy Pancerz na poziom 5 (1), Losowy Doskonały Pancerz na poziom 5 (1), | 100% jeden z: Losowy Słaby Pancerz na poziom 15 (1), Losowy Pancerz na poziom 15 (1), Losowy Doskonały Pancerz na poziom 15 (1), | Lootbagi!A22:E22 |
| Torba z Pancerzami | C) | 80% jeden z: Losowy Słaby Pancerz na poziom 5 (1), Losowy Pancerz na poziom 5 (1), Losowy Doskonały Pancerz na poziom 5 (1), | 80% jeden z: Losowy Słaby Pancerz na poziom 15 (1), Losowy Pancerz na poziom 15 (1), Losowy Doskonały Pancerz na poziom 15 (1), | Lootbagi!A23:E23 |
| Torba z Pancerzami | D) | 50% jeden z: Losowy Słaby Pancerz na poziom 5 (1), Losowy Pancerz na poziom 5 (1), Losowy Doskonały Pancerz na poziom 5 (1), | 50% jeden z: Losowy Słaby Pancerz na poziom 15 (1), Losowy Pancerz na poziom 15 (1), Losowy Doskonały Pancerz na poziom 15 (1), | Lootbagi!A24:E24 |
| Torba z Pancerzami | E) | 25% jeden z: Losowy Słaby Pancerz na poziom 5 (1), Losowy Pancerz na poziom 5 (1), Losowy Doskonały Pancerz na poziom 5 (1), | 25% jeden z: Losowy Słaby Pancerz na poziom 15 (1), Losowy Pancerz na poziom 15 (1), Losowy Doskonały Pancerz na poziom 15 (1), | Lootbagi!A25:E25 |
| Torba z Galdurytami | A) | - | 100% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A26:E26 |
| Torba z Galdurytami | B) | - | 100% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A27:E27 |
| Torba z Galdurytami | C) | - | 100% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A28:E28 |
| Torba z Galdurytami | D) | - | 80% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A29:E29 |
| Torba z Galdurytami | E) | - | 55% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A30:E30 |
| Torba z Galdurytami | F) | - | 40% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A31:E31 |
| Torba z Galdurytami | G) | - | 25% jeden z: Losowy Galduryt na poziom 20 | Lootbagi!A32:E32 |
| Torba Ducha Umarłego Króla | A) | 30% jeden z: Drop unique broń lvl 10, Drop unique pancerz lvl 10 | 30% jeden z: Drop unique broń lvl 20, Drop unique pancerz lvl 20 | Lootbagi!A33:E33 |
| Torba Ducha Umarłego Króla | B) | - | 50% jeden z: Królewska ektoplazma (1-2) | Lootbagi!A34:E34 |
| Torba Ducha Umarłego Króla | C) | - | 60% jeden z: Granat (3-5) | Lootbagi!A35:E35 |
| Torba Ducha Umarłego Króla | D) | - | 30% jeden z: Bursztyn (2-3) | Lootbagi!A36:E36 |
| Torba Ducha Umarłego Króla | E) | - | 30% jeden z: Losowy Galduryt na poziom 20 o unikalności co najmniej 5 (3); | Lootbagi!A37:E37 |

Obowiązują korekty z sekcji rozbieżności. Istniejące LT_Catacombs_*_1_10 i ID_TestLootBag_Catacombs są bazą techniczną, nie potwierdzonym pokryciem tych tabel. Skrzynie: 2–3 na 6 encounterów, złoto jak 2–3 encountery i składniki jak 2–3 lootbagi, proporcjonalne szanse sprzętu/galdurytów.

## Produkcyjne pokoje — VS-14

| Rodzaj | Liczba | Kandydaci reuse / praca |
| --- | --- | --- |
| Startowy | 1 | DRT_Catacombs_Start |
| Zwykłe | 9 | Normal_Small, Normal_Large, L, Tactical_CryptL, Tactical_Pillars, Tactical_Passages, Tactical_Ritual; brakujące dwa warianty zaprojektować w VS-14 |
| Opcjonalne | 3 | Optional, Optional_Lever, Optional_Condition |
| Korytarze | 2 | Corridor, Tactical_Corridor |
| Finałowy | 1 | Final; zakończenie wyprawy z bossem lub bez według definicji |

To wybór kandydatów, nie potwierdzenie gotowej geometrii. Każdy kandydat musi przejść kontrolę braku podestów, przejść, kotew encounterów/obiektów i osiągalności. Stairs_Test, MidBalcony i CombatV4_*_Demo pozostają poza pulą produkcyjną, bez usuwania plików. DD_Catacombs_Procedural ma obecnie nazwę „Katakumby testowe” i testowe encountery; DLP_OrganicSpaces / DLP_OrderedCorridors wymagają wyboru i konfiguracji w VS-14.

## Weryfikacja VS-01

Odczytano wszystkie osiem arkuszy, sprawdzono 20 startowych skilli, 8 × 20 pozycji węzłów, 7 mobów + bossa, 182 materiały L1/L11, 192 wpisy części, 11 klejnotów i 53 efekty galdurytów. Indeks assetów oparty na aktualnych plikach YAML czytanych bez modyfikacji. Review runtime i poprawności referencji należy do VS-02; żadna pozycja nie otrzymuje statusu gotowego contentu wyłącznie przez istnienie pliku. Zmiana dokumentacyjna nie wymaga testu Play Mode ani nowego setupu Unity. Bez commit/push.
