# Godmist — ustalenia balansu do wdrożenia

Rejestr rozmowy zapisany 2026-09-21. Powiązanie: KANBAN / VS-18 oraz content pass VS-01.
Ten dokument zapisuje decyzje projektowe; nie jest deklaracją ich implementacji.
Przed realizacją sprawdzić bieżący kod i arkusze. Nie nadpisywać zmian użytkownika.

## 1. Zatwierdzone — rozwój wyposażenia

- [x] Koszt usług ma zależeć od aktualnej wartości przedmiotu i wszystkich składników jej wyceny, w tym części, jakości, rzadkości i postępu ulepszenia. Ujednolicić definicję wartości; nie utożsamiać bez sprawdzenia kosztu materiałów, wartości przedmiotu i ceny handlowej NPC.
- [x] Ulepszanie: koszt próby jako różnica jednego cennika całkowitego `F(cel) - F(obecny postęp)`. Przy tej samej szansie i rzadkości podział udanych ulepszeń na etapy nie powinien zmieniać łącznej ceny, poza świadomie ustalonymi zaokrągleniami. Wyceniać każdy punkt krzywej spójnie; nie używać wartości sprzed próby do obu końców tak jak obecny kod.
- [x] Maksymalny przyrost w jednej próbie: 20 punktów procentowych. Mniejsze kroki dozwolone.
- [x] Porażka ulepszenia odbiera 10–20 punktów procentowych postępu, nie 10–20% obecnego postępu. Minimum 0%.
- [x] Upgrade zwiększa atak broni / obrony pancerza; daje jeden slot galdurytu na każde pełne 20% (maksymalnie 5).
- [x] Reforge: liniowa stawka `0.25 + 0.15 * max(0, liczba stopni ponad Common)`, mnożona przez aktualną wartość przedmiotu. Zaokrąglenie ceny do całego złota w górę. Godly nie podlega dalszemu reforgowaniu.

| Aktualna rzadkość | Stawka próby |
|---|---:|
| Trash, Broken, Damaged, Common | 25% |
| Uncommon | 40% |
| Rare | 55% |
| Ancient | 70% |
| Legendary | 85% |
| Mythic | 100% |

To zastępuje wcześniejsze propozycje osobnych dopłat na Legendary i Mythic (37,5/50%, potem 75/100%). Nie wdrażać kilku wariantów równocześnie.

- [x] Zachować obecne szanse reforgowania:

| Aktualna rzadkość | Awans | Bez zmiany | Spadek |
|---|---:|---:|---:|
| Trash | 90% | 10% | 0% |
| Broken | 80% | 15% | 5% |
| Damaged | 80% | 15% | 5% |
| Common | 70% | 20% | 10% |
| Uncommon | 65% | 20% | 15% |
| Rare | 60% | 25% | 15% |
| Ancient | 55% | 25% | 20% |
| Legendary | 45% | 30% | 25% |
| Mythic | 35% | 40% | 25% |

## 2. Przyjęty kierunek mnożników rzadkości

Użytkownik chce, aby wysokie rzadkości dawały dużą nagrodę, ale były drogie i ryzykowne. Poniższa rosnąca tabela była podstawą kolejnych zaakceptowanych kierunkowo porównań; jest punktem wyjścia do balansu, nie wynikiem końcowych testów.

| Rzadkość | M |
|---|---:|
| Trash | 0.70 |
| Broken | 0.80 |
| Damaged | 0.90 |
| Common | 1.00 |
| Uncommon | 1.10 |
| Rare | 1.25 |
| Ancient | 1.45 |
| Legendary | 1.75 |
| Mythic | 2.15 |
| Godly | 2.70 |

- [x] Mnożyć wkład przedmiotu po złożeniu części i uwzględnieniu jakości, nie bazowe statystyki postaci. Nie kumulować mnożników kolejnych rzadkości; zawsze przeliczać od niezmodyfikowanej podstawy.
- [x] HP, szansa kryta i wkład do obrażeń krytycznych: mnożnik `M`. Nie mnożyć całego mnożnika kryta postaci wraz z jego bazowym 1x.
- [x] Celność i unik: `1 + 0.3 * (M - 1)` — użytkownik wyraźnie zaakceptował łagodniejsze skalowanie obu.
- [x] Maksymalny surowiec i regeneracja: ograniczone skalowanie; propozycja użyta w symulacjach to `1 + 0.6 * (M - 1)`. Użytkownik zaakceptował potrzebę ograniczenia; dokładne 0.6 pozostaje współczynnikiem startowym do testów.
- [x] Reguły przypisane do statystyki, nie wyłącznie do klasy maga. Bazowa regeneracja postaci, efekty unikatowe i galduryty nie są automatycznie mnożone.
- [x] Zachować osobne skalowanie ataku/obron przez upgrade.

