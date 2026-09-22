`KANBAN.md` jest głównym źródłem prawdy dla aktualnego stanu prac,
priorytetów i wyboru kolejnej karty. Starsze roadmapy mają charakter referencyjny.

# Godmist - Kanban

Ten plik jest naszym roboczym, tekstowym Kanbanem i punktem odniesienia do synchronizacji pracy.

Założenia:

- nie tworzymy na razie osobnego `Iceboxa`,
- kiedy uznam, że jakaś karta powinna zmienić kolumnę, dam Ci o tym znać,
- przy okazji będę też aktualizował ten plik, żeby nasz „wewnętrzny” Kanban był zgodny z tym, co masz na boardzie.

Aktualne kolumny:

- `Backlog`
- `In Progress`
- `Done`

Priorytety:

- `[P1]` - ważne teraz
- `[P2]` - ważne później
- `[P3]` - fajnie mieć, ale nie na pierwszy etap

## Labelki

- `Core / Architecture`
- `Combat`
- `World / Dungeon / Exploration`
- `Items / Inventory / Economy`
- `NPC / Quest / Dialogue`
- `UI / UX`
- `Tools / Editor / Debug`
- `Content / Data`
- `Design / Systems`
- `Bug`

## Backlog

### Milestone: Combat Passives and Affixes v1

Planowany po `Crafting, Upgrading and Consumables v1`. Wspolny model dla pasywek drzewek skilli, klejnotow i unikatowego equipmentu; rozwija warunkowe efekty walki bez mieszania ich z systemem craftingu.

- `[P1] Dodac unikatowe dropy bossow broni i pancerzy per klasa v1` - `Combat`, `Items / Inventory / Economy`, `Content / Data`
  - Boss ma `30%` szansy na dowolny unikat broni albo pancerza: po `7,5%` na kazda z czterech klas. Unikat losuje normalne czesci equipmentu, ale otrzymuje specjalna pasywke przypisana bossowi zamiast pasywy generycznej.

### Milestone: City Progression and Specialist Services v1

Planowany po `Combat Passives and Affixes v1`. Domyka miejski loop progresji oraz rozwija specjalistyczne uslugi NPC bez mieszania ich z kolejnym passsem walki lub lochow.

- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

### Milestone: Combat v4 — Arena, Obstacles and Transition v1

Branch: `milestone/combat-v4-arena-obstacles-transition-v1`. Acceptance: `COMBAT_V4_ACCEPTANCE.md`.

Planowany po `City Progression and Specialist Services v1`. Rozwija prezentację i taktykę areny walki oraz poprawia przejście eksploracja → walka, nie rozszerzając jeszcze zachowań przeciwników w lochu.

- Pułapki bojowe, zewnętrzne ściany areny i inne przeszkody taktyczne.
- Scrollowana kamera oraz czytelna prezentacja większych aren.
- Lepsza integracja wejścia z eksploracji do walki, w tym ustalone przenoszenie pozycji i formacji.
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

### Milestone: Dungeon Exploration v3 — Threats and Traversal v1

Planowany po `Combat v4 — Arena, Obstacles and Transition v1`. Dodaje aktywne zagrożenia i możliwości przemieszczania w lochu, wykorzystując ustalone zachowania walki i miasta.

- `Dungeon Selector v2`: przed wejściem gracz wybiera spośród kilku wylosowanych propozycji lochów. Każda propozycja ma niezależny seed i poziom losowany wokół średniego poziomu aktywnej drużyny; UI pokazuje theme oraz — gdy zostanie zaimplementowany — rozmiar, nigdy samego seedu.
- Fog of War oraz scouting - stan komórki całkiem nieodkrytej, odkrytej ale poza FOV, oraz odkrytej i w FOV
- Indywidualne rozmieszczenie przeciwników, FOV, pościg oraz zasadzki i niewidzialne encountery.
- Pathfinding sterowany myszką, skakanie oraz nowe typy pułapek eksploracyjnych.
- Prawdziwe zagadki logiczne i zręcznościowe z czytelnym stanem, persistence i konsekwencjami.
- Zaawansowane sterowanie drużyną: przypinanie/odpinanie, tworzenie grup, przełączanie poprzez klik na mapie oraz menu kontekstowe: przypnij/odepnij; "pan to"; inspect
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

## Done

- `[P1] [VS-06] Przygotować drużynę vertical slice’a` - `Content / Data`, `NPC / Quest / Dialogue`
  - Po VS-01/03: wybrane postacie z rolami, statystykami, zasobami, portretami i startowym wyposażeniem. Ustalić początkowy skład oraz dostępność/rekrutację przez istniejące mechanizmy.
  - Sprawdzić wybór lidera, grupy, inventory, rozwój i save/load dla każdej postaci. Nie wprowadzać nowego systemu relacji/rekrutacji bez potrzeby slice’a.
  - 2026-09-22: otwarta po zaakceptowanym smoke i commit/push VS-05 (`d2f4d1c4`). Skład, dostępność, wyposażenie L1 i start kampanii gotowe. Context pass: Statystyki Postaci.xlsx; istniejące CharacterDefinition i fabryka. Do uzupełnienia odporności, zasoby (Wladimir nadal 200 zamiast 50), bazowy kryt Alison/Reimonda oraz prezentacja; sprawdzić lidera/grupy/inventory/progresję/save-load. Startowe skille wdrażane w kartach klas VS-07–09.
  - 2026-09-22: użytkownik zatwierdził Alison 20/Reimond 15 MP/t i istniejące sprite’y do VS-22. DONE — smoke zaakceptowany przez użytkownika: dane i sprite’y skonfigurowane. 37/37 EditMode PASS; końcowy retest karty 10/10 PASS, Play Mode i kontrola obrazu PASS. Sprawdzone lider/grupy/ekwipunek/awans/save-load. Self-review zakończony. Odbiór w UNITY_HANDOFF.md, szczegóły VS06_PARTY.md. Commit/push autoryzowane.

