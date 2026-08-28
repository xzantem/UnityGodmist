# Unity handoff

Karta: `Zablokowac interakcje z obiektami przez sciane v1`

Implementacja jest gotowa do recznego smoke testu w Unity Editorze. Nie uruchamialem Unity Test Runnera ani `Godmist Unity Test Mirror`.

## Model dzialania

- Interakcja nadal wymaga dotychczasowego dystansu od aktywnego lidera.
- Oprocz dystansu system sprawdza bezposrednia linie komorek od komorki lidera do obiektu.
- Kazda krawedz na tej linii musi byc przechodnia w `DungeonFloorMap`.
- Sciana, zamkniete drzwi albo przeszkoda po drodze blokuja interakcje.
- Przeszkoda blokuje interakcje, gdy ma `BlocksMovement` albo `BlocksLineOfSight`.
- Dotyczy skrzyn, leverow, conditionow, drzwi i cial przeciwnikow.
- Zamkniete zwykle drzwi mozna otworzyc z obu sasiadujacych komorek; drzwi `LeverOnly` nadal nie maja bezposrednej interakcji.

## 1. Interakcja po prostej linii

1. Uruchom dungeon z aktywnym `DungeonDefinition`.
2. Podejdz aktywnym liderem do skrzyni, levera albo conditionu tak, aby obiekt byl w zasiegu interakcji i nic nie stalo pomiedzy w tej samej linii komorek.
3. Nacisnij `E`.
4. Powtorz test dla ciala przeciwnika, jesli w lochu istnieje juz cialo.

Oczekiwany rezultat:

- interakcja dziala tak jak przed zmiana, gdy bezposrednia linia nie jest zablokowana;
- skrzynia otwiera loot, lever zmienia stan, condition zostaje spelniony, a cialo otwiera loot;
- aktywny lider pozostaje jedynym zrodlem interakcji.

## 2. Obiekt za sciana

Najlatwiej znalezc dwie komorki po przeciwnych stronach sciany wewnetrznej albo granicznej, gdzie odleglosc nadal jest mniejsza od promienia interakcji.

1. Ustaw lidera po jednej stronie sciany.
2. Upewnij sie, ze skrzynia, lever, condition albo cialo jest po drugiej stronie i znajduje sie w zasiegu dystansu.
3. Nacisnij `E`.
4. Podejdz do obiektu od strony, z ktorej nie ma sciany pomiedzy komorkami i ponownie nacisnij `E`.

Oczekiwany rezultat:

- pierwsza proba nie wykonuje interakcji mimo odpowiedniego dystansu;
- po obejściu sciany interakcja dziala;
- obiekt nie moze byc aktywowany ani zlootowany przez sciane.

## 3. Obiekt za przeszkoda

1. Uzyj template'u albo wygenerowanego pokoju z przeszkoda na drodze miedzy liderem i interaktywnym obiektem.
2. Ustaw przeszkode tak, aby lider nadal byl w zasiegu dystansu od obiektu.
3. Nacisnij `E` dla skrzyni, levera, conditionu albo ciala.
4. Obejdz przeszkode i sprobuj ponownie.

Oczekiwany rezultat:

- przeszkoda z `BlocksMovement` albo `BlocksLineOfSight` blokuje interakcje;
- po obejściu przeszkody interakcja dziala, o ile pozostaje zachowany dystans;
- nie ma interakcji przez zajeta komorke przeszkody.

## 4. Zamkniete drzwi

1. Znajdz zamkniete zwykle drzwi, ktore nie sa `LeverOnly`.
2. Podejdz liderem do jednej z komorek przylegajacych do krawedzi drzwi.
3. Nacisnij `E` i sprawdz, ze drzwi sie otwieraja.
4. Przejdz na druga strone, zamknij drzwi i ponownie nacisnij `E` z drugiej komorki.
5. Jesli istnieja drzwi `LeverOnly`, sprawdz, ze `E` nie otwiera ich bez spelnienia wymaganego warunku.

Oczekiwany rezultat:

- zwykle drzwi mozna otworzyc z obu stron;
- zamknieta krawedz drzwi nie pozwala interakcji przejsc na druga strone, ale nie blokuje interakcji z samego sasiadujacego pola;
- po otwarciu drzwi ich przejscie i dalszy ruch dzialaja normalnie;
- `LeverOnly` zachowuje dotychczasowa blokade bezposredniej interakcji.

## 5. Wszystkie typy obiektow i regresja

Sprawdz osobno:

- skrzynie;
- levery;
- conditiony;
- zwykle drzwi;
- ciala zabitych przeciwnikow.

Dodatkowo sprawdz, ze:

- ruch postaci nadal respektuje sciany i przeszkody;
- pulapki nadal aktywuja sie przez kolizje, a nie przez interakcje `E`;
- otwieranie drzwi przez lever/condition nadal aktualizuje stan progresji;
- interakcje w otwartej przestrzeni nie zostaly przypadkowo zablokowane.

## Testy automatyczne do recznego uruchomienia

W Unity Test Runnerze uruchom:

- `DungeonCorridorPathfinderTests`, w tym testy `HasDirectAccess`;
- `DungeonObjectPlacementSystemTests`;
- `DungeonSemanticAnchorTests`;
- `DungeonEnemyBodyTests`;
- pozostale testy dungeon runtime.

Smoke test tej karty zostal zaakceptowany. Jesli pojawi sie regresja, podaj seed, typ obiektu i screenshot.