## 3. Już zmienione w kodzie podczas tej rozmowy

- [x] EquipmentUpgradeService: do kosztu próby przekazywana jest wartość uwzględniająca rzadkość i aktualny postęp. Zastąpiono następnie spójną wyceną jakości/rarity/postępu i wspólnym cennikiem z sekcji 1.
- [x] Domyślny koszt wyjmowania galdurytu: 70% jego wartości.
- [x] Nieudane wyjęcie niszczy galduryt i od razu usuwa go ze slotu; efekty znikają, miejsce się zwalnia. Zaktualizowano UI i testy.
- [x] Obsługa wcześniejszych zapisów ze zniszczonym galdurytem w slocie: płatna próba usuwa go bez zwracania do ekwipunku. Pozostał historyczny mnożnik kosztu usunięcia 1.5.
- [ ] Zweryfikować ustawienie bieżącego `crafting.galduryte.extraction_cost_percent = 0.7` w GameDefines.asset. Zmiana wartości domyślnej nie zastępuje zapisanego override. Nie potwierdzono wykonania kroku Editor przez użytkownika.

Weryfikacja tamtej implementacji: 34/34 wybrane testy EditMode zaliczone. Szczegóły ręczne: UNITY_HANDOFF.md, sekcja „Koszt ulepszania i wyjmowanie galdurytów — 2026-09-21”. Nie oznacza to testów nowych mnożników lub cenników opisanych tutaj.

## 4. Otwarte kwestie — nie traktować jako zatwierdzonej implementacji

- ZATWIERDZONE w kolejnym kroku rozmowy: po utracie slotu wskutek porażki galduryt pozostaje osadzony, ale jego efekty są wyłączone do odzyskania slotu. WDROŻONE razem z utratą postępu: aktywne pierwsze N slotów w zapisanej kolejności osadzenia, pozostałe nieaktywne; wyjmowanie nadal dostępne.
- Użytkownik polecił implementację pakietu wraz z ostatnim testowanym cennikiem F. Współczynniki poniżej wdrożone jako baza do dalszego balansu.
- Ostatni testowy cennik dla B=100, Common i szansy 70%: skumulowane ceny 21/77/206/443/826 zł przy 20/40/60/80/100%, bez porażek. Wdrożono ten wariant po poleceniu implementacji.
- Formuła tego wariantu: `F(p,r,c) = B * M(r) * (0.6*p + 6.4*p^3) * h(c)/h(0.6)`, `h(c)=(57-37*c)/(76-75*c)`, p i c w zakresie 0–1. Koszt liczony z różnicy zaokrąglonych wartości F. B to bazowa wartość części z mnożnikiem jakości; implementacja zaokrągla oba końce cennika przed odjęciem.
- Pierwsze symulacje nie obejmowały liniowej stawki. Kolejne przeliczenie ze stawką 25/40/55/70/85/100% opisano w BALANCE_LINEAR_REFORGE_COMPARISON.md. Nie cytować wcześniejszych kosztów Godly jako wyniku nowej stawki.
- Symulacje były modelem dla poziomu 15, normalnego T2, bez drzewek i galdurytów, z umowną bazową wartością pojedynczego przedmiotu 100 zł. Nie były testem runtime ani dowodem optymalnej strategii.
- Przed zamknięciem balansu porównać strategie z rzeczywiście finansowanymi galdurytami, sprawdzić HP pancerzy, kryty zwiadowcy i gospodarkę many.

## 5. Wcześniejsze ustalenia rozmowy — kolejka weryfikacji

Poniższe punkty zachowują wcześniejsze decyzje, ale ich aktualnego stanu implementacji i arkuszy nie audytowano przy tworzeniu rejestru. Nie oznaczać automatycznie jako brakujące ani wykonane.

