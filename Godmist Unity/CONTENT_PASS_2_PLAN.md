# Godmist v0.2.0 Content pass 2

Plan wykonawczy 56 kart Trello. Stan ustaleń: 31 października 2026. Branch docelowy: `codex/v0.2.0-content-pass-2`.

Celem jest kompletna, grywalna progresja do L40 z pięcioma biomami, rozwiniętymi klasami, wyposażeniem i gospodarką. Ten plik opisuje zakres przyszłych prac; jego zapis nie rozpoczyna implementacji ani nie zatwierdza otwartych decyzji projektowych. Numery CP2-01–CP2-56 odpowiadają wcześniej zaproponowanym kartom Trello.

## Źródła i reguły wykonania

1. Aktualny `design/content_pass_2.xlsx` w katalogu nadrzędnym projektu jest źródłem kandydackich nazw, statystyk, receptur i przypisań. Po rozpoczęciu karty odczytać jej fragment ponownie: arkusz jest nadal edytowany. Nie kopiować starszych tabel z rozmowy ponad nowsze dane.
2. Obowiązują decyzje użytkownika oraz projektowe `AGENTS.md`, `WORKFLOW.md`, odpowiednie fragmenty `CONVENTIONS.md`, `PROJECT_CONTEXT.md` i `TESTING.md`. Bieżący kod jest źródłem prawdy o działającym systemie. Stare handoffy i content pass 1 są materiałem porównawczym.
3. Przed implementacją ustalić bazę względem worktree v0.1.2. Nie przywracać przypadkiem starych nazw, cen, efektów ani naprawionych błędów. Integracja nie oznacza automatycznego merge całej cudzej, niedokończonej pracy.
4. Zachować stabilne ID i odczyt zapisów v0.1.0 oraz przyjętej bazy v0.1.2. Zapisane instancje przedmiotów i wcześniej przygotowane nagrody nie podlegają ponownemu losowaniu. Zmiany semantyki starej instancji wymagają świadomej decyzji i testu migracji.
5. Kod i testy mogą być zmieniane w ramach zleconej karty. Serialized Unity state tworzyć przez Unity Editor API po autoryzacji właściwej pracy; nie edytować ręcznie YAML, GUID, prefabów ani scen. Gdy wiring wykonuje użytkownik, przygotować dokładny `UNITY_HANDOFF.md`.
6. Każda karta implementacyjna obejmuje potrzebną prezentację, lokalizację/nazwy, ikony lub wskazany zestaw istniejących assetów oraz własny smoke. Karta 55 domyka jakość, nie zastępuje brakującej prezentacji całych systemów.
7. Nową mechanikę realizować w jej istniejącym systemie odpowiedzialnym za stan. Widoki prezentują wynik; nie liczą ponownie kosztów, szans ani efektów. Rozszerzać wspólne kontrakty tylko w zakresie rzeczywistych potrzeb zatwierdzonego contentu.
8. Każda karta kończy się self-review, adekwatnymi testami, opisem wyniku i handoffem, jeśli potrzebny. `READY_FOR_SMOKE` nie oznacza akceptacji użytkownika. Commit i push dopiero po jego `git`.
9. Katalog i mapa źródeł CP2-02: `CONTENT_PASS_2_CATALOG.md` oraz `Tools/ContentPass2/content-manifest.json`. Właściwe karty używają zarezerwowanych ID i jawnych decyzji źródłowych, a zmiany arkusza porównują z hashem manifestu. Nie generować nowego ID tylko dlatego, że poprawiono polską nazwę.

### Wspólne kryteria ukończenia

- Dane mają jednoznaczne ID, kompletne referencje i zgodne jednostki. Krótkie opisy efektów korzystają ze wspólnych formatterów.
- Błędy danych są wykrywalne przed grą; brakujące referencje nie są maskowane cichymi fallbackami.
- Zmiany ekonomiczne nie tworzą utraty zasobów przy nieudanej transakcji ani powtarzalnego odbierania nagród.
- Weryfikacja jest proporcjonalna: inspekcja danych, kompilacja kodu, celowane EditMode; PlayMode dla lifecycle, ruchu, prezentacji i integracji, jeśli wymagane. Pełna regresja dopiero w karcie 54.
- Nie dodawać testów wyłącznie powtarzających wpisane stałe. Sprawdzać inwarianty, granice, składanie mechanik i rzeczywiste ryzyka regresji.

## Zakres i granice

W zakresie: Elfie Ruiny, Morze, Bagno; 21 zwykłych przeciwników i sześciu nowych bossów z fazami; T5 i trzecie drzewka zgodnie z zamkniętym projektem; surowce, crafting, alchemia, biżuteria, galduryty i wyposażenie L21–40; unikaty; płatny reset u kapłana; balans, zapisy i wydanie.

Poza automatycznym zakresem: Pustynia, Świątynia i Góry jako grywalne lochy; całość Dungeon v4 lub Combat v5; nowe grywalne postacie; ich osobiste traity i specjalizacje node'ów; pełne systemy pustyni i lawy; tier 5 katalizatorów powyżej L40; T6/T7 Zbrojnego bez decyzji. Kontrolowany RNG pozostaje pakietem opcjonalnym, rozstrzyganym w CP2-47.

Nie ustalono jeszcze ostatecznej krzywej XP, progów odblokowania, wzoru opłaty za reset, pełnych efektów unikatów ani wszystkich pokojów i prób. Te decyzje należą do wskazanych niżej kart. Ich brak nie blokuje zapisania planu.

## Kolejność i zależności

Numery są identyfikatorami, nie bezwzględną kolejnością wykonania. „Zależności” oznaczają warunki rozpoczęcia implementacji; wyraźnie opisane warunki domknięcia mogą dojść później.

| Etap | Karty | Wynik |
| --- | --- | --- |
| A | 01–04 | Baza, katalog, rozpoznane braki systemowe, kontrakt progresji |
| B | 16–22 i 05–12 | Wyposażenie oraz rozwój klas w zakresie już zaprojektowanym |
| C | 23–26 | Wspólne mechaniki wymagane przez konkretne jednostki i areny |
| D | 27–31, następnie 32–36, następnie 37–41 | Kolejno grywalne wycinki nowych biomów |
| E | 42–46 oraz 15 | Unikaty, pełne źródła surowców, gospodarka i obsługa buildów |
| F | 13–14 | Domknięcie Paladyna po dostarczeniu projektu przez użytkownika |
| G | 47, opcjonalnie 48–51 | Decyzja o RNG i wyłącznie zatwierdzone integracje |
| H | 52–56 | Końcowy balans, regresja, jakość i przygotowanie wydania |

To preferowana kolejność pracy jednej osoby. Projekt Strażnika i unikatów warto przygotować wcześniej, zanim ich brak zablokuje integrację. Reset można wdrożyć wcześniej, gdy kontrakt SP jest zamknięty. Karty klas i rzemiosła można przeplatać. Nie trzeba kończyć wszystkich nowych klas, aby testować pierwszy biom. Produkcyjne udostępnienie całego passu wymaga domknięcia jego obowiązkowych elementów.

Duże karty, zwłaszcza 06, 11, 19, 25 i karty bossów, dzielić wykonawczo na: kontrakt mechaniki, dane, integrację i testy. Jeśli audyt 03 wykaże odrębny duży system, wydzielić podkartę z własnym zakresem; nie ukrywać przebudowy walki w karcie dodającej dane.

## Karty przygotowawcze

### CP2-01 Ustalić bazę v0.2.0 i integrację poprawek v0.1.2

**Stan 2026-10-31: ZAAKCEPTOWANA, smoke PASS użytkownika; commit/push zlecony przez `git`.** Gotowa baza Windows przeniesiona do working tree i zweryfikowana: 1323 różne zwykłe testy regresji PASS po korekcie czterech starych kontraktów tekstów; końcowy retest 19/19 PASS. Zakres: siedem commitów źródłowych do `a35f09ec8` oraz niezacommitowane poprawki ścian, ruchu, obrysów i sortowania. Zgodnie z decyzją użytkownika macOS i pad X360 przejdą później z v0.1.3 i nie blokują CP2-01. Manifest, pierwotne wyniki failure i szczegóły: `CONTENT_PASS_2_INTEGRATION.md`. Manualny smoke PASS; nowy build Windows NOT RUN. Wersja aplikacji bez zmian.

**Zależności:** brak.

**Zakres:** porównać zatwierdzony stan v0.1.2 z branchem passu 2; wskazać poprawki, balans i dane, które muszą wejść przed nowym contentem. Zanotować commity bazowe i lokalne zmiany. Nie wykonywać zbiorczego merge niezaakceptowanej pracy ani porządkowania cudzych plików.

**Przygotować:** listę zaakceptowanych zmian v0.1.2, rozbieżności katalogów, baseline zapisów i krótki smoke obecnych dwóch biomów. Szczególnie sprawdzić biżuterię, Jadeit, katalizatory, nazwy alchemii i przeliczenia siły efektów.

**Implementacja:** wybrać konkretny sposób integracji po sprawdzeniu historii Git; przenosić spójne zatwierdzone zmiany i rozwiązać konflikty semantyczne w danych. Zapisać wynikową bazę dla kolejnych kart. Jeśli v0.1.2 nadal powstaje, najpierw zamknąć listę wymaganych zależności, bez udawania zakończonej integracji.

**Odbiór:** bazowa gra działa jak przyjęte v0.1.2; brak zgubionych poprawek i unrelated zmian w diffie; stare zapisy ładują się. Wynik to jednoznaczna baza, nie samo porównanie branchy.

### CP2-02 Zamknąć katalog contentu i mapę źródeł L1–40

**Stan 2026-10-31: ZAAKCEPTOWANA — przegląd katalogu zakończony, commit/push zlecony przez `git`.** Manifest, rejestr proponowanych ID, tabela `|` i niezależny walidator przygotowane. Pokrycie planu: 406/406 pozycji, w tym 370 materiałów; 30 kontroli PASS. Zapisano decyzje użytkownika: tylko skóra leoparda w Ruinach, Dobry/Mocny proch w pięciu biomach, nowe kamienie bossów L1–40. Brak zmian XLSX, kodu C# i serialized state. Projekty Paladyna, Strażnika, T6/T7, efekty unikatów i wskazane szczegóły receptur pozostają u właścicieli swoich kart. Podsumowanie: `CONTENT_PASS_2_CATALOG.md`.

