# CP2-11 — Mag Mrozu: przygotowanie projektu

**Stan: ZAAKCEPTOWANA — wszystkie siedem pakietów wdrożone; smoke PASS; commit/push zlecony.**

Źródło: `design/content_pass_2.xlsx`, Drzewka!A97:G122. SHA256: `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`.

Zakres: 25 węzłów T1–T5, trzy aktywne zaklęcia. Arkusz ma 43 rangi; zatwierdzono trzy rangi Parzącego Mrozu, więc wdrażane drzewko ma 44 rangi. Trzecie drzewko istniejącej klasy Maga; zachować oba stare drzewa, starter Alison, limit L50 i dotychczasowe progi SP.

## Węzły i wymagania

Wymagania w kolumnie propozycji zatwierdzono w pakiecie 1. T3:3 otrzymuje nazwę „Trwała Aura” i wydłuża Aurę Mrozu do 5 tur; Parzący Mróz ma trzy rangi: 135/145/160%. Identyfikatory pozostają zależne od slotu, bez kolizji przy zmianie nazw.

| Slot / wiersz | Węzeł | Wymagania w arkuszu | Propozycja wymagań | Efekt z arkusza |
|---|---|---|---|---|
| t1_1 / 98 | Siła Mrozu [3] | — | — | Ataki mają 10%/15%/20% spowolnienie przeciwnika o 10 na 2 tury |
| t1_2 / 99 | Hipotermia [2] | t1_4 | t1_4 | Przeciwnicy zmrożeni i zamrożeni otrzymują leczenie zredukowane o 35%/75% |
| t1_3 / 100 | Ostry Sopel [3] | — | t1_4 | Sopel Lodu ma 50% szansy na wywołanie krwawienia 40%/60%/90% na 2 tury |
| t1_4 / 101 | Sopel Lodu [30 PM, 65% PA] | — | — | Zadaje 125% obrażeń; [50%] zamrożenie na 2 tury; |
| t1_5 / 102 | Odmrożenie [2] | — | — | Ataki na ogłuszonych przeciwnikach mają 50%/100% szansy na przekształcenie ogłuszenia w zamrożenie |
| t2_1 / 103 | Mrożenie [1] | t1_1 | t1_1 | Zwiększa siłę spowolnienia Siły Mrozu do 15 |
| t2_2 / 104 | Śnieżny Duch [1] | t2_1 | t2_1 | Siła Mrozu ma 50% szansy na zmrożenie przeciwnika zamiast spowolnienia |
| t2_3 / 105 | Parzący Mróz [2] | t1_3 | t1_3 | Zwiększa obrażenia Sopla Lodu do 135%/145%/160% |
| t2_4 / 106 | Ciężka Kontuzja [1] | — | t1_4 | Zwiększa czas trwania zamrożenia Sopla Lodu do 3 |
| t2_5 / 107 | Podtrzymywanie Lodu [2] | t1_5 | t1_5 | Atakowanie zamrożonych przeciwników ma 10%/20% szansy na skrócenie zamrożenia o 1 turę zamiast całkowitego usunięcia |
| t3_1 / 108 | Soplowa Tarcza Awaryjna [1] | — | — | Raz na 10 tur: Po otrzymaniu obrażeń równych 25% maksymalnego zdrowia w jednej turze, tworzy tarczę o sile równej 30% maksymalnego zdrowia. Podczas trwania tarczy, zwiększa obronę o 30%, po zniszczeniu tarczy, zmniejsza obronę o 30% na 3 tury |
| t3_2 / 109 | Wrażliwość na Bodźce [1] | t3_1 | t3_1 | Zmniejsza próg aktywacji Tarczy z Lodu do 15% |
| t3_3 / 110 | Śnieżny Duch [1] | t3_4 | t3_4 | Zwiększa czas trwania Mroźnej Osłony do 5 tur |
| t3_4 / 111 | Aura Mrozu [40 PM, 40% PA] | — | — | Zwiększa unik o 20 na 3 tury; 40% szansy na zmrożenie przyległych przeciwników na 1 turę |
| t3_5 / 112 | Lodowiec [1] | t2_5 | t2_5 | Zwiększa szansę na efekt Podtrzymywania Lodu do 30%. Efekt działa również dla sojuszników w zasięgu 3 od maga |
| t4_1 / 113 | Wytrzymała Tarcza [2] | t3_2 | t3_2 | Zwiększa obronę otrzymaną z Tarczy z Lodu do 40%/50% |
| t4_2 / 114 | Ochronny Lód [3] | — | t3_1 | Zmniejsza czas odnowienia Soplowej Tarczy Awaryjnej do 9/8/7 tur |
| t4_3 / 115 | Roztrzaskanie [2] | — | — | Ataki na zamrożonych celach mają 3%/6% na natychmiastowe zabicie. Działa dla bossów tylko poniżej 20% HP |
| t4_4 / 116 | Aura Lodu [3] | t3_4 | t3_4 | Aura Mrozu ma 20%/40%/60% szansy na zamrożenie przyległych przeciwników, którzy już są zmrożeni na 1 turę |
| t4_5 / 117 | Prawdziwy Lód [2] | t4_4 | t4_4 | Zwiększa czas trwania zamrożenia Aury Lodu do 2/3 tur |
| t5_1 / 118 | Niebiański Bunt [2] | — | — | Po śmierci przeciwnika, na jego miejscu pojawia na 4 tury się śnieżyca (kwadrat 3), która zadaje obrażenia o wartości 20%/30% ataku + 3%/6% brakującego zdrowia za każdą turę zakończoną w tym polu. |
| t5_2 / 119 | Wieczna Śnieżyca [1] | t5_1 | t5_1 | Poruszanie się przez śnieżyce Niebiańskiego buntu jest o 1 droższe, przeciwnicy wewnątrz są spowolnieni o 10 |
| t5_3 / 120 | Brutalne Roztrzaskanie [2] | t4_3 | t4_3 | Zwiększa szansę na Roztrzaskanie o 2%/4% za każdą turę czasu trwania zamrożenia (licząc 1) |
| t5_4 / 121 | Lodowisko [70 PM, 70% PA] | — | — | Zmniejsza celność przeciwników w obszarze działania o 30 i szybkość o 15 |
| t5_5 / 122 | Mroźna Adaptacja [2] | — | t5_4 | Zwiększa zasięg działania Lodowiska o 1/3 |