- `[P1] [VS-05] Przygotować początek gry w Arungardzie` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`
  - Po katalogu i w integracji z VS-04/06: stan nowej kampanii, startowe zasoby/wyposażenie, przedstawiony cel i czytelne skierowanie do pierwszej wyprawy.
  - Bez debugowego rozdawania testowego contentu w ścieżce gracza. Uzgodnić krótkie wprowadzenie i dostępność usług; sprawdzić nową grę oraz zapis/wczytanie początku.
  - 2026-09-22: otwarta po commit/push VS-04 (`d7c8d8e6`). Context pass: ExplorationSandboxBootstrap tworzy roster z bindingów Arungardu (obecnie 2); CharacterInstanceFactory daje L1 i puste inventory; QuestContentBootstrap rejestruje zadania jako Unavailable. Usługi i quest strażnika istnieją. Zachować wcześniejsze ustalenie czterech postaci dostępnych od początku; docelowy authoring postaci należy do VS-06.
  - DONE — smoke zaakceptowany przez użytkownika (git): zatwierdzone 500 złota, zero mikstur, broń/pancerz L1 czterech klas według przekazanego JSON. Sprzęt startowy: sprzedaż 0, brak ulepszania i reforgowania. Pierwszy cel prowadzi do strażnika; marker dostępnego zadania. Wszystkie usługi od początku, galduryty od L11 dowolnej postaci (także rezerwy). Profil kampanii i konfiguracja Bootstrap/Arungardu gotowe; 104/104 EditMode oraz Play Mode PASS. Brak ponownego przydziału na wczytaniu/powrocie. Self-review zakończony; odbiór w UNITY_HANDOFF.md. Commit i push autoryzowane.

- `[P1] [VS-04] Wprowadzić produkcyjne menu i start gry` - `UI / UX`, `Core / Architecture`
  - DONE — 2026-09-22. Kod menu/ustawień, nowa kampania i kontynuacja gotowe; 39/39 testów PASS (vs04-menu-verified.xml), self-review zakończony. Konfiguracja Bootstrap wykonana za zgodą użytkownika: referencje, przyciski, panele, aktywny suwak i układ. 42/42 EditMode PASS (vs04-ui-verified.xml), automatyczny Play Mode smoke menu i Nowa gra → Arungard PASS; podglądy 1080p/720p sprawdzone. Odbiór użytkownika zatwierdzony poleceniem commit/push; ustawienia ekranu/Wyjdź nie były automatycznie testowane w buildzie.
  - Poprawka opóźnień: cache podsumowań zapisów, brak odczytu przy zamykaniu ustawień, wybór zapisu bez przebudowy listy. 44/44 EditMode i Play Mode smoke PASS; na 268 zapisach ponowny odczyt ~33 ms zamiast ~4,6 s, wybór ~1 ms. Pierwsze indeksowanie pozostaje synchroniczne.
  - Context pass po zamknięciu VS-03. Context pass: obecne BootstrapMenuView uruchamia sandboxy; zachować istniejący SaveManager/SceneLoader. Użytkownik zatwierdził pionowe menu po lewej, osobny panel zapisów, głośność/rozdzielczość/pełny ekran; nową kampanię bez usuwania starych, kontynuację najnowszego poprawnego zapisu i powrót do menu przy błędzie.
  - Nowa gra, kontynuacja, wczytanie, podstawowe ustawienia i wyjście. Produkcyjny przepływ bez „Launch Sandbox”; sandboxy mogą pozostać narzędziami developerskimi.
  - Przed implementacją uzgodnić layout, zakres ustawień, zachowanie przy istniejącym zapisie oraz błędach wczytania. Korzystać z istniejącego save/scene flow i UIW; statyczny UI w prefabach/scenach.
  - Smoke od uruchomienia gry, bez ręcznego otwierania sceny rozgrywki.

- `[P1] [VS-03] Przeprowadzić uzgodniony refaktor klas pod content slice’a` - `Core / Architecture`, `Combat`, `Skills / Progression`
  - DONE — smoke zaakceptowany i commit/push zlecone 2026-09-22. Wspólny resolver kosztów/parametrów, reakcje pasywne i OnHeal, walidacja, ochrona przed rekurencją i wielokrotnymi procami. Poprawki smoke: punktacja kosztu AI oraz wydajność wyszukiwania drogi na piętrach. Kontrakt: VS03_RUNTIME_CONTRACT.md.
  - Weryfikacja workspace: 155/155 głównej regresji, 70/70 po poprawce ataków, 63/63 po poprawce wysokości. Katalog assetów/drzewek pozostaje w kartach contentowych.


- `[P1] [VS-02] Przeprowadzić review gotowości projektu pod content pass` - `Core / Architecture`, `Tools / Editor / Debug`
  - DONE — raport zaakceptowany przez użytkownika 2026-09-22. [VS02_CONTENT_READINESS_REVIEW.md](VS02_CONTENT_READINESS_REVIEW.md): 10 znalezisk z dowodami i właścicielami, pokrycie 8 drzewek oraz minimalny zakres VS-03.
  - VS-03: wspólny resolver kosztów surowca, wymagane operacje reaktywne pasywów, parametry skilli i walidacja obsługi. Pozostałe braki przypisane do kart contentu; bez ogólnego refaktoru.
  - Weryfikacja statyczna i self-review zakończone; nie zmieniano kodu ani assetów. Build/EditMode/PlayMode NOT RUN. Zamknięcie review nie oznacza naprawienia opisanych braków.

- `[P1] [VS-01] Ustalić katalog contentu vertical slice’a` - `Design / Systems`, `Content / Data`
  - DONE — zaakceptowane przez użytkownika 2026-09-22. Katalog: [VS01_CONTENT_CATALOG.md](VS01_CONTENT_CATALOG.md). 20 startowych skilli, 160 węzłów, 7 mobów + boss, materiały/części T1/T2, consumables, biżuteria/galduryty, lootbagi, unikaty i zatwierdzone 16 szablonów Katakumb.
  - Odczyt 8 arkuszy, indeks istniejących assetów, kontrola liczebności i review dokumentu zakończone. Katalog określa zakres oraz zależności mechaniczne; authoring i runtime acceptance należą do kolejnych kart.

- `[P1] Combat v4 - acceptance, code review i UI pass` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`, `Tools / Editor / Debug`
  - Acceptance całego milestone'u: wiele kolejnych walk bez przeładowania sceny, wejście/wyjście, zwycięstwo/ucieczka/porażka, geometria, targeting, przemieszczenia, pułapki, AI, persistence i kamera.
  - Wykonać szerszy code quality review, regresję oraz osobny wizualny UI acceptance. Kontrolować podwójne subskrypcje/input, duplikację obiektów, utratę stanu i sprzątanie lifecycle.
  - Raport rozróżnia PASS/FAIL/NOT RUN. Milestone zamknąć dopiero po smoke użytkownika; commit/push wymagają osobnego polecenia.
  - DONE: smoke i zamknięcie zaakceptowane przez użytkownika. Pełny review milestone'u zakończony; poprawiono pułapki przeciwników przed walką, dopłaty honoru/overflow i komunikat ruchu. Build PASS; wcześniejsza regresja EditMode 805/805 PASS; wykonanie nowych testów po review NOT RUN (Unity otwarte). Raport: `COMBAT_V4_ACCEPTANCE.md`.

- `[P1] Przygotować content i scenariusze taktyczne Combat v4` - `Combat`, `Content / Data`, `Tools / Editor / Debug`
  - Przygotować grywalne układy: korytarz, sala z filarami, nieregularny pokój, duża arena oraz przejścia między przestrzeniami zgodnie z zatwierdzonym zakresem areny.
  - Włączyć przeszkody i pierwsze zatwierdzone pułapki do rzeczywistego contentu, nie tylko sandboxa. Zapewnić powtarzalne wejście do scenariuszy przez istniejące narzędzia debug/testowe.
  - Walidować przejścia, miejsca dla uczestników i brak niezamierzonych softlocków; liczby i efekty ustalać na podstawie danych projektu lub z użytkownikiem.
  - Zatwierdzone: tylko katakumby; pięć układów także w normalnej puli lochów, obecni przeciwnicy i balans. Implementacja: kotwy z opcjonalną konkretną definicją; wspólne szablony produkcyjne/debugowe. DONE: smoke użytkownika PASS; zaakceptowano drugi pass rozmieszczenia. Dalsze ręczne dopracowanie przy pierwszym content passie. Build PASS, regresja 55/55 PASS, testy contentu 9/9 PASS. Raport: `COMBAT_V4_CONTENT_VERIFICATION.md`.

- `[P1] Domknąć prezentację areny i informacje taktyczne v1` - `Combat`, `UI / UX`, `Content / Data`
  - Docelowy pixel-art pass highlightów: zachować zaakceptowaną czytelność pojedynczych kwadratów, granicy zasięgu, ścieżki i gradientu kosztów; dopasować kontury oraz poświatę do pixel artu. Obecny styl zaakceptowany jako rozwiązanie tymczasowe.
  - Dodać stały podgląd aktywnych statusów poza walką (efekt i pozostały czas/ticki), spójny z prezentacją bojową. Odłożone z karty pułapek za zgodą użytkownika; komunikaty floating nie zastępują podglądu. Przed implementacją uzgodnić układ UI.
  - Czytelne granice walki, ściany, przeszkody i stany pułapek; tooltipy, wskazanie blokady ruchu/celowania oraz feedback aktywacji.
  - Naprawić zgłoszoną regresję Combat InspectWindow: biały panel i bardzo jasny tekst powodują brak kontrastu. Zweryfikować faktyczną przyczynę w prefabie/stylowaniu.
  - W Combat v4 wykonać przegląd tego okna i dostosować do istniejących prefabów/stylów UIW: kontrast, hierarchia statystyk, czytelność pasków, spójność języka, odstępy oraz sekcje efektów/umiejętności. Przed zmianami przedstawić układ użytkownikowi; static UI w prefabie/scenie. Bez odkładania tej regresji do Combat v5.
  - Wspólne widoki lochu i walki, bez dublowania geometrii. Statyczne hierarchy, layout, styl i referencje zapisać w prefabach/scenach; kod aktualizuje dane i dynamiczne stany.
  - Przed nowym UI przedstawić układ użytkownikowi. Sprawdzić czytelność dużej areny, zasłanianie jednostek i długie teksty.
  - DONE: wdrożone statusy eksploracyjne, informacje taktyczne, Inspect i wspólny HUD UIW, podgląd AP, rail z liczbami, ikonowe akcje, siatka skilli 2×5 z przewijaniem, wskazanie kolejki i poprawka tooltipa.
  - Smoke użytkownika PASS (2026-09-14), zamknięcie zaakceptowane. Build PASS; ostatni wykonany zestaw EditMode 18/18 PASS. Test regresyjny ostatniej poprawki tooltipa skompilowany; wykonanie w Unity NOT RUN przy otwartym Editorze. Self-review PASS. Raport: `COMBAT_V4_PRESENTATION_VERIFICATION.md`.

- `[P1] Dodać sterowanie kamerą większych aren v1` - `Combat`, `UI / UX`
  - Rozwinąć obecne automatyczne kadrowanie o przesuwanie, zoom, ograniczenie kadru, skupienie na jednostce i powrót do czytelnego widoku.
  - Zachować ciągłość kamery przy przełączeniu trybu; bez wymuszonego ekranu przejścia. Uzgodnić gesty/klawisze i zasady automatycznego śledzenia.
  - Input kamery nie może kolidować z UI, wyborem celu ani ruchem; uwzględnić zasłanianie areny przez HUD i różne proporcje ekranu.
  - Zatwierdzone: MMB drag, WASD/strzałki, kółko zoom, F na jednostkę mającą turę, Home na arenę. Przesuwanie po całym piętrze; oglądanie innych walk nie zmienia aktywnej sesji. Automatyczne przesunięcie tylko przy rozpoczęciu tury poza widokiem, bez śledzenia kroków i bez zmiany zoomu. Kadr zapamiętywany osobno dla sesji. Zoom ma stały zakres 50–200% (100% = size 5), również Home respektuje granice 2.5–10; poprzednie testy limitów 12/12 PASS; zakres 50–200% zaakceptowany w smoke.
  - DONE: konfiguracja zapisana w DungeonSandbox; 58/58 EditMode PASS, build PASS, self-review PASS. Smoke użytkownika PASS. Raport: `COMBAT_V4_CAMERA_VERIFICATION.md`.

