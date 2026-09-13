# City Progression and Specialist Services v1 — acceptance

Data: 2026-09-13. Status: DONE — smoke i zamknięcie karty zaakceptowane przez użytkownika.

## Poprawki po szerszym code review

- Nieudane ulepszenie zachowuje postęp i pojemność socketów; opłata pozostaje pobierana. Usunięto nieużywane ustawienia straty postępu.
- Efekt galdurytu zwiększa końcową szansę trafienia o punkty procentowe (np. 50% + 2% = 52%), bez zmiany celności broni. Działa w rzucie trafienia i podglądzie. Zniszczony galduryt nie daje premii.
- Osadzanie sprawdza własność wyposażenia i tożsamość instancji galdurytu, również przy identycznym ID kopii.
- Kowal, alchemia i crafting galdurytów uwzględniają buyback w dokupowaniu materiałów, z ceną odkupu i zachowaniem źródła przy wycofaniu transferu.
- Nagłówek lojalności, potwierdzenie częściowej zapłaty, nagłówki salda handlu i tło licznika slotu są zapisane w prefabie. Usunięto ich runtime konstrukcję. NpcLoyaltyHeaderView ma osobny plik i serializowane referencje.
- PASS: 189/189 testów regresyjnych; po ostatniej poprawce rollbacku dodatkowy celowany przebieg 29/29 PASS. Obejmuje sprawdzenie referencji prefabów, własności, bonusu trafienia, zachowania postępu i przywracania buybacku. Nie sumować przebiegów jako liczby unikalnych testów.

Nowy smoke: nagłówek lojalności i salda, potwierdzenie sprzedaży (Sprzedaj/Anuluj), liczniki stosów; dokupienie sprzedanego materiału; nieudane ulepszenie przy zajętych socketach i save/load; bonus trafienia w podglądzie walki. Nie dodano migracji wcześniejszych zapisów zawierających już nadmiarowe sockety.

Poniżej raport pierwszego etapu acceptance, sprzed szerszego review.

## Weryfikacja

- PASS: kompilacja Unity oraz 152/152 testy EditMode dotyczące NPC, lojalności, ekonomii, handlu, craftingu, napełniania, wyposażenia, biżuterii, galdurytów i save/load.
- Nowe asercje odtworzyły błąd przed poprawką: dwie płatne operacje wyciągania pobierały 120 g, lecz saldo NPC pozostawało 0. Oba testy przeszły po poprawce.
- Dodano sprawdzenie pojedynczej wpłaty do portfela NPC przy osadzaniu galdurytu.
- Przejrzano księgowanie płatności: wpłata następuje po zakończeniu transferów albo przy płatnej próbie; ścieżki anulowane i zwracające złoto nie otrzymują wpłaty.
- Wcześniejsze smoke testy usług oraz trzech kart lojalności/ekonomii zostały zaakceptowane przez użytkownika. Nie zastępuje to smoke testu nowej poprawki.

## Naprawa

Uzupełniono wywołania istniejącego NpcEconomyService w ośmiu ścieżkach:

- zakup brakujących materiałów alchemicznych;
- zakup brakujących części wyposażenia;
- zakup brakujących składników receptur materiałowych;
- zakup brakujących składników biżuterii;
- ulepszanie wyposażenia;
- przekucie wyposażenia;
- osadzanie galdurytu;
- wyciąganie galdurytu, również płatne niepowodzenie.

Zmiana nie modyfikuje cen, szans, progów lojalności, formatu zapisu ani prefabów. Zwykły handel oraz identyfikacja galdurytów już księgowały wpłatę, więc nie dodano tam kolejnego naliczenia.

## Pozostały smoke test

1. Zanotuj saldo NPC w handlu. Dokup brakujące materiały w craftingu i wróć do handlu: saldo ma wzrosnąć dokładnie o pobraną kwotę.
2. Sprawdź płatną operację Kowala oraz osadzanie/wyciąganie Zaklinacza. Płatne niepowodzenie także zasila portfel NPC.
3. Sprawdź próbę zablokowaną brakiem złota: oba salda pozostają bez zmian.
4. Zapisz i wczytaj grę; sprawdź zachowanie salda i lojalności.

## Drugi pass: UI — 2026-09-13, DONE

- Podglądy: kwadratowe ikony o minimalnym wymiarze 80, wysokość nazwy i podpisu z preferowanej wysokości tekstu, zawijane komunikaty.
- Materiały mikstur/bomb: trzy kolumny i dwa rzędy mieszczące pięć materiałów; ustawienia bezpośrednio w prefabie.
- Poprawione etykiety bomb, klasy wyposażenia, unikalności przekucia, gniazd oraz pełnych mikstur/bomb.
- Napełnianie rozdziela zaznaczenie od stanu pełnego przedmiotu i wyjaśnia pustą sekcję materiałów.
- Identyfikacja bez wyboru nie pokazuje czerwonego kosztu 0/braku 0 ani technicznego błędu szukania instancji.
- Niedostępny tier galdurytu zachowuje bezefektowy podgląd, ale blokuje operację krótkim komunikatem. Nie rozszerzano pul efektów.
- Suwaki wybierają pełne procenty; wspólne obliczenie gniazd dla instancji i podglądu daje jedno gniazdo przy 20%. Liczba jest dostępna także w zablokowanym podglądzie.
- 53/53 testy EditMode PASS, w tym pomiar layoutu długiej nazwy, kwadratowy slot, pojemność materiałów i zaokrąglanie suwaka do 20%.
- Końcowy retest prefabu po korekcie kolejności status/przycisk: 7/7 PASS. Self-review zakończony; tymczasowe skrypty zapisujące prefab usunięte.
- Wizualny smoke zaakceptowany przez użytkownika przed commit/push. Placeholdery ikon poza zakresem.

## Granice weryfikacji

Nie wykonywano nowego wizualnego smoke testu ani pełnego przebiegu PlayMode. Testy automatyczne nie oznaczają pełnego pokrycia wszystkich wariantów contentu i wyglądu tooltipów. Istniejące ograniczenie puli efektów galdurytów wyższych tierów pozostaje zgodne z wcześniejszym ustaleniem, bez rozszerzania zakresu tej karty.