- Penetracja: zastąpić redukcję obrony `(1-pen)` modelem odwrotnym `1/(1+pen)` dla sumowanej penetracji. W późniejszej analizie rozważano współczynnik strojenia; przed implementacją odzyskać ostateczny współczynnik ze źródeł, nie dopisywać go jako zatwierdzonego na podstawie samego skrótu rozmowy.
- Regeneracja surowca z mikstur: procent w opisie to łączny odzysk przez 10 tur, nie odzysk w każdej turze. Kondensacja zachowuje sumę i oddaje ją szybciej. „Siła (M)” dotyczy efektów T1, samo „Siła” — T2 poza bieżącym passem.
- Bomby: moc alchemiczna niezależna od broni. Cel balansu około 40% obrażeń referencyjnego ataku postaci na tym poziomie, około 60% przy dwóch składnikach obrażeń. Konkretny wzór mocy nadal wymaga jawnej kalibracji.
- Uzdrowienie: 120% siły leczenia za 30 MP; siła leczenia = średni atak. Paladyn nie leczy poza encounterami. Wyprawy mają otrzymać odpoczynek/interactables regenerujące zdrowie; przeciwdziałanie stallowaniu walk do ustalenia.
- XP: przyjęto rosnący koszt poziomu i relatywnie malejący udział pojedynczego przeciwnika/misji. Odtworzyć dokładny zaakceptowany wzór przed wdrożeniem; nie zastępować go zasadą „jedna wyprawa = jeden poziom”.
- Ekonomia wypraw: około jeden set dla towarzysza za wyprawę; roboczy budżet netto 650–700 T1 i 2800–3100 T2. Skrzynki: 2–3 na 6 encounterów, każda złoto za około 2–3 encountery i składniki za około 2–3 lokalne lootbagi. Szanse broni/pancerzy/galdurytów wymagają spójnej kalibracji.
- Lootbagi: niezależne kategorie, jedno entry z każdej trafionej kategorii, równy wybór entry w zestawie, zachowanie ilości entry; ten sam materiał może wypaść z kilku kategorii. Alkohol i proch po 40% w odpowiedniej kategorii; u bossa bursztyn i obsydian po 15%, obsydian 1–2 sztuki. Galduryty T1 od poziomu 11.
- Niepospolite materiały (w tym ektoplazma i pazur ghula) dopiero od poziomu 21, poza content passem do około 20. Przedmioty z innych lochów mogą pojawiać się u NPC, bez gwarancji.
- Użytkownik zmienił już część wartości w arkuszach (m.in. tunika, biżuteria, drzewka). Zawsze czytać aktualny arkusz; nie przywracać starszych liczb z rozmowy.

## 6. Istotne inwarianty drzewek do zachowania

Stan authoringu/implementacji do sprawdzenia z aktualnym content_pass_1.xlsx; poniżej ograniczenia doprecyzowane słownie, które łatwo zgubić w tooltipach:

- Pole Osądu: jedno na mapie; nowe zastępuje istniejące.
- Agresywny Ogień: osobny rzut 50% na każdego przeciwnika w zasięgu; kopia ma 50% siły rodzica, dziedziczy numer generacji; do trzeciego pokolenia, bez czwartej generacji. Nie mylić limitu generacji z globalnym limitem trzech instancji.
- Żywienie Ogniem: 20%; uwzględnia podpalenia na polu bitwy.
- Amok i Trauma nie kumulują się; brak opisanych stacków oznacza brak stackowania.
- Pragnienie Zemsty: 1/2/3 wściekłości za każde 5% rzeczywiście utraconego zdrowia; overkill nie generuje surowca.
- Wściekłe Uderzenia: koszt ofensywnych umiejętności we wściekłości obniżony o 10/20/30%.
- Odkrycie Słabości: względne +40% szansy kryta, nie +40 punktów procentowych.
- Rozkaz Przegrupowania: 70% PA, sprowadza AV celu do zera; zamierzone przyspieszenie kolejnej tury sojusznika.
- Podcięcie Gardła przeciw bossom: +100% obrażeń zamiast egzekucji.
- Zadręczenie nie kumuluje się rekursywnie; Uzdrowienie usuwa zniżkę Uzdrowiciela z leczonego celu.
- Hard CC ma tymczasowo zwiększać odporność na kolejne hard CC; liczby i szczegóły do ustalenia.

## Aktualizowanie rejestru

Po realizacji punktu dopisać pliki, testy i ewentualny krok Editor; dopiero wtedy oznaczyć wykonanie. Zmiana decyzji ma zastępować poprzedni wariant, a nie tworzyć drugą sprzeczną regułę. Arkusze są źródłem bieżących danych contentu, ten dokument — słownych decyzji i ich statusu.

## Status implementacji pakietu wyposażenia

Kod wdrożony 2026-09-21. Wspólny model w EquipmentItemInstance, cenniki w EquipmentUpgradeService, przeliczenie generowania i load, filtrowanie nieaktywnych galdurytów w PassiveEffectReconciliationSystem oraz oznaczenia UI. Wycena jakości Poor/Normal/Excellent/Masterpiece = 0.5/1/2/4; dotyczy też wartości używanej w handlu. Brak ręcznych zmian serialized assets. Szczegóły smoke i wyniki testów w UNITY_HANDOFF.md. Wcześniejsze ustalenia mikstur, bomb, penetracji i drzewek nie są objęte tą implementacją.

