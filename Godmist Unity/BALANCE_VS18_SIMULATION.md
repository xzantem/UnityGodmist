# VS-18 — symulacja pierwszej pętli, 2026-09-25

To jest pomiar wygenerowanych wypraw oraz model ekonomii i progresji, nie automatyczny playtest walk. Nie zmieniono tu liczb gameplayowych. Skrypt i surowe wyniki są w `.codex/vs18_balance_sim.py`, `.codex/vs18_balance_floors.csv` i `.codex/vs18_balance_results.json`.

## Dane i metoda

- Izolowana kopia Unity wygenerowała 20 seedów na poziomie 5 i 15 dla wyprawy zwykłej oraz bossowej: 80 wypraw, 160 pięter. Test EditMode 1/1 PASS. Zapisano faktyczne zwykłe encountery, wrogów, skrzynie, złoto i liczbę instancji przedmiotów w skrzyniach. Boss jest osobnym encounterem i nie wchodzi do poniższych średnich zwykłej wyprawy.
- Monte Carlo: 10 000 losowań dla L5 i L15, z dokładnym wzorem złota przeciwnika, wygenerowanym złotem skrzyń, dwiema standardowymi misjami oraz ich wariantami 30% XP, 30% złoto i 40% mix. Zakłada ukończenie każdej walki, zebranie każdego łupu i odbiór obu misji. Nie dolicza sprzedaży, bossa, unikatów, bonusu dropu ani wydatków na mikstury/upgrade/reforge.
- Symulacja materiałów odtwarza aktualne pule `ID_Catacombs_Resources`: 50% torby zasobów z każdego zwykłego wroga, po 2–3 takie torby w każdej skrzyni, szanse kategorii i wybór wpisu. Ceny i sześć części broni+pancerza na klasę pochodzą z bieżącego `design/content_pass_1.xlsx`. Wybrano po jednym wariancie receptury korzystającym z lokalnych surowców Katakumb; nie jest to porównanie wszystkich metali, skór ani statystyk gotowego sprzętu. Limity lokalnej oferty pochodzą z `TRD_Arungard_Blacksmith.asset`. Brak zapasów z wcześniejszych wypraw. Wariant zakupu zakłada pełne odnowienie stocku i brak zniżki lojalnościowej.
- Dostępność zestawu wymaga równoczesnego zebrania wszystkich rodzajów surowców w ilościach z receptury; nadmiar jednego surowca nie zastępuje brakującego innego. Wynik zakłada jednak pełne czyszczenie wypraw w tym samym motywie (Katakumby), z pełnym odbiorem łupu. Nie reprezentuje tempa wyposażania przy przeplataniu różnych motywów lochów, omijaniu walk lub skrzyń ani wydawaniu części materiałów na inne cele.
- Przebieg poziomów: 3000 trajektorii na 0, 1 lub 2 ukończone misje na wyprawę; wybór oferty na poziomie równym średniemu poziomowi równej drużyny przy wyruszeniu. Tablica oferuje również poziomy z odchyleniem, których model nie obejmuje. XP przeciwników przelicza się przy wzroście poziomu, a nagroda misji pozostaje na poziomie ustalonym przy przyjęciu. Rozmiary pojedynczych encounterów są aproksymowane z liczby wrogów i walk próbki; nie uwzględnia to premii XP z przedmiotów ani bossa.

## Wynik zwykłej dwupiętrowej wyprawy

| Miara | L5 | L15 |
|---|---:|---:|
| Zwykłe walki, średnio | 10,25 | 10,25 |
| Zwykli wrogowie, średnio | 31,2 | 31,2 |
| Skrzynie, średnio | 4,75 | 4,75 |
| Instancje łupu skrzyń przed złożeniem w stosy | 120 | 161 |
| Złoto: średnio; 10.–90. percentyl | 1365; 1127–1564 | 5316; 4407–6084 |
| W tym złoto: wrogowie / skrzynie / 2 misje | 561 / 660 / 144 | 2191 / 2566 / 559 |
| XP na każdą aktywną postać: średnio | 25 122 | 111 965 |
| XP jako część progu poziomu na starcie | 172% | 158% |

Gold ze skrzyń to około 48% dochodu z walk, skrzyń i dwóch misji. 10.–90. percentyl pokazuje rozrzut wyników w próbie i losowaniach, nie gwarantowany zakres każdej wyprawy. Instancje przedmiotów nie są liczbą różnych typów; większość materiałów może się złożyć w stosy.

| Ukończone zwykłe misje na wyprawę | Średnie wyprawy L1→L20 | Średnie walki | Czas przy 20–30 min na wyprawę |
|---|---:|---:|---:|
| 0 | 29,6 | 303 | 9,9–14,8 h |
| 1 | 18,8 | 193 | 6,3–9,4 h |
| 2 | 14,0 | 144 | 4,7–7,0 h |

To trajektoria tej samej aktywnej drużyny; nie obejmuje zadań bossa, porażek, zmiany składu, czasu w mieście i przerw. Przy dwóch misjach tempo do L20 mieści się w kilku godzinach, ale ponad jeden poziom na pojedynczą wyprawę jest normą.

