# VS-07 — content Scouta

Status: DONE (2026-09-22). Użytkownik autoryzował commit/push oraz przejście do VS-08. Pełny test ręczny w Unity odłożony do acceptance; nie jest oznaczony jako zaliczony. Implementacja i automatyczna weryfikacja zakończone.

## Zakres

Jerzy zaczyna nową kampanię z pięcioma umiejętnościami: Rzut Sztyletem, Wymiana, Chmura Dymu, Linka z Hakiem, Celny Rzut. Dwa drzewka: Skrytobójca i Tancerz Ostrzy, po 20 węzłów w pierwszych czterech tierach. Guardian pozostaje assetem poza ofertą klasy. Zachowano ID istniejącego Rzutu Sztyletem, Skrytobójcy i Tancerza Ostrzy oraz pierwszych 20 węzłów Skrytobójcy; Gwałtowne Uderzenia i Styl Karambitowy mają zatwierdzone pozycje T2/T3.

Źródła: VS01_CONTENT_CATALOG.md, content_pass_1.xlsx oraz późniejsze decyzje użytkownika. Powtarzalny authoring: `Godmist/Content/Configure VS-07 complete Scout` (VS07ScoutSetup). Assety już skonfigurowane i zapisane; użytkownik nie musi uruchamiać setupu.

## Reguły

- Złość: +10 za trafienie bez DoT, Kontrola Psychiczna +1/3/5; limit 100. Bonus otrzymywanych obrażeń dla całej drużyny. Zemsta działa od bezpośrednich otrzymanych obrażeń.
- Seria Ciosów: po trzech ciosach osobne 50% dla każdego dodatkowego slotu. Dwa sloty dają 25%/50%/25% dla 0/1/2 dodatkowych ciosów. Taniec Zagłady zwiększa liczbę slotów.
- Linka: leżący własny sztylet odzyskuje bez losowania; 75% dotyczy wyłącznie przyciągnięcia przeciwnika.
- Ostry Nóż: własny wbity sztylet można wyjąć z sąsiedniego pola za 0 PA przez PPM albo LPM, gdy nie jest wybrana inna akcja. Wyjęcie odzyskuje sztylet i próbuje nałożyć krwawienie. Jeden zużyty sztylet nie może być zarazem wbity i upuszczony; śmierć celu uwalnia wbite noże.
- Kierunek jednostki wyznacza ostatni ruch lub atak (zatwierdzone 2026-09-22); atak od przeciwnej strony jest atakiem w plecy. Początkowo jednostka zwraca się ku przeciwnikom.
- Wojownik Cieni daje ruch o jedno sąsiednie wolne pole i niewidzialność na 3 tury, bez przechodzenia przez ściany. Nie jest to zapis „1 ładunek”.
- Rykoszet wybiera pierwszy cel wskazany przez gracza, następnie najbliższe niepowtórzone cele w zasięgu poprzedniego odbicia; remis rozstrzyga stabilne ID. Każde odbicie sprawdza widoczność między celami.
- Rzut Sztyletem w zwarciu ma 60% bazowej siły. Styl Karambitowy i premie wręcz mnożą tę wartość. Startowy Rzut, Celny Rzut, Zatruty Sztylet i Szybkie Ciosy nie zużywają noży w zwarciu; Rykoszet zużywa jeden.
- Tonik: bonus 5 tur, bezpośrednio po nim kara 3 tur (2 z Mocną Wątrobą); ponowne użycie zastępuje zaplanowaną wcześniejszą karę.
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
