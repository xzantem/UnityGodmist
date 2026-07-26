# Przyspieszony kurs: inżynieria gier wideo w Unity

## 1. Czym różni się inżynieria gier od zwykłej inżynierii oprogramowania

Inżynieria oprogramowania koncentruje się przede wszystkim na poprawności działania, spełnieniu wymagań, bezpieczeństwie, stabilności i utrzymywalności systemu. W przypadku gier te elementy nadal są bardzo ważne, ale nie wystarczają. Gra może działać poprawnie technicznie i nadal być nieczytelna, frustrująca albo po prostu nudna. Dlatego w game devie poza samą poprawnością implementacji liczy się także jakość doświadczenia gracza.

W praktyce oznacza to, że twórca gry projektuje jednocześnie kod, mechaniki, interfejs, tempo rozgrywki, system nagród, czytelność informacji i warstwę audiowizualną. W zwykłym sofcie stwierdzenie „to działa” bardzo często jest bliskie końca pracy. W grach „to działa” najczęściej oznacza dopiero początek iteracji, ponieważ dopiero po uruchomieniu mechaniki można ocenić, czy jest satysfakcjonująca i zrozumiała dla gracza.

## 2. Trzy warstwy, o których trzeba myśleć przy tworzeniu gry

### 2.1. Warstwa techniczna

To kod, architektura, zapis i odczyt stanu gry, wydajność, zarządzanie zasobami, organizacja scen, narzędzia debugowe i pipeline assetów. W Unity oznacza to między innymi dobry podział odpowiedzialności pomiędzy ScriptableObject, klasy runtime, serwisy i MonoBehaviour.

### 2.2. Warstwa systemowa

To zasady działania samej gry: walka, AI, ekwipunek, questy, crafting, ekonomia, progresja, balans i zależności między mechanikami. Dobrze zaprojektowany system powinien być spójny i rozwijalny, a jego elementy nie powinny sobie wzajemnie przeczyć.

### 2.3. Warstwa doświadczenia gracza

To wszystko, co decyduje o tym, czy gra jest czytelna i daje satysfakcję. Chodzi o tempo, feedback, wyczucie kontroli, poziom trudności, zrozumiałość UI, reakcje na akcje gracza oraz poczucie nagrody za podejmowane działania. Ta warstwa bardzo często wymaga wielu iteracji.

## 3. Najważniejsze pojęcia w inżynierii gier

### 3.1. Core loop

Core loop to podstawowa pętla rozgrywki, czyli sekwencja działań, którą gracz wykonuje wielokrotnie. W przypadku gry Godmist może to być schemat: przygotowanie drużyny w mieście, wejście do lochu, eksploracja, walka, zdobycie łupu, powrót do miasta i dalszy rozwój postaci.

### 3.2. Gameplay loop i progression loop

Gameplay loop to krótsza pętla, np. jedna walka albo jedna interakcja z obiektem. Progression loop dotyczy dłuższego postępu, np. rozwoju całej drużyny na przestrzeni kolejnych wypraw. W dobrze zaprojektowanej grze obie te pętle wspierają się nawzajem.

### 3.3. Feedback i readability

Feedback to sposób, w jaki gra informuje gracza, że coś się wydarzyło. Readability oznacza czytelność sytuacji. W grze taktycznej gracz musi od razu rozumieć, kto ma turę, ile ma zasobów, jaki jest zasięg umiejętności i jaki efekt wywoła jego decyzja.

## 4. Najważniejsze obszary inżynierii gier

- Gameplay engineering – implementacja mechanik takich jak walka, ruch, skille, statusy, AI, questy czy inventory.
- Tools engineering – tworzenie własnych narzędzi wspierających produkcję, np. custom inspectorów, generatorów danych, debug paneli i walidatorów.
- Content pipeline – organizacja danych, assetów, prefabów, ikon, dźwięków i definicji systemowych.
- UX/UI engineering – projektowanie czytelnego interfejsu, tooltipów, paneli i przepływu informacji.
- Performance engineering – pilnowanie płynności działania gry, ograniczanie kosztów Update, GC, instancjowania obiektów i przebudowy UI.

## 5. Metodologie stosowane w game devie

### 5.1. Waterfall