## Czy jedna wyprawa finansuje jeden set?

Set = normalna broń i pancerz jednego towarzysza z wybranych lokalnych wariantów sześciu części. Każdy z czterech setów jest oceniany na alternatywnym, pustym stanie początkowym. W zbiorczym teście maksymalnej liczby setów każdy surowiec i każda sztuka stocku może być wykorzystana tylko raz. To liczba najłatwiej dostępnych poprawnych setów, nie liczba zestawów optymalnych statystycznie.

| Poziom | Z samych materiałów z łupu: 1 / 2 / ≥3 sety | Z łupu, złota i pełnego lokalnego stocku: 1 / 2 / ≥3 sety |
|---|---:|---:|
| L5 | 11% / 76% / 13% | <1% / 41% / 59% |
| L15 | 1% / 64% / 35% | <1% / 12% / 88% |

Zero setów występuje odpowiednio w około 0,1% i 0% prób z samych łupów. Procenty zaokrąglono. To **możliwość** stworzenia tylu setów, nie prognoza, że gracze zawsze to zrobią. Model nie odejmuje surowców wydanych na inne cele. Materiały rzemieślnicze w obecnych assetach mają wagę 0, a inventory nie ogranicza liczby slotów, więc udźwig nie zatrzymuje takiego zbioru.

Warianty czułości dla jednego poziomu, od początku bez zapasów, przy lokalnym stocku:

| Zmiana | L5: udział 1 / 2 / ≥3 sety | L15: udział 1 / 2 / ≥3 sety |
|---|---:|---:|
| Obecnie | <1% / 41% / 59% | 0% / 12% / 88% |
| Torba zasobów z wroga 40% zamiast 50% | 1% / 62% / 38% | 0% / 24% / 76% |
| 1–2 torby zasobów na skrzynię zamiast 2–3 | 2% / 69% / 29% | <1% / 33% / 67% |
| +50% surowców w normalnych recepturach, uproszczone | 23% / 76% / 1% | 2% / 85% / 14% |
| Podwójne surowce, uproszczone | 78% / 16% / 0% (7% zero) | 23% / 77% / <1% |

Zmiany w tabeli są **wariantami do porównania**, nie nową decyzją ani implementacją. Mnożnik receptur skaluje końcowe zapotrzebowanie na surowce, z zaokrągleniem; rzeczywista zmiana partii w recepturze może dać inny wynik. Jeden globalny mnożnik nie ustawia jednakowego tempa T1 i T2. Szczególnie zestaw Zwiadowcy T2 wymaga 7 skór jelenia, podczas gdy T1 wymaga 19 skór kozy, przy podobnej liczbie losowań odpowiedniej skóry z toreb.

## Wyprawa i zdrowie — tylko test wrażliwości

Bez headless wykonania rzeczywistych walk nie ma wiarygodnej wartości „ile HP traci drużyna na encounter”. Dla czterech postaci, faktycznej liczby walk z próbki, dwóch obozów przy około 1/3 i 2/3 wyprawy, leczenia 40% maksymalnego HP na obóz i niezależnych strat na osobę (CV 35%), szansa dotarcia wszystkich żywych do końca wynosi:

| Przyjęta **strata netto HP** na postać i walkę | Wynik syntetyczny |
|---|---:|
| 8% | 100% |
| 12% | 99% |
| 16% | 46% |
| 20% | 7% |

Strata netto oznacza wynik **po** leczeniu w walce. To nie estymacja zachowania AI, umiejętności ani faktycznej śmiertelności; model uznaje każde zejście do 0 HP za niepowodzenie i nie dopuszcza ratowania powalonych. Pokazuje za to, jak silnie powodzenie dziesięciowalkowej wyprawy zależy od realnej straty HP. Dwa obozy dają łącznie najwyżej 80% maksymalnego HP, więc bez innych źródeł leczenia budżet to najwyżej 180% HP na około 10,25 walki, zanim uwzględni się marnowanie leczenia i nierówny focus.

## Wnioski do następnej iteracji

1. Nie zmieniać teraz XP tylko na podstawie modelu. Przy dwóch misjach wychodzi około 14 wypraw do L20; trzeba zmierzyć realny czas wyprawy i częstotliwość odbioru obu zleceń.
2. Cel „jedna wyprawa finansuje jeden normalny set” jest przekroczony dla łatwo dostępnych lokalnych wariantów: materiały z samej wyprawy zwykle wystarczą na dwa sety. Złoto nie jest głównym ograniczeniem tego craftingu. Trzeba jeszcze porównać statystyki tych setów z droższymi alternatywami, zanim zmieni się liczby. Ewentualne dopasowanie powinno uwzględnić osobno T1/T2 i receptury klas; niewielka globalna korekta dropu lub skrzyń nie wystarcza.
3. Nie da się jeszcze stwierdzić, czy walki są za łatwe lub zbyt ciężkie. Potrzebne są co najmniej straty HP/many i liczba tur z rzeczywistych walk na L5, L10, L15 i L20, z normalnym wyposażeniem oraz bez paladyna. Na ich podstawie dopiero kalibrować obóz, mikstury i przeciwników.
