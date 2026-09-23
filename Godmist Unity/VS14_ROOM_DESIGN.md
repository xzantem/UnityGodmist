# VS-14 — role pomieszczeń Katakumb

Status: wdrożono pierwsze 16 szablonów w katalogu produkcyjnym; 11 pokojów ma cztery jawne pozycje wrogów, a 11 celowo używa krawędzi strukturalnych. Ocena czytelności sytuacji w Play Mode pozostaje do smoke.

## Zasada projektowa

Generator układa **autorskie sytuacje**, a nie dopiero po wylosowaniu rozmieszcza dekoracje i pułapki. Każdy szablon ma odpowiedzieć na cztery pytania: po co gracz do niego wchodzi, jaką decyzję podejmuje, co w układzie wpływa na walkę lub eksplorację i po czym pozna konsekwencję. Obiekty definiujące sytuację są stałe; losowe mogą być składy encounterów, łup i drobne warianty, o ile nie niszczą przejścia ani roli pokoju.

Pierwsza pula Katakumb: 1 startowy, 9 zwykłych, 3 opcjonalne, 2 korytarze, 1 finałowy. To początek biblioteki, nie docelowy limit liczby pokojów. Wszystko na poziomie podłogi 0, bez podestów i balkonów. Brakujące pola podłogi służą do ukształtowania trasy, nie do parkouru. Jeżeli w danym miejscu nie ma podłogi, gracz ma obejść lukę istniejącym przejściem. Docelowe zwykłe pokoje powinny być nieco większe od pierwszych szkiców koncepcyjnych: w praktyce potrzebują miejsca na manewr czwórki bohaterów, do czterech wrogów i co najmniej jeden świadomy wybór trasy.

Układ całego piętra również jest częścią projektu. Profil Katakumb preferuje zwarty obrys: generator rozpatruje kierunki i obroty razem, a spośród legalnych połączeń wybiera te, które mniej wydłużają piętro. Wąskie gardło pozostaje pokojem przelotowym, ale zakręt krypty, odetchnienie, rozdzielona nawa i krąg rytualny dopuszczają wybrane wejścia z boku. Korytarze nie powinny dominować głównej trasy; docelowa szansa ich wyboru to 30%. Układ nadal ma zachować przechodnią główną drogę, odnogi i 1–2 możliwe pętle.

Osiem zaplanowanych typów lochów będzie mieć osobne pule komnat o własnej geometrii, obiektach i rytmie. Wspólne mogą pozostać **role** (np. skarbiec, obejście, obrona przesmyku), ale pokój Katakumb nie powinien trafić do innego lochu tylko po zmianie dekoracji. Produkcyjny katalog i generator muszą umożliwiać dopisywanie kolejnych szablonów bez zakładania, że 16 jest liczbą końcową.

Kształt ma służyć roli. W pierwszej szesnastce przewidujemy przynajmniej: klepsydrę z przewężeniem, nawę rozdzieloną brakiem podłogi, L, krzyż z ramionami osłon, kilka wewnętrznych komór, pierścień wokół pustego środka, skarbiec w kształcie klucza i łamany korytarz. Reszta może być prostsza dla kontrastu i tempa. Nie wypełniać puli samymi kwadratami ani samymi labiryntami.

`DRT_Catacombs_MidBalcony` jest użyteczną referencją geometrii: zapisany footprint tworzy obwód wokół pustego centrum, z czterema kotwami wrogów. Mimo nazwy ten asset nie zapisuje schodów ani powierzchni ponad poziomem 0. Inspirację obwodem można więc wykorzystać w Katakumbach bez wprowadzania balkonów; samego assetu nie należy automatycznie dopinać do produkcyjnej puli bez sprawdzenia drzwi, pułapek i roli.

## Katalog sytuacji