**Zależności:** 01.

**Zakres:** spis nowych i zmienianych ID przedmiotów, receptur, przeciwników, bossów, drzewek oraz źródeł łupu. Rozróżnić materiał naturalny, zastępczy, produkt craftingu i legacy. Zachować pokrycie wszystkich potrzebnych materiałów przy dostępnych pięciu biomach.

**Przygotować:** tabelę ID → nazwa → wymagany poziom → odbiorcy → źródła i przedziały; osobno listę brakujących projektów. Starsze lokalne tabele porównać z aktualnym arkuszem, nie przepisywać ich bez sprawdzenia.

**Implementacja:** przygotować manifest i walidator referencji/pokrycia, korzystający z istniejących katalogów. Nie nadawać nowego ID tylko z powodu poprawionej nazwy. Dla `item.material.elf_ear` i `item.vs10.ucho_elfa` ustalić canonical używany przez receptury i nowy drop, zachowując odczyt legacy.

**Odbiór:** każdy wymagany materiał ma osiągalne źródło lub wykonalny łańcuch craftingu; usunięty Pył energetyczny nie trafia do nowych pul; oba bossy Lasu dają Żywioł lasu. Cztery późne materiały zastępcze mają osobny przedział L31–40.

### CP2-03 Przeprowadzić audyt gotowości systemów

**Zależności:** 01–02.

**Zakres:** dla każdej nowej mechaniki z przypisanych skilli, traitów i bossów określić: działa teraz, wymaga rozszerzenia danych, wymaga kodu, wymaga decyzji. Audyt ma odpowiedzieć na konkretne wymagania arkusza, bez mapowania całego projektu.

**Przygotować:** macierz mechanika → istniejący system → karta właścicielska → brak → test ryzyka. Objąć zasięg i AoE, reakcje pasywek, summon, fazy, teleport, teren, wielopolowość, indestructible, skalowanie i zapis stanu.

**Implementacja:** w tej karcie wyłącznie analiza i projekt kontraktów. Punkty odniesienia: systemy efektów i pasywek, `CombatMovementSystem`, istniejące systemy czterech bossów i struktury drzewek. Mechanikę współdzieloną przypisać jednemu właścicielowi, żeby nie powstała oddzielna wersja w każdym bossie.

**Odbiór:** każda wymagana mechanika ma właściciela i zależności; wiadomo, które z kart 23–26 są naprawdę potrzebne przed konkretnym biomem. Duże braki otrzymują jawne podkarty i ocenę wpływu na save.

### CP2-04 Ustalić kontrakt progresji do L40

**Zależności:** 01–03.

**Zakres:** poziomy i XP L21–40, przyrosty statystyk i SP, dostęp do tierów drzewek, biomów, receptur, usług i nagród. Zdefiniować rolę poziomu postaci, wyprawy, bossa i przedmiotu jako odrębnych wielkości.

**Przygotować:** tabelę progów L1–40, plan odblokowań i przykładowe ścieżki rozwoju wszystkich klas. Zaznaczyć poziomy 21, 25, 30, 31, 35 i 40 oraz koszt dojścia do głównych buildów. Formuły i tempo zatwierdzić przed wdrożeniem.

**Implementacja:** rozszerzyć istniejące dane i `CharacterProgressionSystem`/`SkillTreeProgressionSystem`; usunąć tylko rzeczywiste ograniczenia L20. Nie rozrzucać nowego limitu po UI i usługach. Zachować już zdobyty XP/SP, a ewentualne wyrównanie starych postaci opisać jawnie.

**Odbiór:** testy granic poziomów i progów, brak darmowych SP po powtórnym loadzie, osiągalne odblokowania; L40 zachowuje ustaloną regułę dalszego XP. Finalne strojenie liczb należy do 53.

## Klasy i rozwój postaci

### CP2-05 Dodać T5 Dowódcy i Awanturnika

**Zależności:** 03–04.

**Zakres:** po pięć node'ów T5 obu istniejących drzewek Wojownika z arkusza `Drzewka`, wraz z ich prerequisite, rankami, kosztami i przypisanymi efektami. Nie przebudowywać T1–4 poza konieczną zgodnością i zatwierdzonym balansem.

**Przygotować:** specyfikację dziesięciu node'ów, porównanie istniejących efektów, zasady składania buffów i triggerów, ikony oraz układ rozszerzonego drzewa. Brakujące operacje efektów wyłapać przed authoringiem.

**Implementacja:** rozszerzyć `SkillTreeDefinition`, istniejące definicje skilli/pasywek i authoring przez `GodmistSkillTreeSetup`; nowe zachowania dopisać do właściwych processorów, nie do widoku drzewa. Zachować ID i zakupione rangi T1–4.

**Odbiór:** każdy node jest osiągalny dopiero po właściwym progu, działa na wszystkich rankach i znika po resecie; testy triggerów oraz przykład buildu każdego drzewa w walce. Zapis sprzed T5 zachowuje wcześniejszy rozwój.

### CP2-06 Dodać drzewko Zbrojnego

**Zależności:** 03–04; rozstrzygnięcie zakresu T6/T7.

**Zakres:** trzecie drzewko Wojownika wraz z pasywem i zatwierdzonymi tierami. Arkusz zawiera 30 opisanych pozycji, również T6 i T7; nie traktować ich obecności jako automatycznej zgody na rozszerzenie poza przyjęte progi L40.

**Przygotować:** zamkniętą listę node'ów i ich połączeń, budżet SP, znaczenie pasywu, kontrakty nowych efektów oraz layout trzeciego drzewa. Potwierdzić, czy T6/T7 wchodzą teraz, są przeskalowane, czy pozostają zablokowane poza wydaniem.

**Implementacja:** dodać drzewko do istniejącej klasy i progresji, bez tworzenia nowej postaci. Współdzielić mechaniki z pozostałymi drzewami; aktualizować UI i walidację grafu. Podział wykonawczy: model i mechaniki → node'y → integracja → testy buildów.

**Odbiór:** cały zatwierdzony graf jest osiągalny zgodnie z SP i level gatingiem; brak wycieków bonusów przy zmianie ekwipunku/resetach; stary Wladimir otrzymuje dostęp bez utraty istniejących punktów.

### CP2-07 Dodać T5 Skrytobójcy i Tancerza Ostrzy

**Zależności:** 03–04.

**Zakres:** dziesięć node'ów T5 dwóch istniejących drzew Zwiadowcy, z pełnym działaniem w obecnej kolejności zdarzeń walki. Nie dodawać przy tej okazji osobistych traitów Jerzego.

**Przygotować:** dla każdego node'a warunek aktywacji, źródło obrażeń, liczbę triggerów, czas trwania i zasady kumulacji. Szczególnie sprawdzić interakcje z krytem, wieloma trafieniami, niewidzialnością i efektami okresowymi, o ile występują w jego definicji.

**Implementacja:** nowe dane w istniejącym grafie; potrzebne operacje we wspólnym wykonaniu skilli/pasywek. Oddzielić zdarzenie ataku, trafienia i zadania obrażeń, aby jedna akcja nie naliczała bonusu wielokrotnie.

**Odbiór:** testy dodatnie i ujemne warunków oraz wygaśnięcia efektu; dwie reprezentatywne konfiguracje w walce; poprawne preview kosztów i rang. Nowy efekt nie pozostaje po resecie ani wczytaniu innego buildu.

### CP2-08 Zaprojektować docelowego Strażnika

**Zależności:** 03–04.

**Zakres:** zamknąć projekt trzeciego drzewa Zwiadowcy. Obecny zestaw 26 pozycji i notatka o reworku w kierunku Defense + Utility oraz Impetu wymagają decyzji; nie implementować obu wersji równolegle.

**Przygotować:** tożsamość drzewa, definicję Impetu, sposób zdobywania/wydawania i resetu zasobu, limity, utratę poza walką, pełną listę node'ów, koszty, rangi i połączenia. Pokazać dwa przykładowe buildy oraz sens wyboru względem pozostałych drzew.

**Sposób realizacji:** dokument projektowy i aktualizacja uzgodnionego arkusza przez użytkownika lub w osobno zleconej edycji. Zweryfikować wykonalność na systemie pasywek i ocenić potrzebę nowego runtime state, zamiast dopisywać ogólny framework zasobów.

**Odbiór:** nie ma sprzecznych wersji node'ów ani nieokreślonej jednostki wartości; każdy efekt ma techniczne odwzorowanie i testowalny warunek. Wynik jest kompletną specyfikacją dla 09, bez zmian gameplayowych.

### CP2-09 Wdrożyć drzewko Strażnika

**Zależności:** 08 i wymagane rozszerzenia z 03.

**Zakres:** wyłącznie zatwierdzona wersja trzeciego drzewa, w tym Impet, jeśli pozostaje w projekcie, jego prezentacja i integracja z dwoma pozostałymi drzewami.

**Przygotować:** identyfikatory node'ów, ikony, zestaw scenariuszy budowania/wydawania zasobu, zasady trwałości stanu w walce i po niej. Spisać politykę save/load zamiast przypadkowego zapisywania cache obliczeń.

**Implementacja:** stan mechaniki w runtime postaci/walki, reguły w systemach efektów; drzewo w istniejących definicjach. UI odczytuje stan i formatuje krótką nazwę/wartość. Dodać reset oraz ponowne wyliczanie zależnych statystyk bez podwajania bonusów.

**Odbiór:** testy granic i kolejności triggerów, przekroczenia limitów, zakończenia walki, śmierci i resetu. Stary Jerzy może rozwinąć nowe drzewo; istniejące buildy i przypisane skille zachowują poprawny stan.

### CP2-10 Dodać T5 Czarnoksiężnika i Maga Ognia

**Zależności:** 03–04.

**Zakres:** dziesięć node'ów T5 istniejących drzew Maga; pełne działanie z maną, obrażeniami bezpośrednimi, efektami okresowymi i przywołaniami w zakresie faktycznie opisanym przez node'y.

**Przygotować:** dla efektów reaktywnych wskazać źródło, odbiorcę i moment triggera; rozstrzygnąć snapshot lub bieżące skalowanie tam, gdzie różnica wpływa na wynik. Zdefiniować stosowanie odporności i ownership obrażeń jednostek przywołanych.

**Implementacja:** dane T5 plus minimalne rozszerzenia istniejących processorów. Nie kopiować logiki DOT ani summon do node'ów. Wspólne obliczenia powinny zasilać tooltip i faktyczny wynik.

