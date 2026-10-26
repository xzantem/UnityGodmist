# VS-10 — materiały i części wyposażenia

Status: DONE, 2026-09-22. Smoke zaakceptowany; commit i push zlecone.

## Zakres i źródła
Źródło: design/content_pass_1.xlsx, Materiały i Wyposażenie; katalog VS01_CONTENT_CATALOG.md. Dwa progi materiałów L1/L11, części bazowego poziomu 5/15, wszystkie cztery klasy. Wyposażenie startowe VS-05 pozostaje odrębne. Materiały przeciwników L21 poza zakresem. Materiały innych theme dostępne przez rotację NPC, nie gwarantowany stock. Alchemiczne efekty, biżuteria i finalne loot table należą do kolejnych kart.

## Pierwszy odczyt aktualnego arkusza
- 182 materiały poziomów 1/11; 52 mają przynajmniej jedno dokładne dopasowanie DisplayName w istniejących assetach. Pozostałe 130 wymagają kontroli aliasów/ID przed utworzeniem — to nie jest jeszcze liczba nowych assetów.
- 192 wpisy części broni i pancerzy poziomów 5/15.
- Głógowy/Głogowy: wariant pisowni tego samego materiału, normalizacja referencji do nazwy z tabeli Materiały.
- Cztery pozycje Wyposażenie!D25, D61, D97, D133 odwołują się do Ostrokrzewowego patyka. Brak takiego materiału; czwarty wariant T2 w Materiały!A97 to Cedrowy patyk. Potwierdzone przez użytkownika: użyć Cedrowego patyka, błąd w arkuszu wyposażenia.

## Istniejący model
EquipmentPartDefinition: zachować ID, RequiredClass, Kind, LevelBracket, Stats i CraftingMaterials. EquipmentPartStats rozdziela wartości bazowe/procentowe; mapowanie zależy od rodzaju części oraz klasy (mana/regeneracja u maga).
CraftableItemDefinition: jednocześnie wynik i receptura specjalisty, Ingredients/OutputAmount/Discipline; cena wyliczana rekurencyjnie ze składników. Analog: Items/Materials/Smithing/CID_IronIngot.asset.

## Kolejność wdrożenia
1. Sprawdzić aliasy i zamknąć mapę istniejących ID oraz zależności receptur.
2. Przygotować authoring przez Unity Editor API: materiały, receptury, 192 części, katalog i referencje wymagane przez istniejące UI craftingu.
3. Walidować brak cykli receptur, nieosiągalnych składników, mieszania tierów, duplikatów ID i rozbieżności statystyk z arkuszem.
4. Testy katalogu/craftingu i smoke czterech klas; dopiero potem acceptance/commit VS-10.

Assety skonfigurowane przez Unity Editor API. Arkusz pozostawiono bez zmian; korekta cedru jest w danych importu. Źródło powtarzalnego authoringu: Assets/_Project/Scripts/Editor/VS10Content.json i VS10EquipmentSetup.cs.

## Walidacja zależności po korekcie cedru
Przygotowano znormalizowaną inwentaryzację `.codex/vs10-normalized-content.json`: wszystkie referencje materiałów w 192 częściach i recepturach rozwiązywalne, brak cykli. Normalizacja obejmuje oczywiste warianty pisowni Głóg/Głog, igła od/do nici i kolejność słów „sztabka meteorytowego żelaza”. Arkusza źródłowego nie zmieniono.

## Potwierdzone warianty igieł
Materiały!B98/B99 zawierają po cztery alternatywne metale. Aktualny CraftableItemDefinition łączy pojedynczą recepturę z definicją wyniku; UI listuje te definicje, a cena jest rekurencyjnie wyliczana z Ingredients. Nie tworzyć czterech różnych igieł ani nie ograniczać arkusza do pierwszego metalu.
Potwierdzone: gracz wybiera wariant receptury, wszystkie warianty dają ten sam przedmiot. Wycena jest minimum spośród kosztów receptur (obecnie 4/21), niezależnie od faktycznie użytego metalu. Brak osobnych typów igieł ani historii użytego metalu w save. Wariant 0 jest dotychczasową recepturą; stare assety bez alternatyw działają bez migracji.

## Implementacja i weryfikacja
- 180 definicji materiałów (109 wyników craftingu / 115 wariantów receptur) oraz 192 części dla czterech klas i progów 5/15. Dwa wpisy nagród z tabeli Materiały to lootbagi, poza authoringiem materiałów VS-10.
- Zachowane istniejące ID/GUID; nowe materiały i części w ScriptableObjects/ContentPass1. Katalog oraz listy receptur specjalistów uzupełnione. Startowe części VS-05 niezmienione.
- CraftableItemDefinition.AlternativeRecipes: warianty mają ten sam output/ID, domyślny wariant 0 zachowuje stare zachowanie. UI pokazuje osobne pozycje z metalem. Request waliduje indeks i zużywa tylko wybrany zestaw składników. Zakup brakujących składników również używa wybranego wariantu.
- CalculatedBaseValue wybiera minimalny rekurencyjny koszt receptury / liczbę wyników; droższy metal nie zwiększa wartości igły. Brak nowego stanu w zapisach postaci.
- 53/53 EditMode PASS (`.codex/vs10-verified.xml`): dane produkcyjne, wszystkie 192 zestawy statystyk/kosztów, źródła surowców wyposażenia w ofertach NPC, warianty/zużycie/wycena, istniejący crafting, alchemia i restore.
- Wszystkie surowce w łańcuchach wyposażenia występują w obecnych ofertach kowala (w tym rotacyjnych). Pełna konfiguracja ofert alchemika/zaklinacza i lootbagów pozostaje w VS-11/12/13; nie oznacza to gwarancji stocku dowolnego surowca.
- Użyteczność surowców alchemicznych i runicznych poza wyposażeniem domykają VS-11/12. Istniejące definicje specjalistyczne zachowano; nowe surowce mają ceny/dyscypliny/receptury, ale ta karta nie dodaje efektów bomb, mikstur ani biżuterii.

## Smoke
Nowa kampania: kowal → materiały: cztery warianty słabej igły i cztery warianty igły T2, odpowiednie składniki w preview, wspólny wynik/stos. Wycena 4/21 niezależnie od wariantu. Crafting broni/pancerzy: każda klasa ma cztery warianty każdego rodzaju części w obu tierach, a mix tierów pozostaje zablokowany. Sprawdzić zapis/wczytanie utworzonego wyposażenia i materiałów. Ręczny Play Mode: zaakceptowany przez użytkownika.

Poprawka po smoke VS-10: inicjalizacja jakości używa SetValueWithoutNotify, aby nie uruchamiać podglądu przed wyborem wyposażenia; BlacksmithCraftingService odrzuca brak Definition czytelnym komunikatem. 17/17 EditMode PASS (`.codex/vs10-preview-fix.xml`), w tym test callbacku dropdownu i podglądu/craftingu bez wybranej definicji. Smoke: ponownie otworzyć kowala → crafting oraz zmienić jakość po wybraniu wyposażenia.

Wydzielenie commita: test źródeł stocku pozostaje w lokalnym pakiecie VS-18, ponieważ wymaga jego modelu rotacji. VS-10 nie publikuje stocku handlarzy. Trzy części Scout zachowują ID z poprzednio opublikowanej wersji (segment common); GUID pozostaje bez zmian.

Przed commitem: izolowany snapshot indeksu Git, bez pozostałych zmian VS-18 — 42/42 EditMode PASS (.codex/vs10-index-verified.xml). Katalog uzupełniony o referencje materiałów niezależnie od stocku VS-18.