## 8. Penetracja i consumables — wdrożenie 2026-09-21

- Penetracja: obrona / (1 + suma nieujemnej penetracji), współczynnik 1 zgodny z jawnym wzorem użytkownika. Bez limitu 100%, także dla bonusu umiejętności.
- RegenHealth/RegenResource: Value to łączna regeneracja dzielona przez końcowy czas. Domyślnie 10 tur, jawny DurationTurns ma pierwszeństwo. Kondensacja i wydłużenie zachowują sumę. Maksimum HP/surowca nadal odczytywane co turę. Reszty zaokrągleń przechodzą między turami i są zapisywane. Stare aktywne efekty zachowują zapisany procent na turę; nowe użycia stosują nową interpretację sumy.
- Bomby: użytkownik zatwierdził wspólną krzywą poziomu, niezależną od broni i klasy. Bezpośrednie obrażenia korzystają z AlchemicalPower (enum 4, istniejące numery zachowane), również w podglądzie i ocenie AI. Maksymalne obrażenia nie przełączają bomby na statystyki broni.
- Referencja A(L): interpolacja liniowa punktów (1,28), (5,51.125), (15,120.34375), (25,248.625), (35,440.5), (45,709.5625), (50,750.34375), poziom ograniczony do 1–50. Punkty 5–50: średni atak czterech klas z normalnym wyposażeniem z design/Wyposażenie.xlsx, v6. Punkt 1: średnia baza klas.
- Kalibracja: q = siła * 0.2/0.15; raw = ceil(A(L) * (q + sqrt(q*q + 4*q))/2). Odwrócenie wzoru obrony przy obronie referencyjnej A(L). T1 siła 0.15 daje około 40% zwykłego trafienia po obronie; dwa identyczne składniki 2*0.15*0.75 = 0.225 dają około 60%. Rzeczywista obrona zmienia proporcje. Obrażenia nieuchronne pomijają obronę. Pozostałe reguły trafienia, krytów, penetracji i DoT zachowane.
- Bez ręcznych zmian serialized assets. Instancje consumables korzystają z nowego modelu przy budowie akcji.
- Weryfikacja końcowa: 48/48 PASS w penetration-consumables-verified.xml; pełny wcześniejszy przebieg potwierdził 3 znane błędy edytora lochów. Smoke runtime pozostaje w UNITY_HANDOFF.md.

## 9. XP — zatwierdzone i wdrożone w kodzie 2026-09-21

- Baza przeciwnika: 6 * (247 - 2L) * (L^1.5 + 1) / 49, L=1..50.
- Kara: różnica = clamp(poziom gracza - poziom przeciwnika, 0, 6); XP = baza * (1 - 0.15 * różnica). Tylko za słabszych przeciwników, maksymalnie 90%. Zaokrąglenie po karze, osobno na przeciwnika i postać. Bonus pasywny XP działa następnie na sumę nagrody walki.
- Standardowa misja: round(20 * niezaokrąglona baza przeciwnika jej poziomu), bez kary poziomowej. W QuestRewardDefinition nagroda Experience ma Standard Experience Level: 1–50 uruchamia wzór, 0 zachowuje stałe Amount. Wpis dodawać raz za ukończenie misji, nie na każdym etapie, chyba że zamierzona jest oddzielna nagroda etapu.
- Awans: round(1200 * L^1.5 + 1200); na poziomie 50 nadal brak kolejnego awansu. Zmienione domyślne coefficient i constant oraz fallbacki na 1200, exponent pozostaje 1.5. UWAGA: istniejący GameDefines.asset ma coefficient=12 i constant=12. Wymaga ustawienia obu CurrentFloatValue na 1200 w Editorze; kod nie ignoruje jawnych override.
- Charakterystyka formuły: na L1 przeciwnik = 2.5% progu, standardowa misja = 50%; na L50 odpowiednio 1.5% i 30% teoretycznego progu. Dokładny zatwierdzony wzór zastępuje wcześniejsze przybliżenia procentów w rozmowie.
- Poziom nagrody zachowany w CombatEnemyReward, kopiowaniu seamless combat i DungeonEnemyRewardSaveData. Stare przygotowane nagrody z ExperienceLevel=0 zachowują stałe XP. Nowe nagrody używają poziomu lochu. Zachowano poziomy postaci, posiadane XP, skillpointy i odebrane nagrody misji; brak automatycznego przeliczenia starego postępu.
- Pliki: CharacterProgressionSystem, GameDefineDefaults, CombatRewardSystem/Result, DungeonCombatMembershipSystem, save snapshot/restore i DungeonSaveData, QuestRewardDefinition/System/Drawer.
- Ręczna konfiguracja i smoke: UNITY_HANDOFF.md. Bez zmian serialized Unity state, bez commit/push.
- Końcowa weryfikacja XP: 69/69 PASS w xp-progression-final.xml; self-review i diff check PASS. Konfiguracja istniejącego GameDefines oraz misji i smoke pozostają ręczne.

