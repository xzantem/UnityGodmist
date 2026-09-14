# Wysokości lochu — fundament v1

## Zatwierdzone

- Wiele powierzchni w tym samym rzucie X/Y: balkon i podłoga pod nim mogą być dostępne jednocześnie.
- Wysokość jest dyskretnym poziomem. Nie utożsamiamy jej z FloorIndex.
- Najpierw fundament i schody; skoki, zeskoki, upadki, zrzucanie i premie wysokości mają osobne etapy projektu w Dungeon Exploration v3.
- Jedna scena, mapa i reprezentacja jednostek; rozwijamy obecnych właścicieli.

## Wdrożony etap A: tożsamość i snapshot

DungeonSurfacePosition=(Cell,Level) identyfikuje powierzchnię w obrębie DungeonFloorMap. Właścicielem pozostaje mapa; klucze właścicieli pokojów uwzględniają poziom. FloorCells i dotychczasowe API Vector2Int są projekcją poziomu0 dla istniejących odbiorców, nigdy sumą wszystkich poziomów. Nie można uznać balkonu za dostępną podłogę na poziomie0.

DungeonStairConnection przechowuje uporządkowane, dwukierunkowe końce na różnych poziomach. Dodanie wymaga istniejących powierzchni, a odwrotne dodanie tego samego połączenia jest duplikatem. Połączenia nie powstają od samego nakładania X/Y. Model zapisuje końce; nie zatwierdza jeszcze footprintu, długości, animacji ani kosztu schodów. GetStairDestinations jest zapytaniem o topologię, nie poleceniem przemieszczenia jednostki.

DungeonLayoutSnapshot v2 zapisuje poziomy pól i schody w deterministycznym porządku. Odczyt v1 zachowuje poziom0, v0 pozostaje na istniejącej ścieżce legacy. Nieznane wersje, duplikaty powierzchni, brakujące końce schodów i schody w układzie bez mapy odrzucają snapshot bez fallbacku do generatora. Utrwalony content nie jest ponownie losowany.

## Wdrożony etap B: geometria szablonów i połączeń

Zatwierdzona reguła: poziomy są wspólne w obrębie piętra. Zwykłe drzwi łączą sąsiednie powierzchnie na tym samym poziomie na całej szerokości przejścia. Różnica wysokości wymaga jawnych schodów. Pokój może mieć wyjścia na różnych poziomach. Wysokość ściany przesłaniającej LOS pozostaje osobnym zagadnieniem.

Szablon ma opcjonalną jawną listę Surfaces (LocalCell, Level) i Stairs. Pusta lista powierzchni zachowuje dawny footprint na poziomie0. Niepusta lista określa komplet powierzchni; ich rzut X/Y musi odpowiadać footprintowi, który nadal służy do układania pokojów. Walidacja odrzuca duplikaty, powierzchnie poza footprintem, brakujące końce schodów, schody na jednym poziomie oraz nieprawidłowe wyjścia. Schody opisują topologię; długość, koszt i animacja wymagają ustalenia w etapie C.

Kotwy strukturalne i AllowedBoundaryEdges mają Level (domyślnie0). Obrót zmienia X/Y oraz kierunek, zachowuje Level. Generator wybiera wspólny poziom dla wszystkich pól wyjścia i rezerwuje je osobno według poziomu. Przy kilku zgodnych poziomach wybiera pierwszy rosnąco; nie dodaje losowania do generacji starych szablonów. Builder sprawdza powierzchnie należące do właściwych pokojów. Stare ograniczenie rozstawiania pokojów według rzutu footprintu pozostaje; piętrowe nakładanie różnych instancji pokojów nie jest dodane w tym etapie.

DungeonSurfaceEdge łączy kardynalnie sąsiednie pola na jednym poziomie. DungeonFloorMap przechowuje ściany, drzwi i bramy według tego klucza; stare kolekcje są cache'owaną projekcją poziomu0. Ściana i brama na balkonie nie blokują podłogi pod nim. Istniejący FloorInstance synchronizuje bramy na poziomie połączenia.

Snapshot v2 rozszerzono o Level ścian i połączeń (etap A nie był jeszcze wydany). Zapis obejmuje wszystkie ściany, sortowane według poziomu i pozycji. Odczyt sprawdza powierzchnie na poziomie ściany i obu stronach przejścia, zachowuje odczyt v1 z domyślnym0 i ścieżkę v0. Nie uruchamia ponownie generatora/content RNG dla snapshotu.