| Typ | Rola i cel gracza | Geometria oraz faktyczna decyzja |
| --- | --- | --- |
| Start — Przedsionek | Rozpoznać kierunek i przygotować szyk. | Bezpieczna przestrzeń z czytelnymi wyjściami; żadnej pułapki ani wymuszonej walki tuż przy wejściu. |
| Zwykły 1 — Wąskie gardło | Przebić się przez obronę. | Sylwetka klepsydry: dwie szersze strefy łączy krótkie przewężenie. Część pasażu jest pułapką, reszta pozwala ostrożnie przejść. Przeciwnicy po dalszej stronie; wejście nie może ich ominąć. |
| Zwykły 2 — Rozdzielona nawa | Wybrać skrzydło natarcia. | Długi ubytek podłogi rozdziela środek na dwa przejścia, połączone na końcach. Gracz wybiera krótszą eksponowaną trasę lub dłuższą osłoniętą. |
| Zwykły 3 — Zakręt krypty | Złamać linię widzenia. | Pokój w kształcie L: brak strzału przez narożnik, bliskie spotkanie dopiero po obejściu. |
| Zwykły 4 — Grobowce | Obejść osłonę przeciwnika. | Krzyż z czterema ramionami, sarkofagami przy ich ujściach i maksymalnie czterema przeciwnikami na odmiennych pozycjach. Środek jest punktem kontaktu, ramiona dają flankę i odwrót. |
| Zwykły 5 — Galeria łuczników | Zamknąć dystans pod ostrzałem. | Długie osie strzału przerwane osłonami po stronie gracza; osłonięte przejście jest dłuższe. Docelowy skład z VS-15 powinien wykorzystywać jednostki dystansowe. |
| Zwykły 6 — Labirynt cel | Odnaleźć i oskrzydlić patrol. | Dwa małe wewnętrzne pomieszczenia i łączniki; jeden przesmyk bezpieczny, drugi z widoczną pułapką. Więcej niż jedna trasa, ale nie pełny widok całej walki od wejścia. |
| Zwykły 7 — Krąg rytualny | Wybrać połowę obwodu. | Płaski pierścień wokół pustego środka, inspirowany geometrią MidBalcony. Jedna połowa obwodu ma jawne pułapki, druga jest bezpieczniejsza, lecz broniona przez przeciwników. Brak skrótu przez pusty środek. |
| Zwykły 8 — Kostnica | Podzielić przeciwników. | Rzędy regałów/nisz tworzą dwa poprzeczne łączniki. Można skupić walkę w jednym przejściu lub przejść drugim i rozdzielić front. |
| Zwykły 9 — Sala sarkofagów | Manewrować wokół centralnego bloku. | Masywne sarkofagi dzielą salę na dwie asymetryczne drogi; jedna ma lepsze osłony, druga lepsze dojście do przeciwnika. |
| Opcjonalny 1 — Skarbiec z pułapką | Zdecydować, czy zapłacić ryzykiem za łup. | Kształt klucza: wąski trzon prowadzi do szerszego skarbca. Jedno krótkie przewężenie z jawną pułapką leży na każdej pieszej trasie od wejścia do skrzyni. Nie tworzyć dywanu wielu kolejnych pułapek ani bocznych drzwi przy skrzyni. |
| Opcjonalny 2 — Mechanizm grobowca | Dotarcie do wymaganej dźwigni. | Dźwignia za zakrętem i pozycjami wrogów; gracz nie aktywuje jej z progu. Obecny status wymaganej odnogi zostaje zachowany. |
| Opcjonalny 3 — Krypta warunku | Spełnić warunek otwierający dalszą trasę. | Warunek umieszczony za zwartą komorą, do której prowadzą dwie drogi o innym stopniu ekspozycji. Obecny status wymaganej odnogi zostaje zachowany. |
| Korytarz 1 — Odetchnienie | Zmienić tempo między walkami. | Krótki, czytelny łącznik z łagodnym zakrętem; żadna pułapka ani zbędna walka. |
| Korytarz 2 — Przełamanie | Przejść przez bronione przejście. | Łamany korytarz z dwoma małymi rozszerzeniami i krótkim przesmykiem między nimi; pułapki zamykają część przesmyku, pozostawiając bezpieczny wąski pas. Zakręt daje moment kontaktu i możliwość wycofania. |
| Finał — Komnata nekromanty | Stoczyć finałową walkę lub dotrzeć do wyjścia w wariancie bez bossa. | Dwie drogi obchodzenia centralnego obszaru, kilka osłon, wolny środek pod zagrożenia obszarowe. Żadne wejście nie daje natychmiastowego dostępu za plecy bossa. Wariant bez bossa zachowuje sensowną drogę do końca wyprawy. |