## 10. Audyt pozostałych ustaleń

Wynik sprawdzenia 2026-09-21: BALANCE_REMAINING_AUDIT.md. Potwierdzono zapisany koszt wyjmowania galdurytu 0.7; zapisane progi XP nadal mają 12/12, mimo zgłoszonego ustawienia. Wykryto niewdrożony kierunek zaokrąglania PA i brak zakazu mieszania tierów części. Tolerancja statusów istnieje per status, ale nie jest potwierdzonym wspólnym systemem hard CC. Pozostałe reguły node’ów rozdzielono na zapisane w arkuszu, niejednoznaczne opisy i niezweryfikowane pokrycie runtime. Audyt nie wprowadza nowych zmian gameplayu.

## 11. Dokładność PA i wypełnienie ikon — 2026-09-21

Zatwierdzone przez użytkownika: zachować proporcje PA, zwiększyć precyzję do setnych; ułamki przedstawiać wypełnieniem diamencików.

- BattleUnit.CurrentActionPoints i MaxActionPoints oraz koszty wykonania/przemieszczenia mają jednostkę 0.01 PA (ActionPointUnits.PerPoint=100). 6 PA to 600. ActionPointsPerSpeed nadal 0.25, bez zmiany Game Defines.
- Maksimum ze szybkości i procentowy koszt są zaokrąglane do najbliższej setnej PA. Dodatni koszt procentowy ma minimum 0.01 PA; świadomie zerowe skille nadal kosztują 0. To zastępuje wcześniejszą propozycję ceil/floor na całych PA.
- Ruch 1/2/3 PA, narzuty trudnego terenu/ścian i płaskie modyfikatory zachowują wartości w PA. Impet liczy realnie wydane PA i przenosi ułamkowy przyrost. Kontekst pasywek nadal przekazuje wartości ruchu w PA, nie w setnych.
- Bonus ActionPointModifier w runtime/save pozostaje wyrażony w PA, dopuszcza teraz ułamek (float); starsze wartości całkowite zachowują znaczenie. Stan BattleUnit nie jest zapisywany w środku walki.
- UI: liczba ikon = ceil(maximum/100), z uwzględnieniem nadmiarowego bonusu bieżącej tury. Dostępna część wypełniona, planowany koszt zaznaczony kolorem podglądu, reszta pusta/przygaszona. Nowy ActionPointPipFill koloruje istniejący mesh ikony, również Image bez sprite’a. Wymaga dodania komponentu do istniejącego szablonu w prefabie; bez tworzenia statycznego UI w runtime.
- Tooltipy, liczby w HUD/debugu i feedback wydatku PA pokazują jednostki gracza. Poza walką tooltip skilla pokazuje procentowy koszt, nie udawany koszt w PA.
- W tej karcie nie zmieniano tolerancji hard CC ani mieszania tierów.

- Weryfikacja PA: 175/175 PASS, ap-verified.xml. Pozostaje dodanie ActionPointPipFill do wspólnego szablonu prefab i smoke opisany w UNITY_HANDOFF.md.

- Aktualizacja podłączenia PA: na polecenie użytkownika dodano ActionPointPipFill do PF_UIW_CombatVitals/ActionPointPipRoot(Clone)/ActionPointPipTemplate. Wszystkie trzy prefaby dziedziczą komponent; ap-prefab.xml 17/17 PASS. Ręczne dodanie komponentu nie jest już wymagane.


## Wspólna tolerancja Suppression — wdrożone 2026-09-21

Stun, Freeze, Sleep, Paralysis, Immobilize, Chill i Taunt korzystają z jednej tolerancji, zgodnie ze wspólną odpornością Suppression. Tolerancja innych statusów pozostaje oddzielna. Zachowano dotychczasowe parametry i lifecycle: domyślnie +0.25 za skuteczną aplikację, dwa końce tury celu, kolejne nałożenie dodaje stack i odświeża czas. Wartość za stack jest teraz odczytywana z istniejącego GameDefine combat.status.tolerance_per_stack. Odrzucona aplikacja nie dodaje stacka. Stan pozostaje lokalny dla BattleUnit, bez zmian save i serialized assets.

