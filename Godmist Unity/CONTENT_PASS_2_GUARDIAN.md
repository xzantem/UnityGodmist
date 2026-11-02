# CP2-09 — Strażnik T1–T5

2026-11-02. READY_FOR_SMOKE — 911/911 testów PASS + końcowy retest obrażeń 336/336 PASS (913 różnych testów). Projekt v6 zatwierdzony przez użytkownika. Produkcyjny zakres:
25 węzłów, 44 rangi, trzy aktywne: Osłona w Ruchu, Prowokacja, Kontratak.
T6/T7 usunięte z assetu drzewa; pozostają projektem w
CONTENT_PASS_2_GUARDIAN_DESIGN.md. Cztery aktywne są nadal kontraktem całego
przyszłego T1–T7, nie bieżącego T1–T5. Refleks w kolumnie 2, Czujność w 1.

## Mechaniki

- Globalnie +0,5% obu obron za SP. Refleks i Odwrócenie w istniejącym
  pipeline statystyk i rozbiciu wartości, bez podwajania przy refreshu.
- Osłona: 15% maks. HP odbiorcy, bazowo 3 tury; Przedłużona Osłona +1/2.
  Wspólny Front daje rzucającemu połowę nadanej innemu sojusznikowi tarczy,
  do 10% własnego maks. HP na 3 tury. Powtórne użycia zastępują warstwę.
- Prowokacja: 90% bazowej szansy, 3 tury, normalna odporność/tolerancja.
  Zaślepienie: −5/10/15 celności umiejętności tego celu, podczas własnej
  Prowokacji. Nieustępliwość: −10/20% jego bezpośrednich obrażeń.
  Stanowczość zwraca 50/100% rzeczywiście wydanego Impetu po nieudanej
  opłaconej Prowokacji, w dół; brak zwrotu PA i nieprawidłowego użycia.
- Czujność: pierwsze trafienie bezpośrednie −10/20% między początkami
  własnych tur; trafienie w tarczę zużywa, pudło/unik/DoT nie zużywają.
  Neutralizacja odejmuje 10/20 pp bazowej szansy sześciu rodzajów CC
  przed odpornością/tolerancją. Blokada Zaklęcia dopuszcza unik magii
  według 100/(100+unik/2), bez fizycznej celności broni.
- Taneczny Krok: jeden darmowy krok po pierwszym uniku, do końca następnej
  własnej tury. Swobodny Ruch korzysta z tego samego limitu po odzysku
  własnego sztyletu. Bez kumulacji, Impetu ani zwiększania płatnego numeru
  kroku. Przeszkody i unieruchomienie nadal blokują. Pewny Krok działa
  dopiero po rzeczywiście płatnym ruchu, do początku następnej własnej tury.
- Szybki Odwet: pierwsza reakcja na unik, zwykły Rzut Sztyletem 40/70%
  obrażeń, bez PA/Impetu, z normalnym zasięgiem, LOS i kosztem sztyletu.
  Bez duplikatów, Podcięcia Gardła/Ciosu Poniżej Pasa, zużywania krytyku
  Przewrotu i Odzyskanej Siły ani kaskady reakcji. Odepchnięcie działa
  tylko po trafionym wręcz Odwecie; respektuje odporność i kolizje.
  Adrenalina daje 6/10 Impetu raz na przedział, bez połówkowania ponad 100.
- Eskorta: +5/10 uniku dla siebie i sąsiadujących członków drużyny,
  gdy obok jest inny żywy członek drużyny; najmocniejsza premia.
  Zwarty Szyk: własne obrony +10/20% przy takim sąsiedztwie.
  Zasłonięcie: −10% bezpośrednich obrażeń dla sąsiada od atakującego
  z odległości co najmniej 2 od odbiorcy. Przywołań/klonów nie liczymy
  jako członków drużyny. Bonusy zależą od aktualnej pozycji i życia.
- Ochrona Sztyletu: −5/8/10% bezpośrednich obrażeń przy własnym sztylecie
  na ziemi na tym samym/sąsiednim polu; najsilniejsza ochrona, żywy właściciel.
- Opanowanie: pierwsza aktywna umiejętność Strażnika we własnej turze
  −10/20% bazowego Impetu, zaokrąglenie w górę przed innymi modyfikatorami.
  Ranga 2: 28/44/56 Impetu. PA bez zniżki; nieważny cel nie zużywa premii.
- Kontratak: przygotowanie do końca następnej własnej tury. Pierwsza próba
  ataku bezpośredniego uruchamia odbicie także po pudle/uniku. Jednorazowo
  100/175/250% już wylosowanej bazowej wartości przed obroną i krytykiem,
  z zachowaniem typu fizycznego/magicznego. Obrony i tarcze napastnika
  obowiązują; odbicie nie krytuje, nie osłabia przychodzącego trafienia,
  nie pobiera krytyku Przewrotu i nie tworzy łańcuchów reakcji.

## Integracja i trwałość

GuardianCombatState należy do BattleUnit, nie do zapisu postaci.
Dotychczasowe ID węzłów i asset drzewa zachowane. Znane skille, inwestycje
SP, reset i loadout używają istniejącej infrastruktury. Nie zmieniono schematu
save. Stary zapis bez Strażnika może kupować nowe węzły. Powrót z walki usuwa
combat-scoped markery; nowy BattleUnit zaczyna bez tokenów i przygotowań.
Dotychczasowe zasady generowania/utraty Impetu i starter Jerzego zachowane.

Podglądy kosztów, obrażeń i trafienia oraz AI odczytują te same reguły.
Stan planera jest oddzielony od żywej walki; podgląd nie zużywa tokenów,
nie przesuwa sztyletów ani nie wykonuje reakcji. Czujność jest liczona
oddzielnie dla kolejnych trafień w podglądzie, bez zmiany stanu celu.

Opisy mają standardowe Następne/Aktualne oraz jeden link pełnych zasad.
25 ikon native32: 22 pasywki leśno-oliwkowe, trzy aktywne wg działania.
Ikony węzła i przyznawanego skilla wskazują ten sam Sprite.
Źródła, Aseprite, eksporty i QA: Tools/Art/GuardianIcons20261102/.

## Weryfikacja

Wyniki rzeczywistych przebiegów, idempotencji authoringu i hashe:
Tools/ContentPass2/guardian-validation.json.
Checklistę smoke zawiera lokalny UNITY_HANDOFF.md.


## Korekta tooltipów po smoke

Wszystkie 25 nazw węzłów rejestrowane jako odwołania do istniejących
node ID, ikon i opisów aktualnej/następnej rangi. Dodano odmiany nazw
używane w opisach i zasadach. Szybki Odwet nie korzysta ze wpisu
catacombs.skeleton_slayer.retaliation. Słownik bossa zachowuje „Odwet”.
Pełna nazwa chroniona przed dopasowaniem krótszego fragmentu także po
pierwszym linku i przy wykluczeniu własnego wpisu. Testy obejmują ostatni
przebieg linkowania, ikony, opis wszystkich 25 talentów i tooltip w panelu.
Wyniki: Tools/ContentPass2/guardian-tooltip-links-validation.json.