## Zatwierdzone decyzje

1. **ZATWIERDZONE:** Parzący Mróz: trzy rangi 135/145/160%; T3:3 „Trwała Aura” modyfikuje Aurę Mrozu; uzupełnienie czterech wymagań bazowych skilli.
2. Pasyw: własne krytyki przeciw zamrożonym, także magiczne; Hipotermia obejmująca każde leczenie zmrożonych/zamrożonych wrogów i reguła wielu magów.
3. Siła Mrozu i Odmrożenie: częstotliwość proców, brak DoT/reakcji, zamiana ogłuszenia z zachowaniem pozostałego czasu i zasad odporności.
4. Podtrzymywanie Lodu, Lodowiec i Roztrzaskanie: zachowanie bonusu obrażeń, próba per użycie/cel, moment progu bossa i pozostały czas zamrożenia.
5. Tarcza awaryjna: okno utraty HP, czas warstwy, rozbicie i kara, oba typy obrony oraz cooldown.
6. Aura Mrozu: zasięg rzucenia, pulsowanie i czas odbiorcy, brak podwójnej próby Chill→Freeze w jednym pulsem.
7. Niebiański Bunt i Lodowisko: brakujące HP celu, nakładanie pól, zasięg i wielkość obszaru oraz znaczenie Mroźnej Adaptacji.

Pakiet 1 oraz wszystkie pozostałe sześć pakietów zostały wyraźnie zatwierdzone przez użytkownika. Pełne zatwierdzone zasady zapisano poniżej.

## Istniejące kontrakty, które zachowujemy