Weryfikacja: cc-shared.xml 86/86 PASS (statusy, pasywy, obrażenia, AI), w tym 10 nowych przypadków wspólnej tolerancji, izolacji DoT, odświeżania, wygasania i obu ścieżek aplikacji. Self-review zakończony.

Otwarty problem balansu: CalculateEffectChance dla bazowej szansy 100% daje nadal 100% przy odporności do 50%. Ponadto tolerancja wygasa po 2 turach, także podczas 3-turowego CC. Wspólny licznik naprawia izolację statusów, ale nie gwarantuje ochrony przed perma-CC. Propozycja do zatwierdzenia: oddzielny mnożnik tolerancji (1 - tolerancja) po bazowym wzorze oraz wygasanie ochrony dopiero po zakończeniu blokady działania. Nie zmieniano tych zasad automatycznie.

## Czas i stackowanie tolerancji CC — decyzja i wdrożenie 2026-09-21

Nadpisuje wcześniejszą zasadę odświeżania do 2 tur oraz propozycję czekania na koniec CC: pierwsze skuteczne CC ustawia 1 stack i czas 2 + długość CC. Każde kolejne skuteczne CC podczas tolerancji dodaje 1 stack i swoją długość do pozostałego czasu; nie dodaje ponownie bazowych 2 tur. Czas maleje na końcu tury celu. Po wygaśnięciu następne CC rozpoczyna nowy licznik. Przykład: stun 3 = 5 tur; po jednej turze zostają 4; kolejne CC 2 = 6 tur i 2 stacki. Nieudane nałożenie niczego nie przedłuża. Dotyczy wspólnej grupy Suppression; DoT zachowują osobną tolerancję i dotychczasowy czas. Siła stacka nadal z GameDefines, domyślnie 25 pp. Nie zmieniono wzoru szansy aplikacji; wcześniejsza propozycja osobnego mnożnika pozostaje niezatwierdzona.

Testy cc-duration.xml: 88/88 PASS. Obejmują obie ścieżki aplikacji, sumowanie pozostałego czasu, stacki, wygaśnięcie/reset i odrzucenie aplikacji. Self-review oraz diff-check zakończone. Bez zmian serialized assets i save.

## Zakaz mieszania tierów części — wdrożone 2026-09-21

Nowe wyposażenie wymaga jednakowego LevelBracket wszystkich trzech części. Kowal odrzuca mieszany zestaw przed zużyciem materiałów i pokazuje powód w istniejącym UI. Losowy generator wybiera spośród kompletnych tierów, a następnie części tego tieru; brak kompletnego tieru oznacza brak wyniku. Dotyczy również generowania unikatów tym samym mechanizmem. Nie zmieniano odtwarzania zapisanych przedmiotów ani serialized contentu.

Weryfikacja equipment-tier.xml: 45/45 PASS (generator, crafting/upgrade/consumables acceptance, save snapshot; filtr obejmował również nazwę LootTableSystemTests). Testy potwierdzają odrzucenie mieszanego zestawu, brak zużycia materiałów, poprawny wybór kompletnego tieru oraz zapis/odczyt wyposażenia. Self-review i diff-check zakończone. Smoke: u kowala wybrać części z różnych tierów i sprawdzić blokadę, następnie jednolity zestaw.

## Szanse toreb zwykłych przeciwników — zatwierdzone 2026-09-22

Użytkownik przyjął jako bazę do dalszych obliczeń niezależne szanse na każdego zwykłego przeciwnika: torba zasobów 50%, broni 2%, pancerza 2%, galdurytu 1% od poziomu 11 (poniżej 11: 0%). Zatwierdzono wartości, nie wdrożono jeszcze konfiguracji loot tables. Nie dotyczy bossów. Dodatkowy model sprzętu ze skrzynek 4%/4%/2% pozostaje propozycją. Następny krok: porównanie podaży materiałów z kosztami aktualnych receptur. Analiza: BALANCE_LOOT_PACING.md.

## Analiza materiałów — 2026-09-22

Wynik w BALANCE_MATERIAL_SUPPLY.md. Przy 50% toreb ruda ogranicza sety wojownika/paladyna; inne surowce nie wymagają ogólnego buffa. Propozycja dostępności 15–20 lokalnej rudy u kowala wymaga decyzji. Nie zmieniono receptur/dropu ani zasady dostępności materiałów innych lochów.

## Cała drużyna i jakość — analiza 2026-09-22

