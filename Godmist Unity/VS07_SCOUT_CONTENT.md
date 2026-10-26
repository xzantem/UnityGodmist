# VS-07 — content Scouta

Status: DONE (2026-09-22). Użytkownik autoryzował commit/push oraz przejście do VS-08. Pełny test ręczny w Unity odłożony do acceptance; nie jest oznaczony jako zaliczony. Implementacja i automatyczna weryfikacja zakończone.

## Zakres

Jerzy zaczyna nową kampanię z pięcioma umiejętnościami: Rzut Sztyletem, Wymiana, Chmura Dymu, Linka z Hakiem, Celny Rzut. Dwa drzewka: Skrytobójca i Tancerz Ostrzy, po 20 węzłów w pierwszych czterech tierach. Guardian pozostaje assetem poza ofertą klasy. Zachowano ID istniejącego Rzutu Sztyletem, Skrytobójcy i Tancerza Ostrzy oraz pierwszych 20 węzłów Skrytobójcy; Gwałtowne Uderzenia i Styl Karambitowy mają zatwierdzone pozycje T2/T3.

Źródła: VS01_CONTENT_CATALOG.md, content_pass_1.xlsx oraz późniejsze decyzje użytkownika. Powtarzalny authoring: `Godmist/Content/Configure VS-07 complete Scout` (VS07ScoutSetup). Assety już skonfigurowane i zapisane; użytkownik nie musi uruchamiać setupu.

## Zatwierdzona zmiana balansu — Linka z Hakiem (2026-10-09)

Status: bazowa zmiana wdrożona w runtime, definicji umiejętności, authoringu i podglądzie kosztów. Historyczna weryfikacja VS-07 nie obejmuje tej zmiany.

- Koszt bazowy: 25 PP i 25% PA.
- Cel — przeciwnik: 80% szansy na przyciągnięcie o 3 pola; pełny koszt bazowy.
- Cel — własny sztylet: odzyskuje go do ekwipunku bez losowania. Bazowo oba składniki kosztu są o 20% niższe: 20 PP i 20% PA.
- Bez dodatkowego limitu jednego odzysku na turę; każde użycie opłaca PP i PA.
- Sprawny Odzysk (Tancerz Ostrzy, T4:2, jedna ranga za 1 punkt) zastępuje Mocną Wątrobę i zwiększa zniżkę odzysku do 60%: 10 PP i 10% PA. Nie wymaga Siły Naparu ani innego konkretnego węzła; nadal obowiązuje standardowy próg dostępu do T4. Koszt przyciągania przeciwnika pozostaje bez zmian. Bez dodatkowego efektu.
- Nowe ID węzła: `node.scout.blade_dancer.efficient_recovery`. Wczytywanie zapisów migruje dawne `node.scout.blade_dancer.strong_liver` na nowe ID bez utraty punktu; obecność obu ID nie podwaja inwestycji. Kolejny zapis zawiera wyłącznie nowe ID. Authoring i importer ikon używają nowego węzła; ikona pochodzi z Linki z Hakiem. Stary efekt skracania kary Toniku usunięto. Runtime wspiera modyfikator parametru `knife_recovery_cost_multiplier` (bazowo 0.8, z pasywką 0.4).
- Bez osobnego skilla do dobywania sztyletów ani premii do ruchu. Tonik Pobudzający pozostaje bez zmian.

Weryfikacja zmiany (2026-10-09): kompilacja Unity oraz 47/47 testów `ScoutContentTests` i `ScoutMechanicsTests` PASS, `out/hook-balance-verified.xml`. Obejmuje pełny koszt i parametry przyciągania, obniżony koszt odzysku, dostępność przy zasobach wystarczających tylko na odzysk oraz brak pobrania kosztów/odzysku przy niewystarczających PA lub PP. Manualny smoke: NOT RUN. Bez ręcznego wiring w Editorze.

Weryfikacja podmiany Mocnej Wątroby na Sprawny Odzysk (2026-10-09): kompilacja Unity oraz 67/67 testów `ScoutContentTests`, `ScoutMechanicsTests` i `SkillTreeProgressionSystemTests` PASS, `out/efficient-recovery-tests.xml`. Obejmuje koszt odzysku 10 PP/10% PA, granice dostępnych zasobów, niezmieniony koszt przyciągania przeciwnika, usunięcie skrócenia kary Toniku, reset inwestycji, opis kosztów i odtworzenie starego ID inwestycji z zapisu. Manualny smoke: NOT RUN.

Weryfikacja nowego ID i migracji (2026-10-09): kompilacja Unity oraz 80/80 testów Scouta, progresji drzewek, snapshotów/serializacji zapisów i ikon pasywek PASS, `out/efficient-recovery-id-verified.xml`. Stare ID migruje do `efficient_recovery`; zapis zawierający oba ID daje jedną inwestycję, a kolejny snapshot zawiera tylko nowe ID. Importer ikon obsługuje historyczny katalog i wiąże nowy węzeł z ikoną Linki z Hakiem.

