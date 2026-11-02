# CP2-06 — Zbrojny

Status: ACCEPTED — smoke PASS potwierdzony przez użytkownika 2026-11-02. Zakres zatwierdzony przez użytkownika: tylko T1–T5.

Trzecie drzewko istniejącego Wojownika, bez nowej postaci. Stabilne ID drzewa:
`tree.warrior.man_at_arms`. Graf ma 25 węzłów i 44 rangi po 1 SP. T1/T2
wymagają 0 SP wydanych w drzewie, T3/T4 — 10, T5 — 20. Wszystkie węzły
są osiągalne przed L40, ale wykupienie wszystkich rang wymaga 44 SP.
Content kończy się na L40, limit postaci nadal wynosi L50.

T6/T7 pozostają rezerwacjami projektu: nie mają assetów runtime ani ukrytych
węzłów. Nie wdrożono Żelaznej Ostoi ani wydłużania snu przez Pełne Odnowienie.
Arkusz `../design/content_pass_2.xlsx` nie został zmieniony przez tę kartę.

## Zatwierdzone reguły

- Każdy SP: +15 maksymalnego HP i 2% zredukowanych obrażeń do banku.
  Bank pomija tarcze, uniki, przekierowanie i ochronę przed spadkiem poniżej
  1 HP. Sumuje się bez dodatkowego limitu; następny atak zużywa go raz,
  także przy pudle. Pierwsze trafienie otrzymuje bonus tego samego typu.
  Bank jest lokalny dla walki. Przeliczenie obrażeń nie losuje drugi raz proców.
- Postawa Defensywna: tarcza 20% maksymalnego HP na 5 tur. Wzmocniona
  Defensywa zwiększa jej wartość względnie o 10%, czyli do 22% maksymalnego HP.
- Cios Rękojeścią: 40 PW, 70% PA, celność 70, zasięg 1, bez obrażeń.
  Najpierw 90% szansy na −50 pp odporności na stłumienie na 3 tury, następnie
  próba ogłuszenia 70% na 3 tury z uwzględnieniem aktualnych odporności.
  Odebranie Żywiołu obniża pozostałe odporności względnie o 8/16/24% na 3 tury.
  Dezorientacja wymaga trafienia i nieudanego ogłuszenia: limit końcowej szansy
  trafienia 40/20%, wszystkie ataki do końca następnej tury celu.
- Przewidywanie: 50 PW, 40% PA, 5 tur; +5 uniku za komórkę odległości
  Manhattan. Przewidywanie Magiczne: 2/3% redukcji magicznych obrażeń za komórkę.
- Odnowienie Energii: 55 PW, 100% PA; sen przez 2 tury, leczenie 20%
  maksymalnego HP w każdej turze. Rzeczywista utrata HP kończy sen i leczenie.
- Wampiryzm: 5/10% faktycznej utraty HP wroga z obrażeń bezpośrednich i DoT.
  Pomija tarcze, samookaleczenie i odbicia. Ułamki leczenia są kumulowane.
  Smak Krwi działa poniżej 50% HP. Bez Granic: nadmiar leczenia tworzy tarczę
  na 5 tur, do 10% maksymalnego HP, nowe nałożenie odświeża czas.
- Odporność Nabyta: 2% redukcji za zabicie, maksymalnie 25 zabójstw (50%).
  Licznik trwa między walkami w tym samym lochu, jest zapisywany i zeruje się
  przy wyjściu lub zamianie aktywnego lochu. Standardowy kill credit źródła.
- Nieugiętość: 20% szansy przy śmiertelnych obrażeniach, przed powaleniem
  i finalną śmiercią; przywraca 25% maksymalnego HP. Zachowuje efekty i kolejkę,
  nie emituje ostatecznej śmierci. Blokada 5 własnych tur wyłącznie po sukcesie.

Wszystkie sześć pytań projektowych zostało zamkniętych odpowiedziami użytkownika.
Pozostałe liczby i prerequisites pochodzą z Drzewka!A14:G39.

## Katalog węzłów