Podejście sekwencyjne, dobre do dokumentacji i formalnego opisu projektu, ale słabe jako główny proces tworzenia gry. W grach zbyt wiele rzeczy odkrywa się dopiero po uruchomieniu mechaniki, więc sztywny model liniowy zwykle nie działa dobrze.

### 5.2. Agile

Podejście iteracyjne, w którym projekt rozwija się małymi krokami i regularnie sprawdza efekty. Dla gier jest to podejście bardzo naturalne, ponieważ pozwala szybko ocenić, czy mechanika działa nie tylko technicznie, ale również gameplayowo.

### 5.3. Scrum

Bardziej sformalizowana odmiana Agile oparta na sprintach, backlogu i przeglądach. W małym projekcie lub pracy indywidualnej zwykle nie wykorzystuje się Scrum w pełnej formie, ale można brać z niego wybrane elementy, np. sprinty tygodniowe i jasne cele na każdy etap.

### 5.4. Kanban

Metoda bardzo praktyczna przy pracy nad grą. Pozwala organizować zadania w prostych kolumnach, takich jak Backlog, Do zrobienia, W trakcie, Do testów i Zrobione. W projektach indie oraz pracach jednoosobowych Kanban często sprawdza się lepiej niż pełny Scrum.

### 5.5. Vertical slice

To mały, ale pełny wycinek gry pokazujący rzeczywisty charakter rozgrywki. Zamiast budować od razu wszystko, tworzy się jeden grywalny fragment, który zawiera prawdziwy flow gry. Dla Unity jest to świetne podejście, bo pozwala wcześniej zweryfikować jakość mechanik i interfejsu.

## 6. Najlepsze podejście dla projektu tworzonego w Unity

Dla gry takiej jak Godmist najbardziej sensowne jest połączenie podejścia iteracyjnego, Kanbanu, milestone’ów i vertical slice. Oznacza to:

- duże etapy rozwoju gry są planowane jako milestone’y,
- codzienna lub tygodniowa praca odbywa się w stylu Kanban,
- najważniejsze systemy najpierw są prototypowane,
- zamiast tworzyć wszystko naraz, buduje się mały grywalny fragment gry,
- każda większa mechanika przechodzi przez iteracje i poprawki.

## 7. Jak wygląda dobry proces tworzenia gry

### 7.1. Preprodukcja

Na tym etapie ustala się fundamenty projektu: fantasy gry, core loop, zakres MVP, główne systemy, styl wizualny i kierunek architektury.

### 7.2. Prototypowanie

Powstają szybkie, proste i często brzydkie wersje mechanik. Ich celem nie jest estetyka, tylko odpowiedź na pytanie, czy walka, eksploracja i interakcje są zrozumiałe i przyjemne.

### 7.3. Vertical slice

To pierwszy pełny fragment gry, który powinien przypominać docelowy produkt. W tym momencie warto sprawdzać nie tylko kod, ale również UI, flow sceny, tempo i feedback.

### 7.4. Produkcja

Dopiero po sprawdzeniu, że rdzeń działa, rozszerza się grę o kolejne klasy, potwory, lochy, questy, przedmioty i panele interfejsu.

### 7.5. Polish

Na końcu dopracowuje się balans, czytelność, animacje, dźwięki, wygodę interfejsu, loading i ogólny feeling gry.

## 8. Jak myśleć architektonicznie w Unity

### 8.1. Rozdziel dane od widoku

W Unity łatwo popełnić błąd polegający na wrzuceniu zbyt dużej ilości logiki do MonoBehaviour. Lepszy model to:

- ScriptableObject – definicje danych,
- zwykłe klasy C# – runtime state,
- serwisy – logika operacyjna,
- MonoBehaviour – prezentacja i integracja ze sceną.

### 8.2. Unikaj przeładowanego GameManagera

Jeden ogromny GameManager szybko staje się nieczytelny i trudny do rozwijania. Lepszy podział to osobne serwisy, np. CombatService, QuestService, InventoryService, SaveService czy DungeonService.

### 8.3. Myśl wcześnie o debugowaniu i narzędziach

Im bardziej systemowa gra, tym bardziej opłaca się tworzyć panele debugowe, walidatory danych, podglądy statusów, wymuszanie walki czy szybkie dodawanie przedmiotów i XP.

### 8.4. Save/load to część architektury, nie dodatek

