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