**Odbiór:** testy kosztów i przywracania many, czasu trwania i składania efektów oraz brak samonapędzających się pętli triggerów. Smoke obu drzew na celu podatnym i odpornym; poprawny reset i stary zapis Alison.

### CP2-11 Dodać drzewko Maga Mrozu

**Zależności:** 03–04.

**Zakres:** trzecie drzewko Maga zgodne z arkuszem, wszystkie przyjęte node'y i pasyw, interakcje zimna/kontroli oraz ich prezentacja. Liczbę node'ów ustalić z aktualnej wersji arkusza, bez kopiowania liczby Zbrojnego.

**Przygotować:** pełny graf, progi SP, słownik statusów i ich odporności; dla każdej nowej kontroli: nałożenie, odświeżenie, przełamanie, immunitet i koniec tury. Zidentyfikować mechaniki wspólne z bossami i odpornościami.

**Implementacja:** najpierw kontrakty brakujących statusów, potem dane drzewa i powiązanie z klasą. Efekty kontroli muszą być respektowane przez wspólną legalność akcji i AI. Ikony/status HUD korzystają z istniejącego modelu prezentacji.

**Odbiór:** testy odporności i nakładania wielu źródeł kontroli, brak nieskończonej blokady bez projektu, pełny reset. Co najmniej konfiguracja głównie mroźna i mieszana działa na zwykłych przeciwnikach i bossie.

### CP2-12 Dodać T5 Kapłana

**Zależności:** 03–04.

**Zakres:** pięć opisanych node'ów T5 Kapłana. Ta część Paladyna może wejść wcześniej; nie uzależniać jej od niezaprojektowanego trzeciego drzewa i pustego T5 Krzyżowca.

**Przygotować:** doprecyzować targetowanie sojuszników, leczenie, przeleczenie, tarcze i czas trwania wyłącznie dla efektów obecnych w node'ach. Sprawdzić, jak składają się z obecną alchemią i pasywkami.

**Implementacja:** rozbudować istniejące definicje i procesory wsparcia. Przeliczanie efektów działa w tym samym pipeline dla bohatera i legalnego sojusznika; UI nie tworzy własnej wersji reguł.

**Odbiór:** testy pełnego HP, martwego/niedozwolonego celu, wielu źródeł efektu i resetu; reprezentatywny build Reimonda. Brakujący rozwój Paladyna pozostaje jawnie zaplanowany w 13–14, bez pustych aktywnych node'ów.

### CP2-13 Zaprojektować brakujący rozwój Paladyna

**Zależności:** 04; końcowa część projektowa klas zgodnie z decyzją użytkownika.

**Zakres:** pięć pustych node'ów T5 Krzyżowca (`Drzewka`, dotychczas wiersze 130–134) i całe trzecie drzewko, obecnie placeholder o powtórzonej nazwie w wierszach 136–161. Nie uznawać placeholdera za docelową nazwę ani liczbę node'ów.

**Przygotować:** użytkownik dostarcza tożsamość i efekty; wspólnie zamknąć nazwę, graf, rangi, koszty, skalowanie, zasób i relacje z Kapłanem/Krzyżowcem. Dodać przykłady buildów L40 oraz listę nowych wymaganych mechanik.

**Sposób realizacji:** sprawdzić projekt względem kontraktu SP i gotowych systemów, wskazać konflikty i skorygować specyfikację przed kodem. Nie dopowiadać własnych umiejętności w pustych komórkach.

**Odbiór:** wszystkie pozycje mają testowalne efekty i miejsce w grafie; brak nieopisanych progów i powielonych nazw drzew. Wynik odblokowuje 14; karta może pozostać oczekująca, gdy pozostały pass jest wdrażany.

### CP2-14 Wdrożyć brakujący rozwój Paladyna

**Zależności:** 12–13, wymagane mechaniki z 03.

**Zakres:** zatwierdzone T5 Krzyżowca i trzecie drzewo, ich skille, pasywy, UI, dane i integracja ze starym Reimondem. Brak nowej postaci i osobistych traitów.

**Przygotować:** manifest node'ów/efektów, authoring i zestaw interakcji między trzema drzewami, w szczególności zdarzenia wspólne z istniejącymi efektami wsparcia.

**Implementacja:** analogicznie do 05–12, z zachowaniem stabilnych ID i wspólnego silnika efektów. Nowy stan mechaniki dodać tylko tam, gdzie wymaga go projekt 13; nie przebudowywać przy okazji modelu wszystkich klas.

**Odbiór:** wszystkie trzy drzewa mają grywalne konfiguracje i poprawny reset; stary zapis zachowuje dotychczasowy rozwój. Testy specyficznych triggerów, kosztów i ograniczeń oraz smoke buildów ofensywnego i wspierającego. Gotowość tej karty jest warunkiem końcowego balansu klas.

### CP2-15 Dodać płatny reset umiejętności u kapłana

**Zależności:** 04; kontrakty resetowania nowych mechanik z kart klas.

**Zakres:** usługa NPC zwracająca wydane SP za opłatą zależną od liczby zwracanych punktów. Ustalić, czy reset dotyczy wszystkich drzew naraz, oraz zatwierdzić wzór kosztu; nie zakładać ceny z samej notatki w arkuszu.

**Przygotować:** regułę kosztu, dostępność usługi i UX podglądu/wykonania. Określić postępowanie z aktywnymi skillami na pasku, ich cooldownami, bonusami pasywnymi i nowymi zasobami. Brak wydanych SP i brak złota to jawne przypadki.

**Implementacja:** jedna transakcja domenowa przez progresję i ekonomię NPC: walidacja → pobranie opłaty → refund → usunięcie pochodnych efektów → aktualizacja dostępnych akcji. Widok usług NPC prezentuje ten sam koszt i wynik; nie resetuje bezpośrednio pól danych.

**Odbiór:** brak częściowego resetu/utraty złota po błędzie, brak duplikacji SP i pozostawionych pasywów. Testy powtórzenia operacji i save/load; smoke resetu buildu z każdego drzewa, domknięty po dodaniu ostatniej klasy.

## Przedmioty i rzemiosło

### CP2-16 Dodać surowce i receptury rzemieślnicze L21–40

**Zależności:** 02–04.

**Zakres:** nowe materiały bazowe, przetworzone i receptury dla przedziałów 21–30 i 31–40 z `Nowe przedmioty` oraz `Nowe wyposażenie`. Włączyć rudy annodytu, srebra, kobaltu i złota; nie pomijać ich przez starszą wersję arkusza. Receptury alchemiczne realizuje 19.

**Przygotować:** graf surowiec → półprodukt → część, progi, koszty, nazwy, ikony, dostępność usług i kanoniczne ID. Ujednolicić aktualną nazwę wielbłądziej skóry i igieł; literówki nie tworzą nowych przedmiotów.

**Implementacja:** rozszerzyć istniejące definicje i katalogi craftingu przez idempotentny authoring. Walidować brak cykli bez źródła wejściowego, brak receptury zużywającej nieosiągalny produkt i zgodność jednostek ilości.

**Odbiór:** każdy półprodukt da się wykonać z osiągalnych zasobów na właściwym poziomie; transakcje nie gubią składników przy odmowie. Testy niedoborów, progów i końcowych łańcuchów; nowe materiały mają przypisane źródła w 02/46.

### CP2-17 Dodać wyposażenie L25 i L35

**Zależności:** 04, 16.

**Zakres:** zestawy części broni i pancerza L25 oraz L35 z `Nowe wyposażenie`: obecnie 96 części broni i 96 pancerza łącznie, po 48 każdego rodzaju na tier. Liczebność ponownie sprawdzić przed authoringiem.

**Przygotować:** macierz slot/typ/materiał/tier, statystyki bazowe, receptury i wymagania; zweryfikować kompletność wszystkich przewidzianych archetypów, w tym nazwę Sułtańskiej blachy. Określić istniejące wizualne reprezentacje lub brakujące assety.

**Implementacja:** użyć aktualnego systemu generowania/craftingu i definicji części. Rozszerzyć zakres generowania zamiast tworzyć osobne warianty algorytmu na L25/L35. Zmiany formuł obejmujące stare przedmioty wymagają osobnej świadomej decyzji.

**Odbiór:** każdy nowy typ można wytworzyć i założyć, preview zgadza się z wynikiem, bonusy znikają po zdjęciu. Testy granic level i slotów, reprezentatywne kombinacje; stare wyposażenie zachowuje zapisane parametry.

### CP2-18 Dodać biżuterię L30 i L40 oraz nowe klejnoty

**Zależności:** 01, 04, 16.

**Zakres:** metale L30: annodyt, srebro, kobalt, złoto; L40: kryferyt, mosiądz, stal sułtańska, tytan. Dodać odpowiadające warianty biżuterii i klejnoty wskazane w `Biżuterie i Galduryty`, zachowując bieżący Jadeit i Obsydian.

**Przygotować:** wartości efektów, kategorie, unikalność, ceny, receptury i źródła kamieni; odróżnić procent od punktów/flat. Aktualne wartości bazy v0.1.2 porównać z propozycją, nie przywracać passu 1. Każda rozbieżność powinna być świadomą zmianą balansu.

**Implementacja:** rozszerzyć `JewelryItemDefinition`, `JewelryGemDefinition`, `JewelryGenerationService` i `JewelryCraftingService` zgodnie z istniejącym ownership. Formatter efektów wspólny dla craftingu, inventory i statystyk.

**Odbiór:** poprawne tierowanie i losowanie, craft i equip, brak mnożenia premii po loadzie; testy nowych efektów i progu L30/L40. Źródła klejnotów obecne w dostępnych biomach, nie tylko w przyszłym lochu lub sklepie.

### CP2-19 Rozszerzyć alchemię o efekty B/C i katalizatory

**Zależności:** 01–04, 16.

**Zakres:** dostępne do L40 warianty mikstur i bomb B/C, składniki, receptury oraz katalizatory odpowiednich tierów. Arkusz: `Nowe przedmioty`, siła C w R, siła B w S, materiały B w T/U, materiał C w V, czas w W. Tier 5 poza limitem pozostaje wyłączony.