Zapis gry nie powinien być zostawiony na sam koniec. Jeśli gra ma wiele systemów i stanów, model trwałości danych trzeba przewidzieć wcześnie, inaczej późniejsza integracja bywa bardzo bolesna.

## 9. Typowe błędy początkujących twórców gier

- za szybkie tworzenie dużej ilości contentu zanim systemy są stabilne,
- zbyt wczesne polerowanie niedojrzałych mechanik,
- brak jasnego MVP,
- wrzucanie całej logiki do MonoBehaviour,
- brak narzędzi debugowych,
- odkładanie save/load na koniec,
- mylenie „działa technicznie” z „działa dobrze jako gra”.

## 10. Jak testuje się gry

W grach testuje się nie tylko poprawność działania, ale też jakość doświadczenia gracza. Dlatego testowanie można podzielić na trzy poziomy:

- poprawność techniczna – czy system działa bez błędów,
- poprawność systemowa – czy mechaniki są spójne i nie psują się nawzajem,
- jakość doświadczenia – czy gra jest czytelna, satysfakcjonująca i sensownie zbalansowana.

## 11. Co w game devie naprawdę znaczy „gotowe”

W przypadku gry funkcjonalność jest gotowa dopiero wtedy, gdy:

- działa technicznie,
- jest czytelna dla gracza,
- daje właściwy feedback,
- ma sens systemowo,
- nie psuje innych obszarów gry,
- da się ją rozwijać bez przebudowy całej architektury.

## 12. Proponowane podejście dla Godmist

Dla tego projektu najlepsze będzie budowanie gry etapami:

- najpierw solidny fundament techniczny,
- potem mały vertical slice,
- następnie rozwój kolejnych systemów,
- na końcu balancing i polish.

Przykładowa kolejność:

- fundament: game state, postacie, walka, grid, save/load,
- vertical slice: jeden mały loch, podstawowe UI, kilku przeciwników, prosta ekonomia,
- rozbudowa: inventory, questy, NPC, crafting, kolejne klasy,
- polish: balans, feedback, UX, wygoda obsługi i dopracowanie flow.

## 13. Wnioski

Inżynieria gier wideo różni się od klasycznej inżynierii oprogramowania tym, że poprawność techniczna jest tylko jednym z warunków sukcesu. Równie istotne są grywalność, czytelność, balans i iteracyjne dopracowywanie doświadczenia gracza. W przypadku Unity szczególnie ważne stają się dobra organizacja architektury, rozdzielenie definicji danych od runtime’u i warstwy widoku, rozsądne podejście do metodologii oraz tworzenie narzędzi wspierających rozwój gry.

## Notatki do zeszytu

### Najkrótsza definicja

Inżynieria gier to tworzenie systemu, który ma nie tylko działać, ale też być czytelny, angażujący i przyjemny dla gracza.

### Najważniejsze różnice względem zwykłego software engineering

- w grach „działa” nie znaczy jeszcze „jest dobre”,
- trzeba testować fun, feedback i czytelność,
- content i UX są dużo ważniejsze niż w zwykłych aplikacjach,
- potrzebne są częste iteracje,
- wydajność czasu rzeczywistego ma duże znaczenie.

### Co zapamiętać o metodologii

- Waterfall nadaje się bardziej do papierów niż do samego tworzenia gry,
- Agile i iteracje pasują do game devu lepiej,
- Kanban jest bardzo dobry do solo projektu,
- vertical slice jest ważniejszy niż wiele niedokończonych systemów.

### Co zapamiętać o Unity

- ScriptableObject do definicji,
- klasy C# do runtime state,
- serwisy do logiki,
- MonoBehaviour do widoku i sceny,
- nie wrzucać wszystkiego do jednego GameManagera,
- save/load planować wcześnie,
- robić debug tools i walidację danych.

### Najważniejsze hasła

- core loop,
- gameplay loop,
- progression loop,
- readability,
- feedback,
- vertical slice,
- MVP,
- polish,
- iteration.

### Jedno zdanie do promotora lub prowadzącego

Produkcja gry wymaga podejścia iteracyjnego, ponieważ poprawność implementacji nie wystarcza – mechaniki muszą być jeszcze sprawdzane pod kątem czytelności, balansu i jakości doświadczenia gracza.