- `[P1] Zapewnić trwałość stanu obiektów areny v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Zachować tożsamość i stan drzwi, przeszkód oraz pułapek między eksploracją, walką, ucieczką i kolejnym encounterem. Rozszerzyć istniejący mechanizm persistence zamiast tworzyć niezależne kopie stanu.
  - Obsłużyć zapis/odczyt w ramach aktualnie wspieranych momentów zapisu i kompatybilność starszych save'ów. Nie rozszerzać automatycznie zakresu o zapis w środku walki.
  - Testy obejmują zużytą pułapkę, otwarte drzwi, stan przeciwników i brak resetu po powrocie do eksploracji.
  - Uwzględnić aktualizacje puli contentu: obecne odtwarzanie lochu regeneruje obiekty z seeda i bieżących definicji. Nowe sarkofagi/gruz/filary są na razie w osobnym demo; przełączenie produkcyjnej puli wymaga zachowania tożsamości i układu obiektów istniejących zapisów.
  - DONE: snapshot wygenerowanej geometrii i tożsamości obiektów, dotychczasowe delty stanu, zachowanie przygotowanych nagród. Starszy zapis odtwarza loch dotychczasowo i migruje przy następnym zapisie (zatwierdzone przez użytkownika). Automatyczne testy 82/82 PASS, build PASS, self-review PASS. Smoke użytkownika PASS. Raport: `COMBAT_V4_PERSISTENCE_VERIFICATION.md`.

- `[P1] Teren taktyczny i koszty przemieszczania v1` - `Combat`, `Content / Data`, `Design / Systems`
  - Combat v4, przed grywalnym contentem i acceptance: typ podłoża, dodatkowy koszt ruchu, czytelny podgląd AP i integracja z AI.
  - Zatwierdzone: trudny teren +1 AP za wejście na pole, sumowane z +2 AP murka. Nie zmienia licznika zwykłych kroków. Dash/charge/push/pull/teleport bez dopłaty AP podłoża.
  - Eksploracja: trudny teren 50% prędkości drużyny i przeciwników; niski murek — jednostajne przejście 0,6 s z blokadą sterowania ruchem. W eksploracji postacie nie rezerwują pól; obecność towarzysza nie blokuje przejścia przez murek ani natychmiastowego powrotu.
  - Ścieżka gracza: bezpieczeństwo → koszt AP → najmniej zakrętów; gdy brak bezpiecznej drogi, można wybrać nawet śmiertelną z ostrzeżeniem. AI: bezpieczeństwo → AP; odrzuca przewidywane powalenie.
  - Czytelność: odmienne wypełnienie trudnego terenu i kolor kosztownych odcinków preview; pułapki zachowują czerwone ostrzeżenie.
  - DONE — smoke użytkownika PASS: demo dungeon.catacombs.terrain_demo. Produkcyjny content bez zmian.
  - Unikalne przeszkody i efekty rozwijać wspólnie z istniejącymi kartami przeszkód/pułapek; nie dodawać w tej karcie rozbudowanego systemu reakcji środowiskowych.
  - Końcowy styl: delikatna siatka pojedynczych kwadratów, gradient żółty → brązowo-rudy, ścieżka nad pułapkami. Docelowy pixel-art pass odłożony do karty prezentacji Combat v4.
  - Testy: regresja 108/108 PASS, uzupełnienie terenu 8/8 PASS; końcowy retest terenu/seamless 57/57 PASS.

- `[P1] Dodać pułapki bojowe i integrację z ruchem v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Wykorzystać istniejący model pułapek eksploracyjnych tam, gdzie pasuje; dodać brakujące przeniesienie do runtime walki, stan aktywacji i wykonanie efektów.
  - Przed implementacją uzgodnić wyzwalanie przez ruch, przemieszczenia wymuszone i teleport, podatne strony, widoczność, jednorazowość/odnawianie oraz pierwsze efekty.
  - Preview i wykonanie nie mogą uruchamiać pułapki podwójnie; walidacja i podgląd nie mutują jej stanu. Wspierać przerwanie ruchu przez skutki pułapki.
  - Zatwierdzone: kolce (fizyczne + krwawienie), jednorazowy gaz na wskazanych polach (zatrucie), pieczęć grobowa (magiczne + osłabienie). Pule według motywu lochu; współdzielona mechanika i warianty wyglądu. Pierwszy content: katakumby.
  - Wyzwalanie: zwykły ruch/dash/charge/push/pull na przekroczonych polach, teleport wyłącznie na celu; obie strony. Jednorazowe grupy lokalne dla pokoju, bez trwałej chmury. Same obrażenia nie zatrzymują ruchu, utrata możliwości działania zatrzymuje.
  - Skalowanie poziomem lochu, standardowa obrona i odporności. AI preferuje bezpieczne obejście; przy braku bezpiecznej drogi wybiera najmniejsze zagrożenie i odrzuca przewidywane powalenie.
  - Zatwierdzone: poza walką tick statusów co 6 sekund aktywnej gry; w walce według tur jednostki, bez resetowania efektu przy zmianie trybu. Osłabienie: −20% zadawanych obrażeń przez 4 tury. Pułapki bez rzutu trafienia/uniku/krytyka, z normalną obroną i odpornościami. Minigra rozbrajania poza zakresem.
  - Wzrost obrażeń: 6,5% bazy × skumulowany wzrost schodkowy postaci.
  - Zatwierdzony balans poziomu 1: kolce 30 fizycznych + krwawienie 10 × 3; gaz zatrucie 12 × 5; pieczęć 40 magicznych + osłabienie na 4 tury.
  - DONE: smoke użytkownika PASS; testy 105/105 PASS, kompilacja poprawki feedbacku PASS; demo `dungeon.catacombs.traps_demo`, seed 4205. Produkcyjna pula pozostaje bez zmian do karty persistence.
  - Stały podgląd statusów eksploracji odłożony za zgodą użytkownika do karty prezentacji Combat v4.

- `[P1] Rozwinąć przeszkody taktyczne areny v1` - `Combat`, `Content / Data`, `Design / Systems`
  - Rozwinąć istniejące przeszkody z niezależnym blokowaniem ruchu i linii widzenia/efektu; nie tworzyć drugiego modelu tych samych właściwości.
  - Zweryfikować zwykły ruch, dash/charge, push/pull, teleport i kolizje według istniejących zasad. Dodać reprezentatywne warianty contentu oraz walidację danych.
  - Niszczenie obiektów nie jest jeszcze zatwierdzoną mechaniką: uzgodnić zakres przed dodaniem HP, obrażeń lub nagród obiektów.
  - Zatwierdzone: sarkofag i duży gruz blokują ruch, przepuszczają LOS; filar blokuje oba. Każdy obiekt zajmuje jedno pole. Bez niszczenia.
  - Status: DONE — 96/96 EditMode PASS, zapis prefabów/referencji i demo seed 4205 PASS, self-review PASS; smoke użytkownika PASS — funkcjonalność zaakceptowana.
  - Tymczasowe grafiki zaakceptowane; użytkownik zastąpi je docelowymi sprite’ami w przyszłości.
  - Uwzględnić aktualizacje puli contentu: obecne odtwarzanie lochu regeneruje obiekty z seeda i bieżących definicji. Nowe sarkofagi/gruz/filary są na razie w osobnym demo; przełączenie produkcyjnej puli wymaga zachowania tożsamości i układu obiektów istniejących zapisów.

