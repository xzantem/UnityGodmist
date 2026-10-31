# Godmist CP2 02 Katalog contentu i źródła do L40

Stan: 31 października 2026, **ZAAKCEPTOWANA — użytkownik polecił commit/push i przejście do CP2-03**. Nie jest wymagany gameplay smoke nowych funkcji: ta karta przygotowuje manifest i walidator, bez wdrażania nowych danych do gry.

Baza implementacji: `f30ab8f38d85490f47ba1e7c36afce94ad28bf70`, po zaakceptowanej CP2-01. Arkusz: `../design/content_pass_2.xlsx`, SHA256 `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`. Arkusza nie zmieniano. Jawne decyzje użytkownika poniżej mają pierwszeństwo przed jego starszymi wpisami.

## Wynik katalogowania

| Część katalogu | Wynik |
| --- | --- |
| Pozycje przedmiotowe w manifeście | 414: istniejące definicje, proponowane dodatki i jawne wyłączenia |
| Aktywny plan L1–40 | 406 pozycji: 370 materiałów, 35 nazwanych unikatów, Mikstura Oczyszczenia |
| Legacy zachowane dla zapisów | 4 ID: dwa pyły energetyczne, sprzedażowe Ucho elfa, Krew przywódcy |
| Zakres ponad L40 | 4 pozycje: Lity marmur, Marmurowy kamień galdurytu, Perfekcyjny alkohol i Perfekcyjny proch strzelniczy |
| Relacje źródło → przedmiot | 277, z przedziałami poziomów i rozróżnieniem naturalne/zastępcze |
| Materiały z przyszłych biomów mające drop zastępczy | 28: 24 surowce/składniki oraz 4 materiały przyszłych bossów |
| Receptury | 233 warianty źródłowe; 232 warianty w planie po przyjęciu nowej receptury kamienia granitowego; 220 różnych produktów łącznie z jawnym zakresem ponad L40 |
| Części wyposażenia | 192: 96 broni i 96 pancerza, L25/L35, wszystkie cztery klasy |
| Nowe zwykłe jednostki | 21, po 7 na każdy nowy biom |
| Nowi bossowie | 6, po dwie fazy: 12 definicji postaci w planie |
| Wpisy nowych skilli | 116 kandydatów: 99 akcji przeciwników/faz bossów oraz 17 aktywnych umiejętności z drzewek; część zależy od odłożonego projektu |
| Pozycje drzewek | 144 node'y i 4 pasywy globalne, w tym 31 jawnych placeholderów Paladyna bez wymyślonych ID |
| Unikaty | 40 slotów bossowych: 35 nazwanych i 5 oczekujących na projekt/nazwę |
| Biżuteria | 8 wariantów metali L30/L40; Jadeit i 3 nowe kamienie z premią biomową |
| Galduryty | L21, 3–4 efekty, brak nowych rodzajów efektu, wymaganie poziomu przedmiotu osadzenia |
| Rejestr nowych ID | 664 propozycje, jeszcze nieobecne w runtime |

Pozycje w rejestrze są kandydatami do authoringu właściwych kart. Nie oznacza to zatwierdzenia wszystkich wartości walki, T6/T7, efektów unikatów ani przebudowy Strażnika. Nie tworzymy nowych postaci grywalnych.

## Pliki do dalszej pracy

- [Pełny manifest JSON](Tools/ContentPass2/content-manifest.json): ID, nazwy, poziomy, status, źródła, receptury, odbiorcy i referencje do komórek/assetów.
- [Tabela do Excela z separatorem pionowej kreski](Tools/ContentPass2/material-sources.txt): każdy materiał/przedmiot osobno, biomy naturalne także przyszłe, torby zastępcze, przedziały i łańcuch craftingu. Bez szans i ilości dropu.
- [Rejestr proponowanych ID](Tools/ContentPass2/proposed-ids.json): trwałe rezerwacje identyfikatorów do przeglądu. Zmiana polskiej etykiety nie powinna zmieniać istniejącej tożsamości.
- [Walidacja planu](Tools/ContentPass2/validation.json): 30 kontroli PASS, zero FAIL; hash zweryfikowanego manifestu.
- [Instrukcja generatora i walidatora](Tools/ContentPass2/README.md): sposób aktualizacji po zmianach arkusza, zależności i granice modelu.

Manifest zachowuje obecne ID nawet tam, gdzie nazwa lub cena ma zmienić się w pass 2. Nazwy są dopasowywane dokładnie po normalizacji jawnie wymienionych aliasów; brak dopasowań rozmytych i automatycznego łączenia dwóch definicji o podobnej nazwie. Nowe propozycje ID nie są zmianą katalogu runtime.

## Zamknięte decyzje źródłowe

