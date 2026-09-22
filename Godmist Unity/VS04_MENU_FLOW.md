# VS-04 — produkcyjne menu i start gry

Zatwierdzone: pionowe menu po lewej (Kontynuuj, Nowa gra, Wczytaj, Ustawienia, Wyjdź), osobny panel zapisów, głośność główna/rozdzielczość/pełny ekran. Nowa kampania nie usuwa starych zapisów. Kontynuacja szuka najnowszego poprawnego zapisu wszystkich kampanii. Błąd pozostawia użytkownika w menu z komunikatem.

## Implementacja

- MainMenuView: przyciski, komunikaty, blokada akcji podczas ładowania i otwartych paneli. Reużywa SaveLoadPanelView oraz istniejący SaveManager/SceneLoader.
- GameBootstrap.TryStartNewGame tworzy świeże CampaignId i przechodzi do skonfigurowanej sceny Arungardu, zachowując rollback sesji przy braku sceny. TryContinueGame odtwarza pierwszy poprawny zapis. TryReplaceSession odrzuca ponowne wejście podczas ładowania.
- SaveManager.TryLoadLatestValid: próbuje zapisy według UpdatedAtUtc, pomija błędne JSON i nieodtwarzalny content. Nie zapisuje ani nie usuwa plików.
- MainMenuSettingsView: ręczne Zastosuj; globalna głośność AudioListener oraz tryb okno/pełny ekran i dostępne rozdzielczości. PlayerPrefs przechowuje ustawienia poza kampaniami. Przy starcie nie stosuje rozdzielczości niedostępnej na aktualnym monitorze. Editor zapisuje wybór, ale nie zmienia Game View; wyświetla stosowny komunikat.
- Panel wczytywania resetuje poprzedni komunikat i ukrywa przycisk zapisu w trybie Load.

## Granice i weryfikacja

39/39 EditMode PASS w izolowanej kopii (`vs04-menu-verified.xml`). Nowe przypadki: wybór kampanii po dacie, fallback przy brakującym contencie i uszkodzonym JSON, pusta lista, zachowanie wcześniejszego zapisu po utworzeniu nowej kampanii. Self-review bez znanych krytycznych problemów w zakresie kodu.

Statyczny UI został skonfigurowany w Bootstrap za zgodą użytkownika. Scena zawiera wszystkie referencje, lokalną interaktywną wersję suwaka i układ dopasowany do 1080p/720p. Build Settings były już poprawne. 42/42 EditMode PASS (vs04-ui-verified.xml), automatyczny Play Mode smoke menu/nowej kampanii/Arungardu PASS. Podglądy graficzne sprawdzone. Nie wykonano Screen.SetResolution/Application.Quit w samodzielnym buildzie. VS-05/06 odpowiadają za zawartość nowej kampanii; ta karta nie dodaje czterech postaci ani startowego ekwipunku. Nie zmienia formatu zapisów.

## Poprawka opóźnień menu (2026-09-22)

Refresh menu nie odczytuje już listy zapisów przy zamykaniu ustawień ani zmianie stanu ładowania. SaveManager zachowuje podsumowania w pamięci, wykrywa zmiany długości/dat plików oraz unieważnia wpis przy zapisie. Nie cache'uje odtworzonej sesji i nie zmienia plików użytkownika. Wybór zapisu aktualizuje zaznaczenie i szczegóły bez przebudowy listy.

44/44 EditMode PASS (vs04-delay-tests.xml), w tym ponowne użycie podsumowania, zewnętrzna edycja/usunięcie i nadpisanie quicksave. Play Mode smoke PASS, zachowanie istniejących wierszy po kliknięciu potwierdzone. Pomiar na 268 zapisach (~143 MB): pierwszy pełny odczyt 4608 ms, kolejne odświeżenia średnio 33 ms; otwarcie panelu ~199 ms, wybór zapisu ~1 ms. Pierwsze indeksowanie po uruchomieniu nadal jest synchroniczne. Artefakty lokalne: .codex/vs04-ui/save-timing.txt, play-smoke.txt; .codex/vs04-delay-smoke.log.

Odbiór VS-04 zatwierdzony przez użytkownika (commit/push). Dodatkowo 44/44 PASS na samodzielnym zestawie źródeł z indeksu Git, bez oczekującego pakietu VS-18 (vs04-commit-tests.xml).