- `[P1] Dodać niskie ściany i balustrady jako krawędzie pokoju v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Rozwinąć model rzeczywistych krawędzi pokoju: pełna ściana, niski murek, balustrada. Nie zastępować ich semantic anchors ani wolnostojącymi przeszkodami na polach.
  - Rozdzielić blokowanie ruchu i LOS/efektu na krawędziach; używać tych samych właściwości w eksploracji, walce i prezentacji. Balustrada blokuje ruch i przepuszcza strzały.
  - Zachować płaską logikę podłogi Combat v4. Dostępne balkony, schody i różne wysokości należą do szerszego Combat v5.
  - Weryfikacja: krawędź zewnętrzna i wewnętrzna, narożniki, pokój O/U, brak przejścia przez balustradę, zgodność widoku i LOS. Wykonać przed kartą przeszkód taktycznych.
  - Zatwierdzone: niski murek przepuszcza zwykły ruch za koszt kroku +2 AP (trzeci krok = 5 AP); eksploracja bez opłaty AP. Oba typy przepuszczają LOS i blokują dash/charge/push/pull; teleport zachowuje istniejące zasady.
  - Brak podłogi za krawędzią wyklucza przejście; wizualnie może oznaczać szyb, przepaść lub niedostępną niższą kondygnację. Generator nie zastępuje zadeklarowanych murków/balustrad drzwiami.
  - Status: DONE — 119/119 EditMode PASS, prefaby i referencje zapisane, walidacja i self-review PASS; smoke użytkownika PASS — zaakceptowany przez użytkownika.

- `[P1] Ustalić i wdrożyć pozycje oraz formację przy wejściu i wyjściu z walki v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - Seamless już przenosi pozycje eksploracyjne; rozstawianie na przeciwnych krawędziach pozostaje w starszej ścieżce/sandboxie. Domknąć istniejącego resolvera bez nowego szyku.
  - Uzgodnić snap do siatki, przypadki kolizji, brak miejsca, pozycje przeciwników i powrót po ucieczce. Rozwiązywanie konfliktów ma być deterministyczne i nie umieszczać jednostek w ścianach ani na zajętych polach.
  - Zachować pozycje po zwycięstwie i obsłużyć kolejne walki w tej samej scenie bez duplikowania jednostek.
  - Zatwierdzone: snap do środka zajmowanego pola, jednakowo dla obu stron; bez ustawiania nowej formacji. Zachować końcowe pozycje po zwycięstwie i miejsce przekroczenia granicy przy wyjściu.
  - Rezerwować najpierw wszystkie poprawne pozycje, potem rozwiązywać konflikty deterministycznie na najbliższym wolnym polu dostępnym bez przekraczania ścian/zamkniętych drzwi.
  - Brak miejsca to zabezpieczenie błędnego układu: atomowo odrzucić start bez zmiany pozycji; przy dołączeniu pozostawić postać przed wejściem i utrzymać walkę. Komunikat dla gracza.
  - Status: DONE — 47/47 EditMode PASS; prefab komunikatu UIW zapisany i podpięty, weryfikacja zapisu i self-review PASS. Manualny smoke PASS — zaakceptowany przez użytkownika.
  - Combat v4: naprawić regresję InspectWindow (biały panel i jasny tekst), następnie przegląd kontrastu, hierarchii, pasków, języka i integracji UIW w karcie prezentacji. Przed zmianą przedstawić układ użytkownikowi; static UI w prefabach/scenie.

- `[P1] Combat v4 — nawigacja przeciwników wokół przeszkód i obsługa drzwi v1` - `Combat`, `Core / Architecture`
  - Karta zatwierdzona przez użytkownika; wykonać po domknięciu granic i geometrii areny.
  - Rozwinąć istniejące AI i mechanizmy ruchu: planować pełną trasę ponad budżet jednej tury, a wykonywać odcinek za dostępne AP. Obsłużyć obejście wymagające chwilowego oddalenia się od celu.
  - Uwzględnić otwieranie dozwolonych drzwi przez istniejący system interakcji oraz koszt AP. Respektować blokady i zatwierdzone granice sesji; nie dodawać pościgu ani pathfindingu eksploracyjnego.
  - Przeliczać trasę po zmianie przeszkód, stanu drzwi lub zajętości pól. Brak dostępnej drogi nie może powodować zapętlenia tury ani powtarzania bezskutecznych akcji.
  - Weryfikacja: obejście ściany w wielu turach, początkowe oddalenie od celu, wąskie przejście, zajęte pola, drzwi otwarte/zamknięte/niedostępne oraz cel nieosiągalny. Sprawdzić rzeczywistą ścieżkę, koszty AP i poprawne zakończenie tury.
  - DONE — implementacja i self-review zakończone; 65/65 EditMode PASS, smoke użytkownika PASS. Commit i push zatwierdzone.

- `[P1] Domknąć granice i geometrię areny w lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Wykorzystać rzeczywisty footprint lochu, ściany zewnętrzne i wewnętrzne, drzwi i przejścia. Zapewnić spójność widocznych granic z ruchem, celowaniem i linią efektu.
  - Zatwierdzone: arena obejmuje jeden pokój i nie rozszerza się; indywidualne wejście/wyjście przez granicę, istniejące równoległe sesje. Każde opuszczenie pokoju, także przejściem bez drzwi, jest wyjściem. Linia strzału może przechodzić poza pokojem; blokuje ją rzeczywista geometria i właściwości obiektów.
  - Acceptance obejmuje wąski korytarz, nieregularny pokój, filar i przejście między przestrzeniami.
  - DONE — smoke użytkownika PASS: kontekst zbiera także ściany graniczne; LOS sprawdza ściany oraz żywy stan geometrii poza areną bez dodawania tam pól walki. Wyjście respektuje również przeszkody ruchowe za progiem.
  - PASS: 56/56 EditMode (seamless, LOS, grid, integracja, AI). PASS: manualny smoke scenariuszy geometrii zaakceptowany przez użytkownika. Bez zmian serialized state i commit/push zatwierdzone przez użytkownika.

- `[P1] Wdrożyć reperkusje porażki i odzyskiwanie towarzyszy v1` - `Combat`, `Core / Architecture`, `NPC / Quest / Dialogue`
  - Status: DONE — smoke funkcjonalny i UI zaakceptowane przez użytkownika.
  - Porażka kampanii po śmierci głównej postaci albo braku żywych aktywnych członków: najnowszy poprawny zapis tej kampanii / inny zapis / menu. Brak zapisów blokuje oba przyciski wczytywania. Żywy członek poza pokojem nadal może ratować powalonych.
  - Lokalna przegrana pokoju zachowuje stan lochu; martwi towarzysze trafiają do odzyskiwania u kapłana. StoryLocked pozostaje zablokowany.
  - Odzyskanie: ceil(300 * 4^((level-1)/10)), pełne HP i zasoby, zachowanie tożsamości/ekwipunku/progresji, aktywny slot lub rezerwa. Tylko honor rozmówcy; bez lojalności kapłana.
  - Nowe złoto przeciwnika: baza ceil(10 * 4^((dungeonLevel-1)/10)), niezależny równomierny rozrzut 50–150% na przeciwnika; wcześniej wylosowane nagrody w zapisach pozostają bez zmian.
  - Dziewięć zatwierdzonych tierów honoru bez ograniczenia do −100…100. Progi i zniżki w Game Defines; zniżki domyślnie 0% do balansu, tier −75…−50: Niechlubny.
  - UI zapisane w scenie/prefabach za zgodą użytkownika. Regresja EditMode 89/89 PASS. Manualny smoke i wizualny UI pass: PASS użytkownika. Końcowe testy odzyskiwania 43/43 PASS, konsoli 51/51 PASS; kolejne poprawki UI skompilowane i zweryfikowane.

- `[P1] Zintegrować walkę seamless w scenie lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture` - `DONE`
  - Zastąpić produkcyjne przełączanie scen eksploracja/walka przełączaniem trybu na istniejącej mapie i widokach jednostek. Nie budować drugiej kopii mapy ani równolegle aktywnych kontrolerów tych samych postaci.
  - Rozdzielić lifecycle eksploracji i walki: zatrzymanie eksploracyjnego sterowania, start tur, podpięcie HUD/input, zakończenie walki i przywrócenie eksploracji. Uwzględnić zwycięstwo, ucieczkę, porażkę i nieudany start bez pozostawienia częściowego stanu.
  - Zachować sandbox/debug walki oraz istniejące systemy combat; nie przepisywać core combat bez potrzeby. Przed implementacją uzgodnić konieczne zasady pozycji i granic, od których zależy integracja.
  - Zatwierdzone rozszerzenie: wiele równoległych sesji przypisanych do pokojów, indywidualne uczestnictwo i przełączanie sterowania między walką a eksploracją. Osoby poza pokojem dołączają po wejściu; wyjście za próg za zwykły koszt ruchu zastępuje losową ucieczkę i może rozdzielić drużynę.
  - Snap do środka komórki; deterministyczny wybór dostępnego pola bez przekraczania ścian/zamkniętych drzwi; nieudany start nie pozostawia aktywnej sesji.
  - Dołączenie czeka pełny odstęp Action Value według szybkości (bez wprowadzania wspólnych rund). Ponowne wejście nie odnawia zasobów ani nie skraca oczekiwania.
  - Powaleni nie kończą automatycznie walki, gdy żywy członek drużyny poza pokojem może przyjść na ratunek. Reperkusje porażki są osobną następną kartą.
  - Smoke użytkownika zaakceptowany. Regresja 71/71 PASS; końcowy zestaw po poprawkach widoczności i kotwiczenia napisów 22/22 PASS. Karta zamknięta na polecenie użytkownika.

### Karty do decyzji o przypisaniu do milestone'u

- `[Do decyzji] Rozdzielić warianty modyfikatorów danych contentu v1` - `Core / Architecture`, `Content / Data`, `Tools / Editor / Debug`
  - Zastąpić wspólne modele serializowane, które przechowują nieużywane pola zależne od typu (np. jednocześnie statystykę, odporność i zmienną rozgrywki), rozłącznymi wariantami danych albo wrapperem z dedykowanym payloadem dla każdego wariantu. Inspector ma ujawniać wyłącznie pola aktywnego wariantu, walidacja odrzucać niepełne dane, a runtime konsumować typowany payload zamiast ignorować „martwe” wartości. Obecny model `JewelryModifierData` może pozostać rozwiązaniem przejściowym do czasu refaktoru.
  - To karta o ogólnym design patternie dla wariantowych danych contentu; modyfikatory biżuterii są tylko pierwszym konkretnym przykładem tego problemu.
  - Przypisanie do milestone'u oraz priorytet do ustalenia.
- `[Do decyzji] Dodac indywidualne rozmieszczenie przeciwnikow, FOV i poscig w lochu v2` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`, `Design / Systems`
  - Przeciwnicy sa rozmieszczani indywidualnie albo wedlug autorskiego ukladu pokoju; wykrycie wrogiej postaci w FOV rozpoczyna walke zgodnie z Combat v5; poscig odbywa sie juz w ramach starcia. Bez jednego wspolnego anchoru dla calej grupy.
  - Przypisanie do milestone'u oraz priorytet do ustalenia.
