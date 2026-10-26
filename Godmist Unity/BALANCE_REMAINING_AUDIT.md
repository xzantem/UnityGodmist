# Audyt pozostałych ustaleń balansu — 2026-09-21

Zakres: porównanie rozmowy i BALANCE_DECISIONS_PENDING.md z aktualnym kodem, zapisanym GameDefines.asset i design/content_pass_1.xlsx. Odczyt statyczny, bez nowych testów runtime i bez zmian gameplayu/arkusza. Nie jest to acceptance całego contentu ani deklaracja pełnego sprawdzenia wszystkich node’ów.

## Potwierdzone w kodzie

- Rozwój wyposażenia, cenniki upgrade/reforge, utrata postępu i nieaktywne sloty: wdrożony pakiet, wcześniejsze 77/77 testów PASS.
- Penetracja obrona/(1+pen), sumaryczna regeneracja, kondensacja i poziomowa moc bomb: wdrożony pakiet, wcześniejsze 48/48 PASS.
- XP przeciwników, kara tylko za słabszych, nagroda standardowej misji i nowy próg awansu: kod wdrożony, wcześniejsze 69/69 PASS. Aktywacja konfiguracji ma rozbieżność opisaną poniżej.
- Wyjmowanie galdurytu: zapisany crafting.galduryte.extraction_cost_percent ma CurrentFloatValue=0.7. Ten krok konfiguracji jest wykonany.
- Lootbagi: LootTableSystem.RollLootBagContents losuje niezależnie każdą pulę, potem jedno poprawne entry z równymi szansami i jego ilość. Mechanizm zgadza się z kategoriami A–Z; nie wymaga przebudowy. Nie oznacza to potwierdzenia zawartości wszystkich tabel ani częstotliwości dropu.

## Konkretne rozbieżności do zamknięcia

1. **XP — zapis konfiguracji.** Assets/_Project/Resources/GameDefines.asset nadal zawiera CurrentFloatValue=12 dla progression.experience.formula_coefficient i formula_constant; exponent=1.5. Użytkownik potwierdził zmianę, ale na dysku nie ma 1200/1200. Możliwy niezapisany asset lub zmiana innego źródła; przyczyny nie potwierdzono. Nie nadpisywano ustawień użytkownika.
2. **PA — przeciwne zaokrąglenia.** CombatActionCostCalculator.CalculatePercentageCost używa CeilToInt, a CombatTurnSystem.CalculateMaxActionPoints i CombatSessionFactory używają RoundToInt dla szybkości. Rozmowa wskazuje kierunek: PA ze szybkości w górę, koszt skilla w dół. Przed zmianą określić zachowanie dodatniego kosztu zaokrąglonego do zera; nie wprowadzić nieświadomie darmowych akcji. Uwzględnić również przywołania i wspólne wywołania kalkulatora.
3. **Mieszanie tierów części.** EquipmentGenerationService.GenerateFromSelectedParts waliduje klasę i typy części, ale nie wymaga równego LevelBracket; poziom bierze z maksimum. BlacksmithCraftingService korzysta z tej metody. Wypowiedź użytkownika wskazuje plan zakazu mieszania tierów; bieżący kod go nie egzekwuje. Dalszy pakiet powinien objąć walidację i czytelny powód odrzucenia w craftingu, bez niszczenia już istniejących przedmiotów.
4. **Hard CC — mechanika częściowo istnieje.** BattleUnit przechowuje tolerancję per statusId: +25 pp za stack, domyślnie 2 tury. StatusEffectSystem dodaje ją po nałożeniu statusu. To nie wspólna odporność na wszystkie hard CC; różne statusy mają oddzielne liczniki. GetStatusTolerance używa stałego 0.25, mimo obecności combat.status.tolerance_per_stack w GameDefines. Trzeba doprecyzować wspólną tolerancję kontra per status, listę CC i wygasanie; nie oznaczać systemu jako całkiem nieistniejącego.

## Drzewka i Uzdrowienie — stan ustaleń

Aktualny XLSX potwierdza:
- Startowe skille H12/H13: Uzdrowienie 30 MP, 40% PA, 120% leczenia.
- Drzewka wiersze 35/37: Pragnienie Zemsty 1/2/3 za 5% utraconego maksymalnego HP; Wściekłe Uderzenia redukują koszt o 10/20/30%.
- Wiersz 78: Podcięcie Gardła ma +100% obrażeń przeciw bossom.
- Wiersze 164/165: Agresywny Ogień 50% szansy, 50% siły, trzy pokolenia; Żywienie Ogniem 10/20%.
- Wiersze 17/201: Rozkaz Przegrupowania 70% PA; Uzdrowienie usuwa wykorzystaną zniżkę Uzdrowiciela.

