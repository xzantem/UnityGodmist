# CP2-08 — Strażnik, projekt v6 i podgląd w grze

Status: projekt v6 zaakceptowany do realizacji 2026-11-02. CP2-09 wdraża T1–T5; T6/T7 pozostają projektem. Aktualny stan implementacji: CONTENT_PASS_2_GUARDIAN.md.

## Stałe założenia

Dokładnie cztery aktywne w T1–T7: Osłona w Ruchu, Prowokacja, Kontratak,
Ostatnia Linia. Pasyw globalny: +0,5% obu obron/SP. Istniejący Impet,
bez zmiany jego bazowych zasad. Darmowy Taneczny Krok we własnej turze.
29 węzłów, 50 rang; T1–T5 to 25 węzłów i 44 rangi. W CP2-09 produkcyjne drzewo pokazuje wyłącznie
T1–T5. T6/T7 poniżej są odłożonym projektem, bez node’ów i skilli w grze.

## Kierunek v5

Lewa część: mobilność, uniki i Odwet. Środek: opcjonalna tarcza, a następnie
niezależna ochrona przez pozycję i leżące sztylety. Prawa: przejmowanie
zagrożenia i przygotowany Kontratak. Tylko dwa ulepszenia zależą od Osłony:
Osłona → Przedłużona Osłona → Wspólny Front. Żaden późniejszy węzeł nie
wymaga tej gałęzi ani mechanicznie nie zakłada jej używania.

Usunięto Bastion i tarczowy warunek Eskorty/Zgrania. Swobodny Ruch nagradza
odzysk sztyletu zamiast rzucenia Osłony. Opanowanie jest samodzielne.
Ostatnia Linia: 75 Impetu/40% PA, bez wymagań węzłów, tylko próg 30 SP.
Nie trzeba brać wszystkich T6. Pozostałe zasady capstone'u zachowane.

## Układ

| Tier | Kolumna 1 | Kolumna 2 | Kolumna 3 | Kolumna 4 | Kolumna 5 |
| --- | --- | --- | --- | --- | --- |
| T1 | Czujność | Refleks | Osłona w Ruchu | Prowokacja | Odwrócenie |
| T2 | Pewny Krok | Taneczny Krok | Przedłużona Osłona | Zaślepienie Wściekłością | Neutralizacja |
| T3 | Szybki Odwet | Swobodny Ruch | Wspólny Front | Nieustępliwość | Blokada Zaklęcia |
| T4 | Odepchnięcie | Przypływ Adrenaliny | Eskorta | Stanowczość | Zasłonięcie |
| T5 | Opanowanie | Ochrona Sztyletu | Zwarty Szyk | Kontratak | Koncentracja |
| T6 | — | Zachowanie Osłony | Zgranie | Chłodna Krew | — |
| T7 | — | — | Ostatnia Linia | — | — |

## Węzły, rangi i połączenia

Koszt 1 SP/ranga. Progi T1/T2: 0; T3/T4: 10; T5/T6: 20; T7: 30 SP.

### t1_1 — Refleks

Wymagania: tylko próg tieru.

- Ranga 1: Zwiększa unik o 8.
- Ranga 2: Zwiększa unik o 16.

Zasady: Unik +8/16 jednostek statystyki; nie są to punkty procentowe końcowej szansy uniknięcia.

Uzasadnienie: Punkt wejścia do mobilnego obrońcy. Inwestycja w unik działa bez Osłony w Ruchu.

### t1_2 — Czujność

Wymagania: tylko próg tieru.

- Ranga 1: Zmniejsza obrażenia pierwszego otrzymanego trafienia w turze o 10%.
- Ranga 2: Zmniejsza obrażenia pierwszego otrzymanego trafienia w turze o 20%.

Zasady: Pierwsze trafienie bezpośredniego ataku między początkami własnych tur zadaje o 10/20% mniej obrażeń. Pudło, unik i obrażenia okresowe nie zużywają ochrony; trafienie w tarczę ją zużywa. Redukcja mnoży się przez pozostałe redukcje.

Uzasadnienie: Jedno czytelne zabezpieczenie przed pierwszym trafieniem zastępuje kilka podobnych pasywów antykrytycznych.

