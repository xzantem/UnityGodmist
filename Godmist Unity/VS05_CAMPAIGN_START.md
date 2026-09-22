# VS-05 — początek kampanii w Arungardzie

## Zatwierdzony zakres

Nowa kampania: Jerzy, Wladimir, Alison i Reimond, poziom 1; 500 złota wspólnie; bez mikstur, biżuterii i galdurytów. Każda postać otrzymuje założoną broń i pancerz L1. Podane statystyki części są importowane bez przeliczenia jakości: Normal / Common. Material=None oznacza brak materiału i brak receptury. Wyposażenie startowe ma wartość sprzedaży 0; nie można go ulepszać ani reforgować. Darmowy odkup nie tworzy nowej instancji przedmiotu.

| Klasa | Atak | CritMod części | Crit chance | Celność | Obrona fiz./mag. | HP z pancerza | Unik |
|---|---|---|---|---|---|---|---|
| Wojownik (JSON 0) | 7–14 | 0,5 | 3% | 78 | 6 / 2 | 75 | 8 |
| Scout (JSON 1) | 3–15 | 1 | 7% | 80 | 4 / 0 | 50 | 10 |
| Mag (JSON 2) | 5–10 | 0,6 | 0% | 0 | 2 / 6 | 30 | 4 |
| Paladyn (JSON 3) | 7–10 | 0,5 | 2% | 75 | 8 / 8 | 100 | 6 |

Są to wkłady wyposażenia do istniejącej stat pipeline, nie końcowe statystyki postaci. Bazowe statystyki, portrety i skille czterech postaci pozostają przedmiotem VS-06–09.

## Przepływ

GameBootstrap tworzy kompletną oddzielną sesję przez NewCampaignService, zanim zmieni aktywną kampanię. Błędny profil nie modyfikuje dotychczasowej sesji. Profil NC_Arungard zawiera cztery jawne loadouty. Flaga campaign.start.initialized.v1 identyfikuje nowy przepływ; wyposażenie i złoto trafiają do standardowego save. Nie ma przydzielania zasobów podczas wczytania ani powrotu do miasta. Arungard wiąże istniejące postacie; nie dopisuje brakujących bohaterów do wcześniejszego save'a. Stabilne CharacterId nie są zmieniane.

Niebędący modalem komunikat kieruje do strażnika. Wykrzyknik oznacza osiągalną ofertę rozpoczęcia nieprzyjętego zadania w dialogu; cykle w dialogach są obsługiwane bez zapętlenia. Po przyjęciu zadania wykrzyknik i wprowadzenie znikają. Rejestracja zadania i jego przyjęcie wykorzystują istniejące systemy.

Usługi są dostępne od początku; cztery zakładki galdurytów pozostają widoczne, ale nieaktywne przed L11 dowolnej zapisanej postaci, również rezerwowej. Powód jest w tooltipie. Taką samą regułę sprawdzają crafting, identyfikacja, osadzanie i wyjmowanie oraz zakup brakujących materiałów w craftingu galdurytu.

## Authoring i weryfikacja

Editor: Godmist > Content > Configure VS-05 campaign. Polecenie jawnie tworzy/aktualizuje 24 części startowe, 8 definicji wyposażenia i profil kampanii, dopisuje referencje do katalogu, konfiguruje Bootstrap/Arungard, nazwy i klasy postaci oraz marker/komunikat. Nie wykonuje się w runtime. Części startowe są wyłączone z losowej generacji i craftingu. Normalny sprzęt zachowuje poprzednie reguły.

104/104 EditMode PASS (vs05-verified.xml): pakiet i dokładne statystyki importu, osobne kampanie, save round-trip po wydaniu złota, nieważny profil, odblokowanie usług na rezerwowej postaci L11, blokady akcji, sprzedaż/odkup za 0 bez duplikowania, brak ulepszania/reforge oraz regresja handlu i save/load.

Play Mode PASS: menu → Arungard, cztery aktywne widoki/postacie, 500 złota, brak dodatkowych przedmiotów, sprzęt L1 oraz marker i komunikat, które znikają po przyjęciu zadania. Konfiguracja przeniesiona z kopii testowej do projektu; nowe referencje assetów zweryfikowane. Kopia wcześniejszych scen/katalogu/postaci: .codex/vs05-before. Self-review: brak znanych krytycznych problemów w zakresie karty; brak commit/push.

Odbiór: użytkownik zaakceptował smoke poleceniem `git`. Izolowany zestaw źródeł przygotowany do commita (bez VS-18): 92/92 EditMode PASS.
