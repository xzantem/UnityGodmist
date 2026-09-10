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