- Zmrożenie (`chill`) ogranicza szybkość do maksymalnie 20; nie przyspiesza wolniejszej postaci. Zamrożenie (`freeze`) jest twardą kontrolą. Oba korzystają z odporności na kontrolę.
- Bazowe zamrożenie pęka po bezpośrednim trafieniu, dając +50% obrażeń za każdą pozostałą turę. DoT go nie przełamuje. Pasyw i nowe węzły wymagają wspólnego snapshotu zamrożenia sprzed kontaktu.
- Zamrożenie, ogłuszenie i inne twarde kontrole nadal korzystają ze wspólnej legalności akcji, tolerancji i lifecycle; nie powstaje drugi system statusów.
- Pole Osądu i przywołane obiekty już mają model obszaru, właściciela i czasu. Pola mrozu potrzebują wspólnego rozstrzygania zajętości, ruchu, ticków, śmierci i sprzątania.
- Warstwy tarczy mają źródło i czas. Wzmocnienie obrony musi należeć do konkretnej warstwy awaryjnej, a nie do sumy wszystkich tarcz.

## Kolejność implementacji po rozstrzygnięciach

1. Utrwalić odpowiedzi, manifest 25 węzłów i graf; ponownie sprawdzić wymagania skilli bazowych oraz progi SP.
2. Rozszerzyć `MageCombatSystem` o trzecią gałąź, wspólny snapshot zamrożenia i kontrolowane proce. Wspólne wykonanie i podgląd muszą widzieć te same wartości.
3. Dodać modyfikatory leczenia, tarczę awaryjną z własnym cooldownem i odróżnianiem rozbicia od końca walki.
4. Dodać Aurę Mrozu, pola śnieżycy i Lodowiska do istniejących kontraktów obszarów. Legalność akcji, koszty ruchu i AI korzystają z tych samych reguł.
5. Dane i powiązanie z klasą przez kontrolowany Unity API authoring w izolowanej kopii; zachować wszystkie stare węzły i kolejność katalogu. Bez ręcznego YAML/GUID/meta.
6. Opisy Aktualne/Następne, linki do właściwych talentów, wartości ze wspólnego formattera i pełne reguły wyłącznie w dodatkowych tooltipach.
7. 25 ikon w dotychczasowym workflow: oryginalna baza stylu Maga, B-v2 RAW, źródło ImageGen, Aseprite full-canvas nearest32, natywne pliki i QA. Nowa paleta pasywek Mrozu musi pozostać odrębna od kolorów aktywnych zaklęć.
8. Testy granic i kolejności statusów, multi-hit, odporności i bossów, warstw tarcz, pól/movement/AI, podglądów, resetu oraz starych zapisów Alison. Handoff READY_FOR_SMOKE dopiero po zakończonej weryfikacji.

## Znaczenie ikon

- Sopel Lodu: pocisk lodowego zaklęcia, bez stalowego grotu i drzewca strzały.
- Parzący Mróz: większa siła lodowego pocisku; bez sugerowania obrażeń ogniowych.
- Odmrożenie: zamiana ogłuszenia w zamrożenie; nie przedstawiać tego jako zdejmowania kontroli lub topienia lodu.
- Niebiański Bunt: śnieżyca wywołana śmiercią wroga; bez sugerowania zaklęcia świętego lub przywołania anioła.

Wszystkie decyzje zatwierdzone; implementacja i ikony w toku.

## Pełna akceptacja pakietów 2–7

Pasyw +1,5 pp/SP przeciw zamrożonym działa także dla własnych zaklęć; +0,5 pp odporności na kontrolę/SP. Hipotermia 35/75% każdego leczenia i regeneracji zmrożonych lub zamrożonych wrogów, niezależnie od źródła statusu, gdy mag żyje; najwyższa wartość, bez sumowania.

Jedna próba Siły Mrozu i Odmrożenia na trafiony cel i użycie bezpośredniego ataku, bez DoT i reakcji. Śnieżny Duch zamienia 50% udanych proców Siły Mrozu na Zmrożenie na 2 tury; bez fallbacku. Konwersja ogłuszenia obecnego przed kontaktem zachowuje pozostały czas, stosuje normalne zasady CC i usuwa ogłuszenie tylko po sukcesie.