**Przygotować:** tabelę efekt → jednostka → siła → czas → koszt → składniki → próg. Rozdzielić całkowitą regenerację od wartości na tick oraz siłę bomby przed/po alchemical power. Zatwierdzić zachowanie katalizatorów czasu i zasięgu na bazie aktualnego runtime, nie starego opisu passu 1.

**Implementacja:** istniejące `AlchemyRuntimeRules`, `AlchemyCraftingService` i definicje receptur; rozszerzyć katalog oraz obsługę rzeczywiście nowych efektów. Produkty przechowują parametry zgodnie z obecnym modelem instancji; nie zmieniać znaczenia starych butelek ukrytą migracją.

**Odbiór:** testy reprezentantów instant/regen/status/AoE, B/C oraz każdego nowego katalizatora; preview i wynik zgodne. Receptury da się ukończyć bez przyszłych biomów. Sprawdzić toksyczność, zaokrąglenia i odświeżanie efektu.

### CP2-20 Dodać Miksturę Oczyszczenia

**Zależności:** 19.

**Zakres:** efekt opisany w `Nowe przedmioty!I160`: usuwa wszystkie efekty mikstur i ustawia toksyczność na 0. Receptura L31: dobry alkohol oraz Proch światła, zgodnie z bieżącymi ilościami arkusza. To nie jest usunięcie wszystkich buffów i debuffów postaci.

**Przygotować:** listę efektów mających pochodzenie mikstury, w tym wieloskładnikowe i okresowe; ustalić kolejność użycia, naliczenia własnej toksyczności i jej zerowania, jeśli obecny kontrakt nie rozstrzyga wyniku.

**Implementacja:** korzystać z identyfikacji źródła efektu i wspólnego lifecycle. Usunąć efekty mikstur wraz z zależnymi modyfikatorami, bez kasowania pasywów, ekwipunku, statusów wroga i bomb. Nie odwracać już wykonanego natychmiastowego leczenia.

**Odbiór:** końcowa toksyczność wynosi 0; usunięte są wszystkie aktywne efekty mikstur, pozostałe źródła zachowane. Test mieszanego zestawu efektów, pustego stanu, ponownego użycia i odtworzenia statystyk po save/load.

### CP2-21 Dodać galduryty L21

**Zależności:** 04, 16; 18 przy wspólnych nowych materiałach.

**Zakres:** rozszerzenie istniejącego systemu do galdurytów L21 z trzema/czterema efektami, bez automatycznego dodawania nowych rodzajów efektu. Przedmiot przyjmujący musi spełniać wymaganie poziomu galdurytu.

**Przygotować:** zatwierdzić rozkład liczby efektów, siły, koszty, receptury i generowanie oraz interpretację wymaganego poziomu hosta. Ustalić, czy i jak występują powtarzające się efekty. Nie uznawać samego zakresu „3–4” za gotowy rozkład.

**Implementacja:** rozszerzyć `GalduryteRuntimeRules`, crafting, socketing i extraction. Wspólna walidacja hosta działa w każdej ścieżce, również przy loadzie i alternatywnych ekranach; zachować obecne ID efektów i format instancji, jeśli wystarcza.

**Odbiór:** testy host L20/L21, trzech/czterech efektów, montażu, ekstrakcji i odtworzenia bonusów. Brak obchodzenia wymagań, utraty kamienia w odmowie i ponownego losowania zapisanej instancji.

## Odporności teren i cechy

### CP2-22 Wprowadzić aktualizację odporności przeciwników

**Zależności:** 02–03.

**Zakres:** porównać `Aktualizacja odporności` z obecnymi definicjami i zmienić istniejące oraz zatwierdzone nowe jednostki. Arkusz zawiera też przyszły content; jego obecność nie rozszerza wydania o kolejne biomy.

**Przygotować:** diff jednostka/typ odporności/stara wartość/nowa wartość i jednoznaczne jednostki. Oddzielić odporność na obrażenia, status, przesunięcie i wartości pancerza. Sprawdzić, czy opisy bossów nie wprowadzają sprzecznych wyjątków.

**Implementacja:** aktualizacja właściwych danych przez Editor API; rozszerzenie `ResistanceSet` i obliczeń tylko przy realnym braku z 03. Jedno źródło wartości dla walki i tooltipów; nie dodawać wyjątków nazw jednostek w kalkulatorze obrażeń.

**Odbiór:** test neutralnej, dodatniej/ujemnej odporności i immunitetu, jeśli projekt je przewiduje; reprezentatywne stare i nowe jednostki. Brak przypadkowej zmiany jednostek nieobjętych passem, a nowe phase modifier nie nadpisują trwale danych bazowych.

### CP2-23 Wdrożyć wodę i błoto

**Zależności:** 03; reguły odporności z 22, jeśli wykorzystywane.

**Zakres:** teren potrzebny Morzu i Bagnu. Według arkusza woda: podwójny koszt ruchu, brak ataku, unik −25%; błoto: podwójny koszt ruchu i unik −50%. Potwierdzić jednostkę modyfikatorów uniku i wyjątki przed kodem.

**Przygotować:** kontrakt wejścia/wyjścia, końca tury, teleportacji, wymuszonego przesunięcia i footprintu; oznaczenia graficzne, koszt preview i lista jednostek ignorujących teren. Ustalić, czy zakaz ataku obejmuje wszystkie akcje ofensywne i z jakiego pola oceniany jest wielopolowy aktor.

**Implementacja:** dane terenu współdzielone przez eksplorację, walkę, pathfinding, legalność akcji i AI. Efekty wyliczać ze stanu pozycji, usuwać po wyjściu; nie zakładać, że zawsze zaszło zdarzenie zwykłego kroku.

**Odbiór:** testy ścieżki z ograniczonym budżetem, wejścia/wyjścia, teleportu i immunitetu; preview zgodne z wykonaniem. Nie dodawać w tej karcie kompletnej pustyni i lawy.

### CP2-24 Wdrożyć cechy poruszania jednostek

**Zależności:** 03, 23 dla interakcji z terenem.

**Zakres:** cechy faktycznie przypisane jednostkom passu: latanie, lewitacja, niematerialność i właściwe ignorowanie terenu. Implementować semantykę danych, nie osobne wyjątki dla nazw przeciwników.

**Przygotować:** macierz przeszkód niskich, ścian/drzwi, przepaści, pułapek i jednostek; zasady legalnego końca ruchu. Dla latania zamknąć zmianę trybu kosztem ruchu, −20% odporności przesunięcia i regułę zasięgu względem nielatających, aby AI i melee nie interpretowały jej różnie.

**Implementacja:** profil możliwości ruchu konsumowany przez pathfinding, koszt, targetowanie i wykonanie. Niematerialność pozwala przejść przez określone blokady, ale koniec wymaga legalnego wolnego miejsca. Widok pokazuje tryb, nie zarządza kolizją.

**Odbiór:** testy każdego typu bariery i niedozwolonego końca ruchu, interakcji z wodą/błotem, zmiany trybu i zasięgu. Brak wejścia w ścianę na stałe i rozbieżności między AI a graczem.

### CP2-25 Wdrożyć jednostki wielopolowe

**Zależności:** 03, kontrakty ruchu 23–24.

**Zakres:** footprint 2×2 i 3×3 dla jednostek wymagających cech Great/Huge, w eksploracji/arenie w zakresie ich faktycznego występowania. To osobna zmiana modelu przestrzennego, nie tylko skalowanie sprite'a.

**Przygotować:** definicję anchor, zbioru zajętych pól, odległości, wejścia w teren, spawn/teleport, pchnięcia, AoE i wąskich przejść. Ustalić trafienie jednego aktora raz przy AoE obejmującym kilka jego pól i sposób liczenia zasięgu od footprintu.

**Implementacja:** wspólne zapytania o zajętość i legalność całego footprintu; aktualizować wszystkie pola atomowo. Zintegrować pathfinding, targetowanie, placement, AI i renderer, bez fałszywych dodatkowych aktorów na sąsiednich polach.

**Odbiór:** testy wąskiego korytarza, brzegu mapy, blokady części footprintu, teleportu, AoE i śmierci. Jednostka nie przenika drzwi ani nie dostaje wielokrotnych obrażeń od jednego efektu bez jawnej reguły projektu.

### CP2-26 Wdrożyć pozostałe cechy wymagane przez nowy content

**Zależności:** 03; 22–25 tylko dla konkretnych powiązań.

**Zakres:** nieruchomość, niezniszczalność oraz pozostałe rzeczywiście przypisane cechy z `Cechy przeciwników`, których nie obejmują 24–25. Nie wdrażać całej listy przyszłych cech prewencyjnie.

**Przygotować:** tabelę cecha → jednostki → skutki dla akcji/AI/końca walki. Dla niezniszczalności: zakaz targetowania i obrażeń oraz zniknięcie po śmierci wszystkich jednostek bez tej cechy. Doprecyzować wpływ sojuszu, summonów i pustej strony.

**Implementacja:** wspólne predykaty legalności i zakończenia encountera; odróżnić zakaz dobrowolnego ruchu od odporności na przemieszczenie. Cechy nie powinny być kolejnymi buffami usuwanymi przez zwykłe cleanse, jeśli są stałą własnością jednostki.

**Odbiór:** brak softlocka walki z samą niezniszczalną jednostką, brak martwej tury AI nieruchomego przeciwnika, zgodne tooltipy i targetowanie. Testy zakończenia walki, summonów i obrażeń pośrednich.

## Elfie Ruiny

### CP2-27 Zaprojektować i zbudować pokoje Elfich Ruin

**Zależności:** 03–04; odpowiednie mechaniki 24–26 przed końcowym authoringiem.

**Zakres:** tożsamość biomu, moduły pokojów i łączników, wejścia/wyjścia, eksploracja, miejsca encounterów, bossów i nagród oraz próby projektowane na bieżąco. Nie przejmować całego backlogu Dungeon v4.

**Przygotować:** funkcjonalny graybox, paletę assetów, skalę z kamery gry, rozmiary przejść dla przypisanych jednostek, warianty start/normalny/boss i małą specyfikację każdej faktycznej próby: cel, reset, nagroda, failure. Liczba modułów wynika z zatwierdzonego zestawu, nie z arbitralnego minimum tego planu.

**Implementacja:** istniejące definicje i systemy placement/generacji; moduły authorować w Editorze. Każda próba używa docelowej jednostki progresji, np. ukończonego pokoju, a nie przypadkowej liczby obiektów.

