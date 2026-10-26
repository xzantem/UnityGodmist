# Aktualna topologia: graf przed geometrią — 2026-10-23

Cała wyprawa (wszystkie piętra) jest planowana przed rozmieszczaniem pokoi. Pętle mają dwa pełne ramiona między wspólnym rozwidleniem i połączeniem, preferowane różnej długości. Każde ramię ma 2–4 pokoje wewnętrzne w krótkiej, 3–5 w normalnej i 4–7 w długiej wyprawie; cykle wraz z F/J mieszczą się w 6–10 / 8–12 / 10–16 pokojach. Do zamknięcia ortogonalnych tras potrzebna jest zgodna parzystość długości. Przykładowe nierówne pary: 2+4 / 3+5 / 4+6 lub 5+7. Przy ciasnym budżecie mogą zostać równe ramiona; dostępna bazowa pula ogranicza wybór podczas planowania, nigdy przy retry geometrii. Geometria ma nieregularne zakręty/wcięcia, a nie prostokątny obwód; tylko minimalny cykl 6 pokoi jest na tej siatce z konieczności prostokątny. Długi Las ma dwie pętle, pozostałe aktualne warianty jedną na piętro. Bramy pozostają wspólnymi mostami pomiędzy sekcjami, wymagane cele przed własną bramą. Pokoje pętli mieszczą się w istniejącej bazowej puli; budżety w tabeli poniżej i liczby walk bez zmian. Dowody: ../LocalHandoffs/irregular-loops-20261023/README.md.

## Zachowane budżety i odnogi — 2026-10-23

Do bazowej puli po skróceniu dodano pokoje dojściowe odnog. Liczba celów bocznych na CAŁĄ wyprawę zależy od długości: krótka 1–3, normalna 2–4, długa 3–5, jednakowo dla Lasu i Katakumb. W Katakumbach są dzielone między piętra, z co najmniej jedną odnogą progresji na piętro. Krótka: 2–3 pokoje dojścia, normalna: 2–5, długa: 3–8. Pokój celu jest osobny i już mieści się w bazowej puli. Każde piętro ma przynajmniej jedną dodatkową trasę między odcinkami głównej drogi, bez omijania bram; odnogi nie dostają skrótów kasujących wymagane dojście.

| Długość | Bazowe pokoje łącznie | Las: pokoje z dojściami | Katakumby: pokoje z dojściami | Las: walki | Katakumby: walki |
|---|---:|---:|---:|---:|---:|
| Krótka | 14–17 | 16–26 | 16–26 | 5–6 | 5–6 |
| Normalna | 28–34 | 32–54 | 32–54 | 9–11 | 10–12 |
| Długa | 42–51 | 51–91 | 51–91 | 14–17 | 15–18 |

To granice konfiguracji, nie gwarancja równomiernego rozkładu wszystkich wartości w próbce seedów. Gęstość 30–40% liczona jest od bazowej puli pokojów, nie od sumy powiększonej o dojścia. Walki mogą wypadać także w odnogach; ich łączny budżet, mnożniki EXP, obozy i nagrody questów nie są zwiększane. Dodatkowe pokoje korzystają z istniejącej zawartości/szablonów, bez nowego mnożnika łupu. Pełne stare snapshoty zachowują dawną geometrię; nowe snapshoty v4 zapisują rolę pokoju dojścia. Porównania rzeczywistych pól i testy: ../LocalHandoffs/expedition-branches-20261023/README.md.

## Poprzedni etap: sama redukcja pokoi — 2026-10-23

Wdrożona decyzja: połowa wcześniejszego zakresu pokoi, ×2 EXP zwykłych wrogów, EXP bossów bez zmian. Mniej dropu i mniejsze zużycie zasobów są zamierzone; bez kompensowania lootów/złota i bez zmiany nagród questów oraz obozów.

| Długość | Pokoje łącznie | Las: walki na 1 piętrze | Katakumby: piętra | Katakumby: walki łącznie |
|---|---:|---:|---:|---:|
| Krótka | 14–17 | 5–6 | 1 | 5–6 |
| Normalna | 28–34 | 9–11 | 2 | 10–12 |
| Długa | 42–51 | 14–17 | 3 | 15–18 |

Walka zajmuje nadal 30–40% pokojów danego piętra. Boss zużywa jeden slot w całej wyprawie; jego zwykła obstawa dostaje nowy mnożnik EXP. Różnica zakresów między motywami wynika z zaokrąglania osobno na piętro katakumb. Skład i siła przeciwników bez zmian. CharacterProgressionSystem zachowuje bazową krzywą i EXP questów; mnożnik jest wyłącznie w nagrodach bojowych. Legacy fixed-XP i schemat zapisu zachowane. Istniejące snapshoty map nie są regenerowane.

179 odrębnych testów PASS, 36 układów wygenerowanych z aktualnych profili. Dowody: ../LocalHandoffs/expedition-pacing-20261023/README.md. Poprzednie analizy poniżej są historyczne: ich czasy, statystyki NPC i liczba wypraw do L20 nie są ponownie potwierdzone dla bieżącego balansu.