Nie wdrożono jeszcze pozycji jednostek/obiektów, wykonywania ruchu po schodach, górnego trudnego terenu, FOV/LOS ani wizualnej geometrii. Authoring poziomów opisuje etap C1 poniżej. Nie dodawać takich szablonów do grywalnej puli przed integracją ruchu i prezentacji. Istniejące assety pozostają bez zmian.

## Etap C1: authoring powierzchni w Room Editorze

Zatwierdzony układ zachowuje trzy panele: poziomy/narzędzia po lewej, aktywny poziom i opcjonalny przygaszony podgląd pozostałych pośrodku, właściwości po prawej. Numer poziomu można wpisać bez tworzenia assetu lub pustej warstwy danych; zapisuje się dopiero namalowane powierzchnie. Edycja podłogi zachowuje komplet innych poziomów i aktualizuje rzut footprintu. Ostatniej powierzchni pokoju nie można usunąć.

Kotwy strukturalne, wyjścia i zaznaczanie są rozdzielone według poziomu. Operacje na wszystkich wyjściach dotyczą tylko aktywnego poziomu. Kotwy zawartości i drzwi jako obiekty nadal obsługują wyłącznie poziom0; wyjścia pokojów i ściany można autorować wyżej. Walidacja zawartości i trudnego terenu wymaga rzeczywistej powierzchni0, nie tylko obecności tego X/Y na balkonie.

Narzędzie Schody wskazuje pierwszy koniec na powierzchni, zachowuje go przy przełączeniu poziomu i przyjmuje drugi koniec. Weryfikuje istnienie powierzchni, różne poziomy i brak duplikatu. Lista schodów oraz panel właściwości pokazują oba końce, pozwalają przejść do ich poziomu lub usunąć połączenie. Nadal autorujemy końce istniejącego modelu topologii; pola pośrednie i wykonywanie ruchu nie są jeszcze wdrożone.

Usunięcie powierzchni z kotwą, trudnym terenem lub końcem schodów jest blokowane konkretnym komunikatem. Resize przesuwa wszystkie powierzchnie, kotwy, wyjścia, końce schodów i trudny teren; odrzuca obcięcie używanej geometrii. Undo/Redo obejmuje edycję powierzchni, schody i resize. Pusta lista powierzchni w starym szablonie jest materializowana dopiero przy edycji podłogi; samo przełączenie poziomu nie zmienia szablonu.

Dalsze zatwierdzenia do etapu ruchu: postać może zatrzymać się na schodach i zakończyć tam turę. Koszt odpowiada liczbie przebytych pól z istniejącymi regułami kosztu ruchu, bez dodatkowej opłaty za zmianę wysokości. Nie traktować długiego połączenia końców jako jednego kroku/teleportu. Integracja musi reprezentować pola pośrednie i pozycję na schodach.

## C2a: jawny przebieg schodów i pozycje pośrednie

Zatwierdzone: autor wskazuje ciąg pól sąsiadujących przez krawędź, także z zakrętami. PathCells obejmuje oba końce i wszystkie pola pośrednie. Długość to liczba przejść między polami, nie płaska kwota AP. Aktualne reguły kosztu zwykłych kolejnych kroków pozostają do użycia przez wykonawcę ruchu.

Room Editor zbiera trasę na poziomie początku; przełączenie poziomu i wskazanie powierzchni końcowej zatwierdza połączenie. Przebieg jest numerowany na siatce. Można cofnąć ostatnie pole szkicu, anulować szkic lub zastąpić trasę istniejących schodów. Anulowanie nie zmienia poprzedniej trasy; zatwierdzenie korzysta z Undo/Redo. Resize przesuwa wszystkie pola i nie pozwala obciąć trasy. Footprint jest rzutem powierzchni oraz schodów, więc pod polem pośrednim nie musi istnieć osobna podłoga.

DungeonTraversalPosition rozróżnia powierzchnię podłogi i pole pośrednie identyfikowane przez kanoniczne połączenie schodów oraz indeks. DungeonFloorMap jest jedynym właścicielem tras; przechowuje kopie tylko do odczytu, indeksuje połączenia przy spocznikach i udostępnia przejścia krok po kroku. Nie ma automatycznego wejścia na schody z podłogi pod polem pośrednim ani skrótu między odległymi końcami. Jawna ściana lub zamknięte drzwi/brama przy wejściu/wyjściu blokują przejście; builder nie zamyka wejść automatyczną ścianą obwodu podłogi.

