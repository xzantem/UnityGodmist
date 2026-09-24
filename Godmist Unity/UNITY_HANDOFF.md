# Unity handoff

Karta: `VS-14 — produkcyjne pokoje Katakumb` (`DONE`, smoke wejścia zaakceptowany 2026-09-23)

2026-09-23: 16 autorskich szablonów jest zapisanych w produkcyjnym katalogu. Poprawiono też pustą tablicę: selektor czerpie dostępne wyprawy z katalogu gry, więc stara definicja przypięta do wejścia w Arungardzie nie blokuje ofert. Po zgłoszeniu rozbieżności ze szkicami ponownie wygenerowano wszystkie 16 assetów tą samą regułą rozszerzania siatki co wizualizacja. Obramowane pokoje zachowują ściany szkicu, z dwupolowymi otworami tam, gdzie wymagają tego drzwi. Po kolejnym review dodano cztery jawne kotwy w 11 pokojach przeznaczonych dla większych składów oraz pełne ściany, niskie murki i balustrady jako `StructuralAnchors` w 11 pokojach. Profil Katakumb wybiera teraz zwarte ułożenie głównej trasy spośród legalnych obrotów i kierunków, a cztery pokoje dostały możliwość bocznego połączenia. Ocena czytelności decyzji taktycznych wymaga smoke Play Mode.

Użytkownik potwierdził w Play Mode poprawę kształtu piętra i zaakceptował obecną jakość pokojów na pierwszy content pass. Pełne sprawdzenie przejść, pułapek i wymaganych obiektów z listy poniżej nie zostało jeszcze potwierdzone; późniejszy polish pokojów pozostaje poza VS-14.

Po tym smoke poprawiono cztery zgłoszenia. Na **nowo wygenerowanej** wyprawie sprawdź, że Skarbiec z pułapką i Odetchnienie nie mają encounterów; zapis już wygenerowanego piętra zachowuje jego dotychczasowy układ. Obserwuj patrol w pokoju z pułapką, również po zapisie i wczytaniu: trasa powinna ją omijać. `togglefog` powinno natychmiast pokazać aktywne pułapki i inne obiekty wraz z aktualnym stanem, a po ponownym włączeniu mgły nie ujawniać ich w pamięci mapy. Sprawdź odczuwalny czas wyboru dalekosiężnej umiejętności obszarowej (np. Kula Ognia) i pojedynczego celu (np. Uzdrowienie); wynik Play Mode pozostaje do potwierdzenia. Duże jednokolorowe podświetlenia skilli mają teraz jedną siatkę renderowania zamiast osobnego renderera na pole. Testy w izolowanej kopii Unity: 61/61 zawartości, 7/7 rozmieszczania encounterów, 9/9 patrolu, 18/18 mgły, 7/7 targetowania i 9/9 prezentacji walki (`.codex/vs14-smoke-fixes-*.xml`).

Naprawiono też błąd generacji, który dla niektórych seedów uniemożliwiał wejście do Katakumb po 32 próbach. Skróty są dopinane dopiero po rezerwacji drzwi głównej trasy i tylko wtedy, gdy mają własne legalne przejście. Seed 15 zwykłej wyprawy, który wcześniej reprodukował wyjątek, teraz generuje oba piętra. Testy w izolowanej kopii Unity: 62/62 zawartości (`.codex/vs14-content-fixed.xml`), 16/16 generatora (`.codex/vs14-generator-fixed.xml`) oraz 40/40 seedów zwykłych i 40/40 z bossem (`.codex/vs14-seed-sweep-fixed.log`). W Play Mode sprawdź ponownie wyruszenie na oba warianty wyprawy.

## Konfiguracja Unity

Konfigurator `Godmist > Content > Configure VS-14 Catacombs` został uruchomiony w izolowanej kopii Unity. Wszystkie 16 zapisanych szablonów, katalog i definicje wypraw przeniesiono do głównego projektu; ich referencje są spójne. Nie ma ręcznej konfiguracji Editora. Komenda pozostaje dostępna i jest idempotentna.