BALANCE_PARTY_SUPPLY.md porównuje 4 normalne lub 4 doskonałe komplety. Stock NPC jest głównym regulatorem dostępności przy obecnej cenie rudy i dochodach. Wariant 20 rudy/piętro/odnowienie to wyłącznie test wrażliwości, nie przyjęta konfiguracja. Nie zmieniono dropu, cen ani ofert.

## Dostawy i rotacja oferty — kod wdrożony 2026-09-22

TraderStockService, konfigurowalne LocalStock/RotatingStock w TraderRoleData, odblokowanie wg najwyższego poziomu aktywnej drużyny. Jedna dostawa na instancję wyprawy po co najmniej jednym ukończonym piętrze, podłączona do obu powrotów w GameBootstrap. Local top-up bez kumulacji; rotacja połowy całej oferty (nie każdej grupy oddzielnie), zaokrąglenie w górę, wymiana w ramach kategorii gdy istnieje alternatywa. Wyprzedane warianty zachowane w osobnej liście. Buyback nietknięty. Przyrostów budżetu NPC nie zmieniono.

Save: RotatingStockItemIds w NPC, TraderStockRefreshed w DungeonSaveData; snapshot/restore obsługują nowe pola. Stare zapisy bez markerów otrzymują domyślne puste/false; zwykłe wczytanie nie uzupełnia stocku. Nowa gra inicjalizuje skonfigurowaną ofertę.

Testy trader-stock-final.xml: 78/78 PASS, w tym JSON roundtrip pustego stocku z zachowaniem wyprzedanej pozycji i markera wyprawy, blokada wielokrotnych powrotów, progi poziomu, niepełna pula, brak kumulacji oraz zachowanie odkupu. Self-review/diff check zakończone. Brak commit/push.

UWAGA: serialized TraderRoleData nadal bez wpisów nowych list. Definicje alternatywnych surowców i T2 nie istnieją w znalezionym katalogu Items. Produkcyjna aktywacja wymaga konfiguracji/uzupełnienia danych; kroki i konkretne limity w UNITY_HANDOFF.md. Nie oznaczać całego wdrożenia oferty jako zakończonego przed authoringiem.

## Aktywacja oferty kowala — wykonane 2026-09-22

Po jawnej zgodzie użytkownika skonfigurowano LocalStock i RotatingStock w istniejącym TraderRoleData, dodano 28 definicji surowców z cenami/rarity XLSX i wpisano do GC_MainContentCatalog. Konkretne wartości z przedziałów: inne rudy 10, skóry 4/2, drewno 3, włókna 8, dodatki 5. Assety przeszły import i 81/81 testów (trader-stock-assets.xml). Nie wymaga dalszego podłączania. Smoke pozostaje; pełny content receptur/wyposażenia T2 jest osobnym zakresem.

## Następny punkt VS-18: regeneracja w wyprawie — propozycja 2026-09-22

Sprawdzono katalog typów DungeonObjectType: brak dedykowanego odpoczynku/kapliczki. Nie rozpoczęto implementacji. Do zatwierdzenia: jeden gwarantowany jednorazowy punkt regeneracji na piętro referencyjne (~6 walk), osiągalny mniej więcej po połowie piętra; odnawia 40% maksymalnego HP i 40% maksymalnej many żywym aktywnym członkom drużyny obecnym przy punkcie, z ograniczeniem do maksimum. Nie daje wściekłości ani impetu, nie wskrzesza. Użycie poza walką, drużyna musi być zebrana; zużycie zapisane w instancji lochu, bez resetu po powrocie do miasta/wczytaniu. Brak opłaty, zapasów obozowych i losowej dostępności w tej propozycji. Nie jest to zatwierdzony ani przetestowany balans. Anti-stall pozostaje oddzielnym punktem: nie zakładać, że odpoczynek rozwiązuje leczenie na ostatnim wrogu.

## Obóz drużyny — zatwierdzone i przetestowane analitycznie 2026-09-22

Zastępuje wcześniejszy stały punkt na mapie: jedno rozbicie obozu na konkretne piętro, dowolne miejsce poza walką, zebrana drużyna. Żywi odzyskują 40% maxHP/maxMP do maksimum, bez wskrzeszenia/wściekłości/impetu. Niewykorzystane użycie zostaje na swoim piętrze, nie kumuluje się na następnym; powroty/load nie odświeżają użycia. BALANCE_CAMP.md zawiera deterministyczny bilans i syntetyczną analizę wrażliwości; rekomendacja zachować 40%. Nie jest to runtime test ani implementacja obozu. Anti-stall pozostaje otwarte.


## Aktualizacja obozów — 2026-09-22 (zastępuje limit raz na piętro)

