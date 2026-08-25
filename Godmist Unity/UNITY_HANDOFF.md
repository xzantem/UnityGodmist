# Unity handoff

Karta: `Combat v3 - dzialalnosc skilli toggle`

## Inne kroki Editor

Otworz `Assets/_Project/ScriptableObjects/Skills/SD_TestAura.asset` i ustaw:

- w `Effects[0]` wybierz `ApplyUniqueBuff` i `AuraStatus` (stara wartosc
  `LegacyApplyAura` nie jest obslugiwana),

- `Toggle`: wlaczone,
- `AP Cost (%)`: `0`,
- `Resource Cost`: `0`,
- `Resource Type`: zasob uzywany przez Jerzego, jezeli skill ma tez zwykly koszt
  aktywacji (sam upkeep zawsze pobiera z puli wlasciciela),
- `Upkeep Flat Resource Cost`: `20`,
- `Upkeep Max Resource Percent`: `0.25`.

Koszt podtrzymania jest wieksza z dwoch wartosci, czyli
`max(20, ceil(25% maksymalnego zasobu))`. Oba pola moga miec wartosc `0`.

Toggle jest dostepny tylko dla `ModifyStat`, `ModifyVar`,
`BlockResourceRegeneration` oraz `ApplyUniqueBuff` w wariantach
`ActionPointsNextTurn`, `OnHitStatus`, `DamageRedirect` i `AuraStatus`.
Gdy toggle jest wlaczony, Inspector nie pokazuje czasu trwania jego efektu:
efekt konczy sie tylko po wylaczeniu toggle. Wyjatek: `OnHit Status Duration`
okresla czas statusu zadanego po trafieniu, nie czas samego buffa toggle.

Upewnij sie, ze `SD_TestAura` znajduje sie wsrod znanych skilli Jerzego i jest
w jego aktywnym loadoucie. Nie sa potrzebne zadne nowe obiekty, komponenty ani
referencje w scenie lub prefabie.

## Smoke test

1. Uruchom walke Jerzym z `Warding Aura` na pasku skilli i uzupelnij jego zasob
   tak, aby mogl zaplacic koszt podtrzymania.
2. Wlacz aure. Ikona skilla ma zostac podswietlona na zielono, a efekty aury
   maja pozostac aktywne.
3. Sprobuj kliknac skill ponownie w tej samej turze. Stan nie moze sie zmienic.
4. Doprowadz do poczatku kolejnej tury Jerzego. Sprawdz, ze pobrano dokladnie
   `max(20, ceil(25% maksymalnego zasobu))` i aura nadal jest aktywna.
5. W tej turze wydaj pozostale AP oraz zasob, a nastepnie kliknij aktywna aure.
   Ma sie wylaczyc mimo braku AP/zasobu; podswietlenie, aura oraz status
   nalozony przez aure na przeciwniku maja zniknac od razu, bez ruchu jednostki.
6. W kolejnej turze wlacz aure ponownie, ustaw zasob ponizej kosztu podtrzymania
   i zakoncz ture. Na poczatku nastepnej tury aura ma wylaczyc sie automatycznie,
   nie pobierajac czesciowej oplaty. W tej samej turze moze zostac wlaczona tylko
   raz, ale jej wylaczenie zawsze pozostaje dostepne.
7. Ustaw oba pola upkeepu na `0`, uruchom walke ponownie i pozostaw aure aktywna
   przez co najmniej dwie kolejne tury Jerzego. Zasob nie moze byc pobierany.
8. Gdy aura jest aktywna, zabij jej wlasciciela testowo. Jej efekty, aura oraz
   podswietlenie nie moga pozostac na innych jednostkach.

Oczekiwany rezultat:

- aktywacja placi zwykly koszt skilla, a wylaczenie jest zawsze darmowe;
- konkretny toggle moze zostac wlaczony najwyzej raz podczas tury wlasciciela;
- wylaczenie toggle jest zawsze dostepne i nie zuzywa AP ani zasobu;
- upkeep jest pobierany od pierwszej kolejnej tury po aktywacji;
- brak pelnego kosztu automatycznie wylacza toggle i usuwa jego efekty;
- upkeep `0` utrzymuje toggle bez pobierania zasobu;
- aktywny toggle jest sygnalizowany samym podswietleniem ikony.