Podtrzymywanie zachowuje bonus obrażeń zamrożenia, skracając je o turę zamiast usunięcia. Lodowiec daje 30% sojusznikom w zasięgu 3 bez sumowania. Roztrzaskanie: jedna próba na cel i użycie, zamrożenie sprzed trafienia, boss tylko poniżej 20% HP sprzed trafienia; Brutalne +2/4 pp za pozostałą turę. Fazy bossów zachowane.

Tarcza: faktyczna utrata HP także z DoT między początkami własnych tur, próg25/15%, warstwa30% maksHP, bez wygaśnięcia do rozbicia/końca walki. Premia obu obron30/40/50%; kara po rozbiciu tej warstwy −30% obu obron na3tury. Cooldown10/9/8/7 własnych tur od proca; bez ratowania przed śmiertelnym trafieniem.

Aura: zasięg6, sobie/sojusznikowi, podąża za odbiorcą; unik20 na3/5 jego tur. Puls przy nałożeniu i na początku kolejnych jego tur;40% Chill1. Już zmrożony przed pulsem cel zamiast tego dostaje próbę Freeze20/40/60%, na1/2/3tury. Bez Chill i Freeze tego samego celu jednym pulsem.

Śnieżyca: każda śmierć wroga, niezależnie od zabójcy;3×3,4tury maga. Tick na końcu tury wroga:20/30% średniego ataku maga +3/6% aktualnie brakującego HP tego wroga, magiczne. Nakładające się pola jednego maga bez mnożenia ticka. Lodowisko: środek do8/9/11, obszar3×3 na6tur maga. Kary i dodatkowy koszt ruchu tylko dla wrogów we właściwych obszarach.

## Integracja i układ

Stable ID odzwierciedlają slot źródłowy arkusza. W widoku Sopel Lodu i Ciężka
Kontuzja są w kolumnie 3, Ostry Sopel i Parzący Mróz w kolumnie 4. Pozwala to
połączyć bazę z ulepszeniami bez przechodzenia przez inną kartę; wymagania bez zmian.

Authoring przez ContentPass2FrostSetup w izolowanym Unity, z ochroną starych assetów
oraz kontrolą idempotencji. Efekty walki należą do BattleUnit; obszary i cooldown
tarczy nie przechodzą do zapisu postaci. Stare dwa drzewka, ich ID i starter
Alison pozostają dostępne. Nowe wartości EffectType dodano na końcu enuma.

25 osobnych ikon, źródła promptów i edytowalne pliki Aseprite:
Tools/Art/FrostIcons20261102. Obszar lodu korzysta z osobnego przezroczystego PNG.
Nie wprowadzono scen, prefabów ani ustawień projektu.

## Weryfikacja

1196 unikalnych testów PASS, w tym 64 testy UI/opisów/odwołań. Dwa dodatkowe testy danych bossów mają istniejące wcześniej rozjazdy: Ent oczekuje pustej puli pierwszego przedziału, a Pogromca Szkieletów ataku 50 zamiast bieżących 42,5. Testy i odczytywane assety są bajtowo identyczne z baseline sprzed CP2-11, zarówno w głównym projekcie, jak i snapshotcie. Nie zmieniano ich w tej karcie.

Authoring i promocja 45 plików PASS; wszystkie wcześniejsze assety chronione. Szczegóły: Tools/ContentPass2/frost-validation.json. Manualny smoke PASS; użytkownik zlecił commit/push.

## Poprawka układu tarczy

Wytrzymała Tarcza przeniesiona do kolumny 2, pod Wrażliwość na Bodźce; Ochronny Lód do kolumny 1, pod Soplową Tarczę Awaryjną. Usunięte skrzyżowanie bez zmiany wymagań, ID i efektów. Unity API: zmienione wyłącznie dwie wartości Column. Testy produkcyjnego drzewka i UI zakupu/wyposażania: 2/2 PASS.
