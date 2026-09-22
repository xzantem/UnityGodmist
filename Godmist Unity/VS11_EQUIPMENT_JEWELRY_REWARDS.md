# VS-11 — wyposażenie, biżuteria i nagrody specjalne

Status: DONE, 2026-09-23. Smoke zaakceptowany przez użytkownika; VS-11 zatwierdzona do commita i pusha.

## Zakres
Źródła: VS01_CONTENT_CATALOG.md, aktualny design/content_pass_1.xlsx (Biżuteria i Galduryty), design/Wyposażenie.xlsx (Arcydzieła). 32 kombinacje metalu/slotu na L10/L20, 11 klejnotów, 53 efekty galdurytów T1 (L11), cztery unikaty Ducha Umarłego Króla w wariantach L10/L20. Bez publikowania odłożonego pakietu VS-18.

## Decyzje użytkownika
- Kostur Umarłych: losowy zwykły szkielet Katakumb na poziomie maga, HP według wariantu unikatu. Do końca walki, limit 1 na maga, nowe zastępuje stare; sojusznicze AI.
- Galduryt: jeden rzut na użycie skilla; udany DoT/CC obejmuje wszystkie trafione cele. Bez uruchamiania przez DoT. Leczenie od sumy bezpośrednich obrażeń użycia.
- Spowolnienie: -20% szybkości, 3 tury. Pozostałe statusy: domyślnie 3 tury, DoT 60%.
- Aktualny arkusz ma kryt galdurytu .10/.13/.16/.19/.25 oraz penetrację .05/.06/.07/.08/.10; stare liczby katalogu nie nadpisują aktualnego źródła.

## Integracja
Rozszerzono pulę do 53 wierszy przy zachowaniu numerów istniejących enumów/save. JewelryGemDefinition oddziela surowiec od modyfikatora. Proce galdurytów agregują trafienia jednego użycia skilla; regeneracja ułamkowa zachowuje resztę w zapisie. Bonus dropu jest utrwalony w wyprawie. Unikaty wybierają pasywkę według poziomu przedmiotu; kostur ma dispatcher, poziom przywołania, limit i sojusznicze AI.

Opisy przedmiotów w wersjonowanym katalogu zostały zredagowane w języku gracza: 194 materiały, 216 części wyposażenia i 25 pozostałych przedmiotów. Cztery definicje należące do otwartej VS-18 pozostają w drzewie roboczym poza tym commitem. Tooltip nie używa nazwy torby, receptury ani odwołania do arkusza jako opisu rzeczy. Zidentyfikowane galduryty pokazują rzeczywiste działanie efektu oraz regułę wyzwalania, a podgląd rzemieślnika używa tych samych nazw i jednostek. Zmiany danych zapisano przez Unity Editor API; nie zmieniają receptur, źródeł łupu ani statystyk.


## Dalsze decyzje
- Bonus dropu: średnia aktywnej drużyny przy wyruszeniu; utrwalony w stanie wyprawy i używany przy losowaniu nagród.
- Katakumby: teren zamknięty i zimny; cechy konfigurujemy w definicji lochu.
- Kostur: mechanika i testy teraz, produkcyjna pula czterech szkieletów dopiero w VS-15 (zaakceptowane). Nie zastępować jej skeleton.test w produkcji.