DungeonExplorationPathfinder ma osobne wejście dla pozycji przejścia i może znaleźć drogę do pola pośredniego lub z niego. Opcjonalny filtr przyjmowania pola pozwala uwzględnić zajętość. Stare wywołania 2D zachowują zachowanie; kontrolery jednostek nie są jeszcze przełączone na nowy typ pozycji. GetStairDestinations nadal opisuje stare końce topologii, nie pojedyncze kroki ruchu; wykonawcy powinni korzystać z GetTraversalNeighbours.

Snapshot v3 zapisuje kanoniczną trasę schodów. Odczyt v1/v2 jest zachowany; v0 pozostaje na ścieżce legacy. Krótkie połączenie sąsiednich pól bez PathCells ma jednoznaczną trasę jednego kroku. Długie stare połączenie bez trasy zostaje zachowane w zapisanym układzie, lecz nie jest udostępniane jako przejście. Edytor wymaga wyznaczenia jego przebiegu przed zapisaniem poprawnego szablonu. Nie zgadujemy trasy, nie regenerujemy lochu i nie przelosowujemy zawartości. Niepoprawna jawna trasa odrzuca cały snapshot.

Zmiana nie zapisuje jeszcze pozycji postaci na schodach, nie wdraża animacji ruchu, wyświetlania balkonów w grze ani reguł FOV/ataków na wysokościach. API pozycji pośredniej i ścieżki jest gotowe do integracji z istniejącymi jednostkami. Nie włączać grywalnego contentu wysokości przed jej zakończeniem.

## Kolejne etapy tej samej karty

C2b. Wspólne pozycje jednostek/obiektów i adapter walki, ruch i wybór celu na powierzchni oraz geometria wizualna. Jedna reprezentacja postaci. Uwzględnić zatwierdzone zatrzymanie na schodach i koszt według liczby pól; wykorzystać topologię pól pośrednich z C2a do wykonania i zapisu ruchu.

D. Zależne FOV/LOS i reguły ataków. Skoki/upadki i premie wysokości dopiero po zatwierdzeniu konkretnych zasad. Nie dodawać zapisu w środku walki.

## C2b — wykonanie, prezentacja i zapis wysokości

Zatwierdzone 2026-09-14: ataki między wysokościami są dozwolone, zasięg pozostaje w rzucie X/Y. Podłoga balkonu jest przesłoną dla ataków i widzenia; otwarte krawędzie i otwory schodów umożliwiają kontakt. Nie wprowadzono premii wysokości, skoków ani upadków.

Page Up / Page Down działa w eksploracji i walce, bez nowego HUD. Podczas planowania zachowujemy ręczny wybór; wykonanie ruchu, zmiana lidera/fokusu i aktywnej jednostki przywracają śledzenie jej poziomu. Projekcja pokazuje najwyższą powierzchnię lub stopień nieprzekraczający wybranego poziomu; niższe odsłonięte podłogi są przygaszone. Stopnie są rysowane z zapisanej trasy. Nie ma dodatkowej produkcyjnej mapy ani osobnych widoków tych samych jednostek.

DungeonTraversalPosition rozróżnia powierzchnię oraz konkretny stopień identyfikowany kanonicznym połączeniem i indeksem. BattleGrid/GridPosition zachowują tę tożsamość. Eksploracja w warstwowym lochu oraz walka wykorzystują sąsiadów istniejącego DungeonFloorMap. Zajętość, klik, ścieżka, przybycie/wyjście z walki i AI uwzględniają wysokość. Schody nie mają dodatkowej opłaty: obowiązuje dotychczasowy rosnący koszt kolejnych kroków; niski murek i trudny teren zachowują dopłaty. Korekta po smoke: eksploracja zachowuje ciągłe pozycje i swobodny ruch, również po przekątnej. Graf sprawdza przekraczanie granic powierzchni, nie rezerwuje pól ani nie narzuca ich środków. Towarzysze nie stanowią blokady całego pola. Zapis zawiera przesunięcie pozycji wewnątrz pola. Usunięto hover-time pathfinding i generowanie podglądu siatki eksploracji. Collidery postaci nie stanowią drugiego modelu ruchu ponad grafem.