Do uszczegółowienia opisów przy zachowaniu już podjętych decyzji:
- Agresywny Ogień nadal mówi „siły pierwotnego podpalenia”; powinno być jednoznacznie 50% **bezpośredniego rodzica**, oddzielny rzut dla każdego celu i dziedziczenie numeru generacji.
- Odkrycie Słabości (wiersz 175) mówi „mają 15%/40% na otrzymanie obrażeń krytycznych”, nie wyjaśnia względnego zwiększenia szansy, ustalonego w rozmowie.
- Pole Osądu (wiersz 219) nie opisuje jednego pola na mapie i zastępowania poprzedniego.
- Amok/Trauma: brak stackowania jest decyzją rozmowy; sam opis nie potwierdza konfiguracji runtime.

W Assets/_Project/ScriptableObjects/Skills/SkillTrees znaleziono definicje Scouta (Assassin, BladeDancer, Guardian), bez pełnego zestawu ośmiu wybranych drzewek. Istniejący SD_Heal to generyczny Heal z ResourceCost=0; nie można przedstawiać go jako wdrożonego Uzdrowienia Paladyna. Pełne pokrycie zachowań wszystkich node’ów wymaga osobnego audytu danych efektów i testów. Brak osobnej klasy nazwanej jak node nie dowodzi braku obsługi w generycznym frameworku. Na tym etapie potwierdzono decyzje i wybrane braki opisów, nie ukończenie drzewek.

## Nadal potrzebne decyzje lub końcowe obliczenia

- Docelowe skalowanie przeciwników i budżety składów encounterów. Różne liczebności i osobne typy elit są kierunkiem, nie ustaloną tabelą statystyk. Nie stosować liniowej korekty do nieliniowego skalowania bez przeliczenia.
- Szanse dropu poszczególnych toreb zwykłych mobów nie zostały w rozmowie ustalone. Bez nich nie da się zamknąć tempa materiałów/ekwipunku, nawet przy gotowej zawartości torby.
- Skrzynki: 2–3 na sześć encounterów i zawartość za 2–3 walki/torebki to przyjęty model. Wspólny bilans dochodów musi objąć również misje, wydatki i cel „jedna wyprawa finansuje jeden set”. CombatEconomyScaling nadal opiera złoto na bazie 10 i wzroście 4^((L-1)/10); sam wzór nie potwierdza docelowego dochodu netto wyprawy.
- Odpoczynek/interactables i przeciwdziałanie stallowaniu: potrzeba zaakceptowana, konkretne zasady nieustalone. Nie rozpoczynać nowego systemu ani dobierać liczb bez decyzji.
- Porównanie buildów z pełnymi skillami oraz finansowanymi galdurytami, HP pancerzy/krytów/zasobów pozostaje końcową weryfikacją balansu. Wcześniejsze testy techniczne nie zastępują tej symulacji.

## Kolejność domknięcia bieżącego etapu

1. Wyjaśnić zapis XP; zakończyć mały pakiet PA + zakaz mieszania tierów, po rozstrzygnięciu minimalnego kosztu akcji.
2. Ujednoznacznić opisane wyżej reguły node’ów i tolerancję hard CC; następnie zweryfikować wymagane mechaniki, bez ogólnego authoringu katalogu VS-01.
3. Ustalić szanse toreb i budżety encounterów, policzyć gospodarkę oraz trudność z aktualnymi wzorami i wyposażeniem.
4. Spisać zasady odpoczynku/anti-stall i kryteria końcowych prób balansu. Dopiero potem uznać bieżące ustalenia balansu za domknięte; pełny playtest pozostaje zależny od gotowego contentu.

## Aktualizacja po audycie — PA

Użytkownik zaakceptował setne PA zamiast zaokrąglania do całych punktów. Kod wdrożono wraz z ruchem, impetem, bonusami i prezentacją ułamków ikon; 175/175 testów PASS. Punkt 2 jest zastąpiony sekcją 11 BALANCE_DECISIONS_PENDING.md. Podłączenie komponentu ikony i smoke pozostają w handoffie. Pozostałych punktów audytu nie wdrażano w tej karcie.

## Aktualizacja 2026-09-22

XP na dysku potwierdzone: coefficient i constant CurrentFloatValue=1200. Rozbieżność zamknięta. PA (w tym fill i highlighty), wspólna tolerancja Suppression z czasem 2 + sumowane długości CC oraz zakaz mieszania tierów wdrożone w kolejnych pakietach; poprzednie opisy tych braków są historyczne. Szczegóły i wyniki testów w BALANCE_DECISIONS_PENDING.md. Kolejna analiza gospodarki: BALANCE_LOOT_PACING.md; podane szanse to propozycje, nie przyjęty balans.