### t1_3 — Osłona w Ruchu

Wymagania: tylko próg tieru.

Koszt: 40% PA • Impet: 35.

- Ranga 1: Zapewnia na 3 tury tarczę równą 15% maksymalnego zdrowia celu.

Zasady: Tarcza 15% maksymalnego zdrowia celu na 3 jego tury. Jeden żywy sojusznik lub rzucający; zasięg 4. Ta sama tarcza odświeżana, bez sumowania.

Uzasadnienie: Opcjonalny aktywny filar tarcz. Bez niej można rozwijać ruch, reakcje, Prowokację i ochronę przy sztyletach.

### t1_4 — Prowokacja

Wymagania: tylko próg tieru.

Koszt: 70% PA • Impet: 55.

- Ranga 1: Ma 90% szansy sprowokowania przeciwnika na 3 tury.

Zasady: 90% bazowej szansy prowokacji na 3 tury; jeden wróg, zasięg 4; standardowa odporność na kontrolę. Bez obrażeń i kosztu noża.

Uzasadnienie: Pozwala przejąć zagrożenie od słabszego członka drużyny i wykorzystać własną obronę.

### t1_5 — Odwrócenie

Wymagania: tylko próg tieru.

- Ranga 1: Zwiększa obronę magiczną o 10%.
- Ranga 2: Zwiększa obronę magiczną o 30%.

Zasady: Obrona magiczna +10/30%.

Uzasadnienie: Zapewnia podstawową ochronę przed magią również bez specjalizacji w magicznych unikach.

### t2_1 — Pewny Krok

Wymagania: tylko próg tieru.

- Ranga 1: Pierwszy płatny krok w turze zwiększa unik o 4 do początku następnej własnej tury.
- Ranga 2: Pierwszy płatny krok w turze zwiększa unik o 8 do początku następnej własnej tury.

Zasady: Pierwszy faktycznie wykonany płatny krok we własnej turze daje +4/8 uniku do początku następnej własnej tury. Bez kumulacji; darmowy krok, teleport i ruch wymuszony nie wyzwalają efektu.

Uzasadnienie: Zwykły ruch przygotowuje obronę i prowadzi do Odwetu, bez zależności od tarczy.

### t2_2 — Taneczny Krok

Wymagania: t1_1.

- Ranga 1: Raz na turę udany unik zapewnia jeden darmowy krok.

Zasady: Po pierwszym udanym uniku między początkami własnych tur otrzymuje jeden darmowy zwykły krok do użycia we własnej turze, do końca następnej własnej tury. Bez kumulacji, generowania Impetu i zwiększania narastającego kosztu/serii płatnych kroków. Nie przerywa akcji przeciwnika.

Uzasadnienie: Przenosi nagrodę za unik do świadomej decyzji w swojej turze, bez przerywania akcji wroga.

### t2_3 — Przedłużona Osłona

Wymagania: t1_3.

- Ranga 1: Wydłuża Osłonę w Ruchu do 4 tur celu.
- Ranga 2: Wydłuża Osłonę w Ruchu do 5 tur celu.

Zasady: Bezpośrednia Osłona w Ruchu trwa o 1/2 tury celu dłużej, łącznie 4/5 tur. Bez wydłużania Wspólnego Frontu, Bastionu lub Ostatniej Linii.

Uzasadnienie: Pozwala utrzymywać ochronę sojuszników mniejszą liczbą ponownych użyć, ale nie zwiększa wartości tarczy.

### t2_4 — Zaślepienie Wściekłością

Wymagania: t1_4.

- Ranga 1: Prowokacja obniża celność umiejętności celu o 5 na czas jej działania.
- Ranga 2: Prowokacja obniża celność umiejętności celu o 10 na czas jej działania.
- Ranga 3: Prowokacja obniża celność umiejętności celu o 15 na czas jej działania.

Zasady: Udana Prowokacja obniża celność umiejętności celu o 5/10/15 na czas prowokacji. Nie odejmuje pp od końcowej szansy trafienia.