| Slot | Nazwa | Rangi | Wymagane węzły | Opis pierwszej rangi |
| --- | --- | ---: | --- | --- |
| t1_1 | Osłona | 3 | — | Zwiększa obronę fizyczną o 15%. |
| t1_2 | Postawa Defensywna | 1 | — | Zapewnia na 5 tur tarczę równą 20% maksymalnego zdrowia. |
| t1_3 | Wzmocniona Defensywa | 1 | t1_2 | Zwiększa siłę tarczy Postawy Defensywnej o 10%. |
| t1_4 | Wytrwałość | 1 | — | Skraca czas trwania krwawienia, zatrucia i podpalenia o 1 turę. |
| t1_5 | Odwrócenie | 3 | — | Zwiększa obronę magiczną o 15%. |
| t2_1 | Odbicie | 2 | t1_1 | Daje 10% szansy na zablokowanie 70% otrzymywanych obrażeń fizycznych. |
| t2_2 | Defensywne Odbicie | 2 | t1_2, t2_1 | Postawa Defensywna zwiększa szansę Odbicia o 10 pp. |
| t2_3 | Cios Rękojeścią | 1 | — | Trafienie ma 90% szansy na zmniejszenie odporności na supresję o 50 pp na 3 tury, a następnie 70% szansy na ogłuszenie na 3 tury. |
| t2_4 | Dezorientacja | 2 | t2_3 | Jeśli trafiony Cios Rękojeścią nie ogłuszy, ogranicza szansę trafienia celu do 40% do końca jego następnej tury. |
| t2_5 | Rozproszenie | 2 | — | Daje 10% szansy na uniknięcie obrażeń ataku magicznego. |
| t3_1 | Zwinność | 2 | — | Zwiększa unik o 8. |
| t3_2 | Zbroja Płytowa | 2 | — | Zmniejsza otrzymywane obrażenia fizyczne o 0,5 za każdy poziom zbroi. |
| t3_3 | Odebranie Żywiołu | 3 | — | Cios Rękojeścią zmniejsza pozostałe odporności o 8% na 3 tury. |
| t3_4 | Odporność Nabyta | 1 | t2_3 | Każdy pokonany przeciwnik zmniejsza otrzymywane obrażenia o 2% do wyjścia z lochu, maksymalnie o 50%. |
| t3_5 | Adaptywny | 2 | — | Otrzymanie obrażeń od umiejętności zmniejsza jej obrażenia w następnej turze o 15%. |
| t4_1 | Przewidywanie | 1 | — | Na 5 tur zwiększa unik o 5 za każde pole odległości od atakującego. |
| t4_2 | Przewidywanie Magiczne | 2 | t4_1 | Przewidywanie zmniejsza otrzymywane obrażenia magiczne o 2% za każde pole odległości od atakującego. |
| t4_3 | Witalność | 2 | — | Zwiększa maksymalne zdrowie o 10%. |
| t4_4 | Bez Granic | 1 | t4_5 | Nadmiar leczenia z Wampiryzmu tworzy na 5 tur tarczę do 10% maksymalnego zdrowia. |
| t4_5 | Wampiryzm | 2 | — | Przywraca zdrowie równe 5% obrażeń zadanych zdrowiu przeciwników. |
| t5_1 | Wigor | 2 | — | Na początku walki daje na 5 tur tarczę równą 10% maksymalnego zdrowia. |
| t5_2 | Skupienie | 2 | t5_3 | Odnowienie Energii zmniejsza otrzymywane obrażenia o 25%. |
| t5_3 | Odnowienie Energii | 1 | — | Usypia na 2 tury i przywraca 20% maksymalnego zdrowia na turę. Otrzymanie obrażeń kończy efekt. |
| t5_4 | Nieugiętość | 1 | — | Śmiertelne obrażenia mają 20% szansy na przywrócenie 25% maksymalnego zdrowia. Po sukcesie efekt odnawia się przez 5 tur. |
| t5_5 | Smak Krwi | 2 | t4_5 | Wampiryzm leczy o 50% więcej poniżej połowy maksymalnego zdrowia. |

## Implementacja i integracja