---

# Długości wypraw i łagodniejszy start — 2026-10-16

Decyzja użytkownika: krótkie wyprawy są bazą progresji; normalne i długie mają proporcjonalnie więcej zawartości. Katakumby mają 1/2/3 piętra, Las zawsze jedno. Wydawane zestawy obozowe: 0/1/2. Quest ukończenia: ×1/×2/×3; niezależny quest zabijania bez mnożnika.

| Długość | Katakumby: piętra | Pomieszczenia łącznie | Walki łącznie | Zestawy | Quest ukończenia |
|---|---:|---:|---:|---:|---:|
| Krótka | 1 | 28–34 | 9–11 | 0 | ×1 |
| Normalna | 2 | 56–68 | 18–22 | 1 | ×2 |
| Długa | 3 | 84–102 | 27–33 | 2 | ×3 |

Las używa tych samych sum na jednym piętrze. Na każdym piętrze encounter zajmuje 30–40% faktycznie wygenerowanych pomieszczeń. Boss zużywa jeden slot walki na ostatnim piętrze. Rozmiary grup przeciwników pozostają charakterystyczne dla motywu, więc podobna liczba walk nie oznacza identycznego XP i trudności.

## Skąd te liczby

### Aktualizacja encounterów Lasu — 2026-10-16

Nowe wyprawy Lasu losują: 3 wilki; 2 rzezimieszków; 2 rzezimieszków + 2 strzelców; 3 łuczników; druid + 2 kłusowników; kłusownik + 2 łuczników; 2 drzewa. Rozkład 2/2/3/3/3/3/4 zachowuje łatwe i trudniejsze walki, średnia liczebność wzrosła z 2 do 2,86. Katakumby są punktem odniesienia. Statystyki/skille NPC, bohaterowie, wzory XP i mechaniki bossów bez zmian w tym passie.

560 pojedynczych walk: średnia presja obrażeń Las/Katakumby 1,121; średnie końcowe HP różni się maksymalnie o około 1,2 pp na poziom. W 48 krótkich seriach L1/L48, full clear/scouting po sześć seedów, obrażenia w walce + netto strata HP między walkami dają średni stosunek 1,0446. Krótkie full cleary L1: 5/6 ukończeń w obu lochach, L48: 6/6 w obu. To porównanie średnich w próbce, nie identycznego wyniku każdego seeda/builda. Rozrzut walk pozostaje zamierzony.

Nowe definicje `.balanced` nie zastępują zapisanych aktorów: stare aktywne snapshoty zachowują dawne grupy przez globalny katalog. Nowe grupy zobaczysz w nowej wyprawie. Owner: `EncounterBalanceSetup`, hook w authoringu Lasu, cztery sloty zwykłych wrogów. 200 unikalnych testów PASS, 72 serie/894 walki + 560 kontrolowanych, razem 1454 zaakceptowane walki bez limitów. Manual gameplay/FPS NOT RUN. Dowody i ograniczenia: `LocalHandoffs/encounter-balance-pass-20261016/HANDOFF.md`.

Nowy conditional model XP krótkiego full clearu z jednym standardowym questem: około **19 wypraw do L20 dla obu lochów**. Scouting: około 19,2 Katakumb i 20,6 Lasu. Ścieżki respektują graf/bramy, nie zakładają automatycznego pominięcia 25% walk ani omijania aktorów wewnątrz odwiedzonych pokojów. Historyczne liczby XP poniżej opisują poprzednią pulę Lasu.

### Wyliczenie długości i historyczne XP

Wcześniejszy zapis `BALANCE_VS18_SIMULATION.md`: 10,25 walk i 31,2 wrogów na wyprawę; z jednym standardowym questem około 18,8 wypraw od L1 do L20. Krótka długość zachowuje około 10 walk, zamiast wprowadzać nową bazę 6–8 walk. Przy 35% obsadzie 10/0,35 ≈ 29 pomieszczeń; zakres 28–34 pozwala uzyskać 9–11 walk i utrzymać 30–40% na konkretnym układzie.

Wzory XP nie zostały zmienione. Standardowy quest odpowiada XP za 20 wrogów poziomu misji. Przy przykładowych 31 wrogach krótka wyprawa z jednym questem daje brutto XP za 51 wrogów; normalna i długa odpowiednio około 102 i 153. To około 128% progu L1 i 77% teoretycznego progu L50 dla krótkiej wyprawy, przed karą za przewyższenie poziomu wrogów. L50 pozostaje limitem, więc drugi odsetek jest wyłącznie porównaniem wzoru. Nie jest to obietnica określonej liczby awansów: level-up w trakcie wyprawy zmniejsza kolejne nagrody za słabszych przeciwników.