Uzasadnienie: Prowokacja otrzymuje część defensywną. Pudło od obniżonej celności nadal nie jest automatycznie unikiem.

### t2_5 — Neutralizacja

Wymagania: t1_5.

- Ranga 1: Zmniejsza bazową szansę otrzymania efektów kontroli o 10 pp.
- Ranga 2: Zmniejsza bazową szansę otrzymania efektów kontroli o 20 pp.

Zasady: Bazowa szansa otrzymania ogłuszenia, unieruchomienia, snu, paraliżu, prowokacji lub strachu −10/20 pp, przed standardową odpornością i tolerancją; minimum 0. Nie dotyczy obrażeń okresowych, debuffów statystyk ani zderzenia z przeszkodą.

Uzasadnienie: Utrzymanie możliwości ruchu i używania ochrony jest częścią roli obrońcy.

### t3_1 — Szybki Odwet

Wymagania: t2_1.

- Ranga 1: Raz na turę po uniku wykonuje Rzut Sztyletem za 40% normalnych obrażeń.
- Ranga 2: Raz na turę po uniku wykonuje Rzut Sztyletem za 70% normalnych obrażeń.

Zasady: Po pierwszym uniku między początkami własnych tur może wykonać reakcję Rzutem Sztyletem w żywego atakującego: 40/70% zwykłych obrażeń, 0 PA i 0 Impetu, rzeczywisty koszt noża według dystansu; standardowe trafienie, krytyk i zasięg. Brak noża poza zwarciem albo brak linii widzenia/zasięgu oznacza brak reakcji, bez długu na później.

Uzasadnienie: Daje ograniczoną ofensywną nagrodę za obronę, zamiast zastępować własne ataki.

### t3_2 — Swobodny Ruch

Wymagania: t2_2.

- Ranga 1: Odzyskanie własnego sztyletu zapewnia darmowy Taneczny Krok, raz na turę.

Zasady: Udany odzysk co najmniej jednego własnego sztyletu może przyznać darmowy krok bez wcześniejszego uniku. Odzysk z ziemi, Linką z Hakiem lub z przeciwnika. Wspólny limit z Tanecznym Krokiem: jedno przyznanie między początkami własnych tur, bez kumulacji; zużycie kroku nie odnawia limitu. Krok nie generuje Impetu, nie zwiększa numeru płatnego kroku i respektuje przeszkody oraz unieruchomienie. Wygasa do końca następnej własnej tury.

Uzasadnienie: Proaktywna mobilność wynika z używania sztyletów, a nie z rzucania Osłony.

### t3_3 — Wspólny Front

Wymagania: t2_3.

- Ranga 1: Osłonięcie innego sojusznika daje na 3 tury własną tarczę równą 50% jego osłony, do 10% własnego maksymalnego zdrowia.

Zasady: Osłona w Ruchu przyznana innemu sojusznikowi daje Strażnikowi osobną tarczę równą 50% rzeczywiście przyznanej osłony, maksymalnie 10% własnego maksymalnego zdrowia, na 3 własne tury. Ten sam efekt odświeża się bez sumowania. Użycie na sobie i wtórna tarcza nie uruchamiają kolejnego efektu.

Uzasadnienie: Drugie i ostatnie ulepszenie Osłony zamyka krótki, pionowy łańcuch. Pozostałe gałęzie nie wymagają tego talentu.

### t3_4 — Nieustępliwość

Wymagania: t2_4.

- Ranga 1: Zmniejsza bezpośrednie obrażenia otrzymywane od sprowokowanych przeciwników o 10%.
- Ranga 2: Zmniejsza bezpośrednie obrażenia otrzymywane od sprowokowanych przeciwników o 20%.

Zasady: Bezpośrednie ataki przeciwnika aktualnie objętego własną Prowokacją zadają Strażnikowi o 10/20% mniej obrażeń. Mnożnik redukcji, bez wpływu na obrażeń okresowych lub ataki w innych sojuszników.

Uzasadnienie: Wzmacnia świadome przejęcie zagrożenia zamiast tworzyć kolejną umiejętność kontroli.

### t3_5 — Blokada Zaklęcia

Wymagania: t2_5.

