# CP2-04 — kontrakt progresji do L40

Status: **READY_FOR_SMOKE — kontrakt i regresje**, 2026-10-31. Użytkownik zatwierdził trzy decyzje poniżej. Zachowujemy obecną bazę liczbową; strojenie tempa pozostaje w CP2-53.

## Źródła i zakres

- `../design/content_pass_2.xlsx`, SHA256 `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`. `Zakres!C3:E3` określa rozszerzenie drzewek, `C4` przedmioty L21–40, `C6:D6` integrację progresji i płatny respec. Arkusz nie podaje nowej formuły XP/SP ani progów odblokowania nowych biomów. `Nowe przedmioty!L1` oznacza poziom wynikający z progresji, a nie uniwersalną blokadę craftingu.
- `Assets/_Project/Resources/GameDefines.asset`: aktualne, serializowane wartości konfiguracji; mają pierwszeństwo przed historycznym przykładem `12 × L^1,5 + 12` w PROJECT_CONTEXT. Aktualna formuła ma współczynnik i stałą **1200**.
- `CharacterProgressionSystem`, `CharacterLevelGrowthSystem`, `SkillTreeProgressionSystem`, `DungeonOfferSystem`, usługi przedmiotów i save/restore. Katalog i audyt CP2-02/03 pozostają źródłem ID i właścicieli mechanik.
- Wynik obliczeń: `Tools/ContentPass2/progression-plan.json`; pełna tabela L1–40 z separatorem `|`: `progression-levels.txt`. Generator `build_progression_plan.py` tylko czyta XLSX/hash i Unity assets; nie edytuje ich.

## Zatwierdzone decyzje

1. **Limit:** content i jego odbiór do **L40**, obecny limit postaci **L50** zachowany. Nie obniżamy poziomu istniejących postaci ani nie zerujemy ich XP/SP na40.
2. **Baza progresji:** zachowujemy aktualne XP, SP, progi tierów i przyrosty statystyk; późniejsze strojenie tempa w CP2-53.
3. **Dostęp do biomów:** wszystkie pięć biomów w puli **od początku**, skalowane jak obecne Katakumby/Las. Authoring nowych definicji pozostaje w31/36/41; nie dodajemy progów odblokowania postaci ani questów.

Decyzje zaakceptowano razem w odpowiedzi użytkownika na pytania CP2-04. Granica opracowania contentu40 nie wprowadza dodatkowego runtime capu wypraw. Obecne oferty i aktywne lochy do50 pozostają zgodne ze starymi zapisami; content41–50 nie otrzymuje deklaracji kompletności tego passu.

## Kontrakt

### Poziomy są odrębnymi wielkościami

| Wielkość | Obecne znaczenie / proponowana kontynuacja |
|---|---|
| Poziom postaci | Własne XP, automatyczny wzrost i SP; limit konfigurowany centralnie. Rezerwa nie zdobywa XP walki. |
| Poziom drużyny do ofert | Średnia aktywnych postaci, zaokrąglenie midpoint away-from-zero. Nie maksymalny poziom. |
| Najwyższy poziom kampanii | Najwyższy poziom w CharactersById, także poza aktywną drużyną; używany m.in. do milestone'ów. |
| Poziom wyprawy | Wynik oferty: średnia drużyny plus offset, albo FixedLevel dla bossa. Nie jest wymaganym poziomem każdej postaci. |
| Poziom wroga/bossa | Poziom instancji i skalowania/nagrody. Dostęp bossów zależy od rzeczywistych FixedOfferLevels i szansy pojawienia się oferty; zakres dropu nie tworzy automatycznie oferty na każdym poziomie. |
| Poziom przedmiotu | Parametr wyniku/siły; RequiredLevel może być odrębny i jest sprawdzany przy wyposażaniu. Poziom surowca nie oznacza poziomu wymaganej postaci. |
| Poziom źródła | Zakres wypraw dających przedmiot. Np. nowe kamienie bossów mają poziom materiału11, ale zatwierdzone źródła bossowe L1–40. |
| Tier drzewa | Dostęp po wydanych rangach w tym konkretnym drzewku i spełnieniu prerequisite, nie po poziomie postaci. |
| Lojalność NPC | Osobna progresja wydanego złota i usług; nie przyrasta automatycznie z levelem postaci. |

### XP i SP

`XP(L → L+1) = round(1200 × L^1,5 + 1200)`; C# Math.Round, midpoint-to-even. XP przechowywane jest jako postęp w bieżącym poziomie; awans odejmuje próg, możliwy wielokrotny awans. Przy rzeczywistym maksymalnym poziomie AddExperience przestaje naliczać, a wejście na cap zeruje pozostałe XP. Przy zachowanym L50 poziom40 zachowuje zwykły próg do41 — zakończenie contentu nie zeruje XP.