- `[Do decyzji] Dodac pathfinding ruchu sterowanego myszka w eksploracji lochu v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`
  - Klikniecie walkable celu wyznacza sciezke po mapie lochu i prowadzi druzyne po kolejnych komorkach z uwzglednieniem przeszkod, scian i zamknietych przejsc.
  - Przypisanie do milestone'u oraz priorytet do ustalenia; istnieje starsza karta o klikanym ruchu po gridzie, ktora nalezy traktowac jako jej poprzednia wersje.
- `[Do decyzji] Rozszerzyc blokowanie layoutu pokoi z bounding boxa na rzeczywisty footprint v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
  - Generator ma dopuszczac lepsze upakowanie nieregularnych pokoi, blokujac rzeczywiste zajete komorki zamiast calego prostokatnego bounding boxa; uwzglednic dziury, sasiedztwo pokoi i kompatybilne przejscia.
  - Karta odlozona poza pierwszy pass nieregularnych footprintow; przypisanie do milestone'u oraz priorytet do ustalenia.





- `[P1] Niszczenie przeszkód i obsługa obrażeń obiektów v1` - `Combat`, `Core / Architecture`, `Content / Data`
  - Osobna karta Combat v5: serwis obsługi niszczenia, integracja targetowania i obrażeń obiektów oraz trwałość skutków.
  - Przed implementacją uzgodnić podatne obiekty, HP, ataki/efekty, nagrody i stan po zniszczeniu. Nie dodawać mechaniki w karcie statycznych przeszkód Combat v4.

Docelowy rozwój po Combat v4, powiązany z eksploracją lochu. Walka seamless nadal korzysta z tych samych obiektów i sceny. V4 zachowuje ograniczenie do pokoju jako etap przejściowy; nie rozwijać w nim dodatkowych mechanik silnie zależnych od granicy pokoju, które v5 zastąpi.

- `[P1] Wdrożyć FOV i wykrywanie wrogich jednostek v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - Wykrycie wrogiej jednostki w FOV rozpoczyna walkę albo powoduje dołączenie obserwatora do istniejącego starcia. Dotyczy dowolnej wrogiej postaci, niezależnie od aktywnego lidera.
  - Zastąpić model „pościg za liderem, start walki po dogonieniu”. Nie czekać na dojście przeciwnika do wykrytej jednostki.
  - Przed implementacją ustalić zakres/kąt FOV, przesłanianie, częstotliwość wykrywania i zasady współdzielenia informacji. Wykorzystać istniejący LOS oraz właściwości geometrii.
- `[P1] Rozszerzyć przestrzeń walki na cały loch i dynamiczne uczestnictwo v1` - `Combat`, `Core / Architecture`
  - Usunąć granicę pokoju jako granicę starcia. „Cały loch” oznacza przestrzeń możliwego starcia, nie automatyczne dołączenie wszystkich przeciwników.
  - Uczestnictwo wynika z wykrywania; kolejne jednostki dołączają po uzyskaniu kontaktu. Zachować oddzielenie runtime od prezentacji i pojedynczą reprezentację jednostek.
  - Uzgodnić łączenie/rozdzielanie równoległych sesji, kolejkę tur oraz upływ czasu dla postaci poza walką. Doprecyzować zakres względem osobnych pięter lochu.
- `[P1] Przebudować pościg, utratę kontaktu i ucieczkę v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - Pościg jest zachowaniem uczestnika rozpoczętego starcia; cel nie jest ograniczony do lidera drużyny.
  - Kierunek docelowy: postać może opuścić walkę, gdy wszyscy przeciwnicy stracą z nią kontakt wzrokowy, zamiast przekraczać granicę pokoju.
  - Do ustalenia przed implementacją: natychmiastowe wyjście czy okres utraty kontaktu/poszukiwań, pamięć ostatniej pozycji, zmiana celu oraz ponowne wykrycie. Nie uznawać pamięci/poszukiwania ani konkretnego czasu ucieczki za zatwierdzone.
  - Sprawdzić rozdzieloną drużynę, chowanie się za filarem, brak wielokrotnego resetowania walki i zasobów przez krótką utratę widoczności.
- `[P1] Zintegrować Fog of War i pamięć odkrytej mapy z FOV v1` - `World / Dungeon / Exploration`, `UI / UX`
  - Rozdzielić wykrywanie przez jednostki od prezentacji wiedzy gracza. Stany mapy: nieodkryta, odkryta poza aktualnym FOV, aktualnie widoczna.
  - Uzgodnić wspólną/indywidualną wiedzę drużyny i prezentację niewidocznych jednostek; nie ujawniać ich przez HUD ani feedback.
- `[P1] Zintegrować wysokości, schody i balkony z modelem lochu i walki` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Element szerszego Combat v5, bez osobnego milestone'u „Wertykalności lochów”.
  - Ustalić model różnych wysokości podłogi w pokoju oraz przejść po schodach; osobno zdecydować, czy wspieramy chodzenie nad i pod balkonem przy tym samym rzucie pola.
  - Uwzględnić pozycje, połączenia pól, ruch/AI, FOV/LOS, zasięgi, kamerę, authoring i save compatibility. Konkretne reguły przewagi wysokości, upadków i skoków nie są zatwierdzone.
  - Rozstrzygnąć model przed dużym content passem, aby nie przebudowywać później gotowych pokojów.
- `[P1] Combat v5 — acceptance zintegrowanego modelu` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`
  - Wykrycie dowolnego członka drużyny, dołączanie posiłków, starcia przekraczające pokoje, rozdzielona drużyna, utrata/odzyskanie kontaktu, równoległe sesje oraz zatwierdzona geometria wysokości.
  - Regresja persistence, brak duplikacji jednostek i subskrypcji; osobny UI pass, raport PASS/FAIL/NOT RUN i smoke użytkownika.

## In Progress

- `[P1] City Progression and Specialist Services v1 - UI acceptance pass` - `UI / UX`, `Bug` - `DONE`
  - Poprawki na podstawie dziewieciu screenow: podglady, materialy alchemii, lokalizacja, stany puste i zaznaczenia, prog gniazda przy 20% ulepszenia.
  - Statyczny layout zapisany w prefabie; bez runtime builderow. Smoke zaakceptowany przez uzytkownika; 53/53 testy PASS i koncowy retest prefabu 7/7 PASS.

- `[P1] City Progression and Specialist Services v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Combat`, `UI / UX`, `Tools / Editor / Debug` - `DONE`
  - Domknieto acceptance i poprawki po code review: ksiegowanie oplat, buyback, walidacja wlasnosci, bonus trafienia, zachowanie postepu ulepszania i statyczne UI w prefabie.
  - Testy regresyjne 189/189 PASS; koncowy retest 29/29 PASS. Smoke zaakceptowany przez uzytkownika. Raport: CITY_SERVICES_ACCEPTANCE.md.

- `[P1] Dodac trwala lojalnosc NPC i postep wydatkow v1` - `NPC / Quest / Dialogue`, `Core / Architecture` - `DONE`
  - Dodac osobny, zapisywalny stan lojalnosci oraz skumulowanych wydatkow dla kazdego NPC. Zdefiniowac dane progresji i przygotowac je pod pozniejsze progi odblokowan oraz rabatow.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.