- Ranga 1: Pozwala unikać bezpośrednich ataków magicznych z wykorzystaniem połowy aktualnego uniku.

Zasady: Bezpośredni atak magiczny rozstrzyga szansę trafienia standardową formułą, ale z połową aktualnego uniku Strażnika; dotychczasowy magiczny atak bez takiego talentu nadal nie dostaje nowego testu celności. Nie unika obrażeń okresowych ani obrażeń nieuchronnych.

Uzasadnienie: Pozwala inwestycji w unik pracować również przeciw magii, ale ze zmniejszoną skutecznością.

### t4_1 — Odepchnięcie

Wymagania: t3_1.

- Ranga 1: Szybki Odwet odpycha trafionego w zwarciu przeciwnika o 1 pole.
- Ranga 2: Szybki Odwet odpycha trafionego w zwarciu przeciwnika o 2 pola.

Zasady: Szybki Odwet, który trafił w zwarciu żywego wroga, próbuje odepchnąć go o 1/2 pola, bez dodatkowej opłaty i losowania dodatkowego efektu talentu. Zwykła odporność na przemieszczenie, geometria i reguły kolizji nadal działają. Odwet na dystansie nie pcha.

Uzasadnienie: Bezpośrednie ulepszenie Szybkiego Odwetu, równoległe do odzyskiwania Impetu. Nie wymaga Adrenaliny.

### t4_2 — Przypływ Adrenaliny

Wymagania: t3_1.

- Ranga 1: Raz na turę udany unik przywraca 6 Impetu.
- Ranga 2: Raz na turę udany unik przywraca 10 Impetu.

Zasady: Pierwszy udany unik między początkami własnych tur przywraca 6/10 Impetu. Zwrot stały, bez losowania, nie podlega połowieniu generacji ruchowej ponad 100.

Uzasadnienie: Alternatywna inwestycja w ekonomię zasobu po uniku; równoległa do Odepchnięcia, bez przymusu kupowania obu.

### t4_3 — Eskorta

Wymagania: tylko próg tieru.

- Ranga 1: Zapewnia sobie i przyległym sojusznikom +5 uniku, gdy obok stoi sojusznik.
- Ranga 2: Zapewnia sobie i przyległym sojusznikom +10 uniku, gdy obok stoi sojusznik.

Zasady: Wymaga innego żywego członka drużyny na jednym z czterech pól przyległych. Strażnik i żywi członkowie drużyny przylegli do niego otrzymują +5/10 uniku. Bez przywołań i klonów. Bonus zmienia się wraz z pozycją i znika po śmierci właściciela. Wielu Strażników nie sumuje tego samego bonusu; działa silniejszy.

Uzasadnienie: Ochrona przez ustawienie obok sojusznika jest niezależną alternatywą do tarcz.

### t4_4 — Stanowczość

Wymagania: t3_4.

- Ranga 1: Nieudana Prowokacja zwraca 50% wydanego Impetu.
- Ranga 2: Nieudana Prowokacja zwraca 100% wydanego Impetu.

Zasady: Gdy Prowokacja została poprawnie opłacona, ale nie nałożyła prowokacji z powodu nieudanej próby lub odporności celu, zwraca 50/100% rzeczywiście zapłaconego Impetu. Zwrot w dół do całej jednostki, bez zwrotu PA i bez przekraczania limitu zasobu. Udana Prowokacja nie zwraca kosztu. Anulowanie lub nieprawidłowy cel nie wyzwalają zwrotu.

Uzasadnienie: Ułatwia zarządzanie ryzykiem kontroli, bez dokładania kolejnej warstwy tarczy lub redukcji obrażeń.

### t4_5 — Zasłonięcie

Wymagania: tylko próg tieru.

- Ranga 1: Zmniejsza bezpośrednie obrażenia otrzymywane przez przyległych sojuszników o 10%, jeśli napastnik jest oddalony od nich o co najmniej 2 pola.