`WarriorCombatSystem.ManAtArms.cs` jest właścicielem reguł drzewa. Współdzieli
pipeline obrażeń, statusów, tarcz i leczenia. `DamageResolution` rozdziela
redukcję, przekierowanie, absorpcję, ochronę 1 HP i rzeczywistą utratę HP;
`HealingResult` rozdziela przywrócone HP i nadmiar leczenia.

Stan walki znajduje się w `WarriorCombatState`, bez mutowania wspólnych definicji.
AI kopiuje zestawy adaptacji i liczniki do odłączonego stanu; preview nie zużywa
banku i nie wykonuje losowych bloków. Nowy opcjonalny licznik zabójstw w save
ma domyślną wartość 0. Stare inwestycje, SP i dwa dotychczasowe drzewa pozostają.

Editor API utworzyło jedno drzewo i cztery skille ze stabilnym effect ID `main`,
podpięło trzecią zakładkę i dopisało skille do głównego katalogu. Dwa istniejące
drzewa zachowano bajt w bajt; w klasie/katalogu dodano tylko referencje.
Ponowny authoring jest idempotentny. Nie jest potrzebny ręczny wiring Editora.

Każdy z 25 węzłów ma dedykowaną ikonę 32×32, źródło, Aseprite i redakcyjne
opisy rang. Szczegóły warunków dostępne są w rozwijanych regułach tooltipów.
Cztery skille korzystają z istniejących póz Wladimira: command (Postawa,
Przewidywanie), punch (Cios Rękojeścią), bandage (Odnowienie Energii).
Nie powstały nowe animacje postaci. Stany obronne mają widoczne ikony/liczniki.

## Weryfikacja i odbiór

Wyniki i hashe przebiegów: `Tools/ContentPass2/man-at-arms-validation.json`.
Surowe XML/logi: `../LocalHandoffs/cp2-06-armored-20261102/`.
Końcowa regresja mechanik: 511/511 PASS. Wcześniejsze testy tooltipów,
trzeciej zakładki i dwukrotnego JSON roundtrip starego/nowego zapisu przeszły.
Authoring: 25 węzłów, 4 skille, 25 różnych sprite'ów, prerequisites, brak
zmian 50 starszych węzłów i idempotencja — PASS. Ikony: dokładne próbkowanie
nearest i roundtrip Aseprite 25/25 PASS. Walidacja katalogu planu: 30/30 PASS.

Testy uruchomiono wyłącznie w ukrytej kopii batch Unity; bez przejmowania okien
użytkownika. Nie wykonano builda gracza, pomiarów FPS ani ręcznego smoke/renderu
walki w oknie gry. Ręczny smoke użytkownika zakończony PASS dnia 2026-11-02.
CP2-03/04 i pozostałe wcześniejsze zmiany użytkownika pozostają zachowane.
Użytkownik polecił commit/push wyłącznie karty 06 i przejście do 07.


## Korekta palet — 2026-11-02

Odtworzono źródłowe prompty i rozdzielono palety pasywek od aktywnych skilli.
Szczegóły: Tools/Art/ICON_COLOR_CONVENTIONS.md. Cztery aktywne skille
Zbrojnego mają teraz bordowe tło aktywnego Wojownika; złoto ochrony,
błękit kontroli i zieleń odnowienia pozostają w efektach. T5 Dowódcy
i Awanturnika porównano ze starszymi ikonami — były zgodne, bez zmian.
U skauta poprawiono wszystkie 10 nowych ikon: 4 śliwkowe pasywki Skrytobójcy,
3 pasywki teal Tancerza i 3 aktywne z paletą odpowiednią dla ich działania.
Korekta obejmuje 14 produkcyjnych PNG; zachowano pliki meta/GUID, węzły,
referencje aktywnych skilli, parametry importerów i cały kod gameplayu.
Źródła przed zmianą i prompty edycji zachowano. Odtworzono natywne PNG32
i Aseprite: pełne próbkowanie nearest i roundtrip obu paczek 35/35 PASS.
Porównanie przed/po/wzorzec obejrzano w32×32 i powiększeniu. Raport:
Tools/Art/IconPaletteFix20261102/validation.json. Pozostaje ręczny odbiór
kolorów w grze; wcześniejszy smoke mechanik nie jest powtarzany ani cofany.
Bez commit/push tej korekty.