**Odbiór:** wejścia i cele osiągalne, brak uwięzienia przez dekoracje; przegląd reprezentatywnych seedów i aren obu bossów. Walka i eksploracja mają spójne przeszkody i granice.

### CP2-28 Dodać przeciwników i encountery Elfich Ruin

**Zależności:** 22, 27; cechy z 24–26 przypisane tym jednostkom.

**Zakres:** siedem jednostek: Niewolnik Demonów, Demoniczny Zbrojny, Demoniczny Czarownik, Chochlik, Hawia'kalb, Demoniczny Iluzjonista, Eayan'masakh. Statystyki, wzrost, trzy skille i traity według aktualnego arkusza; składy encounterów dla przewidzianych poziomów.

**Przygotować:** macierz roli/skill/AI/odporność/loot, brakujące animacje i ikony, budżety składów oraz legalne spawny. Wartości arkusza porównać z bazą v0.1.2, zamiast zakładać identyczną skalę trudności.

**Implementacja:** istniejące definicje jednostek i skilli, wspólne efekty i logika wyboru legalnych akcji. Składy w danych encounterów; bez osobnego systemu walki dla biomu.

**Odbiór:** każdy przeciwnik wykonuje wszystkie przewidziane akcje i ma działające AI; encountery nie składają się wyłącznie z jednej roli. Testy nietypowych skilli oraz smoke dolnej/górnej granicy poziomu i grup mieszanych.

### CP2-29 Dodać Demonicznego Jarla

**Zależności:** 27–28; wymagane cechy i operacje z 03.

**Zakres:** `Bossowie!AF13:AJ14`. F1: Gwardia Władcy daje 30% redukcji obrażeń za żywego sojusznika; przejście F2 po śmierci wszystkich sojuszników lub spadku HP do 50%. Skille: Demoniczna Furia, Błogosławieństwo Demonerhina, Przeklęty Płomień. F2: Płomień Demonerhina podpala pobliskich wrogów, odbiera Jarlowi 5% MaxHP na koniec tury i daje 0,5% obrażeń za 1% brakującego HP; skille Miecz Zagłady, Łańcuch Chaosu, Infernalne Uderzenie. Objąć wszystkie koszty, szanse i skalowanie z tych komórek oraz początkową eskortę i arenę.

**Przygotować:** tabelę F1/F2: stats, akcje, priorytety AI, początkowy skład eskorty i interakcje ze śmiercią sojuszników; kolejność zdarzeń i telegraph groźnych akcji. Ustalić limit/składanie redukcji 30% oraz czy utrata własnego HP może zabić Jarla. Nie dodawać przywołań, których nie ma w projekcie.

**Implementacja:** osobny system/stany bossa według analogów `ForestEntBossSystem` i `BanditLeaderBossSystem`, ze wspólnymi efektami tam, gdzie są ogólne. Nagrody podłączyć do istniejącego outcome, bez dropu z każdego obiektu fazy.

**Odbiór:** deterministyczne testy obu faz i triggerów, brak podwójnego przejścia oraz nagrody; smoke od wejścia do odbioru łupu. Demoniczny róg królewski i Energia Malada rozdzielone poziomowo zgodnie z 46; unikaty w 42.

### CP2-30 Dodać Cerbera

**Zależności:** 27–28 i wymagane cechy 24–26.

**Zakres:** `Bossowie!AF15:AJ16`. F1: trzy osobne paski głów po 25% MaxHP Cerbera, po jednej akcji na głowę bez kosztu AP, lokalna kontrola trafionej głowy; przejście po zniszczeniu dwóch głów lub spadku ich średniego HP poniżej 40%. Piekielne Kły, Demoniczny Szczek i Piekielny Skowyt przywołujący dwa Hawia'kalb z 20% MaxHP. F2: wyłączenie bocznych głów, +1% obrażeń za 1% brakującego HP i 20% odporności na ogłuszenie; Trujące Kły, Ognisty Oddech, Zwierzęca Furia.

**Przygotować:** model targetowania głów, relację ich HP do wspólnego HP bossa, jednostkę średniej 40%, zasady resetu i wpływu kontroli; arena z miejscem na summon oraz czytelne paski głów. Arkusz wymaga osobnego stanu HP, ale nie rozstrzyga sam wszystkich reguł przekazywania obrażeń między głową a bossem.

**Implementacja:** jeden właściciel stanu bossa i jawny automat faz; wspólne summon/statusy. Nie modelować głów jako niezależnych bossów z osobnymi rewardami bez wymagania projektowego. Bonus od brakującego HP wyliczać z bieżącego stanu i ustalonego limitu.

**Odbiór:** test wyłączenia głów, progów HP, braku miejsca na przywołanie, śmierci summonów i końca walki; brak pozostawionych głów po outcome. Pełny smoke obu faz i poprawny łup tylko raz.

### CP2-31 Zintegrować wyprawy i nagrody Elfich Ruin

**Zależności:** 04, 27–30, podstawowe pule z 02; domknięcie unikatów w 42.

**Zakres:** oferta zwykłych i bossowych wypraw, odblokowania, poziomy, generowanie, nagrody encounterów/pokojów, torby, wyjście/porażka/powrót do miasta. Włączyć źródła naturalne i zastępcze przypisane temu biomowi.

**Przygotować:** macierz typu wyprawy → poziomy → pokoje → boss → pule; baseline XP/złota i tabelę nagród bez jeszcze końcowego balansu. Oddzielić to, co gwarantowane, od losowanego.

**Implementacja:** rozszerzyć `DungeonOfferSystem`, istniejące definicje wypraw oraz `DungeonLootTableCatalog`/`LootTableSystem`. Nagrody przygotowywać zgodnie z obecnym cyklem, nie dopiero przy każdym ponownym otwarciu ekranu.

**Odbiór:** pełna pętla miasto → wyprawa → boss/wyjście → nagrody → miasto, także po porażce i loadzie w dozwolonym momencie. Testowy dostęp nie oznacza produkcyjnego udostępnienia biomu z brakującymi wymaganymi nagrodami.

## Morze

### CP2-32 Zaprojektować i zbudować pokoje Morza

**Zależności:** 23, kontrakt 04; 25 dla wielopolowych mieszkańców.

**Zakres:** pokoje nadmorskie/pirackie zgodne z wybranym kierunkiem artystycznym, połączenia lądu i wody, czytelna nawigacja, miejsca encounterów i dwie areny bossów. Próby projektować jako zamknięte moduły, nie kompletny redesign dungeonów.

**Przygotować:** graybox pokazujący drogi obejścia wody, starty obu stron, bezpieczne pola i miejsce na zmienną wodę Alfa-Rekina. Dobrać assety, granice i komunikację kosztu/zakazu ataku.

**Implementacja:** istniejąca generacja/placement plus teren 23; arena opisuje legalne obszary zmiany wody, nie przypadkowo modyfikowane wszystkie kafle. Authoring w Editorze, spójność wizualnej i logicznej linii brzegu.

**Odbiór:** obie strony mogą wykonywać legalne akcje, a arena nie blokuje walki postaci bez danej zdolności ruchu. Smoke kilku układów, footprintów i skrajnych budżetów ruchu; telegraph i brzeg czytelne z kamery.

### CP2-33 Dodać przeciwników i encountery Morza

**Zależności:** 22–24, 32; 25–26 według przypisanych cech.

**Zakres:** Pirat Kombatant, Pirat Harpunnik, Piaszczysty Feniks, Papuga, Człowiek-rekin, Pirat Konsyliarz i Pirat Kanonier; ich statystyki, skille, AI, skalowanie i grupy.

**Przygotować:** role i legalność akcji na lądzie/w wodzie, dystanse i zasady przesuwania przez granicę terenu, grafiki i telegraph. Piaszczysty Feniks nie jest zgodą na dodanie kompletnej Pustyni.

**Implementacja:** definicje jednostek i encounterów, wspólne profile ruchu, strzałów i AoE. AI ocenia zakaz ataku i koszt terenu z tych samych reguł co wykonanie; przy summon Kapitana użyć tych samych definicji z runtime modyfikatorem HP.

**Odbiór:** każdy skill ma legalny scenariusz użycia; mieszane grupy nie blokują się na brzegu. Testy pchnięć/harpuna i AoE, jeśli wynikają z definicji; smoke walk na obu terenach oraz progów poziomów.

### CP2-34 Dodać Alfa-Rekina

**Zależności:** 23–25, 32–33 oraz mechaniki z 03.

**Zakres:** `Bossowie!AF17:AJ18`. F1: premie w wodzie, brak uniku poza wodą, maksymalnie trzy kroki poza nią i dostęp tylko do Szarpnięcia Szczękami; Wir Wodny i Wodny Grzmot. F2: 50% odporności na przesunięcie w wodzie, dwa losowe pola zamieniane w wodę co turę wraz z obrażeniami stojącego tam wroga; wrogowie w wodzie otrzymują 20% więcej obrażeń. Rozszarpanie, Przywołanie Grzmotu i Zanurzenie z niewidzialnością, ruchem bez kary poza wodą oraz premią następnego ataku. Uwzględnić komplet kosztów i efektów tych skilli.

**Przygotować:** dokładną kolejność zmiany wody i efektów tury, dozwolone kafle, zakres premii i powrót do zwykłych zasad po niewidzialności. Zweryfikować wartości +20 odporności/przemieszczenia, 20 uniku i +20% obrażeń w bieżącym arkuszu wraz z jednostkami.

**Implementacja:** system bossa korzysta z terenu 23 i footprintu 25; zmiana wody wywołuje to samo przeliczenie legalności/efektów co wejście na pole. Nie nakładać stale kolejnych kopii premii.

**Odbiór:** testy ląd/woda/F2, aktora stojącego na zmienionym kaflu, pchnięcia, niewidzialności i braku legalnej akcji. Płetwa alfa-rekina i zastępcze Łuski wiwerny mają osobne progi z 46; unikaty w 43.

### CP2-35 Dodać Kapitana Piratów

**Zależności:** 32–33, wspólny summon z 03 i terrain 23.

**Zakres:** `Bossowie!AF19:AJ20`. F1 przywołuje co cztery tury Kombatanta, Harpunnika, Kanoniera lub Konsyliarza z 50% HP oraz zyskuje +5% obrażeń za śmierć sojusznika. Skille: Cięcie Kapitańskie, Rozkaz Kapitański, Alkoholowy Mołotow. F2 przywołuje co dwie tury; Karmazynowe Ostrze skaluje się brakującym HP celu, Rozkaz Abordażu przyspiesza wszystkich sojuszników, Przywołanie Sztormu osłabia wrogów i wzmacnia sojuszników. Objąć wszystkie zapisane parametry.