Zasady: Chroni innych żywych członków drużyny na czterech polach przyległych do Strażnika. Odległość atakującego od chronionego celu co najmniej 2 pola, liczona poziomo i pionowo. Zmniejsza bezpośrednie obrażenia fizyczne i magiczne o 10%, bez wpływu na obrażenia okresowe. Nie chroni samego Strażnika. Mnoży się z innymi redukcjami; wiele źródeł Zasłonięcia nie sumuje się. Wymaga żywego właściciela.

Uzasadnienie: Pozwala chronić drużynę przed ostrzałem przez pozycję. Nie wymaga Prowokacji, tarczy ani dodatkowego przycisku.

### t5_1 — Opanowanie

Wymagania: tylko próg tieru.

- Ranga 1: Pierwsza aktywna umiejętność Strażnika w turze kosztuje o 10% mniej Impetu.
- Ranga 2: Pierwsza aktywna umiejętność Strażnika w turze kosztuje o 20% mniej Impetu.

Zasady: Pierwsza aktywna umiejętność Strażnika we własnej turze kosztuje o 10/20% mniej Impetu. Obniżony koszt bazowy jest zaokrąglany w górę, następnie działają pozostałe modyfikatory kosztu. Nie zmniejsza PA. Zwykły atak, reakcja lub anulowana próba nie zużywają zniżki. Na drugiej randze koszty Osłony, Prowokacji, Kontrataku i Ostatniej Linii wynoszą odpowiednio 28, 44, 56 i 60 Impetu.

Uzasadnienie: Oszczędzanie Impetu jest samodzielnym wyborem: zadziała również w buildzie Prowokacji i Kontrataku bez Osłony.

### t5_2 — Ochrona Sztyletu

Wymagania: tylko próg tieru.

- Ranga 1: Zmniejsza bezpośrednie obrażenia otrzymywane przez siebie i sojuszników o 5% na polu własnego leżącego sztyletu i polach przyległych.
- Ranga 2: Zmniejsza bezpośrednie obrażenia otrzymywane przez siebie i sojuszników o 8% na polu własnego leżącego sztyletu i polach przyległych.
- Ranga 3: Zmniejsza bezpośrednie obrażenia otrzymywane przez siebie i sojuszników o 10% na polu własnego leżącego sztyletu i polach przyległych.

Zasady: Strażnik i żywi członkowie drużyny na polu własnego leżącego sztyletu lub jednym z czterech pól przyległych otrzymują 5/8/10% mniej bezpośrednich obrażeń. Nie obejmuje obrażeń okresowych ani noża wbitego we wroga. Wiele noży i właścicieli nie sumuje tej premii; działa silniejsza. Właściciel musi żyć. Usunięcie noża lub zmiana pozycji odświeża obszar.

Uzasadnienie: Samodzielny filar późnej ochrony i ekonomii sztyletów. Dostępny po progu SP bez inwestowania w tarczę.

### t5_3 — Zwarty Szyk

Wymagania: t4_3.

- Ranga 1: Obecność przyległego sojusznika zwiększa obronę fizyczną i magiczną o 10%.
- Ranga 2: Obecność przyległego sojusznika zwiększa obronę fizyczną i magiczną o 20%.

Zasady: Gdy na jednym z czterech pól przyległych stoi inny żywy członek drużyny, własna obrona fizyczna i magiczna wzrastają o 10/20%. Nie zwiększa obron sojusznika. Nie skaluje się z liczbą sąsiadów, nie obejmuje przywołań i klonów. Przemieszczenie lub śmierć sąsiada aktualizuje warunek.

Uzasadnienie: Wspiera obrońcę pozostającego przy drużynie, tworząc krótką ścieżkę od Eskorty.

### t5_4 — Kontratak

Wymagania: tylko próg tieru.

Koszt: 40% PA • Impet: 70.

- Ranga 1: Do końca następnej własnej tury odbija 100% bazowej wartości następnego bezpośredniego ataku.

Zasady: Przygotowanie trwa do końca następnej własnej tury. Zużywa się na pierwszej próbie bezpośredniego ataku przeciw Strażnikowi, także przy pudle lub uniku, raz na użycie umiejętności. Odbicie wykorzystuje wylosowaną wartość ataku po premiach napastnika, przed obroną i krytykiem. Zachowuje fizyczny lub magiczny typ obrażeń; podlega obronie i tarczom napastnika, samo nie krytuje. Nie zmniejsza otrzymanych obrażeń. Nie reaguje na obrażenia okresowe, samookaleczenie, odbicia ani same osłabienia. Nie tworzy kolejnych reakcji. Ponowne użycie odświeża przygotowanie, bez kumulacji.