- `[P1] Dodac progi lojalnosci, odblokowania uslug i rabaty v1` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX` - `DONE`
  - Dodac data-driven progi lojalnosci, odblokowywanie uslug i rabaty. UI ma pokazywac dostepnosc uslugi oraz czytelny powod blokady.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.
- `[P1] Dodac ekonomie NPC i wyplaty gildii v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Core / Architecture` - `DONE`
  - Dodac osobny, trwaly portfel golda dla kazdego NPC bez maksymalnego limitu. NPC otrzymuje skonfigurowana wyplate po okreslonej liczbie w pelni ukonczonych lochow; kwota skaluje sie z poziomem kampanii (najwyzszym poziomem aktywnej druzyny). Zakupy zwiekszaja portfel NPC, sprzedaz przy niewystarczajacym saldzie wymaga potwierdzenia i wyplaca saldo NPC, a saldo i postep do wyplaty sa zapisywane. UI handlu pokazuje saldo i powod odrzucenia transakcji.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.

- `[P2] Dodac wyciaganie galdurytow/run z broni i pancerzy v1` - `Items / Inventory / Economy`, `Combat`, `Core / Architecture` - `DONE`
  - Osobna zakladka Zaklinacza. Szansa 70%, od lojalnosci 6: 80%. Kazda proba kosztuje 120% wartosci galdurytu; zniszczony kosztuje 180%, bez kumulacji doplat. Niepowodzenie pozostawia zniszczony galduryt bez efektow w sockecie. Powodzenie zwraca sprawny galduryt do wlasciciela/magazynu albo calkowicie usuwa zniszczony. Stan zapisuje sie w save/load.
  - Poprawiony layout osadzania i wyciagania: zawijane ostrzezenie, osobna szansa przy koszcie, wysokosc tytulu zaleznа od tekstu i brak miejsca rezerwowanego przez pusty status. Testy EditMode przeszly; smoke zaakceptowany przez uzytkownika 2026-09-13.

- `[P2] Dodac socketowanie galdurytow/run w bronie i pancerze v1` - `Items / Inventory / Economy`, `Combat`, `Core / Architecture` - `DONE`
  - Osobna zakladka Zaklinacza: wybor wyposazenia i galdurytu, lista zajetych i wolnych socketow, koszt i osadzanie. Walidacja celu, poziomu, rzadkosci, identyfikacji i pojemnosci; koszt konfigurowany jako procent wartosci galdurytu; efekty oraz save/load osadzonych instancji.
  - Poprawione prefaby, interakcje, tooltipy wyposazenia i autouzupelnianie nazwanych argumentow konsoli. Smoke zaakceptowany przez uzytkownika; kompilacja przeszla. Dodane testy konsoli wymagaja uruchomienia EditMode. Mieszanie tierow czesci pozostaje do przyszlej decyzji.

### Milestone: City Progression and Specialist Services v1

- `[P2] Dodac crafting bizuterii v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX` - `DONE`
  - Osobny model craftingu biżuterii oparty o metal + klejnot, bez automatycznego modelu trzech części equipmentu i bez socketów na galduryty. Ujednolicono preview, nazewnictwo, tooltipy, materiały oraz save/load.
- `[P2] Dodac crafting i identyfikowanie galdurytow/run v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data` - `DONE`
  - Galduryty/runy jako wspólny system: Enchanter tworzy i identyfikuje galduryty, a niezidentyfikowany i zidentyfikowany stan jest zapisywany. Dodano materiały, tiering, losowanie efektów, tooltipy oraz komendę debugową.

- `[P1] Dodac role Alchemika, crafting i napełnianie potionow v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data` - `DONE`
  - Data-driven crafting potionow i bomb, materialy Alchemika, alkohole/prochy/katalizator, crafting materialow i napełnianie consumables.
  - Panel zostal ujednolicony z UI Kowala; tooltipy, sloty materialow i refille przechodza przez wspolne reguly UI Workspace.
  - Zweryfikowano testy EditMode dla craftingu i napełniania.
- `[P1] Dodac Kapłana z darmowym leczeniem drużyny v1` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX` - `DONE`
  - Dodano role i definicję Kapłana oraz dialog z opcją darmowego pełnego leczenia aktywnej drużyny.
  - Leczenie jest jawną akcją dialogową `HealActiveParty`; nie tworzy osobnej zakładki usług i nie wskrzesza powalonych postaci.
  - Zweryfikowano wybór dialogowy testem EditMode.

### Milestone: Combat Passives and Affixes v1

- `[P1] Combat Passives and Affixes v1 - acceptance pass` - `Combat`, `Items / Inventory / Economy`, `Skills / Progression`, `Tools / Editor / Debug` - `DONE`
  - Zweryfikowac warunki, stackowanie, tooltipy, save/load oraz wspoldzialanie pasywek z drzewek, bizuterii i unikatowego equipmentu.

- `[P1] Dodac pierwszy pass wykonawczy pasywek node'ow drzewek skilli v1` - `Combat`, `Skills / Progression`, `Content / Data` - `READY_FOR_SMOKE`
  - Podpiac node'y Scouta do wspolnego wykonawczego frameworku: materializacja rank runtime, warunki i triggery combatowe, modyfikatory obrazen i szansy krytycznej, zamiana chybienia w trafienie oraz aplikowanie statusu. Dodac czytelne metadane warunku i aktywacji w tooltipie node'a.
- `[P1] Dodac tagi przeciwnikow i warunkowe modyfikatory v1` - `Combat`, `Content / Data`, `Core / Architecture` - `READY_FOR_SMOKE`
  - Dodac tagi `Human`, `Beast`, `Undead` i `Demon` na definicjach przeciwnikow oraz warunki pasywek dla typu celu i theme lochu. Uzyc ich m.in. dla obrazen przeciwko okreslonym przeciwnikom i bonusow terenowych klejnotow.
- `[P1] Zbudowac rdzen wspolnego frameworku pasywek v1` - `Combat`, `Content / Data`, `Core / Architecture`, `UI / UX`
  - Zdefiniowac wspolny model static definition i runtime state dla pasywek drzewek skilli, equipmentu oraz przyszlych klejnotow: zrodlo, warunek, trigger, target, efekt, stacking, lifetime, tooltip i persistence. Zapewnic rozszerzalne kontrakty dla zlozonych warunkow i efektow bez dodawania nowego contentu combatowego. Zaimplementowano; kompilacja i pelny EditMode suite przechodza.
- `[P1] Zmigrowac dzialajace pasywki do wspolnego frameworku v1` - `Combat`, `Items / Inventory / Economy`, `Skills / Progression`, `Core / Architecture`
  - Przeniesc do wspolnego modelu istniejace dzialajace globalne pasywki drzewek, modyfikatory statystyk i odpornosci equipmentu, pasywki bizuterii dotyczace XP, zlota i theme lochu oraz obecne runtime modifiers. Zachowac dotychczasowe zachowanie, kompatybilnosc runtime item instances i save/load; nie dodawac nowych pasywek contentowych.
- `[P1] Dodac prymitywy ewaluacji combatowej dla pasywek v1` - `Combat`, `Core / Architecture`, `Skills / Progression`, `Items / Inventory / Economy` - `DONE`
  - Dodac wspolne konteksty i punkty integracji dla przyszlych warunkow oraz modyfikatorow combatowych: typ ataku, pozycja, dystans, status celu, ruch, zasoby, unik, kolejnosc aplikacji, stacking i liczniki. Pokryc je testowymi definicjami i testami integracyjnymi, ale nie aktywowac nowych pasywek Scouta, unikatow bossowych ani innych nowych zachowan contentowych. Zweryfikowano recznie w Encounter i Debug; pelny EditMode suite przechodzi.

### Milestone: Developer Workflow and Content Authoring v1

- `[P1] Rozszerzyć nawigację rekomendacji konsoli v1` - `Tools / Editor / Debug`, `UI / UX`
- `[P1] Dodać schematy komend i opcjonalne argumenty konsoli v1` - `Tools / Editor / Debug`, `Core / Architecture`, `UI / UX`
- `[P1] Rozszerzyć zestaw komend content/debug v1` - `Tools / Editor / Debug`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`
- `[P1] Dodać Content Authoring Window dla itemów i equipmentu v1` - `Tools / Editor / Debug`, `Content / Data`, `Items / Inventory / Economy`
- `[P1] Dodać import-preview danych z arkuszy contentowych v1` - `Tools / Editor / Debug`, `Content / Data`
- `[P1] Dodać walidator contentu i raport referencji v1` - `Tools / Editor / Debug`, `Content / Data`
- `[P1] Dodać Game Defines Window v1` - `Tools / Editor / Debug`, `Core / Architecture`, `Design / Systems`, `Content / Data`
- `[P1] Developer Workflow and Content Authoring v1 — acceptance pass` - `Tools / Editor / Debug`, `Content / Data`, `UI / UX`

### Milestone: Crafting, Upgrading and Consumables v1

- `[P1] Crafting, Upgrading and Consumables v1 - acceptance pass` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Combat`, `Tools / Editor / Debug`
- `[P1] Dodac ulepszanie quality i rarity equipmentu u kowala v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Design / Systems`, `UI / UX`
- `[P1] Dodac consumables: potiony, bomby i return scroll v1` - `Items / Inventory / Economy`, `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P2] Przenieść handel NPC do UI Workspace Services v1` - `NPC / Quest / Dialogue`, `UI / UX`, `Items / Inventory / Economy`
- `[P1] Dodac storage w Arungardzie v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `UI / UX`, `Core / Architecture`
- `[P2] Dodac crafting półproduktów materiałów v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`, `Core / Architecture`
- `[P1] Dodac crafting broni i pancerzy z czesci oraz materialow v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`

### Milestone: Dungeon Content and Layout v2

- `[P1] Dungeon Content and Layout v2 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
- `[P1] Dodac przejscia miedzy pietrami lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
- `[P1] Zachowac stan czesciowo pokonanego encountera po ucieczce v1` - `Bug`, `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Dodac testowy content bizuterii do lootbagow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
- `[P1] Dodac UI skrzynek oraz zamki, klucze i lockpicking v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Content / Data`
- `[P1] Dodac boss encountery i boss lootbagi v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac lootbagi oraz theme i level-bracket loot tables v1` - `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac anchory pojedynczych przeciwnikow w pokojach v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`
- `[P1] Zablokowac interakcje z obiektami przez sciane v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`
- `[P1] Dodac rotacje instancji pokoi v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac petle grafu lochu i wymagane odnogi progresji v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
- `[P2] Dodac DungeonRoomEditor v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dopracowac persistence cial przeciwnikow: pozycja smierci v2` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac semantic content anchors i recznie projektowane sytuacje pokoi v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
- `[P1] Dodac nieregularne ksztalty pokoi lochu v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
- `[P1] Dodac pulapki, przeszkody, klucze/dzwignie i proste zagadki lochowe v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`, `UI / UX`
- `[P2] Rozszerzyc DungeonRoomEditor o legalne krawedzie przejsc i resize footprintu v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`