## Kryteria authoringu i testów

1. Wejście i wyjście są częścią projektu **zbioru dozwolonych krawędzi**, a nie zawsze jednym sztywnym polem. Ich konkretne położenie może się zmieniać między generacjami, o ile wszystkie legalne warianty zachowują rolę pokoju. Przykładowo dla pierścienia przejścia po przeciwnych stronach zachowują wybór górnej lub dolnej drogi; dla skarbca dopuszczone są wyłącznie wejścia od strony wąskiego trzonu. Obrót szablonu zmienia kierunek świata, ale nie zasadę sytuacji.
2. Przedmioty blokujące drogę i pułapki, które definiują rolę, mają stałe wymagane kotwy. Test musi sprawdzać ich obecność **po rozmieszczeniu obiektów**, a nie tylko w `.asset`.
3. Skarbiec przechodzi test grafowy: po usunięciu pól aktywnych pułapek nie istnieje piesza ścieżka od żadnego dozwolonego wejścia do skrzyni; z pułapkami ścieżka istnieje. Analogicznie testujemy fizyczne obejścia luk i przejścia w pokojach taktycznych.
4. Generacja wielu seedów potwierdza połączenia, osiągalność celu i nagród oraz brak pokoju, którego drzwi omijają zamierzony układ. Test obejmuje różne legalne wejścia i wyjścia, nie tylko pierwszy wylosowany wariant. Testy samej walidacji assetu nie wystarczają.
5. Po podłączeniu produkcyjnych mobów w VS-15 sprawdzamy, czy kotwy jednostek wykorzystują osłony, zakręty i dystans. W jednym encounterze może być **od jednego do czterech przeciwników**, najwyżej czterech. Kompozycja musi pasować do roli pokoju; przypadkowe 4v4 w każdej geometrii zatarłoby różnice.
6. Pozycje dla większych składów są jawnie zapisane w szablonie. Jedenaście pokojów bojowych ma cztery kotwy, zakręt krypty trzy, przełamanie dwa; pokoje bez walki nie dostają sztucznych kotew. Dzięki temu czwarty przeciwnik nie ląduje w automatycznej formacji w pokoju przygotowanym na 4v4. Pełne ściany, niskie murki i balustrady są krawędziami `StructuralAnchors`, a nie przeszkodami zajmującymi pole. Szkic `#` określa brak podłogi; sam nie wyraża przechodniego niskiego murku ani balustrady.
6. Pułapki w pokojach taktycznych stoją na przewężeniach lub skrótach. Część może być wymuszona tylko przy opcjonalnej nagrodzie; na głównej trasie powinny najczęściej tworzyć wybór między ryzykiem, czasem i pozycją w walce, zamiast przypadkowo karać za przejście.
7. W smoke należy zagrać przynajmniej jeden skarbiec, dwa różne pokoje taktyczne i finał oraz ocenić, czy wybór jest zrozumiały bez czytania tej dokumentacji.

Inspiracja: [Edmund McMillen opisuje](https://edmundmcmillen.blogspot.com/2011/09/binding-of-isaac-gameplay-explained.html?commentPage=2) w *The Binding of Isaac* pokoje podstawowe o osobnych funkcjach oraz dużą pulę ręcznie zaprojektowanych pokojów walki, nagród, pułapek i małych zagadek, z losowaną zawartością w obrębie pokoju. To zasada wyboru i wariantowania sytuacji, nie wzór na kopiowanie tempa gry akcji do turowego Godmist.