Konfiguracja obejmuje przedsionek, dziewięć różnych pokojów walki, trzy odnogi opcjonalne, dwa korytarze i komnatę finałową. Są tu między innymi klepsydra z pułapkami, rozdzielona nawa, pokój L, krzyż z sarkofagami, płaski pierścień z pustym środkiem, skarbiec z pułapką na każdej drodze do skrzyni i łamany korytarz. Trasy, osłony, pułapki oraz wejścia zapisano w assetach. Generator dobiera pozycję pokoju z uwzględnieniem jego dozwolonych drzwi, także po obrocie. Wszystkie pokoje są płaskie, do czterech wrogów. Pełne role i ograniczenia opisuje `VS14_ROOM_DESIGN.md`.

Są dwie definicje wypraw: zwykła bez bossa i z bossem. Obie mają dwa piętra, 11–14 pokojów i 4–6 zwykłych walk na piętro; tablica oferuje oba warianty, a definicje demo pozostają poza ofertą. Testowe definicje pozostają w katalogu do obsługi starych zapisów i scenariuszy testowych. Encountery i boss są tymczasowymi definicjami z `DD_Catacombs_Procedural`; produkcyjne pule przeciwników należą do VS-15.

## Smoke test

1. W nowej kampanii otwórz tablicę wypraw. Musi pokazać przynajmniej jedną wyprawę `Katakumby - wyprawa` i jedną `Katakumby - wyprawa z bossem`; żaden `Demo` ani `test` nie powinien wejść do sześciu ofert.
2. Wejdź do każdego wariantu. Sprawdź dwa piętra, brak balkonów/podestów, przechodnie przejścia, skrzynie i finał. Wariant zwykły kończy się bez walki z bossem, wariant z bossem wymaga jej ukończenia. Kilka różnych ofert powinno zmienić układ pomieszczeń. Zwróć uwagę na kształt całego piętra: główna droga powinna kilkakrotnie zakręcać, a pokoje zajmować przestrzeń w obu osiach zamiast tworzyć bardzo długi pas z pustymi bokami.
3. Zagraj przynajmniej skarbiec, pierścień i dwa inne pokoje taktyczne. Oceń, czy droga do nagrody rzeczywiście wymaga minięcia pułapki, czy w walce widać sensowne obejścia i osłony oraz czy drzwi nie pozwalają ominąć wyzwania.
   W edytorze porównaj też `DRT_Catacombs_Normal_Small_Production` (Wąskie gardło), `DRT_Catacombs_L_Production` (Zakręt krypty), `DRT_Catacombs_Tactical_Pillars_Production` (Galeria łuczników) i `DRT_Catacombs_Tactical_Ritual_Production` (Krąg rytualny) z wizualizacją. Obrys, luki, pułapki, osłony, dodatkowe kotwy wrogów i krawędzie strukturalne powinny odpowiadać szkicom; w pokojach z obwodową ścianą dochodzą otwory drzwiowe. Litera `E` w edytorze Unity oznacza kotwę wroga, a w szkicu wejście. W Play Mode sprawdź też niski murek w Galerii łuczników (+2 AP za przejście), pełne ściany w Labiryncie cel oraz balustrady wokół pustego centrum Kręgu rytualnego.
4. Wyjdź i wczytaj zapis rozpoczętej wyprawy; układ i wybrany wariant mają pozostać te same. Docelowy czas 20–30 minut wymaga ręcznego pomiaru po dodaniu siedmiu produkcyjnych przeciwników i bossa w VS-15.

Automatycznie, w izolowanej kopii Unity: 61/61 testów pokojów i wygenerowanych wypraw (`.codex/vs14-compact-content-tests.xml`) oraz 16/16 regresji generatora (`.codex/vs14-compact-generator-tests.xml`). Obejmują zgodność 16 footprintów ze szkicami, kotwy wrogów, typy ścian, przejścia, wymagane obiekty, 12 seedów obu wariantów wyprawy i zwartość układu. Pomiar 40 wygenerowanych pięter (`.codex/vs14-compact-v3-audit40.log`) dał średnio 4,48 zakrętu głównej trasy, dłuższy bok 73 pola i zajęcie obrysu 48% wobec odpowiednio 1,4 zakrętu, 101 pól i 36% przed zmianą. Wcześniej 16/16 testów tablicy ofert (`.codex/vs14-offer-ui-integration.xml`) potwierdziło sześć ofert, w tym oba warianty Katakumb. Ponowny smoke Play Mode oraz pomiar czasu wyprawy: NOT RUN.

---

Karta: `Dodac crafting bizuterii v1`

