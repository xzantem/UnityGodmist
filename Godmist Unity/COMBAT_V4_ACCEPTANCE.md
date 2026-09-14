# Combat v4 — acceptance, code review i UI pass

Status: DONE — użytkownik zaakceptował kartę i zlecił commit/push. Pełny review zakończony; nowe testy po poprawkach pozostają NOT RUN, ponieważ Unity jest otwarte.

## Wyniki automatyczne
- PASS: pełna regresja EditMode — 805/805, 0 failed, 0 skipped. Wynik `%TEMP%/combat-v4-acceptance.xml`, log `%TEMP%/combat-v4-acceptance.log`.
- Suite obejmuje również wcześniejsze systemy gry; nie są to 805 testów wyłącznie Combat v4. W zakresie milestone'u obejmuje seamless, geometrię, pozycje, AI, pułapki, teren, synchronizację, outcomes, recovery, persistence, kamerę, UI i content. Ostatni test tooltipa kolejki także wykonany w tym przebiegu.
- PASS: `dotnet build Views.csproj -v quiet`, 0 warnings/errors.
- PASS: sprawdzenie zewnętrznych GUID w 15 scenach/prefabach (DungeonSandbox, CombatSandbox, PF_UIW_Combat*, CombatHUD) względem Assets i pakietów — brak nierozwiązanych GUID. To nie zastępuje wszystkich kontroli runtime/fileID.
- NOT RUN: PlayMode end-to-end; projekt nie ma obecnie wykonywalnego zestawu PlayMode w katalogu testów. Końcowy smoke gry należy do użytkownika.

## Pełny code review milestone’u (2026-09-14)

Zakres: diff od `e6ee59b3` do `3027ba3e` oraz lokalne poprawki review. Przejrzano zmiany 126 plików produkcyjnych C# (Core 3, Data 9, Runtime 9, Systems 36, Views 66, Editor 3) i zmiany testów. Ekstrakcję CombatExecutionDriver porównano bezpośrednio z poprzednim CombatSandboxBootstrap, oddzielając przeniesiony kod od zmian zachowania. Serialized state sprawdzono strukturalnie i przez istniejące testy prefabów/contentu; nie oznacza to ręcznego wizualnego sprawdzenia każdego elementu YAML.

Znalezione i poprawione:
- **P1 — przeciwnicy pomijani przez pułapki w eksploracji przed pierwszą walką.** DungeonEncounterSpawner wiązał tylko CharacterDefinition; GatherTrapOccupants pomijał widoki bez RuntimeCharacter. Spawner tworzy teraz CharacterInstance istniejącą fabryką, na poziomie lochu, z unikalnym ID przeciwnika. Coordinator przejmuje tę samą instancję przy wejściu do walki. Dodano test spawnu, poziomu/ID i obrażeń pułapki przed walką.
- **P2 — ujemny rabat honoru ignorowany.** HonorPriceSystem ucinał rabat do 0%, mimo ustawienia użytkownika −100% dla Wygnańca w GameDefines.asset. Ujemne wartości działają jako dopłata (−100% = ×2); koszt saturuje do int.MaxValue zamiast przepełnienia. Dodano 5 przypadków testowych ceny, obciążenia portfela i overflow. Asset balansu użytkownika pozostawiono bez zmian.
- **P3 — uszkodzone polskie znaki w komunikacie blokady ruchu.** Przywrócono poprawny tekst.

Weryfikacja po poprawkach:
- PASS: kompilacja Views, 0 warnings/errors; kompilacja EditModeTests, 0 errors, 7 istniejących ostrzeżeń obsolete API (Editor drawery i GetInstanceID).
- PASS: strukturalna kontrola 70 zmienionych plików serialized — 44 957 lokalnych referencji, brak brakujących fileID i powtórzonych ID dokumentów; 7 409 zewnętrznych odwołań GUID, brak nierozwiązanych odwołań względem Assets i pakietów. Kontrola nie dowodzi poprawności typu każdego zewnętrznego fileID.
- NOT RUN: uruchomienie nowych testów po review — Editor otwarty (proces Unity 11260); poproszono użytkownika o zapis i zamknięcie. Wynik 805/805 poniżej dotyczy stanu sprzed tych poprawek.
- NOT RUN: nowy smoke UI/runtime po poprawkach; wcześniejsze akceptacje użytkownika zachowane.

Dług techniczny do profilowania/refaktoru przy pierwszym content passie: w seamless cache zasięgu ruchu jest unieważniany co klatkę, a renderer odtwarza obiekty highlightów. AI przechowuje wiele etykiet pełnych ścieżek. Nie wykonano pomiarów wydajności dużych aren, więc nie przypisano im niezmierzonego wyniku PASS ani nie zmieniano algorytmu w tym review. Duże klasy i kompresowane formatowanie pozostają do uzgodnionego refaktoru.