**Przygotować:** zamkniętą pulę piratów i rozkład wyboru, licznik tur, limit/liczbę jednoczesnych summonów, brak miejsca i regułę naliczania śmierci sojusznika. Ustalić, czy stacki przechodzą między fazami i co dokładnie robią rozkazy.

**Implementacja:** automat faz i istniejąca usługa spawn; modyfikator HP jako parametr instancji. Śmierć przetwarzać jednokrotnie, oddzielić od despawn i czyszczenia areny. Nie tworzyć pełnych kopii definicji każdego pirata dla summonów.

**Odbiór:** test granic 4/2 tur, przejścia fazy, śmierci kilku sojuszników i pełnej areny. Summony nie mnożą nagród/XP poza przyjętym kontraktem; boss daje jedną nagrodę, a niezaprojektowane unikaty pozostają zadaniem 43.

### CP2-36 Zintegrować wyprawy i nagrody Morza

**Zależności:** 04, 32–35, pule z 02; domknięcie unikatów w 43.

**Zakres:** zwykłe i bossowe wyprawy Morza, poziomy i oferty, komplet nagród, źródła naturalne oraz zastępcze. Uwzględnić np. docelowo pustynne składniki i Cytryn zgodnie z aktualnym przypisaniem, bez dorabiania Pustyni.

**Przygotować:** matrycę ofert i dropów z rozróżnieniem Alfa-Rekina/Kapitana, loot encounterów, toreb i nagród końcowych; baseline czasu i kosztu wyprawy.

**Implementacja:** ten sam pipeline definicji, oferty i przygotowania nagród co w 31. Zintegrować stan wody z przejściem eksploracja/walka i czyszczeniem areny; nie pozostawiać efektów bossowej wody po zakończeniu.

**Odbiór:** pełna pętla wyprawy, porażka i wyjście, poprawne utrwalenie przygotowanych nagród. Próbka generacji sprawdza brak niedostępnych wysp/celów; wszystkie przypisane źródła mają rzeczywiste tabele runtime, nie tylko wpis w planie.

## Bagno

### CP2-37 Zaprojektować i zbudować pokoje Bagna

**Zależności:** 23, 04; 25–26 dla jednostek użytych w pokojach.

**Zakres:** moduły eksploracyjne Bagna, połączenia lądu/błota, miejsca grup roślin i dwóch bossów, próby i nagrody projektowane lokalnie. Układ ma wspierać mechaniki roślin, a nie wymuszać blokadę wejścia do walki.

**Przygotować:** graybox z suchymi dojściami i strefami utrudnień, czytelnością korzeni i toksycznych obszarów, listą legalnych pól teleportu Ludożernej Rośliny. Zatwierdzić art, światło i elementy kolizyjne w skali kamery.

**Implementacja:** istniejący generator i definicje terenu; pod arenę rośliny przygotować dane legalnego placementu, pod Enta czytelne przestrzenie bezpieczne. Próby mają jawny stan ukończenia i resetu.

**Odbiór:** żaden seed nie odcina celu samym błotem/dekoracją lub pełnym footprintem, boss ma miejsce na mechaniki. Testy placement i smoke z widocznością telegraphów przy docelowych efektach wizualnych.

### CP2-38 Dodać przeciwników i encountery Bagna

**Zależności:** 22–26 według przypisań, 37.

**Zakres:** Mięsożerna Roślina, Aligator, Pasożytnicza Roślina, Topielec, Dezerter, Strażnik Bagien, Plująca Roślina; ich wzrost, trzy skille, cechy, AI i składy. Zwykłą Mięsożerną Roślinę odróżnić ID od bossowej Ludożernej Rośliny.

**Przygotować:** role, zasięg nieruchomych jednostek, legalne pozycje walki i sposoby dotarcia do nich; listę nowych statusów oraz grafiki i telegraph. Oddzielić odporność na błoto od niewrażliwości na truciznę.

**Implementacja:** wspólne dane przeciwników i efekty, AI korzystające z cechy nieruchomości i aktualnego kosztu terenu. Encountery muszą pozwalać na wygraną bez konkretnej klasy lub losowego przedmiotu, chyba że zatwierdzony projekt mówi inaczej.

**Odbiór:** każdy skill działa, nieruchome jednostki nie wykonują pustych zapętlonych tur; testy kontroli i okresowych obrażeń. Smoke grup mieszanych na suchym/błotnistym układzie, dolnych/górnych poziomów i źródeł łupu.

### CP2-39 Dodać Bagnistego Enta

**Zależności:** 23, 25–26 według projektu, 37–38.

**Zakres:** `Bossowie!AF21:AJ22`. F1: Bagienna Aura, Zamach Gałęzią, Bagienne Korzenie i Błotnista Kora. F2: Bagienna Plaga przy obrażeniach statusowych wroga zadaje temu wrogowi dodatkowe 2% maksymalnego HP i oddaje Entowi 2 MP; Błotnisty Rozgrom, Rozlewisko Bagna oraz Gniew Bagien zatruwający pola poza kwadratem wokół bossa i rozstrzygany po dwóch turach. Objąć wszystkie koszty, szanse, odporności i czasy z arkusza.

**Przygotować:** definicję „obrażeń statusowych”, częstotliwość reakcji i ochronę przed rekurencją; moment wyboru bezpiecznej strefy, anulowanie castu i reakcję na kontrolę. Telegraphed obszar ma odpowiadać faktycznemu wynikowi.

**Implementacja:** stan bossa i wspólne efekty aury/obszaru. Reakcje emitować w ustalonym miejscu pipeline obrażeń, nie z wyświetlania ticków. Dwuetapowy cast ma jawny stan i cleanup po śmierci/końcu walki.

**Odbiór:** test wielu DOT w turze, śmierci w trakcie reakcji, przejścia fazy i wyjścia z obszaru. Smoke czytelności bezpiecznej strefy oraz wykonalności walki buildem opartym na statusach; unikaty w 44.

### CP2-40 Dodać Ludożerną Roślinę

**Zależności:** 26, 37–38 i kontrakty teleportu/obszarów z 03.

**Zakres:** `Bossowie!AF23:AJ24`. F1: darmowa Podróż Korzeniem co dwie tury, teleport o zasięgu 6 i Chłostający lub Strzelający Korzeń w starej pozycji. Korzenie mają 3 HP, własny atak co turę, +10% obrażeń na turę; zniszczenie zadaje bossowi 5% MaxHP. Koronny Uścisk unieruchamia/przyciąga/zatruwa, Zatrute Żniwa tworzą obszar i wzmacniają korzenie, Trauma Korzenia detonuje korzeń. F2: teleport co turę, dłuższe unieruchomienie Uścisku, Starożytny Głód niszczy korzenie, leczy 1% MaxHP za sztukę i daje ładunki +2% obrażeń rośliny/korzeni; Pożarcie zabija unieruchomiony cel poniżej progu HP wyznaczonego liczbą ładunków.

**Przygotować:** jednostkę wzrostu obrażeń, zasady obrażeń korzeni, ich targetowalność, stacki konsumpcji i dokładny próg egzekucji. Rozstrzygnąć brak wolnego pola oraz to, czy zniszczenie korzenia może zabić bossa.

**Implementacja:** jawny rejestr korzeni należący do bossa, teleport przez wspólną walidację, obrażenia zwrotne/konsumpcja przez wspólny pipeline. Cleanup nie jest zwykłym zniszczeniem korzenia naliczającym dodatkowe obrażenia.

**Odbiór:** test F1/F2, limitów placementu, jednoczesnego zniszczenia kilku korzeni, egzekucji i śmierci bossa. Korzenie nie zostają na mapie i nie tworzą osobnych nagród; unikaty w 44.

### CP2-41 Zintegrować wyprawy i nagrody Bagna

**Zależności:** 04, 37–40, pule z 02; domknięcie unikatów w 44.

**Zakres:** oferty i odblokowania, zwykłe/bossowe wyprawy, poziomy, encountery, pokoje, torby i pozostałe nagrody. Włączyć wszystkie źródła naturalne i zastępcze Bagna, w tym materiały wymagane przez alchemię.

**Przygotować:** tabelę ofert, czas/cel wyprawy, pule bossów oddzielone od zwykłych roślin, baseline nagród i zasady sprzątania korzeni/aur po outcome.

**Implementacja:** istniejące definicje dungeonów i pipeline reward preparation; stan mechanik bossa ma właściciela w walce, nie w trwałej definicji pokoju. Zintegrować zakończenie walki z jednostkami nieruchomymi/niezniszczalnymi zgodnie z 26.

**Odbiór:** pełna pętla wyprawy z obydwoma bossami oraz porażką/wyjściem; brak utrzymanych debuffów terenu po powrocie. Nagrody nie rerollują się po loadzie, przypisane materiały faktycznie wypadają w dozwolonych przedziałach.

## Unikaty i gospodarka

### CP2-42 Zaprojektować i wdrożyć unikaty Elfich Ruin

**Zależności:** 02, 17, 29–30 dla integracji; projekt można rozpocząć wcześniej.

**Zakres:** wszystkie wpisy unikatów Jarla i Cerbera z `Lootbagi`, ich poziomy, bazowy typ, parametry, efekt specjalny i źródło. Sama nazwa w arkuszu nie jest kompletnym projektem efektu.

**Przygotować:** użytkownik zatwierdza pełne specyfikacje: slot, wymagania, skalowanie, stacking, cooldown/trigger, siła i szansa dropu; plan może pozostawić liczby do balansu, ale przed kodem muszą być jednoznaczne. Wskazać ikonę i prezentację efektu.

**Implementacja:** `BossUniqueEquipmentDefinition`/`BossUniqueDropData`, istniejący generator i wspólny runtime pasywek. Osobne stabilne ID przedmiotu i efektu, brak logiki rozpoznającej nazwę w kalkulatorze obrażeń.

**Odbiór:** drop z właściwego bossa i zakresu, equip/unequip, interakcje i zapis z utrwalonymi rollami. Test efektu na granicach i po resecie buildu. Karta zamyka brak unikatowych nagród w 31.

### CP2-43 Zaprojektować i wdrożyć unikaty Morza