## Dane do wprowadzenia w Editorze

Interfejs oraz zakładka Enchantera są już podpięte w prefabie. Do dodania pozostają wyłącznie dane contentowe.

1. Utwórz `EnchanterRoleData` i przypisz do definicji NPC Enchantera razem z `TraderRoleData`; dodaj go ręcznie do miasta.
2. Utwórz cztery `Jewelry Item Definition`: pierścień, naszyjnik, bransoleta i kolczyki. Inspector tych assetów pokazuje wyłącznie pola wspólne dla typu: `Id`, opcjonalną ikonę, wagę, slot, metale i klejnoty.
3. Nie ustawiaj na definicjach `Display Name`, `Description`, `Base Value`, `Base Rarity`, `Required Class` ani `Item Type`. Wynik craftingu wylicza nazwę z typu + metalu + klejnotu, cenę z materiałów, rarity i level z metalu oraz zawsze usuwa wymaganie klasy. `Item Type` jest automatycznie wymuszany jako Jewelry.
4. Dla wariantu żelaznego każdej definicji ustaw wyłącznie `Ingredients: 1 × Żelazny łańcuch`. Poziom i rarity nie są polami wariantu: wynikają automatycznie z najwyższego rarity jego składników (`Common → 10`, `Uncommon → 20`, `Rare → 30`, `Ancient → 40`, `Legendary+ → 50`).
5. Dodaj stackowalne materiały Topaz (`Uncommon`, `1151`, `0`), Bursztyn (`Uncommon`, `1284`, `0`) i Obsydian (`Rare`, `2847`, `0`). Każdy wpis `JewelryGemData` ma wskazywać odpowiedni materiał i `CraftingAmount: 1`.
6. Dodaj trzy klejnoty osobno do listy `Gems` wszystkich czterech definicji biżuterii, zachowując ich istniejące efekty. Dodaj materiały oraz Żelazny łańcuch do `TraderRoleData.InitialOffer` Enchantera.
7. Dodaj wszystkie nowe materiały i cztery definicje biżuterii do `GameContentCatalog.ItemDefinitions`.

## Smoke test

1. Otwórz usługi Enchantera i wybierz typ biżuterii, żelazny metal oraz każdy z trzech klejnotów.
2. Potwierdź wynik: poziom `10`, rarity `Common`, koszt `1 × Żelazny łańcuch + 1 × wybrany klejnot`.
3. Sprawdź zakup brakującego materiału bezpośrednio z `MaterialsGrid`.
4. Wytwórz przedmiot, załaduj zapis i potwierdź metal, klejnot, level, rarity, cenę oraz efekty w tooltipie.

Oczekiwany rezultat:

- Enchanter wytwarza biżuterię bez modelu trzech części Kowala; level i rarity wynikają z metalu, a klejnot jest niezależnym, pojedynczym wyborem.

---

Karta: `Dodac crafting i identyfikowanie galdurytow/run v1`

## Dane do wprowadzenia w Editorze

Kod obsługuje już runtime, save/load, crafting i identyfikowanie. Nie zmieniaj ręcznie wygenerowanych plików `*.csproj`; Unity odświeży je przy imporcie skryptów.

1. Utwórz jeden `Galduryte Item Definition` dla wyniku, np. `Galduryte_Generic`. Ustaw unikalne `Id`, ikonę oraz wagę zgodną z docelowym balansem. `Is Stackable` musi być wyłączone; nazwa, rarity i wartość wyniku są ustawiane runtime.
2. Utwórz sześć zwykłych materiałów o dyscyplinie `Runic`:
   - `Lity wapień` — `Uncommon`, `271g`;
   - `Lity granit` — `Rare`, `840g`;
   - `Lity marmur` — `Legendary`, `16056g`;
   - trzy proszki galdurytowe, po jednym dla używanych kolorów w puli v1 (`Czerwony`, `Niebieski`, `Purpurowy`). Ich cena wynika z istniejącej definicji materiału.
3. Utwórz trzy `Craftable Item Definition` dla kamieni galdurytu. Każda recepta ma dyscyplinę `Runic`, dokładnie jeden składnik i wynik `2`:
   - `Lity wapień` `1x` → `Wapienny kamień galdurytu` `2x`;
   - `Lity granit` `1x` → `Granitowy kamień galdurytu` `2x`;
   - `Lity marmur` `1x` → `Marmurowy kamień galdurytu` `2x`.