Na L1 zdobyte SP=0; awans naL2 daje2SP, każdy kolejny1SP. Stąd łącznie `SP(L)=L` dla L≥2. To suma punktów zdobytych przez zwykłe awanse, nie bieżące AvailableSkillPoints po inwestycjach, migracji lub poleceniach konsoli. Factory tworząca instancję od razu na wysokim poziomie nie jest symulacją zdobytych awansów: startuje z0 wolnych SP. Przykładowe buildy i testy muszą nadawać XP albo używać właściwej ścieżki progresji.

| Poziom | XP do następnego przy cap50 | Łączne XP od L1 | Łącznie zdobyte SP | k awansu na ten poziom | Suma jednostek wzrostu |
|---|---:|---:|---:|---:|---:|
| 1 | 2400 | 0 | 0 | — | 0 |
| 20 | 108531 | 828423 | 20 | 2 | 29 |
| **21** | 116681 | 936954 | 21 | 3 | 32 |
| **25** | 151200 | 1454518 | 25 | 3 | 44 |
| **30** | 198380 | 2303160 | 30 | 3 | 59 |
| **31** | 208321 | 2501540 | 31 | 5 | 64 |
| **35** | 249675 | 3396072 | 35 | 5 | 84 |
| **40** | 304779 | 4753185 | 40 | 5 | 109 |

Tabela wszystkich40 poziomów jest w pliku `progression-levels.txt`; nie zakłada liczby wypraw na awans. CombatRewardSystem daje zwykłym przeciwnikom mnożnik2 po dotychczasowej karze poziomu, bossom1. Nie mnożyć ponownie formuły progów ani questów. Zachować jednorazowe przyznawanie nagrody.

### Statystyki

Aktualne współczynniki awansu: L2–10 k1, L11–20 k2, L21–30 k3, L31–40 k5; dalszy istniejący zakres41–50 k9. Zachowujemy je. Istniejące krzywe HP/obrony i early/late growth wrogów nadal mają własne reguły; nie zastępować ich prostym mnożnikiem bohatera.

Poniżej surowe wartości bazowe z definicji, bez wyposażenia, pasywek, finalnego zaokrąglania i CharacterStatPipeline. To punkty porównania dla CP2-52/53, nie statystyki gotowego buildu:

| Klasa / obecna postać | HP L21 / L40 | Atak min–max L21 | Atak min–max L40 | Obrona fiz./mag. L40 |
|---|---:|---:|---:|---:|
| Wojownik / Wladimir | 775 / 1737,5 | 46,4–62,4 | 100,3–135,55 | 61,05 / 40,7 |
| Zwiadowca / Jerzy | 620 / 1390 | 43,6–71,2 | 105,2–155,9 | 43,97 / 25,8 |
| Mag / Alison | 490 / 1067,5 | 59–77,6 | 136–177,7 | 33,25 / 66,5 |
| Paladyn / Reimond | 1090 / 2630 | 40,2–55,6 | 86,4–117,2 | 81,4 / 81,4 |

### Tiery i koszt buildów

Obecny kontrakt: T1/T2 od0 wydanychSP, T3/T4 od10, T5/T6 od20, T7 od30. Zakup jednej rangi zużywa1SP; limit jednego drzewa50. Nowe tiery contentu dodają wybór, nie dodatkowe SP ani obowiązek inwestowania w każdą specjalizację. T6/T7 Zbrojnego pozostają decyzją zakresu CP2-06.

| Cel | Minimalny budżet wynikający z bramki | Najwcześniejszy poziom zwykłej progresji |
|---|---:|---:|
| Pierwsza ranga T3/T4 jednego drzewa | 10 wydanych +1 zakup =11 | 11 |
| Pierwsza ranga T5 jednego drzewa | 20+1=21 | 21 |
| Dwie rangi T5 tego samego drzewa | 20+2=22 | 22 |
| Pierwsza ranga T5 dwóch różnych drzew | 21+21=42 | 42; poza contentemL40 |
| Pierwsza ranga T7, jeśli dopuszczony | 30+1=31 | 31 |

Są to dolne granice z progów. Konkretny node wymaga także prerequisite i dostępnej rangi. Generator znalazł legalne20-rangowe sekwencje istniejących T1–T4 wszystkich **9** aktualnych drzew (w tym istniejącego Strażnika); to nie dowód gotowości nowych T5. Zapis `8 drzewek` w Zakres!C3 nie opisuje pełnej liczby dzisiejszych assetów. Zakres zmian wynika z kart05–14, a nie z usunięcia dziewiątego drzewa.

