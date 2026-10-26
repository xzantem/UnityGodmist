# VS-13 — gospodarka Arungardu

Źródła: `design/content_pass_1.xlsx`, aktualne role NPC i usługi, `BALANCE_MATERIAL_TRADE.md`. Zakres: pierwszy content pass, poziomy 1–20. Produkcyjne łupy i przeciwnicy Katakumb należą do VS-15, a powtarzalne misje do VS-17.

## Oferta

- Kowal zachowuje skonfigurowany w VS-18 zapas surowców i rotację. Nie zmieniamy cen ani limitów w tej karcie.
- Alchemik: gwarantowane alkohole i prochy strzelnicze właściwego progu (8/6 słabych, od L11 6/4 zwykłych). Z 12 ziół i 12 części potworów losuje po 3 typy, z 8 katalizatorów 2 typy (mocniejsze od L11). Towary spoza Katakumb mogą się pojawić, ale nie ma gwarancji ich dostępności podczas konkretnego powrotu.
- Zaklinacz: stała sztabka żelaza, a od L11 lity wapień i wapienny kamień galdurytu. Rotuje 2 inne metale, 3 z 10 zwykłych klejnotów oraz od L11 2 z 10 proszków. Obsydian pozostaje nagrodą bossa Katakumb. Wyższe kamienie galdurytów nie są oferowane w pierwszym passie.
- Oprócz składników specjaliści wystawiają gotowe egzemplarze: Alchemik 3 mikstury i 3 bomby, Kowal 3 bronie i 3 pancerze, Zaklinacz 3 biżuterie oraz od L11 3 galduryty T1. Każdy jest wygenerowany przez tę samą usługę co crafting gracza, z rzeczywistymi efektami/statystykami i wartością materiałów. W ofercie biżuterii nie ma Obsydianu. Próg galdurytów uwzględnia również postać w rezerwie.
- Po ukończeniu piętra i powrocie do miasta w każdej kategorii jeden z trzech niesprzedanych egzemplarzy zostaje, a dwa są losowane na nowo. Kupione sztuki nie wracają automatycznie przed powrotem. Zapis przechowuje identyfikatory konkretnych egzemplarzy, więc nie zmienia ich efektów ani cen po wczytaniu.
- W każdej trójce gotowych broni i pancerzy dwie sztuki mają jakość dominującą dla najwyższego poziomu w całym rosterze, a jedna sąsiednią. W obrębie każdego progu 1–10 i 11–20 dominacja przechodzi od Słabej przez Normalną do Doskonałej; na granicy T2 stare egzemplarze T1 są zastępowane. Części i baza klasy pozostają losowe.
- Stare `InitialOffer` Alchemika i Zaklinacza są czyszczone przy konfiguracji. To usuwa przedwczesne medium i kamienie wysokich tierów z **nowych kampanii**. Istniejące zapisy przechowują własny stan sklepu; migracja ich dawnych ofert wymagałaby odróżnienia towaru startowego od przedmiotów odsprzedanych NPC i nie jest wykonywana automatycznie.

Wszystkie 24 składniki efektów mikstur/bomb, oba media w każdym z dwóch progów, katalizatory, 8 metali biżuterii, 10 zwykłych klejnotów i proszki mają źródło w ofercie. Alternatywne składniki do bandaża i suchej szmatki pochodzą z oferty Kowala. Oferta rotacyjna nie gwarantuje konkretnego wariantu receptury; gracz może korzystać z łupu i zapasów z wcześniejszych wypraw. Receptury i usługi pozostają dostępne od startu, a wytwarzanie/obsługa galdurytów wymaga postaci L11 (istniejąca blokada runtime).

## Siła nabywcza i koszty usług

Referencyjne piętro: 6 walk i 2–3 skrzynki. Według `BALANCE_MATERIAL_TRADE.md` daje średnio ok. 882 zł na L5 i 3430 zł na L15 bez misji, bossa i sprzedaży. Zestaw broni i pancerza ze wszystkich kupionych materiałów kosztuje 756–816 zł na L5 oraz 3182–3834 zł na L15. Po uwzględnieniu średnich materiałów z wyprawy zakup braków wynosił 45–113 zł na L5 i 34–128 zł na L15; ceny są osiągalne, jeśli właściwy surowiec pojawi się w ofercie. Nie ma podstaw do ogólnego obniżenia złota na podstawie samej tej analizy.

Słaby alkohol (46 zł) i trzy zwykłe składniki (ok. 13–21 zł każdy) dają przykładową miksturę za ok. 85–109 zł, gdy wszystko jest kupowane. Wapienny kamień (135 zł) i najtańszy proszek (ok. 286 zł) tworzą galduryt T1 o wartości bazowej ok. 421 zł; identyfikacja 30% i osadzenie 50% kosztują razem ok. 338 zł przed rabatami. Takie dodatki są zauważalną inwestycją wobec przychodu z piętra L15. Reforge, upgrade i wyjęcie galdurytu używają obecnych cenników VS-18, w tym pełnej bieżącej wartości przedmiotu; nie zmieniamy mnożników w VS-13.

Gotowe przedmioty oferują drogę zakupu bez zbierania pełnej receptury. Ich koszt zakupu wynika z faktycznej wartości wygenerowanego egzemplarza i dotychczasowego rabatu lojalnościowego; nie dostają osobnego mnożnika sklepowego. Ta wygoda zwiększa możliwości wydawania złota, ale nie dodaje źródła przychodu. Tempo dochodu nadal wymaga sprawdzenia na produkcyjnych łupach i misjach.

Jedyna aktualna misja `Zbadaj Katakumby` ma stałe 25 zł i charakter wprowadzający. Nie jest odpowiednikiem docelowej powtarzalnej misji (VS-17). Bilans jej nowych nagród będzie trzeba sprawdzić po autorstwie misji, łupów, skrzyń i rzeczywistej długości wypraw w VS-15–18.