4. W `ER_Arungard_Enchanter` przypisz `GalduryteDefinition`, dodaj trzy wpisy `GalduryteStones` z tierami `Limestone`, `Granite`, `Marble` oraz wpisy `GaldurytePowders` z kolorami proszków. Receptury z kroku 3 dodaj także do `MaterialRecipes`, aby korzystał z nich istniejący panel wytwarzania materiałów.
5. Dodaj wynik galdurytu, trzy kamienie galdurytu, trzy proszki oraz receptury do `GameContentCatalog.ItemDefinitions`. Do `TRD_Arungard_Enchanter` dodaj materiały, które mają być dostępne w sklepie.
6. Prefab `PF_UIW_NpcServicesWindow` ma mieć dwa osobne content rooty usług: `GalduryteCraftingServiceRoot` oraz `GalduryteIdentificationServiceRoot`. Crafting: po lewej `Broń/Pancerz`, tier i proszek; po prawej podgląd bez efektów przed wykonaniem, materiały i `Wytwórz`. Identyfikacja: po lewej niezidentyfikowane galduryty z ekwipunku; po prawej podgląd, koszt i `Identyfikuj`. Nie dodawaj `PF_UIW_Window` wewnątrz paneli — oba widoki są treścią istniejącego okna usług.
7. W `tabDefinitions` prefab ma mieć wpisy `GalduryteCrafting` oraz `GalduryteIdentification`; zakładki pojawiają się tylko dla NPC z `EnchanterRoleData`.

## Reguły v1

- Kamień ustala poziom: wapień `11`, granit `21`, marmur `41`.
- Rarity jest losowane wyłącznie w zakresie `Common–Godly` z wagami `35/25/15/10/7,5/5/2,5%`.
- W puli v1 broń ma: obrażenia ogólne (czerwony), szansę krytyczną (niebieski), celność (purpurowy). Pancerz ma: maksymalne zdrowie (czerwony), obronę fizyczną (niebieski), szybkość (purpurowy).
- Kolor proszku gwarantuje efekt z własnej puli; pozostałe efekty są wybierane z innych kolorów bez powtórzenia koloru.
- Tier 1 losuje `2–3` efekty. Tier 2 i 3 celowo zwracają czytelny błąd, dopóki nie zostaną dodane kolejne pule efektów.
- Wytworzony galduryt jest od razu zidentyfikowany. Galduryt utworzony z lootu/debugu może być niezidentyfikowany; zapis zachowuje jego ukryte efekty.
- Identyfikacja u Zaklinacza kosztuje domyślnie `30%` wartości galdurytu i jest konfigurowalna jako Game Define `items.galduryte.identification_cost_percent`.

## Smoke test

1. Prefab `PF_UIW_NpcServicesWindow` jest już podpięty: uruchom NPC Zaklinacza i sprawdź osobne zakładki `Wytwarzanie galdurytów` oraz `Identyfikacja galdurytów`.
2. Dodaj do aktywnej postaci jeden kamień tieru 1 i proszek, wybierz `Broń` albo `Pancerz`, wytwórz galduryt i sprawdź `Poziom 11`, rarity, efekt gwarantowany oraz brak powtórzeń kolorów.
3. Wykonaj `creategalduryte 1 weapon` oraz `creategalduryte 1 armor unidentified`; sprawdź tooltip `Niezidentyfikowany`, identyfikację w drugiej zakładce za złoto i zachowanie efektów.
4. Wykonaj `creategalduryte 2 weapon`. Oczekiwany jest kontrolowany błąd o zbyt małej puli efektów, nie duplikat i nie częściowy item.
5. Zapisz grę z niezidentyfikowanym galdurytem, wczytaj zapis i sprawdź, że po identyfikacji pokazuje te same efekty.

## VS-15 — przeciwnicy i łupy Katakumb, 2026-09-23

Zapisano siedmiu przeciwników, 21 umiejętności, osiem składów po 2–4 wrogów oraz tabele łupów dla poziomów 1–10 i 11–20. Kostur Umarłych przywołuje produkcyjne szkielety; wariant wyprawy z bossem pozostaje wyłączony do VS-16. Sprite’y mają osobne klatki z importem Point/16 PPU/bez kompresji. Docelowe grafiki należą do VS-22.