LOS używa środków komórek na wysokości powierzchni + 0,5 warstwy; na schodach wysokość interpoluje się według indeksu kroku. Jest to normalizacja geometrii, nie dodatkowy wymiar zasięgu. Test przecięcia podłogi obejmuje szwy między jej polami. Dotychczasowe przeszkody/drzwi pozostają na poziomie 0. Efekty obszarowe i aury nie przechodzą przez strop. Nie dodano pełnego FOV/Fog of War ani nowego modelu wykrywania i pościgu.

FloorDelta.ActorPositions zapisuje pozycje zatwierdzonych kroków dla kluczy player:<characterId>, enemy:<encounterId>:<enemyId> oraz body:<bodyId>. Klucze przeciwników są rozdzielone między encounterami. Stare save’y bez listy zachowują dotychczasowe pozycje poziomu 0. Wczytanie sprawdza istnienie powierzchni/trasy/stopnia; nie generuje nowej geometrii ani nagród. Zapis w trakcie walki pozostaje niedozwolony. Ciała zachowują powierzchnię/stopień śmierci, bez automatycznego spadania na parter.

Weryfikacja C2b: pełne Unity EditMode 941/941 PASS w mirrorze, raport ../.codex/height-integration/final-results.xml; kompilacja PASS. Nowe testy obejmują strop i jego szwy, otwartą krawędź, zajętość schodów/podłogi, wykonanie kroków z AP, celowanie między wysokościami, pośredni poziom schodów i round-trip pozycji/nagród. Stary test scenariusza Pillars sprawdza teraz kompletność opcjonalnej grupy zgodnie z zatwierdzonym modelem zawartości. Smoke użytkownika i PlayMode NOT RUN. C2b READY_FOR_SMOKE; bez commit/push i bez zamykania karty.


## Korekta C2b po smoke: wspólny widok i powrót do eksploracji

Domyślny pułap prezentacji wynika z pokoju aktywnej postaci. Balkon i odsłonięty dół są widoczne razem; lokalny przekrój nad postacią (3×3) usuwa zasłaniające powierzchnie/stopnie i zachowuje obrys podłogi nad nią. PgUp/PgDown ręcznie wybiera przekrój i warstwę siatki; ruch lub zmiana aktywnej jednostki przywraca automat. Prezentacja nie zmienia fizycznej przesłony podłogi, FOV ani grafu ruchu. Robocze ranty podwyższeń są generowaną geometrią mapy, do późniejszego art passu. Swobodny ruch eksploracyjny zachowany; wejście na schody niezależne od kamery. Po ostatniej walce wraca podążanie drużyny, także trasą wokół przeszkody. Ciała pojawiają się podczas walki przez istniejący idempotentny mechanizm nagród. Status i bieżące dowody testów: UNITY_HANDOFF.md.


Korekta zakresu 2026-09-15: pathfinding towarzyszy wycofany do osobnej przyszłej karty. Pozostaje proste podążanie i wznowienie po zakończeniu walk. Aktualny handoff opisuje synchronizację widoku z pełną pozycją i uwzględnienie promienia istniejącego collidera w swobodnym ruchu.


## Ustalenie 2026-09-15 — podesty jako podstawowe zastosowanie

Domyślny pułap to najwyższa wysokość całego bieżącego piętra lochu od chwili Bind, niezależnie od pokoju i wysokości postaci. Nie oznacza to pokazywania innych FloorIndex ani omijania Fog of War. Podesty są priorytetem, dopracowanie balkonów później. Zachowujemy istniejące nakładające się powierzchnie i zapis. Docelowo przekrój odsłania obszar rzeczywiście widoczny dla postaci, w tym widocznych przeciwników pod podestem; obrys pomocniczy. Ta integracja należy do wspólnej karty FOV/Fog of War, bez zastępczego wykrywania w karcie wysokości. Bieżący kod zachowuje lokalny przekrój wokół aktywnej postaci.


Doprecyzowanie 2026-09-15: podest oznacza podwyższone podłoże bez dostępnej podłogi pod spodem; balkony to dodatkowy przypadek na później. Wysokość nie przyciemnia podłogi. Docelowe przyciemnienie wiedzy o mapie należy do Fog of War, niezależnie od wysokości.