## Reguły wdrożone

- Złość: +10 za trafienie bez DoT, Kontrola Psychiczna +1/3/5; limit 100. Bonus otrzymywanych obrażeń dla całej drużyny. Zemsta działa od bezpośrednich otrzymanych obrażeń.
- Seria Ciosów: po trzech ciosach osobne 50% dla każdego dodatkowego slotu. Dwa sloty dają 25%/50%/25% dla 0/1/2 dodatkowych ciosów. Taniec Zagłady zwiększa liczbę slotów.
- Linka: leżący własny sztylet odzyskuje bez losowania za 20 PP i 20% PA; przyciągnięcie przeciwnika o 3 pola ma 80% szansy i kosztuje 25 PP oraz 25% PA.
- Ostry Nóż: własny wbity sztylet można wyjąć z sąsiedniego pola za 0 PA przez PPM albo LPM, gdy nie jest wybrana inna akcja. Wyjęcie odzyskuje sztylet i próbuje nałożyć krwawienie. Jeden zużyty sztylet nie może być zarazem wbity i upuszczony; śmierć celu uwalnia wbite noże.
- Kierunek jednostki wyznacza ostatni ruch lub atak (zatwierdzone 2026-09-22); atak od przeciwnej strony jest atakiem w plecy. Początkowo jednostka zwraca się ku przeciwnikom.
- Wojownik Cieni daje ruch o jedno sąsiednie wolne pole i niewidzialność na 3 tury, bez przechodzenia przez ściany. Nie jest to zapis „1 ładunek”.
- Rykoszet wybiera pierwszy cel wskazany przez gracza, następnie najbliższe niepowtórzone cele w zasięgu poprzedniego odbicia; remis rozstrzyga stabilne ID. Każde odbicie sprawdza widoczność między celami.
- Rzut Sztyletem i Celny Rzut w zwarciu mają 60% bazowej siły (Celny Rzut: decyzja użytkownika 2026-10-05). Styl Karambitowy i premie wręcz mnożą tę wartość. Celny Rzut nadal gwarantuje krytyczne trafienie. Startowy Rzut, Celny Rzut, Zatruty Sztylet i Szybkie Ciosy nie zużywają noży w zwarciu; Rykoszet zużywa jeden.
- Tonik: bonus 5 tur, bezpośrednio po nim kara 3 tur; ponowne użycie zastępuje zaplanowaną wcześniejszą karę. Po zastąpieniu Mocnej Wątroby Sprawnym Odzyskiem żadna z tych pasywek nie skraca kary Toniku.
- Podcięcie Gardła: próg pięciokrotności obrażeń, boss zamiast egzekucji otrzymuje podwojone obrażenia przy udanym procu. Oznaczenie bossa pochodzi z definicji lochu i BossEnemyIndex.

## Integracja

Aktywne skille z węzłów są wyliczane z inwestycji. Nie dopisują się do listy umiejętności poznanych na stałe; po resecie nie pozostają w aktywnym zestawie. Zapis zachowuje inwestycje i aktywne ID, a odtworzenie rozpoznaje skille udostępniane przez drzewka. Nowe definicje dodano do GC_MainContentCatalog.

Aktualizacje UI: lista poznanych skilli, tooltip odblokowania i kosztów węzła, ukrycie pustych tierów, wybór pierwszego celu łańcucha, odzysk Linką, koszt ruchu z Lekkiego Kroku oraz prezentacja Złości, Słabego Punktu, wbitych sztyletów i Dogłębnej Analizy. Nie dodano nowych prefabów ani wymaganego ręcznego wiring.

Zmiana startowych skilli dotyczy nowych kampanii. Istniejące zapisy zachowują zapisany zestaw poznanych umiejętności; smoke startu należy wykonać na nowej kampanii. Ikony wykorzystują istniejącą grafikę zastępczą; docelowa grafika należy do VS-22.

## Weryfikacja

- Pełny EditMode: 1302/1302 PASS, `.codex/vs07-all3.xml` (z urządzeniem graficznym).
- Końcowe testy Scouta, drzewek/loadoutu i prezentacji statusów: 72/72 PASS, `.codex/vs07-final-check.xml`.
- Obejmują: pięć skilli startowych i 40 węzłów, grants/save/load/reset, trafienia i obrażenia, niezależne losowania, Złość, sztylety, odzysk Linką i koszt, Rykoszet, kierunek/plecy, gwarantowany kryt, ruch Wojownika Cieni, czas Toniku, idempotentny limit noży, próg egzekucji i boss.
- Ręczny smoke Unity: NOT RUN — lista w UNITY_HANDOFF.md.
- Bez commita/pusha; jedna karta = jeden commit po akceptacji smoke.