Kompilacja i 100/100 testów powiązanych systemów EditMode PASS; po konfiguracji sprite’ów 7/7 testów zawartości VS-15 PASS. Użytkownik potwierdził poprawioną prezentację w Play Mode i zlecił commit/push.

## VS-16 — Duch Umarłego Króla, 2026-09-23

Assety zostały skonfigurowane i skopiowane do głównego projektu za zgodą użytkownika. Nie trzeba uruchamiać `VS16CatacombsBossSetup.Configure` w Unity. Boss ma trzy umiejętności: Królewskie Cięcie, Straż Przyboczną oraz Zemstę Zaświatów. Przekroczenie każdego progu 75/50/25% HP daje bossowi jedno dostępne użycie Zemsty. W swojej kolejnej akcji używa jej przed innymi skillami za 0 PA i 0 MP, stawiając do pięciu run; jeśli nie ma wolnego miejsca, użycie czeka. Każda runa ma dwie własne tury AV, ostrzega w pierwszej i wybucha w drugiej. Wejście postaci gracza na runę usuwa ją. Dla runy przygotowano tymczasowy sprite 16×16, Point, 16 PPU, bez kompresji. Grafika docelowa pozostaje w VS-22.

Tablica odblokowuje wyprawę z bossem po osiągnięciu 100% Danger motywu i utrzymuje ją do ukończenia, również gdy zwykłe wyprawy obniżą Danger. Low = 0–39%, Medium = 40–69%, High = 70–99%; 100% oznacza maksymalne zagrożenie. Od poziomu 8 ukończona wyprawa obniża Danger własnego motywu o 30 punktów i podnosi Danger pozostałych dostępnych motywów o losowe 10–20 punktów. Kiedy dowolna postać osiąga poziom 10, 20 itd., jeden losowy dostępny motyw osiąga 100%. W obecnym passie zawsze są to Katakumby; boss ma stały poziom wariantu 10 lub 20, niezależny od późniejszej zmiany składu drużyny. Losowanie progu i odblokowana oferta są zapisane w kampanii; po pokonaniu bossa Danger Katakumb wraca do 0%. Obecne UI nadal pokazuje sześć slotów ofert, a docelowy wariant jednej wyprawy na motyw należy do przyszłej zmiany tablicy.

Smoke w Play Mode:

1. W nowej kampanii ustaw jedną postać komendą `setlevel <characterId> 10`, otwórz tablicę i sprawdź Danger 100% oraz ofertę „Katakumby - wyprawa z bossem” na poziomie 10. Na poziomie 1 i 9 boss nie powinien pojawić się w ofercie. Zamknij i otwórz tablicę oraz zapisz i wczytaj grę: seed, poziom i oferta bossa mają pozostać te same. Ukończ zwykłą wyprawę Katakumb: Danger spadnie o 30 punktów, ale oferta bossa pozostanie. Po ukończeniu bossa Danger wróci do 0%, a po osiągnięciu poziomu 20 pojawi się wariant bossa poziomu 20.
2. W finale wyprawy z bossem sprawdź jego ruch, dwupolowe Cięcie oraz przywołanie jednego z czterech szkieletów. Jednocześnie może mieć do trzech strażników. Po spadku HP przez 75%, 50% i 25% runy nie pojawiają się od razu; boss używa Zemsty Zaświatów jako swojej następnej akcji, bez zużycia PA/MP, i wtedy stawia do pięciu run. Jeśli jedno trafienie przekroczy kilka progów, powinien rzucić skill osobno dla każdego z nich. Wejdź na runę; powinna zniknąć bez zadania obrażeń. Pozostałe mają ostrzec w swojej pierwszej turze AV i uderzyć najbliższego bohatera w drugiej. Po śmierci bossa runy i strażnicy znikają.
3. Wygraj i sprawdź nagrody. Na poziomie 10 torba wypada tylko wtedy, gdy wylosuje unikat (30%); pusta torba nie powinna trafiać do ekwipunku. Jeśli stary zapis zawiera już pustą torbę, kliknięcie usuwa ją bez ostrzeżenia. Na poziomie 20 otwieralna torba może zawierać Królewską ektoplazmę, granat, bursztyn i trzy galduryty o unikalności co najmniej 5. Zapisz i wczytaj przed walką oraz po walce: ta sama walka nie może wypłacić nagród ponownie, a nowa wyprawa może znowu wylosować łup. Sprawdź też przegraną i możliwość rozpoczęcia następnej wyprawy.