Przykładowe kierunki rozwoju i jawne budżety. W `example_build_paths` pliku `progression-plan.json` zapisano po40 kolejnych inwestycji dla każdej klasy:20 legalnych obecnych rang, jedna opisana nowa rangaT5 bez prerequisite,9 rang drugiego drzewa,9 dodatkowych istniejących rang głównego i1 drugiego. Nowy T5 nadal wymaga implementacji swojej karty; jest to dowód osiągalności planu, nie obecności skilla w runtime.

| Klasa | Przykładowy kierunek | ZakupT5 naL21 | Budżet L21 | L30 | L40 |
|---|---|---|---|---|---|
| Wojownik | Dowódca + Awanturnik | Pojedynek, T5:3 | 21/0 | 21/9 | 30/10 |
| Zwiadowca | Skrytobójca + Tancerz Ostrzy | Przewrót w Przód, T5:2 | 21/0 | 21/9 | 30/10 |
| Mag | Piromanta + Czarnoksiężnik | Słup Ognia, T5:5 | 21/0 | 21/9 | 30/10 |
| Paladyn | Kapłan + Krzyżowiec | Poświęcona Broń, T5:2 | 21/0 | 21/9 | 30/10 |

Każda pokazana ścieżka ma własną kolejność nodeID i poziom pierwszego możliwego zakupu; nie jest rekomendacją końcowego balansu. Inne T5 mogą wymagać konkretnych T4 lub wcześniejszegoT5. Dla Krzyżowca, Strażnika i nowych trzecich drzew nie udawać zamknięcia brakującego designu. Alternatywny rozdział20/10/10 naL40 daje dostęp do bramkiT5 głównego drzewa, ale **bez wolnego punktu na zakup**. Nie pokazywać go jako buildu mającego T5.

### Plan źródeł i odblokowań

| Próg | Nowy content / powiązanie | Owner i warunek |
|---|---|---|
| 1–10 | Podstawowe źródła pięciu biomów, kamienie bossów wg zatwierdzonych zakresów | 31/36/41/46; dostęp biomów zależy od decyzji3, bossowie od prawdziwych ofert, nie wpisu w tabeli dropu |
| 11 | Obecne usługi galdurytów; bazowe kamienie o poziomie11 | GalduryteServiceAccess: dowolna postać w CharactersById naL11; nie nowa blokada dropu |
| 21 | Nowa warstwa materiałów, galdurytL21 i alchemiaB/C zgodnie z konkretnymi konfiguracjami; najwcześniejszy zakupT5 | 16/19/21 i05–14; nie utożsamiać całej alchemii z jedną formułą tier→level |
| 25 | Części/wyposażenieL25 | 17; dostęp surowców i usług oddzielnie od RequiredLevel wyniku |
| 30 | BiżuteriaL30; milestone bossowy poziomu30 | 18 oraz31/36/41; konkretne oferty muszą mieć FixedOfferLevels |
| 31 | Materiały z kolejnej warstwy i dodatkowe zastępcze dropy bossów | 16/46, zachować zaakceptowane zastępcze źródła czterech materiałów; nowe k5 już istnieje |
| 35 | Części/wyposażenieL35 | 17; analogicznie do25 |
| 40 | BiżuteriaL40, pełne pokrycie źródeł i milestone bossowy40 | 18/31/36/41/46; dalsze XP zależne od decyzji1 |

Nie dodaję nowych level-locków usług ani receptur na podstawie samego ItemLevel. Obecne BlacksmithCraftingService i lista części w BlacksmithCraftingWindowView nie stosują ogólnego filtra „ukryj wyższy tier przed kolejną dziesiątką poziomów”, opisanego w historycznym PROJECT_CONTEXT. Utrzymanie zachowania runtime jest propozycją bazową: materiały i uprawnienia usług warunkują crafting, RequiredLevel warunkuje używanie wyposażenia. Dodanie nowej blokady wymaga osobnej jawnej decyzji, nie „naprawy” w tle.

AlchemyRuntimeRules dla ogólnego mapowania ma11/21/41, lecz pass zawiera konkretne konfiguracjeB/C. CP2-19 musi sprawdzić wszystkie ścieżki tworzenia/uzupełniania i wymagania medium, żeby wynik planowany do40 nie otrzymał automatycznie41. Katalog pozostaje źródłem poziomu danej konfiguracji. Płatny respec należy do15; koszt zależny od zwracanychSP nie jest jeszcze formułą zatwierdzoną w04.

## Realizacja i granice karty