| Sprawa | Przyjęta reguła | Miejsce wymagające późniejszego authoringu |
| --- | --- | --- |
| Skóry Elfich Ruin | Wyłącznie skóra leoparda; skóra aligatora pozostaje przy Morzu i naturalnym przyszłym źródle | `Lootbagi!F42` zastąpione w planie decyzją użytkownika; nie zmieniano XLSX |
| Dobry i Mocny proch strzelniczy | Drop z toreb zasobów wszystkich pięciu dostępnych biomów; odpowiednio przedziały L21–30 i L31–40 | `Nowe przedmioty!D158:D159` oraz rozszerzenie tabel łupu |
| Jaspis, Perła, Malachit | Obaj bossowie odpowiedniego biomu, zakres wypraw L1–40 | `Biżuterie i Galduryty!O3:Q5` i decyzja użytkownika; poziom materiału 11 nie jest minimalnym poziomem dropu |
| Obsydian i Jadeit | Zachować bossowe źródła CP2-01 w obu dotychczasowych przedziałach L1–10 i L11–20, rozszerzyć plan do L40 | Nie przywracać ich do toreb zasobów ani nie usuwać istniejących dropów z bossów L10 |
| Ucho elfa | Nowe dropy i crafting wskazują `item.vs10.ucho_elfa` | Zachować `item.material.elf_ear` w starych zapisach; bez cichej konwersji |
| Bossowie Lasu | Obaj dają Żywioł lasu | Krew przywódcy nie jest nowym składnikiem ani nowym dropem |
| Pył energetyczny | Oba dawne ID wyłącznie legacy | Bez nowych pul i receptur |
| Przyszłe Pustynia/Świątynia/Góry | Zachować właścicieli naturalnych; do czasu dodania biomów działają jawne źródła zastępcze | Dodanie przyszłego biomu nie kasuje automatycznie zastępczego źródła |

### Cztery zastępcze materiały bossowe

| Materiał | Źródło dostępne w pass 2 | Poziomy dropu |
| --- | --- | --- |
| Krew berserka | Szkielet Zabójca, Katakumby | 31–40 |
| Zaczarowana dłoń | Duch Umarłego Króla, Katakumby | 31–40 |
| Energia Malada | Demoniczny Jarl, Elfie Ruiny | 31–40 |
| Łuski wiwerny | Alfa-Rekin, Morze | 31–40 |

Łączne wiersze `Lootbagi!93:99` nadal mają zapis L11–40. Manifest rozdziela dodatkowe materiały L31–40 od materiałów bazowych tych bossów zgodnie z przyjętym planem. Sam poziom materiału z `Nowe przedmioty!E124:E135` nie nadpisuje osobno opisanych zakresów istniejących dropów bazowych.

## Kompatybilność i istniejące modele danych

Zachowano nazwy i ID istniejących definicji oraz osobną informację o proponowanym poziomie/cenie. Przykładowo Jadeit pozostaje `item.runic.jadeite`; aktualna cena 2847 i propozycja 3032 z arkusza są oddzielnymi wartościami, a nie nowymi przedmiotami. Decyzję balansu ceny wdraża właściwa karta.

Receptury alternatywne igieł są osobnymi wariantami. Nowa receptura Granitowego kamienia galdurytu zastępuje starszą w planie zamiast występować równolegle jako druga darmowa ścieżka. Skondensowane proszki korzystają z jawnego selektora czterech sproszkowanych metali L21; nie wymagają wszystkich czterech naraz.

Biżuteria zachowuje dotychczasową tożsamość opartą na materiale i ilości. `JewelryMetalData` nie otrzymuje fikcyjnego nowego ID definicji metalu; `GemId` nadal pochodzi z `CraftingMaterial.Id`. Nowe warianty metali i gemów są osobnymi rekordami planu do podpięcia do istniejących czterech typów biżuterii. Galduryty i mikstury wariantowe pozostają instancjami istniejących modeli; nie tworzymy osobnego item ID dla każdego możliwego rolla.

Istniejąca definicja `tree.scout.guardian` jest zachowana jako definicja wymagająca przeglądu przypisania/reworku, a nie dublowana pod nowym ID. Placeholder trzeciego drzewa Paladyna nie jest drugim Krzyżowcem: ma klucz planistyczny, bez wymyślonej nazwy i runtime ID. Pasywy globalne są odróżnione od node'ów.

Zapisane przedmioty, stary loot i efekty pozostają bez zmian. Runtime nie konsumuje manifestu. Docelowy authoring skilli musi użyć systemu stabilnych ID efektów z CP2-01; ten generator nie zmienia historycznych aliasów zapisów.

## Jawnie odłożone elementy

