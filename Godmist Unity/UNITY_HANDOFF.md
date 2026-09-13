# Unity handoff

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