Uzasadnienie: Pozwala zaplanować kosztowną odpowiedź na przewidywany mocny atak, bez darmowych nieograniczonych kontrreakcji.

### t5_5 — Koncentracja

Wymagania: t5_4.

- Ranga 1: Zwiększa odbicie Kontrataku do 175% wartości bazowej.
- Ranga 2: Zwiększa odbicie Kontrataku do 250% wartości bazowej.

Zasady: Kontratak odbija +75/150 pp (łącznie 175/250% wartości bazowej). Pozostałe warunki i jednorazowe zużycie bez zmian.

Uzasadnienie: Daje opcjonalną specjalizację w odbiciu i znaczący wydatek punktów poza gałęzią tarcz.

### t6_2 — Zachowanie Osłony

Wymagania: t5_2.

- Ranga 1: Odzyskanie leżącego sztyletu zachowuje jeden jego obszar ochrony do końca następnej własnej tury.
- Ranga 2: Odzyskanie leżącego sztyletu zachowuje jeden jego obszar ochrony do końca drugiej następnej własnej tury.

Zasady: Odzyskanie własnego leżącego sztyletu pozostawia obszar Ochrony Sztyletu w jego dawnym miejscu do końca następnej/drugiej następnej własnej tury. Tylko jeden ostatni obszar na Strażnika, bez sumowania z realnymi nożami i innymi obszarami tej ochrony. Nie jest przedmiotem i nie można go ponownie zebrać. Wymaga żywego właściciela, znika po walce.

Uzasadnienie: Łagodzi koszt odzyskania broni, rozwijając wyłącznie gałąź sztyletów.

### t6_3 — Zgranie

Wymagania: t5_2.

- Ranga 1: Na początku tury przywraca 2 Impetu za każdego innego sojusznika chronionego przez własny leżący sztylet, maksymalnie 6.
- Ranga 2: Na początku tury przywraca 4 Impetu za każdego innego sojusznika chronionego przez własny leżący sztylet, maksymalnie 12.

Zasady: Na początku własnej tury +2/4 Impetu za innego żywego członka drużyny stojącego na polu własnego leżącego sztyletu lub jednym z czterech pól przyległych; maksymalnie +6/12 na turę. Każdy sojusznik liczony raz niezależnie od liczby noży. Bez siebie, przywołań i klonów. Czasowy ślad po odzyskanym nożu nie daje zwrotu.

Uzasadnienie: Ekonomia późnej ochrony wynika z rozmieszczenia sztyletów, niezależnie od Osłony w Ruchu.

### t6_4 — Chłodna Krew

Wymagania: t5_4.

- Ranga 1: Kontratak przywraca 20 Impetu po wykonaniu odbicia.

Zasady: Po wykonaniu odbicia Kontrataku przywraca 20 Impetu, raz na przygotowanie. Odbicie musi zostać skierowane w poprawnego żywego napastnika; nie musi odebrać mu zdrowia. Samo przygotowanie, wygaśnięcie lub brak poprawnego napastnika nie daje zwrotu. Limit zasobu nadal obowiązuje. Nie wyzwala kolejnego odbicia ani reakcji.

Uzasadnienie: Późna specjalizacja Kontrataku działa bez sztyletów, uników i tarcz.

### t7_3 — Ostatnia Linia

Wymagania: tylko próg tieru.

Koszt: 40% PA • Impet: 75.

- Ranga 1: Przez 2 tury celu zmniejsza bezpośrednie obrażenia o 30% i chroni przed jednym śmiertelnym atakiem, pozostawiając 1 punkt zdrowia. Raz na walkę.