| Element | Właściciel | Co pozostaje do przygotowania |
| --- | --- | --- |
| Strażnik | CP2-08/09 | Docelowy projekt Defense + Utility i Impetu; obecne 25 node'ów i pasyw to materiał do przeglądu |
| Zbrojny T6/T7 | CP2-06 | Decyzja o włączeniu czterech node'ów i skilla T7 do zakresu L40 |
| Paladyn | CP2-13/14 | 5 node'ów T5 Krzyżowca oraz trzecie drzewo: 25 node'ów i pasyw; łącznie 31 placeholderów |
| Nowe unikaty | CP2-42/43/44 | Efekty wszystkich nowych unikatów; 5 brakujących nazw: Alfa-Rekin 1, Kapitan 2, Bagnisty Ent 1, Ludożerna Roślina 1 |
| Materiały przeciwników L21 | CP2-19 i karty biomów | 11 nazw ma źródła i zastosowania w tabelach, ale brak samodzielnych wierszy definicyjnych w `Nowe przedmioty`: Ektoplazma, Pazur ghula, Starożytna skóra, Histyna, Pióro ruka, Pozawymiarowa macka, Błyszczący piasek, Płetwa rekina, Żywioł wiatru, Płuco topielca, Ssawka komara |
| Wydajność receptur | CP2-20/21 | D160 i dziewięć komórek D163/D165:D172 nie określają jawnie liczby wyników. D164 podaje 2 sztuki; tej wartości nie rozciągano na pozostałe proszki |
| Liczby i jednostki walki | CP2-03 i karty klas/biomów | Rebalans do zintegrowanej bazy, m.in. liczba ranków względem opisanych wartości; nie uznawać samych nazw node'ów za zamknięty balans |
| Szanse, ilości i tempo zdobywania | CP2-31/36/41/46/53 | Model gospodarki, poziomy ofert i blokady progresji; katalog nie zatwierdza dawnych szans |

Powtarzająca się nazwa node'a, np. Śnieżny Duch w drzewie Mrozu, nie łączy efektów ani nie tworzy kolizji ID. Tożsamości rezerwowane są jawnie na poziomie pozycji projektu; nazwy można doprecyzować przy karcie drzewa.

### Porządkowanie dawnych referencji testowych

W `GC_MainContentCatalog.asset` pozostają trzy GUID-y bez obecnych assetów. Sprawdzono ich pochodzenie w commicie `0ae2dc088`:

| GUID | Dawna definicja | Dawne ID |
| --- | --- | --- |
| `b6d1f8a3c5e74920a4d7b2f9c6e1a580` | ID_TestGalduryte | `item.galduryte.test.01` |
| `c7e2a9b4d6f85130a5c8e3f1b7d9a260` | ID_TestPotion | `item.potion.test.01` |
| `d8f3b1a6c9e24750a4d7e2f8b5c1a690` | ID_TestValuable | `item.valuable.test.01` |

Nie są to brakujące materiały content passu. Walidator raportuje je jako znane porządkowanie starej bazy, nie dopisuje im nowych przedmiotów. Usunięcie pustych wpisów z katalogu należy wykonać przez Editor API podczas zatwierdzonego authoringu/porządkowania katalogu; w tej karcie serialized state pozostaje bez zmian. Każdy inny nieznany brak GUID jest błędem blokującym ekstrakcję.

## Weryfikacja i granice wyniku

Przeliczenie grafu potwierdza osiągalność 406/406 pozycji objętych planem. Sprawdzono osobno źródła zastępcze, materiały dla części L25/L35 i alchemii B/C, oba bossy Lasu, canonical Ucha elfa, brak nowych źródeł legacy, komplet siatki wyposażenia, ID i zakresy L1–40. Walidator ma także przypadki kontrolne cyklu bez źródła, alternatywnych receptur i wymagania wszystkich składników jednej receptury.

Łącznie **30 kontroli PASS, zero FAIL**. Proponowane ID sprawdzono względem istniejących root/nested ID; brak kolizji. Powtórne generowanie bez nowych rezerwacji zachowuje wynik. Mapa materiałów zachowuje wszystkie wcześniejsze naturalne przypisania dostępnych biomów z wyjątkiem jawnie zastąpionej przez użytkownika skóry aligatora w Ruinach; stara nazwa skóry skorpiona jest odczytywana jako wielbłąd.

Nie jest to dowód osiągalności L40 w bieżącym runtime. Nowe biomy, dropy, przepisy i progi trzeba wdrożyć w odpowiednich kartach. Graf nie liczy podaży, prawdopodobieństwa, ilości, czasu ani finalnych odblokowań ofert. Nie uruchamiano Unity ani testów runtime, bo nie zmieniono kodu C# ani danych gry.

Arkusz XLSX, serialized Unity state, zapisy i wcześniejsze lokalne zmiany użytkownika pozostają bez zmian. Użytkownik zaakceptował katalog poleceniem `git` 2026-10-31. Odłożone projekty pozostają przypisane do swoich kart.
