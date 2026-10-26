# Materiały a crafting — 2026-09-22

Źródło: design/content_pass_1.xlsx, arkusze Lootbagi D4:E14, Materiały A2:E123, Wyposażenie A2:S97. Bez edycji XLSX i runtime. Model: piętro z 6 walkami po 4 zwykłe moby; niezależnie 50% na torbę zasobów; 2 lub 3 skrzynki z równym prawdopodobieństwem, każda 2 lub 3 równoważniki torby, też z równym prawdopodobieństwem. To jawne założenia rozkładu dla symulacji, nie dodatkowe zatwierdzone reguły. Średnio 18.25 torby na piętro. Bez NPC, początkowego zapasu, bossa, misji, bezpośrednich dropów i wykorzystania wylosowanego wyposażenia.

Kategorie B/C/D losowane niezależnie z prawdopodobieństwem 1/0.7/0.3; po jednym z pięciu materiałów, równe szanse, jednostajna całkowita ilość. Zgodnie z późniejszą decyzją użytkownika dopuszczono powtórzenia, mimo pozostałych w XLSX dopisków „inny niż B/C”. Średnie liczności materiałów są takie same przy losowaniu bez powtórzeń, ale wariancja różna. Nie zmieniano modelu ani arkusza automatycznie. Wiersze alkoholu/prochu oraz galdurytów również zawierają starsze zapisy; nie wpływają na ten bilans materiałów kowalskich.

## Podaż średnia

| Materiał | T1 | T2 |
|---|---:|---:|
| Ruda żelaza | 29.2 | 29.2 |
| Skóra kozy / jelenia | 14.6 | 10.95 |
| Drewno lokalne | 10.95 | 10.95 |
| Włókno lokalne | 25.55 | 25.55 |
| Pył lokalny | 10.95 | 10.95 |
| Węgiel | 0 | 36.5 |

## Zapotrzebowanie normalnego kompletu

Komplet oznacza broń + pancerz, po trzy części; bez biżuterii/galdurytów. Wybrano warianty z materiałów dostępnych w Katakumbach, nie maksymalizowano statystyk. Liczono pełne partie pośrednich receptur od zerowego zapasu, z agregacją wspólnych półproduktów. Normalna jakość = mnożnik materiałów 1. Warianty słabe/doskonałe nie są objęte poniższymi wynikami. Alias „igła od nici” potraktowano jako „igła do nici”; dla igieł wybrano dostępną lokalnie alternatywę żelazo/stal.

- T1, Wojownik: Ruda żelaza: 44, Skóra kozy: 6, Drewno modrzewiowe: 2. Szansa uzbierania całego zestawu po piętrze: 10.2%. Wiersze części: 2, 2, 10, 10, 18, 18.
- T1, Zwiadowca: Ruda żelaza: 16, Skóra kozy: 11, Drewno modrzewiowe: 2. Szansa uzbierania całego zestawu po piętrze: 66.9%. Wiersze części: 26, 26, 34, 34, 42, 42.
- T1, Czarodziej: Ruda żelaza: 8, Skóra kozy: 6, Włókno bawełny: 24, Drewno modrzewiowe: 2. Szansa uzbierania całego zestawu po piętrze: 53.1%. Wiersze części: 50, 50, 58, 58, 66, 66.
- T1, Paladyn: Ruda żelaza: 36, Skóra kozy: 8, Drewno modrzewiowe: 2. Szansa uzbierania całego zestawu po piętrze: 24.5%. Wiersze części: 74, 74, 82, 82, 90, 90.
- T2, Wojownik: Ruda żelaza: 44, Węgiel: 22, Skóra jelenia: 2, Drewno brzozowe: 2. Szansa uzbierania całego zestawu po piętrze: 10.5%. Wiersze części: 6, 6, 14, 15, 23, 23.
- T2, Zwiadowca: Ruda żelaza: 16, Węgiel: 8, Skóra jelenia: 4, Drewno brzozowe: 2. Szansa uzbierania całego zestawu po piętrze: 86.9%. Wiersze części: 30, 31, 39, 39, 46, 47.
- T2, Czarodziej: Ruda żelaza: 8, Węgiel: 4, Skóra jelenia: 2, Włókno palmy: 24, Drewno brzozowe: 2. Szansa uzbierania całego zestawu po piętrze: 55.0%. Wiersze części: 54, 55, 63, 63, 71, 71.
- T2, Paladyn: Ruda żelaza: 36, Węgiel: 18, Skóra jelenia: 3, Drewno brzozowe: 2. Szansa uzbierania całego zestawu po piętrze: 26.7%. Wiersze części: 78, 78, 87, 87, 95, 95.

Symulacja 100000 pięter na tier, generator numpy seed 220926; nie jest to test runtime. Szanse dla poszczególnych postaci oznaczają wydanie wszystkich potrzebnych materiałów na jedną wybraną postać, nie cztery jednocześnie. Zaokrąglenie do pełnych procentów jest wystarczające dla wniosków.

## Wnioski i propozycje

1. Nie podnosić teraz ogólnej szansy torby ponad przyjęte 50%. Niedobór dotyczy określonego surowca, nie wszystkich materiałów. Cztery normalne sety potrzebują łącznie 104 rudy, czyli około 3.56 piętra średniego dopływu samej rudy (wskaźnik podaży, nie oczekiwany czas zebrania kompletu).
2. Wojownik ma średnio około 15.4 brakującej rudy po piętrze, paladyn 8.8. Propozycja: dostępny u kowala niewielki zapas lokalnej rudy, np. 15–20 sztuk na wyprawę/odnowienie oferty, aby złoto uzupełniało niedobory. Zasady odświeżania/dostępności wymagają decyzji; nie dotyczy gwarancji materiałów z innych lochów.
3. Zwiadowca T2 jest łatwiejszy do wyposażenia z materiałów niż T1: jedna skóra jelenia daje trzy wyprawione. Skóry i węgiel będą narastać, jeśli ruda ograniczy crafting. Nie zwiększać ich dropu.
4. Alternatywa do NPC: zwiększyć ilość rudy w istniejących entry z 3–5 do 4–6 (+25% podaży, średnio 36.5). To nadal nie gwarantuje zestawu wojownika i wpływa na wszystkie klasy. Na razie rekomendowany jest zakup braków, nie automatyczna korekta dropu.
5. Torba broni/pancerza ma średnio 3.55 sztuki. Same moby przy 2% dropu to 1.704 broni i 1.704 pancerza na piętro. Nie muszą pasować klasą/jakością ani być upgrade'em. Sprzęt ze skrzynek nie policzony: proponowany dodatkowy model nie został zatwierdzony. Torba galdurytów ma 5 sztuk średnio, więc 1% daje 1.2 galdurytu z mobów od L11.
6. Cel „jedna wyprawa finansuje jeden set” nie oznacza „sam drop zawsze daje wszystkie składniki”. Ten model jest zgodny z potrzebą zakupienia braków, ale ostateczna dostępność u NPC, marże oraz liczba pięter wymagają wspólnego bilansu przed uznaniem celu za osiągnięty.