Automatyczna weryfikacja w izolowanej kopii Unity: 96/96 testów VS-16, walki, ofert, łupów i sąsiednich systemów PASS; 27/27 testów zakończenia walki, nagród i generatora PASS; po korekcie nazwy finałowego pokoju 5/5 testów bossa PASS, w tym generacja i rozmieszczenie pojedynczego bossa dla obu poziomów i trzech seedów. Kompilacja 0 błędów. Status: READY_FOR_SMOKE; Play Mode i zapis/wczytanie NOT RUN. Bez commit/push.

Korekta po smoke: AI ocenia teraz wszystkie cztery kierunki prostokątnego Królewskiego Cięcia. Wcześniej cel stojący po lewej lub za bossem nie był uwzględniany, bo AI zawsze sprawdzało kierunek +X. Test bossa po poprawce 7/7 PASS (w tym cztery strony i atak z dostępną Strażą Przyboczną); regresja AI 50/50 PASS. Sprawdź w Play Mode, czy boss atakuje po podejściu drużyny z różnych stron.

Korekta widoczności strażników po smoke: przywołany szkielet miał sprite, ale nowy widok `PF_BattleUnit` używał rzędu renderowania 0, wspólnego z posadzką. Nowe widoki jednostek w lochu mają co najmniej rząd 10, jak istniejące widoki eksploracji. Test bossa 8/8 PASS, regresja widoków lochu 56/56 PASS, mgły 18/18 PASS. Sprawdź w Play Mode, czy każdy z przywołanych szkieletów jest widoczny zaraz po pojawieniu się i znika po zakończeniu walki.

Korekta po kolejnym smoke: po zwykłym wyjściu z lochu sojusznicy w Arungardzie mają pojawić się na wolnych polach obok lidera i podążać za nim; wcześniej byli ustawiani w linii przez ścianę. W lochu wrogowie mają zachować oryginalne kolory sprite'ów, a przedmioty w ekwipunku i oknie łupów mają pokazywać tooltipy ponad oknami. Klawisz I otwiera i zamyka okno aktywnej postaci podczas eksploracji, również w lochu; wpisywanie w polu tekstowym nie wywołuje skrótu. Weryfikacja kompilacji Views, Systems i EditModeTests PASS; 21/21 testów EditMode bossa i lootbagów PASS w izolowanej kopii Unity. Ponowny smoke Play Mode wymagany. Bez commita/pusha.

Zmiana dostępności wyprawy bossowej po decyzji użytkownika: Danger i odblokowanie wariantu są zapisywane. Ukończenie zwykłej wyprawy Katakumb od poziomu 8 odejmuje 30 punktów Danger, lecz nie usuwa już odblokowanego bossa. Wyprawa z bossem resetuje Danger do 0%. W pierwszym passie próg najwyższej postaci 10 i 20 zawsze wskazuje Katakumby. Kompilacja `EditModeTests.csproj` PASS; regresja ofert, bossa oraz zapisu/odczytu 61/61 EditMode PASS w izolowanej kopii. Sprawdzić w Play Mode punkty 1–3 powyżej; bez commita/pusha.

Do szybkiego smoke ofert można wpisać w konsoli `setdanger catacombs 100`. Komenda ustawia Danger bez zmiany poziomu postaci i odświeża tablicę ofert; przy 100% powinna pojawić się wyprawa z bossem na najbliższym dostępnym poziomie wariantu. `setdanger catacombs 50` ustawia poziom Medium. Odblokowana już wyprawa bossowa pozostaje dostępna do jej ukończenia, nawet po ręcznym obniżeniu Danger. Kompilacja `EditModeTests.csproj` PASS, testy konsoli i ofert w izolowanej kopii Unity 42/42 PASS.