### Milestone: Save and Load v1

- `[P1] Dodac GameSaveData DTO i JSON serialization v1` - `Core / Architecture`
- `[P1] Dodac snapshoty runtime dla postaci, inventory i itemow v1` - `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Zapisywac questy, world flags oraz stan NPC v1` - `Core / Architecture`, `NPC / Quest / Dialogue`
- `[P1] Dodac GameContentCatalog v1` - `Core / Architecture`, `Content / Data`
- `[P1] Dodac API restore dla Quest i Dungeon runtime v1` - `Core / Architecture`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac GameSession restore factory v1` - `Core / Architecture`, `Content / Data`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac SaveManager oraz manual save/load v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zapisywac party, inventory, quest/world flags i dungeon deltas v1` - `Core / Architecture`, `World / Dungeon / Exploration`
- `[P2] Dodac autosave po wejsciu/wyjsciu z lochu i po ukonczeniu questa v1` - `Core / Architecture`
- `[P1] Dodac CampaignId i strukture katalogow zapisow v1` - `Core / Architecture`
- `[P1] Dodac liste oraz zarzadzanie zapisami kampanii v1` - `Core / Architecture`, `UI / UX`
- `[P1] Dodac menu pauzy i UI zapisow v1` - `UI / UX`, `Core / Architecture`
- `[P1] Save and Load v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`

### Milestone: Combat Core v2 — Closed

- `[P1] Combat Core v2 - acceptance pass` - `Combat`, `Tools / Editor / Debug`, `UI / UX`

### Milestone: Combat v3 — Closed

- `[P1] Combat v3 - acceptance pass` - `Combat`, `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P2] Zrefaktoryzowac payloady PassiveEffectRuntime v1` - `Core / Architecture`, `Combat`, `Design / Systems`

- `[P1] Combat v3 - tooltipy, inspect i feedback bojowy v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Combat v3 - penetracja oraz DealDamage z penetracja` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc targetowanie` - `Combat`, `Core / Architecture`, `Content / Data`

- `[P1] Combat v3 - rzeczywista sciezka ruchu AI i preview kosztu v1` - `Combat`, `UI / UX`

- `[P1] Combat v3 - czytelnosc feedbacku i sekwencjonowanie akcji v1` - `UI / UX`, `Combat`

- `[P1] Combat v3 - dzialalnosc skilli toggle` - `Combat`, `Core / Architecture`, `UI / UX`, `Content / Data`
- `[P1] Combat v3 - mechanika sztyletow Scouta` - `Combat`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc execution pipeline skilli` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

### Milestone: UI Workspace v1 — Closed