Próbka generatora: 36 układów, sześć produkcyjnych definicji × trzy długości × dwa seedy. Zwykłe Katakumby średnio 10,5/20,5/30,5 walk; zwykły Las 11/19/28,5. Każdy układ spełnia zakresy oraz gęstość. Szczegółowe wyniki: `LocalHandoffs/dungeon-length-pass-20261016/generated-pacing.json`.

Historyczne obliczenie progresji sprzed passa encounterów, przy każdej wyprawie ukończonej i jednym standardowym queście ukończenia: Katakumby około **19/10/7** wypraw do L20 dla krótkiej/normalnej/długiej; Las około **22/13/10**. To warunkowy model XP, nie prognoza wygranych. Pula ma cztery układy krótkie i po jednym dłuższym na motyw; 1000 trajektorii przelosowuje tę małą pulę, więc nie daje szerokiej estymacji rozrzutu generatora. Wcześniejszy cel około 19 krótkich katakumb pozostaje zachowany. Różnica Lasu wynika z mniejszych grup przeciwników, bez zmiany ich składów. Dowód: `pacing-recalculated.json` w katalogu passa.

## Bazy i przyrosty przeciwników

Bazowe HP: 40% poprzedniej wartości; bazowy minimalny/maksymalny atak: 60%, zaokrąglane do całkowitych wartości. HP Widmowego Rycerza pozostaje 3. Zmniejszona baza zostaje dodana do pełnego późnego przyrostu jako `(stara baza − nowa baza)/199`. Dzięki temu wcześniejszy punkt na L50 pozostaje zachowany, a L48 zmienia się tylko o małą, nieodrobioną część obniżonej bazy. Bohaterowie, drzewka, zasoby, koszty oraz krzywa 199 jednostek nie są zmieniane.

Owner: `EnemyScalingBalanceSetup`; niezmienne stare bazy i przyrosty zapewniają idempotencję ponownej konfiguracji i zgodność obu faz bossów. Profile długości: `DungeonLengthSetup`, zapisane w sześciu produkcyjnych DungeonDefinition. Menu Editorów nie wymaga zmiany Inspectorów ani scen.

## Zapis i nagrody

Długość jest losowana osobno dla oferty, pokazywana na tablicy i zachowywana przy ponownym otwarciu oraz save/load. Wybór długości oczekującego bossa pozostaje aż do jego ukończenia, także po innych wyprawach. Aktywny loch zapisuje długość razem z istniejącym snapshotem układu. Stare aktywne lochy bez tej informacji zachowują geometrię i dawny przydział obozów; stare oferty otrzymują długość jednokrotnie.

XP, złoto i honor questu ukończenia mnożą się po rzeczywistym zaliczeniu odpowiedniej wyprawy. Flaga mnożnika jest zapisywana przed odbiorem, resetowana przy ponownym wystawieniu questu, nie zmienia bazowej oferty i nie pozwala odebrać nagrody dwukrotnie. Bonusowy przedmiot pozostaje jedną sztuką. XP/łup wrogów oraz obiekty pokojów korzystają ze zwykłych zasad, bez dodatkowego mnożnika za długość.

## Granice symulacji

**Założenie użytkownika po analizie wyników:** obecne serie są pełnym czyszczeniem wszystkich encounterów. Dobór trasy i scouting mogą w rzeczywistej grze pozwolić ominąć około 20–30% walk. To robocze oszacowanie, nie zmierzona własność wszystkich seedów. Dalsza kalibracja powinna rozróżniać pełne czyszczenie oraz trasę obejmującą około 70–80% encounterów, z poszanowaniem połączeń mapy i obowiązkowego bossa. Nie wystarczy losowo usunąć dowolnych walk ani proporcjonalnie przeskalować strat HP: wybór trudnych składów, leczenie, zasoby i awans zmieniają wynik. Pełne czyszczenie może być wyraźnie bardziej wymagające; porażka startowej drużyny na długim full clearze sama nie dowodzi, że Katakumby są za mocne. Omijanie zmniejsza również XP i łup z przeciwników, więc wcześniejsze około 19 wypraw do L20 dotyczy pełnych clearów z questem.

Symulacje używają rzeczywistych kosztów, zasobów, AV, statusów, faz, XP, level-up i zestawów obozowych. Cztery instancje bohaterów przechodzą serię walk bez darmowego resetu HP/many. Polityka bohaterów leczy, używa tarczy, podnosi sojuszników i odzyskuje sztylety; nowo zdobyte punkty nie są inwestowane w trakcie serii. Na L1 używa startowego wyposażenia i niewydanych punktów; wyżej legalnych drzewek i wyposażenia poziomu postaci, bez ulepszeń/unikatów. Geometria walk jest stałą otwartą areną: test mierzy presję i wyczerpanie zasobów, nie pathfinding konkretnego pokoju ani FPS.

Pełne wyniki weryfikacji i końcowe liczby symulacji są zapisane w `LocalHandoffs/dungeon-length-pass-20261016/HANDOFF.md`.