## Przegląd integracji
- DungeonSandboxBootstrap buduje mapę, obiekty i jednostki przed inicjalizacją coordinatora. Start walki korzysta z istniejących widoków; osobny CombatSandbox pozostaje ścieżką debugową.
- DungeonCombatCoordinator: przełączenie focus anuluje targeting, wyłącza input, czyści siatkę i ustawia mapowanie kamery/HUD dla sesji. Inspect czyści wskazanie na OnDisable.
- CombatSandboxBootstrap: sesje mają własne drivery; End/OnDestroy odpina relay obiektów i feedback bus, usuwa driver. Finish coordinatora zwalnia widoki, czyści callbacki pułapek i zapamiętany kadr. EndCampaign kończy wszystkie sesje.
- GameBootstrap blokuje zapis/przejście piętra przy HasCombat, również z perspektywy postaci eksplorującej. Nie dodano save w środku walki.
- Content: współdzielone szablony produkcja/debug, opcjonalna przypięta definicja obiektu. Stare kotwy zachowują losowanie, istniejące zapisane układy pozostają zachowane.
- Wynik review: znalezione problemy opisano wyżej i poprawiono w kodzie. Użytkownik zaakceptował zamknięcie z odnotowanym NOT RUN nowych testów. Nie jest to dowód braku wszystkich błędów; refaktor i docelowe ręczne dopracowanie contentu pozostają poza tym passem.

## UI acceptance
- PASS (wcześniejsza akceptacja użytkownika): układ HUD UIW, Inspect, AP/HP/zasób, party rail, ikony akcji, siatka skilli, tooltip kolejki, highlighty, zoom 50–200%, drugi pass pokojów.
- PASS (automatyczne): testy prefabów, HUD, clamp okien, skilli 5/6/10/12 i prezentacji.
- NOT RUN: końcowy zintegrowany UI smoke w kilku rozdzielczościach, podczas zmiany sesji i zakończenia walk. Nie oceniano nowych screenów ani nie wprowadzano dalszych zmian wizualnych w acceptance.

## Końcowy smoke milestone'u
Pracuj na osobnym zapisie testowym. Do contentu użyj `seed 404` i komend z COMBAT_V4_CONTENT_VERIFICATION.md, generując nowy loch.

1. **Seamless / lifecycle:** rozegraj trzy kolejne walki na jednym piętrze. Bez przeładowania mapy, duplikacji jednostek, podwójnych akcji, błędów Console i pozostawionych highlightów po walce.
2. **Uczestnictwo / sesje:** zostaw członka poza pokojem; obserwuj walkę, wejdź nim do pokoju i wyjdź. Sprawdź pełny odstęp do jego tury i brak odnowienia zasobów przez wyjście/powrót. Jeżeli możliwe w scenariuszu, rozpocznij drugą równoległą walkę i przełączaj aktywną postać.
3. **Geometria / AI:** sprawdź pokój L, filary, drzwi i wyjścia. AI obchodzi osłony/otwiera drzwi; celowanie respektuje LOS. Niski murek kosztuje dodatkowe AP, balustrada blokuje ruch; poza walką można wrócić przez murek bez odsuwania towarzysza.
4. **Pułapki / teren:** kolce, gaz, pieczęć, statusy i floating damage w eksploracji/walce; wygaśnięcie efektów. Koszt ścieżki i podgląd AP zgodny z ruchem, droga omija zagrożenia według ustalonych priorytetów.
5. **Outcomes:** zwycięstwo, opuszczenie pokoju/ucieczka i porażka. Komunikat powodu porażki oraz opcje wczytania/menu. Odzyskanie towarzysza u kapłana aktualizuje rail, koszt i stan drużyny.
6. **Persistence:** po zakończeniu wszystkich walk zapisz/wczytaj. Układ, drzwi, pułapki, obiekty i pokonani wrogowie zachowują stan; brak ponownych nagród. Spróbuj zapisu przy trwającej walce z perspektywy osoby poza nią — ma być blokowany.
7. **Kamera / UI:** MMB, WASD/strzałki, zoom, F i Home; obejrzyj całe piętro. Sprawdź Inspect po obu stronach, tooltipy znikające po zmianie wskazania, 2×5 skilli i overflow, zwiń/rozwiń panele. Przynajmniej 1920×1080 i 1280×720: czytelne przyciski, brak obcięcia istotnych danych i kolizji input UI z mapą.

Użytkownik zaakceptował kartę i zlecił commit/push. Checklista pozostaje do powtórzeń regresji; akceptacja nie zmienia wyników NOT RUN na PASS.