- `[P2] Dodac glossary i linki do UI Workspace tooltip v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P2] Ujednolicic stack count i zloto w slotach UI Workspace v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] UI Workspace v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Podpiac generowany equipment do LootTable i tooltipow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
- `[P1] Dodac floating loot window ciala v1` - `UI / UX`, `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Core / Architecture`

- `[P1] Przeniesc drzewka umiejetnosci do UI Workspace i dopracowac UX v2` - `UI / UX`, `Design / Systems`, `Core / Architecture`

- `[P1] Przeniesc skill loadout do UI Workspace v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Dodac wspolne tooltipy UI Workspace v1` - `UI / UX`, `Core / Architecture`, `Items / Inventory / Economy`, `Combat`

- `[P1] Dodac transfer drag-and-drop miedzy Character Info v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] Dodac Character Info floating window v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`

- `[P1] Dodac fundament floating windows v1` - `UI / UX`, `Core / Architecture`

- `[P1] Ustandaryzowac design system i template paneli v1` - `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P1] Rewards and Progression v2 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Combat`, `Items / Inventory / Economy`, `UI / UX`

- `[P1] Dodac generowanie broni i pancerzy z czesci v1` - `Items / Inventory / Economy`, `Content / Data`, `Design / Systems`
- `[P1] Dodac runtime oraz zapis inwestycji w skill tree v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
- `[P1] Dodac trwale ciala i interakcje z lootem w lochu v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Core / Architecture`
- `[P1] Rozbic combat rewards na indywidualny loot przeciwnikow v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Core / Architecture`
- `[P1] Dodac modal loadoutu i zamiane aktywnych skilli v1` - `UI / UX`, `Combat`
- `[P1] Dodac skalowalny model loadoutu aktywnych skilli v1` - `Core / Architecture`, `Combat`, `Design / Systems`
- `[P1] Dodac UI progresji i wydawania punktow v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`
- `[P1] Dodac GrowthData klas i automatyczny wzrost statystyk v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
- `[P1] Dodac status indicators i tooltipy v1` - `UI / UX`, `Combat`
- `[P2] Dodac wizualne stany obiektow i jednostek v1` - `UI / UX`, `Combat`, `World / Dungeon / Exploration`
- `[P1] Przebudowac feedback ruchu i targetowania v1` - `UI / UX`, `Combat`
- `[P1] Rozszerzyc Floating Combat Feedback v2` - `UI / UX`, `Combat`
- `[P1] Przebudowac istniejacy CombatHUD do ukladu docelowego` - `UI / UX`, `Combat`
- `[P2] Dodac personality AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac role AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac reakcje oraz podstawowe counterattack v1` - `Combat`, `Content / Data`
- `[P1] Dodac scoring dostepnych skilli AI v1` - `Combat`
- `[P2] Dodac scheduled effects i wieloturowe skille v1` - `Combat`, `Content / Data`
- `[P2] Dodac CreateObject i Summon v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac zrodla i wygasanie modifierow v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac Poison i Burn v1` - `Combat`, `Content / Data`
- `[P1] Przeniesc Stun i Immobilize do PassiveEffectRuntime oraz dodac Sleep, Freeze i Paralysis v1` - `Combat`, `Content / Data`
- `[P2] Dodac Taunt i Invisibility v1` - `Combat`, `Content / Data`
- `[P1] Dodac Pull i Fallback v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac Charge i Teleport v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac runtime ModifyVar v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac runtime ModifyStat v2` - `Combat`, `Core / Architecture`
- `[P1] Przeniesc Bleed do PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac aplikowanie i usuwanie statusow pod PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac warunki efektow na effectId/index v2` - `Combat`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac lifecycle passive effectow v2` - `Combat`, `Core / Architecture`
- `[P1] Ujednolicic PassiveEffectRuntime i TimedPassiveEffect dla statusow oraz modifierow v2` - `Combat`, `Core / Architecture`
- `[P2] Dopracowac inspector SkillData v2` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P2] Dodac podglad runtime jednostki w narzedziach debugowych v1` - `Tools / Editor / Debug`, `Combat`
- `[P2] Dodac autouzupelnianie i sugestie Dev Console v1` - `Tools / Editor / Debug`, `UI / UX`
- `[P1] Dodac walidatory danych combatowych v1` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P1] Dodac Combat Scenario Runner v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Rozszerzyc Dev Console o komendy gameplayowe v2` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Rozszerzyc Dev Console o komendy globalne i targetowanie po characterId v1` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`
- `[P1] Dodac Dev Console v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Dodac generowanie drzwi z kotwic pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Combat`
- `[P1] Dodac strukturalne kotwice pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac wykrywanie, poscig i rozpoczecie walki przez encounter v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac DungeonCombatContext i snapshot obszaru walki v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Zbudowac BattleGrid z DungeonCombatContext v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac spawn uczestnikow walki w obszarze lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Wyswietlic obszar lochu jako scene walki v1` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Wyswietlic encountery jako przeciwnikow na mapie lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Polaczyc wynik walki lochowej z DungeonInstance v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Dodac testy Dungeon Combat Integration v1` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Dodac interakcje z drzwiami w walce lochowej v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dungeon Combat Integration v1 - acceptance pass` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Arungard, NPC and Quests v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Tools / Editor / Debug`
- `[P2] Dodac okno handlu NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `UI / UX`
- `[P2] Dodac TradeService i bazowe ceny v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P2] Dodac TraderRoleData oraz prosta oferte NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac pierwszy quest "Zbadaj Katakumby" v1` - `Content / Data`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac dziennik zadan i sledzone zadanie v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac akcje wyborow dialogowych v1` - `NPC / Quest / Dialogue`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac odbieranie nagrod questowych i world flags v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P1] Dodac cele questa: rozmowa i wyjscie z konkretnego lochu v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac QuestService oraz eventy postepu v1` - `NPC / Quest / Dialogue`, `Core / Architecture`
- `[P1] Dodac QuestDefinition, QuestInstance i cele etapow v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac dialogi NPC v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac DialogueDefinition oraz DialogueService v1` - `NPC / Quest / Dialogue`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac interakcje z NPC i rozpoczecie rozmowy v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac NpcDefinition, NpcInstance i NpcView v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac Arungard jako statyczna lokacje i powrot z lochu v1` - `World / Dungeon / Exploration`
- `[P1] Dodac punkt wejscia do lochu powiazany z DungeonDefinition v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Procedural Dungeon Loop v1 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
- `[P2] Dodac podstawowe obiekty lochu: skrzynia i przeszkoda v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Powrot do aktywnego lochu po walce v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac generowanie encounterow i triggerow walki w lochu v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`
- `[P1] Dodac grywalna mape podlogi lochu: podlogi, sciany, drzwi i kolizje v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
- `[P1] Dodac wejscie do lochu, spawn druzyny i wyjscie z lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Utworzyc scene DungeonSandbox i widok wygenerowanego lochu v1` - `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac DungeonGeneratorService dla seedowanego layoutu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `Tools / Editor / Debug`
- `[P1] Dodac katalog szablonow pokoi i korytarzy v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac runtime graph pokoi, polaczen i pieter lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Rozszerzyc DungeonDefinition o konfiguracje proceduralnego lochu v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Rewards and Progression v1 - acceptance pass` - `Items / Inventory / Economy`, `Combat`, `Tools / Editor / Debug`
- `[P1] Dodac combat rewards i LootTable v1` - `Items / Inventory / Economy`, `Combat`
- `[P2] Dodac Inventory i Equipment UI v1` - `UI / UX`, `Items / Inventory / Economy`
- `[P1] Dodac pipeline statystyk z equipment i passive effects v1` - `Core / Architecture`, `Items / Inventory / Economy`, `Combat`
- `[P1] Dodac EquipmentSet oraz walidacje slotow i wymagan v1` - `Items / Inventory / Economy`
- `[P1] Dodac ItemDefinition, ItemInstance i Inventory v1` - `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac zdobywanie XP, level i skill points v1` - `Core / Architecture`, `Design / Systems`
- `[P1] Gameplay Loop v1 - acceptance pass` - `Core / Architecture`, `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Polaczyc wynik walki ze stanem lochu i druzyny v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac EncounterDefinition, trigger walki i CombatSessionFactory v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac DungeonDefinition i DungeonInstance dla lochu statycznego v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Utworzyc ExplorationSandbox z ruchem po statycznej mapie v1` - `World / Dungeon / Exploration`
- `[P1] Dodac Party runtime oraz przelaczanie lidera poza walka v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zrefaktoryzowac GameBootstrap i SceneLoader v1` - `Core / Architecture`
- `[P1] Wprowadzic GameSession i GameModeState v1` - `Core / Architecture`
- `[P1] Combat Sandbox - acceptance pass` — `Combat`, `Tools / Editor / Debug`
- `[P2] Dodać line of sight skilli v1` — `Combat`, `World / Dungeon / Exploration`
- `[P2] Dodać Escape v1` — `Combat`
- `[P2] Dodać interakcję z obiektami bojowymi v1` — `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodać ShieldPool z warstwami tarczy v1` — `Combat`
- `[P2] Dodać używanie skilli przez AI v1` — `Combat`
- `[P2] Dodać hard CC statusy v1: Stun i Immobilize` — `Combat`
- `[P1] Dodać wykonanie wielu TargetMode i targetRef v1` — `Combat`
- `[P1] Dodać Heal i RestoreResource effect v1` — `Combat`
- `[P1] Dodać hit chance, crit i podstawowe DamageType v1` — `Combat`, `Design / Systems`
- `[P1] Dodać ResourceRules i generację zasobów v1` — `Combat`, `Design / Systems`
- `[P2] Dopracować zasięgi i kształty targetowania skilli` — `Combat`
- `[P1] Dodać warunkowe gałęzie efektów skilli v1` — `Combat`
- `[P1] Dodać docelową kolejkę tur opartą o AV` — `Combat`
- `[P1] Dodać pełniejsze targetowanie skilli v1` — `Combat`
- `[P1] Dodać obsługę Downed i Dead` — `Combat`
- `[P1] Dodać akcję Revive v1 w CombatSandbox` — `Combat`
- `[P1] Dodać testy StatusEffectSystem i kumulacji Bleed v1` — `Combat`
- `[P1] Dodać testy reguł obrażeń i odporności` — `Combat`
- `[P1] Dodać wspólny CombatDamageSystem v1` — `Combat`
- `[P2] Dodać ApplyStatus effect v1` — `Combat`
- `[P2] Doprecyzować uproszczone zasady statusu Bleed v1` — `Design / Systems`
- `[P1] Dodać debug button Start Combat` — `Tools / Editor / Debug`
- `[P1] Przygotować bazowy bootstrap i game flow` — `Core / Architecture`
- `[P1] Utworzyć scenę Bootstrap` — `Core / Architecture`
- `[P2] Dodać inspect celu v1` — `UI / UX`
- `[P2] Dodać highlight celu umiejętności` — `UI / UX`
- `[P1] Dodać CombatSession` — `Combat`
- `[P1] Dodać BattleUnit` — `Combat`
- `[P1] Dodać BattleGrid` — `Combat`
- `[P1] Dodać GridPosition` — `Combat`
- `[P1] Podpiąć ResistanceSet i ShieldPool do CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ShieldPool` — `Core / Architecture`
- `[P1] Dodać ResistanceSet` — `Core / Architecture`
- `[P1] Dodać CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ResourcePool` — `Core / Architecture`
- `[P1] Dodać CharacterStats` — `Core / Architecture`
- `[P1] Dodać klasę Stat` — `Core / Architecture`
- `[P1] Dodać CharacterClassData` — `Content / Data`
- `[P1] Dodać CharacterView v1` — `Core / Architecture`
- `[P1] Dodać podstawowy prefab jednostki bojowej` — `World / Dungeon / Exploration`
- `[P1] Podłączyć prefab jednostki do CombatSession i synchronizować widok z BattleUnit` — `Combat`
- `[P1] Dodać execution pipeline skilli v1` — `Combat`
- `[P1] Dodać drugi skill testowy z prostym dodatkiem, np. bleed albo push` — `Combat`
- `[P2] Dodać status runtime v1` — `Combat`
- `[P1] Dodać prosty restart walki testowej` — `Tools / Editor / Debug`
- `[P2] Dodać 2-3 przeszkody na planszy testowej` — `World / Dungeon / Exploration`
- `[P2] Dodać blokowanie ruchu przez przeszkody` — `Combat`
- `[P2] Doprecyzować minimalny ruleset ruchu v1` — `Design / Systems`
- `[P2] Doprecyzować AP economy dla vertical slice` — `Design / Systems`
- `[P1] Dodać CharacterDefinition` — `Content / Data`

- `[P1] Ustawić strukturę folderów projektu Unity` — `Core / Architecture`
- `[P1] Ustawić assembly definitions i zależności między modułami` — `Core / Architecture`
- `[P1] Utworzyć scenę CombatSandbox` — `Combat`
- `[P1] Spisać konwencje ID, nazw i podziału Data / Runtime / View / System` — `Design / Systems`
- `[P1] Dodać bazowe enumy i typy wspólne` — `Core / Architecture`
- `[P2] Dodać klikany ruch po gridzie i highlight osiągalnych pól` — `UI / UX`

## Git checkpoints

- Commit po zamknieciu jednej karty: kod, assety, testy i zmiana dokumentacji stanowia jeden spojny commit.
- Nie commitujemy pojedynczych checklist itemow ani niedzialajacego stanu posredniego.
- Po zakonczeniu milestone'u wykonujemy acceptance pass, push na GitHub i tag wersji.
- Aktualny checkpoint: `v0.1.0-combat-sandbox` po uporzadkowaniu lokalnej nazwy galezi na `main`.
- Kolejny checkpoint: `v0.2.0-gameplay-loop` po zamknieciu Gameplay Loop v1.
