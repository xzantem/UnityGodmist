# Regresja przed pierwszym wydaniem — 2026-10-11

Wszystkie 39 przypadków nieprzechodzących po cleanupie zostały rozwiązane.
Końcowy pełny EditMode: **2406/2406 PASS, 0 FAIL, 0 pominiętych**.
Nie usunięto ani nie wyłączono testów. Zmienione nazwy odpowiadają obecnemu
zachowaniu UI; lokalna mapa wyników śledzi wszystkie pierwotne 39 przypadków.

## Poprawione błędy

- Wieloefektowa akcja zachowuje footprint pierwotnej bariery, nawet gdy wcześniejszy
  efekt zniszczył przeszkodę. Szarża zatrzymuje się przed nią bez dodatkowego trafienia.
- Postać stojąca na zablokowanym polu może wyjść do poprawnego celu również przy
  kliknięciu dokładnego punktu świata. NavMesh przekazuje takie przypadki do istniejącej
  nawigacji odzyskującej ruch; wejście z powrotem w przeszkodę pozostaje zabronione.
- Menu zdejmowania wyposażenia sprawdza miejsce w plecaku przed akcją i pokazuje
  powód blokady. Operacja nadal ponownie sprawdza własność przedmiotu i stan walki.
- Tooltip nie udostępnia zniszczonego obiektu Unity jako aktywnej instancji;
  ukrywanie widoku toleruje zniszczone dzieci. Katalog animacji odtwarza sprite'y
  i klipy, które Unity usunęło z pamięci. Dodano trzy przypadki regresji lifecycle.
- Narożnik North/Side ma jednego właściciela zamiast dwóch nakładających się sprite'ów.
- Naprawiono uszkodzone kodowanie UTF-8 planów pokoi i uzupełniono specyfikację
  dwóch istniejących ścian korytarza. Test uwzględnia późniejszy authoring drzwi
  i celowe przesunięcie pułapki poza pola interakcji.

## Zaktualizowane testy

Oczekiwania sprawdzają aktualny wspólny popup przedmiotów i bogaty tooltip jednostki,
krótkie polskie opisy efektów, przewijane materiały alchemii, padding siatki skilli
oraz jeden batch narożników zaznaczania. Testy grafik sprawdzają używany w runtime
katalog ośmiu kierunków, idle/chód i natywne importy 64×64 przy 32 PPU, Point,
bez kompresji i mipmap. Zachowano kontrole fallbackowych sprite'ów drużyny.

Poprawiono również fixture'y dziedziczonych pól, argumenty wywołań reflection
i asercje NUnit dla leniwych kolekcji. Test gruzu sprawdza bezpieczne obejście
fizycznej przeszkody oraz odrzucenie celu wewnątrz niej; jego walidator ignoruje
collidery innych scen. Kontrole faz pikseli narożników korzystają z kierunków
logicznej geometrii zamiast przesuniętych środków rendererów.

## Weryfikacja i evidence

- Unity 6000.4.0f1, pełny EditMode z renderowaniem D3D11: 2406/2406 PASS.
- Wszystkie 39 pierwotnych przypadków odnalezione i PASS w końcowym XML.
- Windows x64 release build: Succeeded, 0 błędów, 874 ostrzeżenia,
  259 169 633 bajty. Pięć włączonych scen: 0 brakujących skryptów.
- Final self-review i kontrola whitespace: PASS. Kod i katalogi w kopii testowej
  zgodne z głównym projektem. Bez edycji serialized Unity state i modelu zapisu.
- Bez stage, commit, push i publikacji. Główny Editor nie był zamykany ani przerywany.

Evidence: `C:/Users/Patryczku/Documents/Godmist/LocalHandoffs/release-regression`:
`full-editmode.xml`, `full-editmode.log`, `failure-resolution-map.json`,
`final-build.log`, `final-build-result.txt`, `current-card.diff`, `source-manifest.json`.
Wcześniejsze iteracje zachowano do diagnozy, w tym pierwszy pełny przebieg z jednym
błędem izolacji testu fizyki.

Build: `C:/Users/Patryczku/Documents/Godmist-release-validation-20261011/Builds/Windows`.
Do dystrybucji potrzebna jest cała zawartość folderu. Symbole Burst przeniesiono do
`Godmist-release-validation-20261011/Symbols/release-regression-20261011`.

## Pozostałe sprawdzenia przed publikacją

READY_FOR_SMOKE dla tej karty; bez ręcznego wiring. Sprawdzić w Play Mode zdejmowanie
przedmiotu przy pełnym plecaku, zmianę scen z tooltipami i animacjami, narożniki ścian,
wyjście postaci z nakładającej się przeszkody oraz szarżę niszczącą przeszkodę.
Następnie przejść nowa gra → wyprawa → walka → zapis/wczytanie w gotowym buildzie.
Manualny gameplay smoke po tej karcie nie został wykonany.

864 z 874 ostrzeżeń buildu nadal dotyczą limitu głębokości rekurencyjnej serializacji
efektów skilli. To wcześniejszy, osobny problem wymagający analizy zgodności danych;
zielone testy nie stanowią jego rozwiązania ani pełnej akceptacji wydania.