- Zatwierdzona zmiana użytkownika: przy rozpoczęciu nowej wyprawy przydział zużywalnych zestawów obozowych równy faktycznej liczbie wygenerowanych pięter. Przydział trafia do ekwipunku aktywnego przywódcy; zestawy można przekazywać w drużynie.
- Zapasy są wspólne na wyprawę. Można zużyć kilka na jednym piętrze. Nie ma już osobnego limitu piętra ani wielorazowego przedmiotu startowego.
- Użycie przez PPM na zestawie w ekwipunku albo PPM na widocznej podłodze → Rozbij obóz. Obie drogi używają jednej walidacji i wykonania.
- Poza walką, przy zebranych żywych członkach aktywnej drużyny, natychmiast +40% maksymalnego HP i +40% maksymalnej many, do maksimum. Bez wskrzeszania, odnawiania wściekłości ani impetu. Nie tworzy obiektu obozu na mapie.
- Każde wyjście z lochu, również zwój, usuwa niewykorzystane zestawy. Wznowienie tej samej wyprawy nie daje nowych; przydział zapisany jako CampSuppliesIssued w DungeonSaveData. Ilość pozostałych przedmiotów używa istniejącego zapisu ekwipunku.
- Eksploracja: co 6 sekund naturalna regeneracja many oraz utrata wściekłości według RageDecayPerMissedTurn (domyślnie 10). Impet i jego ułamkowa reszta zerowane poza walką. Zegar działa bez aktywnych statusów; postacie w walce nie są dodatkowo tickowane. Nie ma pasywnego leczenia HP z tej reguły.
- Kod wdrożony. Authoring zestawu: komenda Unity Godmist → Content → Configure Camping Kit tworzy ID_CampingKit i wpis katalogu; do uruchomienia w głównym projekcie przez użytkownika. Smoke według UNITY_HANDOFF.md.

- Weryfikacja końcowa pakietu: kompilacja i 73/73 EditMode PASS, `camp-resources-acceptance.xml`. Self-review zakończony. Authoring zestawu w głównym projekcie i smoke Play Mode NOT RUN; bez commit/push.


## Anty-stall — ostrzeżenie o posiłkach, 2026-09-22

- Użytkownik: próg 3 tur z rzędu bez otrzymania obrażeń przez przeciwników, alert jedną turę wcześniej. Warunek działa wyłącznie przy jednym żywym przeciwniku; tura oznacza ruch tego przeciwnika.
- Wdrożenie bieżącego etapu: na końcu drugiej kolejnej tury tego przeciwnika bez obrażeń pojawia się jeden komunikat w battlelogu. Nie dodano spawnu posiłków ani komunikatu udającego ich faktyczne przybycie.
- Okno obrażeń obejmuje czas między końcami jego tur, również ruchy bohaterów oraz efekty na początku/końcu tury. Dodatnie obrażenia HP lub tarczy, także DoT, resetują serię; pudło, 0 obrażeń i obrażenia drużyny nie resetują. Tury bohaterów ani obiektów nie przyspieszają odliczania.
- Drugi żywy przeciwnik, zmiana ostatniego przeciwnika lub zakończenie walki resetują stan. Licznik należy do konkretnej CombatSession; nie jest globalny. Dalsze nieaktywne tury nie spamują logu. Po obrażeniach nowa seria może ponownie ostrzec.
- Kompilacja i 117/117 testów EditMode PASS (`stall-warning.xml`): scenariusze alarmu oraz regresja damage, statusów, AV i seamless combat. UI używa istniejącego battlelogu; brak nowych assetów lub wiring. Smoke użytkownika NOT RUN; bez commit/push.

## Anty-stall — rzeczywiste posiłki, zatwierdzone 2026-09-22

- Zastępuje wcześniejszy etap samego ostrzeżenia: po trzeciej kolejnej turze ostatniego przeciwnika bez obrażeń przybywa fala.
- Losowy zwykły encounter z definicji lochu, bez encountera bossa; losowe 50% jego jednostek, liczba zaokrąglona w górę.
- Poziom wyprawy, brak XP, złota i lootu. Dołączenie do normalnej kolejki AV.
- Wolne pola przy wejściach do pokoju; brak miejsca na pełną falę odracza próbę do następnej tury przeciwnika.
- Kolejne fale możliwe, gdy znów zostanie jeden przeciwnik i spełni warunek. Obrażenia HP/tarczy, w tym DoT, resetują serię.
- Wybrany skład posiłków i brak nagród zachowywane przy zapisie/odczycie lochu.
- Kod wdrożony; 119/119 EditMode PASS (`reinforcements-fixed.xml`). Ręczny smoke Play Mode pozostaje do wykonania; VS-18 nadal In Progress.