**Zależności:** 02, 17, 34–35 dla integracji.

**Zakres:** komplet unikatów Alfa-Rekina i Kapitana. W dotychczasowym `Lootbagi!F109` brak jednej nazwy Alfa-Rekina, w F110 dwóch Kapitana; przed wdrożeniem odczytać aktualny stan. Nie zastępować braków losowym zwykłym itemem.

**Przygotować:** nazwy oraz pełne efekty i parametry wszystkich zaplanowanych pozycji, zasięg poziomów, grafiki i drop. Jeśli efekt zależy od wody, zatwierdzić zachowanie w pozostałych biomach, aby przedmiot zachował ustaloną wartość poza jedną areną.

**Implementacja:** te same definicje i pipeline co 42; warunki terenowe korzystają ze wspólnych zapytań 23. Brak kopiowania stałych reguł wody do indywidualnej pasywki.

**Odbiór:** test aktywacji/dezaktywacji na lądzie i w wodzie, gdy dotyczy, brak pozostawionych bonusów i podwójnych triggerów. Drop i zapis poprawne; 36 ma pełny zatwierdzony zestaw nagród.

### CP2-44 Zaprojektować i wdrożyć unikaty Bagna

**Zależności:** 02, 17, 39–40 dla integracji.

**Zakres:** komplet unikatów Bagnistego Enta i Ludożernej Rośliny. Dotychczas w `Lootbagi!F111/F112` brakowało po jednej nazwie; nazwy i efekty wymagają zamknięcia projektu, nie dopowiedzenia w kodzie.

**Przygotować:** pełne definicje slotów, parametrów i efektów, triggerów, limitów i interakcji ze statusem/terenem, jeśli przewiduje je projekt. Oddzielić flavor od rzeczywistej mechaniki; określić użyteczność poza Bagnem.

**Implementacja:** obecny model boss uniques i efekty pasywne; reakcje na DOT/śmierć nie mogą uruchamiać samych siebie bez granicy. Współdzielić potrzebne operacje z 39–40 zamiast tworzyć ich drugie wersje dla ekwipunku.

**Odbiór:** poprawne źródła, poziomy, utrwalone parametry i działanie przy założeniu/zdjęciu; testy pętli triggerów i kilku nakładających się źródeł. Karta domyka nagrody 41.

### CP2-45 Rozszerzyć obecne unikaty do L40

**Zależności:** 04, 17; istniejące definicje z bazy 01.

**Zakres:** istniejące unikaty Katakumb i Lasu w przedziałach do L40, według zamkniętych zasad dropu/skali. Nie projektować na nowo ich efektów tylko dlatego, że pojawia się wyższy level.

**Przygotować:** tabelę dotychczasowych itemów i bossów, aktualne zakresy, parametry skalowane i stałe; ustalić znaczenie poziomu wyprawy dla wyniku i ewentualne progi siły efektu. Sprawdzić dolne zakresy: nie wprowadzać skrótu „1–20”.

**Implementacja:** rozszerzyć reguły generowania i tabele dropu; zachować ID definicji oraz zapisane parametry istniejących egzemplarzy. Nie przeliczać starych rolli do nowych wartości podczas zwykłego loadu.

**Odbiór:** nowe egzemplarze z L21–40 są zgodne z regułą, stare identyczne po wczytaniu; test granic L20/21/30/31/40 i źródła właściwego bossa. Porównanie siły z nowymi unikatami należy do 52–53.

### CP2-46 Przeliczyć źródła materiałów i gospodarkę pięciu biomów

**Zależności:** 16–21, 31, 36, 41, 45; 42–44 do pełnego bilansu bossów.

**Zakres:** faktyczne pule toreb, materiałów zwykłych i bossowych, ilości, szanse, ceny i relacja do receptur dla Katakumb, Lasu, Elfich Ruin, Morza i Bagna. Utrzymać przypisane naturalne źródła oraz jawne źródła zastępcze dla nieobecnych trzech biomów.

**Przygotować:** zestawienie oczekiwanych materiałów na wyprawę, kosztów craftingu i czasu zdobycia reprezentatywnych produktów. Modelować warunkowy wybór z puli, a nie sumować każdej pozycji tak, jakby dropiła niezależnie. Stare proporcje są punktem porównania, nie nienaruszalnym prawem.

**Implementacja:** przeliczyć i wdrożyć tabele w istniejącym `LootTableSystem` oraz katalogach. Oddzielić bossowe materiały bazowe L11–40 od zastępczych L31–40: Zabójca Szkieletów → Krew berserka; Martwy Król → Zaczarowana dłoń; Jarl → Energia Malada; Alfa-Rekin → Łuski wiwerny. Wspólny wiersz L11–40 w arkuszu nie może odblokować późnego materiału od L11.

**Odbiór:** audyt runtime dowodzi osiągalności wszystkich składników i zachowania uzgodnionych naturalnych źródeł; NPC nie jest jedyną drogą do zastępczego surowca. Test granic i statystyczna kontrola rozkładów; brak Pyłu energetycznego i legacy ucha w nowych dropach. Finalne tempo stroi 53.

## Opcjonalny kontrolowany RNG

### CP2-47 Zdecydować o zakresie kontrolowanego RNG

**Zależności:** 03; dane z działających wycinków i 46 do świadomego wyboru zakresu.

**Zakres:** decyzja, czy system wchodzi do v0.2.0 i w których domenach: trafienia/kryty, efekty, loot/nagrody, ulepszanie. Kandydat `k=2` nie jest zatwierdzoną wartością ani uniwersalnym ustawieniem.

**Przygotować:** porównanie dotychczasowego RNG i proponowanego modelu: średnia, serie sukcesów/porażek, stan, reset i persistence. Zdefiniować klucze strumieni, wpływ gwarantowanych/niemożliwych zdarzeń, zależność od seed i zachowanie przy reloadzie. Osobno sprawdzić wybór ważony i kolejne testy procentowe.

**Sposób realizacji:** dokument kontraktu i mały deterministyczny model porównawczy, bez cichego przepinania gry. Użytkownik wybiera domeny i parametry albo odroczenie.

**Odbiór:** jawna decyzja per domena. Przy odroczeniu 48–51 otrzymują status odłożone, nie blokują wydania. Przy przyjęciu wspólny kontrakt implementuje pierwsza aktywna karta; kolejne go wykorzystują. Nie zaczynać passu od tej karty automatycznie.

### CP2-48 Zintegrować kontrolowany RNG z trafieniami i krytami

**Zależności:** pozytywna decyzja 47 dla walki.

**Zakres:** wspólna implementacja zatwierdzonego modelu oraz rozstrzyganie trafienia i kryta. Pozostałe domeny tylko przez kolejne karty, bez automatycznego zamieniania każdego wywołania random.

**Przygotować:** mapę obecnych miejsc losowania, definicję właściciela historii i oddzielenia hit/crit; zasady multi-hit, obszarów, summonów, celów, gwarancji i save/load. Ustalić, czy nietrafiony atak w ogóle zużywa próbę kryta.

**Implementacja:** usługa/stan w istniejącej warstwie domenowej zgodnie z 47, wywoływana przez wspólny pipeline ataku. Persystencję wersjonować tylko w potrzebnym zakresie; bez nowego globalnego singletona. Nie zmieniać widocznej szansy, jeżeli zatwierdzony model obiecuje zachowanie średniej.

**Odbiór:** deterministyczne testy sekwencji i niezależności kluczy, 0/100%, multi-hit, reload; statystyczne sprawdzenie oczekiwanych częstości i długości serii. UI/podgląd pozostaje zgodny z zatwierdzonym znaczeniem prawdopodobieństwa.

### CP2-49 Zintegrować kontrolowany RNG z efektami skilli i pasywek

**Zależności:** 47–48; pozytywna decyzja dla tej domeny.

**Zakres:** probabilistyczne efekty i procki korzystające ze wspólnego silnika, po audycie miejsc faktycznie używających szansy. Nie zmieniać triggerów deterministycznych, obrażeń ani losowego wyboru celu bez zatwierdzenia.

**Przygotować:** macierz definicja efektu/źródło/wykonawca/cel → klucz historii; zasady proców wielokrotnych, AoE, DOT, reakcji łańcuchowych i efektów ekwipunku. Ustalić czy identyczny efekt z dwóch źródeł współdzieli stan.

**Implementacja:** przepiąć wspólne wykonanie skilli i pasywek na kontrakt 48, a wyjątki ograniczyć do jawnej polityki danych. Jedna próba logicznego zdarzenia nie może być zużywana osobno w walidacji, preview i wykonaniu.

**Odbiór:** testy tożsamości strumieni, wielu celów i źródeł, braku kwalifikującego triggera oraz rekurencji. Smoke starych i nowych buildów; brak pominiętych głównych ścieżek albo podwójnego testowania tej samej szansy.

### CP2-50 Zintegrować kontrolowany RNG z lootem i nagrodami

**Zależności:** 47, wspólna usługa z pierwszej aktywnej integracji, 46; decyzja dla lootu.

**Zakres:** wyłącznie zatwierdzone kategorie dropu i nagród. Gwarantowany drop, roll na wystąpienie, ważony wybór rodzaju i ilość są różnymi operacjami; nie zastępować wszystkich jednym algorytmem.

**Przygotować:** politykę historii per gracz/biom/pula/boss/przedmiot, obsługę rzadkich unikatów, resetu sezonu/wyprawy, nowych tabel i seed. Sprawdzić farmienie zmienianiem poziomu i otwieraniem toreb po reloadzie.

**Implementacja:** integracja w `LootTableSystem` i reward preparation w chwili właściwego rozstrzygnięcia. Historia i wynik nagrody muszą zapisywać się spójnie; odczyt gotowej nagrody nie wykonuje kolejnego rolla.

**Odbiór:** testy ponownego odbioru/loadu, pustych pul, zmian level, gwarancji i niezależności kategorii. Porównanie częstości i czasu do rzadkiego dropu po integracji, następnie strojenie 53. Stary zapisany loot pozostaje bez zmian.

### CP2-51 Zintegrować kontrolowany RNG z ulepszaniem

**Zależności:** 47, wspólny kontrakt RNG; decyzja dla ulepszania.

**Zakres:** zatwierdzone losowe próby istniejącego ulepszania. Nie zmieniać kosztów, konsekwencji porażki ani podstawowej krzywej sukcesu bez odrębnej decyzji balansu.