1. Zatwierdzone decyzje zapisano w tym kontrakcie i generatorze danych planu. Tabela zachowuje istniejące liczby.
2. Wspólny limit postaci pozostaje w GameDefines/CharacterProgressionSystem. DungeonOfferSystem zachowuje clamp50 i offsety do±3: przy drużynieL40 nadal możliwa jest ofertaL41–43. To zachowane zachowanie istniejącej gry, nie obietnica nowego contentu41–50. Nie zmieniono DTO, oferty ani aktywnego lochu ze starego save i nie dodano niezamówionej blokady wypraw na40.
3. Nie było realnej blokadyL20 ani brakującej obsługi wyższych progów do usunięcia. Rozszerzono testy granic i kompatybilności, bez pozornej zmiany systemów już spełniających kontrakt. Authoring nowych drzew, receptur, bossów i usług pozostaje we właściwych kartach.
4. Ustalić poziomy FixedOfferLevels i zakres nowej puli w31/36/41 zgodnie z kontraktem04. Nie uznawać zakresuL1–40 za40 nowych fixed boss offers.
5. Zachować XP/SP/inwestycje i stabilneID. Istniejąca LegacySaveMigrations.RecoverSkillPointTotal dodaje1, gdy suma wolnych+wydanych punktów wynosiL−1. Zachowanie jest idempotentne po wyrównaniu doL; zmiana liczbySP wymaga ponownej oceny tej reguły.

## Kryteria integracji kolejnych kart

- XP:19→20→21,29→30→31,39→40, dokładny próg, próg−1/+1, multi-level i overflow; reguła XP na40 oraz rzeczywistym capie.
- SP:2 naL2, potem1; suma40 naL40 przy normalnej progresji, poprawne available po zakupach; dwa kolejne save/load bez przyrostuSP, test legacy wyrównania i zachowania istniejących inwestycji.
- Tier:9/10 i19/20 wydanychSP, brak wolnego punktu mimo strukturalnego odblokowania, prerequisite i limit drzewa. Nowy T5 dostaje test konkretnej ścieżki w karcie autora.
- Wzrost:wartości graniczne20/21/30/31/40 zgodne z GrowthData i pipeline; load nie przyznaje ponownie przyrostu HP ani punktów.
- Oferty:średnia aktywnych a najwyższy poziom kampanii, offset±3, wszystkie zatwierdzone biomy, milestone30/40 i kolejka bossa; zachować istniejący runtime zakres1–50 oraz tożsamość/seed/level wczytanego aktywnego lochu. Weryfikacja nowego contentu w tym passie obejmuje1–40.
- Integracja:stara postać, aktywny loch, nagroda walki i questa przyznana raz; ścieżki craftingu/medium i używania nowych przedmiotów w odpowiednich kartach.

## Weryfikacja obecnego etapu

Sprawdzono niezmieniony hash arkusza, odczyt aktualnego GameDefines, monotoniczny roll-forward40 poziomów, cztery profile wzrostu bohaterów, legalną sekwencję20 istniejących rang we wszystkich9 drzewach i cztery40-punktowe ścieżki przykładowych buildów. Wszystkie kontrole przeszły.

Unity: **90 różnych testów PASS po ukierunkowanym reteście**. Zachowany raport siedmiu fixture'ów ma89PASS/1FAIL; izolowany retest widoku drzewka ma1PASS/0FAIL. Failure dotyczył Destroy wEditMode w teście przechodzącym doPlayMode; w czasie pracy lokalnego runnera następował reload skryptów. Interferencja harnessu jest prawdopodobną przyczyną, nie potwierdzoną diagnozą zmiany gameplayu. Retest wykonano bez zmian kodu produkcyjnego i bez edytowania skryptów podczas testu. Wynik końcowy, oba XML, kontrola plików i ograniczenia: `../LocalHandoffs/cp2-04-progression-20261031/verification.json`.

Nowe regresje sprawdzają granice19/20/29/30/39/40, dokładny próg−1 i overflow7XP po awansie, poprawną sumęSP, wyrównanie legacySP raz naL21/31/40/50 oraz gotowość systemu ofert do pięciu syntetycznych definicji naL1. Wzrost sprawdzono na20/21/30/31/40. Istniejące testy obejmują dwa pełne cykle save/load starej postaciL50, cap50, bramki drzew, stan aktywnego lochu i konfigurację. Pełnych cykli save/load osobno dla każdego poziomuL21/L31/L40 nie uruchamiano.

Test ofert używa definicji tworzonych w pamięci: nie dodaje trzech prawdziwych biomów. Nie zmieniono kodu produkcyjnego, konfiguracji XP/SP, XLSX, schematu save ani referencji assetów. Testy wykonano w istniejącym Editorze projektu docelowego; nie sterowano Editorem worktreev0.1.x. Tymczasowe runnery usunięto przez AssetDatabase. Sześć wcześniejszych wartości materiałów, wyzerowanych podczas ponownego otwarcia Bootstrap przez framework testowy, odtworzono przez Editor API i porównano z fingerprintem sprzed testów. Manual gameplay smoke i build Windows NOT RUN. Nie ma nowego wiring do ustawienia w Editorze; odbiór to przegląd kontraktu i wyników regresji. Bez commit/push.