Korekta tooltipów po smoke: w scenie lochu osobny tooltip jest osadzony w HUD-zie walki. Po zakończeniu walki HUD wyłącza się, a globalna referencja wskazywała nadal jego nieaktywny tooltip; dlatego opisy przedmiotów nie pojawiały się ani w ekwipunku, ani w oknie łupów. Cykl włączenia/wyłączenia przywraca teraz aktywny tooltip eksploracji; pokazanie opisu ponownie podnosi jego warstwę ponad okna. Kompilacja PASS, testy cyklu HUD-u i sąsiednich okien 27/27 PASS w izolowanej kopii Unity. Użytkownik potwierdził działanie tooltipów w Play Mode. Bez commita/pusha.

Korekta AI przywołanych szkieletów: w lochu kontakt z bohaterami wymagał widoku eksploracji, którego przywołania nie posiadają. Szkielety widzą teraz cele w standardowym zasięgu 10 pól, nadal z uwzględnieniem ścian i przeszkód. W smoke walki z bossem pozwól strażnikowi zużyć manę na aurę i sprawdź, że w kolejnych turach, kiedy nie może ponownie zapłacić za aurę, atakuje widocznego bohatera. Miecznik, Łucznik i Obrońca mają atak za 0 MP; Okultysta potrzebuje 5 MP na Inkantację i regeneruje 15 MP na początku tury. Kompilacja EditModeTests PASS; testy niskiej many 4/4, bossa 13/13 i walki lochowej 56/56 PASS. Play Mode pozostaje do sprawdzenia; bez commita/pusha.

Korekta Zemsty Zaświatów: boss sam używa umiejętności w swojej turze po przekroczeniu progu HP. Próg nie tworzy run w chwili otrzymania obrażeń. AI nadaje temu użyciu pierwszeństwo, a skille za 0 PA/MP nie blokuje nawet wyczerpany budżet PA. Każdy próg daje najwyżej jedno użycie, również gdy jedno trafienie przekroczy kilka progów. Testy bossa i AI 64/64 PASS, kompilacja EditModeTests PASS. Smoke Play Mode według punktu 2 powyżej pozostaje do wykonania; bez commita/pusha.

Korekta skrzyń i run po smoke: skrzynia losuje teraz tę samą liczbę torb zasobów co wcześniej, ale umieszcza w sobie ich przedmioty bezpośrednio. Dodatkowe losowania broni, pancerzy i galdurytów pozostają bez zmian. Zemsta Zaświatów wybiera wolne pola wyłącznie w pokoju, w którym boss stoi podczas rzucania, nawet jeśli siatka walki obejmuje całe piętro. Kompilacja EditModeTests PASS; testy bossa i prefabu 16/16, tabel łupu 14/14, AI 50/50 i walki lochowej 56/56 PASS w izolowanej kopii Unity. Nowo utworzona wyprawa jest potrzebna do sprawdzenia nowej zawartości skrzyń; istniejący zapis zachowuje wcześniej wylosowane przedmioty.

### Wskaźnik użycia skilla

Za zgodą użytkownika skonfigurowano prefab `Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab` przez Unity Editor API. Zawiera ciemny baner na środku górnej krawędzi oraz tekst TMP; referencje `CombatHudView.skillCastBanner` i `skillCastText` są przypięte. Scena `DungeonSandbox` dziedziczy ten HUD z prefabu. Ręczna konfiguracja Editora nie jest potrzebna. Kod pokazuje przez 1,6 s nazwę rzucającego i umiejętności; zdarzenie bossa jest obsługiwane osobno od zwykłych skilli.

Korekta po smoke: baner nachodził na portrety kolejki tur. Obecnie znajduje się bezpośrednio pod panelem celu: panel kończy się na Y −204, a baner zaczyna na Y −212 (odstęp 8). Baner ma 560×80, tekst 36 i dwie linie: nazwa rzucającego oraz nazwa skilla. Prefab zaktualizowano w Unity Editor API; kompilacja `Views.csproj` PASS. Układ wymaga smoke Play Mode w używanej rozdzielczości.

Smoke w Play Mode: użyj skilla bohatera, zwykłego przeciwnika oraz Zemsty Zaświatów. Napis powinien pojawić się u góry, zniknąć po 1,6 s i nie zdradzać skilla niewidocznego przeciwnika. Nowo utworzona wyprawa ma dawać bezpośrednie przedmioty w skrzyni, a runy mają pozostać w pokoju bossa. Bez commita/pusha.

2026-09-24: po korekcie położenia banera użytkownik zlecił commit/push VS-16 i przejście do VS-17. Szczegółowy smoke końcowy pozostaje w acceptance passie.