**Przygotować:** klucz historii: postać, typ usługi, poziom ulepszenia lub instancja itemu zgodnie z 47; reguły zmiany przedmiotu, sprzedaży, loadu, anulowania i prób niemożliwych. Zidentyfikować ryzyko przenoszenia „nabitej” szansy między tanim i drogim celem.

**Implementacja:** rozstrzygnięcie w istniejącej transakcji ulepszania, atomowo z kosztem, zmianą przedmiotu i historią. Preview i nieudana walidacja nie konsumują próby. Nie umieszczać stanu w oknie UI.

**Odbiór:** testy sukcesu/porażki, braku środków, zmiany itemu i ponownego loadu; brak utraty zasobów bez próby i bezpłatnego rolowania historii. Symulacja średniego kosztu ulepszeń jest wejściem do 53.

## Balans i zamknięcie wydania

### CP2-52 Przeprowadzić końcowy balans klas i encounterów L1–40

**Zależności:** 05–07, 09–12, 14, 17–22, 28–30, 33–35, 38–40, 42–45; zakończone aktywne karty RNG walki.

**Zakres:** strojenie wszystkich czterech klas i pięciu biomów, zwykłych grup, bossów/faz, unikatów i interakcji. Nie otwierać ponownie pełnego redesignu drzewek bez wykazanego problemu i decyzji.

**Przygotować:** zestaw referencyjnych buildów i ekwipunku na punktach L10/20/25/30/35/40, poziom zasobów na wejściu i miary: liczba tur, obrażenia, przeżywalność, zużycie many/mikstur, skuteczność kontroli. Zdefiniować akceptowalne pasma trudności przed zmianą liczb.

**Implementacja:** małe, mierzalne korekty danych lub potwierdzonych błędów formuł; po każdej grupie powtórzyć odpowiedni scenariusz. Odróżnić nadmierną siłę od błędnego triggera, którego nie należy maskować nerfem.

**Odbiór:** każda klasa ma wykonalne reprezentatywne buildy i przechodzi pięć biomów; brak obowiązkowego pojedynczego przedmiotu lub trwałego softlocka. Zapis porównań przed/po i uzasadnienie istotnych korekt, z testem bazowych L1–20.

### CP2-53 Przeprowadzić końcowy balans progresji i gospodarki

**Zależności:** 04, 15–21, 31, 36, 41–46, 52; zakończone aktywne RNG lootu/ulepszania.

**Zakres:** tempo L1–40, koszt rozwoju/craftingu/resetów, napływ i odpływ złota, dostępność składników oraz długość zdobywania wyposażenia, galdurytów i unikatów. To końcowe strojenie systemów już zintegrowanych.

**Przygotować:** model całej ścieżki od nowej postaci oraz zapisów kończących v0.1.x. Oprócz wartości oczekiwanej badać rozrzut czasu pozyskania rzadkich materiałów, bossowe bottlenecki i zapasy wymagane do zmiany buildu.

**Implementacja:** korekty tabel XP, kosztów, cen, ilości i szans w ich istniejących źródłach. Audytować pętle kupno/craft/sprzedaż, refund/reset oraz zastępcze źródła. Nie usuwać fallbacku tylko dlatego, że w planie istnieje przyszły biom.

**Odbiór:** sensowna ścieżka bez przymusowego farmienia jednego niedostępnego źródła, brak nieskończonego arbitrażu i martwych receptur. Raport czas/koszt dla kluczowych progów; modele potwierdzone reprezentatywnym przejściem gry.

### CP2-54 Wykonać regresję zapisów i pełnej pętli gry

**Zależności:** wszystkie obowiązkowe funkcje i 52–53; opcjonalne RNG tylko jeśli przyjęte.

**Zakres:** kompatybilność v0.1.0, przyjętej bazy v0.1.2 i nowych zapisów, migracje ID/state, pełna pętla miasto/usługi → wyprawa → walka/boss → łup → crafting/rozwój → kolejna wyprawa.

**Przygotować:** wersjonowane fixture zapisów: cztery postacie, rozwinięte stare drzewa, stare i nowe przedmioty, legacy materiały, przygotowane nagrody oraz reprezentatywne nowe stany w miejscach dozwolonych przez system zapisu. Zachować oryginały plików.

**Implementacja:** uruchomić wymagany przekrojowy zestaw i dodać brakujące testy rzeczywistych ryzyk migracyjnych. Naprawy przypisać do systemu odpowiedzialnego za błąd; po zmianie powtórzyć dotkniętą ścieżkę. Nie testować zapisu mid-combat, jeśli gra go nie wspiera, jako nowego domyślnego wymagania.

**Odbiór:** brak utraty postaci, ekwipunku, SP, złota i ID; brak rerollu nagród, podwójnych refundów i bonusów. Zapisany raport testów oraz lista zaakceptowanych ograniczeń; bez znanych blockerów pełnej pętli.

### CP2-55 Zamknąć prezentację i wydajność v0.2.0

**Zależności:** gotowy content, 54; pierwsze pomiary można wykonać wcześniej.

**Zakres:** końcowa spójność tooltipów, ikon, statusów, trzecich drzew/T5, usług, ofert i bossowych telegraphów; czytelność w docelowej kamerze i rozdzielczościach; wydajność dużych footprintów, summonów, obszarów i zmian terenu.

**Przygotować:** listę docelowych ekranów i konfiguracji, najcięższe zatwierdzone encountery, baseline profiler i budżety akceptacji. Sprawdzić assety/licencje oraz brak debug-only widoków w normalnej pętli.

**Implementacja:** poprawki oparte na pomiarze i inspekcji, wspólne formatowanie nazw/wartości, usunięcie zbędnych alokacji i powielonych przeliczeń. Nie wprowadzać nowego redesignu całego UI ani stałych ścian tekstu objaśniających mechaniki. Serialized zmiany przez Editor/handoff.

**Odbiór:** brak zasłoniętych telegraphów, brakujących ikon i uciętych wartości; akceptowalny czas tur/generacji oraz brak wzrostu zużycia pamięci po kolejnych wyprawach. Zmiany dotykające gameplayu wracają na odpowiedni test z 54.

### CP2-56 Przygotować wydanie v0.2.0

**Zależności:** 54–55 i zaakceptowany smoke zakresu wydania.

**Zakres:** podsumowanie zmian, ostateczny zakres, znane ograniczenia, manifest wersji, Windows x64 build i jego weryfikacja. Pakiet ma obejmować komplet obowiązkowego contentu i wyłącznie zatwierdzone części RNG.

**Przygotować:** changelog dla gracza, instrukcję zgodności zapisów, checklistę builda, stan wszystkich 56 kart i wskazanie odłożonych. Oddzielić lokalne artefakty projektu od plików dystrybucyjnych.

**Implementacja:** numer aplikacji zmienić dopiero w tej karcie, nie w planowaniu. Wykonać lokalny build z zatwierdzonego stanu, sprawdzić uruchomienie, zapis/odczyt i reprezentatywną wyprawę; przejrzeć końcowy diff pod kątem unrelated plików i danych roboczych.

**Odbiór:** konkretny przetestowany pakiet i materiały wydania gotowe do oceny. Commit/push według `git`; merge, tag i publikacja wyłącznie po odpowiednim poleceniu. Samo przygotowanie wydania nie jest upoważnieniem do publikacji.

## Decyzje do zebrania przed właściwymi kartami

Nie trzeba rozstrzygać wszystkiego przed CP2-01. Gdy rozpoczynamy dany pakiet, zebrać wszystkie dotyczące go blokery w jednej wiadomości, po sprawdzeniu aktualnego arkusza i implementacji.

| Decyzja | Blokuje | Co trzeba ustalić |
| --- | --- | --- |
| Baza v0.1.2 | 01 i dalsze implementacje | Który zaakceptowany stan/pakiet zmian integrujemy |
| Kontrakt L40 | 04, rozwój i oferty | XP, SP, tier gating, odblokowania i zachowanie na capie |
| T6/T7 Zbrojnego | 06 | Wchodzą, są dostosowane do L40 czy zostają poza wydaniem |
| Strażnik | 08–09 | Docelowe node'y i pełna semantyka Impetu |
| Brakujący Paladyn | 13–14 | T5 Krzyżowca, nazwa/tożsamość i komplet trzeciego drzewa |
| Reset | 15 | Zakres refundu, wzór opłaty, dostępność i istotny UX |
| Galduryty | 21 | Rozkład 3/4 efektów, siły i wymagania hosta, jeśli nadal nieopisane |
| Nietypowe traity/teren | 23–26 | Jednostki wartości, zasięg lotu, footprint i granice legalnych akcji |
| Areny i próby | 27, 32, 37 | Małe projekty zatwierdzane przy budowie danego biomu |
| Niejednoznaczności bossów | 29–30, 34–35, 39–40 | Kolejność faz/triggerów, limity summonów, szczegóły specjalnych akcji |
| Unikaty | 42–44 | Wszystkie nazwy, efekty, sloty, skalowanie i zasady dropu |
| Kontrolowany RNG | 47–51 | Czy wchodzi, domeny, model, parametry, klucze i persistence |

Nazwy, odporności, ceny i receptury już opisane w aktualnym arkuszu nie są pytaniami do użytkownika, dopóki nie ma konkretnej sprzeczności lub braku. Wartości brakujące w tym planie odczytuje się z danych źródłowych; nie zastępuje ich nowym założeniem implementacyjnym.

## Prowadzenie kart

Na początku realizacji dopisać w karcie Trello: przyjętą wersję arkusza, wynik punktowego context passu, zamknięte decyzje i konkretne podzadania wynikające z audytu. W opisie zostawić numer CP2 jako stały identyfikator. Każda zmiana zakresu aktualizuje ten plan i odpowiednią kartę; nie rozszerza sąsiednich kart po cichu.

Status początkowy kart: **planowane**, z wyjątkiem zaakceptowanej CP2-01 po **smoke PASS** i zaakceptowanej CP2-02 po przeglądzie katalogu; macOS i X360 są odłożone do późniejszej integracji. Karty projektowe 08, 13, 47 kończą się zamkniętą specyfikacją/decyzją; nie wymagają pozornej implementacji. Karty 48–51 mają dodatkowo status **warunkowe** do decyzji 47. Ten plan nie tworzy i nie aktualizuje kart w zewnętrznym Trello.