Zasady: Raz na walkę można oznaczyć siebie albo jednego żywego członka drużyny w zasięgu 4. Ochrona trwa do końca drugiej następnej tury celu. Otrzymywane bezpośrednie obrażenia są o 30% mniejsze (mnożnik). Pierwszy bezpośredni atak, który po obronie i tarczach nadal byłby śmiertelny, pozostawia cel z 1 punktem zdrowia i kończy całe oznaczenie. Nie chroni przed obrażeniami okresowymi ani samookaleczeniem. Nie wymaga wcześniejszej Osłony w Ruchu. Raz na walkę oznacza jedno skuteczne użycie, także gdy ratunek nie nastąpi. Nie resetuje się po wskrzeszeniu; nie można usunąć oznaczenia zwykłym rozproszeniem. Efekt kończy się po śmierci Strażnika lub końcu walki.

Uzasadnienie: Finał dostępny po 30 SP z dowolnej ścieżki, bez obowiązku wykupienia trzech T6. Koszt obniżony do 75 Impetu i 40% PA; zachowane okno ochrony i jedno użycie na walkę.

## Walidacja i dalsze ryzyka

Każde połączenie obejmuje najwyżej jeden tier i jedną kolumnę. Maksymalnie
dwa wyjścia z węzła; jedno bezpośrednie wyjście z Osłony. Dwa zależne od niej
węzły łącznie. Przykładowe buildy 30+10 i 21+19 SP legalne; drugi nie wykupuje
Osłony ani jej ulepszeń. Osobno legalny capstone przy 31 SP bez Osłony i bez
wszystkich T6. Rozpiski i wyniki: guardian-design-proposal.json.

Do oceny w walce: sumowanie redukcji Czujności/Nieustępliwości/aur,
łatwość utrzymywania sąsiadów, ekonomia Stanowczości i Zgrania, automatyczne
Odepchnięcie oraz koszt i okno Ostatniej Linii. Redukcje mnożą się; wiele
kopii tej samej aury nie sumuje się. Brak nowych reakcyjnych pętli.

## Granica implementacji

Wyłącznie podgląd węzłów/opisów/połączeń. IsDesignPreview blokuje SP,
brak GrantedSkill, PassiveEffects i globalnych efektów. Cztery aktywne
wyróżnia wyłącznie PreviewActive. Opisy Następne/Aktualne zgodnie ze wspólnym widokiem.
Pełne zasady wyłącznie pod świadomie otwieranym linkiem. Ikony tymczasowe.
Runtime, AI, preview obliczeń i testy mechanik należą do przyszłego CP2-09.
Nowe mechaniki wymagają osobnych testów ruchu/odzysku, kolejności reakcji,
redukcji i clampów zasobu; nie zmieniać bazowej reguły Impetu ani zapisu.


## Doprecyzowanie konwencji — v5

Wymaganie może oznaczać bazową mechanikę lub kolejny etap tej samej
specjalizacji, jak w pozostałych drzewkach. Zachowano krótkie ścieżki tarczy,
Prowokacji i odporności magicznej. Dwa ulepszenia Odwetu są równoległe:
Odepchnięcie T4:1 oraz Adrenalina T4:2. Opanowanie samodzielnie w T5:1.
Nie trzeba kupować odzysku Impetu, żeby uzyskać przemieszczenie wroga.

Opisy: krótki efekt jak w innych drzewkach, wspólne objaśnienia wartości,
koszt PA i Impetu nad aktywną oraz osobny cel/zasięg. Bez etykiet
Aktywna/Pasywna w każdym opisie. Jedno „Zasady działania” na cały węzeł.
Przy zerowej inwestycji widoczne Następne; kolejne rangi opisane standardowo przez Aktualne/Następne.

Automatyczny odbiór podglądu: 146 testów PASS; brak skrzyżowań połączeń w siatce węzłów. Raport: Tools/ContentPass2/guardian-preview-validation.json.

## Korekta v6

Zamieniono wyłącznie kolumny Refleksu (T1:2) i Czujności (T1:1); stabilne identyfikatory i wymagania zachowane. W opisach usunięto wyjątek podglądu wszystkich rang. Przy zerowej inwestycji widoczne „Następne:”, jak w pozostałych drzewkach. Nadal brak wydawania SP i funkcjonalności.
