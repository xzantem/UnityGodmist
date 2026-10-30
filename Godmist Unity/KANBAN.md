## v0.1.1 — korekta grafiki jadeitu według workflow C — 2026-10-27 — READY_FOR_SMOKE

Zastąpiono wcześniejszy ręczny rysunek jadeitu nowym masterem z wbudowanego image_gen, zgodnym z przyjętym stylem C. Eksport oryginalnym skryptem Aseprite: premultiplied area average, 32×32, alfa 0/255, do 24 kolorów, bez ditheringu. 32 warianty biżuterii złożono oryginalnym pipeline'em warstw/metali i kanonicznych masek gniazd; 8-kolorowa rampa pochodzi z nowej ikony. Zachowano również produkcyjne przesunięcia centrowania. Usunięto poprzedni recolor HSV ze skryptu BossGemLootSetup, który teraz korzysta z gotowych PNG.

QA: 33/33 plików PASS; poza gniazdami piksele metali oraz obrysy identyczne z istniejącymi wariantami produkcyjnymi. Kontrola zakresu wykazała dokładnie 33 zmienione PNG jadeitu, zero zmian pozostałych PNG, meta, GUID, assetów, scen i prefabów. Unity: 11 testów PASS, zero failure; 4 jawne narzędzia pomocnicze pominięte zgodnie z ich oznaczeniem. Końcowa plansza eksportowana z Unity obejrzana. Metoda, pełny prompt, źródło generacji, PNG, dwuwarstwowe Aseprite i dowody: ../output/jadeite-style-c/. Bez zmian mechanik/balansu/dropu, manualnego wiring, commit/push. Smoke: wygląd jadeitu w ekwipunku i czterech typach biżuterii w craftingu.

## v0.1.1 — obsydian i jadeit z bossów — 2026-10-27 — READY_FOR_SMOKE

Zatwierdzone pule: każdy boss Katakumb ma osobne 50% na 1–2 obsydianu, każdy boss Lasu osobne 50% na 1–2 jadeitu. Dodano osiem pul w czterech torbach, w obu przedziałach 1–10 i 11–20 (wyprawy bossów poziomu 10/20). Pozostałe pule bossów i unique bez zmian. Oba kamienie wykluczone z toreb zasobów. Usunięcie obsydianu kompensuje szansę wspólnej puli, zachowując bazową podaż pozostałych materiałów. Przy istniejącym addytywnym bonusie dropu +10 pp pozostałe kamienie tej puli mają względnie około +1,85% w Katakumbach / +1,37% w Lesie; wzoru dropu nie zmieniano.

Jadeit: zatwierdzony leśny odpowiednik obsydianu, rzadki, cena bazowa 2847, materiał do biżuterii, +3,5% zadawanych obrażeń w Lesie za sztukę. Cztery sztuki dają łącznie +14%; poza Lasem brak premii. Katalog i cztery definicje biżuterii rozszerzone o jadeit, również dla istniejącego losowania biżuterii. Nowa ikona kamienia i 32 warianty biżuterii; stare ikony/referencje zachowane. Średnia nowej puli bez bonusu dropu: 0,75 sztuki / 2135,25 wartości bazowej na próbę wygenerowania łupu. Aktualizacja skryptów authoringu zabezpiecza zmianę przed ponownym setupem contentu.

Weryfikacja: 130/130 EditMode PASS oraz końcowe 2/2 testy symetrii czterech jadeitów/obsydianów w odpowiednim biomie PASS; 1/1 jawny test zastosowania przez Editor API i powtórzenia bez różnic PASS. Zestawy obejmują rzeczywiste losowania, crafting/generację biżuterii, ikony, zasoby oraz fabryki save/restore. Kontrola 17422 istniejących plików: dokładnie 12 zmienionych assetów w zakresie karty, osiem nowych pul, zero zmian istniejących Runtime/Systems, scen, prefabów i starych ikon/meta względem początku karty. Self-review PASS. Raport tabel łupu: 6 toreb / 55 pul / 622 wiersze, QA PASS. Dowody: ../LocalHandoffs/boss-gems-20261027/ i ../LocalHandoffs/loot-tables-20261027/.

Znany niezwiązany failure: `ApprovedBossUniqueTests.OnlyFirstPhysicalHitIsReducedAndOnlyOwnersTurnRearmsIt`, odtworzony osobno. Test ponownie rozpoczyna niezakończoną turę; istniejący guard `HasUnfinishedCombatTurn` nie wykonuje ponownego resetu ochrony tuniki. Test, efekt i kod walki niezmienione przez tę kartę; nie rozszerzano zakresu o tę usterkę testu.

Bez ręcznego Editor wiring i zmian schematu zapisów; istniejące ID i zapisane/prerollowane nagrody zachowane. Smoke NOT RUN: rozpocząć NOWE wyprawy bossów poziomu 10 i 20 w obu biomach, sprawdzić pule i ikony; torby zasobów bez obu kamieni; wytworzyć cztery typy biżuterii z jadeitem, sprawdzić +3,5% za sztukę wyłącznie w Lesie oraz zapis/wczytanie starego obsydianu i nowego jadeitu. Stare wyprawy/torby nie przelosowują nagród. Bez commit/push, zmiany wersji i integracji do v0.2.0.

## v0.1.1 — wspólne nazewnictwo UI — 2026-10-27 — READY_FOR_SMOKE

Wspólny słownik `Scripts/Core/Utilities/UiTerms.cs`: wszystkie 15 statystyk, 9 modyfikatorów, 7 odporności i 50 efektów galdurytów, zasoby, statusy oraz wspólne etykiety. Podłączono tooltipy workspace i starszy tooltip walki, kartę postaci, inspect, drzewka, kowala, przedmioty/usługi, HUD PA, log walki i prezentację komunikatów konsoli. Glosariusz zachowuje istniejące ID i stare aliasy, a tytuły korzystają ze słownika. Historyczne opisy są normalizowane podczas wyświetlania; assety i stare zapisane etykiety pozostają nietknięte. Ustalono `pp`, „Penetracja fizyczna/magiczna” oraz jawny kontekst many/furii/impetu dla postaci, skilli i statusów o znanym odbiorcy. Mikstury/galduryty pozostają ogólne; nieznany zasób celu nie jest zastępowany zasobem rzucającego. Reguła zapisana w CONVENTIONS.md.

Weryfikacja: 366 różnych testów EditMode PASS, bez skipów w zestawach pozytywnych; 31 nowych przypadków terminologii, kontekst odbiorcy w walce i eksploracji, operacje %/pp, kultura liczb, glosariusz, tooltipy przedmiotów, lifecycle UI, drzewka i istniejąca prezentacja efektów. Self-review PASS. Porównano 6692 istniejące pliki: brak zmian istniejących assetów/prefabów/scen/meta, brak zmian Systems; model Runtime galdurytu poza klasą tekstową identyczny. Bez zmian mechanik, balansu, identyfikatorów i formatu save. Scoped diff check PASS. Pełny diff zawiera wcześniejszy whitespace wygenerowany przez Unity w ST_Scout_Assassin z poprzedniej karty — nie edytowano YAML.

Znany niezwiązany failure: `ItemTooltipPresentationTests.LootGoldCollectsOnlyWithLeftClickAndCannotBeClaimedTwice`, odtworzony osobno. Pobranie złota przechodzi; asercja oczekuje `slot.activeSelf=false`, gdy niezmieniony kod zamyka całe puste okno. Kod slotu/okna oraz prefab identyczne z HEAD. Nie rozszerzano tej karty o naprawę łupu. Dowody: ../LocalHandoffs/ui-naming-standard-20261027/verification.json i zestawy XML.

Bez konfiguracji serialized state w Editorze. Smoke w worktree v0.1.1: porównać kartę/inspect, koszt i premie na drzewkach czterech klas, buff na postaci o innym zasobie niż rzucający, eksploracyjny buff, miksturę i galduryt oraz kowala/ulepszanie/przekuwanie. Sprawdzić identyczne nazwy, `pp` vs `%`, zasób klasowy vs ogólny i linki glosariusza. Manualny smoke NOT RUN. Długich opisów nie skracano. Zależność integracyjna: wcześniejsza karta prezentacji efektów bohaterów (również jeszcze bez commita). Bez commit/push i integracji do v0.2.0.

## v0.1.1 — prezentacja efektów skilli bohaterów — 2026-10-27 — READY_FOR_SMOKE

Poprawiono prezentację nazw i powiązania istniejących ikon dla triggerowanych efektów bohaterów, również starych etykiet w zapisanych efektach. Tooltipy pokazują wartości aktywnych efektów i talenty: Słaby Punkt, Dogłębna Analiza, Złość, wbite sztylety, rozkazy/Trauma/darmowe kroki, Święta/Trwała Regeneracja, Łaska i odzysk zasobu, Odsłonięcie Słabości, Ofiara i podtrzymanie Pragnienia Śmierci. Dodano read-only prezentację Adaptacji Energetycznej per napastnik oraz liczbę nałożeń Wypalania Odporności. Ożywcza Łaska grupuje dwie składowe z niezależnymi czasami. Pole Osądu pokazuje warunek przebywania w obszarze zamiast technicznych 9999 tur. Efekty jednorazowe pokazują ładunek. Błogosławieństwo i redukcja otrzymywanych obrażeń są buffami; Odsłonięcie Słabości jest debuffem; dobrowolny mieszany toggle Pragnienie Śmierci pozostaje buffem.

Wyłącznie warstwa Views i testy; bez zmian mechanik, balansu, serialized content, schematu zapisów i bez modyfikowania odczytywanych efektów. Ikony z istniejących skilli/node'ów, bez nowej grafiki i Editor wiring. Kompilacja w Editorze v0.1.1 oraz 92/92 EditMode PASS, zero skips (45 przypadków nowej karty i 47 regresyjnych). Kontrola 1215 plików gameplay/scen/Runtime/Systems przed i po testach: zero zmian. Self-review i scoped whitespace PASS. Wcześniejsza karta opisów drzewek zachowana. Dowody: ../LocalHandoffs/hero-status-presentation-20261027/checks.xml i verification.json.

Smoke w tym worktree: Słaby Punkt z Wykorzystaniem Słabości i Analiza; rozkazy przy zmianie sąsiedztwa + wykorzystanie darmowych kroków; Regeneracja i Błogosławieństwo/Łaska od innego paladyna; Ożywcza Łaska w kolejnych turach; Ofiara z talentami; Adaptacja od dwóch wrogów, Wypalanie Odporności i spowolnienie Pola Osądu. Sprawdzić ikony/nazwy, wartości, czas i liczniki w pasku efektów i inspect/tooltipach. Manualny gameplay smoke NOT RUN. Bez commit/push i integracji do v0.2.0.

## v0.1.1 — aktualne nazwy w opisach drzewek — 2026-10-27 — READY_FOR_SMOKE

Poprawiono 7 nieaktualnych odwołań w opisach: Pewna Ręka, Lekki Krok, Krwawa Seria, Zasięg Pożaru, Podsycanie Żaru, Zbiorowe Przejęcie Mocy i Łaska Zwrotna. Pięć assetów zapisano przez Unity Editor API; uzupełniono dwa opisy w źródłowych skryptach konfigurujących (pozostałe były już poprawne). Bez zmian nazw node'ów, ID, wartości, wymagań, efektów, referencji ani formatu zapisów.

Weryfikacja: 7/7 opisów PASS, porównanie pełnych danych przed/po wykazało dokładnie 7 zmian Description i zero innych zmian pól; ponowne zastosowanie bez zmian plików; odczyt po ponownym uruchomieniu Unity PASS. Final self-review PASS. Pierwszy import nowego worktree zgłosił przejściowe błędy pakietów; końcowe uruchomienie weryfikacyjne zakończone kodem 0. Unity dodaje whitespace do pustych pól ST_Scout_Assassin; nie poprawiano ręcznie YAML. Bez manualnego wiring; smoke: obejrzeć siedem tooltipów na drzewkach Zabójcy, Tancerza Ostrzy, Ognia, Czarnoksiężnika i Kapłana w projekcie v0.1.1. Manualny smoke NOT RUN. Dowody: ../LocalHandoffs/skill-tree-description-names-20261027/. Bez commit/push i integracji do v0.2.0.

## Finalny build Early Access 0.1.0 — 2026-10-27 — READY_FOR_SMOKE

Ustawiono0.1.0 przez Editor API i zapisano etykietę0.1.0 — Early Access w Bootstrap. Windows x64 BuildOptions.None Succeeded/0errors/1247warnings. Nowa paczka75,1MB/206plików, ZIP integrity i SHA256 PASS. 9 ikon EXE zgodnych piksel w piksel z PNG. Start EXE z rozpakowanego ZIP odpowiada, brak wykrytych wyjątków; własny proces zamknięty. Dwa testy wersji/menu PASS, natywny render obejrzany. Dane finalnego buildu:136skilli/23505pól,0różnic względem zaakceptowanego audytu edytora; katalog i13plików binarnych ZIP zgodne. Znane1246ostrzeżenia rekurencyjnej serializacji oraz2przy starcie pozostają, bez wykrytej utraty obecnych danych. 724pliki zapisów SHA256 bez zmian. Save/restart/load oraz ruch wcześniej potwierdzone przez użytkownika na RC1, nie powtarzano gameplay smoke przy zmianie wersji/ikony.

Serialized diff względem początku karty: bundleVersion; build-generated preloaded input settings; tekst etykiety i normalizacja TMP color/style cache. Brak ręcznych zmian YAML i migracji zapisów. Self-review i scoped whitespace PASS. Bez manualnego Editor wiring. Poprzednie paczki zachowane. Nie wykonano commit/push/tag/GitHub Release.

Paczka: ../Builds/0.1.0/Godmist-0.1.0-Windows-x64.zip. Dowody: ../LocalHandoffs/ea-010-20261027/report.html, build-result.json, package-result.json, checks-version-menu.xml, comparison.json, exe-icon-verification.json i portable-startup.log.

## Ikona aplikacji Ghorn — 2026-10-27 — READY_FOR_SMOKE

Podpięto Ghorn w PlayerSettings default i Standalone przez Editor API. Baza rysunku64×64; 128/256/512/1024 wyłącznie nearest64. Mniejsze16/24/32/48 z pipeline BOX + paleta32 bez ditheringu; 96=32×3,192=64×3. Postprocessor Windows zachowuje przygotowane ICO zamiast interpolowanych slotów24/96/192 Unity. 9/9 rozmiarów odczytanych z EXE zgodnych piksel w piksel z PNG. Odczyt referencji i kontrola importów Editor PASS. Build0.1.0-rc.1 Succeeded,0errors,1247warnings (historyczna rekurencyjna serializacja pozostaje). EXE odpowiada, brak wykrytych wyjątków; zatrzymano wyłącznie własny proces testowy. ZIP sprawdzony,206plików; poprzednia paczka RC1 zachowana. Użytkownik wcześniej potwierdził zapis/restart/wczytanie; nie powtarzano ruchu ani save smoke przy zmianie graficznej. Brak manualnego wiring, zmian gameplay/save, commit/push/tag/publikacji.

Nowa paczka: ../Builds/0.1.0-rc.1/Godmist-0.1.0-rc.1-Windows-x64-icon.zip. Dowody: ../LocalHandoffs/application-icon-20261027/configuration.json, build-result.json, exe-icon-verification.json, package-result.json i player-startup.log. Wyjątek whitespace dla pustego m_BuildTarget zapisanego przez Unity; bez ręcznych zmian YAML.

## Audyt serializacji RC1 — 2026-10-26 — READY_FOR_SMOKE

Audyt oryginalnego RC1: 136 skilli, 23 505 pól, 150 referencji i katalog 136 ID zgodne z edytorem; 0 różnic. 209 efektów wraz z reakcjami, maksymalna rzeczywista głębokość zagnieżdżenia 1. Cztery nieobecne assety to wyłącznie skill.test.*. 4/4 testy PASS. 13 plików binarnych zgodnych z ZIP; SHA-256 paczki bez zmian.

Nie znaleziono uciętych danych skilli w obecnym RC1. Ostrzeżenia pozostają: rekurencyjna deklaracja SkillEffectData.counterattackEffects przekracza formalny limit Unity nawet dla pustych list. Nie zmieniano schematu, assetów, zapisów ani paczki i nie wyciszano ostrzeżeń. Głębsze przyszłe grafy wymagają osobnej zmiany reprezentacji i migracji; audyt dotyczy obecnej zawartości.

Przed publikacją nadal do potwierdzenia: nowa gra → zapis → zamknięcie EXE → ponowne uruchomienie → wczytanie w rozpakowanej paczce. Testów ruchu nie powtarzać. Dowody: ../LocalHandoffs/serialization-audit-20261026/report.html, comparison.json i checks-final.xml. Brak manualnego setupu, commita i pusha.

## Pierwszy kandydat Early Access — 0.1.0-rc.1 — 2026-10-26 — READY_FOR_SMOKE

Windows x64 release BuildOptions.None, Succeeded/0 errors, ZIP 75,1 MB. Version label w natywnym menu; zachowane company/product i katalog zapisów. 44 testy menu/zapis/restore PASS, start EXE i EXE z ZIP odpowiadają bez wykrytych wyjątków; 723 pliki zapisów SHA-256 bez zmian. Testów poruszania nie powtarzano (potwierdzone przez użytkownika).

Publikacja nadal OPEN: 1259 ostrzeżeń buildu, 1246 rekurencyjna serializacja efektów; 2 ostrzeżenia także w standalone. Nie migrowano danych ani nie wyciszano ostrzeżeń. Ręczny nowa gra/zapis/restart/wczytanie w EXE NOT RUN. Unity wygenerowało przy buildzie zmiany cache/prefilter URP, input preloaded assets, batching i fallback TMP; sprawdzić przed commitem. Brak ręcznej edycji serialized state. Szczegóły i dowody: ../LocalHandoffs/ea-rc1-20261026/status.md oraz report.html. Paczka: ../Builds/0.1.0-rc.1/Godmist-0.1.0-rc.1-Windows-x64.zip. Bez tagu, commit/push lub GitHub Release.

## Pierwsze Early Access — Windows / GitHub — 2026-10-26 — PREPARATION

Dystrybucja: Windows x64, ZIP w GitHub Releases; bez Steam/itch w pierwszym wydaniu. Użytkownik potwierdził wykonanie testów poruszania po lochach — nie powtarzać tego smoke. Nadal osobno do sprawdzenia aktualny release build, zapisy/wczytanie poza edytorem, historyczne ostrzeżenia głębokości serializacji i packaging.

Przegląd Git: 20470 ścieżek ze zmianami/untracked (w tym grafiki i .meta), 492 już w indeksie, 462 jednocześnie z późniejszymi zmianami roboczymi. Nie zmieniono indeksu ani HEAD. Zachowano kopię indeksu i staged binary patch. Proponowany podział: 10 grup tematycznych; 33 pliki współdzielone wymagają weryfikacji zależności/hunków przed faktycznym stage. Każda ścieżka przypisana dokładnie raz, meta z assetami, rodziny partial razem; 12 ścieżek nie jest automatycznym kandydatem do stage (design, materiały pracy, artefakty, nieustalone pochodzenie). To wstępny manifest, nie wykonane commity ani potwierdzenie kompilacji każdego stanu pośredniego.

Manifest/pathspecy/raport: ../LocalHandoffs/ea-release-20261026/commit-plan.json, commit-plan.html, pathspecs/. Bez commitów, push lub publikacji. Testów gameplayu nie powtarzano.

## Korekta zakończenia pionowego muru — 2026-10-26 — READY_FOR_SMOKE

Aktualna zatwierdzona zasada: górny odcinek cap → cap → cap → lico na wolnym dolnym końcu; dolny odcinek nadal ma zamknięty cap u góry. Lico otrzymuje wyłącznie południowy terminalny segment wewnętrznej przegrody, bez zamiany początku ciągu na lico. Natywny materiał 24px lica + 8px capa, bez nowej grafiki, zmiany kolizji, bram, low-wall lub serialized assets. 82/82 celowanych testów renderowania/ścian PASS. Porównanie i checks.xml: ../LocalHandoffs/side-terminal-face-20261026; aktualny render również w raporcie loot-portraits-walls-20261026/report.html. Manualny gameplay smoke NOT RUN; bez commit/push.

## Pojedyncze dropy, aktywny portret, capy i floating text — 2026-10-26 — READY_FOR_SMOKE

Skrzynie i bezpośrednie nagrody jednostek rozwijają źródła wyposażenia do niezależnych rzutów na sztukę, bez wspólnego gate pakietu. Zachowane średnie 5 sztuk na wyprawę, wagi klas/jakości, lootbagi i istniejące zapisane łupy. Aktywny wróg używa ramki zaznaczenia z czerwonym kolorem, bez grafiki slot_invalid/X. Floating Downed jest pomijany; liczby DoT i mechanika powalenia pozostają. Pionowe pełne ściany zachowują cap na całym odcinku, a wolne wewnętrzne końce mają natywne zamknięcia. Bez zmiany geometrii logicznej, kolizji, save ani serialized assets.

Weryfikacja i native porównania przed/po: ../LocalHandoffs/loot-portraits-walls-20261026/status.md oraz report.html. Smoke: nowe skrzynie (stare zachowują zawartość), torba, aktywny wróg, DoT i oba wolne końce pionowej przegrody. Manualny gameplay/FPS NOT RUN. Bez ręcznego Editor setupu, Computer Use, commit/push.

## Animacje wsparcia i nazwa celu zapisu — 2026-10-26 — READY_FOR_SMOKE

Przygotowano przekazanie support cast feedbacku poza walką do wspólnego handlera póz/VFX na istniejącym ExplorationCharacterView (bez Borrow/snapowania/dołączania do walki). Poprawiono read-only podgląd zapisu: nazwa z BoardContract przed nazwą szablonu. Odtworzony save 31815c ma prawidłowy kontrakt „Oczyść: Las” L1/25 w dawnym slocie quest.catacombs.radiant.boss; błąd nazwy pochodził z SaveSummaryPresentation.Facts/Details, nie z celu runtime.

Import i kompilacja zakończone; 39/39 testów celowanych PASS (checks.xml w katalogu karty). Diff/self-review PASS. Manualny gameplay smoke NOT RUN. Szczegóły: LocalHandoffs/exploration-support-save-20261026/status.md. Bez zmian save/schema, plików zapisu użytkownika, serialized assets ani commit/push.

## Korekta: celowanie przy stopach, oryginalne zasięgi — 2026-10-26 — READY_FOR_SMOKE

Wycofano ciągłe obrysy z poprzedniej karty. SkillHover oraz SkillRange znów używają dotychczasowego zestawu pól, obwiedni, wypełnienia i kolorów; bez nowego okręgu, skalowania i zmiany typu zasięgu. Obszarowe AffectedArea wróciło do oryginalnej prezentacji; nie jest zamieniane na nowe narożniki przy jednostkach.

W opening preview poza walką wyłącznie znaczniki SkillTarget/ChosenSkillTargets mają dotychczasowe narożniki pod rzeczywistymi stopami. Wskazanie jednostki kursorem może użyć tego samego hitboxu przy stopach, także ponad granicą logicznego pola; wybierana jest nadal ta sama BattleUnit/pozycja logiczna. Zasady legalności, LOS, koszty, typ/promień skilla, AoE, teleport i ground placement pozostają. Resolver odrzuca niewidoczne/niewybieralne widoki i jest odłączany na cancel/commit; w walce wszystko nadal przy siatce.

31 EditMode PASS: identyczne vertexy/kolory dawnych highlightów Square/Radial/Ring przy włączonym resolverze stóp; klik przy stopach po przeciwnej stronie granicy pola; najbliższy cel i ukryte jednostki; znaczniki i reset na siatkę; regresje wejścia do walki, PA i pojedynczego DoT. Render porównawczy obejrzany. Import/kompilacja i diff check PASS. Serialized assets, żywopłot, tracker i obrażenia z poprzedniej karty bez nowych zmian. Manualny gameplay/FPS NOT RUN; bez commit/push.

Raport: LocalHandoffs/target-feet-correction-20261026/report.html; checks-final.xml; targeting-restored.png. Ten wpis zastępuje opis ciągłych obrysów z poprzedniej karty.

## Prezentacja eksploracji: Godmist UI, stałe obrażenia, swobodne markery i żywopłot — 2026-10-26 — READY_FOR_SMOKE

Śledzone zadanie przebudowane na natywnym PF_UIW_Section (ramka, tytuł, font/material), bez przechwytywania kliknięć. DamageRange ma wspólny formatter: stałe MinAttack/MaxAttack/AverageAttack pokazują N, rzeczywista zmienność nadal min–max; opis skilla i preview używają go wspólnie. Nie zmieniono obrażeń ani krytów.

W eksploracji zwykłe zasięgi Square/Radial/Ring mają ciągły obrys zakotwiczony w rzeczywistej pozycji. Wybór celów/legalność/LOS nadal z istniejącej siatki; kierunkowe AoE pozostawiają swój footprint. Markery celu/wybranych/trafionych postaci rysują się pod aktualnymi stopami, także w ruchu wewnątrz pola. Callbacki ownera eksploracji są usuwane na cancel/commit; walka nadal pokazuje znaczniki w polach. Bufory obrysu i aktualizacji markerów są wielokrotnie używane, lookup bez LINQ w LateUpdate.

Nowy atlas hedge-segments-v2.png (16 sprite'ów, 32 PPU, Point, alpha) oraz LowHedgeSegments na DVP_Forest_Prototype. Nowa grafika z built-in imagegen, a nie rozciągnięte LowShrub. H/V natywne, bez obrotu i skali; wnętrze pionowego segmentu nie powiela końcowych korzeni. Łączenia składane z właściwych ramion nowych grafik H/V według tego samego endpoint index co murki, osobne zakończenia. Test pikselowy kontroluje brak niepodłączonych ramion. Nie zmieniono kolizji, walki, niskich ścian katakumb ani dekoracyjnych LowShrub używanych poza żywopłotem.

55 unikalnych końcowych EditMode PASS; natywne rendery UI, obrysu/markerów i żywopłotu w lesie oraz L/T/+ obejrzane. Regresje wejścia do walki potwierdzają brak darmowych PA i podwójnego DoT. Serialized import przez Editor API: wspólny prefab trackera i DVP lasu; sceny zachowują dotychczasowe referencje. Brak zmiany save/schema, agentów, Computer Use, commit/push. Gameplay/FPS smoke NOT RUN.

Raport: LocalHandoffs/exploration-presentation-20261026/report.html. Wyniki: checks-final.xml. Grafika źródłowa i prompt są zachowane w tym samym folderze.

## Statystyki drugorzędne, mana, diagnostyka i UX — 2026-10-26 — READY_FOR_SMOKE

Wdrożono profile 14 zwykłych mobów względem wyposażonych bohaterów, z hybrydami (np. ghul). L1 obrońca: PDEF 80 → 40, MDEF 10 → 12; Reimond z wyposażeniem startowym ma 24/24. HP, ATK, bossowie/formy, celność i szybkość mobów bez zmian. Pełne skopiowanie przyrostów heroicznych dawało nadmierny buff na wysokich poziomach, dlatego zwykłe moby dostają część budżetu ekwipunku/biżuterii/drzewka. Pancerz obrońcy pozostaje wyjątkiem roli. Na L10–50 średni indeks fizycznych trafień do zwykłego moba względem poprzedniego wariantu: katakumby około −3% do −1%, las około +1.5% do +4%. To indeks statyczny, nie procentowa prognoza trudności wyprawy.

Walka: końcowa regeneracja many bohaterów 0/50/100% PA → 25/100/200% bazowej regeneracji, liniowo pomiędzy. Dla 15 regen daje 4/15/30 punktów. Automatyczna regeneracja w eksploracji nadal OFF; mikstury i obóz zachowane. Celny Rzut: SkillAccuracy 85 → 120, bez zmian kosztu/obrażeń. Rzut Sztyletem przy pustej puli nie pokazuje dalekich wrogów jako celów; darmowe zwarcie i odzysk własnych sztyletów zachowane, także spójność poziomu pola.

UI/świat: „Przywraca zdrowie: 15% maks. HP” i „Przywraca zasób: 20% maks. zasobu”; krótkie blokady usług zaklinacza z poziomem 11; kowal bez surowego/startowego gearu w głównej ofercie (również naprawa podczas load, bez naruszania odkupu i ekwipunku gracza). Śledzone zadanie ma wspólny prefab HUD pod minimapą, przypięty przez Editor API w Arungard/DungeonSandbox; odświeżanie zdarzeniami, nie skan co klatkę. Płomienie/elementy pochodni przygasają według wspólnego filtra zapamiętanego terenu, źródła oświetlenia pozostają aktywne.

Diagnostyka opt-in: `combattrace on|off|show|clear|export`, do 4000 wpisów w pamięci. Seed/piętro, koszty PA/zasobu, oceny planów i powody rezygnacji/przerwania, faktyczne HP/tarcze przed/po obrażeniach, źródło/cel/skill/kryt, jeżeli kontekst go zawiera. Plik tylko po export w persistentDataPath/CombatDiagnostics. Test obrońcy z zerową furią potwierdza podejście otwartą drogą i powód braku ruchu przy przegrodzie; nie jest to deklaracja naprawienia wszystkich przypadków AI bez ich zapisu.

193 unikalne końcowe EditMode PASS, 345 punktów kontroli HP/ATK, porównania statyczne L1/5/10/15/20/25/30/40/50 i kontrola pośrednich poziomów. Natywne rendery HUD oraz pochodni w obu lochach obejrzane. Starsze authoringi zachowują nowy wariant i raportują faktyczną zmianę stanu. Import/kompilacja i self-review PASS. Manualny gameplay/FPS smoke NOT RUN; bez agentów, Computer Use, commit/push.

Raport: LocalHandoffs/combat-economy-ui-20261026/report.html; wyniki: checks-final.xml; statystyki: stats-before.csv/stats-after.csv; skille/hity: theory-level-N.json. Powyżej L20 odniesienie ekwipunku korzysta z projektowych części referencyjnych 25/35/45 z arkusza, a nie wyłącznie przedmiotów już dostępnych w grze. All allies/enemies → skończony duży obszar: tylko analiza w raporcie, bez zmiany mechaniki; do ustalenia promień, LOS, poziomy oraz wyjątki bossów.

## Mana poza walką, wyjścia i Umarły Król — 2026-10-26 — READY_FOR_SMOKE

Poza walką automatyczna regeneracja many jest wyłączona (bazowa oraz pasywna z wyposażenia). Leczenie za manę pozostaje dostępne; jawny odzysk z mikstur i obozu, HP z wyposażenia oraz reguły regeneracji w turach walki pozostają. Zaktualizowany opis many i testy dawnego przyrostu eksploracyjnego.

Nowe schody w górę v3 i zamknięte zejście z kratą. Wyjście lasu ma zamknięty wariant furtki; wspólny pivot, rozmiar i położenie z otwartą. Stan otwiera się po pokonaniu straży końcowego pokoju, liczony ponownie tylko po zmianie liczby ukończonych encounterów. Zablokowane wyjście pozostaje widoczne w znanym terenie, ale nie ma aktywnego hover/interakcji ani popupu o oczyszczeniu. Potwierdzenie zakończenia wyprawy zachowane. Dodatkowa rejestracja schodów w oświetleniu gruntu i cleanup przy zniszczeniu.

Widmowy Rycerz: poprawione E/W w wyborze grafik idle/ruch/akcji, bez zmiany kierunków świata. Techniczny summon.health ukryty w statusach, w zamian prezentacyjna Widmowa powłoka z ikoną/opisem i pozostałymi trafieniami. HP/modyfikator przywołania i mechanika 3 trafień/odporności na DoT niezmienione. Cień Destrukcji korzysta z opisu właściwej mechaniki, bez pustego „przyspieszenia 0%”.

189 odrębnych końcowych EditMode PASS. Natywne rendery obejrzane; kontrolowana reakcja shaderów schodów na pochodnię: up 2,219× / down 2,369× jasności otoczenia. Sprawdzone mikstury, obóz, przepełnienie, regeneracja w walce, statusy/fazy Króla, zachowanie wyjść i mgły oraz pivot furtki. Import/kompilacja i self-review PASS; gameplay/FPS NOT RUN. Editor API: prefab wyjścia, 2 wpisy glosariusza i import 3 nowych sprite'ów; brak zmiany save/schema, ręcznego setupu, Computer Use, agentów, commit/push.

Raport/obrazy/prompty builtin image_gen: ../LocalHandoffs/mana-exits-deadking-20261026/report.html. Dokładne wyniki: validation.json oraz końcowe XML. Wcześniejsze informacje o naturalnym odzysku many co 6 s i grafice upstairs v2 są zastąpione tą decyzją.

## Korekta polaryzacji przeciwników — 2026-10-25 — READY_FOR_SMOKE

Wdrożono zatwierdzony wariant HP/MDEF dla14zwykłych mobków. ATK i jego cała krzywa, PDEF, dodge/crit/speed/PA, zasoby, skille, odporności statusów, łupy, encountery oraz wszystkie9pozostałych definicji przeciwników (bossowie/fazy i specjalny Rycerz) zachowane.929hashy assetów: dokładnie14zmian; wszystkie pola pozaGrowthData.HealthCurve, BaseStats.MagicalDefense oraz MDEF istniejącejDefenseCurve identyczne z zamrożonym stanem przed kartą. Nie zmieniano przywołań Druida.

CharacterGrowthData ma opcjonalną HealthCurve (absolutneHP, pusty/błędny profil zachowuje legacy). CharacterLevelGrowthSystem interpoluje jednostki wzrostu, fabryka stosuje krzywą również przyL1. Ranne/ocalałe zapisane jednostki nie dostają darmowego leczenia przy odtworzeniu/refreshu; HP ponad nowymax jest clampowane. Brak zmiany schematu save. Wcześniejsze współczynnikiHP w definicjach pozostawione jako fallback. EnemyRoleDurabilitySetup autoruje tylko zwykłe14ID przez EditorAPI, bez mnożenia już zmodyfikowanych wartości; oba wcześniejsze authoring paths zachowują nowy wariant i idempotencję.

Zachowano wszystkie zatwierdzone kotwiceHP1/10/20/30/40/48/50. Pierwszy eksport wszystkich50poziomów wykrył+6–7%pracy przyL15i około+3%przyL25; dodanoHP15/25i ponowiono produkcyjne eksporty wszystkich zmienionych poziomów11–29. Finalne eksporty wszystkich1–50zgodne z finalną krzywą. Wyjątek zaokrągleniaMDEF względem propozycji: WilkL48=61 zamiast60, pozostałe kotwice dokładne.

Wynik: średnia rozpiętość trwałości naL1 katakumby3,83×→2,33×, las2,76×→1,95×. Wszystkie poziomy: katakumby2,18–2,62×, las1,88–2,16×. Budżet mieszanej drużyny we wszystkich długościach/poziomach względem sprzed specjalizacji: katakumby−1,8%…+1,4%, las−2,6%…+1,7%; pojedyncze seedy odpowiednio około−5,5%…+6,4% i−6,2%…+5,7%. Normalne lochyL15 około+0,1%katakumby/−0,2%las. Klasy nie idealnie równe: Mag w katakumbach do+7,2%pracyL44; Mag w lesie od−5,9%L14do+4,6%L50. Nie utożsamiać budżetu basiców z procentem trudności całych walk.

Weryfikacja: autorowanie1PASS, dopasowaniepośrednie1PASS, finalna regresja111PASS/0FAIL/0SKIP (wzrost, staty, role, idempotencja, legacyfallback, save/restore, pełny ponowny eksport11–29). Pierwszy przebieg:116PASS/1FAIL; stara asercja ATK porównywała raw baseline z efektywnym ATK po istniejącym mnożniku0,85. Poprawiono wyłącznie test; frozen i current MiecznikL22 mają identyczne171,84584minimumATK. HistoricalHPassertions sprawdzają nadal legacy na odłączonej kopii bezHealthCurve; noweHPma własne testy wszystkich50poziomów.

156024produkcyjne podglądy skilli: basici4klas×2drzewka×4materiały na1–50; dodatkowe skille, prawdziwePA/zasoby, przebicie i pełny oczekiwanyDoT na17poziomach, także progi zależne od nowegoHP. Porównanie oryginalny stan / poprzednie role / wdrożenie. Bez AI/autoplay i losowańRNG. Druid przy1własnej narośli, bez osobistego udziału DPS/healu przywołań i bez wyceny setup/CC. Późny gear25/35/45 referencyjny, aktualna produkcyjna pula części kończy się15.

Te same36pełnych układów6seedów71016–71021 ×3długości ×2biomy, przeliczone na50poziomach; nie wygenerowano1800nowych map. Zachowane censusy1/10/20/50są identyczne poza poziomem statystyk; topologia i wybór encounterów bazują na seed. Żadne definicje map/encounterów nie zmienione.

Raport: ../LocalHandoffs/enemy-role-durability-implementation-20261025/report.html. Dowody: validation.json, regression-tests.xml, actual-stats.csv, profiles.csv, defense-spans.csv, dungeon-work.csv, per-route-work.csv, skill-costs-and-dot.csv, defender-armor.csv oraz applied-level-1…50.json. Pierwotne baseline'y i historyczne raporty zachowane; authoring starej karty nie jest częścią finalnego filtru odczytowego. Bez Computer Use, agentów, commit/push. Brak ręcznego wiring w Unity.

Smoke użytkownika (NOT RUN): świeże spotkanie Obrońca/Okultysta oraz grupa zDruidem, cios fizyczny i magiczny oraz kontrskill z przebiciem; zapis/wczytanie rannegoNPC bez darmowego leczenia. Szczególnie sprawdzić odczuwalny czas działania supportaDruida. Nie obiecywać identycznego zużyciaHP/zasobów na podstawie samej teorii.

## Propozycja zmniejszenia polaryzacji mobków — 2026-10-25 — PROPOSAL_ONLY

Nie wdrożono zmian gameplayu. Wariant: redystrybucja HP i obniżenie wybranych MDEF, bez zmian PDEF/ATK/dodge/crit/speed/PA/status resist/encounterów. Mocny fizyczny pancerz Obrońcy zachowany; jego HP na L1 339→209. Okultysta HP154→176, MDEF95→48. Rozpiętość średniej trwałości L1 katakumby3,83×→2,33×, las2,76×→1,95×; per-klasa nadal możliwe większe różnice, np. Wladimir katakumby3,14×. HP ma osobne proponowane kotwice1/10/20/30/40/48/50; nie kopiować procentuL1 na całą grę. MDEF stały mnożnik istniejącej krzywej.

7/7 nowych odczytowych testów ExportRoleProposalStatGrid PASS. Produkcyjne preview na detached runtime stats, source JSON i RNG niezmienione, wariant1,0 zgodny z frozen theory. Użyto144 poprzednich census instances (6seedów×3długości×2biomy×4poziomy); nie generowano nowych. Dla mieszanej drużyny łączne budżety długości/poziomów od−1,3% do+0,9% względem sprzed zmian; pojedynczy seed około−5,0%…+5,4%. Poszczególne klasy nie idealnie0: np. normalne L50 katakumby Mag+6,1%, fizyczne−1,6%…+0,8%. To teoria trwałości/basiców, nie dowód równej trudności pełnych kitów. Druid1growth, przywołania/CC/setup poza oceną osobistegoDPS. DoT model ma caveat progów zależnych od nowegoHP. Bossowie/fazy bez zmian. Przy wdrożeniu wymagane dopasowanie krzywych i testy poziomów pośrednich; jeszcze nie zaimplementowane.

Raport: ../LocalHandoffs/enemy-role-correction-proposal-20261025/report.html; proposed-stats.csv, profiles.csv, defense-spans.csv, per-route-work.csv, dungeon-work.csv, validation.json, grid-tests.xml. Scope-check nadal PASS: dokładnie22 wcześniej zatwierdzone assety zmienione względem stanu pierwotnego; brak nowych zmian definicji przez tę propozycję. Bez agentów, Computer Use, commit/push.

## Profil ofensywa/defensywa każdego mobka — 2026-10-25 — ANALYSIS_COMPLETE, NO_BALANCE_CHANGES

Użytkownik chce przed dalszym balansem ocenić każdego mobka: ile ataków bohatera wymaga zabicie go, ile własnych ataków potrzebuje na bohatera, proporcję osi i odchylenie od średniej oraz rozpiętość względem klas bohaterów. Bez zmian gameplayu i bez autoplay. Poprzednie+15,5% katakumb to praca basicami, nie pomiar całej trudności. Nerf przeciwnej osi może kompensować moc, ale zwiększa specjalizację — nie utożsamiać neutralnej siły z niewielką polaryzacją.

D=HPmobka/średni damage basicu bohatera; A=HPbohatera/średni damage basicu mobka; ofensywaO=1/A, defensywaD. Pudła/krytyki wliczone, ułamkowe indeksy bez overkilla. Obie osie normalizowane osobno do średniej mobków tego samego biomu/poziomu i tego samego bohatera/gear/build, potem równo uśrednione. ProporcjaR=Orel/Drel; indeksroli=log2R−średnia(log2R) po typach mobków. Mean0; dodatni ofensywny, ujemny defensywny, +1 oznacza2× bardziej ofensywną proporcję od środka geometrycznego, NIE procent mocy. Osobny wskaźnik siły wymianyD/A, przed/po na stałej starej bazie, geometryczne uśrednienie sparowanych relacji. Dzięki temu centrowanie roli nie ukrywa zbiorowego buffa. Bossów nie wliczano do średnich zwykłych przeciwników.

14 zwykłych mobków, 4 bohaterów ×2 legalne drzewka ×4 materiały ×7 poziomów. 4 modele: bezpośrednie basici; basic+pełny oczekiwanyDoT; tempoPA/AV; tempo+DoT. 25088 wariantów,784 profile mobków,448 profili bohaterów. Ten sam finalny theory.json poprzedniej karty (obrońca1500/1900 na48/50), bez implementacji proponowanego dalszego nerfa. HP/ATK/szybkość/zasoby przed/po zachowane.

Druid wymaga jawnego stanu: Gniew Natury zadaje0 przy0 własnych narośli. Nowy odczytowy EnemyBattleSimulationTests.RolePolarity.cs liczy produkcyjne podglądy przy0/1/3 naroślach w detached session, bez przywoływania/skilli/tur/RNG i zapisu assetów. 7PASS/0FAIL; model główny dokładnie1 narośl, osobne stany i ograniczenie: nie wyceniono leczenia/trucizny samych narośli. Druid nie jest klasyfikowany jako zwykły tank z zerowym atakiem. Ghul/Umarlak/Wilk i Drzewo mają osobny wynik zDoT; bez niego profil jest niepełny. DoT liczone z szansą/hitem/odpornością za cały czas jednej aplikacji, nie pewne natychmiastowe obrażenia.

WynikL1: rozpiętość defensywy katakumb przed2,57×, po3,83×; bohaterowie w tym samym benchmarku2,68×. Las przed2,53×, po2,76×; bohaterowie2,57×. Obrońca uśredniony po4 klasach: D15,7→28,8, A32,2→33,2, indeksroli−1,96 i siła wymiany+64%. OkultystaD6,1→7,6 A10,2→10,2, rola+1,46, siła+18,8%. UpiórD7,3→9,3 A14,5→14,5, rola+0,73, siła+27%. Paladyn w benchmarku katakumb rola−0,94, Mag+0,99 — słabsza skrajność niż mobki. Ghul bezDoT−0,78, zDoT około+0,2; nie uznawać go na podstawie samego bezpośredniego hitu za wyłącznie defensywnego.

Przykład kompensacji: przy pozostawieniu obecnej trwałości Obrońcy samo odtworzenie starego wskaźnika wymiany wymagałoby naL1 około39% dodatkowo mniej SKUTECZNYCH obrażeń basicu (nie równoznaczne zATK−39%). Względem Wladimira około−49,4%, Jerzego−45,2%, Maga−1,9%, Paladyna−49,1%. Jeden globalny nerf obrażeń nie odtworzy naraz każdej starej relacji. Podwojenie defensywy i połówka ofensywy utrzymuje iloczyn mocy, a polaryzacjęO/D przesuwa4× w stronę defensywy. Przed decyzją o zmianach przejrzeć obie osie, nie sam indeks roli.

Raport: ../LocalHandoffs/enemy-role-polarity-20261025/report.html. CSV: before-after, mob-profiles, hero-reference, polarization-spans, paired-inputs, druid-contexts, compensation-by-hero, boss-stat-controls oraz validation.json. Bossowie/fazy to osobne kontrole1×ATK, bez pełnych mechanik osłon/aur/niewidzialności; nie porównywać ich liczby hitów wprost z realnymi basicami zwykłych mobków. Widmowy Rycerz poza ilorazem, nadal3 trafienia. Wyższe części25/35/45 referencyjne z arkusza. Sprawdzone znaki i mean0 na niezależnych przykładach, widoczność buffa i algebra kompensacji; źródła niemutowane. Bez agentów, Computer Use, commit/push.

## Audyt trwałości przed/po balansem ról — 2026-10-25 — ANALYSIS_COMPLETE, RETUNING_REQUIRED

Zgodnie z prośbą porównano aktualne mobki z zamrożonymi definicjami SPRZED zmiany, a nie tylko między sobą. Gameplay niezmieniony; propozycja dalszego osłabienia Obrońcy jeszcze niewdrożona. Źródło: finalny theory.json poprzedniej karty, realne basici, te same drzewka/wyposażenie przed/po. 14 zwykłych mobków, 4 klasy ×2 drzewka ×4 materiały ×7 poziomów, 392 porównania i6272 warianty. Osobno HP/średni damage próby z pudłami/krytykami oraz HP/średni damage pod warunkiem trafienia. Indeksy ułamkowe bez overkilla, nie dokładne tury/ETTK.

Nowy odczytowy EnemyDurabilityRouteCensusTests eksportuje AKTUALNY generator/placement:36 pełnych układów (6 seedów71016–71021 ×3 długości ×2 biomy), każdy na1/10/20/50 =144 instancje. Wszystkie piętra/pokoje/odnogi/encountery, bez omijania i urywania po porażkach AI. Składy15 aktualnych encounterów zgodne z wejściem statystycznym. Nie użyto starych map lasu ze starą wersją patrolu. Census4PASS/0FAIL i niemutowanie definicji; bez symulacji walk, AI i zmian assetów. Końcowa kontrola929 hashy nadal dokładnie22 wcześniejsze zmienione definicje.

WYNIK: zmiana nie jest neutralna dla budżetu trwałości. Normalne lochy, praca mieszanej drużyny na równowartość pełnych pasków PA: las na1/10/20/50 +4,7/−5,8/−6,4/−5,1%; katakumby +15,5/+1,4/+0,9/+3,6%. W katakumbach na1 poszczególne klasy potrzebują +20,8% Wladimir/+17,4% Jerzy/+25,2% Mag/+22,4% Paladyn prób basiców. Na50 fizyczni −0,3/+0,3/+3,1%, Mag+18,1%. Las na50: fizyczni około−10..−11%, Mag+2,5%. Warianty długości i seedów w raporcie; krótkie katakumby na1 +17,8% mieszanej pracy, długie+14,8%.

Największe zmiany na1: Obrońca vs Cięcie16,3→33,3 prób (+104%); Okultysta vs Kula Energii5,2→12,3 (+136%); Upiór vs Kula Energii5,9→11,1 (+90%). Sam Obrońca odpowiada za+10,7pp z+15,5pp mieszanego budżetu normalnych katakumb. Niewielkie spadki presji wrogich basiców (około1–3%, zmiany krytyków) nie dowodzą kompensacji większej trwałości. Bossy oddzielnie: stat-only x1ATK przed/po, bez aktywnych osłon/aur/faz; Widmowy Rycerz nadal3 trafienia. Bez mieszania bossów do standardowej puli.

Wnioski ograniczone do basiców; nie dowodzą liczbowej zmiany całej trudności/zasobów. DoT/odporności, aktywne kontry, CC, leczenie, Kościana Osłona, geometria i rzeczywiste rotacje poza tym modelem. Późne części25/35/45 referencyjne z arkusza. Przed uznaniem balansu ról za neutralny potrzebna ponowna kalibracja względem łącznego budżetu składów i poziomów (proponowana tolerancja±5%, do decyzji), nie samych relacji pancerzy. Wcześniejszy READY_FOR_SMOKE oznaczał zakończenie implementacji i testów kodu, nie potwierdzenie neutralności trudności; ten audyt wyraźnie wskazuje nierozwiązany problem balansu.

Raport: ../LocalHandoffs/enemy-durability-before-after-20261025/report.html. CSV: per-enemy-before-after, per-build-indices, dungeon-work-summary, per-route-work, encounter-work, change-contributions, enemy-basic-pressure, boss-stat-only-indices; validation.json i census-tests.xml. Bez agentów, Computer Use, commit/push i mutacji gameplayu.

## Drugorzędne statystyki przeciwników według ról — 2026-10-25 — READY_FOR_SMOKE

Wdrożone 18 profili bazowych +4 warianty faz (22 definicje): obrony, uniki, krytyki i wybrane odporności zgodnie z zatwierdzonym kierunkiem lore/roli. Mocny pancerz Obrońcy, magiczna osłona Upiora/Okultysty, zwinność bestii/elfów, słabsza ochrona strzelców po dojściu, pień Drzewa/Enta. Upiór i Król odporni na bleed/poison; zachowana słabość drewna na burn. HP, ATK (łącznie z mnożnikiem .85), szybkość, celność, zasoby, skille, łupy, wygląd oraz reguła 3 trafień Widmowego Rycerza pozostają bez zmian.

Opcjonalna absolutna DefenseCurve w CharacterGrowthData; interpolacja po dotychczasowych jednostkach wzrostu, walidacja i zaokrąglenie wyniku. Brakująca/pusta/niepoprawna krzywa zachowuje legacy. Nowy EnemyRoleStatsSetup zapisuje celowane definicje przez Editor API i jest końcowym krokiem EnemyScalingBalanceSetup, więc ponowny pełny authoring nie cofa profili. Bez zmian schematu save i pracy per-frame; restore przelicza wzrost z definicji.

Użytkownik zastąpił akceptację przez autoplay porównaniem statystyk/teoretycznych trafień. Nie uruchamiano kolejnych symulacji AI po tej decyzji; wcześniejsze wyniki zachowane wyłącznie jako historyczne artefakty, nie dowód balansu. Finalny odczyt: 224 konfiguracje bohaterów (4 klasy ×2 legalne drzewa ×4 materiały ×7 poziomów), 546 wierszy statystyk, 9856 indeksów trafień, 18480 kart skilli. Ten sam gear builder co wcześniejsza propozycja: level-fit Common, jakość zależna od poziomu, biżuteria od10, bez unikatów/ulepszeń. Basici obejmują wszystkie materiały; dodatkowe skille/DoT materiał0. Uwzględnione trafienie, krytyk przed pancerzem, przebicie, faktyczne PA/zasoby, Wyładowanie i próg50 oraz szansa/czas DoT. Bez RNG, rzutów, decyzji i tur. HP/średni damage to ułamkowy indeks prób, nie dokładna prognoza tur/zabicia z overkillem.

Pomiar realnego Cięcia ujawnił przeszacowanie późnego Obrońcy: pierwotna propozycja dawała średnio74,7% redukcji na48 i72,4% na50. Skorygowano wyłącznie późny PDEF:1500 na48 i1900 na50, nowa kotwica48; MDEF322/360 zachowane. Finalne średnie redukcje przed przebiciem na1/10/20/30/40/48/50:66,2/65,5/66,7/67,2/67,3/64,4/64,0%. Każdy poziom sprawdza osobny test celu60–70%. Na50 indeks prób zwykłego Cięcia wzrósł16,4→21,6. Pozostałe profile zachowują zatwierdzone wartości.

Pełny parytet kitów NIE jest dowiedziony: same pancerze mają małą różnicę globalną (kontrola około−7,6% do+5,1% przewagi magii), ale istniejąca przewaga Maga w darmowych basicach pozostaje. Przykład50, damage/pasekPA vs średnia Wladimir/Jerzy: las75,8→51,4%, katakumby54,3→26,6%. Nowe pancerze zmniejszają tę różnicę, nie tworzą jej. Bez zmiany bohaterów/AI/celności czy kosztów w celu wymuszenia parytetu. Dalszy balans basiców i gwarantowanego trafienia wymaga osobnej decyzji. Bossowe aury/fazy/osłony i DoT pełnej rotacji opisano jako oddzielne ograniczenia; nie mieszano ich do standardowej puli15 encounterów. Części25/35/45 są referencyjne z arkusza; aktualna pula produkcyjna kończy się15.

Końcowy pakiet157 PASS/0 FAIL, 4 explicit authoringi pominięte; obejmuje7 odczytów teoretycznych, wzrost/stat pipeline, damage/statusy, Wyładowanie, stun/DoT i save/restore. Celowany authoring osobno1 PASS. Wczesny zbyt szeroki eksport przekroczył limit180s; finalnie rozdzielony na7 poziomów, wszystkie PASS. Spośród929 hashy zmienione dokładnie22 dozwolone definicje; pełne porównanie pól23 definicji i kontrola core na7 poziomach PASS. Sprawdzone przywrócenie stanu bohatera, niemutowanie źródeł i brak pobierania combat RNG. Kompilacja/import i final self-review PASS. Manualny gameplay/FPS NOT RUN. Bez ręcznego setupu, agentów, Computer Use, commit/push.

Raport i dane: ../LocalHandoffs/enemy-role-implementation-20261025/report.html; theory-stats.csv, theory-hit-indices.csv, theory-skills.csv, basic-attack-summary.csv, defender-comparison.csv, armor-only-screen.csv, validation.json oraz XML. Smoke: nowa walka z Obrońcą, zwykły cios vs Wyładowanie z Przełamaniem, fizyczny doskok do Okultysty/Upiora i obie fazy bossów. Balans całych kitów nie jest zamknięty samym statusem READY_FOR_SMOKE.

## Wdrożony balans dropu materiałów — 2026-10-25 — READY_FOR_SMOKE

Zgodnie z „dobra to zmieniaj drop” zapisano celowanym Editor API dwa produkcyjne assety toreb zasobów, las/katakumby i oba przedziały. Wdrożone proporcje kowalskie55/32/3/10% oraz80/8/3/9% (metale/skóry/drewno/włókna), wszystkie gatunki i profile biomów z uzgodnionej analizy oraz obniżenia ilości pod2,5 ekwiwalentu przeciętnego przedmiotu Normal na normalną wyprawę. Mnożniki względem wcześniejszej propozycji: próg5 las0,777678 /katakumby0,456628, próg15 las0,478574 /katakumby0,278843. Runiczne11–20:85% klejnotów/15% wapienia, to samo obniżenie ilości; bez nowych gwarantowanych runicznych dropów1–10. Nieużywane pyły energetyczne usunięte z podstawowych toreb. Zmiana dotyczy nowych losowań: już otrzymane torby i zachowane nagrody starej wyprawy nie są rerollowane.

Nowe opcjonalne SelectionWeight w LootBagPoolEntryData i ważony wybór w LootTableSystem; brakująca/zerowa/ujemna/niefinitywna waga oznacza1, stare pule zachowują wybór uniform i liczbę drawów RNG. Suma wag w double. Nowe profile stosują jednostkowe losowania i jedną część ułamkową kategorii; małe średnie nie są zaokrąglane do gwarantowanej1 sztuki. Bonus dropu nie jest dodawany niezależnie do każdego nowego gatunku. Nowe pule dopuszczają powtórzenia zwiększające stos, stare DistinctSelectionGroup nadal wykluczają powtórzone wpisy. Authoring kompletny lasu/katakumb kończy konfigurację tym samym samodzielnym profilem C#; nie wymaga lokalnych CSV.

144 pary pełnych clearów na identycznych seedach241026..241037 ×3 długości ×2 biomy ×L10/L20: identyczne piętra/pokoje/walki/wrogowie/skrzynie. Zaobserwowany budżet Normal na normalnej wyprawie: las L10 2,56, katakumby L10 2,35, las L20 2,68, katakumby L20 2,46. Wszystkie w celu2–3. To ekwiwalent wartości materiałów do przeciętnej broni/pancerza Normal, nie gwarancja integer craftów konkretnych receptur; początkowy zapas, dokupywanie, opłaty złota, bezpośrednie materiały poza podstawowymi torbami i gotowy gear poza metryką. Biżuteria współdzieli metale, nie jest darmowym dodatkiem do ekwiwalentu.

86 odrębnych przypadków ma końcowy PASS; końcowa regresja77 PASS/0 FAIL, explicit baseline/authoring/audyty osobno. 100000 losowań toreb obejmuje każdy materiał i bonus0/10%; niezależne testy rodzin/ilości, granice/legacy/duże wagi, distinct, zapis/wczytanie zachowanych łupów, alchemię, jedzenie i source authoring. Wczesne błędy dwóch helperów testowych naprawione i wszystkie przypadki ponownie PASS; historyczne XML zachowane. Spośród929 sprawdzonych assetów zmienione wyłącznie dwie torby; pełny Editor JSON potwierdza zachowanie dwóch nie-rzemieślniczych pul na każdy biom/przedział. Szanse całych toreb, alchemia, jedzenie, gotowy gear, ceny i NPC nie były zmieniane. Wyniki losowe pojedynczego seeda mogą się różnić przez zmienioną sekwencję losowania.

Kompilacja/import, scoped whitespace i self-review PASS. Brak nowej pracy per-frame i zmian formatu save; tylko generowanie zawartości. Manualny gameplay/FPS NOT RUN. Bez ręcznego setupu, agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/crafting-material-drop-20261025/report.html; comparison.csv/json, validation.json, source.diff/asset.diff i scope.json. Smoke: rozpocząć NOWĄ wyprawę, otworzyć torby obu biomów i sprawdzić gatunki/ilości; stara mapa może mieć wcześniej zachowane nagrody. Ta sekcja zastępuje stan NOT_IMPLEMENTED wcześniejszych analiz.

## Ilość materiałów pod 2–3 przedmioty na normalną wyprawę — 2026-10-25 — RECALCULATED, NOT_IMPLEMENTED

Użytkownik ustalił cel2–3 zamiast zachowania obecnej wartości materiałów. Środek celu2,5 na tej samej miarze kosztowej co poprzednia tabela: ekwiwalent materiałów przeciętnej broni/pancerza Normal, nie gwarantowana liczba integer craftów. Na próbie144 wcześniejszych pełnych wypraw (12 seedów na biom/poziom/długość) mnożniki względem poprzedniej propozycji: próg5 las0,777678 (−22,23%), katakumby0,456628 (−54,34%); próg15 las0,478574 (−52,14%), katakumby0,278843 (−72,12%). Każdy profil normalnej wyprawy daje w tej próbie średnią2,5. Krótkie:1,13–1,58; długie:3,59–4,50. Jakości Poor/Excellent mają inne koszty, więc nie obowiązuje dla nich ta sama liczba przedmiotów.

Zachowane wszystkie proporcje wewnątrz każdego profilu biomu/puli: rodziny55/32/3/10 i80/8/3/9 oraz poszczególne gatunki. Runiczna pula11–20 skaluje ilość tym samym mnożnikiem, zachowując85/15; bez nowych dropów runicznych1–10. Różne mnożniki biomów zmieniają ich udział w łącznej podaży przy stałej liczbie wizyt; nie oznacza to zmiany lokalnych wag gatunków. Nie mnożyć szans całej torby ani alchemii/jedzenia/gotowego wyposażenia. Nie zaokrąglać wszystkich małych ilości w górę do1 — zachować średnią szansą/losowym zaokrągleniem.

Przeliczone ilości każdego surowca na torbę, runiczne ilości i każda długość/jakość: ../LocalHandoffs/material-drop-volume-20261025/report.html i plikiCSV. 14 kontroli rachunku/proporcji PASS. To nie pełna symulacja przyszłego samplera i wykonywania konkretnych receptur; złoto, wcześniejszy zapas, dokupywanie, bezpośrednie materiały poza podstawowymi torbami i gotowy sprzęt poza modelem. Biżuteria współdzieli metale i nie jest darmowym dodatkiem do ekwiwalentu broni/pancerzy. Bez zmiany assetów, gameplayu, sklepów, commit/push. Poprzednia propozycja zachowania wartości zostaje zastąpiona tym niższym budżetem; proporcje pozostają podstawą. Użytkownik poprosił o przeliczenie, nie wdrożenie.

## Materiały rzemieślnicze — analiza wszystkich receptur i propozycja — 2026-10-25 — ANALYSIS_COMPLETE, DROP_NOT_CHANGED

Zgodnie z prośbą użytkownika wykonano analizę i propozycję bez wdrażania dropu. Snapshot przez odczytowy test w Unity: 202 materiały, 114 receptur (90 Smithing, 13 Runic, 11 Alchemy), 195 aktywnych niestartowych części, wszystkie 352 kombinacje biżuterii i 13 toreb. Uwzględniono OutputAmount, pełne partie i współdzielone półprodukty, alternatywne receptury, wszystkie warianty części i jakości Poor/Normal/Excellent. 8192 pełne zestawy na scenariusz ×2 progi ×3 jakości =49 152 zestawy; to neutralny równy wybór wariantów, nie telemetria preferencji ani symulacja wypraw. Osobno koszty biżuterii i galdurytów. Zwykły upgrade kosztuje złoto, bez dopisywania fikcyjnego popytu na materiały.

Faktyczny katalog zawiera tylko progi części 5/15 i metali biżuterii 10/20. Enumy 25/35/45 i poziomy biżuterii 30/40/50 nie mają odpowiednich nowych części/metali. Galduryty: receptury granitu/marmuru są, ale aktywny EnchanterRole mapuje tylko wapień (11). Bez projektowania fikcyjnego popytu dalszych progów. Przy samych 8 sztukach gearu Normal: próg5 około49,2% metali/36,9% skór/2,6% drewna/11,4% włókien; próg15 około76,6/10,0/2,6/10,8. Wynik różni się od wcześniejszego przykładu jednej skóry kozy, bo uwzględnia wszystkie gatunki i uzyski.

Propozycja podstawowej puli kowalskiej, udział oczekiwanej liczby SUROWYCH sztuk (nie szanse wyboru nazwy): 55% metale/32% skóry/3% drewno/10% włókna dla 5, a 80/8/3/9 dla15; rezerwa na16 sztuk biżuterii odpowiedniego progu w modelu pełnego odnowienia czterech bohaterów. Konkretne procenty i średnie ilości KAŻDEGO surowca/biomu w proposed-material-shares.csv. Zróżnicowanie biomów zachowane; wspólny cel odtwarzany przez jedną torbę każdego biomu. Oczekiwany bazowy budżet wartości każdej puli/biomu zachowany, liczba surowców może wzrosnąć. Nie oznacza to gwarancji zwiększenia każdego rodzaju rudy w każdym biomie. Runiczna pula11–20:85% klejnotów/15% litego wapienia; rozpisane kolory i budżet. Bez nowej gwarantowanej puli runicznej1–10 ani zmian alchemii. Pyły energetyczne czerwony/żółty nie mają żadnego konsumenta recepturowego w aktualnym obiegu; rekomendowane wyłączenie z puli craftingu do nadania zastosowania, ich budżet nie został dopisany do nowych materiałów.

Pełny raport: ../LocalHandoffs/material-demand-20261025/report.html. CSV: wszystkie materiały/receptury/części/kombinacje biżuterii, popyt jakości, obecne torby, propozycja per surowiec/per biom i budżety. Eksport Unity PASS; 23 kontrole rachunku/inwariantów PASS. Brakujące odwołanie tylko w starej testowej torbie oznaczone osobno. Bez zapisu assetów/zmian gameplayu, sklepu, commit/push, agentów i Computer Use. Analiza oferty sklepowej oraz całych wypraw NOT RUN. Propozycja pozostaje do decyzji; nie oznaczać jako wdrożonego balansu.

## Furia: agresja chroni, obrażenia generują — 2026-10-25 — READY_FOR_SMOKE

Każdy zatwierdzony skill ofensywny resetuje licznik bezczynności i zapobiega utracie furii. Bazowe +10 generują tylko skille z obrażeniami kierowanymi na przeciwnika: bezpośrednimi albo DoT. Pudło/unik/absorpcja nadal nie odbierają generacji; raz za użycie niezależnie od liczby trafień/celów, bez kolejnego zysku z ticków i opóźnionych trafień. Efekt z obrażeniami na sobie/sojuszniku nie kwalifikuje osobnego osłabienia przeciwnika. Dotychczasowy wyjątek Wyładowania bez bazowej generacji pozostaje, zwrot za zabicie zachowany. Mana/impet, koszty, talenty, grace/CC/exploration decay zachowane.

Przegląd wszystkich produkcyjnych skilli z furią: bazowe +10 usunięte z Kopnięcia, Ogłuszającego Okrzyku, Rozkazu Unicestwienia i Zwodniczego Skoku ghula. Młot Zniszczenia zachowuje generację, bo obrażenia opóźnione realizuje istniejący owner bossa. Kopnięcie→Cięcie bez talentów: −20+0+10 = netto −10 furii. Bez sztucznej odporności: kolizyjne ogłuszenie nadal można ponawiać. Bonusy talentów mogą nadal poprawiać bilans rozwiniętych buildów; nie wprowadzono gwarancji ujemnego bilansu dla każdego buildu.

291 odrębnych końcowych PASS (290 w końcowej regresji i osobny celowany authoring słowniczka), zero nierozwiązanych FAIL. Sprawdzone wykonanie prawdziwych skilli, obie strony, kontrola/DoT/pudła/blokady/limity/opóźnienia, boss, Wyładowanie, stun/DoT i słowniczek. Zaktualizowany fallback i tylko opis furii w istniejącym assetcie przez Editor API; bez zmian assetów skilli/scen/prefabów. Kompilacja/import, scoped whitespace i self-review PASS. Bez nowego stanu runtime, schematu save i pracy per-frame. Manualny gameplay/FPS NOT RUN. Bez agentów, Computer Use, ręcznego setupu, commit/push. Dowody: ../LocalHandoffs/rage-damage-generation-20261025/README.md, classification.csv, validation.json, source.diff i test XML. Smoke: bilans Kopnięcie→Cięcie, tura samej kontroli i ponowne ogłuszenie kolizją.

## Balans DoT i Zatruty Sztylet 40 PP — 2026-10-25 — READY_FOR_SMOKE

Zmienione współczynniki na turę: Ognista Strzała 90→30% ×2, Kula Ognia 80→25% ×3, Zatruty Sztylet 90→40% ×3, Rozcinający Cios 90→40% ×4; NPC: przeszywający bełt 90→40% ×2, podpalona strzała 50→25% ×3, cięcie krzyżowe 85→30% ×3 na każdy z dwóch proców, toksyczne wymioty 75→50% ×3. Początkowe obrażenia, szanse, czasy, PA, trafienie, zasięgi i dodatkowe efekty zachowane. Zatruty Sztylet kosztuje 40 PP zamiast 25; koszt sztyletu na dystansie i brak kosztu sztyletu w zwarciu zachowane. Rank 0 Viruses nie blokuje już całej umiejętności przez opcjonalny zerowy debuff odporności; wyjątek ograniczony do tej umiejętności i odporności na truciznę. Ranki Venom/Viruses sprawdzone, inne niepoprawne zerowe efekty nadal odrzucane.

8 assetów skilli + 2 opisy w drzewkach zapisane celowanym Editor API, pełne źródła authoringu uaktualnione. Pełny JSON config przed/po: wyłącznie uzgodniony współczynnik/opis i koszt Sztyletu. Audyt 1536 rzeczywistych wykonań przed i tyle samo po na tych samych 64 seedach ×8 skilli ×3 odporności. Uwzględnia początkowe obrażenia, szanse, wszystkie ticki, dwa proce Cięcia Krzyżowego, zapłacone PA/surowiec oraz odzysk surowca. W 24 parach niezmienione obrażenia początkowe, częstość proców, PA i noże; surowiec zmienia się wyłącznie dla Sztyletu. To kontrolowany audyt jednego celu, nie pełny test balansu encounterów/AoE/buildów. Baseline Sztyletu wykonany po izolowanej naprawie ranku 0, przed balansem.

264 odrębne końcowe PASS, brak nierozwiązanych FAIL; osobny explicit authoring przeszedł, dlatego skip tego samego explicit w głównym przebiegu jest oczekiwany. Poprawiono przy okazji ujawnione stare rozbieżności: indeksy authoringu Miażdżącego Ciosu zgodne z istniejącą kolejnością debuff→stun i asercję lootów uwzględniającą już dodany Prowiant, bez zmiany tych assetów/gameplayu. Kompilacja, import, kontrola scope/whitespace i self-review PASS. Manualny gameplay/FPS NOT RUN. Bez zmiany schematu save; trwające DoT zachowują zapisany payload, nowe nałożenia używają nowych wartości. Bez ręcznego setupu, agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/dot-balance-20261024/README.md, comparison.csv/json, validation.json. Smoke: Sztylet przy 40 i 39 PP, z/bez Viruses, zwarcie/dystans oraz pełny czas DoT. Dalsze statystyki mobów, materiały i pętla furii pozostają osobno.

## Krótsze uzgodnione ogłuszenia — 2026-10-24 — READY_FOR_SMOKE

Wdrożone: Prawy Sierpowy i Miażdżący Cios ogłuszają na 2 tury zamiast 3; Okrzyk na 1 turę przy wydaniu <50 furii i 2 tury przy ≥50 zamiast 3. Próg korzysta z faktycznie zapłaconej furii. Kolizja z przeszkodą lub ścianą ogłusza na 1 turę niezależnie od niewykorzystanego dystansu, zarówno dla zwykłego Push, jak i łańcucha pchania Charge; nadal tylko jednostka na czole uderzająca w barierę. Wspólna stała CollisionStunDuration=1, bez globalnego clampu innych stunów.

Szanse/obrażenia/koszty/ruch i kolejność efektów zachowane. Miażdżący Cios nadal najpierw obniża odporność na kontrolę o 30% na 3 tury, potem próbuje stuna. Podgląd szansy z debuffem nadal zgodny. Pozostałe ogłuszenia, unieruchomienia, freeze, bomby/pasywki i reguły furii bez zmian. Cztery produkcyjne assety DoT (Fireball/FireArrow/PoisonedDagger/BleedingDestruction) identyczne SHA256 przed/po. DoT pozostają następnym tematem do uzgodnienia.

Trzy assety skilli zapisane celowanym Editor API; aktualne opisy, specjalny tooltip Okrzyku i źródła authoringu (w tym starszy focused setup) zgodne. Unity dopisało bieżące pola domyślne schematu przy reserializacji dwóch starszych assetów. 126 różnych końcowych PASS: 15 nowych przypadków wykonania i kolizji + explicit authoring oraz regresje warrior/paladin/destructible/status/preview/October24. Kompilacja, kontrola źródeł i self-review PASS; manualny gameplay/FPS NOT RUN. Już odliczane statusy w starym zapisie nie są przepisywane; nowe wartości dotyczą nowych nałożeń. Bez ręcznego setupu, agentów, Computer Use, commit/push. Dowody: ../LocalHandoffs/stun-duration-20261024/README.md, validation.json i dot-asset-hashes-before/after.json.

## Jedzenie i mocniejszy obóz — 2026-10-24 — READY_FOR_SMOKE

Dodany Prowiant: jedna zużywana porcja leczy 10% maks. HP właściciela poza walką, z zaokrągleniem w górę i clampem do maks. HP. Przy pełnym HP, martwej postaci lub cudzej porcji brak użycia/zużycia; bez wskrzeszania i odnawiania zasobów. PPM „Zjedz”, zwięzły tooltip, własna ikona importowana do 32×32 (Point, alpha), stosowanie porcji zgodne z obecnym inventory. Jedzenie wykluczone z wyboru przedmiotów bojowych, wykonania akcji bojowej, alchemii i refill. Food dopisany na końcu enumów bez zmiany istniejących wartości. Zapis/wczytanie porcji w inventory/schowku sprawdzony, bez zmiany formatu save.

Dopisana niezależna pula: 35% na jedną porcję z przeciwnika, 50% na jedną ze skrzyni, w 8 produkcyjnych tabelach lasu/katakumb dla obu bracketów. Pozostałe wpisy/szanse zachowane; pełne źródła authoringu również dopisują jedzenie. Zapisane/prerolled nagrody pozostają nietknięte. Porównane te same 32 pełne generacje (4 seedy ×oba biomy ×3 długości na L10, Normal na L20): identyczne liczby pięter/pokoi/walk/wrogów/skrzyni. 0→322 porcji z 343 walk (0,94/walkę; las 0,82, katakumby 1,05), 0→46 porcji z 88 skrzyń (0,52/skrzynię). Bez bonusu dropu; istniejący bonus nadal działa. To średnia losowa, nie gwarantowana porcja na encounter.

Obóz odnawia teraz 60% maks. HP i many zamiast 40%; furia/impet i liczba zestawów bez zmian. Opis istniejącego zestawu oraz źródło authoringu uaktualnione. 159 odrębnych końcowych PASS, brak nierozwiązanych FAIL; celowany Editor API wykonany, imported icon obejrzany, kompilacja/kontrola źródeł/self-review PASS. Dwie nieaktualne asercje wcześniejszej regresji bomb/ikony Straż! poprawiono do istniejących zasad/assetu, bez zmiany ich gameplayu. Manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, agentów, Computer Use, commit/push. Raport i pomiar: ../LocalHandoffs/dungeon-food-recovery-20261024/README.md, comparison.csv/json, validation.json. Ta sekcja zamyka uzgodniony punkt jedzenia i buffu obozu; szerszy balans CC/DoT, furii i materiałów pozostaje osobno.

## Baner użycia umiejętności — 2026-10-24 — READY_FOR_SMOKE

Zmieniony istniejący SkillCastBanner: 480×64 pod kolejką tur (szerokość ograniczona dostępnym miejscem), ciemna powierzchnia surface_solid, cienka ramka tooltip_frame, czcionka i kolory obecnego HUD. Ikona użytej umiejętności z CombatSkillPresentation.Skill oraz sama nazwa; nazwy bez prezentacji/ikony mają tekst bez pustego slotu. Czas 1,4 s z wygaszeniem przez ostatnie 0,25 s. Nowy cast podmienia treść i restartuje czas/alfa; koniec sesji, zmiana busa i disable czyszczą treść, ikonę i subskrypcję. Zachowane filtrowanie niewidocznych/obcych jednostek, synchroniczny pierwszy cast po ujawnieniu oraz rezerwa ActionSafeViewport. Baner nie blokuje kliknięć. Bez coroutine, nowych listenerów ani alokacji per-frame; alfa zmieniana tylko gdy faktycznie się zmienia.

Celowany zapis jednego prefabu przez Editor API i nowe pole referencji; bez ręcznego setupu ani zmian scen. 15 różnych przypadków końcowo PASS: mechanika prezentacji, binding/unbinding, legacy tekst, ukryte jednostki, viewport i pierwszy ujawniający cast (3 przypadki). Natywny render trzech przykładów obejrzany: ../LocalHandoffs/skill-cast-banner-20261024/banner-examples.png. Kompilacja, kontrola źródeł i self-review PASS. Manualny gameplay/FPS NOT RUN. Bez agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/skill-cast-banner-20261024/README.md i validation.json. Ta sekcja zastępuje wcześniejsze odłożenie wyglądu nazwy skilla; pozostałe tematy do uzgodnienia pozostają odłożone.

## Wycieńczające Zaklęcia — kumulowany debuff — 2026-10-24 — READY_FOR_SMOKE

Wdrożone i wczytane: −5 szybkości przeciwnika na kumulację, do 3 (−15), 3 własne tury odbiorcy. Udane nałożenie odświeża cały efekt także przy limicie. Zachowana bazowa szansa 70% i istniejąca formuła odporności na osłabienie. Jeden rzut na cel z użycia skilla, także gdy pierwszy rzut zawiedzie; wielokrotne/opóźnione trafienia tego samego użycia nie powtarzają próby. Każdy cel AoE ma własny rzut. DoT, kopie i reakcje pasywek oraz dodatkowe obrażenia nie dodają stacków. Zachowana blokada odzysku many podczas toggle, brak bonusu szybkości maga. Wyłączenie skilla nie usuwa istniejącego debuffu przeciwnika. Jak wcześniej wymagane są obrażenia HP: całkowita absorpcja przez tarczę nie daje próby.

Jeden wspólny efekt `mage.tiring`, pełny modyfikator −5×liczba stacków i czytelna liczba kumulacji; nazwa/ikona oraz zapis/wczytanie zachowane. Stary efekt −3 aktualizuje się do nowych wartości przy kolejnym udanym nałożeniu lub sam wygasa. Śledzenie prób jest lokalne dla walki, leniwe i czyszczone po ostatnim efekcie użycia, bez pracy per-frame i zmian schematu save. Zaktualizowane oba źródła opisu i celowany asset przez Editor API; reserializacja dopisała aktualne wartości domyślne schematu.

165 różnych przypadków ma końcowy PASS (16 nowych przypadków mechaniki + explicit authoring + regresje statusów, obrażeń, prezentacji, save/restore i VFX). Import/kompilacja, kontrola źródeł i self-review PASS; reserializowany YAML ma standardowe końcowe spacje przy pustych stringach Unity; manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, Computer Use, agentów, commit/push. Zakres i dowody: ../LocalHandoffs/mage-tiring-stacks-20261024/README.md oraz validation.json. Ta sekcja zastępuje wcześniejsze odłożenie buffu Wycieńczających; pozostałe nierozstrzygnięte tematy pozostają odłożone.

## Rzadkie składniki alchemiczne — 2026-10-24 — READY_FOR_SMOKE

Wdrożone/wczytane: jedna pula 20% na 1 składnik w obu torbach zasobów (także rozwijanych w skrzyniach); bezpośrednio ze szkieletu 5% na 1 kość, z nieumarłego 10% na 1 mózg. Alchemik zachowuje 3 typy ziół i 3 typy części, po 1 sztuce: 6 zamiast 27. Stary normalny zapas sklepu przycinany do nowych limitów podczas refresh/restore, bez refill na load. Buyback, zapasy gracza i zapisane zawartości łupów nietknięte. Inne pule, media, katalizatory i ceny bez zmiany konfiguracji. Pięć assetów zapisane celowanym Editor API, źródła authoringu spójne.

Powtórzone te same 144 generacje (12 seedów ×3 długości ×2 biomy × poziomy 10/20), identyczne liczby pięter/pokoi/walk/przeciwników w każdej parze. Katakumby L10 składniki krótka/normalna/długa: 48,25→4,00 /89,00→6,17 /130,00→9,42; kości 5,83→1,33 /8,17→2,58 /18,17→3,33. Normalne L20 90,17→6,75 składników i 11,83→2,33 kości. Las L10 35,33→0,50 /58,83→2,00 /94,17→3,00 używalnych składników; starsze ucho z leśnej puli nie jest skonfigurowanym składnikiem craftingu, referencję pozostawiono. Cel normalnych katakumb 5–8 składników i 1–3 kości osiągnięty średnio, bez twardego capu na seed. Bonus dropu w pomiarze zero.

118 różnych przypadków ma końcowy PASS; import/kompilacja, scoped diff-check i self-review PASS. Stary sklep, brak refill na load, buyback i zachowanie zapasów/torby gracza sprawdzone snapshot/restore. Gameplay/FPS NOT RUN, brak ręcznego setupu, Computer Use, agentów, commit/push. Nowe wartości dotyczą nowych generacji nagród; zapisane łupy nie są rerollowane. Raport i porównanie: ../LocalHandoffs/alchemy-scarcity-20261024/README.md (comparison.csv/json, before-results, after-results, validation.json). Ta sekcja zastępuje wcześniejsze odłożenie balansu składników i ilości u alchemika; materiały rzemieślnicze nadal osobnym tematem.

## Władimir — Wyładowanie i zamiana kitu — 2026-10-24 — READY_FOR_SMOKE

Wdrożone: Rozcinający Cios zamiast startowego Bandaża; Wyładowanie w Awanturniku: fizyczny cios 100% +1% za wydany punkt furii do 100 (mnożnik maks. 200%), minimum 20, zużycie do 100 z pozostawieniem nadwyżki, koszt 60% maks. AP jak Cięcie, zasięg 1 i celność 82. Brak generowania furii przez sam atak i brak redukcji tego kosztu. Żądza Zwycięstwa zwraca po bezpośrednim zabiciu 25%/50% wydatku (w dół); Przełamanie ignoruje 20%/40% obrony fizycznej, przy wydaniu co najmniej 50 furii podwójnie: 40%/80%. Atak/kryt i próg liczone przed zapłatą/zwrotem, podgląd i AI zgodne. Stare dwie pasywki nie wzmacniają już krwawienia. Pozostały balans CC/DoT i pętli furii nadal osobnym tematem.

ID węzłów i punkty zachowane; restore starego kitu migruje Bandaż i dawny grant/pasek bez utraty luk lub mutowania źródłowego snapshotu. Nowe ID skilla w katalogu, asset i trzy zmienione węzły zapisane Editor API, bez uruchamiania pełnego authoringu. 418 różnych przypadków ma końcowy PASS; 24 nowe przypadki mechaniki PASS, explicit setup osobno PASS. Import/kompilacja, scoped diff-check i końcowy self-review PASS; gameplay/FPS NOT RUN. Bez ręcznego setupu, Computer Use, agentów, commit/push. Zakres, raporty i smoke: ../LocalHandoffs/warrior-discharge-20261024/README.md. Ta sekcja zastępuje wcześniejsze odłożenie zamiany kitu.

## Poprawki walki i leczenia — 2026-10-24 — READY_FOR_SMOKE

Wczytane: Miażdżący Cios nakłada debuff przed stunem, a podgląd uwzględnia jego szansę (bez mutacji/RNG i dublowania zapisanego debuffu); 1 zwykły składnik alchemiczny napełnia miksturę do pełna; Wycieńczające Zaklęcia mają nazwę i ikonę także dla legacy źródeł; Święta Transfuzja leczy za 200% faktycznych obrażeń; leczenia siłą leczenia mają rozrzut (Uzdrowienie 100–140%, średnia zachowana) i podgląd zakresu krytycznego/szansy; pusta skrzynia zamyka się po zabraniu także złota; schody powrotne katakumb mają osobny sprite w górę; linka nie pozwala wybierać przeciwnika bez kosztu impetu, zachowując dostępny tańszy odzysk sztyletu; efektywny atak 23 produkcyjnych profili wrogów −15%, HP i wzrost bez zmian. Powtórzony native test dołączania przez niski murek: 4/4 sojuszników w obu biomach.

415 odrębnych końcowych przypadków PASS (ostatnia seria 412 PASS, 0 FAIL, 1 Explicit setup pominięty, wykonany osobno PASS). Obejrzane natywne rendery obu schodów z oświetleniem; kompilacja, scoped diff-check i self-review PASS. Manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, Computer Use, agentów, commit/push; prefab i wartości definicji zapisane Editor API. Dowody/pełny zakres i smoke: ../LocalHandoffs/combat-balance-20261024/README.md; deduplikacja: validation.json. CSV rzeczywistych drugorzędnych statystyk wrogów na poziomach 1/10/25/48/50 przygotowane do dalszej oceny, bez zmiany tych statystyk.

Zgodnie z poleceniem „nie implementuj spraw do rozpatrzenia/uszczegółowienia” odłożone: pełna zamiana kitu wojownika (brak kosztu/formuły nowego skilla), lootbag rarity i podaż materiałów, +5/3 stacki Wycieńczających (obecnie to −3 szybkości przeciwnika), zmiana wyglądu nazwy po skillu, jedzenie/nowa regeneracja HP, ogólny balans CC/DoT i pętli furii, balans drugorzędnych statystyk per lore. Bez zmian tych tematów, bez nowego blokującego pytania.

## Paczka poprawek lochów, walki i łupu — 2026-10-24 — READY_FOR_SMOKE

Wczytane: wspólne LOS/widzenie/odkrycie z limitem 20 kratek (zastępuje wcześniejsze 5/bez limitu); trudny teren wyłączony w generacji i restore, niskie przeszkody pozostają; deduplikacja ofert „oczyść” per biom; podgląd zasięgu na hover i przewidywane leczenie; Zbierz wszystko [B] w prawdziwym prefabie, auto-close pustego ciała i bordowe/szare krzyżyki; żywopłot x2; spójne nowe ikony złota/XP/honoru; patrol 2 rzezimieszków + 1 strzelec jako wersjonowana definicja zgodna ze starymi zapisami; XP faktycznie na zabicie (także od pułapki poza walką), pozostaje po ucieczce, bez powtórzenia przy zwycięstwie/scaleniu; stun po push w ścianę/przeszkodę; pauza podczas walki z zatrzymaniem czasu, AI/deadline i blokadą zapisu. Widoczni pobliscy sojusznicy są włączani atomowo przy zauważonym starciu przez low edge, z zachowaniem pełnych ścian, legalnej pozycji i obserwacji 360° w walce.

Ucho elfa: faktyczny drop lasu item.material.elf_ear miał pustą ikonę; wcześniejsza kontrola poprawnego item.vs10.ucho_elfa dotyczyła innej definicji. Editor API podpiął 12 brakujących ikon materiałów. Audyt 258 produkcyjnych definicji: 0 braków; testowe definicje/puste legacy sloty wyłączone. Rodziny jakości i kamienie współdzielą istniejące bazowe ikony. Render prawdziwego okna pokazuje oba ucha, także przedmiot z odtworzonej wyłącznie w pamięci kampanii 7E8AB8.

652 różnych przypadków ma końcowy wynik PASS w celowanych seriach; bez pozostałych failures. Native oba biomy: 4/4 bohaterów przez murek przed kolejnym tickiem, XP callback przed końcem walki; pause/save/restore/AI i oba rastry map sprawdzone. Import/kompilacja i C# whitespace PASS; prefab zapisany przez Unity zachowuje dwa odstępy w pustych polach YAML, bez ręcznej edycji. Obejrzane rendery łupu i niskich przeszkód. FPS/manual gameplay/hotkey B smoke NOT RUN. Brak ręcznego setupu, drugiego edytora, Computer Use, agentów i commit/push. Ta sekcja rozstrzyga wcześniejszy BLOCKED_USER dotyczący XP oraz zastępuje dawne zasady trudnego terenu. Pełny zakres, smoke, grafiki, prompty builtin imagegen i audyt: ../LocalHandoffs/dungeon-polish-20261024/README.md; deduplikacja wyników: validation.json.

## Stabilność podłoża lasu — seed 959042448 — 2026-10-24 — READY_FOR_SMOKE

Reprodukcja w końcowej GameView przy nieruchomej kamerze: alfa filtru i wiedza stałe, różnice na dekoracyjnej ziemi poza przechodnią mapą.781 różnie zabarwionych płatów współdzieliło jeden sortingOrder. Każdy ma teraz osobny priorytet poniżej podłogi; ciągłe tło pozostaje pod wszystkimi płatami. Rezerwacja nowej głębokości zachowuje kolejność. Bez zmian RGB/alfa, oświetlenia, wiedzy/LOS, sprite'ów, geometrii/generacji/save/assetów, nowych rendererów lub zegara aktualizacji. Short377/Normal781/Long1341 priorytetów unikalnych.30 aktualnych celowanych testówPASS. Dwie stare fixture'y dostosowano do produkcyjnych pokoi/przegród i inicjalizacji świateł poFogFloor, zachowując silne asercje. Import/scoped self-review zakończone. Końcowa GameView:200 klatek, trzy poziomy zoomu;179 porównań stałego kadru i607760–687879 pikseli dekoracyjnego podłoża, zero zmienionych kanałów RGB. Kadry sprzed/po mają inny zoom/rozdzielczość, więc oceniono stabilność w czasie, nie bezpośrednią parity. Klatka150 obejrzana; las renderuje się prawidłowo. Dowody: ../LocalHandoffs/fog-seed-959042448-20261024/README.md. Brak Editor setupu, Computer Use, commit/push. FPS NOT RUN.

## Sztylety po Wymianie i szarżach — 2026-10-23 — READY_FOR_SMOKE; XP — BLOCKED_USER

Wczytane odzyskiwanie własnych sztyletów po zamianie miejsc, szarży (w tym po drodze i łańcuchu pchnięcia), teleportacji i przemieszczeniu wymuszonym. Wspólny CombatCellEntrySystem używa istniejącego ScoutKnifeSystem po obsłudze pułapek; martwy aktor nie podnosi. Zwykły ruch zachowuje poprzednią kolejność i feedback, limity/własność/+8 Momentum pozostają kanoniczne. 102/102 EditMode PASS, w tym 4 nowe regresje i rzeczywiste skille SD_ScoutSwap oraz SD_Charge. Kompilacja/import/self-review/scoped diff-check zakończone; bez ręcznego Editor setup/save/assetów/commit/push. Dowody: ../LocalHandoffs/knife-arrival-xp-20261023/README.md. Smoke: Wymiana na sztylet, szarża przez sztylet/na sztylet; zapas i +8 raz.

XP niezmienione: obecnie faktyczne przyznanie i popup na zwycięstwo. Oczekiwana odpowiedź, czy przenieść przyznanie na zabicie (zachować także po ucieczce), czy jedynie popup. Nie zaimplementowano zależnej zmiany przed decyzją.

## Rytm encounterów na grafie tras — 2026-10-23 — READY_FOR_SMOKE

Nowe wyprawy z planowanymi odnogami wybierają pokoje encounterów przez DungeonEncounterRoomSelector po dotychczasowym losowaniu budżetu/składów. Ocena używa rzeczywistych Connections: obu ramion pętli, dojść do odnóg i przejść przez skrzyżowania. Preferuje krótsze ciągi bez walki, pokrycie różnych części grafu i mniej sąsiadujących starć. Cel czterech pustych pokoi jest miękki. Finał jest zablokowany w selekcji; na ostatnim piętrze bossa liczy się jako już obsadzony, bez drugiej regularnej walki. Start to granica tempa, nie dodatkowy encounter. Końce odnóg nie mają nowej gwarancji walki. Dotychczasowa kwalifikacja szablonów, liczby/definicje walk, RNG składów, bramy, EXP/loot i geometria zachowane. Seedowane remisy korzystają z istniejącej potasowanej puli; nie zużywamy nowego combat RNG.

Owner: generation-only Systems/Dungeons/DungeonEncounterRoomSelector, wywołany trzema nowymi liniami DungeonEncounterPlacementSystem wyłącznie dla profili MaxBranchTransitRoomCount>0. Zachowany stary draw jako kandydat, greedy dobór i najwyżej cztery przebiegi poprawiających zamian; bez zmian runtime AI/patroli/merge/LOS. Odległości grafu przygotowane raz i najbliższe centra cache'owane podczas ocen. Brak zmian serialized assetów/scen/prefabów i schematu save. Pełne snapshoty zachowują każdą zapisaną lokalizację encountera; zwykłe ponowne Place nie przestawia istniejących walk.

50 par świeżych generacji / 76 pięter: osiem seedów × sześć wariantów oraz dwa dodatkowe seedy normalnych katakumb. Wszystkie pary mają identyczne pokoje, łączniki, pola, bramy oraz sekwencję definicji encounterów na każdym piętrze. Najdłuższa prosta trasa przez puste pokoje: średnio 8,42→3,72, maksimum 24→6; 75 pięter krócej, jedno bez zmiany, zero pogorszeń. Sąsiadujące pary encounterów 124→15. Bramy w tej metryce otwarte, Start wyłączony, bez patroli/łączenia walk i bez ponownego chodzenia po odwiedzonych pokojach. To miara grafu, nie czasu gameplayu.

38 odrębnych focused testów PASS: 32 w głównym zestawie, trzy dodatkowe (zamknięty cykl i dwa wyposażenia długich wypraw), dwa pełne save/restore i jeden jawny baseline. Dwa explicit helpery pominięte w głównym zestawie: baseline uruchomiony wcześniej, orphaned navigation cleanup niepotrzebny i NOT RUN. Oba schematy przed/po obejrzane. Import/kompilacja/self-review/whitespace PASS. Koszt generation-only selector dla 90 pokoi/17 walk: mediana 19,241 ms w EditMode (po cache; wcześniej 57,049); całe Place na 50 wyprawach mediana 21,148→22,976 ms, max 96,556→83,170 ms. Nie jest to benchmark FPS ani dowód braku kosztu. Gameplay smoke/FPS NOT RUN. Bez ręcznego setupu, drugiego Unity, Computer Use, agentów, commit/push. Dowody: ../LocalHandoffs/encounter-rhythm-20261023/README.md.

Smoke: nowe wyprawy Lasu i Katakumb, także bossowe; przejść oba ramiona pętli i dłuższą odnogę, sprawdzić mniej skupionych walk, wymagany finał oraz zapis/wczytanie. Zapisane mapy zachowują stary rozkład.

## Minimapka kamery i obrys viewportu — 2026-10-23 — READY_FOR_SMOKE

Minimapa śledzi środek aktualnej kamery prezentacji DungeonFloorMapView zamiast lidera, również w trybie F i podczas kadrowania walki. Ta sama kamera jest już jawnie przekazywana przez istniejące oświetlenie lochu; brak nowych referencji sceny lub wyszukiwań obiektów. Cztery rogi viewportu są rzutowane promieniami na płaszczyznę mapy i przeliczane tą samą bazą komórek co znaczniki. DungeonMinimapMarkers rysuje biały pusty obrys 1,5 jednostki UI w minimapie i mapie M, reagujący na pozycję, zoom oraz aspect. Każda krawędź jest przycinana osobno, bez sztucznych boków na granicy widżetu; znaczniki pozostają nad obrysem. Duża mapa nadal kadruje wyłącznie znany teren, niezależnie od kamery.

Projekcja aktualizuje się w Canvas.preWillRenderCanvases po LateUpdate kamer, a obsługa M/Esc zachowuje wcześniejszą kolejność. Subscribe/unsubscribe przy enable/disable/destroy; stałe bufory rogów i reuse istniejącej geometrii markerów. Sam zoom i sub-cell pan nie zmieniają rastra terenu, nie wyzwalają LOS ani odkrycia. 30/30 focused testów PASS (26 minimapy + 4 projekcji/cache markerów), w tym callback renderu, oddzielny ruch lidera/kamery, pan/zoom, pusta i biała geometria, clipping oraz brak dirty mesh dla stania. Obejrzano oba rendery produkcyjnego prefabu. 120 niezmienionych odświeżeń overview: 4,533 ms łącznie z reflection w EditMode; to nie pomiar FPS. Import/kompilacja i scoped self-review/whitespace PASS; manual gameplay/FPS NOT RUN. Bez serialized edits, ręcznego setupu, drugiego Unity, subagentów, commit/push. Raport: ../LocalHandoffs/map-camera-20261023/README.md.

Smoke: w F odsuń kamerę od drużyny, zmień zoom, otwórz M i porównaj biały obrys z rzeczywistym widokiem. Powrót do śledzenia lidera oraz wejście w walkę mają zachować śledzenie faktycznej kamery. Skala minimapy pozostaje dotychczasowa; fragment obrysu poza widżetem jest przycinany.

## Przekraczanie pionowego murka — 2026-10-23 — READY_FOR_SMOKE

Pionowy niski murek był sortowany według południowego końca całego segmentu, przez co nakrywa zasłaniała przechodzącą w poprzek postać. Widok aktora rezerwuje teraz istniejące pasmo przed przekraczanym low edge; uwzględnia też nakrywę jego bezpośredniej południowej kontynuacji. Korekta działa tylko podczas przekraczania: zwykła eksploracja, layered traversal, pożyczone widoki walki oraz CharacterView nowych jednostek/przywołań. Koniec ruchu, teleport, wyłączenie widoku i wyjście z combat czyszczą stan prezentacji. Koszt PA, czas przejścia 0,6 s, logika kolizji, geometria, fog, authoring i zapis bez zmian. Zapytania korzystają z lokalnych krawędzi i istniejących posortowanych pasm, bez skanowania wszystkich ścian.

33 focused testy PASS: 16 przypadków obu orientacji/kierunków i czterech ścieżek widoku, 2 statycznej głębi, 8 tactical terrain, 3 kosztów AP i 4 borrowed combat. GPU odtwarza poprzednie zasłanianie pionowej korony; porównania przed/po korzystają z tej samej pozy i natywnych sprite'ów. Testy sprawdzają także przywracanie głębi po zakończeniu/przerwaniu oraz odrzucenie teleportów/diagonali jako low edge. Import/kompilacja w istniejącym Unity zakończone; self-review i scoped diff check PASS. Gameplay smoke/FPS NOT RUN. Bez serialized assetów, ręcznego Editor setupu, commit/push. Dowody: ../LocalHandoffs/low-wall-crossing-20261023/README.md.

Smoke: w istniejącej mapie przejdź przez pionowy i poziomy murek w obie strony, także podczas walki. Nakrywa nie powinna przecinać postaci w trakcie przejścia; po odejściu obowiązuje normalna głębia.

## Funkcyjne układy niskich przeszkód i 4wayTomb — 2026-10-23 — READY_FOR_SMOKE

Do obu produkcyjnych katalogów katakumb (zwykłych oraz Skeleton Slayer) podpięto kopię DRT_Catacombs_4wayTomb, zachowując ID, rozmiar, granice, 20 murków i cztery sarkofagi. Oryginalny asset użytkownika pozostał identyczny bajtowo. Kopia: ProductionRooms/DRT_catacombs_4way_tomb.asset; RoomFunction=sarcophagus. Aktualny footprint sarkofagu jest światowym 1x2, dlatego kopia dopuszcza obrót 0; pozostałe ustawienia powodowały wyjście rekwizytu poza podłogę albo nakładanie na low edge. Manifest authoredRooms zachowuje integrację także przy pełnym authoringu; jawne zastosowanie authoringu odświeża kopię ze źródła, zwykły import jej nie nadpisuje.

Przeprojektowano 7 istniejących szablonów katakumb: dwie krypty L, cztery zatoki sarkofagów z nawą 2 pola, balustrady T przy filarach, otwarty dziedziniec rytuału U, galeria między podporami, strefa mechanizmu i dwie wnęki przy pieczęci. Przeprojektowano 4 leśne: osłona kieszeni skały/pnia, podmokłego zagajnika, lewego stanowiska zasadzki przy skale i wewnętrznego łuku przesieki. Każdy układ ma opis funkcji oraz sprawdzany skrót/obejście. Dwa północne sarkofagi w sali sarkofagów przesunięto o jedno pole. Uwzględniono pełne rezerwacje rekwizytów oraz tylko bezpieczne obroty: crypt_bend 0/90/270, sarcophagus i seal 0/180; pozostałe warianty zachowują cztery obroty. Nie zmieniono wejść, footprintów pokoi, kotew/liczby przeciwników, grafu tras, bram, trudnego terenu ani budżetów walk i łupów.

39 odrębnych focused testów PASS: 37 regularnych (produkcja trzech długości, bramy i save/restore, rotacje/meble, trasy z uwzględnieniem propów, rzeczywisty wybór pokoju przez generator) + 2 jawne audyty renderów. Pozostałe dwa jawne ogólne audyty poprzedniej karty NOT RUN. Obejrzano 7 natywnych renderów: 4wayTomb, crypt_bend, winch oraz wszystkie cztery leśne; reszta katakumb sprawdzona na planach i przez testy. Import/zapis przez otwarty Editor API, bez ręcznego Refresh, Computer Use, drugiego Unity lub subagentów. Self-review/scope PASS; gameplay smoke i FPS NOT RUN. 13 istniejących assetów zmienia wyłącznie low anchors, safe rotations, wskazane dwa rekwizyty oraz dwie listy katalogów; jeden nowy asset produkcyjny. Bez commit/push.

Nowe rozmieszczenie dotyczy nowych wypraw. Pełne snapshoty istniejących zapisów zachowują wcześniejszą geometrię i obiekty. Brak ręcznego setupu w Editorze. Raport i obrazki: ../LocalHandoffs/authored-low-barriers-20261023/README.md.

## Niskie przeszkody — orientacja i łączenia — 2026-10-23 — READY_FOR_SMOKE

Pionowy żywopłot obraca istniejący krzak o 90 stopni; jeden renderer na krawędź w obu kierunkach. Pozycja korzysta z środka sprite’a po obrocie, a pion ma grounded baseline na południowym końcu. Sąsiadujące odcinki spotykają się na wierzchołkach logicznych ścian, również w L/T/krzyżach.

Natywne murki mają domknięte końce capu, narożniki L, T i krzyże oraz osobne lico na odsłoniętym południowym końcu pionu. Lico pochodzi z tego samego wycinka North co poziom; pionowa nakrywa zachowuje pełne 32 px fazy Side, bez szczelin między komórkami. Topologia używa istniejącego indeksu WallsAtEndpoint/GetEdgeEndpoints, a capy tej samej funkcji CreateDedicatedCornerSprite co pełne ściany. Nowe maski ramion rozszerzają ją o końce/T/krzyże; dotychczasowe pełne ściany zachowują wcześniejszą ścieżkę. Jeden właściciel capu per wierzchołek, bez zaślepek na prostych kontynuacjach. Zostają ścisłe przycięcia do wysokich ścian, collidery, traversal, fog i materiały oświetlenia.

101 odrębnych focused testów PASS (99 regresyjnych + 2 jawne rendery produkcyjne; końcowy rerun 7 PASS obejmuje zmienioną głębię żywopłotu). Sprawdzono 15 masek wierzchołków, ciągłość pionowych nakryw, styk lica z capem, fog, UV, ruch/AP, wysokie ściany i natywne rendery obu biomów. Obejrzano macierze murków (native i z pochodniami), izolowane żywopłoty L/T oraz ujęcia prawdziwych map. Self-review PASS; gameplay smoke/FPS NOT RUN. Bez ręcznego Editor setupu, zmian assetów/scen/prefabów/PNG, commit/push. Raport: ../LocalHandoffs/low-wall-joins-20261023/README.md.

## Las i katakumby — trudny teren i niskie przeszkody — 2026-10-23 — READY_FOR_SMOKE

Przywrócono mechanikę do produkcyjnego authoringu, bez zmiany grafu tras, liczby walk lub kotew przeciwników. Las: 10 szablonów z 33 polami trudnego terenu, 4 szablony z 12 krawędziami niskich krzaków. Katakumby: 12 szablonów z 42 polami oraz 7 szablonów z 18 niskimi krawędziami. Małe płaty 3–4 pola i odcinki 2–3 krawędzie, z zachowaną drogą wokół każdego odcinka. Wejścia/wyjścia i areny pozostają bez nowych stref. Blueprinty jawnie zapisują współrzędne; pełny authoring je odtwarza, a osobny request zapisuje wyłącznie DifficultTerrainCells/StructuralAnchors przez Editor API. 22 assety terenowe; przy zapisie potwierdzono wyłączność tych dwóch pól.

Reguły bez zmian: trudny teren +1 PA przy wejściu i 50% prędkości eksploracji; niski murek/krzak +2 PA i przejście 0,6 s. Las używa istniejących natywnych LowShrub/variants, bez nowych bitmap. Krzaki nie są drzewami, nie wygaszają się jako korony i nie dostają blokujących colliderów; mają grounded depth, światło i wiedzę mgły per-edge. Jeden renderer na odcinek poziomy, dwa na pionowy, zamiast trzech nakładających się casterów.

Katakumby: UV crop obecnej tekstury North/Side usuwa dokładnie dolne 8 pikseli; bez edycji PNG lub zmiany roota/collidera. Child kompensuje skalę przed obrotem; zachowane przycięcie przy pełnych ścianach. Stary renderer z arkusza low-wall nie jest używany w produkcyjnej prezentacji. Trudny teren korzysta z obecnego kamienia, tintu i drobnego gruzu zamiast starego brązowego kafla testowego. Leśne błoto dostaje osobną statyczną maskę dokładnie według pól gameplayowych, bez skanowania co klatkę i bez podglądu hazardu na ściółce poza mapą. Maska ma jeden texel na pole i jawny release przy rebuild/teardown.

97 odrębnych focused testów PASS: ruch/AP/przejście przez murek, 18 generacji trzech długości po placement obiektów, authoring/obejścia, zapis snapshotów, dostęp do wyjścia, światło/mgła, UV/native bounds i macierz połączeń murków. Obejrzano oba biomy i obie orientacje murków; GPU potwierdził różnicę leśnej powierzchni trudnego terenu. Nowy test sprawdza ukrycie krzaków przed odkryciem oraz zniszczenie maski przy rebuild. Test starego arkusza junction dostosowano do pojedynczego cropu, zachowując ścisłe granice przy pełnych ścianach i collider roota. Fixture AuditMap dostał istniejący lowWallPrefab, już podpięty w scenie gry.

Self-review zakończony: lifecycle, fizyka, głębia, koszt renderowania, content i save compatibility sprawdzone. Stare snapshoty pozostają bez nowych hazardów; nowe rozmieszczenie wymaga nowej wyprawy. Brak ręcznego setupu, zmian scen/prefabów/ProjectSettings i PNG. Unity zaimportowane poza Play Mode; bez ręcznego Refresh/Computer Use/drugiego Unity/agentów, commit/push. Gameplay smoke i FPS NOT RUN. Raport: ../LocalHandoffs/biome-terrain-20261023/README.md.

## Las — wyjście na obrysie całej mapy — 2026-10-23 — READY_FOR_SMOKE

Błąd: wybór furtki sprawdzał tylko jedną kratkę poza pokojem. Brama mogła przeciąć wąską przegrodę między polanami i wyglądać jak ich połączenie. Wspólna reguła DungeonForestTransitionBoundary indeksuje najwyższe/najniższe pole każdej kolumny całej mapy. Całe trzykratkowe pasmo otworu musi leżeć na jej zewnętrznym horyzoncie; za progiem w kierunku wyjścia nie może być żadnej podłogi innego pokoju lub łącznika, niezależnie od odległości. Zachowany dostęp 3x2 bez aktywnych przeszkód, preferencja północ/południe i centrowanie w płaskiej krawędzi. Bez skanowania co klatkę: wybór tylko podczas generacji/budowy prezentacji.

Wszystkie trzy ścieżki embeddingu generatora sprawdzają tę samą regułę po realizacji mapy. Jeżeli Final (lub Start na dalszym piętrze) nie ma takiej krawędzi, istniejący retry rozmieszcza ponownie dokładnie ten sam zaplanowany graf. Bez zmiany liczby pokoi, pętli, odnóg, budżetu walk lub zasad bram. Katakumby bez zmiany kryteriów. Nowy seed może dać inny układ, gdy dotychczasowe rozmieszczenie było nieprawidłowe.

Stare pełne snapshoty nie są przebudowywane: widok najpierw szuka rzeczywistej zewnętrznej krawędzi także na nich. Tylko jeśli zapisany Final jest całkowicie osłonięty innymi pokojami po obu pionowych stronach, zachowuje historyczne działające wyjście, żeby nie zablokować zapisu. W takim historycznym układzie pełna gwarancja obrysu wymaga nowej wyprawy. Jawny test sprawdza brak przesuwania zapisanych pokoi/mapy. Nie twierdzimy, że ten fallback naprawia dawny zamknięty układ.

75 odrębnych focused EditMode PASS: 71 w integration-tests.xml oraz 3 nowe przypadki bossa i 1 nowy seed native w final-render-boss-tests.xml (7/7); końcowy native retest w final-native-tests.xml. 72 układy regularnego lasu (24 seedy x 3 długości) sprawdzone po placement obiektów; 6 wariantów bossa, dostęp do progu, scena natywna, światło/głębia, E/LPM/confirm i zgodność planu/pętli/bram w obu biomach. Przed poprawką odtworzono dwa FAIL na sąsiedniej i dalekiej polanie; dodatkowo odkryto seed9 Normal/Long bez żadnej zewnętrznej krawędzi, naprawiony przez walidację embeddingu. Rendery obejrzane; aktorzy w native audycie stoją po przechodniej stronie niezależnie od orientacji wyjścia.

Self-review: zachowany graph/RNG planu, brak nowej pracy per-frame, brak modyfikacji save schema, serialized state lub artu. Nowy .cs.meta wygenerowany przez Unity. Bez manualnego Refresh/Computer Use/drugiego Unity, commit/push. Manualny gameplay/FPS NOT RUN. Ręczny setup niepotrzebny. Smoke: ponowne wejście do sceny dla istniejącej mapy; pełna gwarancja zewnętrznego położenia po nowej generacji. Raport: ../LocalHandoffs/exit-exterior-edge-20261023/README.md.

## Las — weryfikacja odpowiedzi furtki na światło — 2026-10-23 — READY_FOR_SMOKE

Poprzedni test potwierdzał rejestrację renderera i shader, ale nie zmianę pikseli. Dodano kontrolowane porównanie GPU na rzeczywistych produkcyjnych rendererach furtki/przęseł: ten sam ambient 0.34, tekstury, tint i property blocks; bez lokalnych źródeł vs jedna pochodnia przy progu (radius5, intensity1, kolor produkcyjny). Kamera izoluje renderery na czerni, bez zmiany ich materiałów na shader diagnostyczny. Przywracane są wszystkie parametry materiałów, warstwy i kamera.

2/2 produkcyjne seedy PASS. Seed171026: średnia luminancja pikseli ogrodzenia 0.1930 → 0.3256, 1.69x; 17654/17684 pikseli jaśniejszych o >0.01. Reakcja na oświetlenie jest potwierdzona. Wygląd względnie jasnego drewna wynika z wartości źródłowej grafiki oraz białego tintu furtki (przęsła .82/.83/.78), nie z renderowania unlit. Ta karta nie zmienia tintu, artu ani runtime. Nie jest to pomiar FPS ani zapis ruchu prawdziwej pochodni w Play Mode; to kontrolowany GPU test właściwego shadera na receiverach pobranych z produkcyjnej sceny.

Oba obrazy ambient/torch obejrzane; self-review testu/restore zakończony. Brak ręcznego setupu. Dowody: ../LocalHandoffs/exit-light-response-20261023/README.md, response-*.txt, tests.xml oraz gate-*-ambient/torch.png. Bez serialized state, Computer Use, kolejnego Unity, commit/push.

## Las — głębia przęseł wyjściowych — 2026-10-23 — READY_FOR_SMOKE

Przęsła furtki zachowywały sortingOrder z chwili budowy. Późniejsza rejestracja koron/obiektów przebudowywała kompaktowe pasmo głębi, ale odświeżała tylko drzewa; przęsła mogły przykrywać bliższe korony. Na dwóch produkcyjnych seedach regresja przed poprawką FAIL: 14 zamiast -210 oraz -250 zamiast -1154. Teraz ConfigureWallDepthSorting odświeża także przęsła według ich rzeczywistego progu. Bez nowego Update/skanowania co klatkę, nowych warstw projektu, bitmap, zmian serialized state, gameplayu lub zapisu.

51/51 focused EditMode PASS (dwa produkcyjne audyty + WorldObjectPrimaryInteractionTests); dodatkowa końcowa kontrola renderów zapisana osobno. Kontrola obejmuje późno dodaną linię głębi, zgodność brama/przęsła, kolejność aktora przed/za progiem i zachowanie porządku po oświetleniu. Natywne rendery dwóch seedów obejrzane. Strona za progiem jest poza mapą i nie służy jako wizualny test aktora; jej relacja głębi sprawdzana liczbowo. Self-review: brak rekurencyjnej rejestracji baz w refreshu, istniejący cleanup zachowany. Ręczny setup niepotrzebny; smoke po ponownym wejściu do sceny: końce przęseł pod bliższymi drzewami i postacie przed ogrodzeniem. Bez Computer Use, drugiego Unity, commit/push. Raport: ../LocalHandoffs/exit-depth-layers-20261023/README.md.

## Las — furtka połączona z linią graniczną — 2026-10-23 — READY_FOR_SMOKE

Poprzednie wycięcie koron obejmowało całą furtkę z przęsłami i pozostawiało szeroką lukę. Teraz rezerwowany jest tylko otwór 1,6 x 2,4; brama jest centrowana względem rzeczywistego prostego fragmentu krawędzi, zamiast średniej X pokoju. Przęsła z natywnego wycinka istniejącej grafiki v2 biegną po zamkniętej krawędzi i kończą się przy pierwszych korzeniach. Na schodkowych końcach mogą wejść maks. 3 kratki w solidny las; nigdy na przechodnią ziemię poza właściwą krawędzią. To wizualna ciągłość istniejącej blokady granicznej, bez nowych colliderów lub zmian grafu/nav.

Odcinki mają per-edge wiedzę mgły, istniejące oświetlenie/sortowanie i jawny cleanup materiału/sprite'a przy rebuild/destroy. Nie są koronami ani dodatkową interakcją/outline'em. Dobór jest wykonywany podczas budowy sceny, bez nowego skanowania co klatkę. Brak zmian encounterów, zapisu, PNG, scen/prefabów i ScriptableObjects.

58/58 końcowych testów PASS: E/LPM/hover/confirm, dostęp do trzech długości, dwa natywne rendery i kontrola ciągłości przęseł, oświetlenie/cache pokrycia/mgła. Dwa rendery obejrzane przed/po. Szerszy zestaw 74/75: historyczny ProductionLowerRoomCanopiesHaveBeforeAndAfterRenders nadal oczekuje braku legalnych divider trees i wycofanych room.forest.long_glade/blind_glade; nie zmieniano ani nie wyłączano tego testu. Jego założenia są sprzed produkcyjnych szablonów i poprawki cienkich przegród. Ten historyczny test nie jest potwierdzeniem gotowości obecnego authoringu. Szczegóły: ../LocalHandoffs/exit-boundary-continuity-20261023/README.md.

Self-review zakończony; 924 ScriptableObjects bez zmian. Ręczny setup niepotrzebny. Smoke po ponownym wejściu do sceny: oba końce furtki połączone z lasem, nadal czytelny otwór i hover/LPM/E. Istniejąca mapa może być wznowiona. Bez Computer Use, drugiego Unity, commit/push; gameplay/full clear/FPS NOT RUN.

## Wyjście z lochu — granica lasu, interakcja i obowiązkowa walka — 2026-10-23 — READY_FOR_SMOKE

Furtka lasu zastępuje fragment drzew NA zewnętrznej krawędzi pokoju; jej miejsce i prześwit są rezerwowane przed generowaniem koron. Punkt podejścia/interakcji jest pół kratki wewnątrz, bez otwierania logicznego ruchu poza mapę. Nowa grafika v2, natywne 128x85/32 PPU, profil Single/Point/FullRect, neutralniejsze materiały i oczyszczona alfa. Wyjścia rejestrowane do oświetlenia po jego inicjalizacji; sortowanie pionowego sprite'a według faktycznego progu. Katakumby zachowują schody.

LPM obejmuje grafikę wyjścia i kolejkuje podejście; E z bliska zachowane, wspólny hover outline. Opuszczenie wyprawy otwiera istniejący ItemActionPopup z pytaniem „Opuścić loch?”, anulowaniem i ponowną walidacją po zatwierdzeniu. Zmiana referencji wyłącznie PF_DungeonExitView przez Editor API. Potencjalne stare TMP etykiety wyjścia są wyłączane przy każdej inicjalizacji; prefab nie ma debugowego napisu.

Nowe generacje z profilem długości rezerwują jeden encounter w każdym pokoju Final w OBECNYM budżecie walk, zamiast dodawać dodatkową walkę. Na ostatnim piętrze bossa rolę tę zachowuje istniejący boss. Wyjście/zstąpienie blokowane do oznaczenia encounterów tego pokoju jako ukończone; powrót na poprzednie piętro bez nowej blokady. Stare pełne snapshoty bez takiej walki pozostają możliwe do zakończenia, bez dopisywania przeciwników lub migracji schematu.

264/264 PASS (148.91 s), po 61/61 testach celowanych; następnie 9/9 po końcowym hardeningu legacy labels i wyjątku asset-audit. Zakres: 48 generacji / sześć wariantów, final guard i dotychczasowe limity liczby walk, E/LPM/outline, anulowanie i ponowna walidacja, legacy labels, oba rendery produkcyjne, bramy/pętle/save/legacy/bossowie, stress i nawigacja. Początkowo dwie regresje NavMesh wskazały zanieczyszczenie EditMode (226 NavMeshData); usunięto 225 osieroconych DontSave danych bez żywego właściciela, a test narożników sprząta także po failure. Runtime nawigacji bez zmian; izolowany retest 9/9, narożniki x16, potem pełny retest PASS.

924 ScriptableObjects bez zmian, jeden prefab z jedną referencją przez API, nowe PNG/metadane przez importer. Self-review i rendery PASS. Ręczny setup niepotrzebny; bez Computer Use, drugiego Unity, commit/push. Gameplay/full clear/FPS NOT RUN. Smoke: NOWA wyprawa dla gwarantowanej walki; brama na granicy zamiast koron, hover/LPM/E, Anuluj/potwierdzenie, brak debugowego napisu w katakumbach. Dowody: ../LocalHandoffs/dungeon-exit-20261023/README.md.

## Katakumby — wejścia poza środkiem ścian — 2026-10-23 — READY_FOR_SMOKE

Wczytane jawne pasma wejściowe w 16 niearenowych szablonach Katakumb oraz wspólne wyrównywanie/kompaktowanie według rzeczywistych progów. Każdy szablon ma wejście poza środkiem; dziewięć ma przesunięte przeciwległe wejścia. Część centralnych otworów i areny pozostają. Bez zmian obrysów, mebli, celów, szerokości drzwi, grafów, pętli/odnóg/bram, budżetów walk/EXP/lootów lub schematu save. Las zachowuje dotychczasowy dryf/pakowanie.

204/204 PASS (210.19 s): podejścia wolne we wszystkich obrotach, kompaktowość, bramy/gate-cut, save/legacy/JSON, 144 stress wyprawy, native render i NavMesh. 48 par ma identyczny graf/role/liczby pokoi i walk; 24 pełne snapshoty Lasu identyczne. W Katakumbach 64,8% końców połączeń poza środkiem (wcześniej 0%), łącznik średnio 2,1–2,4 kratki, maks. 12; powierzchnia -0,7–1,4%, brak rozrostu pustych dojść.

16 assetów DRT_catacombs_* przez API Unity; inne 908 ScriptableObjects bez zmian. Rendery/mapy obejrzane, self-review zakończony. Bez Computer Use, drugiego Unity, scen/prefabów, commit/push; gameplay/full clear/FPS NOT RUN. Ręczny setup niepotrzebny; smoke na NOWEJ wyprawie Katakumb (M, progi, bramy, save/load). Stare pełne snapshoty zachowane. Dowody: ../LocalHandoffs/catacombs-ports-20261023/README.md.

## Las — wydłużone obrysy, boczne wejścia i pakowanie łańcuchów — 2026-10-23 — READY_FOR_SMOKE

Wczytane 15 leśnych obrysów z łukami, L/S, zatokami i autorskimi wejściami poza środkiem. 10/15 niearenowych szablonów ma proporcje >=1,6:1; szerszy zagajnik/obozowiska i arena pozostają. Osadzanie wyrównuje faktyczne porty zamiast środków i kompaktuje łańcuchy względem lokalnych sąsiadów, z dolnymi/górnymi ograniczeniami odstępu i kontrolą kolizji. Zapisane ślady gruntu łączą środki pasm wejściowych, bez wymuszonego huba w centrum. Schema save v5 bez zmian.

203/203 testów PASS (183,74 s), w tym proporcje/porty, istniejące limity kompaktowości oraz maksimum łącznika 16, 144 stress wyprawy, bramy, save/legacy/JSON, obroty wyposażenia, native render/NavMesh. 48 par map zachowuje grafy/role/liczbę pokoi; wszystkie 24 snapshoty Katakumb identyczne. Budżety walk bez zmian; retry osadzania może przelosować dobór/liczbę w dotychczasowym zakresie (w próbie Short/Normal suma walk bez zmiany, Long 121→123). Względem ostatniej kompaktowej wersji Las ma 5–10% więcej pól i 19–23% dłuższą najkrótszą drogę, średni łącznik nadal około 3 kratek, 20–24% połączeń bez łącznika. Nie jest to pomiar czasu clearu.

Scope: 15 assetów DRT_forest_* przez API Unity; inne ScriptableObjects i katakumbowe blueprinty bez zmian. Natywne rendery i rzeczywiste mapy ze ścieżkami obejrzane. Ręczny setup niepotrzebny. Smoke na NOWEJ wyprawie Lasu: kręte wnętrza, boczne wejścia, czytelność pod koronami, bramy/save-load. Stare pełne mapy nie są regenerowane. Bez Computer Use, drugiego Unity, scen/prefabów, commit/push; gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/forest-organic-20261023/README.md.

## Las/Katakumby — zwarte sale, rozrzut i pomijanie łączników — 2026-10-23 — READY_FOR_SMOKE

Wczytane: losowane odstępy z możliwością bezpośredniego styku wyrównanych wejść, proste łączniki krótsze niż ich szerokość i mniejszy boczny dryf z miejscem na prawidłowy zakręt. Nie pomijamy krawędzi grafu ani bram. Zmniejszono 15 leśnych i 14 katakumbowych szablonów, zachowując ich funkcje, różnorodność selektora i rozmiary aren finałowych/bossów; wyposażenie/cele przeniesione do mniejszych wnętrz. RNG geometrii odseparowane od doboru szablonów. Brak zmian encounter placementu i budżetów walk/pokoi/odnóg/pętli/EXP/lootów, brak nowego schematu save. Istniejące pełne mapy zachowują poprzedni układ.

Porównanie 48 par (8 seedów × dwa biomy × trzy długości): identyczne grafy, szablony/role i liczby walk; powierzchnia Lasu -40,9–43,9%, Katakumb -22,9–25,0%; najkrótsza droga do wyjścia odpowiednio -39–41% i -20–22%. Łączniki średnio około 3 kratek (90. percentyl 5–6); 7–10% połączeń bez łącznika, w tym 12 z bramą w całej próbie. Pomiary geometrii z otwartymi bramami, bez rekwizytów, nie gameplay/full clear.

202/202 bounded regresji PASS (152,81 s), następnie celowane limity kompaktowości sześciu wariantów; fizyczne gate-cuty, save/legacy/JSON, role odnóg/pętli, 144 stress wyprawy, native render/NavMesh i wyposażenie we wszystkich obrotach. 29 istniejących assetów ProductionRooms zmienionych przez API Unity; inne ScriptableObjects bez zmian. Natywne rendery i obrazy faktycznych map obejrzane. Brak scen/prefabów, Computer Use, drugiego Unity, commit/push. Brak ręcznego setupu; smoke na NOWYCH wyprawach obu biomów, bezpośrednich przejściach/bramach i save/load. Dowody: ../LocalHandoffs/spatial-density-20261023/README.md.

## Las/Katakumby — produkcyjny authoring pokoi i łączników — 2026-10-23 — READY_FOR_SMOKE

Wczytane: 16 szablonów Lasu, 17 Katakumb i dwa warianty aren bossów. Nowe wyprawy używają jawnych ProductionTemplates; poprzednie pokoje są poza tą pulą, lecz pozostają do snapshotów i fixture'ów. Czyste obrysy, konkretne porty, prawdziwe łączniki bez szyjek i przesunięcia Tetris; dobór po pełnym grafie tras, zagęszczanie geometrii według rozmiarów sal. Las ma nieregularne polany/przesieki, niewielkie boczne przesunięcia i grunt łączący wejścia/cele bez centrum-gwiazdy. Katakumby mają skalę krypt/galerii/sal, korytarze proste/L/T, funkcjonalne wyposażenie i preferencje pięter. Budżety pokoi/odnóg/pętli/walk/EXP/obozów oraz zasady bram zachowane. Łączniki są geometrią, nie dodatkowymi liczonymi pokojami.

Snapshot v5 przechowuje łączniki i ślady gruntu; odczyt v1–4 zachowany, stare układy nie są regenerowane. Wspólne lookupy rozróżniają pulę generacji i wszystkie historyczne referencje. Kotwy posiłków/przejścia i dekoracje aren nie liczą łącznika jako wnętrza sali. Weryfikacja ujawniła i naprawiła zależność voxelizacji NavMesh od dalekiego Y, wyciek NavMesh z fixture'ów EditMode, dynamiczny ogień Enta widoczny poza wiedzą fog oraz duplikaty ID skrzyń galerii audytowej.

195/195 testów PASS, 0 failure/skip (165,25 s), z nowymi 48 wyprawami, 144 stress wyprawami, obrotami wszystkich szablonów, fizycznymi gate-cutami, save/JSON/legacy, bossami i replay natywnej prezentacji/nawigacji. Test ruchu po narożnikach powtarzany 16 razy. Natywne rendery i schematy obu biomów/wszystkich długości/pięter obejrzane. Scope: 35 nowych assetów pokojów i dziewięć istniejących katalogów przez API Unity (cztery aktywne, pięć Tactical tylko domyślne puste pola). Bez zapisanych scen/prefabów, Computer Use, drugiego Unity, commit/push. Self-review zakończony; ręczny setup niepotrzebny. Gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/biome-room-authoring-20261023/README.md.

Smoke: rozpocząć nowe wyprawy obu biomów; sprawdzić wejścia, M, pętle/odnogi, bramy i zapis/wczytanie. Istniejące wyprawy pozostają niezmienione.

## Wyprawy — liczba odnóg zależna od długości — 2026-10-23 — READY_FOR_SMOKE

Wczytane dla sześciu ofert Lasu/Katakumb: Short 1–3, Normal 2–4, Long 3–5 odnóg na CAŁĄ wyprawę, nie na piętro. Pula Katakumb nadal dzielona między piętra z minimum jedną odnogą progresji na piętro; nowe minima 1/2/3 pasują do ich liczby pięter. Bez zmiany długości dojść (2–3 / 2–5 / 3–8 plus cel), zakresów ramion pętli, bazowych pul pokoi, walk/EXP/obozów/questów ani zasad bram. Zmiana tylko dwóch pól branch-count w profilach Short i Long sześciu definicji przez istniejący authoring; Normal bez zmiany. Pozostałe 883 assety identyczne. Snapshot/schema bez zmian; pełne stare mapy odtwarzane bez regeneracji i bez walidacji ich liczby odnóg wobec nowych limitów.

155/155 testów PASS, zero failure/skip. Nowe 384 plany sprawdzają oba krańce zakresów oraz bramy na wszystkich piętrach; dotychczasowe testy sprawdzają sześć ofert ×trzy długości ×dwa seedy, 144 dodatkowe stress wyprawy, dokładny graf, geometrie pętli, role, save/legacy/JSON, rewards i natywny replay rendera/maski/nawigacji. Historyczna regresja czterech odnóg ma przypięty pierwotny profil na klonie testowym; produkcyjne 1–3/2–4/3–5 są badane oddzielnie. Self-review/scoped diff zakończone. Brak ręcznego setupu/Computer Use/scen/prefabów/meta/commit/push. Gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/branch-counts-20261023/README.md.

Smoke: NOWE wyprawy każdej długości w obu biomach, M i liczba bocznych celów łącznie na wszystkich piętrach; pełna odnoga i brama przed/po spełnieniu warunku, save/load. Stara aktywna mapa zachowuje swój układ.

## Poprzedni etap: wyprawy — nieregularne pętle i nierówne ramiona — 2026-10-23 — READY_FOR_SMOKE

Wczytane. Graf wszystkich pięter nadal powstaje przed geometrią. Ramiona dobierane osobno, preferowane nierówne: Short 2–4, Normal 3–5, Long 4–7 pokojów wewnętrznych. Krótki max wzrósł 3→4, aby umożliwić różne ramiona na ortogonalnej siatce. Zamknięte przebiegi mają wcięcia/uskoki, minimum 6 zakrętów dla 8/10 pokoi i 8 dla większych pętli, z zachowaną przestrzenią w środku. Minimalne 6-pokojowe cykle mogą być wyłącznie prostokątne. Zewnętrzne połączenia nie mogą być kierowane w zamkniętą kieszeń pętli. Długie odnogi korzystają z ograniczonego lokalnego backtrackingu; nie zmienia to planu ani zasad bram. Bramy nadal są mostami między sekcjami, cele dostępne przed własną bramą. Budżety pokojów/walk/EXP/obozów/questów i role v4 zachowane; stare mapy nie są regenerowane.

149/149 testów PASS, zero failure/skip. 384 plany +384 kształty, realizacja dokładnego grafu wszystkich pięter, 144 dodatkowe stress wyprawy, sześć ofert ×trzy długości ×dwa seedy, regresja Las Long seed 231035/cztery odnogi, save/legacy/JSON/rewards oraz natywny replay rendera/maski/nawigacji. Obrazki rzeczywistych pól/ścian obu biomów i trzech długości obejrzane. Tylko sześć assetów zmienionych przez istniejący authoring: Short max ramienia 3→4; pozostałe 883 identyczne. Self-review i kontrola nowych linii/diffu zakończone. Brak ręcznego setupu, scen/prefabów/meta, Computer Use, commit/push. Gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/irregular-loops-20261023/README.md.

Smoke: NOWA wyprawa w Lesie i Katakumbach, M, oba ramiona F→J, pełne dojście do celu i wspólna brama przed/po spełnieniu warunku; save/load oraz kolejne piętra. Stara aktywna mapa zachowuje poprzedni układ.

## Poprzedni etap: wyprawy — graf całej wyprawy przed geometrią — 2026-10-23 — READY_FOR_SMOKE

DungeonRoutePlanner planuje wszystkie piętra przed wyborem szablonów i położeniem pierwszego pokoju: budżety, sekcje progresji, pełne oba ramiona pętli, odnogi i krawędzie bram. DungeonGeneratorService osadza ten sam niezmienny plan przy kolejnych próbach geometrii. Nie szuka już okazjonalnych skrótów w gotowej produkcyjnej mapie i nie przelosowuje późniejszych pięter po nieudanej próbie pierwszego. Legacy/demo bez profilu zachowują dawną ścieżkę.

Ramiona (bez wspólnych F/J): krótka 2–3, normalna 3–5, długa 4–7; całe cykle 6–8/8–12/10–16 pokoi, maksimum ograniczane dostępną pulą podczas planowania, nigdy po fakcie. Długi Las ma dwie pętle, pozostałe warianty jedną na piętro. Bramy są mostami między sekcjami, wymagane cele dostępne przed własną bramą. Zachowane 2–4 odnogi na całą wyprawę, ich długości, bazowe pule pokojów, budżety walk/EXP/obozów/questów i pełne snapshoty v1–4. Bez zmiany schematu zapisu.

142/142 testy PASS, zero failure/skip: 384 plany, dokładna realizacja stałego planu z różnymi próbami geometrii, porównanie wszystkich pięter produkcyjnej generacji z uprzednio zaplanowaną całą wyprawą, sześć definicji ×trzy długości ×dwa seedy, 36 dodatkowych stress, bramy/role/cykle/JSON/legacy/rewards, natywne wejście/render/maska/nawigacja powtórzone dwukrotnie. Diagramy rzeczywistych pól/ścian obu biomów i trzech długości obejrzane. SHA 889 assetów: zmiana tylko dwóch nowych pól arm-length w trzech profilach sześciu definicji; pozostałe 883 identyczne. Self-review/scoped diff-check zakończone. Gameplay/full clear/FPS NOT RUN. Bez ręcznego setupu/Refresh/Computer Use/commit/push. Dowody: ../LocalHandoffs/graph-first-generator-20261023/README.md.

## Poprzedni etap: dodatkowe odnogi i alternatywne trasy — 2026-10-23 — READY_FOR_SMOKE

2–4 cele odnog na całą wyprawę, rozdzielane między piętra Katakumb; pokoje dojścia ponad dotychczasową pulę: krótka 2–3, normalna 2–5, długa 3–8 przed osobnym celem. Każda odnoga ma własne skrzyżowanie i pełne dojście. Każde piętro ma dodatkowe połączenie odcinków głównej drogi, bez omijania bram lub skracania dojścia do celu. Generator nie oddaje po cichu liniowej mapy bez pętli. Walka nadal ma dotychczasowy budżet, liczony od bazowej puli bez pokoi dojścia; bez zmian EXP/obozów/questów. Zapis pełnego układu v4 zachowuje role pokojów i czyta v1–3; stare mapy nie są przebudowywane.

126 odrębnych aktywnych testów PASS: sześć definicji ×trzy długości ×dwa seedy, dodatkowe 36 generacji stress, geometria/progresja, limity walk/boss, deterministyczny seed, save/load i legacy, natywne wejście/render/maska/nawigacja powtórzone po dwa razy. Osiem przestarzałych porównań optymalizacyjnych do historycznej zawartości zastąpiono replay obecnej zawartości; nie akceptowano ich dawnych snapshotów jako wymagań nowej topologii. 889 assetów sprawdzone SHA: tylko sześć definicji i tylko cztery nowe pola profili, reszta 883 identyczna. Diagramy rzeczywistych pól/ścian obu motywów obejrzane. Self-review/scoped diff-check zakończone. Bez manualnego setupu/Refresh/Computer Use/commit/push. Gameplay full clear i FPS NOT RUN. Dowody: ../LocalHandoffs/expedition-branches-20261023/README.md.

## Poprzedni etap: połowa pokojów i ×2 EXP zwykłych wrogów — 2026-10-23 — READY_FOR_SMOKE

Na zaakceptowane polecenie wdrożono 14–17/28–34/42–51 pokojów łącznie zamiast 28–34/56–68/84–102. Las nadal jedno piętro, walki 5–6/9–11/14–17; Katakumby 1/2/3 piętra, walki 5–6/10–12/15–18. Liczby walk są połową dawnych zakresów z zaokrągleniem w górę na piętro i istniejącą gęstością 30–40%; boss wliczony raz. Zmieniono przez Editor API wyłącznie cztery bounds w trzech profilach sześciu produkcyjnych DungeonDefinition. Mniej łupów i mniejsze zużycie zasobów są zamierzone, bez kompensacji dropu/złota/obozów/questów.

CombatRewardSystem podwaja EXP zwykłych przeciwników po obliczeniu starej nagrody i kary poziomu. Boss zachowuje ×1, zwykła obstawa ×2. Bazowa krzywa oraz nagrody questów bez zmian. Rola bossa w runtime reward jest wyliczana z istniejącej definicji, zachowana przez namespaced kopie i odtwarzana po save/cache; brak nowego pola w DTO. Legacy fixed-XP zachowuje zapisany amount. Aktualny feedback pokazuje +120 zamiast +60 XP w przykładzie L1. Istniejące pełne snapshoty map zachowują dawną geometrię; zmniejszenie dotyczy nowej generacji.

179 odrębnych ostatnich wyników PASS: 56 focused +124 regresje z jednym wspólnym przypadkiem. 36 generacji (6 definicji ×3 długości ×2 seedy), gęstość, bramy/obiekty, boss/obstawa, loot/gold, questy, realne przyznanie raz i UI feedback, merge/outcome/factory/save. Stary większy las 56–68 pokoi przechodzi JSON restore z identycznymi zbiorami pól i ścian. Testową quadratic asercję porównania zbiorów zastąpiono HashSet.SetEquals (102→1.03 s całego testu). 6 zmienionych assetów, reszta ScriptableObjects bajtowo identyczna. Self-review/scoped code diff-check zakończone. Import automatyczny w istniejącym Unity, bez Computer Use/ręcznego Refresh/scen/prefabów/commit/push. Dowody: ../LocalHandoffs/expedition-pacing-20261023/README.md. Gameplay/full clear/FPS NOT RUN; aktualne zakresy i historyczne analizy rozdzielono w BALANCE_EXPEDITION_LENGTHS.md.

## Las — niewidoczne cienkie przegrody i odczyt seeda — 2026-10-22 — READY_FOR_SMOKE; MIGOTANIE OTWARTE

Odtworzono brak grafiki solidnej ściany między dwiema przechodnimi kratkami: las wyłączał mur, ale sadzenie drzew wymagało nieprzechodniej komórki. Przed zmianą dwa testy pion/poziom FAIL. Takie krawędzie mają teraz natywne małe drzewa osadzone na istniejącej granicy, z własnym chronionym pasem korzeni, głębią i istniejącym wygaszaniem koron. Nie dodano losowych drzew na podłodze ani nowych colliderów, nie połączono tras. Cienkie pasy pustych komórek nadal wypełniają małe drzewa poza podłogą. Bramy progresji i stare przechodnie markery TreeTrunk zachowują osobną obsługę.

Na kolejne polecenie użytkownika seed bez argumentu pokazuje rzeczywiste DungeonInstance.Seed, ID, długość/poziom wyprawy, piętro i ostatnią pozycję drużyny. seed <liczba> nadal ustawia seed debugowy nowego generowania; naprawiono wcześniejsze odrzucanie zera i wartości ujemnych przez pozytywny typ Integer parsera, używając istniejącego typu signed CoordinateInteger tylko dla tej komendy. Odczyt nie zmienia RNG, debug override ani wyprawy. Help/autouzupełnianie aktualne. Zapis diagnostyczny zawiera odtąd seed i parametry wyprawy oraz wybiera aktywne oświetlenie lasu zamiast pierwszego komponentu innej lokacji.

181 odrębnych testów PASS według ostatniego wyniku każdego przypadku: 6 przegrody/stacjonarne maski, 98 korony/cienie/fog, 77 konsola. W starszej regresji skorygowano nieaktualne oczekiwanie kontaktu floor+1 na istniejące floor+2 (mgła zajmuje +1, propsy +3/4). Obejrzano render przed/po dla obu orientacji i wąskiego pasa void. Brak serialized zmian, setupu Editora, Computer Use, ręcznego Refresh, commit/push.

Migotanie NIE JEST oznaczone jako naprawione: kontrolowane 30 stacjonarnych ticków na produkcyjnym lesie zachowało identyczne pełne maski, a 93 klatki z gry użytkownika obejmują ruch, zmianę zoomu i overlay walki bez potwierdzonego problematycznego miejsca. Użytkownik nie zna seeda poprzedniego odtworzenia. Centralne próbkowanie masek w zapisie live nie dowodzi stabilności całego obrazu. Do dalszej diagnozy potrzebne odtworzenie miejsca z seedem/parametrami/pozycją. Dowody: ../LocalHandoffs/forest-thin-dividers-shadow-stability-20261022/README.md.

## Eksploracja — podgląd odkrytej mapy pod M — 2026-10-22 — READY_FOR_SMOKE

Na nowe polecenie użytkownika rozszerzono dotychczasową minimapę o duży podgląd podczas eksploracji lochów. M otwiera; M, Esc lub nagłówek X zamyka. Automatyczny kadr obejmuje wyłącznie odkryte/obecnie widoczne pola aktualnie oglądanego poziomu, z zachowaniem proporcji. Niezbadane odnogi i inne wysokości nie powiększają kadru. Pozostają dotychczasowe zasady pamięci obiektów, ukrywania przeciwników i niewidzialności. Debug togglefog zachowuje jawny wyjątek. Podgląd zamyka się przy przejściu do walki, utracie sesji lub dezaktywacji widoku.

Wykorzystuje istniejący prefab minimapy i runtime layout, bez serialized zmian ani dodatkowego setupu. Przy zamknięciu przywraca rodzica, kolejność, geometrię oraz stan zwinięcia. Blokuje ruch, interakcje i sterowanie kamerą; klatka zamknięcia nie przepuszcza wejścia do świata ani okna w tle. M jest ignorowane podczas pisania, konsoli i menu kontekstowego. Podgląd nie pauzuje czasu gry.

Cache tekstury do 1024 px, brak ponownego rysowania/uploadu przy niezmienionym stanie. Kontrolowany pomiar EditMode, 4491 znanych pól: otwarcie z layoutem i pierwszym uploadem 13.702 ms, zmiana widoczności mediana 6.191 ms / max 6.464 ms. To koszt wywołania testowego, nie całej klatki w Play Mode. 163/163 odrębne testy PASS; produkcyjny prefab wyrenderowany i obejrzany przed/podczas/po. Self-review i scoped code diff-check zakończone. Import automatyczny, bez Computer Use, ręcznego Refresh, commit/push. Dowody: ../LocalHandoffs/map-overview-20261022/README.md. Pozostaje smoke użytkownika na jego ekspedycji.

## Nawigacja — koszt przygotowania po zmianie bramy — 2026-10-22 — READY_FOR_SMOKE

Zastąpiono ponowne skanowanie całej podłogi i hierarchii colliderów cache'em stałych źródeł współdzielonym przez promienie agentów na danym poziomie oraz indeksem i numerycznym snapshotem colliderów terenu. StaticGeometryRevision zmienia się tylko dla powierzchni/ścian, niezależnie od stanów drzwi i bram. Dynamiczne krawędzie pochodzą z jawnych zbiorów zamkniętych drzwi/bram. Zewnętrzne propsy przechowują referencje do colliderów i odczytują bieżące bounds; nieudana trasa zachowuje pełne odświeżenie fizyczne. Dekoracje nie invalidują geometrii. Cache ma reset przy zmianie piętra/mapy/projekcji/roota, a lokalne ściany po bramie aktualizują własne entries. Bez zmian save schema, serialized setupu, geometrii kolizji i reguł ruchu.

Ta sama produkcyjna ekspedycja Long/seed 171026: przygotowanie las 77.06→6.12–8.01 ms, katakumby 36.19→2.77–3.23 ms. Brama: 0 ponownych skanów sceny, 0 przebudów stałych źródeł, odczyty nowych collider bounds 0 las/8 katakumby. Są to osobne wywołania przygotowania, nie cała klatka/FPS; początkowy cold setup nadal w ładowaniu. 214 odrębnych testów PASS, 5 dodatkowych jawnych audytów/capture skipped. Sprawdzono cache invalidation, przesuwanie/wyłączanie/nowe propsy, bramy/drzwi, native/fallback pathfinding, floor/elevation/projekcję/map replacement, fog i native wall render; pixel parity bramy 0. Self-review/scoped code diff-check zakończone. Bez Computer Use, ręcznego Refresh, commit/push. Dowody: ../LocalHandoffs/navigation-preparation-20261022/README.md.

## Eksploracja — zatrzymanie po dźwigni/warunku — 2026-10-22 — READY_FOR_SMOKE

Usunięto pełny Rebuild widoku mapy po normalnej zmianie bramy progresji. Las przełącza istniejące bariery i collidery; katakumby aktualizują bramę oraz sąsiednie narożniki, zachowując pozostałe renderery i pasma głębokości. Pamięć bram/LOS zachowane. Nietypowa zmiana grupowania nowej lub częściowej bariery leśnej nadal wymaga pełnego setupu. Dodatkowo przebudowa NavMesh po zmianie topologii lub fizycznych przeszkód odbywa się w tle, z anulowaniem przy zmianie mapy i bez używania starej siatki przez zamkniętą bramę. Początkowy bake pozostaje częścią ładowania piętra.

Kontrolowany pomiar Long/seed 171026, aktywacja warunku: las 2437.33→3.42 ms, katakumby 1639.43→30.32 ms. Zachowano 13978/13978 i 7807/7807 dalekich rendererów. Osobno następne ApplyFog: 17.32/20.90 ms; zlecenie nawigacji: 76.79/24.67 ms — przygotowanie geometrii nadal może powodować krótszy spike, brak gwarancji stałego frame time. Render lokalny zgodny pikselowo z pełną przebudową i po zamknięciu/otwarciu. 148 testów PASS, 5 jawnie pominiętych dodatkowych audytów/capture; brak failure. Self-review i scoped diff-check zakończone. Bez serialized zmian/setupu, Computer Use, ręcznego Refresh, commit/push. Dowody: ../LocalHandoffs/progression-interaction-stall-20261022/README.md. Pozostaje przejazd w Play Mode na mapie użytkownika.

## Las — regresja kosztu mgły na mocno odkrytej mapie — 2026-10-22 — READY_FOR_SMOKE

Potwierdzono wzrost kosztu nowego filtru ForestKnowledge: każda zmiana widoczności ponownie malowała margines wokół całej historii odkrycia. Dodano cache pamięci, osobną lokalną warstwę widoczności, gotowe wagi/maskę gruntu i animowanie tylko aktywnych pikseli. Pamięć malowana jest dla nowych źródeł; skan starego zbioru potrzebny tylko przy wzroście liczby odkrytych komórek. Reset po debug/map/floor/geometry/depth/teardown. Bez zmian wyglądu, transmisji .35, głębokości lasu, LOS, odkrywania, shadera i save schema.

Kontrolowany pomiar samego filtru w EditMode: mediana 128 odkrytych kratek 7.30→1.28 ms, 1024: 33.96→1.28 ms, 6144: 180.96→1.25 ms (maksimum 209.44→1.41 ms). To nie pomiar całej klatki/FPS. 57 odrębnych testów PASS: niezależny poprzedni algorytm zgodny dla 50 625 pikseli/25 stanów, przejściowa alfa, licznik pracy po rozgrzaniu, behind-tree/deep-woods, produkcyjne GPU renderingi dla 2 seedów, jednolita transmisja oraz dotychczasowe fog/transition testy. Aktualne rendery obejrzane. Import/self-review/scoped diff-check zakończone; bez serialized setupu, Computer Use, ręcznego Refresh, commit/push. Pozostaje powtórzenie przejazdu użytkownika w Play Mode. Dowody i ograniczenia pomiaru: ../LocalHandoffs/forest-fog-spikes-20261022/README.md.

## Konsola — noaggro do testów eksploracji — 2026-10-22 — READY_FOR_SMOKE

Dodano noaggro [on|off]: brak argumentu przełącza, on blokuje automatyczne aggro, off przywraca normalne zachowanie. Stan należy do bieżącej GameSession, działa między lokacjami i nie wchodzi do save DTO. Nowa sesja domyślnie ma normalne aggro. Komenda jest w help i autouzupełnianiu; błędne argumenty nie zmieniają stanu.

W lochach noaggro pomija wykrywanie rozpoczynające/dołączające walki, zeruje narastającą świadomość wolnych wrogów i ukrywa jej wskaźniki. Zatrzymuje alarmy, dołączanie oraz oczekujące scalenia; zwalnia ich MembershipChangePending. Patrole nie zatrzymują się, aby obserwować drużynę. Geometria LOS/FOV, mgła, trasy patroli, trwające walki i ręczne rozpoczęcie walki pozostają aktywne. To narzędzie testowe, nie optymalizacja normalnej eksploracji.

88/88 testów EditMode PASS: konsola/schema/sugestie i integracja wykrywania 74, patrole/drzwi/persistence patroli 14. Nowe testy pokrywają toggle/on/off, idempotencję, brak sesji, błędne argumenty, help/sugestie, wyczyszczenie wykrycia, wznowienie zapytań po off i usunięcie oczekujących alarmów/scalenia bez zakończenia walk. Import automatyczny i self-review/scoped diff-check zakończone. Bez serialized setupu, Computer Use, ręcznego Refresh i commit/push. Dowody: ../LocalHandoffs/noaggro-command-20261022/.

## Furia — tura przerwy i ochrona podczas CC — 2026-10-22 — READY_FOR_SMOKE

Pierwsza pełna własna tura bez ofensywy jest bezpłatna; na końcu drugiej i kolejnych ubywa 10 furii. Użycie skilla ofensywnego resetuje licznik, również przy pudle, pełnym pasku i blokadzie generacji. Sam pasywny przyrost go nie resetuje. Ogłuszenie, zamrożenie, paraliż i sen zatrzymują licznik bez resetu. Unieruchomienie chroni tylko wtedy, gdy nie było legalnej ofensywy na żywego przeciwnika; sprawdzane są wspólne reguły targetowania, koszt/AP, LOS i blokady skilla. Okazja dostępna przed wydaniem AP zostaje zapamiętana. Sam dystans bez CC nie chroni. Poza walką nadal -10 co 6 sekund.

Licznik jest lokalny dla BattleUnit.WarriorState, bez zmiany save schema. Rozliczenie następuje przed końcowoturnowym wygasaniem statusów. Zaktualizowano produkcyjny oraz fallback glosariusza i opis GameDefine, zachowując ID/wartość. Import automatyczny zakończony. 179/179 testów EditMode PASS: zasoby, eksploracja, glosariusz, timeline, produkcyjny wojownik i statusy. Nowe regresje obejmują zabicie celu + podejście, CC i jego końcowe wygaśnięcie, root z atakiem bliskim/dalekim, LOS/koszt/niewidzialność/mobilność, zapamiętaną okazję, cap/blokadę generacji i pasywny przyrost. Self-review zakończony; brak ręcznego setupu Editora, Computer Use i commit/push. Dowody: ../LocalHandoffs/rage-grace-cc-20261022/.

## Las — ograniczenie presji dystansowych encounterów — 2026-10-22 — READY_FOR_SMOKE

Na prośbę użytkownika zmieniono dwa produkcyjne składy: Elfi zwiadowcy z 3 łuczników na kłusownika + druida + łucznika; Bandycki patrol z 2 rzezimieszków + 2 strzelców na 3 rzezimieszków + 1 strzelca. Zaktualizowano odpowiednie dwa assety EncountersBalanced i źródło reauthoringu EncounterBalanceSetup.ForestRosters. Druid otrzymał też zgodne z jego definicją ClassData, zamiast pozostawionej klasy łucznika. Zachowano liczebność, ID slotów i układ spawnów; wszystkie trzy pule dostępnych wypraw korzystają z tych samych poprawionych definicji. Pozostałe encountery i statystyki bez zmian.

Import poza Play Mode zakończony. 2/2 celowanych testów PASS: konfiguracja produkcyjnych pul oraz integracyjny test obu składów, poprawnych klas (w tym many druida), reauthoringu na kopii i generacji z legalnymi, odrębnymi polami spawnów. Nie uruchamiano pełnego ApplyAll ani szerokiego reauthoringu contentu. Self-review zakończony, bez ręcznego setupu Unity, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-roster-pressure-20261022/.

Dyskusja furii: bieżąca reguła odejmuje 10 na początku kolejnej tury, jeżeli od poprzedniego startu nie zaznaczono generacji furii; wykonywana jest przed obsługą pominięcia tury przez twarde CC. Nie ma wyjątku dla ogłuszenia/zamrożenia/paraliżu/snu. Rekomendacja do decyzji użytkownika: pełna tura odebrana przez takie CC powinna wstrzymywać decay; efekty pozostawiające możliwość ataku nie powinny dostawać wyjątku. Nie wdrożono zmiany reguł zasobu.
## Las — usunięcie pni z przechodniego obrzeża i łagodniejsza przezroczystość — 2026-10-22 — READY_FOR_SMOKE

Po kolejnym smoke użytkownik wskazał trzy pnie wewnątrz polany i zbyt zanikające dolne korony. Pominiętym źródłem były markery TreeTrunk tworzone niezależnie przez DungeonFloorMapBuilder.AddVegetationFringe, na krawędziach pomiędzy dwiema przechodnimi kratkami. Usunięto ich generowanie, pozostawiając te same przechodnie pola obrzeża. Widok pomija takie wewnętrzne markery w starszych układach: nie tworzy grafiki ani kolizji, a wyłączona ceglana ściana prefabu nie wraca. Usunięto nieużywaną funkcję BuildIndividualTrunk. Zapis nadal rozpoznaje dotychczasowy enum i format; nie ma migracji/usuwania danych ani zmiany seedów/zajętości podłogi. Zewnętrzne drzewa graniczne zachowują rezerwacje poza przechodnią mapą.

Łagodna alfa koron polan/przejść podniesiona z 130/255 do 160/255. Aktorzy zachowują 90/255, kontur kontaktu, chronione korzenie i czasy fade. Bieżący import zakończony. 53/53 testów logiki i renderu PASS, następnie 2/2 testów renderu/kontaktu PASS po dodaniu kontrolowanego porównania alf (54 unikalne przypadki). Regresje sprawdzają brak nowych markerów na trzech seedach, brak grafiki i fizycznych przeszkód w układzie ze starym markerem, dostępność kieszeni przez native navigation, wszystkie zarejestrowane dojrzałe korony na zewnątrz przechodniej mapy oraz wcześniejsze GPU/LOS/kontury i priorytety fade.

Obejrzano polanę i blind_glade z prawdziwą drużyną i normalnym LOS, bez RevealMap; dodatkowo kontrolowane pary alfa 130/160 na identycznej scenie po usunięciu wewnętrznych drzew. Korony przy 160 są pełniejsze. Starsze obrazy previous-alpha-130 zawierają również stare pnie i są wyłącznie historycznym porównaniem. Self-review zakończony, brak ręcznej konfiguracji Editora, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-interior-tree-removal-20261022/.
## Las — korekta niewidocznego wygaszania i pustego pasa za drzewami — 2026-10-22 — READY_FOR_SMOKE

Poprzedni smoke odrzucony przez użytkownika: efekt koron niewidoczny, za pierwszym rzędem nadal widać pusty grunt. Zapytania przejść i dolnej krawędzi sprawdzają teraz przecięcie rzeczywistego konturu liści z powierzchnią widocznej kratki (98% jej wymiarów), zamiast wyłącznie środka kratki. Chronione korzenie są wycinane z tego zapytania. Nie zwiększono zasięgu kontaktu bohaterów. Łagodna alfa przejść/polan zmieniona z 175/255 na 130/255; mocny kontakt aktorów nadal 90/255, czasy przejścia zachowane.

Wypełniające małe drzewa są generowane w wolnych miejscach również na dalszych głębokościach, zamiast kończyć się na drugim rzędzie. Zachowano rezerwacje poza przechodnią mapą, spacing i natywną skalę. Ograniczono dodatkowe przyciemnienie RGB głębokich drzew do łagodnego zakresu 65–100%; zanikanie lasu w mroku nadal zapewnia wspólny filtr mgły. To zapobiega znikaniu sylwetek drzew przy nadal widocznym ciągłym podłożu. Bez zmiany ambientu, palety, LOS i odkrywania.

58/58 celowanych testów PASS. Nowe regresje: liście tylko przy brzegu kratki, wypełnienie głębi po czterech stronach i brak podwójnego wygaszania RGB. Zachowane native-contour/GPU/actor-priority/zero-allocation/LOS testy. Uaktualniono stary test, który mylił ciągły backdrop z lokalnym patchem podłoża. Obejrzano A/B wygaszania long_glade i blind_glade z prawdziwą drużyną i normalnym LOS, bez RevealMap; różnica odpowiednio ponad 116 tys. i 249 tys. pikseli. Obejrzano też normalny filtr z czterech stron wygenerowanego lochu oraz izolowanej polany 8x8, bez sąsiednich pokoi. Izolowana polana ma 61 głębokich rezerwacji drzew; cztery kratki za granicą filtr ma alfa ok. 0,502 we wszystkich kierunkach. Sąsiednie rzeczywiste nieodkryte podłogi nadal pozostają czarne. Import i self-review zakończone. Brak ręcznej konfiguracji Editora, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-canopy-readability-recheck-20261022/.
## Las — łagodne wygaszanie dolnych koron polan i wnęk — 2026-10-22 — READY_FOR_SMOKE

Rozszerzono istniejące wygaszanie 175/255 na wygenerowane drzewa graniczne, których rzeczywisty kontur korony nachodzi od południa na aktualnie widoczną podłogę polany lub wnęki. Wybór dotyczy lokalnej dolnej krawędzi każdego obszaru, nie globalnego minimum Y mapy; nie wymaga rozpoznania wąskiego przejścia. Korzenie pozostają chronione. Bohaterowie i widoczni przeciwnicy zachowują mocniejszy cel 90/255 i pierwszeństwo. Zapamiętany/nieodkryty grunt nie uruchamia efektu; odkrywanie, LOS, oświetlenie i geometria nie są zmieniane.

48/48 celowanych testów PASS: dolna krawędź szerokiej polany i osobnej wnęki, pamięć/nieodkrycie, zachowanie górnych i odległych drzew, priorytet bohatera, płynność, brak alokacji rozgrzanego zapytania, istniejące native-contour/GPU/regresje przejść i LOS. Obejrzano produkcyjne rendery przed/100ms/po dla long_glade i blind_glade oraz dodatkowe ujęcia z prawdziwym bohaterem i aktualnym LOS. A/B izoluje nową regułę na w pełni odsłoniętym terenie; stare reguły przejść/bohaterów działają w obu wariantach. Przy 4–5 nakładających się koronach efekt 175 pozostaje celowo łagodny; nie usuwa całej roślinności i nie gwarantuje pełnego odsłonięcia podłogi. Import i self-review zakończone; brak ręcznego Editor setupu. Bez Computer Use i commit/push. Dowody: ../LocalHandoffs/forest-foreground-canopy-20261022/.
## Las — kontakt bohatera z konturem korony zamiast prostokąta grafiki — 2026-10-22 — READY_FOR_SMOKE

Zgłoszenie: korona wygaszała się, gdy Jerzy stał daleko od widocznych liści. Prawdziwe grafiki drzew są importowane jako FullRect (4 wierzchołki); dotychczasowy test kontaktu na siatce obejmował cały przezroczysty prostokąt. Syntetyczne testy TightMesh nie wykrywały tej różnicy. Cache koron korzysta teraz z istniejących importowanych konturów sylwetki (GetPhysicsShape), współdzielonych na gatunek, z dotychczasową siatką jako fallback dla sprite’ów bez konturów. Nie dodaje colliderów, nie zmienia fizyki, importu grafiki ani czytelności tekstur. Test punktu i małego obszaru podtrzymującego używają tego samego konturu, z zachowaniem flipów i chronionych korzeni.

Usunięto też podbicie łagodnego celu przejścia 175/255 do mocnego 90/255 przez sam fakt istnienia stanu fade. Margines podtrzymania bohatera dotyczy tylko wcześniejszego mocnego kontaktu. Punkt wejścia nadal jest przy stopach bohatera, czasy fade i wartości alfa zachowane.

41/41 celowanych testów PASS: sześć prawdziwych sprite’ów normalnie/flip, istniejące regresje aktora i przejścia, nowy przypadek priorytetu, GPU, zero alokacji rozgrzanych zapytań, produkcyjne A/B prostokąt→kontur oraz LOS. Diagnostyka przed zmianą: 5457 przezroczystych próbek traktowanych jako kontakt w każdej orientacji. Importowany kontur ma drobną aproksymację liści, zmierzona granica odchyleń 5–7 px przy 32 PPU; test ogranicza ją do maks. 8 px (1/4 kratki), bez deklaracji maski alfa idealnej co do piksela. Obejrzano rendery rzeczywistego bohatera na przechodnim polu poza koroną: poprzedni prostokąt wygaszał drzewo, kontur pozostawia je pełne. Import/self-review zakończone, bez Computer Use, serialized setupu i commit/push. Dowody: ../LocalHandoffs/forest-canopy-contact-20261022/.

## Las — delikatne wygaszanie koron nad widocznymi przejściami — 2026-10-22 — READY_FOR_SMOKE

Wąskie przejścia (do 3 kratek szerokości, z kontynuacją wzdłuż przejścia) w aktualnym LOS wygaszają faktycznie nachodzące korony do alfy 175/255. Selekcja korzysta z istniejącej siatki koron i tight mesh; chronione korzenie, przezroczyste narożniki i drzewa poza kamerą nie uruchamiają efektu. Szerokie polany oraz zapamiętane/nieodkryte przejścia nie są źródłem wygaszania. Bohaterowie/widoczni przeciwnicy zachowują mocniejszy cel 90/255, który ma pierwszeństwo. Po odejściu bohatera korona wraca do 175, jeśli nadal osłania widoczne przejście; po utracie LOS płynnie do 1. Istniejące czasy wejścia/wyjścia i shader alfy bez zmian.

41/41 testów PASS: rozpoznawanie szerokości, pamięć/nieodkrycie, rzeczywisty mesh liści, priorytet bohatera i płynny powrót, invalidacja po zmianie ściany, zero alokacji rozgrzanego zapytania, dwa GPU dla 175/255, dotychczasowe regresje bohaterów/koron, LOS i głębia lasu. Render produkcyjny przed/100ms/po obejrzany. Podgląd izoluje drogę na w pełni odsłoniętej mapie, stan mgły jest testowany osobno. Celowo wybrano trudny przypadek z 7 nakładającymi się koronami: efekt 175 pozostaje subtelny, wartość próbna do oceny użytkownika. Cache topologii jest przypisany do widoku i odświeżany przez TraversalRevision, bez zmian save/assetów/scen/prefabów. Import i self-review zakończone, bez Computer Use/commit/push. Dowody: ../LocalHandoffs/forest-passage-canopy-20261022/.

## Las — dalsze warstwy drzew za pasem brzegowym — 2026-10-22 — READY_FOR_SMOKE

Screenshot z czerwoną linią pokazywał wizualny koniec lasu tuż za pierwszym pasem drzew. Porównano tę samą scenę z filtrem i bez niego: geometria zawierała 399 dalszych zarezerwowanych pni, ale filtr odkrycia przechodził w pełną czerń już po 3 kratkach, podczas gdy drzewa generowano do 7. Filtr dekoracyjnego lasu używa teraz tej samej głębokości co geometria (wspólne ForestBoundaryDepthInCells), więc dalsze sylwetki przechodzą stopniowo w mrok zamiast znikać za pierwszym rzędem. Bez dodawania drzew na polanach, nowych obiektów i zmian serialized state.

29/29 celowanych testów PASS: cztery strony polany na renderach produkcyjnych, cztery testy dekoracyjnej głębokości/pamięci i nieodkrytej podłogi, cztery LOS za drzewem, jednolite przyciemnienie całej scenerii oraz regresje koron. Obejrzano cztery ujęcia po poprawce i zachodni A/B z filtrem/bez. Znane rzeczywiste pola zachowują swój stan mgły; promień odkrywania, LOS, wykrywanie wrogów, alfa zapamiętania 0.65, światło i wygaszanie koron niezmienione. Na południu próbka 4 kratki poza wybranym brzegiem trafia w inne rzeczywiste nieodkryte pole i prawidłowo pozostaje czarna. Import i self-review zakończone; bez Computer Use, ręcznego Refresh, commit/push. Pozostaje gameplay smoke. Dowody: ../LocalHandoffs/forest-background-depth-20261022/.

## Las — usunięcie dużych drzew ze środka polan — 2026-10-22 — READY_FOR_SMOKE

Usunięto dwie kotwy tree.0/tree.1 z szablonu long_glade przez istniejący authoring Unity. To pełne obiekty przeszkód 2×2, więc brak ich instancji usuwa również fizyczne/logiczne blokowanie ruchu i LOS. Authoring nie dodaje ponownie drzew wewnętrznych, także przy późniejszym imporcie grafiki. Drzewa obrzeży, definicja/grafika drzewa do testów i przyszłego użycia, inni przeciwnicy oraz pozostałe rekwizyty zachowane; bez zamienników. Istniejące zapisane wyprawy zachowują snapshot obiektów — zmiana dotyczy nowych wypraw.

7/7 celowanych testów PASS: dziewięć generacji lasu (3 długości × 3 seedy) bez drzew wewnętrznych i z pozostałymi przeszkodami, cztery przypadki zawartości wypraw/bossów, dwa testy renderu lasu/katakumb. Obejrzany render wnętrza lasu. Diff assetu obejmuje wyłącznie usunięcie dwóch kotew (24 linie); brak zmian scen/prefabów, save migracji, wygaszania koron, światła lub mgły. Import i self-review zakończone, bez Computer Use/commit/push. Dowody: ../LocalHandoffs/forest-remove-interior-trees-20261022/.

## Las — usunięcie nierównego, podwójnego przyciemnienia mgły — 2026-10-22 — READY_FOR_SMOKE

Screenshot użytkownika: ścieżka i obiekty za ciemne, ściółka obok jaśniejsza. Poprzednie zwiększenie alfy nie rozwiązywało przyczyny. Stary tint pamięci 0.55 nadal przyciemniał obiekty i kafle, a shader gruntu stosował go do ścieżki, omijając zwykłą ściółkę. Nad tym działał już wspólny filtr mgły. Usunięto drugi tint w lesie dla kafli, dekoracji, przeszkód/obiektów, bram i ciał. Wspólna polityka jasności pamięci zachowuje 0.55 w katakumbach. Alfa wspólnego filtru 0.65, ambient, shader podnoszenia cieni, paleta, LOS, odkrywanie, widoczność wrogów i wygaszanie koron bez zmian.

Nowy test z rzeczywistym LateUpdate 150 widoków obiektów odtworzył błąd (31 451 pikseli i 211 kafli dodatkowo przyciemnionych). Po naprawie: 0 zmian statycznej scenerii po odjęciu filtru, 42 353 próbki GPU z jednakową transmisją bez odstępstw. Animowane emitery i ich kontakty wyłączone wyłącznie z dowodu identyczności statycznych pikseli; zachowane w oglądanym renderze mieszanej widoczności. Finalne 33/33 testów PASS, rendery obejrzane; self-review i import zakończone. Bez Computer Use, serialized setupu, commit/push. Pozostaje gameplay smoke. Dowody: ../LocalHandoffs/forest-fog-uniformity-20261022/README.md.

## Las — mocniejszy kontrast zapamiętanego terenu — 2026-10-22 — READY_FOR_SMOKE

Alfa czarnego filtru nad zapamiętanym lasem zwiększona z 35% do 65% (transmisja liniowa 0.65 → 0.35). Bieżąca widoczność i teren nieodkryty zachowują dotychczasowe wartości. Bez zmian LOS, odkrywania, wykrywania przeciwników, światła i wygaszania koron.

Import zakończony; 6/6 celowanych testów EditMode PASS: cztery przypadki mgły za drzewem 1×1/2×2 i dwa testy renderu produkcyjnego lasu/katakumb. GPU zapamiętanego białego wzorca: 0.827451 → 0.627451, widoczny sąsiad nadal 1. Obejrzano normalny i zapamiętany widok wejścia oraz zapamiętane wnętrze lasu: wyraźniejsze przygaszenie, zachowane detale. Katakumby bez regresji. Self-review zakończony; bez ręcznego setupu, Computer Use, commit/push. Pozostaje ocena w grze. Dowody: ../LocalHandoffs/forest-fog-contrast-20261022/.

## Las — zgodność renderu mgły z linią wzroku — 2026-10-22 — READY_FOR_SMOKE

Przyczyną fałszywie odsłoniętej ścieżki za drzewem był3-kratkowy margines maski, który przenosił jasność widocznej kratki na sąsiednie ukryte pola. Margines dotyczy teraz wyłącznie dekoracyjnego lasu poza grywalnym gruntem. Każda grywalna kratka zachowuje własny stan Fog.Get — ten sam, którego używa widoczność przeciwników. Zapamiętywanie mapy, LOS, wykrywanie przeciwników, wartości zaciemnienia, paleta i wygaszanie koron niezmienione. Dodano zwalnianie tekstury filtru w istniejącym ReleaseSoftFog.

90/90 focused testów PASS. Nowe4przypadki: drzewo1×1/2×2, pole za nim nieodkryte/zapamiętane, a pole obok widoczne; sprawdzono cały grunt maski, GPU i zgodność z IsCurrentlySeen. Obejście drzewa odsłania pole; powrót przywraca stan zapamiętany bez utraty odkrycia. Obejrzane maskiGPU i rendery produkcyjne127/171026. Pierwszy test cleanupu wymagał jawnego wywołania ReleaseSoftFog w fixture EditMode (bez polegania na callbacku MonoBehaviour). Import zakończony, self-review bez dalszych uwag; brak ręcznego Editor setupu/Computer Use/commit/push. Pozostaje smoke na przykładzie użytkownika. Dowody: ../LocalHandoffs/forest-los-fog-20261022/.
## Las — usunięcie sztucznej obwódki postaci — 2026-10-22 — READY_FOR_SMOKE

ForestPropSeparation nie dodaje już zewnętrznego szarobeżowego konturu do profilu aktora. Zachowane oryginalne piksele/alfa sprite'a, dotychczasowy mnożnik jasności1.16 i osobny system zaznaczenia/hover. Profile rekwizytów/roślin, światło, mgła i wygaszanie koron zachowane. GPU na czterech rzeczywistych sprite'ach bohaterów, pełna alfa i0.4:0zmian alfy wobec źródłowej sylwetki; kontrola dodatnia wykrywa9088pikseli obwódki na profilu rekwizytu. 24/24 testów PASS (GPU konturu, ton światła i korony), rendery obejrzane; self-review zakończony. Import odebrany, bez Computer Use/serialized setupu/commit/push. Pomocnik testowy otrzymał opcjonalny jednorazowy odczyt stanu Editora do diagnozy oczekującego importu. Pozostaje gameplay smoke wyglądu postaci. Dowody: ../LocalHandoffs/forest-actor-contours-20261022/.
## Las — filtr odkrycia i płynność światła — 2026-10-22 — READY_FOR_SMOKE

Poprzednie READY_FOR_SMOKE odrzucono po gameplay: tło omijało wizualne zaciemnienie nieodkrycia. Dodano należący do widoku mapy filtr wiedzy nad leśną scenerią: nieodkryte zaciemnione, zapamiętane przygaszone, widoczne czytelne; miękka granica i animowane przejście, togglefog wyłącza filtr. Aktualna linia wzroku poza promieniem trwałego odkrycia pozostaje odsłonięta. Filtr respektuje granicę pasma HUD i zwalnia teksturę/sprite przy przebudowie mapy. Ambient0.34/detail0.30, paleta i wygaszanie koron zachowane; bez serialized wiring/save zmian.

80/80 focused testów PASS dla filtru, następnie2/2 po korekcie dalekiej linii wzroku. Dwa seedy127/171026,32kroki kamery/FOV/flip: maski GPU cache identyczne z pełnym przeliczeniem. Wykonano24-sekundowy zapis82klatek z gry użytkownika (bez Computer Use/profilera); użytkownik potwierdził brak dalszego zaniku cieni. Zgłosił natomiast migotanie światła. Potwierdzony problem: sinus2.5Hz próbkowany5Hz daje naprzemienne skoki energii. Las otrzymał łagodne ciągłe zmiany0.65/1.1Hz, amplituda0.031 zamiast0.108; same uniforms energii aktualizują się co klatkę, geometria i selekcja źródeł nadal5Hz. Katakumby i shader płomienia bez zmian. Końcowy przebieg81/81 PASS: GPU cache vs pełny render, zmiany energii między tickami bez przebudowy masek, zakres/płynność energii, fog/togglefog/teardown i regresje koron. Zweryfikowano finalne rendery. Utkwione pobieranie katalogu ominięto jawną listą testów w pomocniku, nadal przez Unity Test Runner. Self-review zakończony; bez ręcznego Editor setupu. Do smoke pozostaje nowa płynność światła podczas gry. Dowody: ../LocalHandoffs/forest-discovery-regression-20261022/.
## Las — stabilność cieni i granic mgły — 2026-10-22 — READY_FOR_SMOKE

Usunięto konflikt rysowania mgły i kontaktów (las: podłoga→mgła→cienie→outline→rekwizyt). Cienie zapamiętanych przeszkód korzystają z tej samej widoczności co ich grafika, a statyczne dekoracje nie znikają po utracie bieżącego FOV. Kontakt na leśnej ściółce nie jest przycinany do widzenia jednostek. Nieodkryte pola nadal nie rysują drogi/obiektów; jedna warstwa neutralnej ściółki wypełnia czarne dziury. Statyczna ściółka ma spójne światło niezależnie od wiedzy; przyciemnienie zapamiętania pozostaje na ścieżkach/rekwizytach. Mesh mgły ma własne wejście shaderowe, wspólny tint i siatkę próbkowania32PPU. Zabezpieczono dzielenie przez zero przy odkrywaniu. Zachowane ambient0.34/detail0.30, migotanie pochodni, paleta i wygaszanie koron.

114/114 odrębnych focused testów PASS, w tym finalne3/3 po domknięciu dziedziczenia warstwy tła. Dwa produkcyjne seedy171026/127,12klatek ruchu/FOV, obejrzane rendery granic i różnic GPU. W teście bez dróg/kontaktów0niezgodnych pikseli ponad2/255 między podłożem znanym i tłem. Brak ujawniania kanałów dróg/kontaktów; reuse/destroy i warstwa tła sprawdzone. Historyczny benchmark ContactShadowCostTests ma nieaktualny wzorzec trasy2026-10-18 i nie przeszedł kontroli hash przed pomiarem; nie zmieniano go ani nie wyłączano, nie jest dowodem aktualnegoFPS. Bez Computer Use, ręcznego Refresh, serialized setupu, zmian save/statystyk/pathfindingu lub commit/push. Import zakończony; pozostaje gameplay smoke użytkownika. Raport: ../LocalHandoffs/forest-fog-shadow-stability-20261022/README.md.

## Nocny klimat lasu — korekta jasności — 2026-10-22 — READY_FOR_SMOKE

Po uwadze użytkownika poprzedni profil0.62/detail1 uznano za zbyt dzienny. Aktualnie ambient0.34, siła podnoszenia cieni0.30. Średnia jasność poprzednio ciemnych detali: wejście33.00→16.97, wnętrze33.87→17.65; nadal około1.8× pierwotnej wersji. Parametry są sterowane kodem bez serializacji, aby istniejący Editor nie zachowywał starej jasności po reloadzie. Shader, paleta, pochodnie, mgła i wygaszanie koron bez zmian względem poprzedniej implementacji.

Import zakończony,24/24 testów PASS (GPU, dwa ujęcia produkcyjne obu biomów, regresje koron). Dwa rendery przed/po obejrzane: mrok poza pochodniami i lepsza niż pierwotnie czytelność drzew/ścieżek. Dodany limit rozjaśnienia w teście względem pierwotnej sceny. Katakumby identyczne piksel w piksel. Bez Computer Use, ręcznego Refresh, serialized setupu lub commit/push. Pozostaje ocena użytkownika w grze. Dowody: ../LocalHandoffs/forest-night-readability-20261022/README.md. Ta korekta zastępuje wcześniejsze ustawienie0.62/detail1.

## Czytelność cieni w lesie — 2026-10-22 — READY_FOR_SMOKE

Las ma osobne światło rozproszone 0.62 (katakumby nadal 0.22) i łagodne podniesienie ciemnych tonów przez wspólny mnożnik RGB. Zachowane grafiki, paleta, czerń, alfa koron, odkrywanie terenu i lokalne pochodnie; bez światła przy bohaterze. Profil obejmuje grunt, drzewa, aktorów, rekwizyty i metal pochodni, z pominięciem samych płomieni. Pamiętany teren zachowuje przyciemnienie mgły; nieodkryty teren nadal podlega dotychczasowym regułom.

69/69 odrębnych testów EditMode PASS: 66 integracyjnych/regresji + 3 GPU. Dwa ujęcia produkcyjnego lasu obejrzane przed/po; średnia jasność próbek wcześniej ciemnych detali około 3.5× większa. Katakumby w dwóch ujęciach identyczne piksel w piksel przy zmianie profilu lasu. GPU potwierdza proporcje RGB w liniowej przestrzeni, zachowanie czerni, alfy i jasnego wzorca. Dwa pliki wygaszania i funkcja alfy w shaderze niezmienione. Import w istniejącym Unity zakończony; bez Computer Use, ręcznego Refresh, setupu serialized state lub commit/push. Smoke użytkownika: las w eksploracji/walce, obszar bez pochodni i przy pochodni, znany teren poza widocznością oraz fade koron. Bez pomiaru FPS i ręcznego gameplay smoke. Dowody: ../LocalHandoffs/forest-shadow-readability-20261022/README.md.

## Wyśrodkowanie ikon przedmiotów — 2026-10-22 — READY_FOR_SMOKE

Naprawiono nierówne marginesy odziedziczone po starych grafikach:269PNG przesuniętych całymi pikselami (93bazowe/kopie +176wariantów bransolet/naszyjników). Bez zmiany skali, koloru, alfy, kształtu lub utraty widocznych pikseli. Wszystkie558ikon ekwipunku mieszczą środek obrysu w tolerancji0.5px od środka32×32. 40pomocniczych warstw biżuterii przesunięto zgodnie;17warstw runtime zachowano z rejestracją baz. 575meta/GUID i biblioteka ikon bez zmian, UI/prefaby bez zmian.

Wczytane w istniejącym Unity,13/13testów PASS,15próbek GPU zgodnych z PNG,12kompozycji runtime bez zmian. Obejrzano porównania i93ikony bazowe. Raport, źródła i backup: ../LocalHandoffs/item-icon-centering-20261022/README.md. Historyczne output/ niezmienione; aktualny plan w LocalHandoffs/active-item-art-import/import-plan.json. Bez Computer Use, Ctrl+R lub drugiego Editora. Pozostał smoke użytkownika w ekwipunku, handlu i craftingu. Nie commitowano/pushowano.

## Galduryty — znaki według efektów — 2026-10-22 — READY_FOR_SMOKE

Naprawiono prezentację: klucz ikony zawiera teraz kolor:EffectType zamiast samego koloru. 50 unikalnych natywnych znaków 5×7, pigment według wszystkich 10 proszków, oryginalna kolejność i powtórzenia, układy 1–6. Rozróżnienie efektów tego samego koloru; siła/stopień nadal współdzielą znak. Subtelne nacięcie bez białego obrysu i bez dziur w alfie. Stare klucze kolorowe obsługiwane dla bezpośrednich wywołań. Brak zmian serialized biblioteki, wymagania ręcznego wiring lub migracji save; statystyki i pule efektów bez zmian.

Wczytane w istniejącym Unity, 12 testów ikon + 3 testy tooltipów PASS. Obejrzano 106 renderów wszystkich efektów, pojedynczych i kompaktowych. 1276 plików PNG/meta VS22 oraz biblioteka ikon niezmienione. Bez Computer Use, drugiego Editora lub cyklicznego Refresh. Raport, mapowanie efektów i plansze: ../LocalHandoffs/galduryte-effect-runes-20261022/README.md. Pozostał smoke użytkownika: różne efekty tego samego proszku, kolory Pink/White/Gold/Amber, 6 efektów. Nie commitowano/pushowano.

## Import ikon przedmiotów C — 2026-10-22 — READY_FOR_SMOKE

Za zgodą użytkownika podmieniono i zaimportowano do istniejącego Unity 575 tekstur: 206 bazowych PNG/kopii zgodności (203 unikalne), 352 warianty biżuterii, 17 warstw składania. 569 plików zmienionych, 6 już identycznych. 575/575 meta i GUID bez zmian; ItemIconLibrary.asset bajtowo identyczna, referencje zachowane. 63 nieużywane stare warianty galdurytu niezmienione. Jedna zbiorcza prośba o import, bez Computer Use, Ctrl+R lub drugiego Editora.

8/8 VS22ItemArtTests PASS w otwartym Editorze; sprawdzone ustawienia wszystkich 575 tekstur i dane warstw runtime. Obejrzane 23 rendery Unity: 11 statycznych GPU identycznych z PNG, 6 mieszanek z odchyleniem <=1/255, 6 galdurytów zgodnych z runtime (w tym oryginalna alfa tuszu paneli). Dokumentacja, backup, plan i dowody: ../LocalHandoffs/item-art-import-20261022/README.md. Pozostał użytkowy smoke ekwipunku/craftingu/mieszanek/run; manualnego wiring nie potrzeba. Brak zmian gameplay, scen, prefabów, definicji przedmiotów i konfiguracji biblioteki. Nie commitowano ani nie pushowano.

## Biżuteria — 352 warianty w stylu C — 2026-10-22 — ART_READY_FOR_REVIEW

4 typy × 8 metali × 11 kamieni, po 88 kombinacji. Techniczny eksport Aseprite Lua z 4 zaakceptowanych baz i 11 kamieni przerysowanych wcześniej przez image_gen; bez nowych generacji. Odtworzone kolory metali, dopasowanie jasności osobno dla kształtów, zarejestrowane gniazda i palety nowych kamieni. 352 PNG 32×32 + 352 dwuwarstwowe Aseprite; dodatkowo 32 warstwy metali, 44 kamieni i 4 maski. 352/352 QA PASS, brak duplikatów, końcowe plansze wszystkich kombinacji ocenione. 638/638 istniejących PNG VS22 niezmienionych. Bez importu/Refresh/Computer Use, kodu lub serialized state; gameplay smoke NOT RUN.

Paczka, metoda, prompty źródłowe i porównania: ../output/item-jewelry-variants-restyle-v1/README.md. ZIP: ../output/item-jewelry-variants-restyle-v1/jewelry-variants-restyle-v1.zip. Łącznie przygotowane 203 bazowe ikony i 352 warianty biżuterii. Nazwy wariantów zgodne z obecnymi plikami/kluczami biblioteki, podmiana i import nadal niewykonane. Nie przebudowywano nieużywanych starych wariantów galdurytu.

## Ostatnie 7 baz — styl C — 2026-10-21 — ART_READY_FOR_REVIEW

7 indywidualnych edycji przez wbudowany image_gen: bomba, mikstura, galduryt, bransoleta, kolczyki, naszyjnik i pierścień. Eksport Aseprite 32×32, alfa 0/255, <=24kolory; zachowana dokładna natywna zajętość pod maski/gniazda/runy. 21 warstw pomocniczych (w tym niezmienione 6 masek wag i 6 paneli efektów) oraz 24 offline próby składania. Końcowe plansze ocenione. Wszystkie istniejące PNG VS22 niezmienione. Bez importu/Refresh/Computer Use, zmian kodu lub serialized state; gameplay smoke NOT RUN.

Paczka, prompty, porównania i ograniczenia: ../output/item-neutral-bases-restyle-v1/README.md. ZIP: ../output/item-neutral-bases-restyle-v1/neutral-bases-restyle-v1.zip. Łącznie 203/203 bazowe ikony przygotowane do oceny. Nie oznacza to przebudowy wszystkich 352 wariantów biżuterii: nowe neutralne bazy z istniejącymi kamieniami sprawdzono w 12 próbach rejestracji. Przy późniejszej integracji mikstur/bomb wymagane również companion shading/overlay, a galdurytu baza modularna; sama podmiana neutralnych PNG nie aktualizuje runtime.

## Wyposażenie i użytkowe — 19 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po kamieniach: 4 ikony broni, 4 pancerze i 11 przedmiotów użytkowych/materiałów. 19 indywidualnych edycji przez wbudowany image_gen. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów bez ditheringu. 19/19 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. Wszystkie 26 pozostałych PNG Assets niezmienionych. Bez importu/Refresh/Computer Use i zmian serialized state. Brak ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, prompty i porównania: ../output/item-equipment-utility-restyle-v1/README.md. ZIP: ../output/item-equipment-utility-restyle-v1/equipment-utility-icons-restyle-v1.zip. Łącznie 196/203 bazowych ikon przygotowanych. 7 neutralnych baz (bomba, mikstura, galduryt, bransoleta, kolczyki, naszyjnik, pierścień) odłożonych do skoordynowanego przeglądu z warstwami modularnymi. Warianty biżuterii i grafika modularna pozostają osobnym zakresem. Ta paczka NIE kończy całego katalogu.

## Kamienie i proszki — 21 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji drewna: 11 indywidualnych kamieni i10 wariantów proszku ze wspólnej nowej sylwetki, 12 rysunków przez wbudowany image_gen. Trzy kopie plików proszków pod drugimi nazwami zachowane identycznie bajtowo: 21 unikalnych ikon, 24 PNG i24 Aseprite. Eksport32×32/alfa0–255/do24 kolorów (proszki do16), bez ditheringu. 21/21 unikalnych ikon i3/3 kopie PASS; końcowe plansze1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-gems-restyle-v1/README.md. ZIP: ../output/item-gems-restyle-v1/gems-icons-restyle-v1.zip. Łącznie przygotowano177/203 bazowych ikon;26 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Drewno — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji tekstyliów: 8 bali drewna, 8 desek i 8 kijów. 24 indywidualne edycje przez wbudowany image_gen; odrębne sylwetki, kora i kolory gatunków zachowane. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów, bez ditheringu. 24/24 kontroli PASS, w tym ciągłość kijów; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-wood-restyle-v1/README.md. ZIP: ../output/item-wood-restyle-v1/wood-icons-restyle-v1.zip. Łącznie przygotowano 156/203 bazowych ikon; 47 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Tekstylia — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji skór: 8 włókien, 8 nici i 8 tkanin. 24 indywidualne edycje przez wbudowany image_gen oraz 3 dodatkowe korekty splotu (owcza tkanina v2/v3 i juta v2); odrębne sylwetki i kolory materiałów zachowane. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów, bez ditheringu. 24/24 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-textile-restyle-v1/README.md. ZIP: ../output/item-textile-restyle-v1/textile-icons-restyle-v1.zip. Łącznie przygotowano 132/203 bazowych ikon; 71 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Skóry — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Użytkownik zaakceptował paczkę kowalską i zlecił kolejne ikony. Osobna paczka: 8 skór surowych, 8 garbowanych i 8 pasków. 24 indywidualne rysunki przez wbudowany image_gen, zachowane odrębne sylwetki i materiały. Eksport Aseprite 32×32, alfa 0/255, maksymalnie 24 kolory, bez ditheringu. 24/24 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use i zmian runtime, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-leather-restyle-v1/README.md. ZIP: ../output/item-leather-restyle-v1/leather-icons-restyle-v1.zip. Łącznie z alchemią i kowalstwem przygotowano 108/203 bazowych ikon; 95 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Kowalstwo — 48 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Użytkownik zaakceptował paczkę alchemii i zlecił kolejne ikony. Przygotowano osobno 48 ikon kowalskich: 8 surowców i 5 rodzin po 8 metali. 13 nowych rysunków przez wbudowany image_gen, z nich 40 spójnych wariantów istniejących kolorów metali. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów (warianty do 16), bez ditheringu. 48/48 kontroli PASS, maski identyczne wewnątrz rodzin. Końcowe plansze 1×/2×/3× ocenione. 48/48 obecnych PNG Unity niezmienionych. Bez importu/Refresh/Computer Use i zmian runtime, statystyk, definicji lub metadanych. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty, pochodzenie i porównania: ../output/item-smithing-restyle-v1/README.md. ZIP: ../output/item-smithing-restyle-v1/smithing-icons-restyle-v1.zip. Łącznie z alchemią przygotowano 84/203 bazowych ikon, 119 pozostaje do przeglądu; biżuteria i warstwy modularne osobno.

## Itemy — alchemia, styl C — 2026-10-21 — ART_READY_FOR_REVIEW

Pierwsza paczka36 ikon:4 podstawy alchemiczne,4 składniki katakumb,10 ziół,10 składników potworów,8 prochów ziołowych.32 nowe edycje przez wbudowany image_gen i4 zachowane zaakceptowane próby C. Wspólny eksport32×32, premultiplied area average, alfa0/255, do24 kolorów bez ditheringu. Dodatkowo2 poprawki kontrastu prochów strzelniczych i4 korekty odcieni prochów ziołowych. Plansze1×/2×/3× ocenione po poprawkach;36/36 walidacji plików PASS,4 próby identyczne bajtowo. Bez podmiany w Unity:36/36 obecnych PNG niezmienionych. Bez Computer Use/Refresh/commit/push. Pozostałe167 bazowych ikon katalogu jeszcze nieprzeglądnięte; biżuteria/warstwy poza tą paczką.

Paczka, pełne prompty i porównania: ../output/item-alchemy-restyle-v1/README.md. ZIP: ../output/item-alchemy-restyle-v1/alchemy-icons-restyle-v1.zip. Brak zmian gry/statystyk/definicji/metadanych.

## Odporności i obliczenia statystyk — 2026-10-21 — READY_FOR_SMOKE

Usunięto podwójne naliczanie bonusów odporności w statusach/ruchu/DoT. Wspólny resolver: raw base + typed specific/All + klasa + aktywne galduryty + legacy combat modifiers; tolerancja tylko przy statusie, clamp po pełnej sumie. Naprawione pominięte All, brak refresh przy expiry/removal, brak galdurytowej odporności w tickach, celność bez ResourcePool, regen maga względem starego maxMP i preview bez penetracji Analizy. Ocena ryzyka pułapek używa produkcyjnej szansy statusu.

650/650 testów PASS, w tym16 nowych regresji i3 kolejne JSON roundtrip z buffem; kompilacja4 assemblies PASS. Ponownie2148 pomiarów pułapek eksploracji/1432 cykle DoT;991/991 serialized plików bez zmian. Parametry balansu bez strojenia. Import automatyczny w istniejącym Unity; bez Computer Use/ręcznego Refresh/commit/push. Manualny gameplay i pełne wyprawy NOT RUN. Poprzednia notatka audytu „odporność NIENAPRAWIONA” została zastąpiona tą poprawką.

Raport: ../LocalHandoffs/stat-calculation-fixes-20261021/REVIEW.txt.

## Koszt HP pułapek eksploracji — 2026-10-21 — VERIFIED

Wyłącznie pułapki środowiskowe; potwory wyłączone po doprecyzowaniu zakresu. Poziomy1/5/10/20/30/40/48/50, ten sam budżet zakupów rozszerzony doL50. 2880 wcześniejszych snapshotów ekonomii odtworzonych dokładnie. Główny wariant12 historii loot25, czułośćcash25/cash75; stopniowe EQ/reforge/upgrade, losowe arcydzieła i dwa komplety biżuterii. Bransoleta chroni jeden stały typ DoT na postać. Pełny koszt i wartość po szansie aplikacji liczone osobno.

Test eksportu1/1 PASS:2148 pomiarów na716 stanach,1432 pełne cykle DoT;3 pary las/katakumby mechanicznie identyczne. NaL48 średnio:kolce9.19%HP,gaz12.64%,pieczęć3.77%; mag odpowiednio16.73/22.37/4.38%. DoT środowiskowe nie eskaluje late, gaz pełny około20–38%mocniejszy od kompletnych kolców, na5 zamiast3 ticki. Wykryto podwójne uwzględnienie dodatku odporności bransolety (pipeline+resolver); NIE naprawiano. 991 serialized plików i8 chronionych źródeł runtime bez zmian. Raport: ../LocalHandoffs/trap-health-audit-20261021/REPORT.txt. Bez Computer Use/ręcznego Refresh/commit/push.

## Ujednolicenie i skalowanie bomb — 2026-10-21 — READY_FOR_SMOKE

Wdrożono wspólną moc poziomu wybuchów/DoT, współczynnik wydajności wszystkich składników obrażeń natychmiastowych niezależnie od typu oraz celność fizycznych bomb z bazą100 niezależną od broni. Krzywa zachowana doL30 i płynnie przyspiesza do1200 naL50. DoT liniowy względem mocy, z dotychczasowymi odpornościami/długością; zwykłe skille i już aktywne snapshoty DoT bez przeliczenia. Tooltipy i preview wspólne z wykonaniem. Koszt/zasięg/AoE/ładunki bez zmian.

266/266 testów PASS;4212 wierszy produkcyjnych receptur/definicji,180 aplikacji wybuchu i5 pełnych cykli DoT. 991/991 plików serialized bez zmian. L30–50 2P utrzymuje około7.6–8.3%HP zamiast spadku do4.4%; mieszane1P1M naL48 +14% trafionych obrażeń zamiast+40% względem2P (obrony i unik nadal różnicują typy). Kod automatycznie wczytany; bez Computer Use/ręcznego Refresh/commit/push. Gameplay i pełne wyprawy NOT RUN. Raport: ../LocalHandoffs/alchemy-balance-20261021/REVIEW.txt.

## Audyt mocy alchemicznej — 2026-10-21 — VERIFIED

Bez zmian balansu/runtime. Legalne receptury trzech różnych materiałów,13 poziomów,14 zwykłych przeciwników i4 bossów:3276 porównań podglądu,180 prób aplikacji obrażeń do HP, test eksportu1/1 PASS. 991/991 assetów/prefabów/scen identycznych. Potwierdzone: plateau obrażeńL45–50, przewaga mieszania fizycznych/magicznych przy tym samym miejscu na dodatkowy efekt, przewaga magii po uwzględnieniu uniku. DoT bomb nadal korzysta z AverageAttack rzucającego (odczyt kodu; pełnych pomiarów DoT/buildów nie wykonywano). Uniezależnienie celności od broni pozostaje propozycją, nie wdrożeniem. Raport: ../LocalHandoffs/alchemy-power-audit-20261021/REVIEW.txt. Bez Computer Use, ręcznego Refresh, commit/push.

## Głębia NPC w Arungardzie — 2026-10-21 — READY_FOR_SMOKE

NPC byli pominięci w miejskiej liście sortowaniaY i pozostawali na stałym order0. Bootstrap przekazuje teraz istniejące partyBindings/npcBindings do miejskiego ownera; grupa NPC i drużyny korzysta ze wspólnego sortowania według stóp. Wewnętrzne warstwy stanowisk/foreground/etykiet zachowane. Inaktywne postacie drużyny ujęte, ponowny setup bez duplikacji. Bez serialized zmian ani globalnego wyszukiwania.

22/22 testów PASS:8 nowych miejskich i14 regresji lochów. Rendery produkcyjnych sprite’ów pięciu NPC z Jerzym przed/za; Kapłan obejrzany z obu stron. 991/991 assetów/prefabów/scen identycznych. Import automatyczny; bez Computer Use/commit/push. Gameplay smoke NOT RUN. Raport: ../LocalHandoffs/city-npc-depth-20261021/REVIEW.txt.

## Tooltipy zasobów i czas efektów — 2026-10-21 — READY_FOR_SMOKE

Dodatkowy hover pasków many/furii/impetu wyłączony we wspólnym triggerze. Otwarte tooltipy efektów eksploracji aktualizują pozostałe sekundy bez ruszania kursora, resetu przypięcia lub ponownego otwierania. Ikony czasowych efektów poza walką: radialne przyciemnienie55%, odsłanianie zgodnie z ruchem wskazówek przez6 sekund i reset na kolejnym ticku. Brak zegara sekundowego dla walki i efektów stałych.

188/188 testów PASS (39 focused +149 lifecycle), render kierunku odsłaniania sprawdzony pikselowo i wizualnie. 984/984 assetów/prefabów identycznych; zmiany tylko kod/testy, automatycznie wczytane w otwartym Unity. Bez ręcznego Refresh/Computer Use/commit/push. Gameplay smoke NOT RUN. Raport: ../LocalHandoffs/resource-status-tooltip-20261021/REVIEW.txt.

## Bossowie +40% bazy i impet za ruch — 2026-10-21 — READY_FOR_SMOKE

Wdrożone cztery główne bossy i cztery fazy: bazowe HP/minATK/maxATK ×1.4, bez zmiany growth. Specjalny przywołany rycerz 3 HP zachowany. Impet z kolejnych kratek: 2, 4, 6… niezależnie od AP; poza walką limit50, po6 sekundach bez ruchu −10 i reset serii, kolejne −10 co6 sekund. W walce seria od nowa co własną turę, tura bez ruchu −10. Faktycznie zatwierdzony ruch, również darmowy; teleporty i animacje walki nie naliczają. AI i glosariusz zgodne.

773/773 regresji PASS. Wczytane w otwartym Unity przez Editor API, bez ręcznego Refresh/Computer Use. 880/889 assetów identycznych; zmienione8 definicji bossów/faz i glosariusz. Zapisane bieżące HP nie są leczone; nowe spawny do oceny. Gameplay/FPS/pełne wyprawy NOT RUN. Raport: ../LocalHandoffs/boss-base-and-exploration-momentum-20261021/REVIEW.txt. Bez commit/push.

## Zwykłe moby — baza HP i ATK +40% — 2026-10-21 — READY_FOR_SMOKE

Wdrożony zaakceptowany wariant dla14 zwykłych typów w katakumbach i lesie. Bazowe HP/minATK/maxATK ×1.4 względem poprzednich zaokrąglonych baz, zaokrąglenie do int; dotychczasowe growth i offsety bez zmian. Idempotentny Editor owner, domyślny testowy kandydat zgodny z produkcją. Bossowie/fazy i3HP rycerz poza zakresem. Rzeczywisty średni efekt L1 około+40%, L48 +0.71%HP/+0.83%ATK. 14 assetów zmienione tylko w3 polach bazowych;875/889 identycznych.

633/633 regresji PASS; końcowy retest zgodności starego JSON ustawień1/1 PASS. Wyjaśniona kolejność strzał→Flara po wzmocnieniu ATK, test używa rzeczywistej puli AP; runtime AI bez zmian. Automatycznie wczytane w otwartym Unity, bez ręcznego Refresh/Computer Use/commit/push. Manualny gameplay i pełne wyprawy NOT RUN. Do porównania zacząć nową wyprawę: zapisane currentHP nie są automatycznie uzupełniane. Dowody: ../LocalHandoffs/enemy-base-plus40-20261021/REVIEW.txt, final-tests.xml, verification.json.

## Audyt 28 wcześniejszych porażek testów — 2026-10-21 — VERIFIED

Wszystkie 28 sklasyfikowane i potwierdzone ponownym wykonaniem: 8 starych oczekiwań HP/ATK sprzed kalibracji, 15 scenariuszy Enta ustawiających 700 HP przy obecnym maksimum560 zamiast progu50%, 5 testów obrażeń/leczenia ograniczanych zbyt małą pulą HP. Poprawione wyłącznie testy. W tej grupie nie potwierdzono błędu gry; asercje mechanik zachowane, żaden test nie został wyłączony.

118/118 dotkniętych zestawów PASS; cały poprzedni zakres z końcowym testem preview 534/534 PASS. Wszystkie28 oryginalnych nazw są obecne i PASS. 889/889 assetów identycznych bajtowo; kod produkcyjny poprzedniej karty zgodny z hashami. Bez zmian statystyk, mechanik, kosztów, runtime/Editor code. Automatycznie wczytane w otwartym Unity, bez ręcznego Refresh/Computer Use. Manual gameplay NOT RUN, bez commit/push. Raport i lista: ../LocalHandoffs/legacy-test-audit-20261021/REVIEW.txt, verification.json; wyniki final-tests.xml.

## Glosariusz i pozostałe punkty AI — 2026-10-21 — READY_FOR_SMOKE

87 niepustych wpisów glosariusza, aktualne zasady zasobów i walki, krótkie linki do pełnych zasad pod paskami. Trzy prefaby podpięte automatycznie przez Editor API, z pełną przezroczystą powierzchnią hoveru. Ukończona lista AI 16/16: wiarygodne dobicia, faktyczne leczenie, ochrona/control/cleanse względem zagrożenia, wspólny focus, ruch zachowujący AP, alternatywne cele i pułapki; bossowie zachowują fazy. Naprawione runtime All/IncludesCaster i niewidzialni sojusznicy. Bez zmian statystyk i kosztów skilli.

264/264 odrębne testy głównego zakresu PASS. Szerszy zestaw 505/533; 28 błędów istniało przed kartą, zero nowych. Końcowa regresja preview 8/8 PASS. Kompilacja i scoped self-review PASS; automatyczny import w otwartym Unity, bez ręcznego Refresh/Computer Use. Manualny gameplay, pełne wyprawy i FPS NOT RUN. Raport: ../LocalHandoffs/glossary-ai-completion-20261021/REVIEW.txt; lista: ../LocalHandoffs/ai-wraith-combo-20261020/PROGRESS.md. Bez commit/push.

## Pakiet many maga i paladyna — 2026-10-21 — READY_FOR_SMOKE

Naturalny odzysk many bohaterów na końcu tury zależy od niewydanych AP: regeneracja × 2 × pozostała część AP. Mag ma bazowy regen 15; paladyn nadal 15. Czarnoksiężnik −0,5% kosztu/SP; Odzyskiwanie Many +0,25%/+0,5% regenu za brakujący 1% many. Opisy drzewka i glosariusz zaktualizowane. Koszty skilli/AP i EQ bez zmian; NPC/przywołania AI zachowują tick na początku tury, eksploracja zwykły tick co 6 sekund.

275/275 odrębnych synchronicznych EditMode PASS; rzeczywiste wykonanie Fireball/Heal, AP, blokady, overflow, brak podwójnego ticka, eksploracja, UI i save/load. Kompilacja czterech assembly i self-review PASS. Wąski import bazowej regeneracji/opisów automatycznie w otwartym Unity; 887/889 assetów identycznych bajtowo, zmienione tylko CD_Mage i ST_Mage_Warlock. Bez ręcznej konfiguracji/Refresh/Computer Use/commit/push. Manualny gameplay i pełne wyprawy NOT RUN. Dowody: ../LocalHandoffs/mana-package-20261021/REVIEW.txt, regression-tests.xml, integration-tests.xml, scoped.patch.

## Przytrzymanie LPM w drzewku po przewijaniu — 2026-10-21 — READY_FOR_SMOKE

Naprawione natychmiastowe anulowanie holda przez historyczne scrollDelta przechowywane przez InputSystemUIInputModule. Nowe zdarzenie przewijania anuluje hold i jest przekazywane do ScrollRect rodzica. Bez zmian kosztu, wymagań SP, serialized state ani sceny. Wcześniejsze zmiany zachowane.

Reprodukcja 3/3 nowych przypadków FAIL przed poprawką; po zmianie 45/45 synchronicznych EditMode PASS, w tym rzeczywisty template aktywnego/pasywnego węzła, pół obwódki, pojedynczy zakup, anulowanie i zachowanie scrollowania. Preflight Views/EditModeTests PASS; automatyczny import/testy w otwartym Editorze, bez manualnego Refresh/Computer Use. Coroutine testy i ręczny gameplay smoke NOT RUN. Dowody: LocalHandoffs/skill-tree-hold-fix-20261021/REVIEW.txt, before-tests.xml, after-tests.xml, scoped.patch.

## Audyt furii / impetu / many — 2026-10-20 — AUDIT_COMPLETE

Na życzenie użytkownika dalsze AI enhancementy odłożone. Audyt read-only 47 skilli bohaterów z aktualnych starterów/ośmiu drzewek, 23 profili NPC/67 różnych skilli, reguł zasobów, ruchu, eksploracji, odzysku sztyletów, talentów i głównych modyfikatorów EQ. Zidentyfikowano wysoki koszt budowania impetu względem cen, znaczenie +8 za odzysk, natychmiastowy reset impetu poza walką, korzystny cykl Cięcie + Kopnięcie oraz legalny pakiet 3 SP maga finansujący jedną Kulę Ognia co turę (34 many kosztu i 34 odzysku, bez innych wydatków/blokad). Sam wzrost poziomu nie zwiększa bazowych limitów/regeneracji tych bohaterów. Porównanie dostępności zasobów nie jest pełnym rankingiem siły klas.

47/47 istniejących testów reguł/eksploracji/maga PASS; osobna jawna arytmetyka cykli, bez nowych symulacji AI/wypraw. Bez zmian kodu Unity, statystyk, kosztów, mechanik i serialized state; bez importu/Refresh/Computer Use. Raport: ../LocalHandoffs/resource-balance-audit-20261020/REPORT.txt, authored-data.json, derived.json, verification.json. Propozycje kolejności korekt w raporcie; brak wdrożenia balansu.

## AI NPC — etap 5: flanka Rzezimieszka i doskoki — 2026-10-20 — READY_FOR_SMOKE

Punkty 6 i 7: sam Cios w Plecy i Zasadzka → flanka → cios jako alternatywy według kosztów/wartości, dwa turowe okno Zasadzki, wspólny bonus od tyłu przed obroną, lokalne trasy do sześciu kroków i zachowanie pozycji po dojściu bez AP. Wilk/ghul: preview geometrii doskoku i pewnych pchnięć, ataki jako następniki zamiast płaskiego bonusu za modyfikator. Losowe kontakty i kolizje nie tworzą pewnego dalszego combo. Bez zmian statystyk, kosztów, zasad, serialized state i save.

214/214 testów finalnego pakietu PASS, 29 nowych przypadków; końcowe fixture'y zawierają pełne authored paski trzech mobów. Szerszy check 391/421; te same 30 nazw błędów co przed etapem. Kompilacja, self-review i automatyczny import PASS; bez Refresh/Computer Use/ręcznego wiring. Wykonane 9/16, pozostało 7. Manualny gameplay/FPS NOT RUN. Dowody: ../LocalHandoffs/ai-flank-charge-20261020/REVIEW.md i verification.json. Bez commit/push.

## AI NPC — etap 4: wartość przygotowania i obaj łucznicy — 2026-10-20 — READY_FOR_SMOKE

Punkty 3 i 8: obsługiwane ofensywne buffy nie dostają stałych punktów za samo nałożenie. Wartość wynika z legalnych następników i ograniczonego kolejnego okna tury, z pozostałymi zasobami, celami, LOS, krytem, obroną, odbiorcami grupowymi i kosztem opóźnienia AV. Sokole Oko korzysta z faktycznego payloadu prognozy; aktywne przygotowanie chroni AP strzału przed ruchem zbierającym impet. Brak zmian prawdziwych statystyk, kosztów, targetowania, mechanik, serialized state i save. Właściciele bossów zachowani także przy brakującej fladze IsBoss. Nie jest to pełna polityka rezerwy many na całą walkę.

207/207 testów pakietu PASS (21 nowych przypadków), kompilacja Runtime/Systems/EditModeTests i self-review PASS. Szerszy check 362/392; pozostałe 30 błędów identyczne jak w poprzednim etapie. Zmienione cztery stare oczekiwania sztywnej kolejności na cel aktualnej wyceny i prawidłowy AP/status owner — opisane w raporcie. Autoimport w otwartym Unity bez Ctrl+R i Computer Use; manual gameplay/FPS NOT RUN. Wykonane 7/16, pozostało 9. Dowody: ../LocalHandoffs/ai-preparation-value-20261020/REVIEW.md, verification.json, final-tests.xml, broader-tests.xml. Bez commit/push.

## AI NPC — etap 3: wspólny planer, budżet i ponowna ocena — 2026-10-20 — READY_FOR_SMOKE

Punkty 1, 2 i 15 audytu: ograniczone planowanie sekwencji na odłączonym stanie, rezerwa AP/zasobów z kosztami ruchu/terenu/drzwi, wykonanie pierwszego kroku, ponowny query po akcji, anulowanie nieaktualnego jobu i wybór alternatywy po odrzuceniu wykonania. Wzorzec Upiora uogólniony z efektów bez ID. Zachowane reguły dystansowców, oczyszczania i bossów; nieobsługiwana złożona użyteczność pozostaje przy dotychczasowym wyborze. Bez zmian statystyk, mechanik, assets i save. 162/162 testów pakietu AI PASS; szerszy check 341/371, pozostałe 30 błędów identyczne w kontrolowanej próbie bez forecastu. Trzy znalezione regresje wyboru Flary/Plagi i podejścia dystansowego naprawione. Kompilacja i self-review PASS. Wykonane 5/16; pozostało 11. Dowody: ../LocalHandoffs/ai-turn-planner-20261020/REVIEW.md, final-tests.xml, verification.json. Autoimport bez Ctrl+R i Computer Use; manual gameplay/FPS NOT RUN; bez commit/push.

## AI NPC — etap 2: warunkowy status przed buffem / Umarlak — 2026-10-20 — READY_FOR_SMOKE

Ogólna reguła odczytuje AnyOpponentHasStatus/NoOpponentHasStatus z pary efektów self-buffa; bez nazw mobów/skilli/statusów. Ocenia atak statusowy + mocniejszy buff przy legalnym AP i zasobach, a po akcji ponownie sprawdza rzeczywisty status. Zapach Krwi nie poprzedza próby krwawienia ani nie jest marnowany po chybieniu; zachowany słaby wariant przy niemożliwym warunku (np. pełnej odporności). Prognoza Rage na odłączonej puli. 134/134 testów PASS; 14 nowych przypadków, w tym 32 wyniki ugryzienia i alternatywne ID/Poison. Bez zmian statystyk/mechanik/assets/save, bez computer use i commit/push. Wykonane 2/16 (punkty 4 i 5); pozostało 14. Dowody: ../LocalHandoffs/ai-undead-combo-20261020/REVIEW.md; tracker: ../LocalHandoffs/ai-wraith-combo-20261020/PROGRESS.md.

## AI NPC — etap 1: kombinacja Upiora — 2026-10-20 — READY_FOR_SMOKE

Materializacja rezerwuje AP i manę na doskok w tej samej turze; niewidzialne podejście zatrzymuje się w zasięgu i czeka na pełny budżet. Zachowane ratunkowe leczenie, atak przy wygasającej niewidzialności i ograniczenia widoczności/doskoku. Bez zmian statystyk, skilli i save. 120/120 testów PASS, w tym 15 nowych przypadków na produkcyjnych skillach. Wykonane 1/16 (punkt 5 audytu), pozostało 15; wspólny planer i Umarlak nadal otwarte. Dowody: ../LocalHandoffs/ai-wraith-combo-20261020/REVIEW.md i PROGRESS.md. Bez computer use, commit/push.

## Sztylet na ziemi bez animacji — 2026-10-20 — READY_FOR_SMOKE

Całkowicie usunięto animację lądowania, także poprzedni ruch skokowy. Sztylet od razu leży w pozycji docelowej; hover jest dostępny natychmiast. Bez zmian pozy rzucającej postaci, obrażeń, zużycia/podnoszenia, fog, grafiki i skali. 60/60 testów PASS; obejrzane pięć identycznych stanów renderu. Wczytane automatycznie, bez ręcznej konfiguracji, Refresh i Computer Use. Dowody: ../LocalHandoffs/knife-static-20261020/REVIEW.md. Manualny gameplay/FPS NOT RUN, bez commit/push. Zastępuje wcześniejszą zasadę lądowania w trzech pozycjach.

## Prezentacja akcji walki — 2026-10-20 — READY_FOR_SMOKE

Naprawione: nazwa/poza skilla ujawniającego przeciwnika (również Materializacja upiora), pierwszy cast/hit po wejściu do walki i wiązanie HUD przed openingiem, kasowanie pozy przez zerowy krok oraz zbyt wczesne usuwanie reakcji po śmiertelnym trafieniu. Lądowanie sztyletu: 0,24 s, trzy utrzymywane pozycje bez płynnego łuku i obrotu. Bez zmian kosztów/obrażeń/niewidzialności/AI/save. Poprawione wspólne sprzątanie rendererów oraz legalne zasoby dwóch fixture'ów testowych.

532/532 odrębne testy PASS; finalne rendery czterech bohaterów cast/hit, śmiertelnego trafienia i etapów sztyletu obejrzane. Audyt ośmiu kierunków i melee/ranged bez brakujących mapowań. Wczytane automatycznie w istniejącym Unity, bez ręcznej konfiguracji, Refresh, Computer Use, serialized edits i commit/push. Manualny gameplay/FPS NOT RUN. Smoke: pierwszy skill, śmiertelny hit, Materializacja/atak z ukrycia, rzucanie/odzysk sztyletu. Dowody i szczegóły: ../LocalHandoffs/combat-action-presentation-20261020/REVIEW.md.

## Pasek wytrzymałości przeszkód — 2026-10-20 — READY_FOR_SMOKE

Fill ograniczony do wnętrza istniejącej ramki (wcięcie i maska), większy pasek z czytelną etykietą i ciemnym tłem. Pozycja nad górą grafiki przeszkody z uwzględnieniem przezroczystego marginesu, zamiast stałego offsetu od komórki. 95/95 odrębnych testów PASS; końcowe 5 przypadków renderu ponowione PASS. Obejrzane porównania przed/po dla 1/3, 2/3 i 3/3 oraz dwóch skal Canvas. Wczytane automatycznie w istniejącym Unity, bez ręcznej konfiguracji/Refresh/Computer Use. Murki i rozmieszczenie filarów bez zmian. Smoke: hover i niszczenie filaru/gruzu, różne wartości i zoom. Dowody: ../LocalHandoffs/obstacle-health-20261020/REVIEW.md. Bez commit/push.

## Niskie murki — dedykowane sprite'y — 2026-10-20 — READY_FOR_SMOKE

Odrzucone ramki/szyny oraz generowana faktura zastąpione gotowym arkuszem niskich murków catacombs-low-wall-sheet-v1.png (built-in image_gen, mechaniczny eksport 128x192 w Aseprite). Lico, fazowana nakrywa, boczny powrót, końce i styki z jednego materiału; usunięte uskoki powtarzanego pionu i dopasowany chłodniejszy odcień. Nakrywa pozioma 6 px, szerokość pionu12 px, lico12 px, front18 px — zgodnie ze skrótem perspektywy rysunku. Zachowany kontakt 1 px alpha70/255, przycięcia przy pełnej ścianie, głębia i fog. Korzenie/collidery, logiczne ściany, mechaniki/save i korony bez zmian. Balkon poza tą korektą.

Po korektach obejrzano finalne rendery GPU obu kierunków/końców, czterech L, T i krzyża, podłogi, pełnej ściany i postaci po obu stronach, w wariancie neutralnym oraz z shaderami gry/pochodniami. 107/107 testów PASS. To kontrolowany harness, bez zapisu żywej gry i pomiaru FPS. Pliki, prompty i ograniczenia: ../LocalHandoffs/low-wall-sprites-20261020/REVIEW.md.

Import automatyczny przez istniejący importer; .meta utworzył Unity. Bez ręcznego YAML/GUID/scen/prefabów/wiring, Computer Use, Ctrl+R, drugiego Unity, agentów i commit/push. Wejść ponownie do lochu i sprawdzić końcowy wygląd z fog/światłem oraz postaciami po obu stronach.

## Drzwi w walce — hover i LPM — 2026-10-20 — READY_FOR_SMOKE

Outline i klik po widocznym spricie także po otwarciu drzwi; płatna interakcja, jeden owner E, ochrona przed dodatkowym ruchem i klikami podczas celowania. 168/168 testów PASS, rendery otwartych/zamkniętych drzwi obejrzane. Import automatyczny; bez serialized edits/commit/push. Szczegóły: UNITY_HANDOFF.md oraz ../LocalHandoffs/combat-door-hover-20261020/REVIEW.md. Osobna diagnoza nawigacji AI pozostaje otwarta.

## Niewidzialność — pełne ukrywanie przeciwników — 2026-10-20 — READY_FOR_SMOKE

Niewidzialny wróg nie ma sprite’a, akcentów, hoveru/inspekcji ani markera minimapy; Known ukrywa portret/HP/szczegóły w kolejce. Kamera nie odczytuje ukrytej pozycji podczas ruchu lub kadrowania. Sojusznicy pozostają widoczni z alfą45%, bez jej narastania; poprawny powrót po ujawnieniu i po zwolnieniu combat binding. Reguły targetingu/AI/break/obrażeń bez zmian: kierowane w teren wzory nadal trafiają niewidzialne cele, ale UI nie podświetla ich pól ani podglądu jednostki. Nie dodano skilla wykrywania. Widoki respetkują mgłę/elevation i permanent invisibility bossa. Wygaszanie koron/drzewa bez zmian runtime.

227/227 testów prezentacji + 67/67 statusów/glosariusza PASS. Dodatkowo 173/175 regresji NPC: dwa stare oczekiwania bazowego HP (500 vs200 i350 vs154), niezwiązane z niewidzialnością; statystyk nie zmieniano. Zaktualizowany wyłącznie stary test statycznych drzew do wcześniejszej reguły braku znikania w fog. Izolowane piksele GPU i obrazy PASS, self-review/whitespace PASS. Import automatyczny, bez Refresh/setupu/migracji, drugiego Unity, agentów lub serialized edits. Manual gameplay/FPS NOT RUN, bez commit/push. Smoke: niewidzialny bandyta/upiór, brak hoveru/minimapy/śledzenia, AoE w teren, ujawnienie; własny Zwiadowca pozostaje półprzezroczysty. Dowody/ograniczenia: `../LocalHandoffs/invisibility-20261020/REVIEW.md`.
## Generowanie lochów — 2026-10-20 — READY_FOR_SMOKE

Skrócono dobór połączeń ścian katakumb przez indeks narożników zamiast porównań wszystkich par. Generator ponownie używa krawędzi, poziomów i walidacji szablonu w obrębie jednego Generate; pamięć wyników nie przechodzi między wyprawami. Zachowano RNG, layout, zawartość, precyzję nawigacji, warstwy i wygaszanie koron.

Te same długie wyprawy, seedy 171026 i 6, L10, produkcyjny harness w istniejącym Unity: Katakumby 15,89 → 5,63 s oraz 24,60 → 7,51 s; Las 8,96 → 6,60 s oraz 8,92 → 7,64 s. Cała próba obejmuje kontrolę manifestu i zapytania testowe; nie jest pomiarem FPS. Generator tworzy wszystkie piętra, widoki pierwszego. Zgodność manifestów i dostępności próbkowanych tras przed/po; 152 odrębne testy EditMode PASS, self-review/whitespace PASS. Import automatyczny, bez ręcznego Refresh/drugiego Unity/agentów i serialized edits. Brak manualnego setupu/migracji; bez commit/push. Smoke: nowa długa wyprawa Katakumb, ściany/narożniki, drzwi i ruch, następnie Las. Dowody i ograniczenia: `../LocalHandoffs/dungeon-generation-20261020/REVIEW.md`.
## Pasek skilli podczas eksploracji — 2026-10-20 — READY_FOR_SMOKE

W lochu pasek aktywnego zestawu lidera jest stale widoczny poza walką. Klik ikony lub skrót 1–0 wybiera skill i uruchamia dotychczasowe celowanie; usunięto opcję PPM Przygotuj atak. Esc/PPM anuluje celowanie i przywraca ruch bez chowania paska. Zmiana lidera i otwarcie menu kończą preview; menu nie jest zamykane przez anulowanie. Kolejka tur z ramką/uchwytem, aktywna karta i przyciski bojowe ukryte poza walką. Właściwe koszty, walidacja, dołączanie drużyny, alarm i osobne AV wykrycia zachowane. Faktyczny wskazany przeciwnik wybiera źródło walki, nie techniczny seed preview. Klik ataku nie przecieka do ruchu po anulowaniu. Zwiadowca widzi początkowe sztylety przed pierwszą walką i zachowuje zużyte po walce.

Bezczynny HUD używa lekkiej otoczki lidera, bez siatki całego piętra, sesji zarejestrowanej w grze, tur lub wydawania zasobów. Pełne preview powstaje dopiero przy wyborze skilla. Sloty są ponownie używane i nie przebudowują się przy przejściu do preview.

220 odrębnych testów EditMode PASS (148 świat/walka + 72 HUD), bez fail/skip; końcowy fokus scen po ochronie kliku zapisany osobno. Natywny render paska obejrzany. Importy w istniejącym Unity automatyczne, bez ręcznego Refresh, drugiego Unity i agentów. Brak zmian serialized state/prefabów/scen, save, balansu lub wygaszania koron. Smoke: wejść do lochu, wybrać skill z paska/skrótu i przeciwnika; sprawdzić Esc, zmianę lidera i normalny start walki. Manualny gameplay/FPS NOT RUN; bez commit/push. Dowody: `../LocalHandoffs/exploration-skillbar-20261020/`.

## Korekta zleceń — 2026-10-20 — READY_FOR_SMOKE

Nowe tablice obejmują Las i Katakumby przez preferowanie najmniej reprezentowanego typu. Nowe „ukończ” wybiera aktualną wyprawę najwyżej na poziomie drużyny. Trudniejsze wyprawy pozostają opcjonalne. Nazwy „Oczyść: Las/Katakumby” bez mało/średnio/dużo; cele i nagrody bez zmian. Stare nieprzyjęte zlecenia za wysokiego poziomu oraz pełne tablice jednego typu są naprawiane raz, bez naruszania przyjętych/nagród ani odnawiania odebranych miejsc.

76/76 EditMode PASS, w tym 384 tablice L1/5/10/20/48/50. Natywne rendery L1/L48 obejrzane. Import automatyczny w istniejącym Unity, bez ręcznego Refresh i setupu. Bez zmian statystyk, schematu save i wygaszania koron; bez commit/push. Smoke: otworzyć zlecenia i sprawdzić oba typy, nazwy i poziomy. Dowody: `../LocalHandoffs/mission-board-correction-20261020/tests.xml`, `REVIEW.md`.

## Wyprawy i zlecenia — unikalne oferty i trzy losowe miejsca — 2026-10-20 — READY_FOR_SMOKE

Wyprawy: 6 ofert bez powtarzania kombinacji rzeczywisty poziom/typ lochu/długość. Skończony dobór kombinacji, także na L1/L50 i po zmianie poziomu drużyny; naprawa starych duplikatów zachowuje seedy, zaznaczenie i kolejkę bossów.

Zlecenia: trzy miejsca łącznie, losowane między „oczyść” i „ukończ”. Oczyść: 8–12 / 16–24 / 24–36 przeciwników danego typu lochu, poziom ze średniej aktywnej drużyny. Ukończ: typ, długość i poziom z oferty obecnej przy losowaniu; dotychczasowa zasada dopuszcza ten poziom lub wyższy. XP/złoto/honor gotowe ×1/×2/×3 odpowiednio do wielkości lub długości, pojedynczy bonus bez mnożenia; brak napisu „Nagroda ×1–3”. Nagroda nie jest ponownie mnożona przy zakończeniu. Postęp tylko po przyjęciu, posiłki/przywołania nie liczą się.

Po ukończeniu lochu ponownie losowane są nieprzyjęte i zwolnione miejsca; przyjęte i nagrody czekające na odbiór zostają. Odebrany slot czeka do następnego ukończenia; porzucenie lub ponowne otwarcie okna nie odświeża zleceń. Cel i gotowa nagroda utrwalane w save. Stare aktywne/ukończone zlecenia zachowane, nieprzyjęte przechodzą na nowy generator przy wejściu do miasta/otwarciu zleceń. Dziennik używa aktualnych nazw i celów.

114/114 odrębnych EditMode PASS, 20 przypadków nowego generatora; natywne rendery wszystkich trzech zleceń na L1 i L48 obejrzane. Backward JSON, częściowy postęp, pojedynczy właściciel bonusowego przedmiotu, brak podwójnej wypłaty i obie tablice po wczytaniu sprawdzone. Końcowa kompilacja/import/self-review/whitespace PASS. Bez ręcznego Refresh, setupu, drugiego Unity/Node/agentów, zmian definicji/scen/prefabów lub commit/push. Manualny gameplay smoke NOT RUN. Smoke: uruchomić grę, otworzyć tablicę wypraw i zlecenia; zaakceptować misję, ukończyć loch, sprawdzić zachowany postęp i odnowienie wolnych miejsc. Dowody: `../LocalHandoffs/mission-board-pass-20261020/final-tests.xml`, `REVIEW.md`.

## Las — usunięte światło aktywnego bohatera — 2026-10-20 — READY_FOR_SMOKE

BuildTorches nie tworzy już niewidocznego źródła Party light source, które podążało za liderem. Zachowane statyczne pochodnie/ogniska, światła areny, ambient, mgła i wygaszanie koron. Kod importowany automatycznie w istniejącym Unity; bez serialized state i setupu. 7/7 focused EditMode PASS: trzy seedy źródeł statycznych bez carried, brak duplikacji, sortowanie, maska/kontakt i wybór świateł. Self-review PASS; canopy SHA bez zmian. Smoke: wznowić Play Mode, ruch/zmiana lidera nie przenosi punktowego światła. Dowody: `../LocalHandoffs/remove-party-light-20261020/tests.xml`. Bez pomiaru FPS/commit/push.

## Las — dedykowana drewniana furtka wyjścia — 2026-10-20 — READY_FOR_SMOKE

Wyjście lasu ma jeden nowy sprite otwartej drewnianej furtki z krótkimi przęsłami, zamiast dwóch dużych drzew. Natywne 160×107, 32 PPU, pivot progu (.5,.245), transform 1. Automatyczny importer dotyczy tylko dwóch nowych PNG. Zachowane LPM/podejście/E, hover outline, callback i fizyczny trigger; bez nowej blokady przejścia. Standing SortingGroup śledzi pasmo głębi, światło podłoża zachowuje sortowanie, dodany kontakt cienia. Wybór wolnego miejsca w pokoju uwzględnia korzenie i korony przed bramą. Katakumby zachowują schody.

42/42 odrębne EditMode PASS: 41 finalnych + 1 drugi produkcyjny seed. Obejrzane dwa produkcyjne rendery wyjścia z drużyną i render hovera; shader oświetlenia i głębia sprawdzone asercjami. Dostępność wszystkich trzech długości PASS. Trzy pliki wygaszania koron identyczne SHA. Self-review/import PASS. Bez ręcznego Refresh, setupu, nowej wyprawy, zmiany save/schema, dodatkowego Unity/Node/agentów lub commit/push. Smoke: uruchomić Play Mode lub wznowić wyprawę, sprawdzić furtkę i LPM/E. Ręcznego gameplay smoke ani FPS nie wykonano. Dowody/art prompt: `../LocalHandoffs/forest-exit-gate-20261020/REVIEW.md`, `ART_PROMPT.md`.

## Las — sortowanie pochodni względem drzew — 2026-10-20 — READY_FOR_SMOKE

Usunięto wymuszanie rysowania leśnych pochodni ponad wszystkimi nakładającymi się koronami. Wolnostojąca pochodnia korzysta z głębi podstawy; ognisko i zamontowane źródła zachowują kolejność względem właściciela. Źródła światła, shader, rozmieszczenie, fizyka, mgła i wygaszanie koron bez zmian.

18/18 focused EditMode PASS, w tym trzy rzeczywiste rendery GPU shaderu pochodni przy przesunięciu podstawy względem drzewa o -1/32, 0 i +1/32. Sprawdzone także aktorzy/ciała, zamontowane pochodnie, stabilny wybór świateł i kontakt. Self-review PASS, trzy pliki wygaszania identyczne SHA. Import zakończony automatycznie. Smoke: pochodnia za drzewem zasłonięta, przed drzewem widoczna; światło nadal oświetla otoczenie. Brak ręcznego setupu, Refresh, konieczności nowej wyprawy i pomiaru FPS. Dowody: `../LocalHandoffs/forest-torch-depth-20261020/tests.xml`.

## Świat — outline oraz LPM z podejściem dla wyjścia, stasha, aktywatorów i drzwi — 2026-10-20 — READY_FOR_SMOKE

Wyjście/przejścia, stash, dźwignie, znaki i drzwi eksploracyjne mają hover outline i LPM: podejdź/użyj. E z bliska zachowane. Wspólny jawnie podpięty komponent respektuje priorytet NPC/jednostek/UI, widoczność, floor owner, życie i walkę. Nowe polecenie/WSAD/anulowanie przerywa pending; użycie dokładnie raz po dotarciu. Zużyte aktywatory bez hovera; LeverOnly i koszt PA w walce nie są obchodzone. Hover wyjścia jest ograniczony do przejścia, nie całych dalekich koron.

110/110 testów PASS (35 nowych), GPU/render pięciu typów obejrzany, self-review PASS. 122 assety oraz wygaszanie koron identyczne SHA. Import automatyczny zakończony; brak setupu/Refresh/migracji/new expedition. Smoke: miasto stash, loch aktywatory/drzwi i końcowe wyjście — LPM z daleka/E z bliska/anulowanie. Bez pomiaru FPS/commit/push. Raport: `../LocalHandoffs/world-object-primary-pass-20261020/REVIEW.md`.

## Las — stałe drzewa, integracja obiektów i sprawdzenie wyjścia — 2026-10-20 — READY_FOR_SMOKE

Naprawione drzewa obrzeży: rysowanie/kolor niezależne od mgły właściciela, nadal naturalnie oświetlane, kamera/elevation zachowane; nieodkryte drogi i obiekty nadal ukryte. Leśne pułapki i aktywatory podłączone do oświetlenia podłoża, natywne warstwy dziedziczone poprawnie. Dźwignia ma małą fizyczną podstawę uwzględnianą przez navmesh lidera/towarzyszy i sortowanie stojącego obiektu. Trzy dedykowane oryginalne sprite’y pułapek 64×64 zastępują recykling NPC. Wyjście faktycznie tworzone na dwóch produkcyjnych seedach, osiągalne po otwarciu bram dla wszystkich długości; wygląd nadal dwa drzewa i interakcja E.

115/115 odrębnych testów PASS, rendery obejrzane, self-review PASS. Wygaszanie koron i shader nietknięte (SHA). Import automatyczny zakończony, bez setupu/Refresh. Smoke: zwykła mgła/togglefog, omijanie podstawy wyciągarki, nowe grafiki pułapek i wyjście w Final. Brak pomiaru FPS, commit/push. Raport: `../LocalHandoffs/forest-visibility-object-audit-20261020/REVIEW.md`.

## Las — pułapki, aktywatory i blokady progresji; otwarte skrzynie obu biomów — 2026-10-20 — READY_FOR_SMOKE

Wdrożone i zaimportowane przez Editor API: wnyki kłusownika (krwawienie), trująca narośl (zatrucie), skażone korzenie (osłabienie); 1–2 pełnoszerokie blokady z pni z osiągalną boczną dźwignią wyciągarki albo znakiem do oczyszczenia. Wszystkie skrzynie obu biomów bez zamków; stara flaga zamknięcia migruje bez rerollowania łupu. Stare zapisane układy nie dostają nowych bram. Nowe elementy wymagają świeżej wyprawy. Wygaszanie koron nietknięte.

153/153 odrębnych Unity testów PASS, rendery produkcyjne obejrzane, idempotentny import 293 assetów/meta, 17 definicji bez zmian pól balansu. Self-review PASS. Bez ręcznego setupu/Refresh/commit/push. Smoke: nowa wyprawa lasu → pień blokuje całą szerokość → aktywator w bocznej odnodze otwiera przejście; pułapki/statusy, zapis/odczyt i skrzynie bez kluczy w obu biomach. Raport: `../LocalHandoffs/forest-exploration-objects-20261020/REVIEW.md`.

## Las — brakujące odpowiedniki obiektów katakumb — 2026-10-20 — AUDIT_COMPLETE / ZAKRES WDROŻONY PONIŻEJ

Read-only porównanie aktywnych definicji i katalogów regularnych wypraw: Katakumby16szablonów, Las11. Las ma8definicji przeszkód i2otwarte pojemniki; zero definicji/kotew pułapek, dźwigni i aktywowanych warunków. Katakumby mają3docelowe pułapki (kolce/bleed,gaz/poison,pieczęć/weakening), dźwignię, warunek, drzwi i skrzynię KeyOrLockpick. Kotwy w katalogu (nie liczba obiektów na wyprawę): Katakumby14pułapek,1dźwignia,1warunek; Las0/0/0. Bramy progresji Cat1–2, Forest0–0. Nie jest to wyłącznie problem rysowania: zawartość nie jest podłączona do definicji/szablonów.

Propozycja do ustalenia: leśne pułapki jako kolczaste korzenie, trujące zarodniki i skażony znak; zamknięte skrzynie; leśne aktywatory bocznych sekretów. Blokady głównej drogi wymagają jawnej decyzji, bo obecny las celowo ich nie generuje. Wysłane jedno zbiorcze pytanie o zakres: pułapki+boczne sekrety / również główne bramy / tylko pułapki. Wygaszanie koron zamrożone na polecenie użytkownika. Bez zmian runtime/assetów/generowania/testów Unity; zapisany powtarzalny audyt `../LocalHandoffs/forest-object-parity-20261020/audit.py` i `audit.json`.

## Las — wejście pod koronę zamiast dystansu od pnia — 2026-10-20 — READY_FOR_SMOKE

Usunięty limit jednego pola od pnia. Początek fade wymaga punktu stóp wewnątrz geometrii liści (tight mesh), a nie samego overlap dużej sylwetki ani bliskości podstawy. Tolerancja wyjścia0,08 pola wyłącznie dla już trwającego fade. Alfa90/255 i czasy zachowane.110/110 Unity PASS; render produkcyjny obejrzany, self-review PASS. Import gotowy bez Refresh. Raport: `../LocalHandoffs/forest-canopy-entry-20261020/REVIEW.md`.

## Las — lokalny zasięg fade koron — 2026-10-20 — ZASTĄPIONE TESTEM WEJŚCIA W KORONĘ

Oprócz faktycznego overlap wymagane maksymalnie jedno pole od realnej podstawy1×1/2×2; dalekie gałęzie nie uruchamiają przygaszania całego drzewa. Alfa90/255 i czasy bez zmian.109/109 Unity PASS, render tego samego lasu przed/po obejrzany, self-review PASS. Import gotowy bez Refresh; raport `../LocalHandoffs/forest-canopy-range-20261020/REVIEW.md`.

## Las — naprawa pozornie znikających koron — 2026-10-20 — READY_FOR_SMOKE

Usunięty wspólny budżet opacity: każda korona ma stałe90/255 zamiast spadku do około18/255 przy sześciu drzewach. Fade/czasy/korzenie/mgła/rozmieszczenie bez zmian.106/106 Unity PASS; nowe testy rzeczywistych pikseli obu shaderów przy1/3/6 drzewach, produkcyjny punkt przed/po obejrzany. Import gotowy, bez Refresh. Self-review PASS; raport `../LocalHandoffs/forest-canopy-opacity-fix-20261020/REVIEW.md`.

## Las — płynna przezroczystość całych koron — 2026-10-20 — READY_FOR_SMOKE

Cała faktycznie zasłaniająca korona przygasa; alfa bazowa 90/255, fade 0,20 s / powrót 0,35 s, korzenie solidne. Wiele warstw dzieli budżet opacity. Lokalne zapytanie tight mesh z głębią i odbiciem sprite'a, bez kwadratów wokół jednostki; drużyna i widoczni żywi przeciwnicy w walce. 100/100 Unity PASS, rendery produkcyjne 0/100/200 ms obejrzane, self-review PASS. Import gotowy bez ręcznego Refresh. Bez zmian statystyk/skali/rozstawu/serialized state; FPS nie zmierzono. Dowody: `../LocalHandoffs/forest-whole-canopy-fade-20261020/REVIEW.md`.

## Las — obrzeża i czytelność — 2026-10-20 — READY_FOR_SMOKE

Ciągła ściółka niezależna od drzew, miękka mgła przechodząca w ściółkę bez ujawniania nieodkrytych ścieżek/obiektów, brak czarnego startu nowego kafla. Zmienny deterministyczny rozstaw i gatunki przy zachowaniu 2×2/1×1; poprawione sortowanie współdzielonej linii głębi, cieplejsze ścieżki i stonowany kontur jednostek. 100 różnych Unity PASS oraz 8 końcowych powtórek, produkcyjne rendery przed/po tego samego lasu obejrzane. Import gotowy bez ręcznego Refresh. Bez balansu/serialized state; FPS nie zmierzono. Dowody: `../LocalHandoffs/forest-edge-legibility-20261020/REVIEW.md`.

## Balans analityczny bez symulowania walk — 2026-10-19 — ANALYSIS COMPLETE

Przeliczone aktualne wzory statystyk/obrażeń, legalne SP8 drzewek, broń+pancerz Common bez biżuterii, EQ0/50/100%, ważone arcydzieła30%/boss co4poziomy,12poziomów1–50 z kotwicą48. Zero walk/AI/MonteCarlo i zero zmian statystyk/mechanik/składów/domyslnejpolityki.13/13testów eksportu/obliczeń PASS,8176wierszy wytrzymałości,544jednostki,1428podglądów burn.38poprzednich źródeł symulatora i1696 chronionych produkcji/assets/cache bez zmianSHA; prawdopodobieństwa/monotonicznośćulepszeń PASS.

L48 EQ0:bohater10,16/10,85atakówfiz/mag,przeciwnik16,38/8,80;EQ50:12,02/13,53 vs13,17/7,15;EQ100:13,87/16,21 vs10,98/6,01. Ulepszenia dają około49/47%offensywy i37/49%przeżywalności. Pierwszy kontakt nie liczy podwójnie efektów aktywnych SP: leczenie/DoT/CC osobno. Mag ma1125PA,Kula844PA:krok+cast944,2kroki+cast1144nie mieszczą się;kusznik ielf mieszczą2kroki+strzał.3trafione elfie strzały3843–5631HP vs4939maga(78–114%,bezcrit). Model idealnego kontaktu Las0,484 vsKat0,553 nie jest prognozą strat/winrate. Mana odnawia się równieżco6s:paladyn15/tick=45przez18s marszu;Heal L48około1864HP/30many;potencjał3skilli w budżecie4tur osobno od bazowej ofensywy.

Rekomendacje jeszcze niewdrożone:zostawić HP/ATKkotwicy48 iobecneulepszenia;test zwykłego ostrzału7zamiast9 albo ograniczenie odwrotu wykonywanego tylko dla zasobu;nie nakładać obu w ciemno. Zachować trudniejsze składy z wyceną salw/kontroli;nie globalnybufflasu na podstawieHP/ATK. Fireball L1wilk84HP,direct31–45+nominalburn96:przeżyciecałego nominalnegocastu wymaga142HP(+69%),więc zakazjednocastowego zabijania nie jest drobną korektą. RarityCommon,wyższe materiały z referencji,brak indywidualnychpasywek unikatów i ekonomii reforgowania jawne w raporcie. Jeden ukrytyUnitynaraz,bezNode/agentów/ręcznegoRefresh/setupu/commit/push. Raport:`../LocalHandoffs/analytical-balance-20261019/REPORT.md`;verification.json,level-*.json,supplement.json,durability.json,healing-budget.json,encounter-proxy.json,SELF_REVIEW.md,scope.diff.

## Regresje Krzyżowca i mieszanego rozwoju v9 — 2026-10-19 — ANALYSIS COMPLETE

Diagnoza bez zmian domyślnej polityki/pasków, statystyk, mechanik, SP, EQ, składów i AI.20/20 jawnych testów PASS:14 neutralnych powtórek7 najgorszych/utraconych tras v8/v9 +6 krzyżowych kontroli paska/decyzji.345 walk,57 szczegółowych; neutralne wyniki/stany/ledgery dokładnie zgodne,0 budżetów.37 wcześniejszych źródeł symulatora,1696 produkcji/assets/cache,3695 wcześniejszych dowodów v9 i7228 v8 zachowują SHA.

Potwierdzone: ogólna flaga repozycji blokuje również potrzebne dalsze podejście/odwrót; gałąź podejścia traktuje Pole Osądu(CreateObject) jak buff i podaje pustą listę konkurencyjnych planów, pomijając wyżej ocenione pole; pasek Krzyżowca na L48 usuwa8SP narzędzi pola. Przykłady identycznego stanu: Jerzy450PA nie kończy odwrotu obniżającego prognozę693→0; paladyn250PA nie robi legalnego podejścia za200PA; L10P2 LasS2 B1T6 pole525PA/score0,07218 wypierane przez ruch300PA+Wyrok525PA/scorezruchem0,04394. Kontrole paska są kontekstowe: L48P7 LasS0 stare decyzje+nowy pasek przegrywają tak jak nowe+stary; S2 nowy pasek ze starymi decyzjami poprawia7→1 powalenie i kończy trasę. P2L48 LasS0:20→122powaleń, maga11→44 przy spadku własnego podpalenia7431→4569; w B9 paladyn24/24tur ma krytycznego pacjenta,11bezmany naHeal. Nie przypisano całego wyniku jednej akcji.

Proponowane, jeszcze niewdrożone: ruch według intencji zamiast blokady każdego kolejnego; porównanie pełnego ruchu+ataku z legalnymi polami/opóźnionym damage/CC; paski pod skład i inwestycje, potem te same trasy przed balansem statystyk. Dodano tylko opt-in audyt; bez setupu/migracji/Node/agentów/ręcznegoRefresh/commit/push, jeden ukrytyUnity. Raport:`../LocalHandoffs/joint-regression-diagnosis-20261019/REPORT.md`;verification.json,summary.json,first-divergences.json,decision-metrics.json,M0–M3,tests-replay.xml,SELF_REVIEW.md,scope.diff.

## Wspólne planowanie ochrony, dobijania i ruchu — symulator v9 — 2026-10-19 — COMPARISON COMPLETE, MIXED RESULTS

Wyłącznie testowe decyzje bohaterów i legalny dobór pasków pod istniejące SP. Bezpośrednie dobicie porównane z ochroną przy zachowaniu pilnego ratowania, tarczy przeciwnika i pozostałego zagrożenia; spójność pozycji do końca tury i legalny ruch+atak zamiast kosztownego buffa poza kontaktem. Trzy jawne warianty P1 (48 pełnych tras); końcowy adaptacyjny pasek zachowuje stary zestaw przed poznaniem potrzebnej kombinacji. Bez darmowych rang/slotów i bez wyjątków per seed/biom. Statystyki, mechaniki, SP, EQ, zasoby, zasięgi, skład encounterów i produkcyjne AI bez zmian.

Te same 112 tras L10/L48 × 7 profili × 2 biomy × 4 seedy, EQ+0, historyczne arcydzieła i realna progresja/zasoby/statusy/1 kit. Ukończenia 91/112 → 93/112, powalenia 1117 → 1191, maga 348 → 423, trwałe zgony 17 → 17, walki 2185 → 2238. Pełna alternatywa 4/16 → 6/16: Katakumby L10 1/4 → 2/4, L48 3/4 → 4/4; Las nadal 0/8. L48 Las S3 druga walka: 98 → 29 tur, 18 → 0 powaleń, 8 → 0 tarcz, 0 → 4 Kule Ognia; dalsze walki nadal przegrywane. Regresje: pojedynczy Krzyżowiec 13/16 → 12/16, mieszane SP 14/16 → 12/16 (L10 7/8 → 4/8, L48 7/8 → 8/8). Bez deklaracji poprawy wszystkich buildów ani pełnego balansu.

170/170 odrębnych końcowych przypadków NUnit PASS; po usunięciu pomocniczego logowania ponowna kompilacja i 35/35 PASS. 4423 ledgery walk przed/po zweryfikowane, 6 dokładnych kontroli v8, 0 wyczerpanych budżetów walki. 1696 chronionych plików produkcji/assets/cache i 7228 wcześniejszych dowodów bez zmian SHA; manifest 224 legalnych pasków/rang. Scoped self-review: 5 istniejących plików testowego symulatora + 3 nowe partiale, bez setupu/save/migracji. Jeden ukryty Unity naraz, bez Node/agentów/ręcznego Refresh/commit/push. Raport: `../LocalHandoffs/simulator-joint-planning-20261019/REPORT.md`; summary.json, verification.json, regression-cases.json, candidate-*/after/control, XML, SELF_REVIEW.md i scope.diff.

## Pełna alternatywna drużyna — synergie i decyzje v8 — 2026-10-19 — ANALYSIS COMPLETE

Diagnoza bez zmian statystyk, mechanik, SP, zestawów, decyzji symulatora, produkcyjnego AI i składów. Analiza112 istniejących wypraw v8 oraz112 świeżych kontroli;9 neutralnych powtórek pełnych tras/135 walk, szczegółowe26 walk, dokładna zgodność wyników/przenoszonych stanów/ledgerów.9/9 NUnit PASS;33 wcześniejsze źródła symulatora,1696 plików produkcji/assets/cache i7228 wcześniejszych dowodów bez zmian SHA.

Potwierdzono nakładanie się pętli ochrony/ratowania z utratą ofensywy i niewykorzystaniem aktywnych synergii. L48 Las S3 druga walka: P1 przegrywa98tur/18powaleń/0zabitych łuczników;8tarcz,1Roztrojenie,0KulOgnia.5osłon przy dostępnych legalnych atakach. P0 wygrywa41tur/0powaleń z11196damage od podpalenia;pojedynczy Czarnoksiężnik65tur/3powalenia,paladyńska zamiana23tury/0. Stany wejścia między profilami różne, brak przypisania całego efektu jednej decyzji. Dowódca0→15→25furii z2Cięć nie uruchamia rozkazów;T36legalneCięcie ustępuje podejściu ratunkowemu. Odwroty/nawroty Jerzego i paladyna;6/11tur paladyna z krytycznym pacjentem zaczyna bezmany naHeal, pozostałe5wyczerpuje ją po1Heal. PreviewHeal1474P0 vs958P1. Poznane transfer/grupowyheal/hak/fala pozostają poza stałymi slotami, częśćSP niewykorzystana w wybranej rotacji. Bonusy Krzyżowca na nieumarłych/demony nie pomagają na elfach/bandytach. Zdrowe walkiL48:Las P1 traci2,78×HP P0, Katakumby0,62×;brak podstaw do globalnego buffa/nerfa pod te wyniki.

Propozycje niewdrożone:porównanie ratowania z eliminacją źródła zagrożenia,spójny plan ruchu do końca tury,wycena buffa/Rytuału kontra podejście+atak,jawne alternatywne legalne paski pod istniejąceSP,potem kontrolowana macierz.Próba4seedów,otwartaarena,fullclear;wynik heurystyki nie jest winrategracza. Self-review zakończony,test-only bezsetupu/save/migracji.Jeden ukryty Unitybatch,bezNode/agentów/ręcznegoRefresh/commit/push. Raport:`../LocalHandoffs/alternative-party-diagnosis-20261019/REPORT.md`;verification.json,decision-metrics.json,decision-examples.json,fresh-controls.json,all-v8-cells.json,replaye,tests-replay.xml,scope.diff.

## Obsługa specjalizacji i porównanie symulatora v7 → v8 — 2026-10-19 — COMPLETE

Wyłącznie testowy dobór aktywnych skilli i decyzje bohaterów. Przegląd8 specjalizacji/160 węzłów, stałe legalne zestawy, pojedyncze zmiany wojownika/Jerzego/maga/paladyna, pełna alternatywa i mieszany rozwój. Produkcyjne planowanie pozycji Pola Osądu i kierunku Roztrojenia Energii, kontekst rozkazów/buffów/transferu i kosztów HP/many, rezerwa krytycznego leczenia. Usunięto spam tarcz wynikający z utożsamiania niskiego HP z pilną ochroną: pilna osłona odwraca forecast powalenia; reszta konkuruje z ofensywą. Bez zmian statystyk, mechanik, SP/inwestycji, EQ, zasobów, zasięgów, składów i AI NPC.

112 par pełnych normalnych wypraw: L10/L48 ×7 profili ×2 biomy ×4 te same seedy, EQ0, historyczne arcydzieła30%/4 poziomy, prawdziwe XP/SP/statusy/zasoby i 1 kit. Ukończenia75/112→91/112 (L10:35/56→43/56; L48:40/56→48/56), trwałe zgony33→17, powalenia1398→1117 (maga514→348), obserwowane walki2060→2185. Pojedynczy Krzyżowiec7/16→13/16, mieszany rozwój8/16→14/16. Pełna alternatywa2/16→4/16 i nadal0/8 Lasu; nie każdy wskaźnik poprawia się: Tancerz więcej powaleń, P0 o1 trwały zgon więcej. Nie uznano wszystkich buildów za zbalansowane ani nie zmieniono statystyk pod słaby profil.

Świeże trudne walki:336 prób,112 na etap (v7/nowy zestaw bez nowych decyzji/v8), wszystkie112/112 wygranych; powalenia27→35→11, maga25→27→8. Dodatkowe72 pary izolują poprawkę tarcz; L48 P0 Las na4 trasach:3→4 ukończenia, powalenia108→6 (maga60→3), tarcze209→32 i KuleOgnia157→218. Odrzucone warianty zachowane z manifestami częściowych wyników.

142/142 końcowych przypadków NUnit PASS;4245 ledgerów pełnych walk+336 świeżych zweryfikowane,6 historycznych kontroli dokładnie zgodnych,0 budżetów.1696 plików produkcji/assets/cache bez zmian SHA, kontrolowany diff8 istniejących źródeł+3 nowe partiale. Użytkownik miał zamknięty Editor: pojedyncze sekwencyjne procesy Unity batch, nigdy równoległe; bez Node/agentów/ręcznego Refresh/setupu/migracji/commit/push. Raport:`../LocalHandoffs/simulator-build-policy-20261019/REPORT.md`; BUILD_REVIEW.md,summary.json,shield-ablation.json,before/,after/,focused/,control/,tests-final3.xml,verification.json,scope.diff.

## Wcześniejsze poziomy i różne buildy — symulator v7 — 2026-10-19 — ANALYSIS COMPLETE

224 porównywane pełne normalne wyprawy: L1/5/10/20/30/40/48 × 4 profile × oba biomy × te same 4 seedy 0–3. EQ+0, zwykła jakość/rarity plus dotychczasowy historyczny model arcydzieł 30% co 4 poziomy, realne legalne SP/XP, przenoszenie HP/zasobów/statusów i 1 zestaw obozowy. P0: Awanturnik/Skrytobójca/Mag Ognia/Kapłan; P1: Dowódca/Tancerz Ostrzy/Czarnoksiężnik/Krzyżowiec; P2 oba drzewa; P4 P0 ze zmienioną ścieżką maga. Bez zmian statystyk, mechanik, składów, produkcyjnego AI ani istniejącej polityki v7.

Ukończenia P0 54/56, P1 16/56, P2 36/56, P4 45/56. Katakumby/Las: L1 15/16 vs 13/16; L5 16/16 vs 15/16; L10 10/16 vs 6/16; L20 i L30 10/16 vs 8/16; L40 11/16 vs 7/16; L48 13/16 vs 9/16. P0 przechodzi wszystkie badane trasy L20–48; P1 na L20/30/40 nie przechodzi żadnej w obu biomach. Duża wrażliwość na build/aktywny zestaw; brak podstaw do globalnej zmiany statystyk bez rozdzielenia siły drzewek od doboru i używania specyficznych czarów. Na L1 profile różnicują się dopiero po awansach; mała próba 4 tras/komórkę, otwarta arena bez geometrii i pułapek, pełne czyszczenie.

151 ukończeń, 73 porażki, 0 nierozstrzygniętych budżetów, 61 trwałych zgonów, 2775 powaleń, 4063 zweryfikowane ledgery walk. 8 kontroli L48 P0 dokładnie zgodnych z wcześniejszym v7 wraz z ledgerami. 57 odrębnych scenariuszy testowych PASS (65 wykonań z powtórkami); 1696 plików produkcji/assets/cache i 29 istniejących plików symulatora bez zmian SHA. Nowy opt-in test i manifest wejść, raporty lokalne. Kolizję nazw diagnostycznych rozwiązano osobnymi folderami; 28 wypraw odtworzono wyłącznie dla eksportu, dokładna zgodność z wynikami macierzy. Automatyczny import w jednym istniejącym Unity, bez ręcznego Refresh, drugiego Unity/Node/agentów/setupu/commit/push.

Raport: `../LocalHandoffs/early-build-expeditions-20261019/REPORT.md`; summary.json, fights.json, build-inputs.json, runs/, cells/, XML i verification.json. Statystyki nadal bez zmian.

## Symulator — podejścia, wspólny focus i bezpieczne podnoszenie v7 — 2026-10-19 — COMPLETE

Domyślna `party-v7-coordination`, wyłącznie decyzje bohaterów w symulatorze. Podejście maga bez strzału respektuje narażenie i DoT; legalny ruch+atak porównuje koszt dojścia i bieżący plan również dla walki wręcz. Focus uwzględnia dostęp drużyny i pozwala zaatakować osiągalną alternatywę. Plan po ruchu jest ponownie walidowany i ustępuje pilnej pomocy. Ręczne podniesienie ocenia ostrzał/realną wcześniejszą osłonę lub leczenie; wyznaczony ratownik zachowuje odpowiedzialność za przygotowanie dojścia, pozostaje awaryjna pomoc. Ochrona obejmuje narażonego healera. Bez zmian produkcyjnych statystyk, mechanik, kosztów, zasięgów, AI i składów.

Te same 24 pełne normalne trasy L48 / EQ0 / realne drzewka i dropy / 1 kit. Las przed→po: ukończenia 12/12→12/12, zgony 1→0, powalenia 235→79, pierwsze 80→47, maga 149→45 (pierwsze 46→22), bez nowej akcji po podniesieniu 62→12, HP 76,4%→88,9%. Katakumby: 11/12→12/12, zgony 2→0, powalenia 44→56, pierwsze 29→44, maga 2→4, HP 80,6%→83,2%, wykonane walki 249→256. Wniosek: duża poprawa Lasu i przeżycia obu biomów, ale więcej powaleń Katakumb; bez deklaracji pełnego balansu.

65 finalnych testów PASS, 502 walki końcowego porównania, 0 budżetów; dwie historyczne kontrole v6 dokładnie zgodne wraz z ledgerami. Dwa wcześniejsze warianty i ich regresje zachowane: naprawiono brak alternatywnego ruchu+ataku, ochrony healera i zbyt szerokie odraczanie przez wyznaczonego ratownika. 1696 plików produkcji/assets/cache bez zmiany SHA, kontrolowany diff narzędzia. Automatyczne importy i testy w istniejącym Unity; bez ręcznego Refresh, drugiego Unity/Node/agentów/setupu/migracji/commit/push. Raport: `../LocalHandoffs/simulator-coordination-20261019/REPORT.md`; summary.json, after/, control/, candidate-*/, scope.diff, XML i verification.json.

## Dlaczego drużyna gorzej wypada w Lesie — 2026-10-19 — ANALYSIS COMPLETE

Bez zmian balansu, mechanik, zasięgów, składów, produkcyjnego AI i domyślnej party-v6. Audyt 495 istniejących walk pełnych tras i 180 świeżych x7; dokładne neutralne powtórki 5 tras / 98 walk, pełna nowa telemetria 11 najgorszych/porównawczych walk. Mag dostaje 30,1% bezpośrednich strat HP od NPC w Lesie vs 6,9% w Katakumbach; 87 vs 2 okna wieloźródłowego ostrzału między jego gotowościami. Fizyczne obrażenia to 96,9% vs 54,7%. Leczenie na walkę 8613 vs 6892 HP, ale 155 vs 15 powaleń jest ponownych.

Potwierdzona luka ruchu: canPrepare dopuszcza śmiertelne podejście maga bez późniejszego czaru. W badanej próbce 8 takich decyzji bez powalonego sojusznika, wszystkie w Lesie; osobno ryzykowny ruch ratunkowy. Nominalny focus strzelca nie zapewnia wykonania wspólnego planu; po zabiciu frontu strzelcy długo utrzymują ostrzał. Brak many na Heal w 30 z 67 tur z krytycznym pacjentem w wybranych walkach Lasu (Katakumby 11 z 25; próba celowo trudna). Ciężkie upiory w Katakumbach również tworzą pętlę ratowania; nie wszystkie problemy to przenoszone rany.

5 finalnych testów PASS; neutralne wyniki i pełne ledgery dokładnie zgodne, 1696 plików produkcji/cache i 26 wcześniejszych źródeł symulatora bez zmiany SHA. Jeden nowy jawny test diagnostyczny, automatyczny import w istniejącym Unity, bez ręcznego Refresh/setupu/commit/push. Raport: `../LocalHandoffs/hero-biome-matchup-20261019/REPORT.md`; summary.json, decision-summary.json, pełne decyzje i replaye, XML, verification.json. Propozycje ruchu/focusu/bufora leczenia pozostają niewdrożone; ich wpływ nie został jeszcze odizolowany kontrolowaną zmianą polityki.

## Całe składy Las vs Katakumby — 2026-10-19 — ANALYSIS COMPLETE

1792walki:56par ×8seedów ×2rozstawienia ×2lustra. L48, standardowe zasoby, produkcyjne9/8kuszników, pełne rzeczywiste składy i jednakowe NPC-AI z obsługą przywołań/pułapek i śmiercią przy0HP. Las755/1792(42.1%), Katakumby1037/1792(57.9%); nierozstrzygnięte0. Ważona rzeczywistymi częstościami punktacja Lasu43.2%. Wynik nie potwierdza ogólnej przewagi puli Lasu; lokalne mocne składy i matchup przeciw bohaterom pozostają osobną kwestią.

20testówPASS, pełna macierz i podzbiór równych liczebności;2dokładne historyczne kontrole.1696chronionych plików oraz wszystkie wcześniejsze źródła symulatora bez zmianySHA. Produkcja/statystyki/zasięgi/składy/AI bez zmian. JedenEditor, automatyczny import, bez ręcznegoRefresh/setupu/commit/push. Raport:`../LocalHandoffs/encounter-versus-encounter-20261019/REPORT.md`;summary.json,battles-*.json,controls/,XML,verification.json.

## Ranged DPS — Katakumby vs Las 1v1 — 2026-10-19 — ANALYSIS COMPLETE

1152pojedynki L48,32sparowane seedy/parę, dystanse3/7/10 i2lustrzane przypisania. Obie strony używają produkcyjnego AI z równym dostępem do ruchu po impet; standardowe zasoby, wszystkie skille. Kościany łucznik wygrywa75%vs kusznik(obecny i7/7),71.9%vs elf. Okultysta100%vs obecny kusznik,99.0%vs7/7,96.9%vs elf. To siła1v1, nie ocena presji kilku NPC na bohaterów; wniosek: sprawdzać wykorzystanie strzelców w składach, bez automatycznego wzmacniania Katakumb.

9finalnych testów PASS,0budżetów, pozycje i zasięg każdego podstawowego strzału zweryfikowane,1152powtórki identyczne po dodaniu telemetrii. Kontrole pełnych tras LasS0/KatakumbyS6 dokładnie zgodne.1696plików produkcji/cache bez zmianSHA. Wspólny Engine ma wyłącznie opcjonalny callback wyniku dla testów; produkcja i balans bez zmian. JedenEditor, automatyczny import, bez ręcznegoRefresh/setupu/commit/push. Raport:`../LocalHandoffs/ranged-npc-duels-20261019/REPORT.md`; summary.json,duels-*.json,controls/,tests-final.xml,verification.json.

## Kusznicy — test zasięgu 7 — 2026-10-19 — COMPARISON COMPLETE, NOT APPLIED

Tylko kandydat w pamięci EditMode: Wystrzał 9→7 i Bełt Przebijający 8→7; wszystkie inne dane i party-v6 bez zmian. Te same 24 pełne normalne trasy L48/EQ0/drzewka. Las: ukończenia12/12→10/12, powalenia235→200, maga149→100, pierwsze maga46→46, HP76.4%→64.3%, zgony1→1; wykonane walki246→236. Patrole125→74powalenia, ale inne postacie86→100 i nowe porażki seed1/5. Wynik mieszany — bez potwierdzenia poprawy pełnych wypraw i bez wdrożenia do gry. Katakumby dokładnie identyczne:11/12,44powalenia,2maga,80.6%HP,2zgony.

9 odrębnych testów PASS;24wyprawy+2dokładne kontrolne Lasu z range9,528walk,0budżetów. Dowód2zmienionych pól zasięgu,1696chronionych plików i12poprzednich źródeł bez zmianSHA. JednoUnity, automatyczny import, bez ręcznegoRefresh/setupu/commit/push. Raport: `../LocalHandoffs/shooter-range-seven-20261019/REPORT.md`; summary.json, verification.json, trajectory-context.json, before/after/control, XML.

## Audyt zagrożenia encounterów Lasu i Katakumb — 2026-10-19 — ANALYSIS COMPLETE

Bez wdrożenia balansu i bez zmian domyślnej party-v6. 360 świeżych walk L48/EQ0 z drzewkami, 12 sparowanych seedów na każdy z 15 składów, otwarcia x7/x5; dodatkowo 495 istniejących walk pełnych tras. Patrol+łowcy generują 210/235 powaleń Lasu i 143/149 maga. Potwierdzone zasięgi 9/9/7/6 oraz wspólne odsuwanie po impet z zachowaniem PA na strzał. Bliższe otwarcie Lasu: 29→4 powalenia; nie jest izolowanym testem zasięgu ani reguły ruchu. Kontrola NPC po odjęciu własnego mage.choleric: Katakumby 51/96 walk, Las 28/84; nie mylić całości hardCC z wrogim CC.

Propozycje: osobne kontrole kusznika 9→7 i wybierania odwrotu; alternatywnie warianty patroli/łowców z jednym strzelcem, stare składy rzadsze. HP/ATK/zasięgi/AI/składy/zasoby/produkcja bez zmian; brak deklaracji pełnego balansu. 4 finalne jawne testy PASS, 0 budżetów, powtórzenia identyczne; 1696 plików produkcji/cache i 12 poprzednich źródeł bez zmian SHA. Jeden Editor, bez ręcznego Refresh/setupu/commit/push. Raport: `../LocalHandoffs/encounter-threat-20261019/REPORT.md`; summary.json, verification.json, tests-final.xml, pełne świeże ledgery.

## Symulator — kontekstowy ruch i leczenie maga v6 — 2026-10-19 — COMPARISON COMPLETE

Domyślna party-v6-mage-recovery. Ruch/odwrót kontekstowo wobec rzeczywistej przewagi zasięgu widocznego przeciwnika: preferencja niższej ekspozycji, brak ryzykownego marszu resztkąPA, ciągłość legalnego ruch+strzał, alternatywne spójne pole zamiast wyłącznie healera. Wspólne leczenie rozpoznaje śmiertelneDoT+ostrzał maga>50%HP i znaną straconą turę kontroli tłumu; rezerwa pomocy obejmuje niepilne wsparcie rannego maga i utility po podleczeniu pod ostrzałem. Pilna pomoc/ostatnia mana/emergency podnoszenia zachowane. Bez zmian statystyk/mechanik/AI przeciwników/focusu/serialized state.

Te same24normalne pełne trasy: startL48/EQ+0%/drzewka/1kit/te same dropy i progresja. Las: ukończenia12/12→12/12, powalenia246→235, mag183→149, pierwsze maga60→46, HP77.4%→76.4%, zgony0→1. Katakumby: ukończenia12/12→11/12, powalenia45→44, mag9→2, HP84.7%→80.6%, zgony0→2. Wszystkie seedy/niekorzystne warianty w raporcie. Nie utożsamiać mniejszego licznika powaleń z przeżyciem ani pełnym balansem.

56UnityPASS finalnej wersji,0budżetów,2dokładne historyczne kontrolnev5;6dodatkowych eksperymentów ról i3neutralne kontrole pierwszego kandydata archiwalne. Sparowane wejścia i ledgery,1672chronione gameplay/assets+24cachetras identyczneSHA. Final source/DLL i self-reviewPASS. Bez ręcznegoRefresh/nowegoUnity/Node/agentów/setupu/migracji/manualnego smoke/commit/push. Raport: `../LocalHandoffs/simulator-mage-recovery-20261019/REPORT.md`, summary.json,verification.json,scope.diff,XML,before/after/control i warianty candidate-*.

## Las — diagnoza regresji powaleń maga v4 → v5 — 2026-10-19 — DIAGNOSIS COMPLETE

L48/EQ+0%/drzewa, wszystkie12 tych samych tras/246walk: mag166→183, pierwsze48→60, powtórki118→123; grupy łowieckie62→92. Powalenia po ręcznej pomocy97→95, bez własnej akcji52→61. WłasnyburnHP323997→282008, ale kończące tiki23→29; głównie ostrzał151/183. Diagnoza nie zmienia statystyk/mechanik/domyslnej party-v5-team.

Neutralne pełne replaye4seedów i7wybranych złych walk, historycznev4, obecnev5, projekcje stan/RNG. Lokalne cofnięcie tylko jednej walki przy tej samej historii/wejściu: seed1/walka19 mag10→1, seed6/walka4 5→0, seed3/walka16 6→6, seed11/walka14 6→8. Nie uzasadnia globalnego rollbacku. Późniejsze walki lokalnych kontroli mają już inne wejścia.

Potwierdzone luki: powrót rannego maga z zerowego ostrzału pod śmiertelną prognozę bez ataku po ruchu (5vs2 w siedmiu wybranych walkach); preferowanie wyłącznie healera odrzuca legalne mniej groźne pola; CriticalPatient wymaga<50%HP maga mimo śmiertelnej łącznej prognozyDoT+strzałów (2576HP vs2801, legalneHeal30, paladyn wydaje81→1many na innych); regeneracja może zużyć rezerwę natychmiastowej pomocy, bo rezerwa osłania utility, nie konkurencyjne wsparcie. Focus nie odejmuje istniejących tikówDoT; osobnego liczbowego wpływu nie izolowano. Próg PlayerNeedsProtection nie zmienił się względem v4.

16UnityPASS, pełna neutralna zgodność, 1672gameplay/assets+24cachetras+8plików mierzonej polityki identyczneSHA; tylko jawny test diagnostyczny i lokalne raporty. Self-reviewPASS, bez ręcznegoRefresh/nowegoUnity/Node/agentów, brak Editor setup/save migration/manual smoke/commit/push. Raport: `../LocalHandoffs/mage-v5-regression-audit-20261019/REPORT.md`, verification.json,decision-summary.json,scope.diff,XML i pełne ledgery. Propozycje poprawienia ruchu/leczenia/focusu pozostają niewdrożone.

## Symulator — wspólne decyzje L48, EQ+0% i drzewa — 2026-10-19 — READY_FOR_SMOKE (narzędzie)

Domyślna polityka `party-v5-team`: rzeczywisty alternatywny plan leczenia przed odroczeniem ręcznej pomocy; emergency i fallback ratowania zachowane. Ochrona ról dystansowych bez progu50%HP, przy odwrocie preferowany healer jako punkt spójności i legalny atak po ruchu. Healer bez many nie goni przeciwnika kosztem oddalenia od krytycznych pacjentów. Focus może reagować na znacznie pilniejsze widoczne zagrożenie; znany śmiertelnyDoT obniża ocenę dodawania burn. Bez zmian statystyk, skilli, kosztów, statusów, podnoszenia, AI przeciwników i serialized state. Historyczne v4 audyty jawnie zachowują wcześniejsze decyzje.

U0: te same24 normalne trasy/seedy, początekL48, nieulepszona broń/pancerz, realne drzewa/SP i historyczne30%dropy bossowe bez ulepszeń,1kit,fullclear. Przed→po: katakumby11/12→12/12 ukończeń,102→45 powaleń,47→24 pierwszych,mag22→9,średnieHP75,5%→84,7%(tylko ukończone82,4%→84,7%),3→0zgonów. Las12/12→12/12,241→246 powaleń,87→96 pierwszych,mag166→183,77,7%→77,4%HP,0zgonów. Po zmianie oba12/12 i bez trwałych zgonów; las pozostaje wyraźnie groźniejszy i problem powaleń maga nie jest zamknięty. Nie ogłaszać pełnego balansu ani ogólnej redukcji powaleń w lesie.

Pierwszy wariant ujawnił złą kolejność odwrotu i pomocy; osobny test zapewnia zachowaniePA/jedynego ratownika. Kolejna korekta spójności usuwa odchodzenie healera od krytycznych pacjentów. Każdy finalny pakiet sprawdzony na pełnych24 trasach; niekorzystne warianty zachowane. Finalnie48 odrębnych UnityPASS,0budżetów,historycznycontrolv4 dokładnie zgodny z baselineU0 poza czasem/GUID. Te same wejścia/składy i ledgery,1672 chronione pliki gameplay/assets,24cachetras identyczneSHA-256; final self-review/kompilacjaPASS. Dane991 walk w finalnym porównaniu(489przed,502po), różnica przez przedwczesną porażkę.

Brak ręcznego setup/migracji/wymaganego smoke gry. Automatyczne importy i testy w istniejącym Unity,bez ręcznegoRefresh,nowegoUnity,Node,agentów. Bez commit/push. Raport: `../LocalHandoffs/simulator-team-policy-20261019/REPORT.md`; summary.json,verification.json,scope.diff,tests-final-*.xml,before/,after/,candidate-initial/,candidate-rescue-guard/,neutralne regression-trace/ i skrypty odtwarzające analizę. Kolejna ocena balansu powinna rozdzielać zagrożenie składów/biomów od nadal przybliżonej polityki ognistego maga.

## Las — audyt powaleń maga, polityka v4 — 2026-10-19 — DIAGNOSIS COMPLETE

Balans nadal niepotwierdzony:179vs22 powalenia las/katakumby mimo24/24 ukończonych wypraw. W lesie mag134:47 pierwszych upadków w różnych walkach i87 powtórek;74 po ręcznej pomocy,46 bez własnej akcji.108/134 kończą strzelcy/łucznicy. Własnyburn kończy17, ale kosztuje282297/749140 utraconegoHP maga(37,7%).174/179 powaleń drużyny z patroli,łowców,trójek łuczników.

Neutralnie odtworzono4 pełne wyprawy z5 najgorszymi walkami(60 powaleń,36maga):1073 obserwacje,4UnityPASS,wyniki dokładnie zgodne z v4. Mag6 razy oddaje całą turę na ręczne ratowanie przed obroną/ofensywą; np.25HP pod ostrzałem podnosi wojownika. Zasięg strzelców9vsMage6, nie zawsze legalny atak.15/32 krytycznych własnych tur paladyna blokuje sama mana przy poprawnychPA/LOS/celu — kolejne realne leczenia przeciążają pulę. Fireball przy259HP i prognozieDoT758 ma nadal najwyższy score; brak pełnego planu przeżycia. Alternatywne decyzje nieizolowane kontrolą, nie przypisywać im liczbowego efektu ani nerfić statystyk na podstawie tego audytu.

Zmiany tylko w jawnym teście/diagnostyce; domyślne decyzje v4 i mechaniki/statystyki bez zmian.1672 pliki gameplay/assets,24 cache tras,9 plików mierzonej polityki identyczneSHA-256; self-reviewPASS. Automatyczny import w istniejącym Unity, bez ręcznegoRefresh, nowegoUnity,Node,agentów. Bez Editor setup/save migration/wymaganego smoke gry/commit/push.

Pełne dowody i propozycje dalszej poprawy decyzji: `../LocalHandoffs/forest-downings-audit-20261019/REPORT.md`, summary.json,decision-summary.json,actions.json,replay/,verification.json,tests-final.xml,scope.diff; odtwarzalne skrypty lokalne. Propozycji napraw nie wdrażano.

## Symulator — budżet wsparcia i grupowe leczenie — 2026-10-19 — READY_FOR_SMOKE (narzędzie)

Domyślne decyzje testowego gracza mają kontekstową rezerwę many na jedno aktywne natychmiastowe leczenie przed opcjonalnymi wydatkami utility. Kwota wynika z realnych kosztów i regeneracji; krytyczne leczenie może wydać ostatnią manę, ataki nadal dostępne. Wyceniane są wszyscy różni odbiorcy grupowego wsparcia, faktyczny rabat PA konkretnego celu oraz koszt RP. Maintenance zachowuje pierwszeństwo Świętej Regeneracji. Bez zmian statystyk, mechanik gry, AI przeciwników, obozów, loadoutów wypraw i zasad podnoszenia.

L48 EQ50, te same24 pełne normalne trasy/seedy i502 składy: katakumby32→22 powalenia,26→16 ręcznych podniesień,średnieHP88,3%→90,8%; las176→179,129→127,średnieHP82,9%→87,8%,mediana90,0%→89,6%.24/24Completed,0 trwałych zgonów,0 wyczerpanych budżetów. Seed6: czwarta walka9→2 powalenia,80→58tur; cała wyprawa35→39 powaleń,831→747tur,29,4%→78,3%HP. To naprawia konkretną decyzję many, nie dowodzi wyrównania biomów ani ogólnego spadku powaleń w lesie; mag nadal134/179. Regresje i poprawy wszystkich seedów zapisane w raporcie, bez odrzucania niekorzystnych wyników.

35 odrębnych przypadków UnityPASS (focused,probe,pełna macierz,historycznycontrol), kontrola v3 seed6 dokładnie zgodna z archiwum poza czasem/GUID. Te same wejścia i ledgery,1672 chronione pliki gameplay/assets i24 cache tras identyczne SHA-256; final self-reviewPASS. Automatyczny import i testy w istniejącym Unity, bez ręcznegoRefresh, nowego procesuUnity, Node lub agentów. Brak ręcznego Editor setup/prefab wiring/save migration; zmiana dotyczy wyłącznie symulatora EditMode. Nie wymaga smoke walki w grze. Bez commit/push.

Dowody i odtworzenie: `../LocalHandoffs/simulator-support-budget-20261019/REPORT.md`, summary.json,baseline.json,verification.json,scope.diff,tests-final-*.xml,expeditions-v4-*.json,trace-*.json,control/,analyze.py,report.py. Domyślna wersja polityki `party-v4-support-budget`; stare audyty v3 jawnie włączają `previousSupportPolicy:true`.

## Seed6 — potwierdzona blokada leczenia przez manę — 2026-10-19 — DIAGNOSIS COMPLETE

Neutralny replay dokładnie odtwarza poprzedni seed6;847 obserwacji bez zmiany stanu/RNG. W25 krytycznych własnych turach paladyna w4 badanych walkach12 razy leczenie maga blokuje mana przy poprawnymPA/zasięgu/LOS. Czwarta walka:100many→regeneracja50→błogosławieństwo40→10, następna tura25many przy koszcie leczenia30 i powalonym magu. Polityka nie rezerwuje zasobu ratowania.

Jedyny eksperyment pomija Błogosławieństwo w turze8 czwartej walki, bez dopisywania zasobów lub zmian reguł. Pierwsze3 walki i wejście do4. identyczne. Paladyn ma65 zamiast25many w turze18, może wykonać dwa legalne leczenia, mag4761HP. Powalenia4.walki9→2, całej wyprawy35→20, tury831→730, końcoweHP29,4%→86,8%, obie próbyCompleted bez zgonów. Późniejsze RNG/przebiegi rozchodzą się; nie jest to jeszcze globalnie zweryfikowana reguła rezerwy ani wdrożona nowa polityka. Dodatkowo wycena wsparcia słabo agreguje korzyść grupowego leczenia i nie wycenia analogicznie mana/PA; wpływu tego osobno nie izolowano.

24 przypadkiPASS (22 regresje+2 audyty),1672 pliki gameplay i24 cache tras bez zmian. Kod zmieniony wyłącznie w narzędziu testowym — obserwatory i jawna kontrola. Automatyczny import w tym samym Unity bez ręcznegoRefresh. Brak Editor setup/migracji/manualnego smoke. Dowody: `../LocalHandoffs/seed6-decisions-20261019/REPORT.md`, verification.json, decisions.json, counterfactual/, scope.diff, XML. Nie zmieniono balansu ani domyślnych decyzji.

## Seed6 — uzupełnienie diagnozy lasu — 2026-10-19

Read-only:22 walki/64 wrogów,831 tur wobec mediany611.28/35 powaleń z dwóch wczesnych patroli i finałowej grupy łowieckiej. Mag po3. walce3200HP, po podróży odtworzone610HP przed obozem (2593 po40%kit), co tłumaczy kit przed4. walką. W środku odzyskuje do92,1%HP, lecz finał patrol→wilki→bleed→bandyci→łowcy kończy na29,4%. Wcześniej seed miał55 powaleń i873 tury, mimo89,9%końcowegoHP; nie traktować dobrego finiszu jako lekkiej wyprawy. Składy L48 bez osobnego mnożnika seedu; wpływ RNG, unikatowej glewii i alternatywnych decyzji nieizolowany. Dowody: `../LocalHandoffs/forest-health-cost-20261019/SEED6.md`, seed6.json. Bez zmian kodu/balansu, nowych testów/importu/Refresh.

## Koszt zdrowotny lasu — 2026-10-19 — DIAGNOSIS COMPLETE, read-only

Zbadane archiwalne48 wypraw L48/EQ50 (24 sparowane trasy,1004 walki), bez nowych przebiegów/importu Unity i bez zmian gameplayu. Spadek średniego końcowego HP lasu87,8%→82,9% jest zdominowany przez seed6:89,9%→29,4%, wkład około−5,04pp do średniej12. Pozostałe przypadki łącznie kompensują około+0,17pp; mediana90,4%→90,0%, diagnostycznie bez seed6 średnia87,6%→87,8%. Oficjalne wyniki nadal zawierają wszystkie12. Finisz seed6: patrol92,1%→74,3%, wilki71,0%→39,4%, podróż32,3%, para bandytów odrabia do53,4%, grupa łowiecka kończy na29,4%; mag1HP,14 powaleń w ostatniej walce. Jeden kit zużyty przed4. z22 walk, bez kolejnego na końcówkę. Nie przeprowadzono kontroli oszczędzania kitu ani dokładnej walidacji odrzuconych planów leczenia; nie przypisywać konkretnego braku leczenia pewnemu błędowi LOS/PA/AI.

Bieżący las: patrole bandytów łącznie+154811HP deficytu podczas34 spotkań. Elfi łucznicy i bandyccy strzelcy1042964/1347429HP=77,4% bezpośrednich obrażeń wroga. Bilans wszystkich12 wypraw: walki−33558HP deficytu, przejścia z obozami+96796HP, koniec63238HP. Katakumby−37755+81239=43484HP. Przejścia bez obozu w lesie: mag138056HP strat netto w pogarszających przedziałach, paladyn37746HP; dominują pozostawione burn/bleed. W katakumbach większe wzrosty HP od przeniesionej Świętej Regeneracji:55082 vs37773HP bez obozów, sam wojownik44500 vs14148. To zyski/straty netto przedziałów, nie surowe sumy poszczególnych tików. Przejścia po zmianie statusów poprawiły się108395→96796; pogorszenie końcowego deficytu lasu18030HP wynika z29629HP słabszego odrabiania podczas walk, częściowo skompensowanego poprawą podróży11599HP.

Spójność:48 dokładnych bilansów stanów HP,502 obecne ledgery leczenia/obrażeń; brak zmian zmierzonego kodu,1667 pozostałych chronionych plików i24 cache tras potwierdzone SHA-256. W501/502 walk koszt netto zgadza się dokładnie z obrażeniami minus leczenie i ręczne1HP; pozostała różnica1HP. Brak arytmetycznie zagubionego dużego kosztu. Obecna arena12×10 bez geometrii/pułapek/bossów/zaskoczenia; wynik końcowy zależy od ostatnich składów i nie ma dodatkowej darmowej fazy leczenia po finałowej walce. Nie uzasadnia globalnego nerfu. Brak Editor setup i wymaganego manual smoke; nowych testów Unity nie uruchamiano (obowiązuje wcześniej zweryfikowany kod95PASS).

Dowody: `../LocalHandoffs/forest-health-cost-20261019/REPORT.md`, analysis.json, battle-costs.json, travel-costs.json, odtwarzalny analyze.py. Bez Refresh, nowego Unity, Node, agentów, commit/push.

## Wygasanie statusów podczas powalenia — 2026-10-19 — READY_FOR_SMOKE

Zatwierdzona zmiana mechaniki: każda pominięta własna tura powalonego zużywa turę wszystkich czasowych statusów oraz osobnych warstw tarczy. Dotyczy też buffów/debuffów i kontroli; paraliż nie zatrzymuje zegarów podczas powalenia. Persistent/combat-scoped zachowują istniejący lifetime. DoT zużywa pominiętą część RemainingTotalDamage, bez obrażeń, trafień, procesorów, rozprzestrzeniania, pasywów, leczenia i RNG; nie przyspiesza trwałej śmierci. Regeneracja/sen tracą czas, bez automatycznego podnoszenia lub zasobów. Podniesienie nadal zachowuje pozostałe statusy i daje1HP, bez oczyszczenia lub nadrabiania tików. Rzeczywiste ataki na powalonego i3 tury do śmierci bez zmian. Eksploracja używa zwykłych6s, bez odliczania trwałej śmierci poza walką.

Zmiana w istniejącym lifecycle, status ownerze i kolejce tur. Produkcyjny CombatExecutionDriver i symulator używają tej samej ścieżki; zachowany też domyślny owner dla innych konsumentów CombatTurnSystem. Bez zmian serialized Unity state, formatu zapisów, assetów/statystyk/kosztów/drzewek, AI i decyzji polityki party-v3-rescue. Zmieniono jedynie komentarz prognozy ratowania, nie jej reguły.

Przeliczone te same24 normalne pełne wyprawy L48/EQ50%, seedy0–11 na biom, te same502 walki i dane startowe. Katakumby: powalenia39→32, mag14→8, ręczne podniesienia33→26, końcowe HP85,3%→88,3%. Las: powalenia198→176, mag155→128, Zwiadowca32→38, Paladyn0→2; ręczne ratowania138→129, upadki po nich105→92, bez własnej akcji61→50, po ostatnim trafieniu na1HP48→59. Powalenia własnym podpalenieniem: Katakumby8→2, Las31→13. HP lasu87,8%→82,9%; mniej powaleń nie oznacza jednoznacznie niższego kosztu zdrowotnego. Własny burn HP brutto wzrósł w obu biomach. Razem237→208 powaleń; wszystkie24 wyprawy przed i po ukończone bez trwałych zgonów, bez limitów. Las nadal ma znacznie większą presję ratowania.

95 odrębnych końcowych przypadków PASS:89 focused/regression +6 partii macierzy. Zachowany pierwotny FAIL nowej fixtury bez ShieldPool, poprawione wyłącznie przygotowanie tego testu. Wszystkie502 ledgery obrażeń/leczenia zgodne z wynikami. SHA-256:1667/1672 chronionych plików niezmienionych; pozostałe5 to wyłącznie właściciele mechaniki;24 cache tras bez zmian. Polityka decyzji niezmieniona, finalne źródła macierzy potwierdzone. Arena12×10, bez geometrii/pułapek/bossów/zaskoczenia, realny dalszy XP/SP. Wyniki nie są szansą zwycięstwa gracza.

Import/testy automatycznie w tym samym otwartym Unity, bez Ctrl+R, nowych procesów Unity/Node, agentów, commit/push. Brak ręcznej konfiguracji ani migracji. Manual gameplay smoke NOT RUN. Smoke: powalić postać z kilkoma turami burn/bleed i buffem lub kontrolą; po jej pominiętej turze sprawdzić krótszy czas,0HP i zwykły licznik śmierci, następnie podnieść i sprawdzić brak oczyszczenia oraz brak nadrabiania tików. Poza walką statusy tracą czas co6s, postać pozostaje powalona do interakcji/leczenia.

Dowody: `../LocalHandoffs/downed-status-expiry-20261019/REPORT.md`, summary.json, downs.json, verification.json, scope.diff oraz surowe trace/XML.

## Ratowanie i ochrona maga w symulatorze — 2026-10-19 — VERIFIED, wynik mieszany

Domyślna polityka testowego symulatora party-v3-rescue: krytyczne leczenie przed podniesieniem, pilność według rzeczywistego terminu AV, wcześniejsze podejście wyznaczonego pomocnika, darmowe odzyskanie dostępnego własnego sztyletu przed podniesieniem kończącym turę oraz wycofanie zagrożonego maga poniżej połowy HP bez automatycznego powrotu pod ostrzał. Prognozy używają aktualnych legalnych zasięgów/LOS, PA i zasobów, przywracają projektowany stan i nie rzucają RNG. Możliwy ostrzał jest miękką oceną, a nie założeniem pewnego focusu. Odłożenie podniesienia wymaga konkretnego planu leczenia przed trwałą śmiercią; bez niego dopuszczone jest ratowanie mimo ryzyka DoT. W macierzy licznik odłożonych decyzji wyniósł 0 — nie przypisywać mu efektu poprawy tras.

24 sparowane normalne pełne wyprawy L48 EQ50%, seedy 0–11 na biom, te same 502 walki/składy/cache/start/drzewka/dropy. Wszystkie ukończone bez trwałych zgonów i bez limitów symulacji. Las: powalenia 224→198, mag175→155, ręczne ratowania163→138, ponowne upadki po nich113→105, bez własnej akcji86→61, ostatnie trafienie na1HP71→48. Katakumby: powalenia26→39, mag6→14, ręczne ratowania17→33. Razem250→237 powaleń. HP przed questem Katakumby86,8%→85,3%, Las87,3%→87,8%. Las nadal ma około5,1× więcej powaleń; nie deklarować wyrównania biomów ani uniwersalnej poprawy decyzji. Własne podpalenie maga w lesie zabrało więcej HP brutto286880→301916 mimo spadku liczby jego ostatnich trafień34→31.

Pełny licznik leczenia obserwuje faktycznie odzyskane HP CombatRecoverySystem także bez feedback bus, bez podwójnego liczenia i z odłączeniem przy Dispose. Kontrole poprzedniej polityki odtworzyły wszystkie inne wyniki dokładnie; brakujące wcześniej leczenie seed0 Katakumby34868HP, Las26119HP. Trzy zmienione pliki produkcyjne zawierają wyłącznie nietrwałą obserwację leczenia i etykietę mage.fire_feeding. Pozostałe1669 chronionych plików oraz24 cache tras bez zmian, finalny kod macierzy potwierdzony SHA-256. Nie zmieniono statystyk, mechanik/statusów, kosztów, produkcyjnego AI ani składów.

51 odrębnych ostatnich wyników PASS:38 funkcjonalnych/regresyjnych,5 trudnych tras kontrolnych,2 kontrole raportowania i6 partii finalnej macierzy. Rejestry HP/obrażeń/leczenia zgodne dla545 walk porównania i kontroli raportowania. Wcześniejsze odrzucone próby zachowane w attempt-1/attempt-2 i XML, bez mieszania ich z finalnymi danymi. Otwarta arena12×10, normalne pełne czyszczenie bez geometrii/pułapek/bossów/zaskoczenia; początkoweL48 i rzeczywisty dalszy XP/SP. Heurystyka tych buildów, nie prawdopodobieństwo zwycięstwa gracza.

Brak ręcznego Editor setup i wymaganego gameplay smoke. Import/testy w tym samym otwartym Unity, bez Ctrl+R, dodatkowych procesów Unity/Node, agentów, commit/push. Propozycja wygaszania statusów podczas powalenia została tylko omówiona — NIE wdrożona.

Dowody: `../LocalHandoffs/rescue-mage-policy-20261019/REPORT.md`, `summary.json`, `verification.json`, `scope.diff` i surowe trace/XML obok.

## Diagnoza 224/26 powaleń — 2026-10-19 — VERIFIED, bez zmian balansu

Dokładny replay 24 normalnych pełnych wypraw L48 EQ50, seedy 0–11 na biom; 502 walki. Odtworzone wszystkie dane wynikowe poza czasem/GUID instancji. Las: Mag175, Zwiadowca38, Wojownik8, Paladyn3; Katakumby: 6/2/18/0. Grupa łowiecka elfów102, patrol bandytów81, łucznicy27 = 210/224. Pierwsze upadki bohatera w konkretnej walce 78/19, kolejne146/7 (Las/Katakumby).

Własne podpalenie maga: 286880/388118 faktycznie utraconego HP brutto, 34/1 ostatnich trafień; tylko5/1 pierwszych upadków. 26 upadków lasu wspólne z ręcznym podniesieniem. Ponowne upadki po ręcznym ratowaniu113/7, przed własną akcją86/5, na ostatnim1HP71/5. Łącznie ręczne ratowania163/17, ratowania leczeniem61/9. Statusy pozostają po StandUp; DoT może powalić przed działaniem. ExtinguishBurns rozlicza fire_feeding, nie usuwa statusów. Leczenie fire_feeding omija bus feedback: stare sumy Heal nie obejmują całego leczenia, dlatego nie traktować obrażeń brutto jako kosztu netto ani braku feedbacku jako braku leczenia.

Dodany tylko opt-in rejestr diagnostyczny w testowym symulatorze. Statystyki, AI, decyzje symulatora, skille, drzewka i assety produkcyjne bez zmian. 8 przypadków PASS, zero limitów; 1672 pliki chronione i24 cache tras bez zmian. Ten sam otwarty Editor, automatyczny import, bez ręcznego Refresh/Node/agentów/dodatkowego Unity. Brak ręcznej konfiguracji ani wymaganego gameplay smoke; nie wdrożono poprawki mechaniki ratowania.

Raport: `../LocalHandoffs/knockdown-attribution-20261019/REPORT.md`; ledger, replay parity, verification i scope.diff obok.

## Domyślna polityka symulatora party-v2 — 2026-10-19 — VERIFIED

Wdrożone domyślnie: wielokrotne legalne podnoszenie z zakończeniem tury, pierwszeństwo leczenia powalonego, ruch dystansowców z planem ataku i rezerwą rzeczywistych PA, pozycja strzelecka zamiast automatycznego podchodzenia, zdobywanie impetu ruchem i odzyskiwanie sztyletów, wspólny widoczny focus z premią tylko w ocenie planu. Produkcyjne statystyki/skille/AI oraz drzewka bez zmian. Jawny przełącznik legacy zachowuje historyczne kontrole diagnostyczne.

72 sparowane normalne full cleary L48 (2 biomy × EQ 0/50/100% × 12 oryginalnych seedów), 1468 walk, zero limitów. EQ0: Katakumby 11/12→10/12 ukończonych (bez zgonu 8→10); Las 1/12→10/12 (bez zgonu 0→9). EQ50/100: oba lochy po 12/12 ukończonych bez zgonu. HP po: EQ0 71,8%/65,8%, EQ50 86,8%/87,3%, EQ100 89,4%/86,6% (Katakumby/Las, przed questem). Las nadal ma znacznie większy koszt ratowania: EQ50 224 powalenia i 163 ręczne podniesienia wobec 26/17 w Katakumbach. Podobne wyniki końcowe nie dowodzą równej presji ani pełnego balansu wszystkich buildów/geometrii.

32 najnowsze odrębne przypadki PASS, w tym 18 macierzy wypraw. Replay legacy odtworzony; 0 rozbieżności startowych danych/loadoutów/rosterów; 24 cache tras i 1672 chronione pliki kodu produkcyjnego/definicji/arkusza bez zmian. Kod polityki po pomiarze niezmieniony; końcowy import rozszerzył tylko testy. Otwarta arena 12×10, bez bossów/pułapek/zaskoczenia; manual gameplay NOT RUN. Poprzednich wyników legacy nie traktować jako aktualnej kalibracji party-v2.

Raport: `../LocalHandoffs/simulator-player-policy-20261019/REPORT.md`. Brak ręcznego Editor setup. Importy i testy w tym samym otwartym Unity, bez Ctrl+R, dodatkowego Unity, agentów, Node, commit/push.

## Diagnoza encounterów Lasu vs Katakumb — 2026-10-19

DIAGNOSIS COMPLETE; bez zmiany statystyk, rosterów, skilli i produkcyjnego AI. 21 odrębnych testów PASS; 660 unikatowych świeżych walk + 120 powtórzeń baseline, 40 pełnych wypraw / 752 walki; zero limitów. 1672 chronione pliki kodu produkcyjnego, assetów i arkusza bez zmian. Domyślna polityka wcześniejszych audytów zachowana; nowe kontrole są jawnie opt-in.

Największe potwierdzone przekłamanie: symulator pozwalał ręcznie podnieść jednego sojusznika najwyżej raz na encounter, choć produkcja nie ma limitu. Kontrola bez limitu przy L48/EQ0: Las 0/4→3/4 ukończonych i bez zgonu; Katakumby 4/4→4/4 (bez zgonu 2/4→4/4). Presję generują szczególnie patrol 2 rzezimieszków+2 strzelców, łowcy i 3 łuczników; silna zależność od rozstawu/ruchu i koncentracji obrażeń na magu. Druidy/drzewa nie są głównym źródłem strat w tej próbce. Sam zakaz podchodzenia dystansowców nie jest uniwersalną poprawką — pogarsza część katakumb i zmienia generowanie impetu.

Przed dalszym globalnym tuningiem: urealnić ratowanie i planowanie ruch+akcja w symulatorze, porównać kilka otwarć/geometrii i buildów, dopiero potem oceniać częstość/mieszankę ciężkich składów. Otwarta arena 12×10, bez bossów/geometrii/pułapek; 4 sparowane seedy kontrolne, nie szansa wygranej człowieka. Kontrole nie są nowym domyślnym AI. Raport: `../LocalHandoffs/forest-encounter-diagnosis-20261019/REPORT.md`.

Brak ręcznego Editor setup; import i testy w tym samym otwartym Unity, bez Ctrl+R. Manual gameplay NOT RUN (karta analityczna). Bez commit/push.

## Ulepszenia i reforgowanie — 2026-10-19 — READY_FOR_SMOKE

Wdrożono maksymalnie +50% ATK broni, +150% obu obron pancerza oraz połowę bonusu rzadkości do obrażeń krytycznych przedmiotu. Pozostałe skalowanie rzadkości, ekonomia, sockety, definicje NPC i drzewka bez zmian. Podglądy kowala korzystają ze wspólnego przeliczenia instancji; zapis zachowuje bazowe modyfikatory i postęp, a stare podstawowe statystyki bez bazy są odzyskiwane według poprzedniej reguły.

284 odrębne końcowe testy PASS. W tej serii wykonano 720 prób wypraw / 12323 walk:104 / 1947 przed zmianą dla kontroli reforgowania oraz616 / 10376 po zmianie.464 próby po zmianie mają sparowany punkt odniesienia, w tym zachowane archiwum wcześniejszego Common. Dodatkowo 96 wypraw rozszerza L48 o seedy 4–11, a 56 kontroluje zmianę tylko priorytetu drzewka maga.0 przekroczeń limitów; 72 bazowe wyprawy Common/EQ 0% odtwarzają dokładnie poprzednie działania, obrażenia, HP i wyniki.884 chronione definicje i arkusz pozostają bez zmian.

L48 Common, podstawowy profil drzewek, normalna długość i pełne czyszczenie: EQ 0% kończy 12/24 prób (8/24 bez trwałej śmierci), EQ 50% 19/24 (14/24), EQ 100% 21/24 (16/24). W tym profilu pozostaje duża różnica biomów: EQ 0% katakumby 11/12, las 1/12; EQ 100% odpowiednio 12/12 i 9/12. Wymaga osobnej diagnozy składów i decyzji heurystyki, nie automatycznego podnoszenia globalnych statystyk NPC.

Uwzględniono produkcyjne aktywne skille, drzewka, DoT, leczenie, tarcze, koszty/regenerację zasobów, XP i obozy. Mana maga w kontrolach Common/Legendary/Godly wynosi 182/220/267. W kontrolowanym Common EQ 100% z samą zmianą maga Fire kończy 3/4 (2/4 bez trwałej śmierci), Warlock 4/4 (3/4); przy EQ 50% Warlock kończy 4/4 mimo trwałego zgonu maga w każdej próbie. Zwycięstwa nie zastępują oceny strat. Legendary/Godly całej drużyny to kontrola siły EQ, bez modelowania budżetu złota na reforgowanie.

Stała arena 12×10, heurystyka bohaterów, brak biżuterii/galdurytów/pułapek/bossów; wysokie materiały pozostają projekcją fixture arkusza. Liczby opisowe, nie prawdopodobieństwo wygranej człowieka. Manual gameplay NOT RUN. Brak Editor wiring; automatyczny import i testy w tym samym Unity 34144, bez ręcznego Refresh, Node, subagentów, drugiego procesu Unity, commit/push.

Dowody: [raport](<C:/Users/Patryczku/Documents/Godmist/LocalHandoffs/equipment-balance-expeditions-20261018/REPORT.md>), [weryfikacja](<C:/Users/Patryczku/Documents/Godmist/LocalHandoffs/equipment-balance-expeditions-20261018/verification.json>), summary.json, scope.diff i archiwalne XML w tym samym katalogu.

## PT-06 — wczesne zagrożenie i zachowanie punktu L48 — 2026-10-18 — READY_FOR_SMOKE

Po decyzji użytkownika podniesiono początkowe zagrożenie zwykłych przeciwników, zachowując aktualny baseline końcowych poziomów. 14 definicji: baza HP ×0,44 zamiast ×0,40 oryginalnej bazy (około +10%), ATK ×0,63 zamiast ×0,60 (około +5%); wczesny przyrost HP/ATK ×1 zamiast ×0,6. Końcowe przyrosty HP×2/ATK×2,5 pozostają zachowane. Dodatkowa baza jest płynnie kompensowana offsetem według dotychczasowej krzywej q, a różnica wczesnego przyrostu ma wagę C×(1−q). Na L48 i L50 korekta ma dokładnie zerowy wpływ. Globalne współczynniki 1/2/3/5/9 nie były zmieniane w tym passie; zgodnie z decyzją użytkownika można je dalej stroić.

Bohaterowie, drzewka, wyposażenie, odporności, obrony, zasoby, nagrody oraz własne statystyki bossów/faz i rycerz 3HP bez zmian. Trzy opcjonalne pola płaskich offsetów w CharacterGrowthData domyślnie mają zero; brak nowych pól save. Restore nie leczy zapisanych przeciwników. Wzmocnienie całego HP należy sprawdzać na nowej wyprawie.

Końcowe 137/137 EditMode PASS, osobny sweep 4/4 PASS. 144 wyprawy przed/po / 1421 walk, te same trasy i seedy, legalne drzewka i wyposażenie, realne HP/zasoby/leczenie/XP przenoszone między walkami, krótkie wyprawy bez obozów. L1 full clear: 7/8 → 6/8, średnie końcowe HP 46% → 34%; scouting: 7/8 → 5/8, HP 51% → 31%. Dwa nowe seedy L1: 8/8 → 7/8. L5–50 wszystkie planowane serie ukończone. 16 par L48/L50 ma identyczne działania, HP, zasoby i wyniki. Odrzucone mocniejsze warianty wyczerpywały drużynę na L1; surowe wyniki i timeout pierwszej zbyt dużej macierzy zachowano, końcową macierz podzielono per poziom bez rozluźnienia budżetu walk.

Audyt chroni 26 plików i 16 pozostałych definicji; zmieniono wyłącznie dozwolone pola HP/ATK 14 zwykłych NPC. Self-review PASS. Weryfikacja w pojedynczym procesie Unity naraz, bez ręcznego Refresh, skryptów Node, agentów, scen/prefabów/PPU i commit/push. Wysokie progi wyposażenia nadal z read-only fixture arkusza. Symulacje używają stałej areny i jednej polityki bohaterów, nie odtwarzają faktycznej geometrii/pułapek i nie są gwarancją win rate wszystkich buildów. Manual gameplay/FPS NOT RUN. Dowody: `LocalHandoffs/early-enemy-threat-20261018/{HANDOFF.md,verification.json,tests-final.xml,tests-grid.xml,source-changes.diff,asset-changes.diff,durability.md,expeditions-before.json,expeditions-after.json}`.

## Drzewa 1×1/2×2 i odsłanianie przeciwników — 2026-10-18 — READY_FOR_SMOKE

Duże drzewa są teraz rzeczywistymi obiektami 2×2: cztery pola zajętości w walce, nawigacji i LOS oraz collider o tych samych granicach. Natywna grafika jest zakotwiczona na południowej krawędzi footprintu; bez rozciągania sprite’u lub zmiany PPU. Ten wpis zastępuje poprzednią decyzję o samym buforze rozmieszczenia 2×2 przy jednopolowej kolizji.

Na obrzeżach najpierw rozmieszczane są duże drzewa, potem nowe kompaktowe 1×1 w pozostałych wolnych polach dwóch najbliższych rzędów. Nie naruszają rezerwacji dużych drzew ani terenu chodzenia; dodanie/wyłączenie uzupełnienia nie zmienia pozycji dużych. Nowy sprite powstał w imagegen według referencji dojrzałego dębu, następnie przeszedł kontrolę obrazu i natywne przygotowanie 80×128 / 32 PPU / Point. Korzenie mieszczą się w jednym polu. Małe drzewa są osadzone we własnej komórce, bez przesuwania korzeni na przejście.

Lokalne odsłanianie koron obejmuje teraz również widocznych, żywych przeciwników uczestniczących w walce. Patrol poza walką i przeciwnik ukryty przez mgłę nie odsłaniają koron. Sylwetki pozostają nieprzezroczyste i sortowane według stóp; shader odsłania wyłącznie pobliskie liście, zachowując korzenie. CPU/GPU używają zgodnych buforów maksymalnie 32 jednostek. Lista przeciwników jest czytana pętlą indeksowaną, bez nowego iteratora/alokacji na klatkę.

Kontrola wykryła odcięte pole na seedzie 4205 po powiększeniu drzewa. Wybór pozycji w szablonach uwzględnia cały footprint, pierścień wolnego miejsca, pozostałe obiekty i spójność dostępnego terenu. Drzewa bez bezpiecznego miejsca nie są dodawane wewnątrz wąskiej polany. Część pozostałych propsów i skrzyń otrzymała nowe pozycje podczas normalnego autorowania, by zachować wolne miejsca. Kontrole połączeń przeszły dla dziesięciu seedów. Ta walidacja odbywa się podczas importu/autorowania, nie w pętli gry.

Ostatnie wyniki 315 odrębnych testów PASS: zajętość/collidery/ruch z czterech stron, połączenia polan, navmesh, synchronizacja walki, zapis/odczyt, mgła, wyłączne rezerwacje, kolejność uzupełniania oraz rzeczywiste piksele shaderów także dla jednostki poza pierwszymi czterema slotami. To zestawienie ostatnich wyników, nie deklaracja jednego bezbłędnego przebiegu: pełny przebieg przed drobną kontrolą alokacji dał 315/315, końcowy 314/315, a niezwiązany test podejścia do niszczonej przeszkody w noclip raz zawiódł podczas szukania trasy. Powtórzenie całej jego fixtury na niezmienionym kodzie dało 14/14. Zachowano oba surowe raporty; tests.xml scala ostatni wynik według pełnej nazwy bez podwójnego liczenia testów. Niestabilność nie została ukryta ani naprawiana poza zakresem tej karty.

Obejrzano produkcyjny las seed 171026, zwykłą/znaną/zapamiętaną/debug mgłę oraz rendery kontrolne odsłaniania przeciwnika. Self-review i whitespace kodu PASS; natywne spacje serializatora Unity pozostawiono i opisano osobno. Brak nowego pomiaru FPS.

Import gotowy w otwartym Unity, bez ręcznego Refresh. Smoke: rozpocząć NOWĄ wyprawę do lasu, sprawdzić zajętość czterech pól i przeciwnika pod koroną podczas walki. Format zapisu bez zmian; stare pozycje zapisanych przeszkód nie są przenoszone do poprawionych kotew. Dowody: `LocalHandoffs/forest-footprints-gapfill-combat-20261018/{REVIEW.md,verification.json,tests.xml,scoped-diff.patch,new-assets.txt}` i `production-*.png`. Bez ręcznego YAML, nowych procesów Unity/Node, agentów, commit/push.

## Nowe drzewa z rezerwacją 2×2 — 2026-10-18 — READY_FOR_SMOKE

Przywrócono nowe dojrzałe grafiki: dąb, wiąz, świerk, obumarły wariant z pustym pniem i rzadki olbrzym. Usunięto zastępowanie ich starymi niskimi drzewami przy ścieżkach i wewnątrz pokoi. Ten wpis zastępuje decyzję o kompaktowych starych grafikach z poprzedniego passa.

Na obrzeżach każde duże drzewo rezerwuje wyłączne 2×2 wolnych pól poza terenem chodzenia. Przy braku miejsca generator pomija pozycję; nie wstawia małego zamiennika. Duże korony nie zakrywają centralnych 60% pól przejścia. Jasność dalszych drzew uwzględnia również koronę wysuniętą w stronę brzegu.

W pokojach drzewa otrzymały rezerwację 2×2 w systemie rozmieszczania obiektów. Rezerwacja ogranicza zagęszczenie, nie powiększa fizycznego pnia ani taktycznego footprintu. Pozycje istniejących przeszkód w zapisach nie są przesuwane; nowy rozstaw wnętrza dotyczy nowo generowanych wypraw. 32 PPU, skala postaci, lokalna przezroczystość liści, nieprzezroczyste korzenie i pamięć mgły zachowane.

225/225 odrębnych testów PASS, zero fail/skip: rezerwacje i konflikty obiektów, ściany wewnętrzne, zapis/odczyt, destrukcja przeszkód, import artu, mgła i odsłanianie koron. Rendery produkcyjnego lasu (seed 171026) obejrzane. Self-review i whitespace PASS. Referencje assetów zastosowano przez Editor API; bez ręcznej edycji YAML. Nowego pomiaru FPS nie wykonano.

Import gotowy w otwartym Unity, bez ręcznego Refresh. Smoke: rozpocząć nową wyprawę do lasu i sprawdzić nowe drzewa oraz ścieżki szerokości dwóch pól. Dowody: `LocalHandoffs/forest-new-trees-2x2-20261018/{REVIEW.md,verification.json,tests.xml,scoped-diff.patch}` i `production-*.png`. Bez commit/push.

## Duże drzewa 2×2 i miejsce na ścieżkach — 2026-10-18 — READY_FOR_SMOKE

Wszystkie duże warianty drzew rezerwują pełne 2×2 wolnych pól, małe 1×1. Cztery pola dużego drzewa nie mogą pokrywać się z innymi drzewami ani terenem chodzenia. Rezerwacja jest skierowana od ścieżki w głąb lasu; pień stoi w jej wnętrzu. Reguła obejmuje zwykłe dojrzałe drzewa, nie tylko rzadkiego giganta. Przy braku miejsca używany jest mały wariant.

Przy brzegu trasy stoją kompaktowe drzewa, duże korony pozostają głębiej i nie zakrywają centralnych 60% pola przejścia. Jednopolowe przeszkody wewnątrz mapy mają kompaktowe grafiki; referencje zastosowano przez Editor API w zgodnych ForestPrototypeSetup/ForestMatureTreesSetup. 32 PPU, fizyka/TrunkRadius, nawigacja, rozmiar mapy, shader odsłaniania i pamięć mgły zachowane.

144/144 odrębnych testów PASS, zero fail/skip. Rezerwacje 2×2/1×1 bez współdzielenia pól, pień we właściwym footprintcie, ścieżki szerokości dwóch kratek w obu orientacjach, produkcyjny długi las seed 171026, zwykła/znana/zapamiętana/debug mgła i dotychczasowe testy colliderów/PPU/odsłaniania. Końcowe rendery obejrzane. Kompilacja/self-review/whitespace PASS; diff assetu obejmuje wyłącznie referencje PresentationSprite/PresentationVariants. Import gotowy w otwartym Unity — wystarczy Play Mode, bez Refresh. Nowego pomiaru FPS nie wykonano.

Dowody: `LocalHandoffs/forest-trail-clearance-20261018/{REVIEW.md,verification.json,tests.xml,scoped-diff.patch}` oraz `before-production.png`, `production-*.png`. Bez ręcznego YAML, agentów, nowych Unity/Node, commit/push.

## Czytelność drużyny pod nakładającymi się koronami — 2026-10-18 — READY_FOR_SMOKE

Poprzednie 50% opacity każdej korony zostawiało zaledwie 6,25% obrazu bohatera przy czterech warstwach. Korony dzielą teraz wspólny budżet zasłaniania sylwetki: około 20% łącznej opacity liści w rdzeniu lokalnego odsłaniania, także na głowie i ramionach. Liczba warstw pochodzi z lokalnego indeksu mapy, bez sortowania/scanu sceny/nowych tablic per-frame. Wynik opacity jest liczony raz na postać w CPU.

Dalsze liście, chronione korzenie 1×1 / gigantów oraz sortowanie według stóp zachowane. Rozstaw drzew, bitmapy, 32 PPU, fizyka, nawigacja, światło i pamięć mgły nie zmienione. Wpis zastępuje wcześniejsze wymaganie minimum 50% opacity pojedynczej korony, które przy wielu warstwach nie zapewniało czytelności.

140/140 odrębnych testów PASS, zero fail/skip. Testy rzeczywistych pikseli obu shaderów z 1/4/6 warstwami, map query, zwykła/znana/zapamiętana/debug mgła i produkcyjny długi las seed 171026. Obejrzany również render izolujący samo oświetlenie bohaterów bez koron. Kompilacja, self-review i whitespace PASS; import gotowy w otwartym Unity. Wystarczy rozpocząć Play Mode, bez ręcznego Refresh. Nowego pomiaru FPS nie wykonano.

Dowody: `LocalHandoffs/forest-canopy-stack-20261018/{REVIEW.md,verification.json,tests.xml,scoped-diff.patch,production-party-canopy-opacity.txt}` oraz `overlap-{before,after}-*.png`, `before-production.png`, `production-*.png`. Bez commit/push.

## Lokalna przezroczystość koron i rozstaw drzew — 2026-10-18 — READY_FOR_SMOKE

Korekta odrzuconego odsłaniania całej korony: liście zachowują minimum 50% opacity wyłącznie przy sylwetkach drużyny, z miękkim ograniczeniem do jednej kratki w poziomie / 1,4 w pionie od środka sylwetki. Dalekie fragmenty tego samego drzewa są nieprzezroczyste. Podstawa 1×1 (dwa pola wysokości dla giganta), sortowanie według stóp i opacity samych postaci zachowane.

Duże drzewa nie są już sadzone co kratkę: deterministyczne odstępy zależne od wielkości, 54 drzewa w polanie kontrolnej 5×5, zachowane głębokie warstwy lasu. Pierwszy rząd jest przy faktycznej krawędzi blokowania; rzadkie giganty mają wolny footprint 2×2 i odstęp sześciu pól. Pamięć odkrytych drzew i podszycia, fizyka, 32 PPU i skala bohaterów bez zmian.

131 odrębnych latest PASS, zero fail/skip; końcowy targeted retest 9/9. Kompilacja, scoped self-review/whitespace oraz rzeczywiste rendery zwykłej, znanej, zapamiętanej i debug mgły sprawdzone. Automatyczny import do otwartego Unity, bez ręcznego Refresh. Wystarczy wznowić Play Mode i sprawdzić przejście pod koronami oraz rozstaw przy normalnym zoomie. Nowego pomiaru FPS nie wykonano.

Dowody: `LocalHandoffs/forest-canopy-local-density-20261018/{REVIEW.md,HANDOFF.md,verification.json,scoped-diff.patch}`. Ten wpis zastępuje poprzedni opis szerokiego odsłaniania korony. Bez commit/push.

## Las: korony, pamięć mgły i różne drzewa — 2026-10-18 — READY_FOR_SMOKE

Siedem natywnych sylwetek: młode i obumarłe, cztery nowe dojrzałe oraz rzadki częściowo obumarły gigant 2×2 tylko poza terenem chodzenia. Stałe 32 PPU/Point; bez zmiany skali bohaterów i colliderów wnętrza. Giganty mają zweryfikowany wolny footprint i odstęp co najmniej sześciu kratek. Korona odsłania się przy podejściu i pokazuje ścieżkę; podstawa 1×1 pozostaje solidna (2×2 dla giganta). To aktualna decyzja użytkownika, zastępująca wcześniejsze usunięcie całego efektu odsłaniania.

Odkryte drzewa i podłoże pozostają w pamięci bez bieżącego FOV. Wspólna wiedza sąsiednich krawędzi usuwa dziury na narożnikach; pustka za mapą nie tworzy schodkowego czarnego pasa. Poprawiony natywny pivot i faktyczny punkt sortowania zastępują stary offset pół kratki. Zachowano zasady nieodkrytego terenu, dynamicznych bram oraz głębi według stóp.

127 odrębnych latest PASS, zero fail/skip; kompilacja w otwartym Editorze, self-review i scoped whitespace PASS. Pięć nowych bitmap, pięć dokładnych roundtripów Aseprite, kontrola zwykłej i zapamiętanej mgły, togglefog oraz produkcyjnych renderów obu biomów. Import/referencje gotowe przez Editor API, bez ręcznych Refresh. Wystarczy wznowić Play Mode i sprawdzić podejście pod koronę, obrzeża oraz powrót do odkrytego pokoju. FPS nie zmierzono ponownie; nie deklarować stabilnych 60 FPS po zmianie grafiki.

Dowody: `LocalHandoffs/forest-border-trees-20261018/{REVIEW.md,HANDOFF.md,verification.json,scoped-diff.patch}`. Grafiki/prompty/pomiary: `output/forest-mature-trees-v2/{PROMPTS.md,ANCIENT_PROMPT.md,manifest.json,qa/variety-with-hero-2x.png}`. Bez commit/push.

## Korekta konturu lasu — 2026-10-18 — RENDER ZWERYFIKOWANY

Brakujący końcowy render wykonano i obejrzano w późniejszym passie drzew. Delikatny ciemny kontur zachowuje autorskie krawędzie, bez jasnych „naklejek”. `LocalHandoffs/forest-contour-correction-20261018/closeup-after.png` jest poprawionym obrazem; `closeup-before.png` zachowuje stan wcześniejszy. Testy konturu są objęte 127 latest PASS późniejszego passa. Dawny blocker pobierania listy testów zakończony; nie wymaga działania użytkownika.

## Audyt sprite–sprite i poprawki prezentacji — 2026-10-18 — READY_FOR_SMOKE

Pełny audyt lasu/katakumb: głębia według stóp, lokalne odsłanianie drużyny przez koronę, poprawione sortowanie drzew oraz podłoża/dekoracji/mgły po zmianie pasma. To stare warstwy podłoża mogły przykrywać sylwetki; dodatkowo własna drużyna nie znika przy opóźnionym FOV. Widziane statyczne obiekty/ściany zachowują pamięć, footprinty wielopolowe są uwzględniane, offscreen zmiany nie wyciekają. Dwie dodatkowe głębokie warstwy drzew; leśny kontur wartości w lit shaderze, bez zmian skali/PPU/colliderów.

331 odrębnych latest PASS, 0 fail/skip; kompilacja, scoped whitespace i self-review PASS. Obejrzane rzeczywiste rendery obu biomów, porównanie przed/po podłoża, shader lokalnego odsłaniania i atlas. Import do otwartego Editora automatyczny; nie trzeba Ctrl+R, zamykania Unity ani ręcznej konfiguracji. Wystarczy wznowić Play Mode i sprawdzić przejście za drzewem/kamieniem, narośle/ciała, obrzeża, zoom oraz pamięć opuszczonego pokoju. Bez nowego pomiaru FPS i bez deklarowania stabilnych 60 FPS. Bez commit/push oraz ręcznych serialized edits.

Dowody i pełna tabela relacji: `LocalHandoffs/sprite-render-audit-20261018/{AUDIT.md,HANDOFF.md,SELF_REVIEW.md,verification.json,scoped-diff.patch}`. Podglądy: `production-forest-before-underlay-fix.png`, `production-dungeon.forest.expedition.png`, `production-dungeon.catacombs.expedition.png`.

## Pełne klatki po badaniu regresji — 2026-10-18 — POMIAR ZAKOŃCZONY / CEL 60 FPS OTWARTY

Prawidłowy zapis długiego lasu poza walką: **1362 klatki / 11,972 s**, alignment 1362/1362, zero brakujących ID, 12 nowych celów myszy, bez pełnego Profilera/CreateContext/GC.Collect/LoadSprite/autosave. Średnia **8,790 ms (~113,77 FPS)**, mediana 8,454, **p95 13,094 / p99 17,019 / max 24,281 ms**. **98,825% klatek w budżecie 16,667 ms, 16 przekroczeń**. Stabilnych 60 FPS nadal nie potwierdzono. Pierwszy zapis miał tylko 2,735 s pokrycia i został wykluczony jako zbyt krótki.

Największy wybrany pik natywny: render kamer 10,521 ms; patrole max 4,239, światło 5,663, mgła 2,801 ms — oddzielne maksima, nie sumować. Brak dodatkowego dopasowania deltaMs do granic kosztów natywnych; nie przypisywać im automatycznie konkretnej wolnej klatki. Workload 80/212/190 actor/object/obstacle callbacks, pokrycie kamery odświeżone 7 razy; wynik dotyczy tej trasy i nie jest kontrolowanym A/B z poprzednią. Kod bez zmian, narzut kolektora mean 0,041 ms, bez odświeżania Unity. Dowody: `LocalHandoffs/environment-regression-pass-20261018/{FULLFRAME_REPORT.md,environment-regression-fullframe-02.summary.json,environment-regression-fullframe-02.raw.json,verification.json}`.

## Regresja pakietu środowiska — 2026-10-18 — READY_FOR_SMOKE / CEL 60 FPS OTWARTY

Historyczne 13,98→15,02 ms nie potwierdzało regresji: kontrolne wyniki tej samej wersji zmieniały się bardziej; obciążone Cyberpunkiem próby wykluczono. Rozbicie mgły wskazało ściany i podszycie/dekoracje jako największy koszt. Indeks geometrii lasu jest teraz ponownie używany do zmiany mapy/topologii/źródeł, a nieodkryte granice pomijają zbędne zapytania o elewację. Identyczne kolory sprite'ów nie są zapisywane ponownie, z zachowaniem naprawy zewnętrznych zmian. Wolniejszy eksperyment cache powierzchni odrzucony.

Ta sama deterministyczna sekwencja produkcyjnego długiego lasu: suma badanych subsystemów średnio **12,25→11,02 ms**, mgła **6,94→5,90 ms**, p95 sumy **63,21→57,38 ms**; przebudowy indeksu **30→0**. To synchroniczny EditMode replay z debug-fog stress i teleportami, nie pełna klatka/FPS. Hash wejść/stanu patroli/raw fog i contact mesh/GPU mask identyczny. **107 odrębnych końcowych testów PASS**, kompilacja/self-review/whitespace PASS. Testy obu lochów porównują aktywne wierzchołki ze starymi wzorcami i wymagają przezroczystości recyklingowanych nieaktywnych slotów; stare wzorce bez zmian.

Wygląd, AI/kadencja, skala, fizyka i save schema zachowane. Automatyczny import do istniejącego Editora bez Ctrl+R, nowych Unity/Node, agentów, serialized edits lub commita/pusha. Smoke i pełny pomiar klatek końcowej wersji pending. Dowody: `LocalHandoffs/environment-regression-pass-20261018/{HANDOFF.md,SELF_REVIEW.md,verification.json,before-package-report.json,after-package-report.json,final-test-results.json}`.

## Zbiorczy pass cieni, światła, mgły i patroli — 2026-10-18 — READY_FOR_SMOKE / CEL 60 FPS OTWARTY

Po wspólnym przeglądzie: mniej native odczytów źródeł kontaktów, dokładna jednostkowa projekcja z fallbackiem, bounds tylko w jednym refresh, kolorowe retargety mgły bez przebudowy slotów oraz jeden odczyt pose podczas persist patrolu. Bez zmiany wyglądu/AI/kadencji/save schema i bez ręcznej edycji serialized Unity state. Automatyczny import do istniejącego Editora, bez Ctrl+R, nowych Unity/Node, agentów, commita/pusha.

**169 odrębnych testów PASS**, zero skip, kompilacja i self-review PASS. Sparowane 432 porównania kontaktów na tym samym świecie/sekwencji: mediana **1.367→1.100 ms**, p95 **4.351→3.344 ms**; 431 par bez GC średnio **1.889→1.539 ms**. 18/18 geometrii i 18/18 byte-exact GPU obrazów; kontrola niepustego renderu. Wspólny replay ma identyczny stan aktorów, fog/contact mesh i shadow-mask hash.

**Cały pakiet nie wykazał przyspieszenia: 13.98→15.02 ms średnio** w identycznym deterministycznym replayu EditMode. To suma subsystemów, obejmująca debug-fog stress i teleporty, a nie pełny PlayerLoop/FPS; nie ogłaszać stabilnych 60 FPS ani odtworzenia historycznej klatki 11.7007 ms. Wolniejszy eksperyment z comparerem Rect odrzucony. Dowody, pełne fazy i granice: `LocalHandoffs/contact-shadow-cost-pass-20261018/{HANDOFF.md,SELF_REVIEW.md,verification.json,after-report.json,before-package-report.json,after-package-report.json}`.

Smoke: wznowić Play Mode bez Refresh; sprawdzić cienie/pochodnie, odsłanianie mapy i patrole/drzwi w długim lesie, potem katakumby. Ręczny setup niepotrzebny; pomiar pełnych klatek pozostaje osobnym kryterium celu 60 FPS.

## Zbiorczy pass eksploracji — pomiar — 2026-10-18 — CEL 60 FPS OTWARTY

714 klatek / 11,935 s, alignment 714/714, 31 celów, bez Profilera/CreateContext/HUD rebuild/GC.Collect/LoadSprite. Mean 16,72 ms (~59,83 FPS), p95 26,59, max 39,75; 59,38% w 16,667 ms, 290 przekroczeń. W klatce 2257 światło 17,75 ms, w tym kontakt 11,70 i ReceiverInputs 4,14; maski tylko 0,59. Osobne inne maksima: ReceiverGround 7,94, maski 5,40, kamery 7,40 ms. Nie sumować faz rodzic/dziecko ani maksimów z różnych klatek. Podzbiór Fog/CameraPresentation/Lighting/Traps max 18,72 ms, 8 klatek >=15. Wynik liczbowo lepszy od 19,09 / p95 27,85 / max 50,06 ms, ale workload 95/162/145 i 31 celów zamiast 104/130/116 i 26, brak kontrolowanego A/B. 257 testów PASS i source hashes bez zmian. Stabilne 60 FPS nieosiągnięte. Dowody: LocalHandoffs/exploration-consolidated-pass-20261018/HANDOFF.md, after-classification-01.json, phase-peaks-01.json, verification.json.
## Zbiorczy pass eksploracji — 2026-10-18 — GOTOWE DO POMIARU / CEL 60 FPS OTWARTY

Przed implementacją: review funkcji w REVIEW.md oraz pełny profil 180/180 klatek gry. Kamery mean 1,67 / max 2,80 ms; wcześniejszy skok 18,32 ms nie odtworzony i nadal niewyjaśniony. Profiler mean 8,12 ms — nie benchmark FPS. Pakiet: snapshot pozycji tylko w synchronicznej fazie wykrywania z invalidacją po dołączeniach, łączenie ostatniego koloru kafla w jednym przebiegu mgły, dokładny clipping obiektów minimapy przed zapytaniem o wiedzę, mniej natywnych odczytów pozycji w Move. Fizyczne casty, wygląd, kadencja i zasady zachowane; żywe MPB/bounds światła pozostają aktualne.

Compile/import/self-review/whitespace PASS. 257 odrębnych latest PASS: szeroki zestaw + 56-test retest scenariusza mgły, 4 produkcyjne testy światła obu lochów i 84 seamless combat. Initial failure dotyczył niewłaściwie przygotowanej granicy jasności w nowym teście; dowody zachowane w XML. Brak manualnego Refresh, dodatkowego Unity/Node/subagentów, serialized edits lub commit/push. Oczekuje jeden wspólny 12-sekundowy pomiar bez Profilera po gotowości użytkownika. Dowody: LocalHandoffs/exploration-consolidated-pass-20261018/HANDOFF.md, REVIEW.md, before-findings.json, verification.json.
## Patrole / cienie / minimapa — wspólny pomiar — 2026-10-18 — CEL 60 FPS OTWARTY

625 poprawnych klatek / 11,931 s, 26 celów, bez Profilera/CreateContext/HUD rebuild/GC.Collect/LoadSprite. Mean 19,09 ms (~52,4 FPS), p95 27,85, max 50,06; 33,28% w budżecie 16,667 ms. Patrole mean 2,80/max 6,83; kamery 2,51/18,32; światło 0,76/8,41; minimapa 0,56/3,87; mgła 0,22/6,95 ms. Osobne maksima, nie sumować. Klatka 50,06 ms nakłada kilka systemów; osobny skok renderu kamer 18,32 ms pozostaje nierozłożony. Rozłączny podzbiór Fog/CameraPresentation/Lighting/Traps max 14,60 ms, co nie oznacza kosztu całej gry. Obecny zapis całościowo wolniejszy od poprzedniego, ale workload 104/130/116 i 26 celów zamiast 96/169/153 i 6; brak kontrolowanego A/B. Cel nieosiągnięty. 169 latest PASS i source hashes bez zmian. Dowody: LocalHandoffs/patrol-contact-minimap-pass-20261018/HANDOFF.md, after-classification-01.json, verification.json.
## Zbiorczy pakiet: patrole / cienie kontaktowe / minimapa — 2026-10-18 — GOTOWE DO POMIARU

Audyt całego dostępnego profilu w AUDIT.md; poprawki wdrożone razem zamiast kolejnych mikro-passów. Mniej powtórnych natywnych odczytów ruchu, brak boxowanych enumeratorów w gorących listach, reuse cieni po dokładnej lokalnej rewalidacji fog, wspólna baza i wypełnienia wierszy minimapy, dirty znaczników tylko po zmianie. Wygląd, jakość, fizyka, kadencja i reguły gameplay zachowane.

Kompilacja/import/self-review/whitespace PASS. Szeroki zestaw 168/169; błąd rozdzielczości helpera GPU naprawiony z zachowaniem dodatniej kontroli, retest 29/29. Łącznie 169 odrębnych latest PASS, w tym wzorce lasu/katakumb i porównania pikseli. Bez manualnego Refresh, serialized edits, nowych Unity/Node/subagentów lub commit/push. Oczekuje jeden wspólny 12-sekundowy lekki pomiar eksploracji po gotowości użytkownika; stabilne 60 FPS nadal niepotwierdzone. Dowody: LocalHandoffs/patrol-contact-minimap-pass-20261018/HANDOFF.md, AUDIT.md, verification.json i XML.
## Patrole / światło / kamera / mgła — pomiary — 2026-10-18 — CEL 60 FPS OTWARTY

Lekki zapis672klatek/11,888s,kalibracja672/672,6celów,bezProfilera/CreateContext/HUD rebuild/GC.Collect/LoadSprite:średnia17,69ms,p9527,12,max41,69;48,21%w16,667ms. Persistmax3,46,ReceiverInputs2,47,Fog.Render2,60; wcześniejsze11,06/8,27nieodtworzone. Suma rozłącznych Fog/Camera/Lighting/Traps max12,56,0>=15ms. Inny workload96/169/153 i6celów zamiast71/175/158 i22; brak kontrolowanego A/B.

Pierwszy pełny profil zawiera walkę i focus/pauzę, zachowany osobno. Powtórzony w oczyszczonym pokoju:180/180klatekgry/~4,34s,bez utraconychpróbek. JednaMainCamera; CPUkamerśrednio1,82/max3,03ms,skok13,42nieodtworzony i nadal niewyjaśniony. Profilerśrednio7,80/max12,25ms—nie benchmarkFPS. Najcięższa klatka:Light7,96,minimapa2,60,Fog2,43,Patrols2,64,kamera3,03,Profiler10,36ms.98testówPASS,hashezgodne,bez zmian runtime/importów/Refresh podczas pomiarów. Dowody:LocalHandoffs/patrol-receiver-camera-fog-pass-20261018/HANDOFF.md,after-classification-01.json,camera-native-findings-02.json,verification.json.
## Patrole / odbiorcy / kamera / mgła — 2026-10-18 — GOTOWE DO POMIARU / CEL 60 FPS OTWARTY

Persist pomija przepisywanie niezmienionych komórek, zachowuje żywe offsety i zewnętrzny restore. Statyczne floor-owned źródła światła filtrowane dokładnym AABB przed natywnym przygotowaniem; dynamiczne props/foreign MPB pozostają żywe. Dane kamery współdzielone wyłącznie w jednym synchronicznym przebiegu światła; następny caller odczytuje aktualny stan. Mgła wysyła zmienione fragmenty geometrii, pełny upload przy wzroście/topologii lub wielu zmianach. Bez zmian wyglądu, jakości, AI, kadencji i formatu save. 126 kanałów, dokładniejsze fazy Persist i ReceiverInputs.

Kompilacja, automatyczny import, self-review, whitespace i 98 odrębnych testów PASS; wzorce cieni/geometrii kontaktowych lasu oraz katakumb identyczne. Pierwszy test katakumb timeout180s w helperze membership; zachowano audyt, zastąpiono równoważnymi HashSet checks, retest72s PASS bez zwiększania limitu. User potwierdził: wcześniejsze108/123ms były wejściem w walkę, nie czystą eksploracją. Po zmianie realny pomiar jeszcze niewykonany; stabilne60FPS niepotwierdzone. Bez manualnego Refresh, serialized state, nowych Unity/Node/agentów lub commit/push. Dowody: LocalHandoffs/patrol-receiver-camera-fog-pass-20261018/HANDOFF.md, verification.json, scoped.diff, test XML.
## Sprite'y patroli — powtórzony pomiar — 2026-10-18 — CEL 60 FPS OTWARTY

821 klatek / 11,953 s, kalibracja 821/821, 22 cele, bez walki/Profilera/HUD rebuild/GC.Collect. 723 facing / 2406 Resolve: zero LoadSprite i PrepareDirections. Średnia 14,56 ms, p95 23,31, p99 29,38, max 37,86; 81,36% w budżecie, 153 przekroczenia. Skoki 108/123 ms nie powtórzyły się; obecne zakresy: światło max11,99 ms (ReceiverInputs8,27), patrole13,57 (Persist11,06), mgła6,66, kamery10,85. Osobne maksima, bez sumowania rodziców/dzieci. Rozłączna suma Fog/Camera/Lighting/Traps max16,98 ms, jedno>=15. Workload71/175/158, inna trasa i22 zamiast4 celów; brak kontrolowanego A/B. Kod i70 testów bez zmian, hashe zgodne, bez importu/Refresh. Dowody: LocalHandoffs/patrol-animation-preload-pass-20261018/after-classification-02.json i HANDOFF.md.
## Sprite'y patroli — pomiar zakończony — 2026-10-18 — STABILNE 60 FPS OTWARTE

977 poprawnych klatek / 11,952 s, kalibracja 977/977, 4 cele, bez walki i Profilera. 745 facing / 2234 Resolve: 0 LoadSprite i 0 PrepareDirections podczas eksploracji. Średnia 12,23 ms, p95 18,25, p99 24,60, max 123,20; 91,71% w budżecie, 81 przekroczeń. Największe klatki: 123,20 ms z BehaviourUpdate 96,31 ms (RebuildSlots tylko 17,20 ms, reszta niewyjaśniona) oraz 107,87 ms z HudLate 26,09 / Focus 24,38 ms. Dwa GC.Collect max 12,92 ms, nieprzypisane jako przyczyna dwóch największych klatek. Suma rozłącznych Fog/Camera/Lighting/Traps max 13,81 ms, 0>=15 ms. Inny workload 74/175/158 i 4 cele zamiast 31; brak kontrolowanego A/B. 70 testów PASS, hashe zgodne. Bez nowych zmian kodu lub Refresh podczas pomiaru. Dowody: LocalHandoffs/patrol-animation-preload-pass-20261018/HANDOFF.md, after-classification-01.json, verification.json.
## Przygotowanie sprite'ów patroli — 2026-10-18 — GOTOWE DO POMIARU / CEL 60 FPS OTWARTY

Kierunki Idle/Walk są przygotowywane przed tworzeniem widoków przeciwników, raz na kanoniczny zestaw w spawnerze. Zachowano dotychczasowe piksele, geometrię, pivot, PPU, czas klatek, fallback i zachowanie. Koszt cache miss przesunięto poza zmianę kierunku patrolu. Kompilacja, automatyczny import i self-review PASS; 70 odrębnych testów PASS. Produkcyjne wejście do długiego lasu: 94 przeciwników, 7 zestawów, 752 zmiany kierunku, 0 LoadSprite; widoki z przygotowaniem 0,423 s, całe wejście 10,949 s. Pusty cache resolvera, rezydencja tekstur/OS niekontrolowana; to nie pomiar FPS. Bez ręcznego Refresh, dodatkowego Unity/Node/agentów, serialized state lub commit/push. Oczekuje 12-sekundowy pomiar eksploracji po gotowości użytkownika. Dowody: LocalHandoffs/patrol-animation-preload-pass-20261018/HANDOFF.md i verification.json.
## Kamery i maski — wynik pomiaru — 2026-10-18 — STABILNE 60 FPS NADAL OTWARTE

1345 poprawnych klatek / 11,954 s, kalibracja 1345/1345, 31 celów, bez walki i Profilera. Średnia 8,89 ms, p95 13,99, p99 18,49, max 24,79; 97,99% w budżecie, 27 przekroczeń. Kamery max 2,44 ms, PrepareDraws max 1,17 ms; wcześniejsze 11,36/8,97 ms nie wystąpiły, brak gwarancji wyeliminowania. Największy skok: patrole/facing i SpriteCache miss -> Animation.LoadSprite 12,43 ms (synchroniczne Resources.Load + Sprite.Create, dokładne wnętrze niezmierzone). Suma Fog/Camera/Lighting/Traps max 13,30 ms, 0>=15 ms. GC bez większego zdarzenia. Workload 88/216/194 zamiast91/150/134; niekontrolowane A/B. 84 testy PASS i hashe bez zmian. Dowody: LocalHandoffs/camera-shadow-preparation-pass-20261017/HANDOFF.md, after-summary-02.json, after-classification-02.json, verification.json.
## Kamery i przygotowanie masek — 2026-10-17 — GOTOWE DO POMIARU / CEL 60 FPS OTWARTY

Rozdzielono ruch źródła cienia od lokalnych parametrów kształtu, przygotowanie ograniczono do przebudowywanych warstw, identyczne parametry rendererów nie są ponownie wysyłane. Zachowano żywe property blocks, obce wartości, piksele masek, jakość, kadencję i gameplay. Kompilacja/import/self-review PASS; 84 odrębne EditMode PASS. Bez manualnego Refresh, nowych Unity/Node/agentów lub serialized state.

Pełny profil active-03: 180 poprawnych klatek gry, ale zawiera walkę/AI/tooltip — diagnostyka, nie benchmark eksploracji. Jedna aktywna kamera. Skok kamer 5,10 ms zawiera 4,06 ms oczekiwania na render jobs; równolegle StdRender.ApplyShader 3,65 ms. Konkretnego shadera/PSO nie ustalono, wcześniejsze 11,36/8,97 ms nieodtworzone. Brak podstaw do zmiany jakości lub globalnego warmupu shaderów. Końcowy pomiar bez Profilera oczekuje gotowości. Dowody: LocalHandoffs/camera-shadow-preparation-pass-20261017/HANDOFF.md, camera-native-findings-03.json, final-tests.xml, verification.json.
## Kamery i przygotowanie masek cieni — 2026-10-17 — W TOKU / POMIAR I TESTY OCZEKUJĄ

Kod masek rozdziela przesunięcie źródła od lokalnych parametrów sprite'a oraz przygotowuje wyłącznie przebudowywane warstwy. Dwa dodatkowe znaczniki przygotowania; pełny profiler zachowuje najgorsze klatki kamer/masek i inwentarz aktywnych kamer przy eksporcie. Kompilacja Views/EditModeTests PASS, runtime kamer jeszcze niezmieniony. Pierwszy profil: 116 klatek Edytora, ZERO klatek gry — niedopuszczalny jako diagnoza gameplayu. Prośba do użytkownika o aktywne Unity/niewstrzymaną grę jest otwarta. Testy NIEPOTWIERDZONE: kolejka czeka poza Play Mode, stary XML 11/11 z poprzedniej karty nie jest dowodem. Cel stabilnych 60 FPS pozostaje otwarty. Bez ręcznego Refresh, zmian serialized state lub dodatkowego Unity/Node. Dowody i dalsze kroki: LocalHandoffs/camera-shadow-preparation-pass-20261017/HANDOFF.md, verification.json, scoped.diff.
## Impet, mgła i światło — pomiar — 2026-10-17 — CEL STABILNYCH 60 FPS OTWARTY

Czysta eksploracja: 1208 klatek / 11,244 s poprawnych danych, kalibracja 1208/1208, 29 celów, bez walki i Profilera. Średnia 9,31 ms, mediana 8,70, p95 14,88, p99 18,85, max 25,41; 97,35% klatek w budżecie, 32 przekroczenia. AdvanceStatuses około 0,04 ms/frame. Suma rozłącznych Fog/Camera/Lighting.Refresh/Traps max 16,94 ms — jedno przekroczenie 15 ms. Workload 91/150/134 zamiast 100/200/180; brak kontrolowanego A/B. Pozostałe skoki: CPU RenderPlayModeViewCameras 11,36/10,55 ms; PrepareDraws 8,97 ms wewnątrz masek 9,26 ms. GC/JIT/duży MeshBuild nie potwierdzają przyczyny tego PrepareDraws. Pierwszy mieszany zapis zachowany: wejście do walki po 0,85 s, pełna klatka 162,10 ms, Detection 138,71 ms zawierające CreateContext 55,75 ms. Dowody: LocalHandoffs/zero-momentum-fog-light-pass-20261017/HANDOFF.md, after-summary-02.json, after-budget-02.json, verification.json.
## Impet 0→0, mgła i cache pokrycia światła — 2026-10-17 — GOTOWE DO POMIARU

Usunięto odświeżanie prędkości przy impecie 0→0, zachowując zerowanie reszty ułamkowej i usuwanie bonusu przy spadku niezerowego impetu. Mgła pomija ponowny przebieg po niezmienionym wnętrzu; nadal sprawdza zmienione sąsiedztwa oraz istniejące granice i przejścia. Cache pokrycia przechowuje dwa dokładne prostokąty dla każdego trybu: światło i margines cieni kontaktowych nie wyrzucają już wzajemnie swoich wyników. Ograniczono powtórne odczyty natywnej geometrii/koloru oraz konwersje nazw parametrów shaderów. Bez zmian wyglądu, kadencji, gameplayu lub serialized state.

Kompilacja/import/self-review PASS, 139 odrębnych EditMode PASS. Naprawiono helper public/private w czterech nowych testach; końcowy retest 11/11 PASS. Importy w istniejącym Unity, bez ręcznego Refresh, Node, drugiego Unity lub commit/push. Pomiar pełnych klatek po zmianie jeszcze niewykonany; stabilne 60 FPS pozostaje celem otwartym. Dowody: LocalHandoffs/zero-momentum-fog-light-pass-20261017/HANDOFF.md, verification.json, scoped.diff, test XML.
## Mgła, kamera, światło — pomiar — 2026-10-17 — CEL60FPS OTWARTY

Pełne832klatki/11,95s,kalibracja832/832,35celów,bezwalki/Profilera. Średnia14,36ms,p9521,42,max35,33;79,81%w16,667ms. Suma zakresów wjednejklatcemax14,82ms(0>=15),sumaosobnychmaks20,93ms. Workload100/200/180actor/object/obstacle vs91/177/159 iwięcejruchu;niekontrolowaneA/B,brakdowodu poprawyFPS. Statusydokładnie:Gather0,400/Advance0,788/Entries0,090/Deaths0,038msśrednio;200tickówśrednio0,004ms,max1,58ms. Resetimpetu0->0czytającyiwalidującyprógdwukrotnie jestkonkretnympodejrzanym,niewyizolowanojeszczejegokosztu. GC2zdarzenia;pełneprzyczynymaxgapnieustalone. Implementacja/testy158PASS,self-reviewzachowane. Dowody:LocalHandoffs/fog-camera-light-budget-pass-20261017/HANDOFF.md,after-budget-01.json,after-summary-01.json,verification.json.

## Mgła, kamera, światło i diagnoza statusów — 2026-10-17 — GOTOWE DO POMIARU

Dwa bufory projekcji bez pełnej kopii, wiedza nakładającego się pokrycia kamery przy aktualnym świecie, mniej powtórnych odczytów kafla lasu. Bounds odbiorców/cieni współdzielone tylko w jednym przygotowaniu, finally cleanup. Dokładne pasma kontaktu z cache pozycji/szerokości i usuwaniem ukrytych źródeł; clipping/visibility nadal świeże. 9 nowych markerów pułapek/statusów, 113 kanałów lekkiego recordera. Bez zmiany wyglądu, jakości, kadencji, logiki i serialized state. 158 odrębnych testów PASS, kompilacja/import/self-review PASS; bez ręcznego Refresh, dodatkowego Unity lub commit/push. Cel 15 ms i 60 FPS wymaga pomiaru po zmianie. Poprzednio max sumy w jednej klatce 10,43 ms, suma osobnych maksimów 17,60 ms — raport poda obie. Dowody: LocalHandoffs/fog-camera-light-budget-pass-20261017/HANDOFF.md, verification.json, test XML.

## Pełny profil skoków — 2026-10-17 — DIAGNOZA Z OGRANICZENIAMI

Zebrano 180/180 klatek przez 3,23 s, bez utraconych próbek. Zachowano surowy profil .data (151 MB) i cztery hierarchie najwolniejszych klatek. Potwierdzono koszty mgły 5,38 ms, pokrycia kamery 2,43 ms, oświetlenia 3,74–4,33 ms, patroli 4,24 ms i pułapek/statusów 5,45 ms. EditorLoop zawiera PlayerLoop; hierarchie rozdzielono przez self i przodków. Edytor poza PlayerLoop: 2,47–4,38 ms, oznaczone Profiler.*: 2,99–3,60 ms. To diagnostyka z narzutem, nie benchmark FPS. GC nie odtworzono; wnętrze Traps i wcześniejsze zdarzenia 29,81/98/594 ms pozostają częściowo niewyjaśnione. Kompilacja, automatyczny import i 11/11 testów PASS. Bez zmian produkcyjnego runtime, serialized state, nowych procesów, ręcznego Refresh lub commit/push. Dowody: LocalHandoffs/full-spike-diagnosis-20261017/HANDOFF.md, full-summary-01.json, verification.json.
## Pełna diagnostyka najgorszych klatek — 2026-10-17 — GOTOWE DO POMIARU

Początkowy bounded90-frame profil złapał28,58ms,potwierdził równoległe koszty lighting/patrol/minimap i narzutProfiler.FlushMemoryCounters~4ms. WnętrzeEditorLoop wymagało rozszerzenia istniejącego narzędzia. Opt-in full-spikes-:180profilerframes/12swall,native.data,JSONmain/renderhierarchy,Editor-onlygaps,toppeaks/GCneighbours,analiza ponagrywaniu. enabled/profileEditorprzywracane;bezdeep/settings/nowychprocesów/runtime/serializedstate. Kompilacja/import/self-reviewPASS,11/11EditModePASS. Oczekuje na ponowne wejście użytkownika do lasu;nie jest benchmarkFPS ani ukończona atrybucja pozostałychskoków. Dowody:LocalHandoffs/full-spike-diagnosis-20261017/HANDOFF.md,verification.json.

## Tooltip przeszkód i alokacje — pomiar — 2026-10-17 — 60FPS CEL OTWARTY

Pomiar tooltip/GC passa zakończony:1037klatek/11,95s,pełna kalibracja,25celów,bez walki. Średnia11,53ms,mediana11,28,p9516,65,p9920,51,max29,81;95,08%w16,667ms. Stabilne60FPS nadal otwarte. Widoczne zbieżne lighting refresh/fog/patrol peaks;GC2zdarzenia irozjazd granicy próbki przymax,bez jednoznacznej atrybucji29,81ms. Różneworkloady,nieskontrolowaneA/B. Dowody:LocalHandoffs/gc-hotpaths-pass-20261017/after-summary.json,HANDOFF.md,verification.json. Bez dodatkowego importu/Refresh.

## Tooltip przeszkód i alokacje — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Usunięto tekstowy hover przeszkód; menu niszczenia/wytrzymałość/camping pozostają, kursor STOP później. Occupancy/live LOS bez iteratorów, scratch statusów i detekcji, cache klucza ciała; świeże sprawdzanie kontaktu/ucieczki i gameplay zachowane. Rich content/6 slotów statusu przygotowane przy ładowaniu piętra. Kompilacja/import automatyczny/self-review PASS; 222 odrębne EditMode PASS (221 regresja + 10 końcowe, 9 powtórek). 1000 LOS+occupancy: 0 alokacji, legacy LOS 2000; 1000 status Advance pomiędzy tickami/body reads: 0. Nie jest to wynik FPS; pomiar długiego lasu czeka na gotowość. Bez manualnego Refresh/serialized assets/Node/drugiego Unity/commit/push. Dowody: LocalHandoffs/gc-hotpaths-pass-20261017/HANDOFF.md, verification.json, test XML.

## BehaviourUpdate i GC — diagnostyka — 2026-10-17 — WYNIKI / RZADKIE SKOKI OTWARTE

11 natywnych kanałów callbacków/JIT/wait/Profiler appended wlekkim recorderze(104razem). Opt-in behaviour-gc profil: bounded90frames,bez deep/streaming; nagrywanie wyłączone przed analizą. Per-frame metodyUpdate inclusive/self,GCAlloc bytes+callback/parent owner,GC/waits i nongameplay gaps zachowane. Main thread only,owner marker nie managed stack; braków danych nie interpretować jako zera. Kompilacja/import/self-review PASS,8/8EditModePASS. Bez zmian runtime/gameplay/assets, ręcznego Refresh, Node/drugiegoUnity, commit/push. Pomiary ukończone. Potwierdzono ownerów alokacji eksploracji71,2kB/frame(tooltip20,7/traps17,3/detection8,6/body6,2),powtórnycontact/escapeUIwcombat150,7kB/frame itooltipcreationCPU16,8ms. GCmax19,19/Incremental30,94;dawne98/594mszdarzenia NIEODTWORZONE,ich dokładna przyczyna otwarta. NativeInvokemarkery nie dostarczają danych bez pełnegoProfilera;analizaProfilerapozaoknem też alokuje,możliwy późniejszy wpływ naheap. Ograniczenia i źródła opisane w raporcie. Dowody: `LocalHandoffs/behaviour-gc-diagnosis-20261017/HANDOFF.md`, `verification.json`.
## Patrole, ukryte obiekty i odbiorcy światła — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Owner-local cache wyłącznie statycznych forest footprint rules (map/revision/ent/room, max4096), współdzielenie wyników planowania tylko przed pierwszą mutacją tego samego aktora, cleanup wfinally. Patrole/LOS/drzwi/fizyka/wait/save działają jak wcześniej również poza ekranem. Nieznane/nieaktywne obiekty fog po ukryciu pomijają camera/bounds/tint/depth; live knowledge/debug/external renderer flags budzą zwykłą ścieżkę, collidery aktywne. Kamera czyta macierze raz dla grupy rendererów. Ground receiver dedupe i sprite/texture/texel inputs wyłącznie podczas jednego refresh; sorting upgrades i świeże foreign MPB zachowane. **150 odrębnych aktualnych EditMode PASS**, regresja146/146, końcowe13/13. Exact GPU fog/masks i footprint/lifecycle/door/path/collision PASS. Kontrolowane36→12receiver preparations/1sprite read,20hidden calls→20skips,100footprint powtórek→0obliczeń — nie FPS. Automatyczny import, kompilacja, scoped diff/self-review PASS; bez ręcznego Refresh/wiring, serialized state, Node, drugiego Unity, commit/push. Pomiar zakończony:728klatek/11,95s,mediana14,91ms,p9523,28,max593,79;72,80%wbudżecie60FPS. Stabilne60FPS NIEOSIĄGNIĘTE. Patrole1,96ms/frame,ObjectLate0,22,ObstacleLate0,29,ReceiverInputs2,37/refresh;workload niższy,nie kontrolowaneA/B. Największa próbka ma wadliwą kalibrację; dodatkowe poprawne skoki~98ms zBehaviourUpdate68–73ms i osobneGC wymagają diagnozy. Pełne dane w handoffie. Dowody: `LocalHandoffs/patrol-hidden-receivers-pass-20261017/HANDOFF.md`, `verification.json`, `final-tests.xml`.
## Oświetlenie, pokrycie kamery i siatka mgły — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Cache kandydatów statycznego lighting/contact coverage dla identycznego viewportu; camera-only refresh pomija niezmienioną globalną prezentację, zachowując dokładne tiles/fog i invalidację świateł. Pełna invalidacja po zmianach wiedzy, bram, transformów i podmianie rendererów (również Ent fire). Kolory mgły Float32 w osobnym strumieniu: częściowy upload zmienionych zakresów zamiast całego mesh; identyczne snapped contact bands projektowane raz. Wygląd/shadery/sampling/cadence zachowane. **Końcowe137/137 EditMode PASS**, exact GPU/native camera pan/zoom i mesh/lifecycle PASS. Kontrolowane81/13608 vertices,1/10 query builds i1/24 contact projections — nie FPS. Automatyczny import, kompilacja, scoped diff i self-review PASS. Bez ręcznego Refresh/wiring, serialized state, Node, drugiego Unity, commit/push. Pomiar długiego lasu wykonany; stabilne60FPS NIEOSIĄGNIĘTE. Mediana25,91ms,p9539,14,max51,39;0,92% klatek wbudżecie. Kamera/animacja mgły tańsze, cały przejazd gorszy przy innym workload; patrole4,81ms/frame,Lightingmax18,14/Receiver12,71. Wynik i ograniczenia w handoffie. Dowody: `LocalHandoffs/lighting-coverage-fogmesh-pass-20261017/HANDOFF.md`, `verification.json`, `final-tests.xml`.
## Ruch towarzyszy, patrole i mgła — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Kierunki idle/walk czterech pakietów drużyny przygotowywane przy konfiguracji (przeciwnicy/NPC bez prewarm); pomijane identyczne zapisy rotacji. Patrole współdzielą native środki komórek wyłącznie w jednym przebiegu, z finally cleanup. Globalna wiedza granic lasu zachowuje remembered wpisy i aktualizuje visible delta; pełne odświeżenie po resecie/debug/mapie, nowa discovery nadal sprawdzana. Zachowane tempo/trasy/kolizje/LOS/AI/zapis oraz wygląd mgły/animacji. **166 odrębnych EditMode PASS**, końcowe22/22, GPU fog pixels i exact knowledge/changed owners PASS. Kontrolowane19 200→0 ponownych odczytów discovery i100→4 native queries patrol cells; nie są to FPS. Import automatyczny, kompilacja/self-review/scoped diff PASS. Koszt ładowania animacji przeniesiono do konfiguracji, jego zimny czas nieweryfikowany. Nowe markery rozdzielają Move/Facing/Commit/LoadSprite i etapy fog. Bez ręcznego Refresh/wiring, serialized state, Node, dodatkowego Unity, commit/push. Końcowy pomiar użytkownika wykonany; cel60FPS nieosiągnięty. Dowody: `LocalHandoffs/follower-patrol-fog-pass-20261017/HANDOFF.md`, `verification.json`.

Pierwszy zapis187klatek/2,66s zbyt krótki; ważna powtórka699klatek/10,98s:mediana15,05ms,p9523,08,max39,84;66,81% wbudżecie60FPS. Cały przejazd gorszy od poprzedniego przy innym ruchu/pokryciu (17CameraPresentation zamiast7). FollowersTick max12,18→1,07ms,FollowerMove11,76→0,35,LoadSprite0; rzadki historyczny skok nie powtórzył się. Patrol średnio1,99ms,max5,65;Fog.Render średnio3,81,max5,76. BoundaryKnowledge max0,19,aleSoftFog4,75/Tiles2,97 iświatło/pokrycie kamery nadal skaczą. Cel stabilnych60FPS NIEOSIĄGNIĘTY. Pełne dane, ograniczenia i pozostałe cele w handoffie karty.

## Wybór źródeł światła i skoki przygotowania masek — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Pomijanie sortowania/koron dla niewidocznych pochodni, odległości przed zapytaniami warstw, pustej enumeracji wszystkich drzew przy braku ognia. Lokalny cache dokładnie identycznych geometrii siatki między klatkami animacji oraz bezpośrednia tablica indeksów bez wzrostu List. Wygląd/shadery/cztery próbki/cadence/promienie i stabilny wybór ośmiu źródeł zachowane. Kontrolowane560→102 zapytań sortowania,40 klatek/1mesh; nie są to pomiary FPS. **86 odrębnych EditMode PASS**, finalne14/14; jeden niezależny FAIL geometrii granicy areny Enta reprodukuje się na pełnym runtime baseline sprzed passa (testu nie osłabiono). Dokładne pixels/vertices/UV/indices/bounds/indexFormat PASS. Automatyczny import, kompilacja, final source restoration i self-review PASS. Nowe liczniki rozdzielają źródła/presentation/mesh build. Końcowy pomiar użytkownika wykonany; stabilne60FPS nieosiągnięte. Bez ręcznego Refresh/wiring, serialized state, Node, dodatkowego Unity, commit/push. Dowody: `LocalHandoffs/light-source-mesh-pass-20261017/HANDOFF.md`, `verification.json`.

Końcowy ważny zapis878klatek:mediana12,73ms,p9518,37,max27,46;89,64% wbudżecie60FPS. SourceSelection średnio2,02→0,155ms,max12,86→0,239;PrepareDraws0,863→0,096,max7,71→0,217.337MeshLookup/0MeshBuild, więc zimne budowy nie były mierzone. Inna trasa/obiekty niż przed; nie przypisywać całej poprawyFPS do kodu. Stabilne60FPS NIEOSIĄGNIĘTE. Największe pozostałe klatki: patrol/ruch,detection,fog i FollowersTick12,18ms. Dane i ograniczenia w handoffie karty.

## Maski cieni i cienie kontaktowe — 2026-10-17 — READY_FOR_SMOKE / CEL60FPS OTWARTY

Przygotowanie geometrii obiektów raz dla wszystkich świateł i ponowne użycie ich danych MPB; wspólne zapytania komórek podczas jednej przebudowy cieni kontaktowych oraz ograniczony cache oryginalnych pasm. Shader/cztery próbki/rozdzielczość/cadence i wygląd zachowane. Kompilacja i automatyczny import PASS, **69 odrębnych EditMode PASS**, zero skip: dokładne piksele masek i geometria kontaktów względem starej ścieżki oraz regresja lifecycle/fog/coverage. Kontrolowany przykład zapytań kontaktowych 218→6, bez interpretowania tego jako FPS. Self-review PASS; bez ręcznego Refresh/wiring, zmian serialized state/save, Node, drugiego procesu Unity, commit/push. Końcowy 12-sekundowy pomiar długiego lasu wykonany; stabilne60FPS nieosiągnięte. Dowody: `LocalHandoffs/shadow-contact-pass-20261017/HANDOFF.md`, `verification.json`.

Końcowy pomiar:822 skalibrowane klatki,10 nowych celów/26 fogRender/94 refresh światła. Mediana13,50ms,p9523,18,max39,49;78,22% wbudżecie60FPS. Cały przejazd gorszy od poprzedniego (inne obiekty/pozycje, nie replay), stabilne60FPS NIEOSIĄGNIĘTE. Maski średnio1,60→1,19ms/refresh, kontakty1,40→1,46ms. Największy skok SourceSelection12,86ms; osobny PrepareDraws7,71ms. Następne cele: wybór źródeł/odbiorcy światła/fog i diagnoza rzadkiego przygotowania masek. Szczegóły w handoffie, bez dalszych zmian kodu/importów.

## Nowy cel ruchu: walidacja, planowanie i wygładzanie — 2026-10-17 — READY_FOR_SMOKE

Cała obsługa kliknięcia używa istniejącego indeksowanego scope nawigacji: resolution, endpoint, planner Safe, diagnostyka failed query i smoothing. Warunki oraz trasy, kolizje, drzwi/schody, tempo, save i serialized state bez zmian. Dodane osobne markery etapów. **79/79 testów EditMode PASS**, compile i self-review PASS. Kontrolowany physicaldivider + 200 odległych przeszkód, 4 naprzemienne próbki: nieindeksowane planowanie+smoothing **58,76 ms**, pełny produkcyjny klik z indeksem **1,46 ms**, z identycznymi native corners. To mikrobenchmark, nie FPS. User opuścił Play przed pracą; import automatyczny bez dodatkowego stopPlay/Refresh. Końcowy profil90frames złapał2kliki: cały NewTarget **0,43 ms**, MeshPlan **0,34 ms** na wywołanie. BezProfilera1197próbek/12,011s: mediana **8,56 ms**,p95 **20,22 ms**,max **43,48 ms**, **91,23%** w budżecie60FPS; odstępy monotoniczne potwierdzająp95~20,44ms. Stabilnych60FPS nadal niepotwierdzono; inne przejazdy/load uniemożliwiają przypisanie wzrostup95 względem wcześniejszej próbki do tej poprawki. Manual smoke pending. Ciężki per-frame kolektor wyłączony. Dowody `LocalHandoffs/new-target-planning-pass-20261017/HANDOFF.md`, `verification.json`, `tests.xml`. Bez commita/pusha.

## Skoki podczas eksploracji — 2026-10-17 — DIAGNOSED

Per-frame zapis523klatek z dokładnymp95 i top5CPU/wall, spójność danychPASS, compilePASS. Skoki gry wskazują na planowanie nowego celu kliknięcia:MeshPlan17–22ms, callerParty.Update do32ms, maxPlayerLoop45,93ms. AdvanceRoute korzysta już z indeksu, ale walidacja/planowanie/smoothing nowego celu lidera nadal skanują pełną listę obiektów poza batch. Osobny skok bez MeshPlan wymaga rozdzielenia endpoint/failed diagnostic. Wallp9548ms to w konkretnej klatce tylko6,15ms gry i41,25msEditor. Kolektor sam2901ms/12,27s,maxbatch81ms:nie traktować tych profiled wallspikes jako FPS gry. Runtime w tym badaniu nie zmieniany. Kolejny zakres:batch dla całego planowania celu, nie wycinanie walidacji. Dowody `LocalHandoffs/exploration-spikes-pass-20261017/HANDOFF.md`, `profiled-spikes.json`, `spike-summary.json`. Bezcommita/pusha.

## Ruch lidera po kliknięciu — 2026-10-17 — READY_FOR_SMOKE

AdvanceRoute wykorzystuje istniejący scope indeksowanych przeszkód, z odświeżeniem przy każdym kroku i zachowaniem zagnieżdżonego kontekstu. Eliminuje pełne skany obiektów piętra dla każdego punktu walidacji. Warunki Physics, tempo/trasy, schody/drzwi/niszczenie i save bez zmian. **77 odrębnych testów PASS**, self-review i scoped diff check PASS. Kontrolowany długi segment na piętrze1000 przeszkód: pełna enumeracja576,105→indeks2,590 ms/walidację (12 naprzemiennych próbek); nie jest to cały FPS. Alokacje niedostępne: licznik Unity zwraca0 również dla sentinel8KiB. Import automatyczny po jednym stopPlay, bez ręcznego Refresh/nowego procesu Unity/commita/pusha. Live profil lidera8,60→0,50ms/klatkę (5,77→0,38ms/krok; przed90/po56 klatek, różne pozycje/load). Końcowy zapis bez Profilera1332próbki/12,012s:mediana8,52ms,p95 13,32ms,max29,44ms,99,17%w budżecie60FPS; monotoniczne odstępy potwierdzają i pokrywają11,987s. Sporadyczne skoki pozostają; brak gwarancji stałych60FPS dla całej gry. Manual smoke pending. Dowody `LocalHandoffs/leader-click-movement-pass-20261017/HANDOFF.md`, `verification.json`.

## Kierownica uruchamia ruch drużyny — 2026-10-17 — READY_FOR_SMOKE

Wspólne osie Legacy InputManager mieszały WASD/strzałki z joystickami. Odczyt ruchu drużyny (miasto/loch), kamer eksploracji/walki i anulowania dojścia zastąpiony odczytem samych klawiszy. Brak zmian ustawień InputManager/scen/prefabów/save. Kompilacja Views/testów PASS, końcowe **37/37 testów PASS** w obecnym GUI Unity, automatyczny import bez ręcznego Refresh. Self-review i scoped diff check PASS. Fizyczna kierownica: smoke użytkownika, niezweryfikowane przez agenta. Dowody: `LocalHandoffs/keyboard-wheel-fix-20261017/HANDOFF.md`, `live-tests.xml`. Niezależny fix; końcowa weryfikacja passa FPS patroli/pułapek/oświetlenia pozostaje otwarta.

`KANBAN.md` jest głównym źródłem prawdy dla aktualnego stanu prac,
priorytetów i wyboru kolejnej karty. Starsze roadmapy mają charakter referencyjny.

# Godmist - Kanban

## Niewidoczna prezentacja — 2026-10-17 — PASS ZWERYFIKOWANY / CEL 60 FPS OTWARTY

Odcinanie prezentacji poza kamerą i lokalna mgła płaskiego lasu zaimportowane. **257 unikalnych testów PASS**, 26 porównań obrazu full/local PASS (tylko animowane płomienie pominięte, limit <5%). Kontrolowana mgła lasu **12,95→3,79 ms**. Pomijane zapytania party LOS dla pustych pasków wykrycia; samo wykrywanie działa bez zmian. Bez zmian AI/fizyki/walki/jakości/skali/save. Jeden GUI Unity, import automatyczny; końcowe 31 testów PASS.

Końcowy live bez Profilera: **748 próbek / 12,014 s, około 62 FPS średnio**. Mediana delta **13,82 ms**, p95 **32,46 ms**. Monotoniczne odstępy aktualizacji potwierdzają: mediana **13,85 ms**, p95 **32,48 ms**, tylko **69,52% w budżecie 16,67 ms**. Cel stałych 60 FPS NIE osiągnięty. Dodatkowy Profiler obejmuje już walkę, więc nie jest porównaniem identycznego stanu; wskazuje patrol/pułapki i refresh świateł jako pozostałe koszty. Nie mylić około 30 ms narzutu Profiler.FlushMemoryCounters z kosztem gry. Dowody/limity: `LocalHandoffs/visibility-budget-pass-20261017/HANDOFF.md`, `verification.json`. Bez commita/pusha.

## Mgła i patrole — kolejny pass — 2026-10-17 — READY_FOR_SMOKE

Płaski las odświeża granice/podszycie ze zmienionych endpointów fog; pełna invalidacja dla mapy/piętra/projekcji/debug/topologii i osobna dla pamięci bram. Soft-fog target cache bez restartowania przejść, tint bez ponownego SetTile. Patrole: bez BFS do sąsiedniego waypointu, memoizacja wyłącznie w jednym query, mniej odczytów pozycji i brak tablic OverlapCircleAll. Bez zmiany zasad, cadence, tras/szybkości/kolizji, grafik/skali, liczby pokojów/walk i save.

**228/228 testów PASS**, 16 zgodnych SHA prezentacji fog przed/po. Kontrolowany las: fog **29,38→14,92 ms**, patrole sense **4,72→3,70 ms**, ruch **3,25→2,75 ms**. Katakumby sense **2,75→1,47 ms**, ruch **1,72→0,97 ms**. Live fog refresh **35,93→21,40 ms**, patrol **5,89→3,93 ms/klatkę**, p95 **89,23→71,31 ms**; mediana całej klatki **46,30→47,61 ms** przy wzroście narzutu profilera i 98→86 aktorach. Nadal pozostają skoki/SortingGroups; nie deklarować stałego FPS. Jeden GUI Unity, import/testy automatyczne, bez serii Refresh/nowych procesów/commita/pusha. Smoke pending. Dowody/ograniczenia: `LocalHandoffs/fog-patrol-pass-20261017/HANDOFF.md`, `verification.json`.

## Oświetlenie i cienie lasu — 2026-10-17 — READY_FOR_SMOKE

Lokalny indeks nieruchomych drzew/podłoża zastępuje pełne skany piętra podczas odświeżania oświetlenia i cieni kontaktowych. Runtime props pozostają live; przebudowa i zmiana transformu właściciela unieważniają indeks. Katakumby zachowują pierwotną enumerację. Bez zmiany jakości świateł/cieni, grafik/skali, AI i save. **180 odrębnych testów PASS**, 10 zgodnych porównań SHA cieni/masek przed/po. Kontrolowany las: odświeżenie **21,35→3,73 ms**, odkryty **42,68→7,46 ms**. Live oświetlenie **26,26→8,26 ms/wywołanie**; cała klatka mediana **46,30 ms**, p95 **89,23 ms**. Osobne wejścia/różne układy i 86→98 aktorów oraz większy narzut profilera nie pozwalają deklarować poprawy całego FPS. Pozostają fog/patrole/SortingGroups; katakumby bez wykazanej poprawy czasu. Import automatyczny, bez serii Refresh/nowych procesów Unity, commita i pusha. Smoke pending. Dowody: `LocalHandoffs/dungeon-lighting-pass-20261016/HANDOFF.md`, `verification.json`.

## Pełne klatki długiego lasu — fog i patrole — 2026-10-16 — READY_FOR_SMOKE

Usunięte powtarzane zapytania o te same granice leśnego podłoża oraz pełne skany przeszkód w zapytaniach patroli. Aktualizowane tylko zmienione podłoże/korony; lazy stany fog dla nieodkrytych sprite’ów, zachowane płynne odsłanianie i natychmiastowe ukrywanie. Native ściany katakumb zachowują odświeżanie nakładek. 178 odrębnych przypadków PASS po retestach, self-review PASS. Bez zmian liczby pokoi/walk, reguł AI, grafik/skali i save.

Rzeczywiste próbki po90 klatek długiego lasu: mediana43,14→34,36ms, p95155,41→88,23ms. Różne układy/83→86aktorów i narzut profilera ograniczają porównanie; nadal pozostają skoki, głównie oświetlenie/contact shadows i native SortingGroups. Nie deklarować stałego FPS ani całkowitego usunięcia lagów. Harness fog lasu116,86→45,91ms w finalnym reteście (pośrednio33ms); katakumb34,14→9,34ms. Pełny PlayerLoop katakumb po zmianie NOT RUN. Kod zaimportowany, bez manualnego wiring/Refresh, bez commita/pusha. Szczegóły/ograniczenia: LocalHandoffs/dungeon-whole-frame-20261016/HANDOFF.md i verification.json. Smoke pending.
## Duży las — kolejne skrócenie wejścia — 2026-10-16 — READY_FOR_SMOKE

Jeden finalny bake podłoża, cache geometrii na czas bake, właściciele granic bez ukrytych murów oraz zbiorcza rezerwacja poziomów sortowania leśnych obiektów. Dwa duże lasy w harnessie **18–20 → 11–13 s**, identyczne SHA układu, sprite’ów i maski podłoża. **219 unikatowych przypadków PASS** po retestach. Native NavMesh zachowuje geometrię/voxel; fizyczne źródła pobierane raz. Bez zmian liczby walk/pokoi, balansu, assetów/save. Katakumby kontrolne bez wykazanej poprawy czasu. Handoff/ograniczenia pomiaru: `LocalHandoffs/dungeon-generation-pass2-20261016/HANDOFF.md`, `verification.json`. Smoke pending, bez commita/pusha.

## Regresja FPS — wielokrotny skan topologii przy odczycie pozycji — 2026-10-16 — READY_FOR_SMOKE

Znalezione i poprawione `HasElevatedSurfaces`: stała właściwość mapy zamiast pełnego `Any` po 10 tys. pól przy każdym odczycie pozycji. Pomiar lasu: detection 355→5,1 ms, patrol 290→10,8 ms; katakumby 34,4→1,5 i 30,7→3,1 ms. **235 regresji + 5 końcowych testów profilingu PASS**. To koszty etapów, nie końcowy FPS; osobne piki planowania patroli i oświetlenia pozostają widoczne w raporcie. Kod zaimportowany w istniejącym Editorze, bez zmian mechanik/assetów/save. Smoke pending. Dowody: `LocalHandoffs/dungeon-frame-pass-20261016/verification.json` i `HANDOFF.md`.

## Regresje długości wypraw — tablica i czas wejścia — 2026-10-16 — READY_FOR_SMOKE

Podpisy ofert mieszczą się w przyciskach po native aktualizacji layoutu i skróceniu „Poziom” do „Poz.”. Wyszukiwanie narożników ścian i źródła NavMesh zoptymalizowane bez zmiany długości/liczb walk. Sam widok długiego lasu 80–82 s → 5,2 s; harness wejścia 4,8–16,4 s dla lasu, 14,5–15,5 s dla katakumb. **70/70 EditMode PASS**, render tablicy/self-review PASS; manual smoke pending. Dowody i granice pomiaru: `LocalHandoffs/dungeon-entry-pass-20261016/HANDOFF.md`, `verification.json`, `verified.xml`. Bez manualnego wiring/Refresh, bez commita/pusha.

## Furia — jeden bazowy zysk na skill ofensywny — 2026-10-16

READY_FOR_SMOKE.590 aktualnych odrębnych testów PASS,14 pełnych symulacji wypraw/95 walk bez limitów. Baseline48 9/12 i ulepszony Zabójca48 2/2 ukończone; pełne wyniki w handoffie. Manual gameplay NOT RUN. Bez commit/push; bez ręcznego Refresh/zamykania Unity.

Zlecone globalnie: wojownik i wszystkie NPC z furią, +10 raz za użycie skilla ofensywnego, również przy pudle, bez mnożenia przez cele/trafienia. Owner przeniesiony z CombatDamageSystem do SkillExecutionSystem po walidacji i pobraniu kosztów. Opóźnione trafienia, DoT, reakcje i automatyczne kopie nie powtarzają bazowego zysku; wsparcie/przyspieszenia na rzucającym nie generują go. Zachowane jawne talenty, modyfikatory ekwipunku, limit/blokada/decay/feedback, kompatybilny ID konfiguracji, standardowe starty i koszt50 Zadania. Dowody i stan końcowy: LocalHandoffs/rage-per-skill-pass-20261016/HANDOFF.md.

## Uwagi z playtestu — backlog 15 tematów — PT-01, PT-02, PT-03, PT-04, PT-05, PT-07, PT-08, PT-09, PT-10, PT-11, PT-12, PT-13, PT-14 i PT-15 READY_FOR_SMOKE, 15/15 z implementacją; PT-06 SIMULATIONS PASS (2026-10-16)

Źródło: uwagi użytkownika po około 10 minutach gry podczas pracy nad VFX oraz późniejsze dopiski o lądowaniu sztyletu i inicjowaniu walki. Pierwotne polecenie obejmowało zapisanie całej listy bez implementacji. Po zleceniu kontynuacji VFX, PT-01 zostało zaimplementowane w passie6 i czeka na smoke. PT-11 zaimplementowane w passie lasu i kosztu renderowania, PT-09 w passie camping kitu i lifecycle tooltipów, PT-10 w passie dwukliku przedmiotów, PT-12 w passie skrzyń, PT-13 w passie modeli kart, PT-07 w passie licznika sztyletów i PT-08 w passie XP/punktów rozwoju, PT-14 w passie lądowania sztyletu, PT-05 w passie nazewnictwa i PT-03 w passie nagrody za odzysk sztyletów oraz PT-02 w passie Magicznej Tarczy oraz PT-04 w passie zasobów przeciwników. PT-15 zaimplementowano w passie inicjowania walki; PT-06 w kalibracji skalowania pełnymi symulacjami — wszystkie15 passów READY_FOR_SMOKE; testy VFX/lasu nie oznaczają naprawienia reszty backlogu.

**Rozróżnienie decyzji:** wymagania i obserwacje użytkownika są podstawą backlogu. Liczby i szczegóły zaproponowane przez asystenta są oznaczone jako robocze; samo zapisanie listy nie zatwierdza ich do wdrożenia. Kolejność prac poniżej jest również propozycją.

### PT-01 — VFX przez pełny czas pozy castowania

- [x] Implementacja pass6 — READY_FOR_SMOKE; manual smoke NOT RUN. Statyczny VFX wykonania skilla ma być widoczny przez cały właściwy freeze-frame castowania, tak długo jak odpowiadająca mu poza postaci. Poprzednio znikał za wcześnie; teraz cele współdzielą deadline pozy castowania.
- Zsynchronizować jego początek/koniec z istniejącym ownerem prezentacji skilla, zamiast nadawać niezależne krótsze czasy.
- Przy wielokrotnych trafieniach zachować faktyczną kolejność i pudła. Statusy/pola/aury zachowują osobny lifecycle; nie wydłużać ich przypadkowo czasem pozy.

### PT-02 — Nerf Magicznej Tarczy Alison

- [x] Magiczna Tarcza:250% średniego ataku,3 własne tury odbiorcy, recast zastępuje poprzednią i odnawia czas — READY_FOR_SMOKE,167/167 testów PASS, manual smoke NOT RUN.
- Propozycja użytkownika: obniżenie siły do około250% wartości ataku.
- Reguła zatwierdzona przez użytkownika:3 tury i zastępowanie; inne źródła tarcz zachowane. Poza walką istniejące6-sekundowe ticki eksploracji, save przechowuje pozostały czas i migruje stare bezterminowe warstwy.
- Przy realizacji sprawdzić definicję wartości ataku, odliczanie, kumulację, przygotowanie przed walką i inne źródła tarcz.

### PT-03 — Nagroda za odzyskiwanie sztyletów

- [x] Udany odzysk daje +8 impetu — READY_FOR_SMOKE;203 odrębne przypadki PASS, w tym15 nowych. Manual smoke NOT RUN.
- Zakres zaproponowany przez użytkownika: płaskie+5–10 impetu/pędu.
- W ramach dyspozycji użytkownika „5–10 wedle uznania” wybrano **+8 impetu za udany odzysk**: raz za zebranie z jednego pola, odzysk skillem lub wyjęcie z celu; bez mnożenia przez wielkość stosu. Obcy/pusty odzysk i pełny zapas bez nagrody, zasób z normalnym limitem i blokadami. Zgrupowane podnoszenie zachowuje nadmiar stosu.
- Sprawdzić wszystkie istniejące sposoby odzyskiwania, koszty i możliwość bezkosztowego powtarzania upuszczenia/podniesienia.

### PT-04 — Zasoby przeciwników i ich stan początkowy

- [x] Profile zasobów i rzeczywisty start przy pierwszym dołączeniu do walki — READY_FOR_SMOKE; po korekcie standardowych startów438/438 testów PASS; manual smoke NOT RUN.
- Propozycja użytkownika: różne typy zasobów podobnie jak u postaci; przy wściekłości koszty umiejętności mniejsze o około50%.
- Obowiązujące: furia0/50, impet0/200, mana100/100; role i bossowie opisani w sekcji realizacji. Koszty furii obniżone o50%, zaokrąglane w górę do5; mana regen15/turę, inne zasoby według zwykłych reguł.
- Audyt23 definicji/67 skilli, idempotentny authoring, migracja starych referencji klas i testy AI/realnego wejścia do walki. Ruch strzelców za normalny koszt może odbudować impet; brak refillów przy rebindzie/fazie.

### PT-05 — Jedna nazwa odporności na Suppression

- [x] Ujednolicono kategorię w UI/opisach jako „Odporność na kontrolę” / „Kontrola” — READY_FOR_SMOKE, 32/32 testów PASS.
- Zachowano nazwę istniejącej karty postaci po sprawdzeniu zakresu mechaniki. Glosariusz wymienia siedem statusów używających tej odporności; bez zmiany zasad walki.
- Uwzględnić tooltipy, statystyki, opisy skilli, przedmiotów/statusów i wspólny formatter/glosariusz; nie zmieniać mechaniki przy samej korekcie nazwy.



### PT-06 — Skalowanie i attrition wyprawy

#### Aktualny pass encounterów — 2026-10-16 — READY_FOR_SMOKE

- [x] Wyrównana średnia presja zwykłych składów Lasu względem Katakumb, z zachowaniem rozrzutu: 2/2/3/3/3/3/4 osób, siedem typów, różne role. Bez nowych korekt statystyk/skilli/zasobów/AI/bossów. Nowe definicje `.balanced` dla nowych wypraw; stare aktywne snapshoty zachowują oryginalne składy. Hook authoringu i brakujące sloty 2/3 podłączone.
- **200 unikalnych testów PASS**, 560 kontrolowanych walk oraz 72 serie/894 walki (razem 1454, bez limitów). W krótkich full clearach L1 oba lochy 5/6 ukończeń, L48 oba 6/6. Średnia presja obrażeń + netto strata HP między walkami w krótkich seriach: Las/Katakumby 1,0446. W pojedynczych walkach 1,121, końcowe HP różni się średnio maksymalnie o 1,2 pp/poziom. Różnice konkretnych seedów, dłuższych tras i bossów pozostają; pełne ograniczenia w raporcie.
- Conditional krótkie full cleary + standardowy quest: około 19 wypraw do L20 w obu lochach. Scouting używa realnego grafu/bram, bez losowego usuwania obowiązkowych walk; omijanie wewnątrz pokoju nie jest modelowane. Save/load, idempotencja, legalne spawny i cache aktualnych pul PASS. Manual gameplay/FPS NOT RUN. Dowody: `LocalHandoffs/encounter-balance-pass-20261016/HANDOFF.md`, `verification.json`. Bez manualnego wiring/commit/push; jeden główny batch Unity naraz, dwa workery; ustawienia symulacji przywrócone.

#### Długości i bazy NPC — wcześniejszy etap

- [x] **Aktualizacja długości i baz NPC — 2026-10-16 — READY_FOR_SMOKE.** Krótka/normalna/długa: 9–11/18–22/27–33 walk, 28–34/56–68/84–102 pokojów, 0/1/2 zestawy obozowe i quest ukończenia ×1/×2/×3. Katakumby 1/2/3 piętra, Las jedno, 30–40% pokojów z walką. Losowane długości widoczne na tablicy, zapisane dla ofert/aktywnego lochu/oczekującego bossa. Stare aktywne lochy zachowują układ i dawny przydział.
- [x] Bazy HP ×0,4 i ATK ×0,6, usunięta baza przeniesiona do późnych przyrostów /199: L50 zachowany, L48 nieznacznie niższy. Widmowy Rycerz nadal 3 HP, fazy zgodne, bohaterowie bez nowych zmian. Krótka zachowuje dawny zakres około 10 walk i około 19 ukończonych katakumb do L20 z jednym standardowym questem.
- Weryfikacja: **232 unikalne testy PASS**, 36 układów, pełne obiekty długich map, zgodność wszystkich 23 profili z symulatorem i idempotencja PASS. 32 unikalne serie/449 walk, 30 ukończonych. Krótkie L1 8/8; krótkie L10/28/48/50 16/16. Dłuższe L1/48 6/8 — dwie porażki startowych Katakumb mimo obozów; Las nadal łatwiejszy. Manual gameplay/FPS NOT RUN. Dowody/ograniczenia: `LocalHandoffs/dungeon-length-pass-20261016/HANDOFF.md`; zasady: `BALANCE_EXPEDITION_LENGTHS.md`. Bez manualnego wiring/commit/push; jeden proces batch naraz, dwa workery. Poniższy opis jest historycznym etapem, poprzedzającym zmianę baz i reguły długości.

#### Poprzednia kalibracja — zapis historyczny

- [x] PT-06 — READY_FOR_SMOKE, EXPEDITION SIMULATIONS PASS (92 wypraw/523 walk; 518/518 testów PASS). Baseline48,199 przyrostu, bohaterowie bez zmian. Zwykłe NPC: HP/ATK×0,6 wczesnego przyrostu, płynnie do HP×2/ATK×2,5 na48; bossowie osobno. Wczesna korekta ma dokładnie0 wpływu na48/50. HP/zasoby/statusy przenoszone między encounterami; rzeczywiste zestawy obozowe jeden/piętro,40% HP/many, bez darmowego leczenia. Uwzględniono uzdrowienie/regenerację paladyna i bandaż wojownika. Stare wyniki bez tego leczenia odrzucone. Korekta po decyzji użytkownika: Wykonać Zadanie kosztuje50 furii zamiast20 i może ponawiać przyspieszenie w dodatkowej turze, jeśli boss ponownie uzbiera zasób; usunięto blokadę dodatkowej tury. Po globalnej korekcie furii obszarówki zwracają10 za cały skill, więc dwa skille zwracają20. Korekta50 furii:90/90 testów PASS,6 wypraw/65 walk bez limitów, baseline48 4/4 i ulepszenia48 2/2 ukończone; identyczny stan przed bossem. Dowody:LocalHandoffs/enemy-scaling-pass-20261016/slayer-rage-cost/HANDOFF.md.23 definicje bonusów + koszt/tooltip Zadania, oryginalne pola bazowe/przyrostów, chronione statystyki/arkusz i zasady zasobów zachowane. Testy/import w otwartym Unity bez ręcznego Refresh. Kontrolowana arena12×10 i generowane prawdziwe składy/liczby; nie jest to test wszystkich geometrii/buildów aniFPS. Gear48/50 z read-only fixture arkusza; produkcyjny import wyższych progów nie był rozszerzany. Pełne wyniki:LocalHandoffs/enemy-scaling-pass-20261016/HANDOFF.md,verification.json,final-expedition-results.json. Manual gameplay NOT RUN, nie jest wymaganym dowodem kalibracji. Bez commit/push.

### PT-07 — Jeden licznik sztyletów Jerzego w HUD

- [x] Jeden licznik przy PA aktywnego sojuszniczego zwiadowcy, pod portretem; aktualny zapas i limit z BattleUnit.
- [x] Usunięto powtarzany zapas pod ikonami; koszty w tooltipach i blokady pozostały.
- [x] Hover podświetla własne leżące sztylety, z zachowaniem widoczności i sprzątaniem przy zmianie tury/sesji.
- READY_FOR_SMOKE;135 odrębnych testów PASS, render GPU/self-review PASS; manual smoke/FPS NOT RUN. Szczegóły i dowody poniżej.
- Zachować koszt skilla i czytelny stan braku wymaganych sztyletów; szczegóły licznika w tooltipie.

### PT-08 — Feedback XP, poziomu i niewydanych punktów

- [x] +N XP i „Zdobyto poziom!” na rzeczywistej nagrodzie, wspólny event dla walk/questów/konsoli, bez komunikatu przy wczytaniu.
- [x] +N w prawym górnym rogu portretu; bezpośredni rozwój właściwej postaci, ponowne użycie okna i zachowana blokada w walce.
- [x] „Rozwój (+N)” w karcie, aktualizowany po nagrodzie i wydaniu punktów. READY_FOR_SMOKE;118/118 testów PASS, render/self-review PASS; manual smoke NOT RUN.
- Dopasować liczbę do rzeczywiście dostępnych punktów i istniejących typów rozwoju; nie mnożyć zbędnych komunikatów.

### PT-09 — Zbędny i zawieszający się tooltip camping kitu

- [x] Usunąć zbędny tooltip wyświetlany po użyciu camping kitu — oba wejścia: przedmiot i podłoga.
- [x] Naprawić pozostający na ekranie tooltip po wykonaniu akcji/usunięciu przedmiotu. READY_FOR_SMOKE;169/169 testów PASS, manual smoke NOT RUN.
- [x] Lifecycle właściciela: disable/rebind/close/destroy, w tym przypięte i oczekujące karty; zachowane niezwiązane tooltipy i feedback odmowy.

### PT-10 — Double-LPM jako domyślne użycie/założenie przedmiotu

- [x] Double-LPM do użycia/założenia i zdejmowania w karcie postaci — READY_FOR_SMOKE;223/223 testów PASS, manual smoke NOT RUN.
- Zaimplementowano: pojedynczy klik wybiera/inspektuje slot, podwójny wykonuje akcję domyślną, PPM menu, Enter/Submit akcję domyślną.
- Zachowano intencjonalne akcje handlu/schowka/łupu, drag i walidację consumable wymagających walki/targetu. Szczegóły i dowody w sekcji PT-10 poniżej.

### PT-11 — Las: czytelność, skala, granice i FPS

Implementacja READY_FOR_SMOKE; aktualny opis i dowody w sekcji PT-11 poniżej backlogu. CPU benchmark PASS; FPS pełnej gry i manual smoke NOT RUN.

- [x] Żywe postacie nadal są zasłaniane przez przeszkody/drzewa.
- [x] Ciała przeciwników także giną za przeszkodami.
- [x] Efekt zasłaniania/wygaszania postaci pokazany na screenie2 jest niepożądany. Użytkownik nie prosił o chowanie ani przezroczystość aktorów.
- [x] Ciało wilka jest znacząco większe od żywego wilka; narośl jest ogromna. Sprawdzić rozmiary widocznej grafiki, canvas, PPU i transformy w jednym spójnym pipeline; nie naprawiać rozmiaru doraźnym przestawieniem PPU kosztem wielkości piksela.
- [x] Niskie krzaki nie powinny tworzyć granicy mapy wyglądającej jak przejście. Wyraźnie odróżnić dekorację od nieprzekraczalnego brzegu.
- [x] Przeszkody zasłaniają preview zużycia PA przy ruchu (screen3). Wskaźniki taktyczne muszą pozostać czytelne ponad dekoracjami.
- [x] Zmierzyć i naprawić spadek FPS po kolejnej zmianie wizualnej. Podejrzenie użytkownika: mechanizm zasłaniania postaci. Przyczyna niepotwierdzona; konieczne profilowanie.
- Wymaganie: czytelne jednostki, ciała, granice i informacja taktyczna. Robocza propozycja asystenta: ewentualnie lokalnie wygaszać zasłaniającą przeszkodę, nie samą postać; finalną technikę dobrać po diagnozie i pomiarze kosztu.
- Dowody: [screen1 — zasłonięta postać](../LocalHandoffs/playtest-remarks-20261015/references/forest-obscured.png), [screen2 — niepożądana prezentacja zasłaniania](../LocalHandoffs/playtest-remarks-20261015/references/forest-occlusion-fade.png), [screen3 — zasłonięte preview PA](../LocalHandoffs/playtest-remarks-20261015/references/forest-movement-ap.png).

### PT-12 — Skrzynie: outline i interakcja LPM

- [x] Outline skrzyń lochów/kontenerów lasu po najechaniu — stabilny cyjan, natywny piksel, wspólny efekt sylwetki.
- [x] LPM do podejścia/interakcji, E jako alternatywa. READY_FOR_SMOKE;161/161 testów PASS, GPU render PASS, manual smoke NOT RUN.
- Zachowane reguły kluczy/loot, pathfinding, fog/elevation, UI i anulowanie rozkazu. Szczegóły i dowody w sekcji PT-12 poniżej.

### PT-13 — Aktualne modele postaci w kartach

- [x] Potwierdzono stare źródło VisualSprite/VS22; karty używają teraz aktualnej pierwszej klatki frontowego idle z resolvera świata.
- [x] Zachowano Point, proporcje i layout karty oraz skalę świata 0,7; twarze portretów bez zmian.
- READY_FOR_SMOKE; 78/78 testów PASS i cztery rendery kart sprawdzone. Manual smoke NOT RUN. Szczegóły i ograniczenie starszego audytu ikon Króla w sekcji PT-13 poniżej.
- Dowód: [screen4 — model w karcie wyposażenia](../LocalHandoffs/playtest-remarks-20261015/references/character-sheet-model.png).

### PT-14 — Animacja lądowania sztyletu po trafieniu przeciwnika

- [x] Animacja rzeczywistego sztyletu: łuk/obrót0,42s do jego legalnego pola odzyskania, bez dodatkowego przedmiotu. READY_FOR_SMOKE;153/153 testów PASS, render/self-review PASS; manual smoke/FPS NOT RUN.
- Wdrożone: po trafieniu wylot z wysokości kontaktu; przy pudle niski opad. Wbicie/zachowanie sztyletu respektuje gameplay. Po końcu ten sam sprite i pickup, z dotychczasową skalą.
- Wizualizacja ma respektować istniejące zasady wbicia/pozostawienia/odzyskania: nie tworzyć dodatkowego fizycznego sztyletu, jeśli mechanika go nie upuściła, ani lądowania w innym miejscu niż legalny pickup.
- Animacja przedmiotu jest osobna od freeze'ów skilla. Wykorzystano istniejącą zatwierdzoną grafikę Godmist; nie generowano ani nie zmieniano bitmap/importu/PPU.

### PT-15 — Gracz inicjuje walkę i zaskakuje przeciwników

- [x] Dowolny aktywny skill przed walką: PPM przeciwnika → Przygotuj atak → istniejący HUD skilli i wybór celu.
- [x] Każdy przeciwnik objęty otwarciem używa własnego paska p; dodatkoweAV = bazoweAV×(1−p), raz na pierwszą turę. p jest pobrane po walidacji i przed rozpatrzeniem akcji.
- [x] Normalny owner walki, alarm pokoju, widoczność i dołączanie; bez sztucznego1v1. Followerzy na wspólnych polach są ustawiani istniejącym resolverem; skill ponownie sprawdzony przed kosztami.
- [x] Zwykłe koszty pierwszej tury atakującego; pudło również rozpoczyna walkę. Podgląd i anulowanie bez obrażeń/kosztów/ticków. Wykrycie przeciwnika nadal może rozpocząć normalną walkę podczas przygotowania.
- READY_FOR_SMOKE;433/433 testów PASS,26 nowych, manual smoke/FPS NOT RUN. Zasoby standardowe zachowane. Szczegóły/dowody w sekcji realizacji PT-15.

### Kolejność realizacji — wszystkie wykonane passy oczekują smoke

1. Las/FPS/czytelność i preview PA (PT-11).
2. Synchronizacja VFX i bug tooltipu camping kitu (PT-01,PT-09).
3. Interakcje, modele kart i wskaźniki HUD/rozwoju (PT-07,PT-08,PT-10,PT-12,PT-13); wspólne nazewnictwo (PT-05).
4. Tarcza, nagroda za sztylety i animacja lądowania (PT-02,PT-03,PT-14).
5. Inicjowanie walki/zaskoczenie z normalnym dołączaniem (PT-15).
6. Mierzalny balans skalowania trudności (PT-06), uwzględniający wdrożone zasoby standardowe (PT-04) i powyższe mechaniki.

## PT-15 — Gracz otwiera walkę dowolnym skillem — 2026-10-16 — READY_FOR_SMOKE

Zatwierdzone przez użytkownika: wybór **dowolnego aktywnego skilla** przed walką i **osobny pasek wykrycia każdego przeciwnika**. Wejście: PPM na widocznym, wolnym przeciwniku → **Przygotuj atak**. Otwiera dotychczasowy HUD skilli i wybór celów (obszary, łańcuchy, kilka celów/trybów, obrót oraz dodatkowe wskazania mobilności/obiektów korzystają z tego samego kontrolera). Skill musi należeć do aktywnego zestawu, mieć legalny cel/zasięg/LOS oraz zwykłe koszty; brak darmowego ataku. Podgląd korzysta z drivera wyłącznie do targetowania, nie rejestruje walki i nie tyka tur, statusów, bossów, kosztów ani obrażeń. Zwykłe wykrywanie/patrole nadal działają; normalna walka przerywa przygotowanie. Esc anuluje wybór lub przygotowanie. Hipotetyczna kolejka tur oraz przyciski końca tury/ucieczki/przedmiotów są ukryte podczas przygotowania.

Zatwierdzenie używa normalnego ownera aktywacji walki, alarmu w pokoju i widoczności bojowej. Bez sztucznego ograniczenia do atakującego i celu; odlegli/oddzieleni sojusznicy oraz późniejsze posiłki dołączają według zwykłych zasad. Istniejący resolver rozdziela wspólne pola towarzyszy i respektuje rezerwacje innych walk. Jednostkowe wskazania celu są przemapowane na wynik ustawienia, a skill ponownie sprawdzany przed rejestracją i kosztami. Ewentualne dodatkowe cele obszaru rozszerzają początkową grupę w ograniczonym, rosnącym procesie; brak szukania co klatkę.

**p jest zapisane po poprawnej walidacji, przed uruchomieniem tury i rozpatrzeniem skilla.** DodatkoweAV danego przeciwnika = bazoweAV×(1−p), doliczone raz do jego początkowego pozostałegoAV. Atakujący rozpoczyna własną pierwszą turę, płaci zwykłePA/zasób/noże i zachowuje resztęPA. Pudło nadal rozpoczyna walkę. Bez trwałej zmiany szybkości, statusu do ponownego nakładania czy bonusu dla późniejszych dołączeń; merge zachowuje pozostałeAV. Zmiana fazy również nie daje ponownie zaskoczenia. Jeśli efekty samego początku tury uniemożliwią wcześniej legalny skill, walka pozostaje normalnie rozpoczęta i gracz wybiera dostępną akcję; nie dostaje darmowego ciosu.

Kompilacja/self-review PASS; końcowe **433/433 EditMode PASS**, w tym26 nowych przypadków (84 w fixture seamless). Scena testowa potwierdza przygotowanie/anulowanie oraz4 sojuszników+2 przeciwników, osobnepaski, zwykłe koszty, zachowanie nagród i różne pola przy nakładających się followerach. Sprawdzone także zaawansowane tryby targetowania, brak skutków w preview, nielegalne cele/zasięg/LOS/PA/mana/wymagania, pudło i brak powtarzania bonusu przy późniejszych dołączeniach. Wszystkie importy/testy w otwartym Editorze, bez ręcznych Refresh. Manual smoke/FPS NOT RUN; bez zmian artu/skali/PPU/scen/prefabów/schematu save, bez commita/pusha. Standardowe zasoby0/200,100/100,0/50 zachowane.

Dowody: `LocalHandoffs/combat-opening-pass-20261016/{verification.json,final-tests.xml,scoped.diff,HANDOFF.md}`. Pozostał **PT-06 — mierzalny balans przeciwników i skalowania**, bez zatwierdzonych nowych liczb.

## PT-04 — Standardowy start zasobów — 2026-10-16 — READY_FOR_SMOKE

**Obowiązująca decyzja użytkownika: impet0/200, mana100/100, furia0/50.** Starty50 impetu i60 many zostały wycofane. Dotyczy wszystkich23 definicji przeciwników, w tym obu faz bossów. Profile klas mają teraz standardowy start: mana100%, pozostałe0%. Te same wartości obowiązują przy tworzeniu oraz pierwszym dołączeniu do walki. Zapisany bieżący stan i zmiana fazy/rebind nie są traktowane jako nowe otwarcie walki i nie doładowują zasobu.

Zachowano typy zasobów, limity, regenerację, zatwierdzone koszty furii (połowa, zaokrąglanie w górę do5), bonus szybkości impetu ponad100 oraz zwykłe zasady generacji. Zmieniono wyłącznie początkowy procent w4 assetach klas przez Editor API i odpowiadające oczekiwania testów. Brak zmian artu/skali/PPU, scen czy prefabów.

Weryfikacja438/438 EditMode PASS: profile wszystkich23 definicji na poziomie10, rzeczywiste wejście do walki po eksploracji, AI, zasady zasobów, bossowie, save/load i bonus szybkości. Kompilacja i audyt zakresu PASS. Wszystko w istniejącym Editorze, bez ręcznego Refresh; manual smoke NOT RUN. Dowody: `LocalHandoffs/standard-enemy-resources-20261016/{tests.xml,verification.json,enemy-resource-profiles.json,scoped.diff,HANDOFF.md}`. Bez commita/pusha.

## Historia PT-04 — Korekta limitu impetu i bonus szybkości — 2026-10-16 — READY_FOR_SMOKE

Użytkownik wskazał, że limit100 wyłącza mechanikę szybkości ponad100 impetu. Poprawiono wszystkich8 przeciwników z impetem (w tym obie fazy Przywódcy) na **start50 / limit200**. Start klasy zmieniono na25%, żeby zachować zatwierdzone50 przy tworzeniu i pierwszym dołączeniu do walki. Furia0/50, mana60/100, regeneracja i koszty bez zmian.

Audyt wykrył dodatkowo, że udokumentowany bonus szybkości nie był wykonywany w runtime. Wspólny pipeline statystyk daje teraz **+4 szybkości za każde pełne25 impetu ponad100**:125→+4,150→+8,175→+12,200→+16. Ruch i odzysk aktualizują bonus; wydanie, wyssanie i reset zasobu go obniżają. Obsłużone źródła: reguły ruchu, odzysk/skille/sztylety, drain i upkeep skilli, obiekty przywołane, reset eksploracji/wejście do walki, mikstury, odzyskanie towarzysza i konsola. Bonus jest wyliczany ponownie po wczytaniu save/stat pipeline, nie zapisuje się jako osobny efekt. Odświeżanie po zmianie zasobu następuje tylko przy przekroczeniu progu szybkości, bez skanowania co klatkę. Szybkość wpływa na zwykłe obliczenia PA/AV przyszłych tur, bez zwrotu bieżących PA i bez zmiany aktualnie zaplanowanego AV.

Weryfikacja: końcowe114/114 testów PASS (63 profile/bonus,51 Scout), w tym17 nowych przypadków. Łącznie358 odrębnych przypadków z najnowszym PASS:244 niezmienione przypadki z pierwszego przebiegu oraz114 końcowych; źródło każdego w latest-test-results.json. Pierwsze3 failure dotyczyły konfiguracji testów Scout: ręczna celność poza Stats oraz tonik mieszany z bonusem impetu. Poprawiono fixture, bez osłabiania kontraktu obrażeń/toniku. Importy i testy wykonano w otwartym Editorze bez ręcznych Refresh. Audyt dysku potwierdza8 definicji limit200 i2 klasy start25%. Manual smoke NOT RUN.

Dowody: `LocalHandoffs/momentum-cap-correction-20261016/{verification.json,initial-tests.xml,final-tests.xml,latest-test-results.json,import-audit.json,scoped.diff,HANDOFF.md}`. Smoke: sprawdzić start50/200, ruch124→125, spadek szybkości po skillu oraz powtarzane odświeżanie bez kumulacji bonusu. Bez zmiany grafiki/skali/PPU, bez commita/pusha.

## Historia PT-04 — Zasoby przeciwników i start walki — 2026-10-16 — READY_FOR_SMOKE

Użytkownik zatwierdził role i wartości; doprecyzował zaokrąglanie po obniżce do **wielokrotności5 w górę**. Furia0/50: kościany wojownik, obrońca, ghoul, umarlak, widmowy rycerz i obie fazy Szkieleta Zabójcy. Impet50/200: łucznicy katakumb/lasu, strzelec, rzezimieszek, kłusownik, wilk i obie fazy Przywódcy. Mana60/100: okultysta, upiór, druid, drzewo, obie fazy enta i Umarłego Króla. Mana regeneruje15 na początku własnej tury (pierwsza regeneracja60→75); furia/impet mają regenerację statystyczną0 i dotychczasowe reguły generowania z ataków/ruchu. Koszt furii = ceil(staryKoszt/10)×5, np.35→20,55→30; koszt0 pozostaje0. Pozostałe koszty bez zmian.

Dodano `CharacterClassData.StartingResourcePercent` z domyślnym-1 zachowującym stare reguły graczy. Trzy nowe warianty klas przeciwników (katakumby: furia/impet, las: impet) i dwa istniejące profile many. `CharacterInstanceFactory` stosuje profil po skalowaniu. Przy pierwszym dołączeniu przeciwnika `ScoutKnifeSystem.ResetForCombat` stosuje start raz, korzystając z istniejącego HasInitializedCombatResources — eksploracja nie zeruje w ten sposób otwarcia impetu i nie zmienia go w pełną manę. Rebind/zmiana fazy nie doładowują; gracze są pomijani. Obie fazy bossa zachowują ten sam zasób.

`EnemyResourceProfilesSetup` aktualizuje wąsko23 definicje/67 skilli startowych, klasy, ich rejestrację i referencje16 encounterów. Tylko zasoby/koszty; brak zmian HP, obrażeń, odporności, PA, składu/liczby encounterów, artu/PPU/skali/scen/prefabów. Audyt77 istniejących assetów +3 nowych klas: bez zmian istniejących wartości poza zatwierdzonymi polami zasobów/referencjami; Unity dopisało domyślne pola i znormalizowało zapis6 nazw bez zmiany tekstu. Aktualizacja idempotentna, nie dzieli kosztów ponownie; zapisuje tylko dotknięte assety. Podpięta do istniejącego authoringu lasu/katakumb/bossów.

AI: przeciwnik dystansowy z impetem może raz na turę wykonać do2 bezpiecznych kroków na pozycji strzeleckiej, płacąc normalny koszt i zachowując PA na podstawowy atak. Nie dostaje darmowego zasobu. Wybór wartościowego skilla specjalnego ma pierwszeństwo przed tym ruchem; blokada zysku/taunt/stacjonarna jednostka wyłączają regeneracyjne przestawienie. Niedobór droższego skilla nie zostaje zablokowany samą dostępnością mniej przydatnego tańszego. Planowanie współbieżne kooperacyjnie w istniejącym budżecie, do8 sprawdzeń krawędzi; brak nowych skanów/Update per-frame. Symulacja potwierdza ruch, odbudowę impetu i użycie skilla z odzyskanego zasobu.

Kompatybilność: zachowano stare wspólne klasy many w katalogu. Stare jawne referencje encounterów/saves są mapowane wyłącznie na nowy wariant zasobu tej samej rodziny przeciwników; stare Mana nie staje się darmową pełną furią/impetem, przy migracji przyjmowany jest zatwierdzony start. Aktualne zapisy zachowują stan zasobu. Bez zmiany wersji/DTO save. Reguły i stany początkowe postaci gracza bez zmian.

Weryfikacja: kompilacja Core/Data/Runtime/Systems/Views/Editor/EditModeTests PASS; **421 odrębnych aktualnych przypadków z najnowszym wynikiem PASS**, w tym46 nowych. Ostatni szeroki przebieg420/421, jedyny błąd dotyczył nadmiernie szczegółowej asercji długiej symulacji wymagającej konkretnego buffa zamiast użytecznego skilla. Finalny EnemyResourceProfileTests46/46 PASS; reszta375 przypadków z szerokiego przebiegu PASS. Obejmuje80 AI,58 seamless,51 zwiadowcy, bossów, mobki, save/restore i reguły zasobów. Wcześniejsze stare asercje pełnej many zastąpiono zatwierdzonymi profilami; mechaniczne testy skilli jawnie fundowane. Test par wilków używa transient kopii definicji z samą parą wilków i bez bossa zamiast rozszerzonego mieszanego prototypu; nadal wymaga dokładnie2 wolnych, patrolujących spawnów dla10 seedów. Test pudła deterministycznie używa gwarantowanego uniku (wysoki Dodge pozostawiał minimalną szansę trafienia). Generator/content nie został zmieniony dla testów.

Dowody: `../LocalHandoffs/enemy-resources-pass-20261015/verification.json`, `latest-test-results.json`, `admission-regression-tests.xml`, `resource-final-tests.xml`, `enemy-resource-profiles.json`, `asset-audit.json`, `scoped.diff`. Self-review/diff-check PASS. Manual smoke/FPS NOT RUN. Importy/testy w otwartym Editorze bez ręcznego Refresh, drugiego procesu lub otwierania/zapisywania scen. Menu Godmist → Verification → Enemy resource profiles (keep Editor open). Bez commit/push. Pozostałe2 tematy: PT-06 i PT-15.


## PT-02 — Magiczna Tarcza Alison: siła i czas — 2026-10-15 — READY_FOR_SMOKE

Użytkownik zatwierdził: **250% średniego ataku,3 własne tury odbiorcy, ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas; inne źródła pozostają**. Produkcyjny `SD_MageShield` ma mnożnik2,5 i duration3. Koszt40 many/90% AP, celowanie i pozostałe właściwości są zachowane. Autorski setup zaktualizowany; istniejący asset zmieniła w otwartym Editorze wyłącznie wąska metoda `VS09MageSetup.ApplyShieldPlaytestBalance`, bez ponownego generowania całej klasy.

`ShieldLayer.RemainingTurns` przechowuje czas przy konkretnej warstwie. Zera zachowują bezterminowość innych źródeł. `StatusEffectSystem.ProcessTurnEnd` odejmuje turę tylko odbiorcy; po trzecim zakończeniu usuwa pozostałą wartość tarczy. Bieżąca tura rzucającego na siebie liczy się zgodnie ze standardowym odliczaniem buffów na końcu tury. Pochłonięta warstwa znika od razu. Recast usuwa tylko źródło `skill.mage.shield`, również przy innym rzucającym. Stun/freeze/paraliż nie zatrzymują odliczania własnych zakończeń tur. Feedback wygaśnięcia pokazuje faktyczną pozostałą wartość; oznaczenie self-source zapobiega fałszywej pozie trafienia.

Poza walką odlicza istniejący tick eksploracji co6s (miasto i loch), bez nowego Update/timera. Jednostki walczące są pomijane przez ownera eksploracji, a powieleni occupants z tą samą postacią liczeni raz. Wejście do kolejnej walki nie odnawia czasu. Tooltip podaje250%, czas i regułę zastępowania.

Save: dodano opcjonalne pole `ShieldLayerSaveData.RemainingTurns` (default0), bez podnoszenia wersji. Aktualny zapis zachowuje pozostały czas/wartość. Stare zapisy z bezterminowymi/zdublowanymi Magicznymi Tarczami dostają3 tury i jedną ostatnią warstwę; zachowuje się jej pozostałą wartość, bez ponownego wyliczania siły bez danych pierwotnego rzucającego. Pozostałe stare tarcze nie zmieniają czasu. Ujemny czas jest odrzucany.

Weryfikacja: kompilacja Core/Data/Runtime/Systems/Views/Editor/EditModeTests PASS; **167/167 testów PASS**, w tym18 nowych. Zestawy: MageShieldLifecycle15, ShieldPool6, MageContent15, StatusEffectSystem28, CombatRecovery35, SaveSnapshot5, Restore19, CombatStatusPresentation31, CombatTooltipGlossary8, ExplorationResource5. Pierwszy166/167: test celowo uszkodzonego zapisu nie oczekiwał komunikatu odrzucenia; dodano precyzyjny LogAssert.Expect, retest15/15, finalny167/167. Self-review i audyt assetu PASS. Unity dopisało tylko domyślne pola obecnego schematu oprócz zatwierdzonych zmian. Dowody: `../LocalHandoffs/mage-shield-pass-20261015/verification.json`, `final-live-tests.xml`, `asset-audit.json`, `scoped.diff`.

Manual smoke NOT RUN. Sprawdzić siłę, odliczanie3 tur odbiorcy, recast bez kumulacji i wygasanie w eksploracji. Importy/testy automatycznie w otwartym Editorze, bez ręcznego Ctrl+R/drugiego procesu; menu Godmist → Verification → Mage shield playtest pass (keep Editor open). Bez artu/PPU/scen/prefabów/commit/push. Pozostałe3 tematy: PT-04, PT-06, PT-15.


## PT-03 — Impet za odzyskanie sztyletów — 2026-10-15 — READY_FOR_SMOKE

Wybrano **+8 impetu** w zakresie5–10 pozostawionym przez użytkownika do uznania asystenta. Nagroda jest płaska, raz za faktyczny odzysk: wejście na jedno pole zbiera własne sztylety z tego pola i daje jedną nagrodę bez mnożenia przez liczbę wpisów/stosów; kolejne pole to osobny odzysk. Odzysk skillem daje jedną nagrodę po opłaceniu zwykłych kosztów. Wyjęcie wbitego sztyletu również daje jedną nagrodę po jego usunięciu z celu. Reset zapasu na początku walki, pusta/powtórna próba, obcy sztylet i pełny zapas nie dają impetu.

Wspólny helper `ScoutCombatSystem.RewardKnifeRecovery` używa istniejącego `CombatRecoverySystem`: tylko zasób Momentum, limit zasobu, aktywne blokady zysku, floating feedback faktycznej ilości (np.+3 przy197/200, brak przy200/200). Powyżej100 impetu bonus nadal wynosi8, ponieważ jest płaskim zwrotem, nie generacją z ruchu. Dotychczasowe koszty, talenty zniżek i zasady generacji z ruchu są zachowane. Nie istnieje osobna bezkosztowa akcja upuszczania: sztylety powstają z rzeczywiście zużytego zapasu podczas akcji, a wyjęcie wymaga wcześniejszego wbicia. Przy okazji naprawiono utratę reszty stosu podczas podnoszenia z ziemi przy częściowo pełnym zapasie.

Opis nagrody znajduje się we wspólnych tooltipach skilli odzysku i przy liczniku sztyletów. Brak zmian artu/PPU/skali świata, serialized assetów/scen/prefabów i schematu save; brak nowych skanów per-frame. Import/testy w otwartym Editorze przez istniejący runner, bez ręcznego Ctrl+R i bez drugiego procesu.

Weryfikacja: kompilacja PASS; **203 odrębne przypadki z najnowszym wynikiem PASS**, w tym15 nowych. Pierwszy przebieg201/203 ujawnił dwie stare asercje kosztu Haka bez nowej nagrody; zmieniono wyłącznie oczekiwany wynik udanego odzysku. Retest całego ScoutContentTests51/51 PASS; nieudane próby nadal nie zmieniają stanu. Pozostałe fixture: ScoutMechanics29, ScoutKnifeHud13, ScoutKnifeLanding17, CombatRecoverySystem35, SeamlessDungeonCombat58 PASS. Self-review/diff-check PASS. Dowody: `../LocalHandoffs/knife-recovery-reward-pass-20261015/verification.json`, `latest-test-results.json`, `first-live-tests.xml`, `scout-content-retest.xml`.

Manual smoke NOT RUN. Sprawdzić +8 z ziemi, przez Hak i wyjęcie; limit zasobu; resztę stosu przy prawie pełnym zapasie. Menu: Godmist → Verification → Knife recovery reward (keep Editor open). Pozostałe4 tematy: PT-02, PT-04, PT-06, PT-15. Bez commit/push.


## PT-05 — Spójna nazwa odporności na kontrolę — 2026-10-15 — READY_FOR_SMOKE

Przyjęto istniejącą nazwę z karty postaci: „Odporność na kontrolę”, skrót w kolumnie odporności „Kontrola”. Wspólny formatter tooltipów skilli, statusów i biżuterii używa teraz „kontrolę”; galduryty pokazują „Szansa na kontrolę” / „Odporność na kontrolę”, a inspektor konsoli „Kontrola”. Glosariusz wyjaśnia dokładny zakres `StatusEffectIds.UsesSuppressionResistance`: ogłuszenie, zamrożenie, sen, paraliż, unieruchomienie, wychłodzenie i prowokacja. Nie rozszerzono kategorii o zwykłe spowolnienie.

Poprawiono autorski opis Miażdżącego Ciosu; istniejący asset aktualizuje w otwartym Editorze wąska, idempotentna metoda `VS09PaladinSetup.RefreshControlResistanceWording`, która dotyka tylko `TooltipDescription`. Pełna konfiguracja skilla nie jest uruchamiana ponownie. Unity przy zapisie dopisało domyślne pola aktualnego schematu; audyt potwierdził, że wszystkie istniejące wartości poza opisem są identyczne. Enumy, ID, pola save, wartości odporności i zasady walki pozostają bez zmian. Import/testy obsługuje istniejący request runner; użytkownik nie musi używać Ctrl+R ani zamykać Unity.

Weryfikacja: kompilacja Runtime/Systems/Views/Editor/EditModeTests PASS; testy glosariusza8, treści przedmiotów7 i paladyna17 — **32/32 PASS** w otwartym Editorze. Self-review PASS. Dowody i smoke: `../LocalHandoffs/control-resistance-naming-pass-20261015/`. Manual smoke NOT RUN. Bez commit/push.


## PT-14 — Lądowanie rzeczywistego sztyletu — READY_FOR_SMOKE (2026-10-15)

Fizyczny sztylet, który faktycznie trafił do CombatSession.MapObjects, otrzymuje jednorazowe, NonSerialized LandingOrigin/LandingFromHit. FinishKnifeAction bierze informację o trafieniu z rzeczywistego wyniku efektu obrażeń (Succeeded), nie z cache rolli follow-upów. Upuszczenia po śmierci również mają punkt wyjścia. Zachowanie sztyletu talentem, darmowy cios w zwarciu i pełne wbicie w cel nie tworzą dodatkowej kopii na ziemi. Nie zmieniono wyboru miejsca dropu, liczby/ceny sztyletów, celności, obrażeń, AP, zasobów ani zasad odzyskiwania.

CombatUnitViewSpawner używa tego samego SpriteRenderera i istniejącej grafiki catacombs-ground-dagger-v2. Przez0,42s sztylet pokonuje krótki łuk, obraca się i zatrzymuje dokładnie na istniejącym polu pickup'u z GroundDaggerPresentationOffset. Trafienie zaczyna na wysokości kontaktu0,65; pudło nisko nad ziemią0,12, bez sugerowania trafienia w postać. Po końcu rotacja jest identity, skala nadal0,45 i grafika/32PPU bez zmian. To animacja fizycznego przedmiotu, odrębna od statycznych VFX skilla; nie przedłuża ani nie blokuje tury.

Odzyskanie w trakcie lotu nadal działa według istniejącej mechaniki i usuwa widok oraz stan animacji. Outline z hovera/HUD zaczyna działać po lądowaniu. Hint jest konsumowany raz; istniejące przedmioty, ponowne utworzenie widoku i odkrycie mgły nie odtwarzają starego lotu. Ukryty początek/cel pomija animację, a utrata widoczności w trakcie przerywa ją i przywraca docelową pozycję. ClearViews/usunięcie map object sprzątają dictionary. Bez nowych rendererów ruchu, colliderów, scen scans, bitmap lub komponentu Update na każdy sztylet.

Regresja ujawniła cache grafiki zwracający wpis bez sprawdzenia żywego sprite'a/tekstury i bez ponownego potwierdzenia profilu Point. Load w istniejącym DungeonCatacombsObjectVisuals odtwarza teraz martwy wpis oraz utrzymuje Point/Clamp także na cache hit. Dwa osobne testy wymuszają zniszczony sprite i Bilinear; brak zastępczej lub rozmytej grafiki. Nie edytowano PNG, metadanych importu, PPU ani skali postaci0,7.

Weryfikacja:153/153 testów PASS w otwartym Editorze, w tym17 nowych. Rzeczywisty skill: trafienie/pudło i koszt; zachowane/darmowe/wbite sztylety, śmierć celu, metadata NonSerialized, łuk/rotacja/dokładny koniec, odzysk w trakcie, hover/reuse, stare przedmioty, odtworzenie cache i widoku, mgła oraz regresja treści/mechanik/HUD zwiadowcy, odzyskiwania i walk łączonych. Render pięciu etapów ze źródłową grafiką sprawdzony wizualnie: PASS. Kompilacja offline Runtime/Systems/Views/EditModeTests i końcowa kompilacja live PASS. Self-review PASS. Manualny smoke/FPS pełnej gry NOT RUN.

Pierwszy offline preflight poprawił wymagany floorIndex fixture przed importem. Pierwszy live:10/15; poprawiono parametry opcjonalnego Bind w reflection, założenie testu o leniwym Awake outline'u i źródło faktycznego trafienia. Następnie15/15. Szerszy run150/151 ujawnił cache; po poprawce i dwóch nowych przypadkach153/153 PASS. Wszystkie importy i retesty wykonano automatycznie przez istniejący request-based Assets Refresh; nie proszono użytkownika o Ctrl+R ani zamknięcie Unity.

Dowody: `../LocalHandoffs/knife-landing-pass-20261015/verification.json`, `final-live-tests.xml` i `knife-landing-stages.png`. Bez zmian serialized scen/prefabów/assetów, schematu save, balansu lub ręcznego wiring. Bez commit/push. Menu testów: Godmist → Verification → Knife landing (keep Editor open). PT-01, PT-07, PT-08, PT-09, PT-10, PT-11, PT-12, PT-13 i PT-14 READY_FOR_SMOKE; pozostałe6 tematów: PT-02–PT-06 oraz PT-15.


## PT-08 — Feedback XP, awansu i punktów rozwoju — READY_FOR_SMOKE (2026-10-15)

CharacterProgressionSystem publikuje po rzeczywistym dodatnim AddExperience nieutrwalany event CharacterInstance.ExperienceGained(amount, levels), po zastosowaniu poziomu, punktów i przyrostu statystyk. Wspólny punkt obejmuje walki, questy i konsolę; bez zmiany obliczeń nagród lub progów. Zero/ujemne XP, maksymalny poziom, wczytanie postaci i samo ustawienie poziomu nie tworzą fałszywych komunikatów. Event ma NonSerialized; bez zmiany schematu save.

Rail zbiera nagrody jednej postaci z tej samej klatki w jeden floating text „+N XP”, z „Zdobyto poziom!” przy awansie i ×N przy wielu poziomach. Korzysta z istniejącego CombatFloatingFeedbackView, fontu Godmist i jawnie zarządzanego materiału outline. Około2,5s czasu unscaled, bez przechwytywania kliknięć. Kotwica nad aktualnie widoczną postacią śledzi kamerę; brak dostępnego/widocznego obiektu świata daje bezpieczny fallback obok portretu. Rebind/disable odpinają subskrypcje i czyszczą pending; przebudowa railu najpierw emituje już przyznane XP, żeby go nie zgubić.

Portret ma w prawym górnym rogu czytelne, ciemne Godmist „+N” z faktycznej AvailableSkillPoints. Zero ukrywa wskaźnik. Klik otwiera bezpośrednio istniejący rozwój właściwej postaci, bez dodatkowej karty ekwipunku; jedno okno jest ponownie używane. Zachowana blokada rozwoju uczestnika walki i walidacja przy kliknięciu, także przy starym stanie przycisku. Hover wskaźnika nie konkuruje z rich tooltipem portretu. W karcie przycisk pokazuje „Rozwój (+N)”; otwarta karta aktualizuje również XP/poziom po nagrodzie i punkty po inwestycji.

Weryfikacja w istniejącym Editorze:118/118 testów PASS, w tym16 nowych. Nagrody, wielokrotny awans, brak duplikacji nagród zwycięstwa, zmiana postaci, disable, przebudowa railu, klik/ponowne użycie rozwoju i blokada walki, stan karty, lifetime/materiał i NonSerialized event. Regresja istniejącej progresji, questów, nagród walk, drzewek, railu/kontekstowego menu i kart. Render GPU sprawdzony: wskaźnik czytelny, floating text nie zasłania nazwy/portretu. Self-review PASS; pełny manual smoke w mieście/lesie/walce NOT RUN.

Pierwszy import ujawnił błędną nazwę metody integracji (Show zamiast Spawn); naprawiono, a offline preflight Runtime/Systems/Views/EditModeTests przeszedł bez nadpisywania DLL Editora. Pierwsze testy:13/15 PASS; poprawiono brak CombatId w fixture oraz istniejące Clear drzewka używające Destroy w EditMode (runtime dalej Destroy, EditMode DestroyImmediate). Następnie15/15 i końcowe118/118 PASS. Ostatni dodatkowy przypadek chroni XP przed utratą przy przebudowie railu.

Do istniejącego LiveVfxVerification dodano jawny request `../LocalHandoffs/unity-refresh.request`: poza Play Mode i kompilacją wywołuje Assets Refresh przed odkrywaniem testów. Nie zmienia preferencji, scen ani assetów serialized i nie uruchamia drugiego procesu. Pierwsze wczytanie helpera wymagało użytkownika; kolejne importy i retesty wykonano automatycznie, potwierdzone unity-refresh.state.txt. Nowy run-tests.ps1 przygotowuje obie prośby. Bez commit/push, nowych grafik, importu/PPU/skali świata lub ręcznego wiring.

Dowody: `../LocalHandoffs/progression-feedback-pass-20261015/verification.json`, `final-live-tests.xml`, `progression-feedback.png`. PT-01, PT-07, PT-08, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe7 tematów otwarte: PT-02–PT-06 oraz PT-14–PT-15.


## PT-07 — Jeden licznik sztyletów Jerzego w HUD — READY_FOR_SMOKE (2026-10-15)

CombatActiveUnitView tworzy jeden licznik „Sztylety / dostępne/limit” pod portretem aktywnego sojuszniczego zwiadowcy, obok PA. Czyta bieżące AvailableKnives/MaxKnives, także limit z talentów, bez wpisania5 na stałe. Zero ma czerwony kolor. Font/materiał z nazwy jednostki i istniejąca ramka Godmist; root80×48, pos8/-112, ignoreLayout, bez poszerzania panelu. Ukryty przy innej klasie, przeciwniku lub nieznanej jednostce. CombatSkillBarView usuwa powtarzany zapas pod skillami; KnifeCost, tooltip kosztu, blokady i mechanika odzyskiwania bez zmian.

Hover licznika podświetla własne sztylety leżące na ziemi w aktywnej sesji. CombatUnitViewSpawner składa niezależny hover licznika i kursora świata; sprzątnięcie jednego nie usuwa drugiego. Zachowane IsPositionVisible, forceRenderingOff, enabled i aktywność renderera; bez ujawniania fog, cudzych lub wbitych sztyletów. Zmiana aktywnej jednostki/sesji, wyjście kursorem, disable/rebind i usunięcie sztyletu sprzątają stan. Reuse istniejących outline'ów, bez scene scans ani nowych bitmap/odczytu tekstur per-frame. Sprzątanie rendererów sztyletów używa istniejącego DestroyPresentationObject, także w EditMode.

Weryfikacja:135 odrębnych testów PASS według najnowszego wyniku każdego przypadku (122 wcześniejszych +13 nowych). Nowe testy: limit/aktualizacja, klasy/widoczność, hover ownership, ukryte renderery, suma dwóch hoverów, reuse, zmiana aktywnej jednostki, usunięcie sztyletu, brak powielania zapasu i zachowanie blokad skilli. Izolowany render GPU właściwego HUD sprawdzony wizualnie: PASS. Początkowe failure nowych testów wynikały z błędnego pola sterownika „driver” zamiast „focused” i nieaktywnego domyślnie prefabu HUD; poprawiono konfigurację testów, bez zmiany produkcyjnych reguł. Self-review PASS. Manualny smoke/FPS pełnej gry NOT RUN.

Dowody: `../LocalHandoffs/scout-knife-hud-pass-20261015/verification.json`, `latest-test-results.json`, źródłowe XML i `scout-knife-hud.png`. Bez zmian balansu/PT-03, PNG/importu/PPU, skali świata, save lub serialized assetów/scen/prefabów. Brak ręcznego wiring; istniejąca scena DungeonSandbox ma referencję spawner→HUD. Assets → Refresh poza Play Mode; Unity pozostaje otwarte. Menu testów: Godmist → Verification → Scout knife HUD (keep Editor open). Bez commit/push. PT-01, PT-07, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe8 tematów otwarte.

## PT-13 — Aktualne modele postaci w kartach — READY_FOR_SMOKE (2026-10-15)

Potwierdzona przyczyna: CharacterInfoWindowView odczytywała VisualSprite, czyli stare frontowe ilustracje VS22. Karta wyposażenia pobiera teraz pierwszą klatkę frontowego idle z CharacterAnimationResolver, zgodnie z aktualnym modelem świata. Bez pakietu animacji zachowany fallback: pierwsza dostępna klatka IdleAnimationFrames, następnie VisualSprite. Brak grafiki czyści poprzedni obraz. Podgląd pozostaje statyczny i preserveAspect, z teksturami Point.

Zachowano układ i rozmiar karty, twarze PortraitSprite/UiPortraitSprite oraz przywróconą skalę świata 0,7. Bez zmian PNG, PPU, importu, transformów, serialized scen/prefabów, gameplayu lub save schema.

Weryfikacja w otwartym Editorze: 78/78 testów PASS (21 karta, 21 akcje przedmiotów, 18 camping/tooltipy, 17 prezentacja lasu, 1 zgodność źródła modelu). Testy porównują czterech bohaterów z rzeczywistym prefabricatem eksploracji po wejściu w idle, sprawdzają skalę/layout, fallback i czyszczenie. Cztery izolowane rendery GPU rzeczywistej karty sprawdzone wizualnie: PASS. Self-review PASS. Pełny manual smoke w grze NOT RUN.

Szerszy początkowy przebieg ujawnił niezależne, stare oczekiwanie audytu VS22 dla ikony skill.enemy.catacombs.dead_king_guard: oczekuje VS22 zamiast istniejącej ikony Króla v2. Assetów Króla ani tego audytu nie zmieniano; initial-live-tests.xml zachowuje failure. Wynik 78/78 dotyczy jawnego zakresu PT-13 i regresji UI, nie całego audytu grafiki.

Dowody: `../LocalHandoffs/character-card-model-pass-20261015/verification.json`, `final-live-tests.xml` i cztery pliki *-card.png. Assets → Refresh wykonane; bez ręcznego wiring i zamykania Unity. Testy można uruchomić przez Godmist → Verification → Character card models (keep Editor open), poza Play Mode. Bez commit/push. PT-01, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe 9 tematów otwarte.

## PT-11 — Korekta regresji skali postaci — READY_FOR_SMOKE (2026-10-15)

Użytkownik odrzucił powiększenie drużyny względem miasta/NPC. Cofnięto błędny kierunek wcześniejszego passa: postacie są punktem odniesienia, a ciała i narośle należy dopasować do nich. Nie zwiększać aktorów ani świata tylko po to, by uzyskać natywny piksel32PPU w world units.

ExplorationCharacterView nie kompensuje już skali rodzica odwrotnością lossyScale. PF_ExplorationCharacter zachowuje root0,7 i autorską skalę dziecka. CharacterView nie narzuca skali1 sprite'om32PPU: PF_BattleUnit zachowuje Body0,7, także dla narośli leczącej i trującej. Dzięki temu postacie nie rosną przy wejściu do walki. Dedykowane ciała32PPU mają skalę0,7, zgodną z żywymi aktorami. Zachowano wcześniejszą korektę samego rysunku wilka i narośli. Bez zmian PNG/PPU/importu, serialized prefabów/scen, rozmiarów colliderów żywych postaci, ruchu, statystyk lub save schema. NPC i otoczenie nie są powiększane.

Weryfikacja w otwartym Editorze:144/144 testów PASS. Rzeczywiste prefaby: czterech bohaterów, wilk, obie narośle; skala0,7 i brak narastania przy ponownym Bind/ConfigureVisual. Kolizje/root bez zmian. Regresja ciał, animacji/póz, downed indicators, awareness i druida. Początkową błędną ścieżkę testu wilka zastąpiono istniejącą stałą WolfPath. Testy odpalone na starym DLL nie były przyjęte jako wynik; przed końcowym dispatch potwierdzono nową kompilację. Powtarzanie testów ujawniło zniszczone sprite'y w cache stacji NPC: TryResolveNpcStation dostał ten sam guard żywego sprite'a/tekstury co istniejący TryLoadSprite, z dwoma testami odtwarzania. Nie zmienia to skali/grafik NPC.

Self-review PASS. Dowody: `../LocalHandoffs/actor-scale-restore-20261015/verification.json` i `final-live-tests.xml`. Nie trzeba zamykać Unity. Po Assets → Refresh uruchomić świeży Play Mode: już istniejące obiekty runtime mogły zachować powiększone Transformy sprzed poprawki. Manualny smoke porównania miasta/NPC/lasu/walki NOT RUN. Bez commit/push. To korekta PT-11; liczba pozostałych tematów backlogu bez zmian.

## PT-12 — Skrzynie lochów: outline i LPM — READY_FOR_SMOKE (2026-10-15)

DungeonChestView obsługuje LPM na widocznej grafice skrzyni. Klik w zasięgu otwiera istniejące okno łupu, a dalszy zleca podejście istniejącej ExplorationPartyController. Przejmuje primary click przed zwykłym ruchem (-90 jak wejście do lochu), respektuje UI/console/text input oraz priorytet interakcji jednostek. E pozostaje alternatywą. Loot nie jest automatycznie zbierany; klucze/lockpicki są zużywane dopiero przez dotychczasowy DungeonChestInteractionService przy rzeczywistym otwarciu. Brak klucza lub dostępu pokazuje istniejący krótki floating feedback, bez nowego tooltipu.

Podejście wybiera legalny cel w zasięgu z HasDirectAccess i widoczną parterową powierzchnią, używając istniejącego pathfindingu/NavMesh. Brak otwierania przez ściany, z innych pięter, w walce, martwym liderem albo na nieaktywnym/ukrytym obiekcie. Brak zbindowanej mapy odrzuca rozkaz bez NullReferenceException. CancelMovement, nowy rozkaz, zmiana lidera/piętra, keyboard override i invalidacja celu sprzątają pendingChest. Osiągnięcie końca trasy zachowuje cel wyłącznie do następnego interaction tick, potem otwiera raz i sprząta rozkaz. Akcja używa autorytatywnej Session kontrolera drużyny, tak jak reszta jego obsługi.

Hover używa istniejącego CombatUnitVisualStateView i shadera sylwetki. Osiem rendererów powstaje raz przy pierwszym hoverze, bez scene scans lub odczytu tekstury w runtime. Szerokość odpowiada jednemu natywnemu pikselowi sprite'a; stabilny cyjan zamiast pulsowania do czerni. Zachowane kolor sprite'a, fog/elevation, lokalny sort i aktualna klatka. Wyłączenie/ukrycie czyści outline; zapamiętana skrzynia poza bieżącą widocznością nie jest interaktywna. Nowy setter pulse speed pozostawia dotychczasowe ustawienia pozostałych unit/NPC bez zmian.

Test odmowy ujawnił istniejące automatyczne tworzenie materiału przez TextMeshPro outlineColor w EditMode. DungeonLootFloatingText otrzymał jawnie zarządzany materiał fontu z OUTLINE_ON, bez mutacji współdzielonego assetu. Ponowna Initialize i OnDestroy zwalniają poprzedni materiał. Regresja dotychczasowych ciał/loot feedback PASS.

Weryfikacja: 161/161 aktualnych unikatowych testów PASS;23 nowe przypadki skrzyń, interakcje świata, pathfinder/NavMesh, placement, kontenery lasu, zbieranie łupu i ciała przeciwników. Licznik bierze najnowszy pełny przebieg każdej fixture. Początkowe failure: typ Vector2/Vector3 w reflection teście, dwie skrzynie w tej samej komórce fixture, material warning TMP, niewywoływany automatycznie lifecycle testowy OnDestroy i brak TilemapRenderer w fixture pamięci mgły — rozwiązane, bez ignorowania logów. GPU render właściwego prefabu i self-review PASS; pełny manual smoke NOT RUN.

Dowody: `../LocalHandoffs/chest-primary-pass-20261015/verification.json`; podgląd: `../LocalHandoffs/chest-primary-pass-20261015/chest-hover.png`. Assets → Refresh poza Play Mode wystarczy; bez ręcznego wiring lub zamykania Editora. Testy live: Godmist → Verification → Chest primary pass. Bez zmian balansu/save schema albo commit/push. PT-01, PT-09, PT-10, PT-11 i PT-12 READY_FOR_SMOKE; pozostałe10 tematów otwarte.

## PT-10 — Dwuklik przedmiotów w karcie postaci — READY_FOR_SMOKE (2026-10-15)

W aktualnym UIW ekwipunku pojedynczy LPM wybiera przycisk bez zakładania/zużywania przedmiotu. Dwuklik zakłada sprzęt, zdejmuje go ze slotu, otwiera torbę, używa zgodnego z eksploracją consumable albo wywołuje istniejącą akcję obozu. Slot założonego sprzętu zachowuje inspekcję pojedynczym kliknięciem. PPM zachowuje menu; Submit/Enter na zaznaczonym przycisku wykonuje akcję domyślną. Usunięto natychmiastową mutację z inventory Button.onClick, żeby pierwszy klik dwukliku nie wykonywał operacji i nie przebudowywał listy przed drugim.

Dispatch należy do istniejącej CharacterInfoWindowView i używa dotychczasowych ownerów: EquipmentService, ConsumableService, LootBagService oraz CampAction. Sprawdzane są własność konkretnej instancji, aktualny stan walki, aktywność widoku/okna/przycisku i interactable. Wymagania sprzętu, miejsce/masa, charges, combat-only bomby, powrót do miasta i warunki obozu nadal walidują istniejące serwisy. clickCount==2, a nie >=2, zapobiega drugiej aktywacji trzecim kliknięciem. Drag nie aktywuje przedmiotu. Stary wiersz/slot nie może użyć przeniesionego przedmiotu ani zdjąć nowego wyposażenia w jego miejsce.

Uzasadnione wyjątki zachowane: handel LPM = transakcja, schowek LPM = transfer, łup LPM = podniesienie; ich własne serie kliknięć, Shift/stack, quantity, PPM i drag pozostają bez zmian. Consumable wymagające walki/targetu nie są automatycznie używane na przypadkowej jednostce.

Weryfikacja: 223/223 aktualnych unikatowych testów PASS (221/221, następnie targeted akcje21/21 z dwoma dodatkowymi przypadkami aktywności).21 nowych testów na rzeczywistym prefabie karty i ExecuteEvents dla Button+view, z mutacjami inventory/charges/equipment. Regresja: CharacterCardPresentation, CampingTooltipLifecycle, DungeonCamp, EquipmentService, TradeService, ItemTooltipPresentation, CraftingUpgradingConsumablesAcceptance. Początkowy compile failure nowych testów wynikał z overloadu NUnit Does.Not.Contain; poprawiono na Has.No.Member. Self-review PASS; manual smoke NOT RUN.

Dowody: `../LocalHandoffs/item-default-action-pass-20261015/verification.json`. Bez nowych usług/timerów, zmian balansu/save schema lub ręcznej konfiguracji serialized assetów/scen/prefabów. Assets → Refresh wystarczy. Testy można też wywołać przez Godmist → Verification → Item default action pass poza Play Mode, bez zamykania Editora. Bez commit/push. PT-01, PT-09, PT-10 i PT-11 READY_FOR_SMOKE; pozostałe11 tematów otwarte.

## PT-09 — Camping kit i lifecycle tooltipów — READY_FOR_SMOKE (2026-10-15)

Usunięto tooltip sukcesu obozu z obu wejść: menu przedmiotu w karcie postaci i menu podłogi. Dotychczas komunikat był przypisany do całego okna/mapy, więc nie otrzymywał zwykłego pointer-exit. Odmowa nadal ma widoczny powód: istniejący zamykany notice przedmiotu albo dismissible menu podłogi; warunki są ponownie sprawdzane przez istniejący owner obozu. Zużycie zestawu, leczenie, zasoby i autosave zachowują swoje reguły.

Źródła kart przedmiotów i slotów wyposażenia invalidują tooltip na OnDisable. Karta postaci sprząta własne źródła przy wyłączeniu lub Initialize/rebind. Wspólny guard tooltipu rozpoznaje także wyłączony Behaviour przy aktywnym GameObject; dotychczas patrzył tylko na activeInHierarchy. Wykorzystano istniejące InvalidateSource/HideForSourceWithin i lifecycle nested/pin; wyłączenie starego źródła nie zamyka niezwiązanej karty. Bez nowych usług, timerów, skanowania inventory per-frame lub ręcznego wiring.

Weryfikacja: 169/169 unikatowych testów PASS (pierwszy przebieg168/168, dodatkowy test rzeczywistego obozu z menu podłogi1/1).18 nowych przypadków oraz DungeonCamp, CharacterCardPresentation, ItemTooltipPresentation i RichUnitTooltipLifecycle. Rzeczywiste prefaby UI, zużycie klikniętego zestawu, sukces obu wejść, odmowa bez zużycia, pending/visible/pinned, disabled Behaviour, destroyed source, rebind/close i zachowanie niezwiązanej przypiętej karty. Self-review PASS. Manual smoke NOT RUN.

Dowody: `../LocalHandoffs/camping-tooltip-pass-20261015/verification.json`. Nie wymaga zmian serialized assetów/scen/prefabów ani migracji zapisu. Można zastosować przez zwykły Assets → Refresh; menu Godmist → Verification → Camping tooltip pass uruchamia testy w otwartym Editorze poza Play Mode. Bez commit/push. PT-01, PT-09 i PT-11 READY_FOR_SMOKE; pozostałe12 tematów otwarte.

## PT-11 — Las: czytelność, skala, granice i koszt oświetlenia — READY_FOR_SMOKE (2026-10-15)

Pass obejmuje całą kartę PT-11. Pełne sprite’y drużyny, wrogów, świeżych summonów i ciał otrzymują czytelny order ponad zasłaniającym drzewem/przeszkodą z istniejącego indeksu komórki. Usunięto runtime półprzezroczystej kopii przeciwnika, jej materiał, odczyty czterech tekstur koron i dodatkowy renderer. Bez wygaszania samych postaci, scenowych skanów ani nowych ownerów. Fog/elevation/invisibility nadal należą do dotychczasowych widoków; podniesiony sprite nie wymusza widoczności. Ciała zachowują ground lighting, ale jego przebieg nie resetuje już ich czytelnego orderu. Aktor ma lane ponad ciałem.

Najbliższe pnie pozostają na rzeczywistej granicy kolizji, bez wizualnego odsuwania całej krawędzi od enemy homes/Enta. Niski boundary scrub usunięty z generowanej granicy; krzaki zostają dekoracją wnętrza. Zachowane logiczne ściany, fizyczne collidery, mgła, podłoże pod lasem i reguły ruchu. Preview PA miało już oddzielny sortAtRoot overlay32000; rzeczywisty render GPU potwierdził numer nad przeszkodą mimo niskiej grupy rodzica, bez dokładania drugiego UI.

**Skorygowane po zgłoszeniu użytkownika:** wcześniejsze wymuszenie skali1/odwrotnej kompensacji było błędne i powiększało aktorów względem miasta. Przywrócono autorską skalę0,7 postaci w eksploracji i walce; do niej dopasowano ciała i narośle, bez powiększania NPC/otoczenia lub zmiany PPU. Aktualne dowody w sekcji korekty regresji skali powyżej. Zwłoki wilka zmniejszone w samym rysunku do długości odpowiadającej żywemu bokowi; narośle zmniejszone do maks20×22 widocznych pikseli i zakotwiczone korzeniami przy ziemi. Pozostaje64×64 canvas/32PPU/Point, bez doraźnego przestawiania PPU.3 kontrolowane edycje built-in image_gen ze shipped referencjami, źródło/native kontrolowane po każdej, stary pipeline BOX/alfa150/paleta/no dither/Aseprite.3 exact roundtrip,4 PNG kopie (w tym brakująca wspólna runtime kopia zwłok), stare źródła zachowane w backup. Prompty/podgląd: `../output/forest-playtest-pass-v1/`.

Potwierdzony koszt: ApplyGroundLighting aktualizował MPB całej dekoracji co0,2s niezależnie od kamery. Dodano culling po już przygotowanych granicach z istniejącym zapasem na ruch; poza nimi nie ma alokowania ani wgrywania MPB. Analogiczny filtr dla wall props. Cadence5Hz i cache masek bez zmian. Kontrolowany benchmark4096 props/64 w kadrze: mediana 18.40→1.86ms (89.9% mniej, 9.9× szybciej), rejestracje4096→64. To CPU tego przebiegu, nie FPS całej gry. Pełny runtime FPS/p95 i akceptacja wyglądu: NOT RUN, wymagany smoke.

429/429 aktualnych unikatowych testów PASS po targeted retest. Obejmują światło/cache/cadence, widoczność/render4 typów sprite’a, skalę/import/bounds, AP overlay, navigation/spawn/forced displacement, ciała oraz wszystkie dotychczasowe VFX. Pierwsze failure nowych testów: brak ścian w fixture, brak UpdateAllSortingGroups przed synchronicznym Camera.Render, niewyczyszczony Camera.targetTexture, błędna nazwa ConfigureVisual i użycie pełnego canvas bounds dla niekatalogowej narośli. Poprawione setupy bez ignorowania logów. Rzeczywisty test prefabów wykrył root0,7 — poprawiona wyłącznie prezentacja childa. Starszy test ghosta zastąpiony obecnym kontraktem pełnego sprite’a. ForestArtBindings zsynchronizowane z dziewięcioma już istniejącymi portretamiVS22 i trzema zmienionymi hashami artu, bez cofania zaimportowanych portretów do klatek idle.

Dowody: `../LocalHandoffs/forest-playtest-pass-20261015/verification.json`. Bez balansu/statów/AI, save schema, serialized scen/prefabów lub zmian zasad nawigacji. Bez commit/push. PT-01 i PT-11 READY_FOR_SMOKE; pozostałe13 tematów nadal otwarte.

## VFX — pass12: Przywódca Bandytów i Ent — READY_FOR_SMOKE (2026-10-15)

Domknięte11 współczesnych definicji: Rozkaz Wzmacniający, Bandycka Mina, Pułapka na Niedźwiedzie, Ogniowa Dysza, Ukryte Przebicie oraz Twarda Kora, Zamach Gałęzią, Spaczona Pięść, Splugawione Korzenie, Zapłon, Piekielne Sanktuarium. Audyt mapowań71/71 współczesnych przeciwników oraz45 bazowych skilli klas. Nie jest to deklaracja pełnego pokrycia każdej gałęzi/talentu/proca ani19 innych definicji legacy/test.

7 nowych PNG64×64 /4 Aseprite /5 klipów. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji. Pierwszy ogień odrzucony za bloom, poprawiona wersja importowana. Premultiplied BOX, alfa150, wspólna paleta256/no dithering, baza trzech płomieni zarejestrowana niezależnie od ruchu iskier.7 exact RGBA/timing roundtrip;110 wcześniejszych klatek SHA bez zmian. Point/RGBA32/Clamp/no mipmaps/32PPU, bez ręcznych zmian serialized assets.

Statyczne wykonanie do wspólnego końca pozy2s; odrębne statusy animowane. Kora: jeden sygnał po compound buffie. Korzenie: tylko udany status. Gałąź/Pięść: neutralne drewniane kontakty po trafieniu, prawdziwy obszar i odrzut. Zapłon/Dysza: dokładna linia/stożek i rotacja, również puste pola, Burn osobno. Ukryte Przebicie: jedna ofiara, bez fikcyjnego obszaru na sąsiadach.

Rozkaz używa oryginalnej zaakceptowanej grafiki nad głową, dopiero po udanym wzmocnieniu; cast intent nie pokazuje sukcesu, compound effects nie dublują znaku. Ukryte miny/sidła bez nowej podpowiedzi przy stawianiu. Aktywacja/detonacja: jeden ActionId, dokładne3×3, kontakty wyłącznie na prawdziwych ofiarach. Zachowane istniejące MapObjectTriggered; driver nie renderuje drugiego centralnego wybuchu. Brak trwałego ognia po minie.

Sanktuarium zachowuje losowe Fields i bezpieczne refuges, warning→fire na następnej turze, trzy tury i once-per-victim-turn. Istniejący renderer markera fire odtwarza cztery niskie, ciepłe klatki; brak dodatkowych rendererów i per-frame skanów. Kontakt na rzeczywistym trafieniu, snapshot przed obrażeniami. Znaczniki nadal sprząta istniejący owner; nie dublowano pól, timerów ani gameplayu.

Weryfikacja: 392/392 aktualnych unikatowych testów PASS, bez nierozwiązanych failure; compile/import oraz focused VFX, boss gameplay, contextual hit, destructible/art/presentation regression. Dowody: `../LocalHandoffs/target-vfx-pass12-20261015/verification.json`. Początkowy helper testu używał nieistniejącego pola baseAccuracy; poprawiono na SkillAccuracy. Starsze testy dopasowano do uzgodnionego pełnego deadline i outcome-gated Rozkazu, bez ignorowania logów lub zmiany zasad. Szczegóły kolejnych retestów zapisane w XML.

Podgląd/prompty: `../output/target-vfx-pass12-v1/review.html`. Import/menu live bez zamykania Editora i bez ręcznego wiring. Manual smoke/FPS NOT RUN. Self-review zakończony. Bez zmian balansu/save/loadout/scen/prefabów.14 tematów PT02–15 nadal otwarte. Bez commit/push.

## VFX — pass11: Król i Szkielet Zabójca — READY_FOR_SMOKE (2026-10-15)

11 pozostałych definicji Katakumb: Królewskie Cięcie, Rozkaz Króla, Straż, Runy Zemsty, Widmowa Fala, Widmowe Ostrze, Krzyk Cienia, Cień Destrukcji oraz Nieugięta Obrona, Wykonać Zadanie i Młot Zniszczenia.13 nowych PNG64×64 /4 Aseprite /8 klipów. Built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji. Pierwsze hammer-charge odrzucone za bloom; import korzysta wyłącznie z poprawionej wersji.13 exact RGBA/timing roundtrip;97 wcześniejszych klatek SHA bez zmian. Point/RGBA32/no mipmaps/32PPU; wspólna rejestracja głównych klastrów czterech klatek, bez przesuwania całości przez ruch drobin.

Wykonanie pozostaje statyczne do wspólnego końca pozy. Rozkaz, Obrona i Przygotowanie Młota mają osobne autorskie4-klatkowe pulsy1,2s co4,8s przy opacity0,7, w istniejącym limicie dwóch motywów poniżej hard-control/DoT. Rozkaz odczytuje aktywne źródła oznaczenia, znika po przeniesieniu/wygaśnięciu. Obrona/Hammer odczytują istniejące liczniki i znikają po przerwaniu/zakończeniu/śmierci; brak nowych gameplayowych timerów.

Cięcie/Fala korzystają z rzeczywistego kształtu i rotacji, także pustych pól. Kontakt fizyczny respektuje hit/krew/tarczę; Fala i Ostrze mają widmowy kontakt, ich debuffy osobny wynik. Krzyk pokazuje jeden kontakt dźwięku na odbiorcę niezależnie od obu rolli statusów. Ostrze oznacza wyłącznie rzeczywiste lądowanie po teleportacji. Straż pokazuje wszystkich naprawdę przywołanych rycerzy, również dwóch dodanych przez ownera.

Opóźniony Młot i Cień tworzą osobną korelację prezentacji przy faktycznym rozstrzygnięciu; driver współdzieli2s deadline z pozą uderzenia. Młot zachowuje istniejącą pozę destruction-impact, Cień używa istniejącej pozy castowania przy detonacji. Każdy faktyczny Hit ma opóźnienie prezentacji według indeksu; pudło/prewencja bez kontaktu. Pozycja kontaktu snapshotowana przed obrażeniami, więc reakcja/ruch nie przenosi błysku na nowe pole. Uderzenie ziemi osobno, tylko w rozstrzygniętym obszarze. Duży Cień ma dokładny obrys wyłącznie dark fields; safe fields bez wybuchu. Istniejące markery zapowiedzi i ich cleanup pozostają ownerem pola. Przerwany Młot bez późnych kontaktów, bez zmiany warunku10% HP lub countdown.

Runy zachowują istniejące assety, bramki HP, turę aktywacji, dobór ofiary i rozbrajanie. Przy utworzeniu SkillUsed z ActionId poprzedza pięć błysków w faktycznych pozycjach. Rozbrojenie ma krótki błysk pola, czasowe trafienie kontakt na rzeczywistej ofierze tylko przy dodatniej wartości ataku. Bez drugiej stałej obwódki. Testy używają istniejącego zgodnego ownera run z realnym skillem jawnie załadowanym jak w wcześniejszych testach; nie zmieniano dostępności/loadoutu bossa.

361 aktualnych unikalnych testów PASS: VFX, kontekstowe trafienia, integracja póz, legacy runy, Król v2 i Zabójca. Pierwszy przebieg355/360: stary test run zbierał ostatni dowolny feedback zamiast SkillUsed; trzy nowe testy timingu nie ustawiały PF_BattleUnit; setup Task omijał prawdziwe przejście fazy i kolejkę tur. Poprawiono setup/assertion kontraktu, bez ignorowania logów lub zmian gameplayu. Retest VFX/CatacombsBoss283/284; diagnostyka ostatniego przypadku wykazała phase=True/gate=True, ale active=False: dummy ModifyActionValue wywołuje ReorderParticipants, a test nie inicjalizował timeline. Dodano normalne InitializeTurnOrder; końcowy targeted1/1 PASS. Self-review dodał snapshot pre-damage i regression test ruchu celu, sprawdzone w pełnym targeted VFX. Wcześniej naprawiono błąd kompilacji testu próbującego przypisać read-only ActiveSkillLoadout, używając istniejącego TryInitialize.

Dowody: `../LocalHandoffs/target-vfx-pass11-20261015/verification.json`. Podgląd/prompty: `../output/target-vfx-pass11-v1/review.html`. Import/menu live bez zamykania Editora lub ręcznego wiring; batch przy nieaktywnym Editorze. Manual smoke/FPS NOT RUN. Bez scen/prefabów/serialized Unity assets, save schema, balansu i nowych usług/skanów per-frame. PT-02–PT-15 nadal otwarte, bez commit/push.

Audyt mapowań: 60/71 współczesnych definicji przeciwników; pozostaje11 skilli Przywódcy i Enta.45 bazowych skilli klas bez zmian. To licznik mapowań, nie pełny manual/runtime audyt każdej gałęzi, talentu/proca lub legacy; osobne wizualne rozszerzenia końcowej kontry Obrony nie były dodawane.

## VFX — pass10: natura, trucizna, flara i pułapka — READY_FOR_SMOKE (2026-10-15)

13 kolejnych skilli: Ancient Whip, Vomit, Tree Dark/Binding Roots i Plague Growth, Druid Nature Wrath, dwa przywołania narośli i oba pulsy, Poacher Poison Bag/Bear Trap oraz Shooter Flare.5 nowych PNG64×64 /5 Aseprite /8 rejestracji klipów. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji, Point/RGBA32/no mipmaps/32PPU.5 exact RGBA/timing roundtrip,92 wcześniejsze klatki SHA bez zmian.

Obszar wykonania osobno od rzeczywistej aplikacji efektu. Oliwkowy rozbryzg nie tworzy trwałej kałuży; istniejące Poison/Immobilize/Bleed animują się tylko po faktycznym statusie. Worek zachowuje rzeczywisty obszar2×2, także puste pola, z wyłączeniem niechodliwej podłogi. Duży zasięg plagi pokazuje dokładny obrys, również przy otworach, w istniejącym limicie128 rendererów. Plaga przedłuża istniejące zatrucie; świeży cel nie dostaje wymyślonego poison. Gniew Natury zachowuje wymaganie własnych żywych narośli. Kontakt Ciemnych Korzeni osobno od opcjonalnego zatrucia, Zniewalające Korzenie tylko po rzeczywistym unieruchomieniu, niezależnie od debuffa obrony.

Narośl: błysk wyłącznie po udanym przywołaniu, w pozycji faktycznie zwróconego summona; brak udawanego natychmiastowego leczenia. Puls leczniczy pokazuje obszar i sygnały faktycznie odzyskanego HP. Flara zachowuje sumę odbiorców dwóch trybów All (wrogowie/sojusznicy), bez wymyślonego koła. Jeden błysk nad mierzoną głową każdego widocznego odbiorcy, niezależnie od rolli debuffa/stun; oczyszczenie tylko po rzeczywistym usunięciu stun. Ukryte cele nie dostają beaconów.

Bicz pokazuje pył tylko w wykonanych polach odrzutu i neutralny kontakt po rzeczywistym ruchu. Zatrzymanie przed przeszkodą nie pokazuje żądanego końca ani fałszywej krwi. Pułapka nie dodaje VFX przy stawianiu. Aktualny asset ma WorldSprite, ale nie ma TriggerSprite ani flagi HiddenUntilTriggered; zachowano jego obecne zasady widoczności. Po prawdziwej aktywacji publikowany jest istniejący MapObjectTriggered, a brakującą klatkę aktywacji uzupełnia runtime klip trap-snap. Statyczne szczęki0,5s, bez pozy castowania. Inne triggery nadal0,18s. Bez ręcznego wiring lub zmian serialized assetu.

VFX castowania nadal jednoklatkowe, do wspólnego deadline pozy; statusy osobno animowane. Zachowane mgła, niewidzialność i budżety. Brak nowych usług/skanów per-frame, zmian balansu, movement/status/damage rules lub schematu save. PT-02–PT-15 nadal otwarte.

297 aktualnych unikalnych testów PASS, obejmujących fixture VFX, kontekstowe trafienia, Katakumby, przeszkody, druida i kłusownika. Pierwszy przebieg292/294: test niechodliwej podłogi błędnie używał niejawnej pełnej siatki, a test pułapki zakładał nieobecne pola prezentacji w realnym assetcie. Poprawiono setup siatki oraz rzeczywisty brak aktywacyjnego VFX, dodając test obu istniejących wariantów widoczności. Dodano też weryfikację dużej plagi/otworów/limitu i nowego importu; poprawiono błąd kompilacji negowanego NUnit Contains dla GridPosition. Targeted retest zakończony PASS. Dowody: `../LocalHandoffs/target-vfx-pass10-20261015/verification.json`. Manual smoke/FPS NOT RUN. Import/menu live bez zamykania Editora; batch wykonano przy nieaktywnym Editorze. Podgląd/prompty: `../output/target-vfx-pass10-v1/review.html`.

Audyt mapowań: 49/71 współczesnych definicji przeciwników, pozostaje22 skilli bossów (Król, Szkielet Zabójca, Przywódca i Ent). To licznik mapowań, nie pełny manual/runtime audyt wszystkich gałęzi, talentów, proców i legacy.45 bazowych skilli klas bez zmian. Bez commit/push.

## VFX — pass9: wsparcie i materializacja przeciwników — READY_FOR_SMOKE (2026-10-15)

11 kolejnych skilli: Battle Fervor, Bone Fervor, Bone Guardian, Blood Scent, Quiver of Envy, Life Drain, Dematerialize, Materialize, Archer Hawkeye, Bandit Ambush i Wolf Lick Wounds.3 nowe PNG64×64 /3 Aseprite /5 statycznych rejestracji klipów oraz reuse Rally/Tonic/DeathWish/Heal/Smoke. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po KAŻDEJ generacji, Point/RGBA32/no mipmaps/32PPU.3 exact RGBA/timing roundtrip,89 wcześniejszych klatek SHA bez zmian.

Wieloskładnikowe buffy: jeden sygnał na faktycznego odbiorcę. Bone Guardian: odbiorca i obrońca tylko po udanym założeniu więzi; self-target nie udaje ochrony. Nie dodano nowych stałych więzi ani osobnego VFX proca przekierowania obrażeń. Lizanie Ran i leczenie sojuszników przez Life Drain wymagają rzeczywistego odzyskania HP. Pełne HP/pełna absorpcja nie udają leczenia. Life Drain zachowuje rzeczywisty kontakt i oddzielne leczenie, ten sam presentation ActionId i delay trafienia.

Dematerialize/Ambush: jednorazowy ślad w ostatniej widocznej pozycji. Ślad nie śledzi niewidzialnej postaci; ponowne znikanie już ukrytego aktora nie daje beacona. Materialize zachowuje oryginalną kolejność efektów (leczenie15% maksymalnego HP, potem usunięcie niewidzialności). Buforowany jest wyłącznie sygnał faktycznego leczenia, publikowany po rozstrzygnięciu skilla i odsłonięciu celu. Widmowy kontakt wymaga rzeczywistej zmiany widoczności. Skill nadal wymaga niewidzialnego castera. Bez zmian warunków skilli lub gameplayu.

Jednoklatkowe VFX wykonania do wspólnego końca pozy castowania; istniejące statusy nadal animują się osobno. Zachowane fog/invisibility gates i limit128. Brak nowych skanów per-frame, usług, serialized Unity state, schematu save lub balansu.

257 unikalnych testów PASS po targeted retest całej fixture VFX. Pierwszy przebieg255/257 ujawnił brak sygnału leczenia Materialize: błędnie zakładany typ HealMissingHealthPercent zamiast rzeczywistego HealPercent. Drugi failure dotyczył błędnego testu castowania Materialize przez już widocznego castera; istniejący asset celowo wymaga invisibility. Naprawiono hook i test zgodnie z assetem, bez zmiany tego wymagania. Dowody: `../LocalHandoffs/target-vfx-pass9-20261015/verification.json`. Manual smoke/FPS NOT RUN. Import/menu live wspierają otwarty Editor, bez zamykania i ręcznego wiring; obecny przebieg batch uruchomiono przy nieaktywnym Editorze. Podgląd/prompty: `../output/target-vfx-pass9-v1/review.html`.

Audyt mapowań: 36/71 współczesnych skilli przeciwników; dalsze35 definicji obejmuje naturę/truciznę, flarę/ujawnienie i mechaniki bossów. To licznik mapowań, nie pełny runtime/manual audyt każdej gałęzi efektu, talentu/proca lub legacy. Bazowe45 skilli klas bez zmian. PT-02–PT-15 nadal otwarte. Bez commit/push.

## VFX — pass8: kontakty i doskoki przeciwników — READY_FOR_SMOKE (2026-10-15)

15 współczesnych skilli Katakumb/Lasu i legacy `skill.enemy.bone_sword`: maczuga/miecz/cięcie krzyżowe, rozszarpanie/ugryzienia, trzy noże, trzy doskoki, Dziki Ryk, Widmowe Cięcie i Starożytna Inkantacja.3 nowe PNG64×64 /3 Aseprite /4 statyczne klipy, plus reuse istniejących kontaktów/pyłu. Stara metoda image_gen ze shipped referencją, kontrola źródła/native po każdej generacji, Point/RGBA32/no mipmaps/32PPU.3 exact RGBA/timing roundtrip,86 wcześniejszych klatek SHA bez zmian.

Kontakty po faktycznym trafieniu, krew wyłącznie po utracie HP i na cielesnym celu. Istniejący selektor materiału rozpoznaje teraz również wysłane szkielety, upiora i drzewo o ID bez słów skeleton/ghost. Zmiana tylko wyglądu kontaktu, bez odporności na krwawienie. Cross Cut zachowuje każde trafienie i kolejność; kontrola/DoT pozostają osobnymi statusami.

Doskoki pokazują wyłącznie rzeczywiste zatrzymanie po rozstrzygnięciu blokad; success bez ruchu nie tworzy lądowania, częściowy ruch nie udaje dotarcia do celu. Brak beacona niewidzialnego właściciela. Kontakt Widmowego Doskoku osobno od lądowania. Dziki Ryk: dokładny obrys zasięgu, także puste pola graniczne, i jeden sygnał na rzeczywistego widocznego odbiorcę niezależnie od obu rolli statusu. Pełne wypełnienie przekroczyło128 sprite’ów przy zaokrąglanym Radial6; obrys zachowuje zasięg w istniejącym budżecie. Cele mają priorytet nad podłożem. Owner chwilowych efektów odzyskuje wygasłe sloty przy osiągnięciu limitu, jeśli kolejny cast zaczął się w Update przed poprzednim cleanup w LateUpdate. Brak nowego skanu na każdej klatce lub zwiększania limitu.

237 unikalnych testów PASS po naprawie limitu obszaru i targeted retest VFX201/201. Weryfikacja batch przy zamkniętym Editorze; import/menu live wspierają otwarty Editor bez zamykania i bez wiring. Brak zmian balansu, movement/damage rules, scen, serialized Unity state i save schema. Manual smoke/FPS NOT RUN. Podgląd/prompty: `../output/target-vfx-pass8-v1/review.html`; dowody: `../LocalHandoffs/target-vfx-pass8-20261015/verification.json`.

Audyt mapowań: 25/71 współczesnych skilli przeciwników ma mapowanie VFX; dalsze46 definicje obejmują wsparcie, naturę/truciznę, niewidzialność/ujawnienie i specjalne mechaniki bossów. To nie jest pełny runtime/manual audyt każdej gałęzi efektu, talentu lub proc. Bazowe45 skilli klas pozostają z poprzednich passów. PT-02–PT-15 nadal otwarte. Bez commit/push.

## VFX — pass7: Tonik i przełączniki — READY_FOR_SMOKE (2026-10-15)

4 skille: Pragnienie Śmierci, Ofiara, Wycieńczające Zaklęcia i Tonik Pobudzający.13 nowych PNG64×64 /4 Aseprite /7 klipów. Trzy czteroklatkowe subtelne pulsy z przerwami (1,2s aktywne, okres6s albo4,8s), trzy statyczne aliasy najczytelniejszej klatki oraz osobny Tonik. Wykonanie nadal jednoklatkowe do wspólnego końca pozy castowania.

Pętle czytają rzeczywisty stan przełącznika, a Ofiara również faktyczny buff. Współdzielą dwumotywowy budżet, po kontroli/DoT/oznaczeniach. Wygaśnięcie po wyłączeniu, nieopłaconym podtrzymaniu, śmierci, ukryciu i unbindzie. Tonik pokazuje początkowy boost i dopiero rzeczywisty opóźniony spadek szybkości; recast/cancel bez fałszywego sygnału. Wycieńczenie celu tylko po sukcesie rzeczywistego modifiera. Kontekst trafienia niesie czas prezentacji i parent presentation id; gameplay action ids i reguły są bez zmian. Brak zmian balansu/save schema/scen.

Stara metoda image_gen ze shipped referencją → premultiplied BOX/alfa150/wspólna paleta/no dithering → Aseprite, kontrola każdej generacji. Pierwsza za cienka wersja Pragnienia odrzucona. Point/RGBA32/no mipmaps/32PPU,13 exact RGBA/timing roundtrip,73 poprzednie klatki SHA bez zmian. 280 unikalnych testów PASS po targeted retest jednego scenariusza testowego z brakującym zasobem. Weryfikacja batch przy zamkniętym Editorze; import/menu live wspierają otwarty Editor bez zamykania lub wiring. Manual smoke NOT RUN. Podgląd/prompty/metoda: `../output/target-vfx-pass7-v1/review.html`; wyniki: `../LocalHandoffs/target-vfx-pass7-20261015/verification.json`.

Domknięto mapowanie bazowych45 skilli klasowych z planu (coverage.json zawiera audyt mapowań). Nie oznacza to pełnego runtime/manual pokrycia wszystkich talentów/proców ani wszystkich135 definicji. Kolejny zakres VFX: pozostałe rodziny skilli przeciwników oraz legacy/proc. PT-02–PT-15 nadal otwarte. Bez commit/push.

## VFX — pass6: pełny czas castowania, oznaczenia i przemiany zasobów — READY_FOR_SMOKE (2026-10-15)

PT-01 zaimplementowane do smoke: VFX wykonania skilla, warstwy przy pozie i target-head utrzymują się do wspólnego końca pozy. ActionId wiąże cast z celami; pasywne kopie dzielą prezentację, zachowując osobne gameplay action ids. Opóźnione trafienia zachowują kolejność, identyczne statyczne kontakty nie mnożą rendererów. Tick leczenia/pola i aktywacja pułapek mają osobny krótki lifecycle.

6 skilli: Rozkaz Unicestwienia, Słaby Punkt, Przejęcie Mocy, Przemiana Witalności, Rytuał Odnowy i Święta Transfuzja.10 nowych PNG64×64 /4 Aseprite /6 klipów; dwa czteroklatkowe pulsy oznaczeń oraz statyczne kontakty. Oznaczenia czytają OrderTurns/WeakPointTurns, zachowują dwumotywowy budżet i priorytet kontroli/DoT. Ukrycie po śmierci, niewidzialności, wygaśnięciu i unbindzie. Koszt HP oraz odzysk zasobu/leczenie mają osobne sygnały tylko po faktycznej zmianie. Rytuał sygnalizuje rzeczywiste ticki leczenia. Brak zmian balansu i save schema.

Stara metoda image_gen ze shipped referencją → premultiplied BOX/alfa150/wspólna paleta/no dithering → Aseprite; kontrola po każdej generacji. Point/RGBA32/no mipmaps/32PPU. 208 unikalnych testów PASS (wyniki pełnego przebiegu i targeted retest),10 exact RGBA/timing roundtrip,63 poprzednie klatki SHA bez zmian. Weryfikacja batch przy zamkniętym Editorze; import i menu live wspierają otwarty Editor bez zamykania i bez wiring. Manual smoke NOT RUN. Podgląd/metoda: `../output/target-vfx-pass6-v1/review.html`; dowody: `../LocalHandoffs/target-vfx-pass6-20261015/verification.json`.

Aktualizacja pass7: death_wish, tonic, offering, tiring zostały zaimplementowane i czekają na smoke; dalszy zakres przeciwników/proców/legacy z planu135 pozostaje otwarty. Pozostałe14 tematów playtestu PT-02–PT-15 nie były implementowane. Bez commit/push.

## VFX — pass5: pola, aury i przemieszczanie — READY_FOR_SMOKE (2026-10-15)

8 skilli: Młyniec/Okrzyk, Fala Przyciągająca/Pole Osądu, Ognista Aura, Hak/Zamiana/Krok w Cień.10 nowych klatek64×64 /4 Aseprite /5 klipów. Dwie animowane pętle rzeczywistego pola/aury; wykonanie skilli nadal jednoklatkowe. Stara metoda ze shipped referencjami i kontrolą po każdej generacji; pierwszy zbyt cienki okrzyk odrzucony. Point/RGBA32/no mipmaps/32PPU, bez ręcznego wiring.

Obszary używają gameplay geometrii i widoczności, również pustych pól; lokalny refresh co0,2s lub po ruchu/zasięgu, reuse i budżet128 persistent rendererów osobno od128 chwilowych. Cleanup po końcu sesji/wygaśnięciu/wyłączeniu. Kontakt pola tylko przy ticku, Młyniec per realne trafienie z przerwami przy pudłach. Hak zapisuje wykonane kroki zamiast żądanej trasy; odzyskanie sztyletu bez fałszywych obrażeń. Zamiana ma dwa punkty po sukcesie, Shadow tylko dym startu bez beacona niewidzialnego właściciela. Brak zmian reguł/saved state.

Import i weryfikacja w istniejącym otwartym Unity, bez drugiego procesu lub wymuszania Play Mode.147 unikalnych testów PASS po diagnozie dwóch błędnych założeń testowych, finalny pełny przebieg147/147 i końcowe rejestracje5/5;53 wcześniejsze klatki SHA bez zmian. Testy coroutine i manual smoke NOT RUN. Podgląd/metoda: `../output/target-vfx-pass5-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass5-20261015/HANDOFF.md`. Pełny plan135 nadal wymaga dalszych passów. Bez commit/push.

## VFX — pass4: wsparcie i święta magia — READY_FOR_SMOKE (2026-10-15)

11 kolejnych skilli: tarcza/skupienie Maga; błogosławieństwo, regeneracja, blask, Wyrok, Miażdżący Cios i Oślepiające Światło Paladyna; ochrona/gotowość/Bandaż Wojownika.4 nowe PNG64×64 /4 jednoklatkowe Aseprite,6 statycznych klipów; stara metoda z kontrolą źródła/native po każdej generacji. Point/RGBA32/no mipmaps/32PPU; bez ręcznego wiring.

Efekty wsparcia po sukcesie; Bandaż deduplikuje leczenie/oczyszczenie, brak zmian nie daje błysku. Regeneracja sygnalizuje nałożenie i faktyczny odzysk HP, także po opuszczeniu walki przez healera. Zwykłe Heal zachowuje poprzednie zachowanie. Ochronny Blask oznacza unik, nie tarczę HP. Kontakty ataków wymagają trafienia, oślepienie sukcesu debuffu; oba zachowują kolejność rzeczywistych celów łańcucha. Statusy i wyjątki Freeze/Paralysis bez zmian.

163/163 testów PASS,0 failure/skipped, Unity EXIT0;49 wcześniejszych klatek SHA bez zmian. Podgląd/prompty/metoda: `../output/target-vfx-pass4-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass4-20261015/HANDOFF.md`. Manual smoke NOT RUN. Pełny plan135 nadal wymaga dalszych passów. Bez commit/push.

## VFX — pass 3: energia i ataki dystansowe — READY_FOR_SMOKE (2026-10-15)

14 kolejnych skilli maga/zwiadowcy oraz dystansowych przeciwników Lasu/Katakumb.6 nowych PNG64×64 /6 jednoklatkowych Aseprite,7 statycznych klipów. Źródła i native sprawdzone po każdej generacji; shipped referencje, BOX/alfa150/shared256/no dither. Point/RGBA32/no mipmaps/32PPU przez istniejącego importera, bez ręcznego wiring.

Dokładne podłoże z geometrii gameplayu (trzy promienie/linia/prostokąt/obszar wokół wybranego pola), również puste pola. Kontakt per rzeczywiste trafienie, tarcze/kości zamiast krwi; seria i Rykoszet mają osobne statyczne okna. Dym sygnalizuje sukces osłabienia nad głową. Mgła, niewidzialność i cleanup zachowane; statyczne freeze/paralysis i dotychczasowe pętle statusów pozostają bez zmian.

126 unikalnych testów PASS po retestach,0 nierozwiązanych failure, Unity EXIT0;43 wcześniejsze klatki SHA bez zmian. Podgląd: `../output/target-vfx-pass3-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass3-20261015/HANDOFF.md`. Manual smoke NOT RUN. Pełny plan135 definicji nadal wymaga kolejnych passów. Bez commit/push.

## VFX — pass 2: statusy i obuch — READY_FOR_SMOKE (2026-10-15)

- Ogłuszenie, zatrucie i unieruchomienie mają animowane pętle. Freeze/paraliż mają statyczny mróz/kamień, delikatną warstwę koloru sylwetki i nieruchomą pozę; zdjęcie statusu wznawia animację. Obsługa obu ownerów widoku, lifecycle/ukrycie/śmierć/cleanup, maksymalnie2 motywy plus opcjonalny kolor hard statusu. Kontakt obuchowy dla Sierpowego/Kopnięcia i crushing/bone_crush Zabójcy; Unieruchomienie Wojownika korzysta z kontaktu cięcia.
-15 PNG64×64 /6 Aseprite;6 generacji starą metodą ze sprawdzeniem native po każdej. Dokładne RGBA/timing roundtripy, Point/RGBA32/32PPU/no mipmaps.1752 pomiary sylwetek dla kotew nad głową i nakładek.28 wcześniejszych klatek zachowane.86 unikalnych testów PASS, Unity EXIT0, bez ręcznego wiring.
- Podgląd/prompty: `../output/target-vfx-pass2-v1/review.html`; dowody/smoke: `../LocalHandoffs/target-vfx-pass2-20261015/HANDOFF.md`. Manual smoke NOT RUN. Pozostałe skille/statusy to kolejne passy. Bez commit/push.

## VFX — statyczne skille, animowane statusy — READY_FOR_SMOKE (2026-10-15)

- Korekta decyzji prezentacyjnej: efekty wykonania skilla to pojedyncze, krótko utrzymywane freeze’y; statusy (stun/freeze/bleed/burn itd.) pozostają animowane przez czas aktywności. Pierwszy pass ma5 statycznych klatek leczenia/cięcia/odprysków/ognia/Wstrząsów oraz2 dotychczasowe pętle Burn/Bleed. Czasy efektów i reguły gameplayowe zachowane.
- Wybrane zatwierdzone klatki bez zmian pikseli;5 jednoklatkowych Aseprite z dokładnym roundtripem.33/33 testów PASS, Unity EXIT0; źródłowe28 klatek i konfiguracja Burn/Bleed bez zmian. Bez ręcznego wiring. Podgląd: `../output/target-vfx-freezes-v1/review.html`; dowody/smoke: `../LocalHandoffs/target-vfx-freezes-20261015/HANDOFF.md`. Manual smoke NOT RUN, bez commit/push. Ta reguła zastępuje animowane chwilowe VFX z poprzedniej wersji passa.

## VFX celów i statusów — pierwszy pass — READY_FOR_SMOKE (2026-10-15)

- Wdrożone leczenie paladyna (pojedyncze i grupowe), kontakt cięć Wojownika/Przywódcy, pojedynczy wybuch Kuli Ognia, rzeczywiste pola Wstrząsów Szkieleta Zabójcy i zapętlone Burn/Bleed. Kontakt uwzględnia trafienie, tarczę i materiał celu; chwilowy efekt zachowuje pozycję przed odepchnięciem. Statusy respektują lifecycle i ukrycie obu rodzajów widoku.
- 7 animacji / 28 klatek64×64 / 7 Aseprite. Wbudowany image_gen i stara metoda Godmist, 8 generacji z poprawioną czytelnością Wstrząsów; dokładne RGBA/timing roundtripy. Import Resources Point/RGBA32/32PPU/no mipmaps, bez ręcznego wiring.
- 78 unikalnych testów PASS po poprawkach i retestach; 28 kopii/metas oraz 1684 wcześniejsze pliki grafiki SHA zweryfikowane. Manual smoke NOT RUN. Podgląd/prompty: `../output/target-vfx-pass1-v1`; dowody i smoke: `../LocalHandoffs/target-vfx-pass1-20261015/HANDOFF.md`. Pozostałe skille z listy135 pozostają kolejnymi passami. Bez commit/push.

## VFX celów, obszarów i statusów — DESIGN_READY_FOR_REVIEW (2026-10-15)

- Pełna propozycja dla135definicji z aktualnego GameContentCatalog:116skilli klas/przeciwników i19pozostałych/starszych/testowych. Przy każdym opis kontaktu przy celu, rzeczywistego obszaru, ewentualnego podtrzymania, warunku uruchomienia i priorytetu. Osobno14motywów statusów. Lista z wyszukiwaniem i filtrami:`../output/target-vfx-plan-v1/review.html`; dane i źródła/SHA:`target-vfx-plan.json`.
- Rozdzielone obszar użycia, trafienie i skuteczne nałożenie statusu; krew zależy od materiału celu, triggery pułapek od aktywacji, niewidzialność bez beaconów. Aktualna Kula Ognia ma pojedynczy cel. Powtarzalne motywy współdzielą grafiki. Pierwsza grupa: leczenie paladyna, cięcia, ognisty kontakt, Wstrząsy i Burn/Bleed.
- To plan do przeglądu, bez generacji nowych grafik ani zmian runtime/assetów. Sprawdzono135unikalnychID, pełne pokrycie, źródła bez zmian, UTF8 i JS galerii.

## Przywódca Bandytów — import całego zestawu — READY_FOR_SMOKE (2026-10-15)

- Unity API zaimportowało i podpięło271 tekstur byte-exact: obie fazy,128 klatek ruchu,72 pozy skilli/reakcji,29 osobnychVFX,2 ciała,6klatek przejścia,2portrety128×128,6ikon skilli i2pasywy32×32 oraz wymagane kopieSprite. Nowe katalogi istniejących resolverów,80wpisów póz/cue,48zmierzonych rejestracji. Point/RGBA32/no mipmaps,32PPU/scale1 dla świata.
- VFX mają osobne180ms bez skracania pozy; Rozkaz korzysta z kotwy nad głową celu. Ukryte pułapki pokazują footprint i burst tylko po triggerze/detonacji, z poprawnymi offsetami i cleanupem. Krótki conceal przy nowej niewidzialności bez stałego beacona. Fazy/progi/obstawa/łupy/statystyki bez zmian;6klatek przejścia nadal w następnej turze po śmierci obstawy.
- ImportEXIT0,271metas/copies i1684wcześniejsze pliki artu SHA bez zmian.91unikalnych testów PASS po poprawkach/targeted retestach;GPU3/3 PASS,0nierozwiązanych failure. Dowody i szczegółowy smoke:`../LocalHandoffs/bandit-leader-art-integration-20261015`. Manual encounter smoke NOT RUN; konsola `combat encounter.forest.bandit_leader`. Bez ręcznego wiring do zrobienia, bez commit/push.

## Przywódca Bandytów — portrety i ikony — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe2 spójne portrety128×128 oraz8 ikon32×32:6 skilli i2 pasywy,10 PNG/10 Aseprite. Kaptur podniesiony/opuszczony przy tej samej twarzy i stroju. Ikony według rzeczywistych shipped referencji drużyny/Katakumb; gest Rozkazu poprawiony osobną generacją na pięść.
- Stara metoda image_gen/BOX/shared256/no dither/Aseprite.3 generacje obejrzane w źródle i native;10/10 exact RGBA/timing roundtripów PASS,1601 wcześniejszych plików6 paczek SHA bez zmian. Galeria/METHOD/HANDOFF/prompty/provenance/ui-mapping:`../output/bandit-leader-ui-v1`.
- Bez importu ani zmian gameplayu. Wszystkie planowane etapy artystyczne Przywódcy są przygotowane do oceny. Kolejny etap: całościowy import zestawu, podpięcie obu faz i smoke animacji/skilli/VFX/ciała/portretów/statusów w Unity.

## Przywódca Bandytów — przejście faz i ciała — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe przejście S:6 rzeczywistych klatek po200ms,1,2s; opuszczenie kaptura i dobycie szabli. Dokładne idle00 obu faz na endpointach; cztery klatki pośrednie wygenerowane wspólnie. Dwa osobne leżące ciała128×128,3 edytowalne Aseprite oraz atlas768×128. Skala~58px/32PPU/scale1.
- Stara metoda image_gen/BOX/alfa150/frozen96/Aseprite.3 generacje obejrzane w źródle i native;8/8 dokładnych roundtripów i timingu PASS, atlas/GIF PASS,1540 wcześniejszych plików SHA bez zmian. Galeria/METHOD/HANDOFF/prompty/provenance:`../output/bandit-leader-finish-v1`.
- Bez importu ani zmian gameplayu. Przy integracji klip w następnej turze po pokonaniu obstawy, zgodnie z istniejącym BanditLeaderBossSystem; facingS tylko prezentacyjny. Kolejny etap: portrety obu faz i ikony skilli/pasywów, potem import i smoke.

## Przywódca Bandytów — osobne VFX — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe29 osobnych efektów i2 grafiki obiektów,31 PNG/31 Aseprite: dysza/cięcie/pchnięcie ×8kierunków, rozkaz/zastawianie/zatrzaśnięcie/wybuch miny/krótki conceal. Nowa mina; istniejąca pułapka skopiowana byte-exact. Stara metoda image_gen/BOX/alfa150/shared256/Aseprite,10 generacji obejrzanych. PchnięcieS/N poprawione po złożeniu z końcem szabli.
-31/31 roundtripów PASS,9 dokładnych odbić,36 zmierzonych rejestracji +2 offsety footprintów,1203 wcześniejsze pliki SHA bez zmian. Galeria z przełącznikiemVFX, kompozyty/pomiary/GIF-y i prompty/provenance: `../output/bandit-leader-vfx-v1`.
- Efekty pojedyncze180ms, nie pełne sekwencje animowane. Bez importu/Resources/meta/gameplay; pułapki pozostają ukryte do triggera, conceal bez stałegobeaconu. Następny etap: przemiana/ciało, potem portrety/ikony, import i smoke.

## Przywódca Bandytów — pozy skilli/reakcji — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe72 pojedyncze kulminacje PNG128/72 Aseprite:9 gestów ×8kierunków (45 autorskich/27 dokładnych odbić). Faza I: pułapka/mina, rozkaz, trafienie i unik; faza II: cięcie, dysza, pchnięcie, trafienie i unik. Dotychczasowa metoda image_gen/BOX/alfa150/frozen96/Aseprite, body~58px/32PPU; większy canvas to margines, nie większy aktor. Kalibracja stojąca chroni skalę kucnięcia.
-18 generacji obejrzanych; poprawione ręce z dyszą, kierunek pchnięcia/wylotu, frontalnySE i średnica dyszy; odrzucone zabrudzenia alfa.72/72 roundtripów PASS,807 plików wcześniejszych paczek bez zmian. Galeria, porównania baza/idle/poza, prompty/provenance i HANDOFF: `../output/bandit-leader-poses-v1`.
- To utrzymywane pozy, nie pełne animacje ataków. Bez importu/Resources/meta/gameplay. Następny etap: osobne VFX do gotowych póz; dalej przemiana/ciało, portrety/ikony, import i smoke.

## Pozy ataku / światło przywołań / skala32PPU — READY_FOR_SMOKE (2026-10-15)

- Naprawiony cache CombatPoseResolver zwracający zniszczone natywne sprite'y; baseline2/2 FAIL → PASS. Nowe obiekty na ziemi i przywołane jednostki są poprawnie rejestrowane odpowiednio w ground/actor lighting, ze sprzątaniem i przenoszeniem rejestracji.
- Potwierdzone64×64/32PPU dla pułapki i obu narośli; usunięte runtime pomniejszenie0.7 dla artu32PPU (efektywne45.7 →32). Podłoga lasu ma shaderową siatkę32/world. Legacy zachowuje wcześniejszy profil. Bez zmian serialized/importu/grafik/mechaniki.
- 89 unikalnych testów PASS, w tym8 nowych; rzeczywisty skill→borrowed pose→refresh oraz render shaderów w dwóch jasnościach. Evidence: ../LocalHandoffs/combat-visual-lifecycle-20261015. Manualny smoke pozostaje; bez commitu/pushu.

## Las — regresja wyglądu i koszt renderowania — READY_FOR_SMOKE (2026-10-15)

- Obwódki dekoracji podlegają wspólnemu światłu zamiast świecić po jego obliczeniu. Opaque pixels pomijają dodatkowe próbki alfa, puste fragmenty sprite’ów/podkładu nie wykonują kosztownego oświetlenia/teksturowania nawierzchni.
- Małe ruchy kamery zachowują przygotowany obszar/mapowanie cieni między tickami; natychmiastowe odświeżenie poza zapasem zachowane. Test panowania: 10 ticków / 5 przygotowań obszaru zamiast 10; rzeczywista siatka contact shadows reuse przez cztery kolejne ticki.
- 99 unikalnych testów EditMode PASS, w tym render GPU jasnego/ciemnego wariantu; cztery nowe regresje odtworzone na starym kodzie. Evidence `../LocalHandoffs/forest-render-performance-20261015`. Brak pomiaru FPS runtime, manualny smoke pozostaje. Bez zmian serialized state/gameplayu i bez commitu/pushu.

## Przywódca Bandytów — idle/chód — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe128 PNG/32 klipy Aseprite (4 klatki):2 fazy ×8kierunków ×idle300ms/chód180ms. Stara metoda image_gen +native/referencje, BOX/alfa150/frozen96/Aseprite; wszystkie klatki pętli generowane wspólnie, bez podstawiania statycznej00. Poprawione powtarzane kroki i skok sylwetki; odrzucona osobna klatka o innym cieniowaniu.80 autorskich/48 dokładnych odbić.128/128 roundtripów PASS;163 pliki wcześniejszych paczek bez zmian. Paczka `../output/bandit-leader-motion-v1` zawiera galerię pause/step, GIF-y, prompty i dowody.
- Bez importu/Resources/meta/gameplay. Następny etap: pozy skilli/reakcji i osobne VFX; potem przemiana/ciało, portrety/ikony, import/smoke.

## Ręczna ucieczka z walki — READY_FOR_SMOKE (2026-10-15)

- Follow-up po smoke: naprawiona odziedziczona rotacja -90° ikony; faktyczne wyszarzenie niedostępnego symbolu i przygaszenie ramki/X, przywracane po odblokowaniu. Naturalne tooltipy: „Wrogowie wciąż cię widzą.” / „Wrogowie wciąż cię szukają.”. Native prefab authoring, 68/68 testów PASS i oba stany sprawdzone renderem/pikselami. Evidence: ../LocalHandoffs/manual-escape-polish-20261015. Gameplay bez zmian.

- Utrata kontaktu już nie usuwa automatycznie postaci z walki. `Ucieknij` / X wycofuje wyłącznie aktywną postać, bez AP i losowania; ponowne wykrycie natychmiast blokuje akcję. Zachowane zwolnienie widoku eksploracji, retencja/reentry, czyszczenie przywołań i zaplanowanych efektów oraz dotychczasowe rozstrzygnięcie i kary ucieczki.
- Trzeci przycisk korzysta z tego samego PF_UIW_CombatIconButton co Q/E. Wpięty przez PrefabUtility, LayoutElement rezerwuje 184px. Tooltip jest dostępny przy zablokowanym przycisku i aktualizuje krótki powód. Własna ikona 64×64: imagegen z referencjami RavenFantasy Q/E (brak historycznego promptu dla kupionych ikon), BOX/Median Cut96/alfa150/Point32PPU. Źródło, prompt i metoda w ../output/manual-escape-20261015.
- 242 unikalne testy PASS w końcowym stanie właściwych zakresów, w tym 13 nowych przypadków; ostatni retest UI/layoutu 68/68 PASS. Natywny render poprawiony po wykryciu szerokości dla dwóch przycisków. Evidence: ../LocalHandoffs/manual-escape-20261015. Manualny smoke pozostaje; bez commitu/pushu.

## Przywódca Bandytów — kierunki — ART_READY_FOR_REVIEW (2026-10-15)

- Gotowe16 statycznych sprite’ów/16 Aseprite:8 kierunków obu faz, po5 autorskich widoków i3 dokładne odbicia. Bazy S byte-exact, nowe SE/E/NE/N sprawdzone po każdej generacji/native. Stara metoda image_gen + shipped kamery, BOX/alfa150/zamrożona96/Aseprite;16/16 walidacji PASS. Poprawiono frontalny SE i stopę P2N. Paczka: `../output/bandit-leader-directions-v1` (galeria, obrót QA, prompty, manifest/provenance).
- Bez importu/Resources/meta/gameplay. Następny etap: idle/chód; później pozy skilli/VFX, przemiana/ciało, portrety/ikony i import/smoke.

## Przywódca Bandytów — sprite’y bazowe — ART_READY_FOR_REVIEW (2026-10-14)

- Gotowe dwie statyczne bazy S obu faz, PNG96 i Aseprite. Image_gen ze shipped mobkami/drużyną, stara metoda BOX/alfa150/wspólna96/Aseprite, kontrola źródła i native po każdej generacji. Ciało58px/32PPU, spójne buty i wyposażenie; fazaII opuszcza kaptur i dobywa szablę. Walidacja2/2 PASS. Paczka: `../output/bandit-leader-base-v1`.
- Runtime nadal placeholder; ten etap nie obejmuje animacji/importu. Pozostają kierunki, idle/chód, sześć skilli i osobne VFX, przemiana/ciało, portrety, ikony i import/smoke. Gameplay bez zmian.

## Kontekstowe zależności trafienia skilli — READY_FOR_SMOKE (2026-10-14)

- Przejrzano 139 assetów SkillData / 214 efekty, w tym klasy, Katakumby, Las, bossów i legacy/testowe definicje. Zmieniono dane siedmiu skilli, zachowując ID, indeksy i meta. Kontaktowe payloady śledzą trafienie właściwego efektu w ten sam cel; Wstrząsy/Zapłon i inne wybrane efekty obszaru pozostają niezależne. Kopnięcie/Bicz współdzielą rzut kontaktu, bez uzależniania debuffa od przesunięcia przy ścianie. Reguły zachowane przez konfiguratory contentu.
- Naprawiono pomijanie szans ModifyVar/ModifyResistance i podwójne losowanie szansy obniżenia celności z Oślepiającego Światła. Tooltipy pokazują faktyczne trafienie, szansę i konkretny indeks zależności; Rozwój Plagi ma opis przedłużania statusu.
- 380 różnych testów PASS, w tym 15 nowych przypadków zależności/szans/UI. Jeden test poza zakresem (ForestWolfTests.TenSeedsPlacePairsOnFreePatrollingAnchors) FAIL także w osobnym powtórzeniu: brak drugiej pozycji patrolowej wilków. Generator/mapa niezmienione w tym passie. Evidence i pełny audit: LocalHandoffs/skill-hit-dependencies-20261014. Manualny smoke pozostaje; bez commitu/pushu.

## Content pass 1b — Forest boundary prototype (READY_FOR_SMOKE, 2026-10-11)

- Organic-boundary follow-up: real single-cell fringe pockets in the shared floor map, no cross-room bridging or filled enclosed islands. Thin 0.06-unit rounded physical edges and forest NavMesh margin 0.02; nearest trunks grounded at those edges. Decorative lit/fogged ground extends under five tree layers below playable floor. Final 67/67 PASS (`out/forest-fringe-final.xml`), including real native routes into pockets, physical approach/deep-forest blocking, footprints and save-layout regression. Fresh generation required; existing saves keep their geometry. Visual smoke pending.

- Alignment follow-up: tree art shifted down half a cell to match the blocked ground strip; sorting still follows ground anchors. Forest tests 13/13 PASS in `out/forest-tree-alignment.xml`; visual check pending.

- Eleven outdoor footprints and the Forest-only layout/profile/catalog are authored; production offers remain disabled pending enemies/loot.
- Boundary vegetation extends five non-walkable cells into the void with deterministic jitter, size/mirror variation and brightness fading towards black. Existing navigation/collision/LoS retained.
- Fixed occupancy refresh restoring Catacombs wall renderers; disabled automatic wall torches for vegetation maps; removed bright rectangular difficult-terrain tint patches from Forest presentation.
- Verification: `out/forest-depth-tests.xml`: 53 passed, zero failed, two GPU-only tests skipped under Null graphics. Includes forest generation, boundary-depth/cache regression, footprint and lighting-cadence tests. Visual smoke pending. Interior props, crown fading, final forest art, enemies/boss/loot remain open.

Ten plik jest naszym roboczym, tekstowym Kanbanem i punktem odniesienia do synchronizacji pracy.

Założenia:

- jedna karta = jeden commit; otwartych pakietów nie przedstawiamy jako ukończonej całej karty,
- nie tworzymy na razie osobnego `Iceboxa`,
- kiedy uznam, że jakaś karta powinna zmienić kolumnę, dam Ci o tym znać,
- przy okazji będę też aktualizował ten plik, żeby nasz „wewnętrzny” Kanban był zgodny z tym, co masz na boardzie.

Aktualne kolumny:

- `Backlog`
- `In Progress`
- `Done`

Priorytety:

- `[P1]` - ważne teraz
- `[P2]` - ważne później
- `[P3]` - fajnie mieć, ale nie na pierwszy etap

## Labelki

- `Core / Architecture`
- `Combat`
- `World / Dungeon / Exploration`
- `Items / Inventory / Economy`
- `NPC / Quest / Dialogue`
- `UI / UX`
- `Tools / Editor / Debug`
- `Content / Data`
- `Design / Systems`
- `Bug`

## Backlog

### Rejestr decyzji balansu

- Ustalenia rozmowy o wyposażeniu, ulepszaniu, rzadkości, reforgowaniu i wcześniejszym content passie: [BALANCE_DECISIONS_PENDING.md](BALANCE_DECISIONS_PENDING.md). Zapis 2026-09-21; dokument rozdziela zatwierdzone reguły, propozycje do kalibracji, zmiany kodu i otwarte decyzje. Powiązanie z VS-18 / VS-01, obecnie In Progress. Status wdrożonych pakietów i pozostałe kryteria zapisano przy kartach; sam wpis decyzji nie oznacza ukończenia.

### Milestone: Vertical Slice v1 — pierwsza kompletna pętla gry

Zatwierdzony kierunek i podział kart: 2026-09-16. Branch: `milestone/vertical-slice-v1`, oparty na merge Dungeon Exploration v3, PR #15, commit `c3f47057`.
Cel: grywalny produkt od rzeczywistego menu przez przygotowanie drużyny, wyprawy, bossów i nagrody po rozwój i kolejne wyprawy. Nie wymaga uruchamiania sandboxa przez gracza.

Zakres zatwierdzony w VS-01 (2026-09-16): 4 postacie dostępne od początku — Jerzy/Scout, Wladimir/Wojownik, Alison/Czarodziejka, Reimond/Paladyn. Po 2 drzewka, pierwsze 4 tiery; 5 startowych skilli na klasę wraz z autoatakiem. Content do około poziomu 20, bez zmiany limitu 50; materiały/wyposażenie 1–10 i 11–20. Tylko Katakumby, bez podestów/balkonów: 7 najważniejszych mobów, powtarzalny Duch Umarłego Króla w wariantach poziomu 10 i 20 oraz specjalny unikat. Nie każda wyprawa kończy się bossem. Pełny zapis decyzji i otwarte pozycje katalogu: `PROJECT_CONTEXT.md`, sekcja „Vertical Slice v1 — katalog VS-01”. Istniejący content wykorzystujemy, nie tworzymy ponownie.
Zasady zakresu: bez planowego polishu mechanik ani automatycznego dołączania Combat v5/Dungeon v4. Naprawiamy blockery i poważne problemy UX; inne ustalenia review trafiają do backlogu. Produkcyjne menu, czytelny przepływ i podstawowa prezentacja wybranego contentu należą do slice’a. Brak pełnego art passu, migracji całego RNG i zapisu w środku walki.
Kolejność według zależności: katalog → review → wymagany refaktor → content i start gry → kompletne Katakumby/pętla → ewentualny drugi loch → wspólne acceptance. Prezentację przygotowywać razem z contentem; VS-22 domyka jej kompletność. Każda karta ma własne testy, self-review i smoke; commit/push po osobnym poleceniu.






















- `[P2] [VS-19] Przygotować pokoje drugiego typu lochu — warunkowo` - `World / Dungeon / Exploration`, `Content / Data`
  - VS-01: drugi theme poza zatwierdzonym zakresem; VS-19–21 pozostają nieaktywne dla acceptance tego slice’a.
  - Tylko jeśli drugi theme zatwierdzony w VS-01, po działającej pętli Katakumb VS-18. Nazwa/theme do decyzji.
  - Odrębna pula pokojów i konfiguracja istniejącego generatora/contentu; reuse narzędzi oraz istniejących mechanik. Weryfikacja seedów i kompletności.

- `[P2] [VS-20] Przygotować przeciwników i encountery drugiego lochu — warunkowo` - `Combat`, `Content / Data`
  - Zależne od VS-19: odmienna zatwierdzona pula przeciwników, składy, umiejętności i nagrody. Różnice wynikają przede wszystkim z contentu, nie nowego frameworku AI.
  - Sprawdzić wymagania drużyny i progresję między dwoma theme.

- `[P2] [VS-21] Przygotować bossa drugiego lochu — warunkowo` - `Combat`, `Content / Data`
  - Zależne od VS-19/20: arena, umiejętności, trudność, loot i zakończenie wyprawy. Ten sam standard weryfikacji co boss Katakumb.

- `[P1] [VS-22] Domknąć podstawową prezentację contentu slice’a` - `UI / UX`, `Content / Data`
  - Zapewnić czytelne sprite’y, portrety, ikony i podstawowe audio wybranego contentu; usunąć testowe nazwy oraz brakujące assety ze ścieżki gracza. Zakres assetów i kierunek wizualny uzgodnić przed tworzeniem.
  - Kontrola spójności menu, Arungardu, drużyny, przedmiotów i gotowych lochów. To podstawowa prezentacja gry, bez pełnego art passu i bez planowego polishu mechanik.
  - 2026-10-10: podłączenie zatwierdzonych grafik bojowych (12 paczek, 573 pozy; osobne warstwy aktora/FX/pocisków), freeze 2 s i kamera z powrotem do wcześniejszego ujęcia, hit/dodge oraz NPC praca ↔ rozmowa. Kowal v3 korzysta z istniejącego kowadła, alchemiczka z osobnego kotła; foreground dłoni/narzędzi. Jedna jawna referencja kowadła zapisana przez Editor w Arungardzie; bez nowych Animatorów. Kompilacja PASS, 136/136 testów EditMode PASS (`output/combat-animation-integration/tests-acceptance.xml`). READY_FOR_SMOKE; manualny Play Mode NOT RUN.
  - 2026-10-10: korekta rozmycia sprite’ów — 981 tekstur CombatPoses miało w głównym projekcie Bilinear/kompresję. Automatyczna naprawa istniejących importów oraz reimport wszystkich 1835 tekstur postaci przez Unity: Point, RGBA32, bez kompresji/mipmap i skalowania NPOT. GUID-y i PNG zachowane; runtime dodatkowo wymusza Point dla póz. Kompilacja i 34/34 EditMode PASS (`output/character-import-sharpness/tests.xml`), audyt importu głównego projektu PASS. READY_FOR_SMOKE; świeży Play Mode, bez ręcznej konfiguracji.
  - 2026-10-10: portret ghula zastąpiony humanoidalną wersją zgodną z obecnym sprite’em; PNG 128×128, zachowany GUID i wszystkie referencje UI. Źródło setupu zmienione na nową grafikę, bez przywracania czworonożnego portretu przy ponownym imporcie. Built-in image_gen; prompt/manifest i preview w `output/asset-review/combat-portraits-ghoul-humanoid/`.
  - 2026-10-10: brakująca dedykowana ikona Okrzyku Wojennego wygenerowana w stylu wojownika i przypisana przez Unity do `skill.battle_cry`; Point/32 PPU/32×32/bez kompresji i mipmap. Katalog i setup uzupełnione do 71 ikon, pełny test 71 przypisań/profili PASS (1/1 EditMode, `output/asset-review/skills-complete/warrior-battle-cry/unity-tests.xml`); GUID skilla zachowany. READY_FOR_SMOKE, główny Play Mode zachowany.
  - 2026-10-10: cienkie paski HP pod portretami kolejki tur — 3 px wypełnienia, bez liczb; wspólny setup i grafiki `slider_track`/`fill_runtime` z dolnym panelem walki, miniatura w skali pionowej 0,5, akcent niskiego HP ≤25%. Aktualizacja po obrażeniach/leczeniu; nieznani przeciwnicy i obiekty przywołane bez paska. Po zgłoszeniu niewidoczności render potwierdził zasłanianie przez dolną ramkę: HUD 108 px, dolny inset 26 px rezerwuje osobny rząd. Portrety nadal 68×68; zapis przez Unity z zachowaniem GUID/fileID. Render 8/8 widocznych pasków i 55/55 EditMode PASS, w tym zgodność sprite'ów z dolnym panelem (`output/combat-timeline-bar-style`), READY_FOR_SMOKE; główny Play Mode zachowany.

- `[P1] [VS-23] Vertical Slice v1 — acceptance i końcowy review` - `Core / Architecture`, `UI / UX`, `Content / Data`
  - 2026-10-11: alchemiczka za kotłem podczas pracy i rozmowy — wyłączona błędna dodatkowa warstwa fartucha przykrywająca tylny rant/wnętrze. Zachowane dłonie nad rantem oraz warstwy kowala. Kod/testy bez bitmap i serialized state; 10/10 focused EditMode PASS, w tym render rantu w 12 klatkach (`output/alchemist-cauldron-layers`). READY_FOR_SMOKE; manualny smoke NOT RUN.
  - 2026-10-11: wspólne consumable z plecaków uczestników drużyny w walce, także powalonych; selektor łączy identyczne sztuki, AP/limit typu należą do wykonawcy, ładunek schodzi z przedmiotu nosiciela. Walidacja własności i tury przed wykonaniem, bez transferu przedmiotów i dostępu do stasha. Kod/testy bez serialized state; 76/76 EditMode PASS (`output/shared-combat-consumables`). READY_FOR_SMOKE; manualny smoke NOT RUN.
  - 2026-10-11: rzadkość na ramkach przedmiotów także w stashu, łupie, handlu/odkupie, wyposażeniu, usługach i ich wynikach, consumablach bojowych, tooltipach i porównaniu; wspólna paleta/styl inventory, niezależne zaznaczenie/niedostępność, reset pustych slotów i klonów tooltipów, maskowanie viewportu. Kod/testy bez serialized state; 145/145 EditMode PASS oraz render Unity sprawdzony (`output/item-rarity-borders`). READY_FOR_SMOKE; manualny smoke NOT RUN.
  - 2026-10-10: usunięty dodatkowy legacy tooltip nazwa/HP przy hover kolejki tur; `CombatHudView` utrzymuje stary panel wyłączony, wspólny bogaty tooltip i podświetlanie jednostki zachowane. Bez zmian serialized state. Kompilacja i 68/68 EditMode PASS (`output/timeline-legacy-tooltip-fix/tests.xml`); READY_FOR_SMOKE, manualny smoke NOT RUN.
  - 2026-10-10: prezentacja tarczy ujednolicona w tooltipach jednostek, party railu/wyborze wypraw, kolejce tur i karcie postaci; wspólny formatter HP/max (+tarcza), cienka warstwa w kolorze tarczy dolnego panelu nad HP. Bez zmian prefabów/wiring. 31/31 testów HP/tooltipów PASS, render sprawdzony (`output/shield-ui-consistency`), READY_FOR_SMOKE. Szerszy zestaw 79/80 PASS: niezwiązany błąd testu znaczników pól walki, szczegóły w handoffie.
  - 2026-10-11: podnoszenie powalonych w eksploracji przez LPM / PPM → Podnieś, z podejściem żywego członka drużyny, blokadą przez ściany i anulowaniem ruchu. Pułapki/DoT pozostawiają Downed do interakcji; po zwycięstwie automatyczne podniesienie również przy końcowym zastosowaniu wyniku. Bez zmian serialized state/save formatu. 97/97 EditMode PASS (`output/exploration-revive/tests.xml`), READY_FOR_SMOKE; manualny smoke NOT RUN.
  - 2026-10-10: naprawione zasłanianie postaci przez filary/stojące przeszkody oraz niskie murki/balustrady; wspólne sortowanie względem podstawy, także w walce, gruzy pozostają na podłodze. Kod/testy bez serialized state. Końcowe 13/13 testów PASS, 5 nowych przypadków; szersze 42/52 z dokładnie tymi samymi 10 błędami narożników co w kontrolnym przebiegu bez tej poprawki (`output/dungeon-obstacle-depth`). READY_FOR_SMOKE, manualny smoke NOT RUN.
  - 2026-10-10: poprawione tekstury warstw mikstur/bomb — objętość szkła i skupiska ziaren odzyskane z neutralnych oryginałów, zamiast płaskiego domalowanego wypełnienia. Podmiana 4 PNG, bez zmian masek v3, obrysu, GUID/meta i runtime. 1/1 test EditMode PASS; audyt 6 wariantów/4 profili Point i 5286 pikseli poza maskami PASS, podgląd Unity sprawdzony (`output/asset-review/consumable-layer-textures-v4`). READY_FOR_SMOKE; świeży Play Mode odświeża cache ikon.
  - Cały przebieg od produkcyjnego menu do końca slice’a; nowa gra/kontynuacja, save/load, kolejne wyprawy i powtarzalne misje, progresja każdej postaci, crafting/handel, bossowie i nagrody.
  - Ważny punkt acceptance: odtechnicznić i poprawić teksty tooltipów w całej ścieżce gracza, tak aby opisy jasno wyjaśniały działanie przedmiotów, umiejętności i statusów bez wewnętrznych terminów systemowych. Sprawdzić też prezentację buffów: jeden buff z kilkoma efektami ma być widoczny jako jeden status ze wspólnym opisem, a nie jako dwa osobne buffy.
  - 2026-10-10: grupowanie aktywnych efektów wspólnego skilla/rzucającego lub instancji mikstury w jedną ikonę i wiersz podglądu, ze wspólnym tooltipem wszystkich składników. Różne czasy działania pokazane per składnik; odświeżanie podglądu uwzględnia wygaśnięcie składnika. Zachowana nazwa mikstury po zużyciu i obsługa starszych efektów eksploracyjnych bez metadanych źródła. Kompilacja oraz 114/114 testów EditMode na kopii projektu PASS (`out/grouped-buffs-verified.xml`): prezentacja statusów/mikstur, efekty bojowe, używanie i crafting mikstur, serializacja zapisów. READY_FOR_SMOKE; manualny smoke NOT RUN. Bez zmian prefabów i ręcznego wiring.
  - 2026-10-10: ikony aktywnych efektów mają wspólny fallback do ikony źródłowego skilla przed pytajnikiem (pasek statusów, podgląd i tooltip). Dedykowane ikony statusów zachowane; obsługa grup buffów, aur i przekierowania obrażeń. Kompilacja PASS; 18/18 testów EditMode PASS na kopii projektu (`out/buff-icons-tests.xml`), zgodność źródeł potwierdzona SHA256. READY_FOR_SMOKE; manualny smoke NOT RUN. Bez zmian serialized state i ręcznego wiring.
  - Regresja i review końcowej integracji; pomiary rzeczywistej gry, sprawdzenie blockerów/poważnych problemów UX, brak wymaganych debugowych komend oraz osobny smoke UI.
  - Raport PASS/FAIL/NOT RUN, porównanie z katalogiem VS-01; zamknięcie dopiero po akceptacji użytkownika. Nie przedstawiać historycznych testów jako końcowego retestu.

### Milestone: Combat Passives and Affixes v1

Planowany po `Crafting, Upgrading and Consumables v1`. Wspolny model dla pasywek drzewek skilli, klejnotow i unikatowego equipmentu; rozwija warunkowe efekty walki bez mieszania ich z systemem craftingu.

- `[P1] Dodac unikatowe dropy bossow broni i pancerzy per klasa v1` - `Combat`, `Items / Inventory / Economy`, `Content / Data`
  - Boss ma `30%` szansy na dowolny unikat broni albo pancerza: po `7,5%` na kazda z czterech klas. Unikat losuje normalne czesci equipmentu, ale otrzymuje specjalna pasywke przypisana bossowi zamiast pasywy generycznej.

### Milestone: City Progression and Specialist Services v1

Planowany po `Combat Passives and Affixes v1`. Domyka miejski loop progresji oraz rozwija specjalistyczne uslugi NPC bez mieszania ich z kolejnym passsem walki lub lochow.

- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

### Milestone: Combat v4 — Arena, Obstacles and Transition v1

Planowany po `City Progression and Specialist Services v1`. Rozwija prezentację i taktykę areny walki oraz poprawia przejście eksploracja → walka, nie rozszerzając jeszcze zachowań przeciwników w lochu.

- Pułapki bojowe, zewnętrzne ściany areny i inne przeszkody taktyczne.
- Scrollowana kamera oraz czytelna prezentacja większych aren.
- Lepsza integracja wejścia z eksploracji do walki, w tym ustalone przenoszenie pozycji i formacji.
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

Ustalony kierunek: seamless eksploracja ↔ walka w jednej scenie lochu, bez przeładowania mapy i bez osobnej produkcyjnej reprezentacji areny. Te same obiekty mapy i postacie pozostają w scenie; zmieniają się tryb, sterowanie, HUD i kamera. Logika oraz runtime walki pozostają odseparowane od prezentacji. Osobna scena walki może pozostać sandboxem do testów.

Branch: `milestone/combat-v4-arena-obstacles-transition-v1`, utworzony z `origin/main` po merge PR #13 (`e6ee59b3`). Karty poniżej są w Backlog; sugerowana kolejność wykonania odpowiada kolejności listy. Testy i self-review należą do każdej karty, nie tylko acceptance.










Poza zakresem: FOV/pościg/zasadzki, nowe sterowanie grupami i pathfinding eksploracyjny z Dungeon Exploration v3, nowe pule galdurytów oraz zaległa karta unikatów bossów. Nie dołączać ich automatycznie do Combat v4.

### Milestone: Dungeon Exploration v3 — Threats and Traversal v1

Planowany po `Combat v4 — Arena, Obstacles and Transition v1`. Dodaje aktywne zagrożenia i możliwości przemieszczania w lochu, wykorzystując ustalone zachowania walki i miasta.

- `Dungeon Selector v2`: przed wejściem gracz wybiera spośród kilku wylosowanych propozycji lochów. Każda propozycja ma niezależny seed i poziom losowany wokół średniego poziomu aktywnej drużyny; UI pokazuje theme oraz — gdy zostanie zaimplementowany — rozmiar, nigdy samego seedu.
- FOV i Fog of War: wspólny model powstaje w tym milestone’u. Scouting eksploracyjny odłożony na polecenie użytkownika do Dungeon Exploration v4; będzie korzystać z tego systemu, bez równoległej implementacji.
- Istniejące kotwy przeciwników, FOV, wykrywanie i pościg należą do v3. Specjalne autorskie zasadzki i ukryte encountery aktywowane warunkiem przeniesione do Dungeon v4; rozwój dopasowania składów do kotew pozostaje przy późniejszym content passie.
- Pathfinding sterowany myszką należy do v3. Skakanie/parkour i nowe zachowania pułapek eksploracyjnych przeniesione do Dungeon v4.
- Rozbudowane zagadki logiczne i zręcznościowe przeniesione do Dungeon v4. V3 zachowuje istniejące dźwignie, warunki i bramy progresji.
- Zaawansowane sterowanie drużyną: przypinanie/odpinanie, tworzenie grup, przełączanie poprzez klik na mapie oraz menu kontekstowe: przypnij/odepnij; "pan to"; inspect
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

Ustalenia startowe: realizujemy jeden połączony milestone Dungeon Exploration v3 — Threats and Traversal v1. Branch: `milestone/dungeon-exploration-v3-threats-traversal-v1`, z `origin/main` po merge PR #14 (`e46f04df`). Zacząć od konfiguracji pokojów, następnie rozkładu zawartości. Kolejność dalszych kart ustalić według zależności (model wysokości przed zależnymi zmianami geometrii/FOV/ruchu, wykrywanie przed dołączaniem i pościgiem); nie rozpoczynać całego milestone'u jednym diffem.



Wspólny model eksploracji i walki: seamless w jednej scenie, te same obiekty i jednostki. Ograniczenie starcia do pokoju z Combat v4 jest przejściowe i zostanie zastąpione uczestnictwem opartym na wykrywaniu. Nie tworzyć osobnego milestone’u dla wysokości.





### Milestone: Dungeon Exploration v4 — zakres zatwierdzony po audycie v3

Decyzja użytkownika 2026-09-16: poniższe mechaniki przeniesione z v3. Szczegóły gameplayowe wymagają projektu przed implementacją. Scouting, toggle skradania i rozwój czujności pozostają w v4 zgodnie z wcześniejszymi kartami.

- `[P1] Zaprojektować i wdrożyć zagadki logiczne i zręcznościowe`
  - Rozwinąć istniejące warunki i bramy o rzeczywiste zasady zagadek, czytelny stan, konsekwencje i save/load. Kolejność aktywacji, symbole i etapy to kandydaci, nie zatwierdzone mechaniki. Uzgodnić authoring i scenariusze obowiązkowe/opcjonalne.
- `[P1] Rozwinąć zachowania pułapek eksploracyjnych`
  - Zachować obecne obrażenia/statusy i wspólny runtime. Zaprojektować nowe zachowania; cykle czasowe i sekwencje zręcznościowe są propozycjami do decyzji. Skoordynować z zagadkami oraz wysokościami.
- `[P1] Zaprojektować i wdrożyć skakanie / parkour eksploracyjny`
  - Uzgodnić zasięg, kwalifikację miejsc startu/lądowania, przesłony, przechodzenie nad dziurami/lawą i konsekwencje niepowodzenia. Schody i przekraczanie niskich przeszkód nie zastępują tego systemu.
- `[P2] Zaprojektować specjalne zasadzki i ukryte encountery`
  - Osobne autorskie warunki ujawnienia/aktywacji ponad istniejące FOV i Fog. Rozwinąć istniejące uczestnictwo, bez drugiej reprezentacji jednostek. Konkretne wyzwalacze wymagają uzgodnienia.

### Karty do decyzji o przypisaniu do milestone'u

- `[Do decyzji] Rozdzielić warianty modyfikatorów danych contentu v1` - `Core / Architecture`, `Content / Data`, `Tools / Editor / Debug`
  - Zastąpić wspólne modele serializowane, które przechowują nieużywane pola zależne od typu (np. jednocześnie statystykę, odporność i zmienną rozgrywki), rozłącznymi wariantami danych albo wrapperem z dedykowanym payloadem dla każdego wariantu. Inspector ma ujawniać wyłącznie pola aktywnego wariantu, walidacja odrzucać niepełne dane, a runtime konsumować typowany payload zamiast ignorować „martwe” wartości. Obecny model `JewelryModifierData` może pozostać rozwiązaniem przejściowym do czasu refaktoru.
  - To karta o ogólnym design patternie dla wariantowych danych contentu; modyfikatory biżuterii są tylko pierwszym konkretnym przykładem tego problemu.
  - Przypisanie do milestone'u oraz priorytet do ustalenia.

- `[Do decyzji] Rozszerzyc blokowanie layoutu pokoi z bounding boxa na rzeczywisty footprint v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
  - Generator ma dopuszczac lepsze upakowanie nieregularnych pokoi, blokujac rzeczywiste zajete komorki zamiast calego prostokatnego bounding boxa; uwzglednic dziury, sasiedztwo pokoi i kompatybilne przejscia.
  - Karta odlozona poza pierwszy pass nieregularnych footprintow; przypisanie do milestone'u oraz priorytet do ustalenia.

- `[P2] Rozwinąć zaawansowane AI i targetowanie w Combat v5` - `Combat`, `Design / Systems`
  - Na polecenie użytkownika: dalszy rozwój decyzji AI i wyboru celów po podstawowym pościgu/utracie kontaktu v1. Szczegóły uzgodnić przed implementacją.

### Combat v5 — inventory i szybkie używanie przedmiotów

- `[P1] Zintegrować okno postaci i inventory z akcjami walki v1` - `Combat`, `Items / Inventory / Economy`, `UI / UX`
  - Combat v5, poza bieżącą kartą niszczenia przeszkód i Dungeon Exploration v3. Okno postaci dostępne w walce, bez przycisków Umiejętności i Rozwój; inventory pozostaje dostępne do przeglądania.
  - Udostępnić zmianę zakładanego wyposażenia za AP oraz używanie potionów i bomb bezpośrednio z inventory. Wszystkie wejścia UI korzystają z tej samej walidacji i wykonania akcji walki, kosztów, targetowania oraz zużycia przedmiotów; bez obchodzenia kosztów przez drag/drop lub inne okno.
  - Przed implementacją uzgodnić koszt zakładania/zdejmowania/zamiany wyposażenia, ograniczenie do własnej tury, transfer przedmiotów między postaciami oraz zachowanie okna podczas targetowania. Nie przyjmować konkretnych wartości AP bez decyzji użytkownika.
  - Weryfikacja: brak AP, anulowanie targetowania, zużycie dokładnie raz, przeliczenie statystyk/pasywek po zmianie wyposażenia, rozdzielona drużyna i równoległe sesje; brak zapisu w środku walki. Osobny smoke UI.

- `[P2] Zastąpić przycisk consumables hotbarem przedmiotów v1` - `Combat`, `Items / Inventory / Economy`, `UI / UX`, `Persistence`
  - Combat v5; po karcie integracji inventory z akcjami walki. Hotbar zastępuje obecny przycisk otwierający wszystkie consumables w inventory i korzysta ze wspólnej obsługi użycia przedmiotów.
  - Przed implementacją przedstawić układ UI i uzgodnić liczbę slotów, przypisywanie/drag/drop, skróty klawiszowe, przypisanie per postać lub drużyna, obsługę stosów/ładunków i pustych slotów oraz save/load konfiguracji. Docelowy model nie jest jeszcze zatwierdzony.
  - Pokazywać dostępność i koszt użycia spójnie z inventory; ten sam przebieg wyboru celu, anulowania i zużycia. Statyczny layout i referencje zapisane w prefabach UIW. Acceptance funkcjonalne i osobny smoke UI.

### Kontrolowany RNG — po pierwszym content passie

- `[P2] Zintegrować kontrolowany RNG z trafieniami i krytami` - `Combat`, `Core / Architecture`
  - Po pierwszym content passie. Osobne historie trafienia/kryta i jednostek; ustalić trwałość historii, tolerancję odchylenia ogólnej średniej i wpływ buffów zależnych od wyników. k=2 to punkt wyjścia, nie gwarancja poprawności wszystkich zależności. UI pokazuje nominalne statystyki. Nie dodawać zapisu w środku walki.
- `[P2] Zintegrować kontrolowany RNG z efektami umiejętności i pasywek` - `Combat`
  - Po pierwszym content passie, po trafieniach/krytach. Ustalić kwalifikację, niezależność źródeł, multi-hit/AOE i efekty zależne; brak zużycia historii przy odporności lub niewykonanej próbie.
- `[P2] Zintegrować kontrolowany RNG z lootbagami i nagrodami` - `Items / Inventory / Economy`, `Persistence`
  - Po pierwszym content passie. Ustalić źródła/kategorie, historię i checkpointy; zapisany loot bez przelosowania. Nie przenosić automatycznie każdego losowania ilości i statystyk.
- `[P2] Zintegrować kontrolowany RNG z ulepszaniem` - `Items / Inventory / Economy`
  - Po pierwszym content passie, po audycie zmiennych szans, kosztów i przygotowywania sukcesów tanimi próbami. Osobno zaprojektować wyniki wielowariantowe. Wymagana weryfikacja wartości ekonomicznej, nie tylko liczby sukcesów.

- `[P2] Zaprojektować i wdrożyć scouting eksploracyjny w Dungeon Exploration v4` - `World / Dungeon / Exploration`, `Design / Systems`
  - Na polecenie użytkownika przenieść scouting z Dungeon v3 do Dungeon v4. Przed implementacją uzgodnić konkretne mechaniki; wykorzystać istniejący model widoczności i pamięci mapy, bez równoległego systemu.
  - Bieżące FOV, Fog of War, odkrywanie mapy i podgląd Ctrl pozostają w zakresie Dungeon v3.

- `[P2] Dodać przełączany tryb skradania w Dungeon Exploration v4` - `World / Dungeon / Exploration`, `UI / UX`, `Design / Systems`
  - Na polecenie użytkownika odłożyć toggle sneak do Dungeon v4. Przed implementacją uzgodnić wpływ na ruch, wykrywanie, ewentualny hałas oraz sterowanie drużyną. Nie oznacza to zmiany podglądu FOV pod Ctrl.

- `[P2] Rozwinąć narastanie wykrycia w Dungeon Exploration v4` - `World / Dungeon / Exploration`, `Design / Systems`
  - Zgodnie z decyzją użytkownika skalowanie odległością i dalsze dopracowanie czujności przenieść do Dungeon v4. Bieżące v1 zachowuje stałe tempo i jeden licznik wróg–drużyna. Konkretne funkcje skalowania wymagają projektu i zatwierdzenia.

## In Progress

### Vertical Slice v1

- `[P1] [VS-18] Zintegrować i zbalansować pierwszą kompletną pętlę` - `Design / Systems`, `Content / Data`
  - 2026-09-22: praca nad kartą odłożona do gotowości contentu. Wdrożone pakiety zachowane; nie zamykać integracji na danych testowych. Bieżący etap wrócił do VS-01 → VS-02.
  - Anty-stall 2026-09-22: alert po 2 turach ostatniego przeciwnika bez obrażeń, posiłki po 3. Losowa połowa zwykłego encountera (w górę), poziom wyprawy, wolne pola przy wejściach, normalne AV, bez XP/złota/lootu. Brak miejsca odracza falę; kolejne fale możliwe po ponownym pozostawieniu jednego przeciwnika. Skład utrwalany w zapisie. Kompilacja i 119/119 EditMode PASS (reinforcements-fixed.xml); smoke Play Mode NOT RUN, instrukcja w UNITY_HANDOFF.md.
  - Obóz + eksploracja 2026-09-22: kod gotowy, 73/73 testów PASS (camp-resources-acceptance.xml). Zużywalne zestawy przydzielane raz na nową wyprawę w liczbie pięter, dowolne zużycie między piętrami, 40% HP/many, usunięcie przy każdym wyjściu i brak uzupełnienia przy wznowieniu. Ekwipunek i menu PPM podłogi; mana/wściekłość co 6 s, impet 0 poza walką. Asset zestawu i rejestracja w katalogu są obecne w workspace; pozostaje smoke według UNITY_HANDOFF.md. Cała karta nadal In Progress, bez commita.
  - IN PROGRESS — synchronizacja 2026-09-21. Rozpoczęto część mechaniczną i liczbową przed końcową integracją pętli. Wdrożono rozwój wyposażenia (upgrade/reforge/rarity i sloty galdurytów), penetrację, regenerację consumables, moc bomb oraz XP.
  - Kod dostaw kowala i rotacji oferty gotowy, zapis stanu i powroty podłączone; trader-stock-final.xml 78/78 PASS. Konfiguracja TraderRoleData i 28 brakujących definicji surowców wykonane za zgodą użytkownika; import, katalog i limity L1/L11/L20 potwierdzone, trader-stock-assets.xml 81/81 PASS. Smoke zaakceptowany przez użytkownika 2026-09-22 („ok, działa”). Pakiet gotowy; cała VS-18 nadal otwarta.
  - Zakaz mieszania tierów części wdrożony w generatorze i craftingu; losowanie kompletnych tierów oraz komunikat kowala. equipment-tier.xml 45/45 PASS. Wymaga smoke craftingu.
  - Wspólna tolerancja grupy Suppression wdrożona; cc-shared.xml 86/86 PASS. Aktualizacja: 2 bazowe tury + długość pierwszego CC, kolejne CC dodaje swój czas i stack; cc-duration.xml 88/88 PASS. Siła stacka z GameDefine. Krzywa szansy pozostaje bez zmian; szczegóły w rejestrze balansu.
  - Kolejny pakiet VS-18: PA w setnych, zachowane koszty ruchu i przyrost impetu, częściowe wypełnienie ikon wraz z podglądem kosztu; ap-verified.xml 175/175 PASS. ActionPointPipFill podłączono na polecenie użytkownika do wspólnego szablonu; dziedziczenie w trzech prefabach i HUD: ap-prefab.xml 17/17 PASS. Pozostaje smoke graficzny według UNITY_HANDOFF.md.
  - Weryfikacja pakietów: equipment-progression-final.xml 77/77 PASS; penetration-consumables-verified.xml 48/48 PASS; xp-progression-final.xml 69/69 PASS. Nie są to testy gotowej pętli slice’a. Użytkownik potwierdził ustawienie Game Defines po pakiecie XP; brak jednoznacznego potwierdzenia pełnego smoke.
  - Tempo nie jest związane ze stałym „1 poziomem na wyprawę”. Awans round(1200 * L^1.5 + 1200); baza XP przeciwnika 6 * (247 - 2L) * (L^1.5 + 1) / 49; kara 15% za poziom przewagi gracza, ograniczona do 6 poziomów; standardowa misja = 20 przeciwników jej poziomu. Limit 50. Długość i liczba walk zależą od wyprawy; boss opcjonalny.
  - Pomiar 2026-09-24 przed zmianą: 12/12 produkcyjnych generacji PASS (6 seedów zwykłych i 6 bossowych, po 2 piętra). Zwykła wyprawa: średnio 10,5 walki, 32,3 wroga i tylko 1,83 skrzyni; na 24 piętrach średnio 0,92 skrzyni zamiast ustalonych 2–3 przy 6 walkach. Skrzynie nie dawały złota. Wdrożono złoto za 2–3 rzeczywiste encountery, pobranie w oknie łupu i zapis/wczytanie; 39/39 testów snapshotu PASS. Za zgodą użytkownika skonfigurowano w izolowanej kopii Unity i przeniesiono do głównego projektu po jednej skrzyni w Mechanizmie grobowca i Krypcie warunku. Testy zapisanych assetów i produkcyjnych wypraw 13/13 PASS. Sweep 40 seedów na każdy wariant wyprawy: 80 wypraw, 160 pięter, na każdym 2–3 skrzynie z dodatnim złotem; test PASS. Ponowne uruchomienie konfiguratora nie zmieniło assetów. Smoke Play Mode pozostaje; bez commita/pusha.
  - Orientacyjny rachunek XP przy średnich 32,3 wroga i dwóch ukończonych zwykłych misjach na wyprawę: ~181% progu następnego poziomu na L1, ~153% na L20. Uproszczona sekwencja osiąga L20 po około 13 wyprawach (około 4–7 h przy 20–30 min/wyprawę); ignoruje zmiany poziomu w trakcie wyprawy, kary poziomowe i pominięte misje. Nie zmieniać krzywej XP na tej podstawie bez pomiaru pełnej pętli Play Mode.
  - Wstępny budżet złota sprzed pomiaru produkcyjnych seedów: przy średnich 32,3 wroga, dwóch zwykłych misjach i pięciu skrzyniach około 1400 zł L5 albo 5500 zł L15 za dwupiętrową wyprawę brutto, przed zakupami, kosztami craftingu i sprzedażą łupu. Ten orientacyjny rachunek zastępuje dokładniejsza symulacja poniżej.
  - Symulacja 2026-09-25 po konfiguracji skrzyń: 80 wygenerowanych wypraw/160 pięter w izolowanej kopii Unity, test 1/1 PASS; 10 000 losowań ekonomii na L5 i L15 oraz 3000 trajektorii XP na wariant misji. Zwykła wyprawa: średnio 10,25 walki, 31,2 wroga, 4,75 skrzyni; złoto brutto z dwoma misjami około 1365/5316, XP na aktywną postać około 172%/158% progu. Dwie misje na wyprawę dają średnio 14 wypraw do L20; jedna 18,8, bez misji 29,6. Sam łup jednej wyprawy zwykle wystarcza na 2 normalne sety broń+pancerz z wybranych lokalnych receptur; z lokalnym stockiem na 2–3, ponad celem „1 set na wyprawę” dla tych wariantów. Obniżenie samej szansy torby 50→40% lub zawartości skrzyń 2–3→1–2 torby nadal daje zwykle 2–3 takie sety. Szczegóły, założenia i test wrażliwości HP: [BALANCE_VS18_SIMULATION.md](BALANCE_VS18_SIMULATION.md). Liczby nie stanowią smoke walk ani decyzji o zmianie dropu/receptur.
  - UX walki 2026-09-25: za zgodą użytkownika boss rzuca zaległą falę dopiero po zniknięciu wszystkich wcześniejszych run; runy wybuchają w trzeciej własnej turze AV, a HUD pokazuje ich ikony i licznik 3→2→1 w kolejce. AI wycenia status według szansy po odporności/tolerancji i pomija już aktywną kontrolę bez stackowania oraz aktywne modyfikatory; DoT mogą być ponawiane, bo ich obrażenia się sumują. Niewidzialność ostatniego wroga wstrzymuje licznik posiłków, również gdy kończy się na początku jego tury. Asset runy i opis skilla zaktualizowane. Kompilacja oraz 84/84 celowanych testów EditMode PASS w izolowanej kopii Unity (`vs18-annoyance-retest.xml`). Smoke Play Mode pozostaje; bez commita/pusha. Nie zmieniono zasięgu ani obrażeń wybuchu run.
  - Korekta umiejętności przeciwników 2026-09-25: Podpalona Strzała ma podpalenie 50% zamiast 100% siły na tick; Dziki Ryk ma krwawienie 50% zamiast 80%; Kołczan Zawiści kosztuje 30 zamiast 40 PA. Opisy pierwszych dwóch umiejętności zaktualizowano. Import/kompilacja i 7/7 testów contentu Katakumb PASS w izolowanej kopii Unity (`vs18-enemy-balance.xml`). Smoke Play Mode pozostaje; bez commita/pusha.
  - Uwaga do modelu ekonomii: liczba setów powyżej uwzględnia pełne wymagane ilości różnych materiałów, ale zakłada pełne czyszczenie kolejnych wypraw Katakumb i wybór lokalnych receptur. Nie opisuje tempa wyposażania przy przeplataniu różnych motywów lochów; szczegóły dopisano w raporcie.
  - Pozostają: końcowa integracja z gotowym contentem, misjami i gospodarką, pomiar rzeczywistego tempa oraz smoke/acceptance. Szczegóły ukończonych pakietów i pozostałych ustaleń: BALANCE_DECISIONS_PENDING.md; konfiguracja i smoke: UNITY_HANDOFF.md.
  - Po gotowości kart pierwszego lochu, drużyny, menu, ekonomii i misji: nowa gra → przygotowanie → wyprawa/misja → boss → powrót → nagroda → rozwój → kolejna wyprawa.
  - Dostroić liczby i pule contentu, tempo XP/ekwipunku, dostępność zasobów i trudność; wyeliminować blockery i poważne problemy UX. Nie jest to ogólny polish mechanik.


## Done

- `[P1] [VS-17] Dodać podstawowe powtarzalne misje` - `NPC / Quest / Dialogue`, `Content / Data`, `Persistence` - `DONE`
  - Trzy równoczesne, odnawialne zlecenia u strażnika Arungardu: 12 zwykłych przeciwników Katakumb, ukończenie wyprawy oraz pokonanie Ducha Umarłego Króla i oficjalne wyjście. Postęp liczy się od przyjęcia; ukończona wyprawa musi mieć poziom co najmniej równy poziomowi misji. Zlecenie bossa jest dostępne tylko z ofertą wyprawy bossowej.
  - Poziom zwykłych zleceń wynika ze średniej aktywnej drużyny, poziom zlecenia bossa z oferty. Nagrody i ewentualny przedmiot są losowane przy generacji zlecenia, widoczne z góry i utrwalane w zapisie. Warianty: 30% XP 125% / złoto 75%, 30% złoto 125% / XP 75%, 40% oba 100%; 2/3 honoru i 10% szansy na przedmiot.
  - UIW tablicy zleceń i przycisk u strażnika, dostęp do dziennika również w lochu. Kod, prefab i sceny skonfigurowane. 43/43 celowanych testów EditMode oraz kompilacja PASS; użytkownik potwierdził poprawne działanie misji w Play Mode 2026-09-24 i zlecił commit/push.


- `[P1] [VS-16] Przygotować bossa Katakumb` - `Combat`, `Content / Data`, `World / Dungeon / Exploration` - `DONE`
  - Duch Umarłego Króla, powtarzalny w wariantach poziomu 10 i 20; specjalny unikat w zakresie. Boss występuje w wybranych wyprawach, nie w każdej. Nowa wyprawa pozwala ponownie zdobywać nagrody; wczytanie tej samej ukończonej walki nie może ich dublować.
  - Arena, zatwierdzony zestaw umiejętności/zachowań, statystyki, nagrody i poprawne zakończenie wyprawy. Sprawdzić porażkę, wygraną, loot i brak ponownego przyznawania nagród po wczytaniu.
  - 2026-09-23: boss, trzy umiejętności, runy z własnymi turami AV, warianty ofert L10/L20, łup i bossowy materiał są skonfigurowane. Pokój finałowy ma nazwę „Komnata Umarłego Króla”. EditMode 96/96 testów walki/ofert/łupu oraz 27/27 testów zakończenia walki/nagród/generatora PASS; końcowy test generacji wyprawy i rozmieszczenia bossa 5/5 PASS. Play Mode smoke, w tym zapis/wczytanie i brak podwójnych nagród, pozostaje do wykonania. Bez commita/pusha.
  - 2026-09-24: po smoke poprawiono rozmieszczenie drużyny przy powrocie do miasta, odrzucanie pustych toreb bossa i usuwanie takich toreb ze starszego stanu, czerwony tint przeciwników, warstwę tooltipów w lochu oraz skrót I dla okna aktywnej postaci. Kompilacja Views/Systems/EditModeTests PASS; 21/21 testów bossa i lootbagów PASS w izolowanej kopii Unity. Ponowny smoke Play Mode pozostaje do wykonania. Bez commita/pusha.
  - 2026-09-24: ofertę bossa oparto na Danger motywu (0–100%) zamiast okna poziomów ±2. Od poziomu 8 ukończona wyprawa zmienia Danger: −30 dla własnego motywu, +10–20 dla innych. Co 10 poziomów najwyższej postaci losowy dostępny motyw osiąga 100%; każde 100% odblokowuje trwałą do ukończenia wyprawę bossową. Ukończenie bossa resetuje Danger motywu. Stan i stały poziom wariantu 10/20 są zapisywane. Docelowa tablica będzie pokazywać jedną wyprawę na motyw; obecne sześć slotów pozostaje w VS-16. EditMode ofert, bossa i save/load 61/61 PASS; smoke Play Mode pozostaje. Bez commita/pusha.
  - 2026-09-24: dodano komendę debugową `setdanger <theme> <0-100>` do szybkiego smoke Danger i oferty bossa bez zmiany poziomu postaci. Kompilacja PASS, testy konsoli i ofert 42/42 PASS w izolowanej kopii Unity.
  - 2026-09-24: poprawiono cykl aktywnej referencji tooltipu przy wyłączeniu HUD-u walki; ekwipunek i łupy po walce korzystają ponownie z tooltipu eksploracji. Kompilacja PASS, testy UI 27/27 PASS; użytkownik potwierdził działanie tooltipów w Play Mode. Bez commita/pusha.
  - 2026-09-23: smoke wykazał, że boss nie atakuje po niektórych stronach drużyny. AI punktowało prostokątne Królewskie Cięcie zawsze w kierunku +X. Teraz ocenia cztery obroty i przekazuje cele z wybranego kierunku do wykonania skilla. Test bossa 7/7 PASS, regresja AI 50/50 PASS. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: przywołane szkielety miały poprawne sprite’y, lecz prefab walki używał rzędu renderowania 0, tego samego co posadzka; zwykłe jednostki korzystały z widoku eksploracji na rzędzie 10. Dynamiczne widoki jednostek w lochu dostają co najmniej rząd 10. Test bossa 8/8, widoków lochu 56/56 i mgły 18/18 PASS. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: przywołany szkielet bez widoku eksploracji nie był uwzględniany przez system widoczności walki. Gdy kończyła mu się mana na aurę, AI nie widziało bohaterów, więc kończyło turę zamiast użyć tańszego ataku. Przywołania otrzymują teraz standardowy zasięg obserwacji 10 pól z zachowaniem ścian i przeszkód. Testy niskiej many 4/4, integracja widoczność→atak 1/1, zestaw bossa 13/13 i regresja walki lochowej 56/56 PASS; kompilacja EditModeTests PASS. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: Zemsta Zaświatów nie odpala już run natychmiast po przekroczeniu 75/50/25% HP. Każdy próg zapisuje jedno użycie; boss w swojej turze rzuca skill przed innymi akcjami za 0 PA/MP, również gdy ma już 0 PA. Przy przekroczeniu kilku progów naraz używa skilla osobno dla każdego. Dotychczasowy czas AV, rozbrajanie i limit miejsc run pozostają. Kompilacja EditModeTests PASS; testy bossa i AI 64/64 PASS. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: skrzynie otrzymują bezpośrednie przedmioty z losowanych toreb, a runy Zemsty Zaświatów ograniczono do bieżącego pokoju bossa. W HUD-zie walki skonfigurowano za zgodą użytkownika górny baner pokazujący użycie skilla, także specjalnego skilla bossa. Kompilacja PASS; testy bossa/prefabu 16/16, tabel łupu 14/14, AI 50/50 i walki lochowej 56/56 PASS. Smoke Play Mode pozostaje do wykonania; bez commita/pusha.
  - 2026-09-24: po zgłoszeniu nakładania się banera na portrety przeniesiono go pod kolejkę tur i nad panel celu. Zmiana obejmuje wyłącznie pozycję, rozmiar i czcionkę w prefabie HUD-u. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: zgodnie z kolejną korektą użytkownika baner jest teraz 8 jednostek pod panelem celu, 560×80 z czcionką 36; tekst dzieli nazwę rzucającego i skilla na dwie linie. Kompilacja Views PASS, smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-24: użytkownik zaakceptował commit/push po korekcie położenia banera; szczegółowy smoke końcowy pozostaje częścią acceptance passa.

- `[P1] [VS-15] Przygotować przeciwników i encountery Katakumb` - `Combat`, `Content / Data` - `DONE`
  - Siedmiu przeciwników, 21 umiejętności, osiem składów po 2–4 wrogów, produkcyjna pula przywołań Kostura Umarłych oraz lootbagi i tabele dla poziomów 1–10 i 11–20. Wariant wyprawy z bossem pozostaje wyłączony do VS-16.
  - Sprite’y przeciwników korzystają z osobnych klatek pixel artu, Point/16 PPU/bez kompresji; docelowa grafika należy do VS-22.
  - 100/100 testów pokrewnych systemów i 7/7 testów zawartości VS-15 po poprawce sprite’ów PASS. Użytkownik sprawdził prezentację w grze i zaakceptował commit/push.

- `[P1] [VS-14] Przygotować produkcyjny zestaw pokojów Katakumb` - `World / Dungeon / Exploration`, `Content / Data` - `DONE`
  - 16 produkcyjnych szablonów: 1 startowy, 9 zwykłych, 3 opcjonalne, 2 korytarze i 1 finałowy; bez podestów/balkonów. Dwa warianty zwykłych pokoi trzeba dodać, pozostałe wybrać i zweryfikować.
  - Pełna konfiguracja Layout Profile i definicji Katakumb; wyprawy z bossem i bez, docelowo 20–30 minut. Sprawdzić stałe wyposażenie, opcjonalne grupy, kilka seedów oraz osiągalność celu i nagród.
  - Nie aktywować karty kotew wysokościowych ani tworzyć nowych zagadek/parkouru.
  - 2026-09-23: za zgodą użytkownika zapisano siedem nowych assetów i dopięto katalog gry. 14 kandydatów reuse poprawnych i płaskich; 16 pokojów produkcyjnych w katalogu. Testy EditMode 46/46 dla pokojów, generacji i zapisanych assetów, 15/15 dla ofert; 20/20 wariantów seed/boss przechodzi rozmieszczenie walk i obiektów. Play Mode i pomiar 20–30 minut pozostają do smoke. Bez commita/pusha VS-14.
  - 2026-09-23: review designu pokojów ujawnił, że dwa nowo dodane pokoje różnią się tylko rozmiarem i ustawieniem filarów, a część starszych szablonów nie ma czytelnej roli. Użytkownik wymaga celowo zaprojektowanych tras, ryzyka/nagrody i wyzwań taktycznych. Status cofnięty do In Progress; poprzednie testy potwierdzają wyłącznie poprawność techniczną, nie jakość projektu pokojów.
  - 2026-09-23: rozpisano role wszystkich 16 sytuacji i kryteria authoringu w [VS14_ROOM_DESIGN.md](VS14_ROOM_DESIGN.md). Kluczowe: skarbiec z pułapką na każdej pieszej trasie do skrzyni, zróżnicowane geometrie walki, kontrola dozwolonych wejść i kotew przeciwników. Assety produkcyjne nadal wymagają przebudowy według tego projektu.
  - 2026-09-23: po review szkiców doprecyzowano do czterech wrogów, zmienne legalne wejścia/wyjścia, większe pokoje i pułapki w przewężeniach. Pula 16 jest początkiem biblioteki osobnej dla Katakumb; co najmniej kilka szablonów ma mieć niestandardowy footprint (klepsydra, L, krzyż, pierścień jak w płaskim MidBalcony, skarbiec-klucz, łamany korytarz), a nie tylko dekoracje w kwadracie.
  - 2026-09-23: wdrożono 16 osobnych szablonów według ról z VS14_ROOM_DESIGN.md, z zapisanymi footprintami, dopuszczalnymi krawędziami drzwi i wymaganymi obiektami. Generator uwzględnia drzwi już podczas szukania pozycji pokoju. Testy EditMode: 59/59 pokojów i wypraw (w tym 12 produkcyjnych seedów z rozmieszczeniem), 16/16 regresji generatora, 15/15 ofert. W izolowanej kopii Unity skonfigurowano assety i przeniesiono je do projektu; pozostaje smoke Play Mode jakości i czasu wyprawy. Bez commita/pusha do akceptacji.
  - 2026-09-23: smoke ujawnił pustą tablicę wypraw. Selektor podawał systemowi ofert tylko starą definicję przypiętą do wejścia w Arungardzie, a ona nie jest dostępna jako oferta. Selektor korzysta teraz również z definicji katalogu gry; test integracyjny otwiera rzeczywisty prefab z tym starym wejściem i potwierdza sześć ofert, w tym oba warianty Katakumb (16/16 testów ofert). Do ponownego smoke w Play Mode.
  - 2026-09-23: smoke ujawnił, że assety pokojów różnią się od wizualizacji. Przyczyną było inne rozszerzanie siatki oraz usuwanie narysowanych ścian obwodowych. Regułę authoringu ujednolicono ze szkicem, dodając tylko otwory wymagane przez drzwi; ponownie zapisano 16 assetów. Po korekcie 60/60 testów EditMode, w tym kontrola footprintów i 12 generowanych wypraw. Do ponownego smoke w Play Mode; bez commita/pusha.
  - 2026-09-23: review ujawnił brak `StructuralAnchors` i przewagę szablonów z dwiema pozycjami wrogów mimo dopuszczenia encounterów 4v4. Dodano pełne ściany, niskie murki i balustrady do 11 pokojów oraz cztery zaplanowane pozycje wrogów do 11 pokojów bojowych; mniejsze pokoje zachowują mniejszą liczbę. Zaktualizowano wizualizację. Po korekcie 60/60 testów EditMode, w tym testy geometrii ścian po generacji i dostępności przejść. Do smoke Play Mode; bez commita/pusha.
  - 2026-09-23: smoke wykazał zbyt liniowy kształt całych pięter. Na 40 piętrach przed zmianą średnio 1,4 zakrętu głównej drogi, dłuższy bok 101 pól, zajęcie obrysu 36%. Profil Katakumb dostał zwarty wybór położenia spośród wszystkich legalnych obrotów i kierunków; cztery szablony dopuszczają dodatkowe boczne przejścia, a udział korytarzy spadł z 40% do 30%. Po zmianie na 40 piętrach: 4,48 zakrętu, dłuższy bok 73 pola, zajęcie obrysu 48%. Testy w izolowanej kopii Unity: 61/61 pokojów i wypraw oraz 16/16 regresji generatora. Pozostaje smoke Play Mode; bez commita/pusha.
  - 2026-09-23: wejście do lochu mogło kończyć się wyjątkiem po 32 próbach (reprodukcja: zwykła wyprawa, seed 15). Skróty między pokojami były dodawane na podstawie styku prostokątów bez sprawdzania autorskich krawędzi drzwi i zajętych przejść. Generator rezerwuje teraz drzwi głównej trasy przed skrótami i dodaje skrót tylko po udanym wyznaczeniu jego geometrii. Testy EditMode: 62/62 zawartości (w tym regresja seedu 15) i 16/16 generatora; dodatkowo 40/40 seedów zwykłej i 40/40 seedów wyprawy z bossem wygenerowało pełne lochy bez wyjątku. Do smoke wejścia w Play Mode; bez commita/pusha.
  - 2026-09-23: użytkownik potwierdził, że układ piętra po korekcie wygląda lepiej. Obecną bibliotekę pokojów uznał za wystarczającą na pierwszy content pass; dalsze dopracowanie geometrii i wyzwań pozostaje na później. To akceptacja kierunku wizualnego, nie potwierdzenie pełnego smoke wszystkich przejść i obiektów. Bez commita/pusha.
  - 2026-09-23: smoke ujawnił cztery usterki: opóźnienie przy wyborze dalekosiężnych skilli, patrole wchodzące w pułapki, walki losowane w pokojach bez walki oraz obiekty ukryte mimo `togglefog`. Duże jednokolorowe podświetlenia skilli są rysowane zbiorczo; usunięto też zbędne obliczanie celów umiejętności obszarowych. Patrol omija aktywne pułapki także po wczytaniu, Skarbiec z pułapką i Odetchnienie wykluczono z losowania encounterów, a debugowa prezentacja obiektów pokazuje ich aktualny stan bez zmiany wiedzy mapy. Testy w izolowanej kopii Unity: 61/61 zawartości, 7/7 rozmieszczania encounterów, 9/9 patrolu, 18/18 mgły, 7/7 targetowania i 9/9 prezentacji walki. Ponowny smoke Play Mode pozostaje; bez commita/pusha.
  - 2026-09-23: użytkownik potwierdził, że wejście do Katakumb po naprawie generatora działa, i zlecił commit/push VS-14. Karta zamknięta; pomiar czasu pełnej wyprawy zależy od produkcyjnych przeciwników VS-15.

- `[P1] [VS-13] Skonfigurować gospodarkę i usługi Arungardu` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`
  - **DONE — 2026-09-23; commit `c57bc387` wypchnięty.** Oferty materiałów oraz po 3 gotowe przedmioty na kategorię. Broń i pancerz mają rozkład jakości 2+1 zależny od najwyższego poziomu w całym rosterze. Rotacja zachowuje część niesprzedanych egzemplarzy; zapis utrwala egzemplarze i efekty. EditMode 53/53 PASS; smoke zaakceptowany przez użytkownika. Audyt: `VS13_ECONOMY_AUDIT.md`. Produkcyjne łupy i misje należą do VS-15/17/18.

- `[P1] [VS-12] Przygotować consumables i receptury` - `Items / Inventory / Economy`, `Content / Data`
  - **DONE — 2026-09-23; commit `f85384d8` wypchnięty.** 24 efekty składników T1, 4 katalizatory oraz receptury Bandaża, Antidotum i Suchej szmatki. Jedna receptura ma jeden kafelek, a warianty materiałów wybiera się w prawym panelu. Scroll i przycinanie list craftingu poprawione.
  - Utility zdejmuje status i daje 30% właściwej odporności na 3 tury. Regeneracja zachowuje łączną wartość przy skracaniu czasu, a ceny utility wynikają z najtańszej receptury. 18/18 testów craftingu/consumables, 19/19 alchemii i 4/4 selektorów PASS; smoke zaakceptowany przez użytkownika. Źródła dropu Katakumb należą do VS-15, oferta NPC do VS-13.

- `[P1] [VS-11] Przygotować wyposażenie, biżuterię i nagrody specjalne` - `Items / Inventory / Economy`, `Content / Data`
  - **DONE — 2026-09-23.** Smoke zaakceptowany przez użytkownika. Cztery sloty biżuterii × osiem metali, 11 klejnotów, 53 efekty galdurytów T1, cztery unikaty bossa w wariantach L10/L20 oraz 435 opisów przedmiotów i części w wersjonowanym katalogu. Runtime, zapis i loot gotowe.
  - Kostur Umarłych ma mechanikę przywołania; produkcyjna pula szkieletów zostanie przypięta w VS-15. EditMode: 156/156, 36/36, 21/21 oraz 12/12 PASS po korekcie tooltipów. Szczegóły: VS11_EQUIPMENT_JEWELRY_REWARDS.md.


- `[P1] [VS-10] Przygotować materiały i części wyposażenia` - `Items / Inventory / Economy`, `Content / Data`
  - Według VS-01: ograniczona pula materiałów i części broni/pancerzy dla klas slice’a, progi progresji, wymagania i zastosowania.
  - Powiązać źródła dropu/zakupu z recepturami; żadnych materiałów bez zastosowania ani receptur bez osiągalnego źródła zasobów. Liczby i nazwy ustalić przed authoringiem.
  - **DONE — 2026-09-22.** Smoke zaakceptowany. 180 materiałów, 115 wariantów receptur i 192 części; wybór metalu igieł oraz minimalna wycena. Poprawiona inicjalizacja podglądu kowala. Szczegóły: VS10_MATERIALS_AND_PARTS.md.


- `[P1] [VS-09] Przygotować drzewka Czarodziejki i Paladyna` - `Content / Data`, `Combat`, `Skills / Progression`
  - Po decyzji VS-01 zakres obejmuje trzecią i czwartą klasę: Alison (Czarnoksiężnik + Mag Ognia) oraz Reimond (Kapłan + Krzyżowiec), pierwsze 4 tiery. VS-08 obejmuje Wladimira (Dowódca + Awanturnik); VS-07 Jerzego (Skrytobójca + Tancerz Ostrzy).
  - Analogiczny zakres do VS-08 dla obu zatwierdzonych klas, z odmiennymi rolami drużynowymi; każdą klasę zweryfikować osobno.
  - Zweryfikować interakcje z pozostałymi klasami, zasoby, opisy i save/load.
  - **DONE — 2026-09-22.** Smoke zaakceptowany; użytkownik zlecił commit/push oraz przejście do VS-10.
  - Poprawki po smoke: jednolite zaokrąglanie odległości euklidesowej dla zasięgów radialnych/pierścieni (bez wyjątku dla 1) oraz indeksowanie siatki/ścian i lokalne wyliczanie highlightów; 103/103 EditMode PASS (`.codex/vs09-radial.xml`). Targetowanie zaakceptowane przez użytkownika.
  - 10 starterów, 4 drzewka po 20 węzłów w pierwszych 4 tierach, ręczny wybór celów Masowego Poświęcenia, Pole Osądu we własnej kolejce AV i zastępowanie istniejącego pola. Assety skonfigurowane w głównym projekcie. 153/153 EditMode PASS (`.codex/vs09-acceptance.xml`); fundament many/save wcześniej 43/43 PASS. Uzgodnienia: VS09_MAGE_PALADIN_CONTENT.md; smoke: UNITY_HANDOFF.md.

- `[P1] [VS-08] Przygotować drzewka drugiej wybranej klasy` - `Content / Data`, `Combat`, `Skills / Progression`
  - **DONE — 2026-09-22; użytkownik zaakceptował commit/push.** Wladimir: 5 starterów, Dowódca + Awanturnik, po 20 węzłów w pierwszych 4 tierach. Assety skonfigurowane; aktywne skille, pasywy, rangi, koszty, wymagania, opisy i przydział skilli z drzewek wdrożone.
  - Uzgodniona i wdrożona wściekłość: pełna pula pozwala zapłacić koszt przekraczający maksimum; kara -2% obrażeń za brakujący PW przez 3 tury, z węzłem Wściekłość -1%. Riposta +50%/+25% priorytetu AI (cap +125%); Pragnienie Śmierci pozostawia 1 HP. Szczegóły VS08_WARRIOR_CONTENT.md.
  - Testy: pełny EditMode 1331/1331 PASS; końcowe 95/95 PASS, self-review zakończony. Pełny test ręczny odłożony do acceptance; nie oznaczać jako zaliczonego. Smoke w UNITY_HANDOFF.md.
  - Poprawki UI: pełne podsumowania pasywów czterech drzewek, natychmiastowe odświeżanie postaci/loadoutu po zakupie i ograniczenie pozycji okna drzewka do workspace.
  - VS-07 zamknięta i wypchnięta osobno (`db70555a`). VS-08 zaakceptowana do commita/pusha; unrelated VS-18 zachowane.

- `[P1] [VS-07] Domknąć content Scouta dla slice’a` - `Content / Data`, `Combat`, `Skills / Progression`
  - **DONE — 2026-09-22; pełny test ręczny odłożony do acceptance na polecenie użytkownika.** Pięć startowych skilli Jerzego, 20 węzłów Skrytobójcy i 20 Tancerza Ostrzy (pierwsze 4 tiery), pasywy drzewek, koszty, wymagania, opisy i odblokowywanie aktywnych skilli.
  - Wdrożone: Rykoszet/łańcuch, niezależne dodatkowe ciosy, Złość, noże/wyjmowanie LPM/PPM, Linka, kierunek i ataki w plecy, Wojownik Cieni z ruchem o 1 pole, efekty obu drzewek; UI/statusy, zapis i odtworzenie skilli z węzłów. Istniejące ID zachowane, Guardian poza ofertą.
  - Testy: pełny EditMode 1302/1302 PASS; końcowe 72/72 PASS. Assety skonfigurowane przez Unity API. Szczegóły VS07_SCOUT_CONTENT.md; smoke UNITY_HANDOFF.md.
  - Użytkownik autoryzował commit/push i przejście do VS-08. Nie oznacza to zaliczenia manualnego smoke; unrelated VS-18 zachowane.

- `[P1] [VS-06] Przygotować drużynę vertical slice’a` - `Content / Data`, `NPC / Quest / Dialogue`
  - Po VS-01/03: wybrane postacie z rolami, statystykami, zasobami, portretami i startowym wyposażeniem. Ustalić początkowy skład oraz dostępność/rekrutację przez istniejące mechanizmy.
  - Sprawdzić wybór lidera, grupy, inventory, rozwój i save/load dla każdej postaci. Nie wprowadzać nowego systemu relacji/rekrutacji bez potrzeby slice’a.
  - 2026-09-22: otwarta po zaakceptowanym smoke i commit/push VS-05 (`d2f4d1c4`). Skład, dostępność, wyposażenie L1 i start kampanii gotowe. Context pass: Statystyki Postaci.xlsx; istniejące CharacterDefinition i fabryka. Do uzupełnienia odporności, zasoby (Wladimir nadal 200 zamiast 50), bazowy kryt Alison/Reimonda oraz prezentacja; sprawdzić lidera/grupy/inventory/progresję/save-load. Startowe skille wdrażane w kartach klas VS-07–09.
  - 2026-09-22: użytkownik zatwierdził Alison 20/Reimond 15 MP/t i istniejące sprite’y do VS-22. DONE — smoke zaakceptowany przez użytkownika: dane i sprite’y skonfigurowane. 37/37 EditMode PASS; końcowy retest karty 10/10 PASS, Play Mode i kontrola obrazu PASS. Sprawdzone lider/grupy/ekwipunek/awans/save-load. Self-review zakończony. Odbiór w UNITY_HANDOFF.md, szczegóły VS06_PARTY.md. Commit/push autoryzowane.

- `[P1] [VS-05] Przygotować początek gry w Arungardzie` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`
  - Po katalogu i w integracji z VS-04/06: stan nowej kampanii, startowe zasoby/wyposażenie, przedstawiony cel i czytelne skierowanie do pierwszej wyprawy.
  - Bez debugowego rozdawania testowego contentu w ścieżce gracza. Uzgodnić krótkie wprowadzenie i dostępność usług; sprawdzić nową grę oraz zapis/wczytanie początku.
  - 2026-09-22: otwarta po commit/push VS-04 (`d7c8d8e6`). Context pass: ExplorationSandboxBootstrap tworzy roster z bindingów Arungardu (obecnie 2); CharacterInstanceFactory daje L1 i puste inventory; QuestContentBootstrap rejestruje zadania jako Unavailable. Usługi i quest strażnika istnieją. Zachować wcześniejsze ustalenie czterech postaci dostępnych od początku; docelowy authoring postaci należy do VS-06.
  - DONE — smoke zaakceptowany przez użytkownika (git): zatwierdzone 500 złota, zero mikstur, broń/pancerz L1 czterech klas według przekazanego JSON. Sprzęt startowy: sprzedaż 0, brak ulepszania i reforgowania. Pierwszy cel prowadzi do strażnika; marker dostępnego zadania. Wszystkie usługi od początku, galduryty od L11 dowolnej postaci (także rezerwy). Profil kampanii i konfiguracja Bootstrap/Arungardu gotowe; 104/104 EditMode oraz Play Mode PASS. Brak ponownego przydziału na wczytaniu/powrocie. Self-review zakończony; odbiór w UNITY_HANDOFF.md. Commit i push autoryzowane.

- `[P1] [VS-04] Wprowadzić produkcyjne menu i start gry` - `UI / UX`, `Core / Architecture`
  - DONE — 2026-09-22. Kod menu/ustawień, nowa kampania i kontynuacja gotowe; 39/39 testów PASS (vs04-menu-verified.xml), self-review zakończony. Konfiguracja Bootstrap wykonana za zgodą użytkownika: referencje, przyciski, panele, aktywny suwak i układ. 42/42 EditMode PASS (vs04-ui-verified.xml), automatyczny Play Mode smoke menu i Nowa gra → Arungard PASS; podglądy 1080p/720p sprawdzone. Odbiór użytkownika zatwierdzony poleceniem commit/push; ustawienia ekranu/Wyjdź nie były automatycznie testowane w buildzie.
  - Poprawka opóźnień: cache podsumowań zapisów, brak odczytu przy zamykaniu ustawień, wybór zapisu bez przebudowy listy. 44/44 EditMode i Play Mode smoke PASS; na 268 zapisach ponowny odczyt ~33 ms zamiast ~4,6 s, wybór ~1 ms. Pierwsze indeksowanie pozostaje synchroniczne.
  - Context pass po zamknięciu VS-03. Context pass: obecne BootstrapMenuView uruchamia sandboxy; zachować istniejący SaveManager/SceneLoader. Użytkownik zatwierdził pionowe menu po lewej, osobny panel zapisów, głośność/rozdzielczość/pełny ekran; nową kampanię bez usuwania starych, kontynuację najnowszego poprawnego zapisu i powrót do menu przy błędzie.
  - Nowa gra, kontynuacja, wczytanie, podstawowe ustawienia i wyjście. Produkcyjny przepływ bez „Launch Sandbox”; sandboxy mogą pozostać narzędziami developerskimi.
  - Przed implementacją uzgodnić layout, zakres ustawień, zachowanie przy istniejącym zapisie oraz błędach wczytania. Korzystać z istniejącego save/scene flow i UIW; statyczny UI w prefabach/scenach.
  - Smoke od uruchomienia gry, bez ręcznego otwierania sceny rozgrywki.

- `[P1] [VS-03] Przeprowadzić uzgodniony refaktor klas pod content slice’a` - `Core / Architecture`, `Combat`, `Skills / Progression`
  - DONE — smoke zaakceptowany i commit/push zlecone 2026-09-22. Wspólny resolver kosztów/parametrów, reakcje pasywne i OnHeal, walidacja, ochrona przed rekurencją i wielokrotnymi procami. Poprawki smoke: punktacja kosztu AI oraz wydajność wyszukiwania drogi na piętrach. Kontrakt: VS03_RUNTIME_CONTRACT.md.
  - Weryfikacja workspace: 155/155 głównej regresji, 70/70 po poprawce ataków, 63/63 po poprawce wysokości. Katalog assetów/drzewek pozostaje w kartach contentowych.


- `[P1] [VS-02] Przeprowadzić review gotowości projektu pod content pass` - `Core / Architecture`, `Tools / Editor / Debug`
  - DONE — raport zaakceptowany przez użytkownika 2026-09-22. [VS02_CONTENT_READINESS_REVIEW.md](VS02_CONTENT_READINESS_REVIEW.md): 10 znalezisk z dowodami i właścicielami, pokrycie 8 drzewek oraz minimalny zakres VS-03.
  - VS-03: wspólny resolver kosztów surowca, wymagane operacje reaktywne pasywów, parametry skilli i walidacja obsługi. Pozostałe braki przypisane do kart contentu; bez ogólnego refaktoru.
  - Weryfikacja statyczna i self-review zakończone; nie zmieniano kodu ani assetów. Build/EditMode/PlayMode NOT RUN. Zamknięcie review nie oznacza naprawienia opisanych braków.

- `[P1] [VS-01] Ustalić katalog contentu vertical slice’a` - `Design / Systems`, `Content / Data`
  - DONE — zaakceptowane przez użytkownika 2026-09-22. Katalog: [VS01_CONTENT_CATALOG.md](VS01_CONTENT_CATALOG.md). 20 startowych skilli, 160 węzłów, 7 mobów + boss, materiały/części T1/T2, consumables, biżuteria/galduryty, lootbagi, unikaty i zatwierdzone 16 szablonów Katakumb.
  - Odczyt 8 arkuszy, indeks istniejących assetów, kontrola liczebności i review dokumentu zakończone. Katalog określa zakres oraz zależności mechaniczne; authoring i runtime acceptance należą do kolejnych kart.

- `[P1] Dungeon Exploration v3 — acceptance, code review i UI pass` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`
  - Dodać wejście do lochu przez LPM na obiekcie wejścia, otwierające istniejący Dungeon Selector. Obsługa nie może wymagać znajomości nieopisanego skrótu E. Przy kliknięciu z daleka podejście do zasięgu interakcji istniejącym pathfindingiem miasta; nowy rozkaz anuluje podejście. Sprawdzić brak równoczesnego rozkazu ruchu/otwarcia oraz blokowanie kliknięcia przez UI. E może pozostać dodatkowym skrótem, nie jedynym sposobem wejścia.
  - Wejście przez LPM READY_FOR_SMOKE: istniejący collider wejścia, wspólne podejście do punktu interakcji dla NPC i wejścia, anulowanie nowym ruchem/zmianą lidera. Kompilacja i 34/34 testów interakcji, selektora i pathfindingu PASS; smoke NOT RUN.
  - Korekta wejścia 2026-09-16 READY_FOR_SMOKE: automatyczne otwarcie selektora po podejściu, raz na pobyt; ponowne uzbrojenie po odejściu. Stały napis Wyprawy, hover i tooltip w scenie. Końcowe testy 41/41 PASS. Użytkownik zaakceptował pozostały zakres; smoke tej korekty NOT RUN. Bez commit/push.
  - Acceptance 2026-09-16: pełna regresja aktualnego workspace 1127/1127 PASS; dodatkowe benchmarki FOV 3/3 PASS. Review krytycznych integracji bez nowego potwierdzonego blockera. Pomiary CPU i pozostały wspólny smoke/UI/profiler opisane w DUNGEON_V3_ACCEPTANCE.md. Play Mode oraz profiler rzeczywistej sceny NOT RUN; karta pozostaje otwarta.
  - Audyt zakresu zaakceptowany 2026-09-16: domknięcie v3 obejmuje zoom, pomiary wydajności i naprawę istotnych problemów, review oraz wspólne acceptance. Większy polish wizualny podestów/balkonów odłożony na później. Wyniki poszczególnych kart nie zastępują końcowej regresji aktualnego stanu.
  - Zatwierdzone: dodać zoom w eksploracji spójny z walką — kółko myszy, wspólne limity i płynność, bez przechwytywania scrolla nad UI i bez nagłego resetowania przy przejściu eksploracja–walka. Rozwinąć istniejący mechanizm kamery/zoomu.
  - Polish po zaakceptowanym pathfindingu: ograniczyć sporadyczne zacięcia towarzyszy na ostrych zakrętach i pozostały niewielki spadek FPS przy Ctrl; zmierzyć profilerem przed dalszymi optymalizacjami.
  - Wykrycie dowolnego członka drużyny, dołączanie posiłków, starcia przekraczające pokoje, rozdzielona drużyna, utrata/odzyskanie kontaktu, równoległe sesje oraz zatwierdzona geometria wysokości.
  - Regresja persistence, brak duplikacji jednostek i subskrypcji; osobny UI pass, raport PASS/FAIL/NOT RUN i smoke użytkownika.
  - Acceptance obejmuje także kompletność pokojów, kontrolowany rozkład zawartości, selector lochów, sterowanie grupami, pathfinding, istniejące dźwignie/warunki progresji, obecne pułapki i niszczenie obiektów. Nie obejmuje nowych mechanik przeniesionych do v4. Pełny code review oraz osobny UI pass; commit/push dopiero po smoke i poleceniu użytkownika.
  - Etap zoomu DONE: kółko w eksploracji, wspólne limity 2.5–10 i płynne przejście w obu trybach; bez scrolla nad UI. Seamless zachowuje przybliżenie przy pierwszym wejściu i powrocie, istniejące sesje zachowują własne kadry. Kompilacja i 17/17 testów kamery PASS; smoke użytkownika PASS (2026-09-16), commit/push zatwierdzone. Performance, końcowy review i wspólne acceptance nadal do wykonania.
  - DONE — 2026-09-16: użytkownik zaakceptował końcowy smoke obwódki/wejścia i zlecił zamknięcie, commit i push. Wykonano commit c0d68eac i push na origin. Regresja przed korektami wejścia 1127/1127 PASS; późniejsze retesty 41/41 i finalny 21/21 PASS. Profiler pełnej rozgrywki NOT RUN; pozostaje follow-up wydajnościowy, bez deklaracji jego zaliczenia.


- `[P2] Ujednolicić prezentację nazw i inspect jednostek w eksploracji v1` - `UI / UX`, `World / Dungeon / Exploration`
  - Usunąć stale widoczne nazwy nad jednostkami w eksploracji; udostępniać identyfikację przez zachowanie inspect/hover spójne z istniejącą walką. Osobna karta zgłoszona podczas smoke czujności.
  - Rozwinąć istniejących ownerów prezentacji i tooltipów, uwzględnić członków drużyny oraz przeciwników. Przed implementacją pokazać konkretny UX wynikający z istniejącej prezentacji combatowej.
  - Nazwy, tooltipy i podświetlenie nie mogą ujawniać niewidocznych jednostek. Zachować wskaźnik czujności i pełny podgląd FOV pod Ctrl. Zweryfikować przejścia eksploracja–walka–eksploracja, brak stale odtwarzanych etykiet po walce oraz brak przechwytywania inputu.
  - Statyczna prezentacja w prefabach; acceptance wizualne i smoke osobno od bieżącej karty czujności.

  - Zatwierdzony UX: hover z nazwą/HP, PPM → Inspektuj wspólne dla walki i eksploracji. LPM zachowuje ruch/wybór/targetowanie; miejski NPC LPM rozmowa, PPM menu.
  - DONE: wspólny istniejący kontroler inspect, menu UIW, ochrona Fog i sprzątanie przy zmianie trybu/sesji. Arungard, DungeonSandbox i CombatSandbox podpięte. Kompilacja i 26/26 testów kierunkowych PASS; smoke użytkownika PASS. Korekta po smoke: kompaktowy tooltip, osobny nagłówek menu, automatyczne podejście do NPC. Retest 39/39 PASS, smoke poprawek PASS (2026-09-16).

- `[P1] Wdrożyć Dungeon Selector v2` - `World / Dungeon / Exploration`, `UI / UX`
  - Osobna karta milestone'u Dungeon Exploration v3 — Threats and Traversal v1; wyodrębniona z zatwierdzonego opisu milestone'u.
  - Przed wejściem gracz wybiera spośród kilku wylosowanych propozycji lochów. Każda propozycja ma niezależny seed oraz poziom losowany wokół średniego poziomu aktywnej drużyny.
  - UI pokazuje theme oraz rozmiar, gdy model rozmiaru zostanie zaimplementowany; nie pokazuje seedu. Przed nowym UI przedstawić układ i wykorzystać istniejące prefaby UIW.
  - Rozwinąć istniejący wybór/wejście do lochu i generator, zachowując istniejące save'y. Wybrana propozycja musi prowadzić do lochu o przypisanych jej parametrach.
  - Acceptance: niezależność propozycji, poprawny poziom względem aktywnej drużyny, zgodność wybranej oferty z wygenerowanym lochem, brak seedu w UI, weryfikacja uzgodnionego save/load i osobny smoke UI.
  - Zatwierdzone: 6 ofert na mapie okolic, panel szczegółów z boku, aktywna drużyna i Wyrusz na dole (referencja: Darkest Dungeon 1). Jedna oferta na zaokrąglonej średniej poziomów aktywnej drużyny, pozostałe z odchyleniem ±1–3, minimum jedna łatwiejsza i jedna trudniejsza, gdy pozwala zakres 1–50.
  - Oferty zachowane po zamknięciu UI i save/load. Zmiana drużyny przelicza poziomy niewybranych ofert bez zmiany seedów. Nowy zestaw po zakończeniu lub porzuceniu wyprawy.
  - Zatwierdzony wyjątek dla zwoju powrotu: zachowujemy istniejącą instancję i opcję Wznów. Wybór nowej wyprawy po zakończeniu albo porzuceniu bieżącej.
  - Context/design: istniejący DungeonEntranceView i GameBootstrap.TryEnterDungeon pozostają ownerami wejścia; dane ofert w sesji i istniejącym save/load. Projekt UI: okno UIW około 1100 × 700, schematyczna mapa z 6 punktami, panel szczegółów, dolny podgląd drużyny. Zgoda na serialized wiring udzielona; użytkownik zamknął Unity. Prefab selektora podpięty do wejścia w Arungardzie.
  - DONE: 6 trwałych ofert, gwarantowany średni poziom, offsety ±1–3, osobne seedy, aktualizacja poziomów po zmianie drużyny, zapis/wczytanie i wznowienie po zwoju. Nowa tablica po zakończeniu/porzuceniu. Testy EditMode 38/38 PASS; końcowy retest UI 13/13 PASS; smoke użytkownika PASS (2026-09-16). Złota ramka wyboru i skrócony opis różnicy poziomów zaakceptowane.

- `[P1] Dodać minimapę lochu v1` - `World / Dungeon / Exploration`, `UI / UX`
  - Dodane jasnoszare X dla ciał przeciwników, z uwzględnieniem pamięci Fog i zapisanej wysokości.
  - Korekta po smoke: zaakceptowane naprawienie nagłówka, płynne centrowanie oraz widoczność rzeczywistej zawartości pokojów (skrzynie, przeszkody, inne obiekty) zgodnie z pamięcią Fog. Znaczniki postaci, drzwi i schodów zachowują dotychczasową prezentację.
  - Zatwierdzony zakres v1: prawy górny róg, około 220 × 220, centrowanie na sterowanej postaci, bez obracania; identyczna prezentacja w eksploracji i walce. Odkryty teren, drzwi, schody, drużyna i wyłącznie aktualnie widoczni przeciwnicy; nieodkryte miejsca czarne. Wysokość zgodna z oglądanym poziomem, bez nakładania powierzchni. Możliwość zwinięcia; bez wydawania rozkazów kliknięciem i bez większej mapy na tym etapie.
  - Dungeon Exploration v3: dodana na polecenie użytkownika. Korzystać z istniejącej mapy lochu i wspólnego Fog of War/odkrycia, bez drugiego generatora ani ujawniania ukrytych przeciwników.
  - Sprawdzić rozdzielone grupy, zmianę sterowanej postaci, przejścia wysokości i save/load pamięci mapy; osobna weryfikacja performance i smoke UI.
  - DONE: prefab UIW i referencja w scenie lochu gotowe. Wspólne dane Fog, teren cache’owany, bez nowych obliczeń LoS. Testy minimapy/Fog po dodaniu znaczników ciał 29/29 PASS; render prefabu PASS; smoke użytkownika zaakceptowany; profiler GPU NOT RUN. Szczegóły w UNITY_HANDOFF.md.
  - Zamknięcie po smoke użytkownika; commit `0360be6c`, push na origin wykonany.


- `[P1] Niszczenie przeszkód i obsługa obrażeń obiektów v1` - `Combat`, `Core / Architecture`, `Content / Data`
  - Karta Dungeon Exploration v3: serwis obsługi niszczenia, integracja targetowania i obrażeń obiektów oraz trwałość skutków.
  - Przed implementacją uzgodnić podatne obiekty, HP, ataki/efekty, nagrody i stan po zniszczeniu. Nie dodawać mechaniki w karcie statycznych przeszkód Combat v4.
  - DONE: wdrożono opt-in Obstacle, trwałość w liczbie trafień, DealDamage/AoE obu stron, niszczenie PPM → Zniszcz w eksploracji oraz czerwony pasek pozostałych trafień, szarżę i pchnięcie za 1 trafienie bez podwójnego naliczania/przebijania, wspólny stan blokowania oraz save/load. Brak lootu/gruzu/XP.
  - Weryfikacja: pełna regresja przed końcowym review 1070/1070 PASS; retest walki/persistence 117/117 PASS; końcowy retest eksploracji/Fog/niszczenia/persistence 73/73 PASS. Bazowy smoke mechaniki zaakceptowany; poprawka menu PPM, paska HP i niszczenia z noclip: 54/54 PASS, smoke zaakceptowany. Optymalizacja skoku przy highlightach: 25/25 PASS; smoke zaakceptowany przy zamknięciu karty. Authoring i smoke w lokalnym UNITY_HANDOFF.md.
  - DONE — użytkownik zaakceptował zamknięcie, commit i push 2026-09-15. Obejmuje poprawki menu/HP, highlightów, cleanup UI oraz przycisków rozwoju w walce. Końcowa kompilacja i regresja grup/cleanup 19/19 PASS; wcześniejsze zakresowe wyniki zachowane powyżej i w handoffie. Karty inventory i hotbara zapisane dla Combat v5.

- `[P1] Dodać ruch eksploracyjny i patrole przeciwników v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`, `Design / Systems`
  - Osobna karta Dungeon Exploration v3, dodana na polecenie użytkownika: ruch przeciwników poza walką i patrole wspierające sneakowanie.
  - Ruch i obrót aktualizują kierunek sprite'a, rzeczywisty FOV oraz podgląd Ctrl. Wykrycie dowolnego członka drużyny korzysta z istniejącego startu/dołączania do walki; nie przywracać pościgu do lidera jako warunku rozpoczęcia starcia.
  - Rozwinąć istniejących ownerów i reprezentacje przeciwników. Nawigacja respektuje ściany, przeszkody, dostępne powierzchnie i schody; brak drugich kontrolerów tych samych jednostek.
  - Przed implementacją uzgodnić: stanie na warcie, trasy autorskie/losowe, punkty i obszar patrolu, postoje, rozglądanie i obrót, prędkość, reakcję na zablokowaną trasę, korzystanie z drzwi oraz zachowanie po zakończeniu walki. Są to tematy do decyzji, nie zatwierdzone mechaniki.
  - Ustalić save/load stanu patrolu i losowości, zachowanie przy nieaktywnym piętrze oraz upływ czasu, gdy część drużyny walczy. Zapewnić brak resetowania wykrywania i zasobów przez przełączanie trybów.
  - Zależności: FOV i dynamiczne uczestnictwo; skoordynować z autorskim rozmieszczeniem przeciwników i wspólną nawigacją eksploracyjną. Pamięć celu, poszukiwanie, utrata kontaktu i ucieczka pozostają w karcie pościgu; tutaj uzgodnić przekazanie sterowania i ewentualny powrót do patrolu.
  - Acceptance: patrol/obrót zmieniają FOV, wykrycie przerywa patrol bez równoległego ruchu podczas tury; testy zablokowanych tras, drzwi, schodów, kilku patroli i rozdzielonej drużyny; weryfikacja save/load, performance i osobny smoke.
  - Uzgodnione v1: jednakowe domyślne zachowanie; automatyczne, wspólnie planowane trasy po dostępnym pokoju, ograniczenie nakładania obszarów. Prędkość 2 pola/s, postoje 2–3 s przy każdym przystanku; stała pętla z automatycznie dobraną liczbą przystanków, pola tranzytowe bez pauz; bez otwierania zamkniętych drzwi. Zauważenie postaci zatrzymuje patrol i utrzymuje patrzenie na nią do zerwania LoS. Wtedy powrót na trasę; walka przejmuje jednostkę. Ocalali wracają z rzeczywistej pozycji. Patrole poza walką działają podczas starcia innej grupy; nieaktywne piętro zamrożone. Zapis trasy, postępu, pauzy i obserwacji.
  - DONE — zaakceptowane i zamknięte na polecenie użytkownika 2026-09-15 — implementacja i self-review PASS; pełny EditMode 1055/1055 PASS (.codex/patrol-final.xml); smoke patroli PASS; profiler NOT RUN. Podział grafu na spójne obszary, stałe pętle między przystankami z pauzą 2–3 s; korekta przewidywalności i migracji: 68/68 PASS (.codex/patrol-stops-final.xml); dojście do własnego obszaru może się pokrywać z trasą innego wroga. Brak gwarancji niemożności przekradnięcia się.
  - Authoring: Enemy anchor → Ruch poza walką → Patrol/Stationary, domyślnie Patrol. Stationary nie dostaje obszaru patrolowego, normalnie wykrywa/dołącza do walki, wraca na zapisaną kotwę po walce. Stare layouty zachowują Patrol.
  - Końcowa regresja po Stationary: 1061/1061 EditMode PASS (.codex/patrol-acceptance.xml); self-review PASS. Osobny smoke Stationary NOT RUN; użytkownik zlecił zamknięcie, commit i push po dodaniu. Naprawiono również revealmap/clearmap i dodano togglefog. Commit 533a33bc i push wykonane.

- `[P1] Dodac pathfinding ruchu sterowanego myszka w eksploracji lochu v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`
  - Doprecyzowanie 2026-09-15: tutaj dopracować także nawigację towarzyszy wokół przeszkód i pomiędzy wysokościami; wycofana z bieżącej karty wysokości na polecenie użytkownika.
  - Klikniecie walkable celu wyznacza sciezke po mapie lochu i prowadzi druzyne po kolejnych komorkach z uwzglednieniem przeszkod, scian i zamknietych przejsc.
  - Dungeon Exploration v3; istnieje starsza karta o klikanym ruchu po gridzie, ktora nalezy traktowac jako jej poprzednia wersje.
  - Zatwierdzone: płynny freeroam i końcowa pozycja pomiędzy polami; niedostępny cel odrzuca rozkaz. Zwykłe drzwi na trasie otwierane po podejściu; blokady progresji i LeverOnly respektowane. Nawigacja tylko sterowanej grupy, bez teleportowania i łączenia grup.
  - READY_FOR_SMOKE: routing na płaskich i wielopoziomowych mapach; trasy towarzyszy przechowywane, aktualizacja nie częściej niż 0,3 s, ponawianie niedostępnej trasy po 1 s. Komórki pośrednie nie kwantyzują ruchu/końcowej pozycji. Pełna regresja 1043/1043 PASS przed końcowymi poprawkami; finalny retest 79/79 PASS. Self-review PASS; smoke/PlayMode/profiler NOT RUN. Bez commit/push.
  - Korekty po smoke: swobodne podążanie na przejezdnych odcinkach, kolizje i nawigacja na colliderach miasta; regresyjnie optymalizacja Ctrl (lokalny cache FOV i reuse sprite’ów). Regresja korekt 1046/1046 PASS, końcowe kierunkowe 30/30 PASS. Smoke korekt i profiler NOT RUN; bez commit/push.
  - DONE — 2026-09-15: smoke zaakceptowany, commit/push zatwierdzone. Użytkownik potwierdził znaczną poprawę FPS pod Ctrl i poprawne podążanie. Niewielki koszt FPS pod Ctrl oraz sporadyczne zacięcia przy ostrych zakrętach pozostają do polishu, nie blokują acceptance.
  - Korekta 2026-10-10 — READY_FOR_SMOKE: runtime NavMesh podłączony do klikanych tras lidera i planowanych tras towarzyszy na tej samej wysokości. Punkty zakrętów są wykonywane w world space; fizyka 2D i mapa nadal weryfikują każdy segment oraz dokładny cel. Cache per wysokość/promień, początkowy bake podczas konfiguracji piętra, invalidacja po zmianach mapy/drzwi/przeszkód; porównanie geometrii fizycznych propsów po nieudanej próbie umożliwia przebudowę bez zmiany mapy. Schody, niskie mury i otwierane zamknięte drzwi zachowują istniejący routing przejść, gdy potrzebny. Log perf rozróżnia backend=NavMesh/Traversal. EditMode 53/53 PASS na TestMirror (`out/navmesh-integrated-final.xml`); 100 powtarzanych zapytań w fixture ściany: 92,417 ms łącznie (z walidacją kontrolera i narzutem testu). Smoke rzeczywistego lochu/profiler NOT RUN; brak ręcznego wiring/pakietu/serialized state. Nie naprawia automatycznie aktora zapisanego w nieistniejącej komórce (warning Wladimira 0,3). Bez commit/push.

- `[P1] Rozwinąć UI eksploracji i sterowanie drużyną przez party rail v1` - `World / Dungeon / Exploration`, `UI / UX`, `Combat`
  - Osobna karta Dungeon Exploration v3, wyodrębniona z zatwierdzonego zakresu sterowania drużyną.
  - Odpinanie pojedynczych członków drużyny od podążania za grupą i ponowne przypinanie przez party rail; wybór oraz swobodne sterowanie odłączoną postacią. Rozwinąć istniejący model drużyny i reprezentacje jednostek.
  - Tworzenie i wybór grup oraz interakcje wskazane w opisie milestone'u: wybór na mapie i menu kontekstowe przypnij/odepnij, pan to, inspect. Dokładne gesty, model grup, zachowanie po zmianie lidera i zasady ponownego łączenia uzgodnić przed implementacją; przedstawić układ UI.
  - Integracja z indywidualnym udziałem w walce i równoległymi sesjami: odłączenie od grupy nie oznacza opuszczenia walki. Wspólna widoczność drużyny pozostaje aktywna.
  - Zachować ciągły freeroam eksploracji; nawigację/pathfinding skoordynować z zależną kartą, bez zastępowania ruchu eksploracyjnego ruchem turowym po polach.
  - Statyczne UI i referencje w istniejących prefabach UIW, po zatwierdzeniu układu i zgody na serialized state. Bez budowania statycznego UI w runtime.
  - Acceptance: detach/attach, zmiana sterowanej postaci i grupy, wejście/wyjście z walki części drużyny, brak podwójnego inputu i duplikacji jednostek, uzgodnione zachowanie save/load, osobny smoke i UI pass.
  - Zatwierdzony UX: portrety grupy połączone linią; drag poza grupę tworzy grupę jednoosobową, do innej grupy dołącza, we własnej zmienia kolejność. Podgląd miejsca upuszczenia. LPM wybiera postać/sesję bez dodatkowej tury, dwuklik centruje kamerę. PPM: Odłącz / Dołącz do grupy… / Wycentruj / Inspektuj / Okno postaci (istniejące okno z inventory). Integracja z aktualnym railem eksploracji; grupy zachowane po walce i w save/load, stare save’y domyślnie jedna grupa.
  - Implementacja: grupy Party + GroupsVersion=1 w save/load, drag/drop i linie grup w istniejącym railu, LPM/dwuklik/PPM z osobnym inventory i Inspect, niezależne podążanie grup. Authoring prefabów UIW i referencji obu scen wykonany za zgodą użytkownika. Pełna regresja pierwotnej implementacji 1038/1038 PASS; retest grup 12/12 PASS. Funkcjonalność zaakceptowana przez użytkownika. Korekta spójności: jeden rail w eksploracji i walce, wspólny wariant portretu bojowego, HP/zasób, grupy/PPM/drag i uchwyt zwijania. Końcowe testy korekty 78/78 PASS, render i self-review PASS. DONE — 2026-09-15: smoke użytkownika PASS, również po korekcie warstw UI; zamknięcie, commit i push zatwierdzone. Szczegóły w UNITY_HANDOFF.md.

- `[P1] Przebudować pościg, utratę kontaktu i ucieczkę v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - DONE — 2026-09-15: smoke użytkownika PASS; zaakceptowano zamknięcie, commit i push. Wdrożone: kontakt 360° w walce z przesłonami i zasięgiem, kierunkowa pamięć pościgu, przerwanie trasy po odzyskaniu kontaktu, alarm pokoju bez kaskady oraz indywidualna ucieczka gracza po konfigurowanych pełnych turach przeciwników. Game Defines i poprawka okna konfiguracji w tym samym zakresie.
  - Pełna regresja 1026/1026 PASS; końcowy retest AI/widoczności/HUD 78/78 PASS; self-review PASS. Brak osobnego automatycznego wyjścia zaalarmowanego przeciwnika po 3 turach — użytkownik nie chce obecnie tej reguły. Szczegóły i historia w UNITY_HANDOFF.md.

- `[P1] Zintegrować Fog of War i pamięć odkrytej mapy z FOV v1` - `World / Dungeon / Exploration`, `UI / UX`
  - Rozdzielić wykrywanie przez jednostki od prezentacji wiedzy gracza. Stany mapy: nieodkryta, odkryta poza aktualnym FOV, aktualnie widoczna.
  - Ustalenie 2026-09-15: domyślnie oglądamy całe bieżące piętro lochu od najwyższej wysokości. Podesty są głównym zastosowaniem wysokości; balkony zostają do późniejszego dopracowania. Przekrój prezentacji ma odsłaniać aktualnie widoczny obszar pod podestem, także z widocznym przeciwnikiem poza otoczeniem lidera. PgUp/PgDown ani kamera nie ujawniają niewykrytych jednostek; strop nadal blokuje logiczny LOS. Obrys tylko pomocniczy. Integracja odsłaniania z FOV w tej karcie; obecny lokalny przekrój jest etapem przejściowym.
  - Wspólna wiedza drużyny zatwierdzona; widoczność 360° bez limitu odległości, tylko przesłony. Zatwierdzone: czarny nieodkryty teren i tło poza lochem, przyciemniona odkryta mapa, normalny obszar widoczny; ostatnio widziany stan obiektów; niewidoczni wrogowie z anonimowym wpisem kolejki, bez pozycji/statystyk; nie ujawniać ich przez HUD ani feedback.
  - Follow-up po smoke: rozjaśnienie pamięci do 55%, odkrywanie w promieniu 5 pól z własną LOS obserwatora (widzenie nadal bez limitu); optymalizacja przecięć LOS i pomijanie niezmienionej prezentacji. Regresja 1005/1005 PASS; ponowny smoke wydajności NOT RUN.
  - READY_FOR_SMOKE: implementacja modelu pamięci, save/load, renderingu i HUD. Tło kamery czarne; anonimowa kolejka „?” podpięta w prefabie. 136/136 testów kierunkowych oraz pełna regresja 1002/1002 PASS (fog-regression-final.xml), self-review PASS. Manualny smoke/PlayMode/profiler NOT RUN. Bez commit/push. Szczegóły i migracja starych save w UNITY_HANDOFF.md.
  - Commit `8723ca20` i push do istniejącego upstreamu wykonane.
  - DONE — smoke użytkownika PASS po końcowych poprawkach jasności, wydajności i promienia odkrywania. Użytkownik zaakceptował zachowanie widzenia bez limitu i polecił zamknięcie, commit oraz push. Regresja 1005/1005 PASS przed ostatnim doprecyzowaniem LOS odkrywania; końcowe testy Fog 15/15 PASS. Profiler produkcyjnej sceny NOT RUN.

- `[P1] Narastanie wykrycia i wskaźnik czujności v1` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`, `Design / Systems`
  - Osobna karta zaakceptowana przez użytkownika, realizowana przed ruchem/patrolami przeciwników jako kolejny fundament sneakowania.
  - Wejście w FOV rozpoczyna narastanie wykrycia; osiągnięcie progu uruchamia istniejący start/dołączanie do walki. Wskaźnik nad widocznym przeciwnikiem informuje o postępie. Nie zmieniać geometrii FOV ani pełnego podglądu Ctrl.
  - Zatwierdzone v1: jeden licznik na przeciwnika wobec całej drużyny; 2 s do progu, wygaszanie pełnego postępu przez 3 s bez LOS. Kilku widocznych członków nie przyspiesza narastania, zmiana celu nie resetuje historii. Już walczący wróg wykrywa natychmiast. Pasek żółty → czerwony nad widocznym wrogiem, tylko dla postępu > 0, niezależnie od Ctrl.
  - Rozwinąć istniejący koordynator i model widoczności, zachowując osobny runtime stanu czujności. UI zapisane w prefabie; kod aktualizuje postęp i widoczność. Przed UI przedstawić układ, przed serialized state sprawdzić aktualny stan Unity i zgodę.
  - Acceptance: wejście/wyjście ze stożka i utrata LOS, kilku obserwatorów/celów, rozdzielona drużyna, brak resetowania istniejącej walki/AV/AP, brak zdradzania ukrytych przeciwników przez wskaźnik, save/load i ręczny smoke sneakowania.
  - Stan: kod i save/load zaimplementowane; kompilacja oraz 112/112 EditMode PASS (awareness-final.xml), self-review PASS. Gotowy prefab PF_ExplorationCharacter podpięty po zgodzie użytkownika; import i retest czujności/prefabu 14/14 PASS (awareness-prefab.xml). DONE — zaakceptowane przez użytkownika, commit c9c1781e i push wykonane; po uwadze użytkownika pogrubiono pasek do 14 i dodano ciemne obramowanie 2. Retest 14/14 PASS (awareness-polish.xml); użytkownik zaakceptował kartę i polecił commit/push. Inaktywne piętro zachowuje postęp bez upływu czasu; stary save bez historii zaczyna od 0. Pamięć ostatniej pozycji, poszukiwanie, ucieczka i patrole nadal w swoich kartach.

- `[P1] Rozszerzyć przestrzeń walki na cały loch i dynamiczne uczestnictwo v1` - `Combat`, `Core / Architecture`
  - Bieżące piętro lochu ze wszystkimi wysokościami stanowi przestrzeń starcia; inne piętra pozostają oddzielne. Nie dołączają automatycznie wszyscy przeciwnicy ani drużyna.
  - Start i dołączanie po wykryciu dowolnej żywej postaci drużyny przez wroga, według jego FOV. Samo zobaczenie przeciwnika przez gracza nie rozpoczyna walki. Pozostali zachowują swobodną eksplorację.
  - Ustalenie użytkownika: dołączenie otrzymuje pełny odstęp Action Value według szybkości (co najmniej bazowy AV), bez natychmiastowej tury i resetowania zasobów. Nie ma wspólnych rund.
  - Sesje łączymy po zakończeniu wykonywanych akcji, zachowując HP, statusy, AP, pozostały AV i niedokończone tury. Automatyczne rozdzielanie, pamięć poszukiwania i ucieczka po utracie LOS pozostają kartą pościgu.
  - 2026-09-15: implementacja integracji i testy w workspace. Wspólni ownerzy mapy/widoków, kwalifikowane ID przeciwników, zachowane źródła łupu i stan encounterów. Pełna regresja EditMode 969/969 PASS. Ręczny smoke i profiler NOT RUN. Bez commit/push.
  - DONE — użytkownik zaakceptował smoke i polecił zamknięcie, commit oraz push. Pełna regresja przed końcowymi korektami 969/969 PASS; późniejsze retesty 70/70, 96/96 i finalny model FOV 15/15 PASS. PlayMode/profiler NOT RUN.
- `[P1] Wdrożyć FOV i wykrywanie wrogich jednostek v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - Wykrycie wrogiej jednostki w FOV rozpoczyna walkę albo powoduje dołączenie obserwatora do istniejącego starcia. Dotyczy dowolnej wrogiej postaci, niezależnie od aktywnego lidera.
  - Zastąpić model „pościg za liderem, start walki po dogonieniu”. Nie czekać na dojście przeciwnika do wykrytej jednostki.
  - Przed implementacją ustalić zakres/kąt FOV, przesłanianie, częstotliwość wykrywania i zasady współdzielenia informacji. Wykorzystać istniejący LOS oraz właściwości geometrii.
  - 2026-09-15 — etap modelu widoczności: zaakceptowane FOV wroga zależne od kierunku, domyślnie 120°/10 pól, osobna konfiguracja każdego wpisu przeciwnika. Widoczność gracza dookoła członków drużyny (360°), wspólna suma dla Fog of War. Na wyraźne polecenie użytkownika integracja startu/dołączania/łączenia sesji pozostaje następnej karcie. Model obliczeniowy nie zmienia jeszcze zachowania walk ani prezentacji Fog of War. Szczegóły i weryfikacja w UNITY_HANDOFF.md.
  - 2026-09-15 — kolejny etap: wdrożone wykrywanie zamiast pościgu do lidera, integracja dynamicznych sesji oraz podgląd FOV pod przytrzymanym Ctrl. Widoczność drużyny filtruje wyłącznie przeciwników: na polecenie użytkownika pod Ctrl pokazujemy pełne rzeczywiste FOV każdego widocznego wroga do sneakowania, bez przycinania pól widocznością drużyny ani odkryciem mapy. Wyświetlana wysokość nadal filtruje projekcję; nie renderuje jeszcze Fog of War. Kierunek patrzenia zachowany w save/load. Zatwierdzone: wspólna widoczność drużyny 360° bez limitu odległości, ograniczona przesłonami; podgląd Ctrl korzysta z tej reguły. FOV wroga nadal ma osobny konfigurowany zasięg i kąt. Pozostałe zasady integracji zatwierdzone. Zamknięte po smoke użytkownika: końcowy retest po zniesieniu limitu widoczności drużyny 70/70 PASS; wcześniejsza pełna regresja 969/969 PASS. Korekta po zgłoszeniu smoke: usunięty potwierdzony przeciek LOS przez narożniki ścian (float); reprodukcja 0/1 przed poprawką, retest 96/96 PASS po poprawce. Ponowny smoke pokoju ze screena wymagany. Weryfikacja i smoke w UNITY_HANDOFF.md.
  - DONE — użytkownik zaakceptował smoke i polecił zamknięcie, commit oraz push. Pełna regresja przed końcowymi korektami 969/969 PASS; późniejsze retesty 70/70, 96/96 i finalny model FOV 15/15 PASS. PlayMode/profiler NOT RUN.

- `[P1] Zintegrować wysokości, schody i balkony z modelem lochu i walki` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - DONE — 2026-09-15: smoke użytkownika zaakceptowany; zamknięcie, commit i push zlecone. Podesty jako podstawowe zastosowanie (bez dostępnej podłogi pod spodem), balkony jako dodatkowy model do późniejszego dopracowania. Wdrożone authoring, powierzchnie/schody, seamless combat/LOS, swobodna eksploracja, zapis zgodny wstecz i wspólny widok wysokości bez przyciemniania. Wizualny polish w acceptance/UI passie; FOV/Fog of War i pathfinding towarzyszy w osobnych kartach. Kompilacja ostatniej korekty jasności PASS. Wcześniejsze testy: pełne 946/946 PASS, retesty opisane w UNITY_HANDOFF.md; finalny retest projekcji 11/11 PASS, następna korekta jasności sprawdzona kompilacją. Profiler i PlayMode NOT RUN.


- `[P2] Określić zasady kontrolowanego RNG dla walki i nagród` - `Design / Systems`, `Combat`, `Items / Inventory / Economy`, `Core / Architecture`
  - DONE — 2026-09-14, smoke użytkownika PASS i zamknięcie zaakceptowane. Zatwierdzono automatyczną pulę progów k=2; projektant podaje szansę, rozmiar obliczany przy odnowieniu. Priorytet średniej przed ograniczaniem serii. Wdrożenie tylko opcjonalnej zawartości lochu, zachowane klucze i historia całego lochu.
  - PASS: 20/20 testów NUnitLite na produkcyjnych źródłach (.NET9), kompilacja Core, self-review. Testy Unity nowej zmiany NOT RUN; nie utożsamiać z wcześniejszymi 87/87. Bez rerollu zapisanych układów. Ograniczenia adaptacyjnych buffów udokumentowane; nie zatwierdzono uniwersalnej migracji ani wszystkich zasad historii walki.
  - Migracje innych mechanik po pierwszym content passie — osobne karty poniżej; decyzje o właścicielu historii, resetach, checkpointach i tolerancji błędów rozstrzygnąć przed każdą migracją.


- `[P1] Wprowadzić kontrolowany rozkład opcjonalnej zawartości lochu v1` - `World / Dungeon / Exploration`, `Design / Systems`, `Content / Data`
  - DONE — wariant ciągły z odświeżaniem fazy 50%, kluczami autora i historią całego lochu. Wspólny mechanizm w Core, integracja tylko opcjonalnej zawartości. Zatwierdzono współdzielenie kluczy wyłącznie między niezależnymi okazjami; wdrożono walidację zależności, konkurencji o miejsce i cykli wpływu między kluczami/szablonami. Kompilacja PASS, końcowy EditMode 87/87 PASS (`../.codex/continuous-rng-final.xml`), self-review PASS. Smoke użytkownika PASS (generacja grupy i save/load). Commit `6dea14b1`, push na istniejący branch zakończony.
  - Zachować docelową średnią konfiguracji i ograniczyć frustrujące serie losowań. Zatwierdzono model zachowujący średnią, nie pity timer podnoszący częstość nagród. Analogia puli jest opisem efektu, nie narzuconą implementacją.
  - Liczyć kwalifikujące się okazje generacji, np. pokoje z odpowiednim miejscem na opcjonalną skrzynię. Stałe wyposażenie nie podlega temu losowaniu. Respektować dokładną wartość konfiguracji: 33% nie oznacza automatycznie 1/3.
  - Nie obiecywać jednocześnie dokładnej średniej i gwarancji sukcesu po dwóch porażkach. Zatwierdzony zakres historii: cały loch, reset dla nowego lochu; parametr odświeżania fazy 50%. Bez gwarantowanej maksymalnej długości serii ani dokładnej liczby sukcesów w każdym lochu.
  - Oddzielić rozkłady różnych kategorii; zachować deterministyczność seedu i zapisanych wyników, nie przelosowywać zawartości przy wczytaniu. Sprawdzić zależności grup, brak kwalifikujących się miejsc oraz zmianę konfiguracji.
  - Weryfikacja: powtarzalne testy wielu seedów, zgodność średniej z konfiguracją, rozkład serii i brak naruszenia kompletności pokojów. Nie dodawać nowych typów nagród tylko na potrzeby mechanizmu.

- `[P1] Uporządkować konfigurację i kompletność zawartości pokojów v1` - `World / Dungeon / Exploration`, `Content / Data`, `Tools / Editor / Debug`
  - 2026-09-14: DONE — smoke i zamknięcie zaakceptowane przez użytkownika; commit/push zlecone. Zatwierdzono jednostronne RequiresAnchorId, stałe wyposażenie we wszystkich typach pokojów oraz pomijanie niewykonalnej opcjonalnej grupy w całości przy błędzie brakującej definicji. Nowe grupy nie zmieniają danych rekonstrukcji legacy; konfiguracja assetów i smoke w UNITY_HANDOFF.md. Nowe testy: 26/26 PASS; regresja kopii testowej: 253 PASS, 1 FAIL starej sceny HUD (szczegóły w handoffie).
  - Dopracowanie Dungeon Room Editora: grupy i referencje w oknie, oddzielne zaznaczanie/stawianie, Ctrl+klik, zoom/pan, trzy panele, walidacja puli, Undo/Redo. Weryfikacja na właściwym projekcie: 70/70 PASS, w tym wcześniejszy test HUD. Ręczny smoke zaakceptowany przez użytkownika; karta zamknięta.
  - Zastąpić wspólny MinObjectCountPerFloor/MaxObjectCountPerFloor modelem oddzielającym stałe wyposażenie pokoju od opcjonalnej zawartości. Limit piętra nie może usuwać filarów, sarkofagów ani elementów zaprojektowanej zagadki.
  - Szablon definiuje gwarantowane elementy i opcjonalne elementy/grupy. Powiązane elementy mają zachowywać kompletność. Nie zastępować wspólnego limitu kolejnymi min/max per floor; dla opcjonalnej zawartości stosować konfigurowany udział/szansę.
  - Sprawdzić zależności kotew, wymagane elementy progresji, konflikty miejsc oraz pule definicji. Brak miejsca lub definicji dla wymaganego elementu ma dawać konkretny błąd walidacji, zamiast cichego pominięcia.
  - Zachować istniejące zapisane układy; migrację definicji i zachowanie starych pól ustalić technicznie przed zmianami serialized state. Testować kompletność pokojów niezależnie od liczby pokojów na piętrze.

  - Końcowa poprawka rysowania siatki: kompilacja PASS, zaakceptowana wraz z kartą; 70/70 testów wykonano przed tą poprawką prezentacji.

- `[P1] Combat v4 - acceptance, code review i UI pass` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`, `Tools / Editor / Debug`
  - Acceptance całego milestone'u: wiele kolejnych walk bez przeładowania sceny, wejście/wyjście, zwycięstwo/ucieczka/porażka, geometria, targeting, przemieszczenia, pułapki, AI, persistence i kamera.
  - Wykonać szerszy code quality review, regresję oraz osobny wizualny UI acceptance. Kontrolować podwójne subskrypcje/input, duplikację obiektów, utratę stanu i sprzątanie lifecycle.
  - Raport rozróżnia PASS/FAIL/NOT RUN. Milestone zamknąć dopiero po smoke użytkownika; commit/push wymagają osobnego polecenia.
  - DONE: smoke i zamknięcie zaakceptowane przez użytkownika. Pełny review milestone'u zakończony; poprawiono pułapki przeciwników przed walką, dopłaty honoru/overflow i komunikat ruchu. Build PASS; wcześniejsza regresja EditMode 805/805 PASS; wykonanie nowych testów po review NOT RUN (Unity otwarte). Raport: `COMBAT_V4_ACCEPTANCE.md`.

- `[P1] Przygotować content i scenariusze taktyczne Combat v4` - `Combat`, `Content / Data`, `Tools / Editor / Debug`
  - Przygotować grywalne układy: korytarz, sala z filarami, nieregularny pokój, duża arena oraz przejścia między przestrzeniami zgodnie z zatwierdzonym zakresem areny.
  - Włączyć przeszkody i pierwsze zatwierdzone pułapki do rzeczywistego contentu, nie tylko sandboxa. Zapewnić powtarzalne wejście do scenariuszy przez istniejące narzędzia debug/testowe.
  - Walidować przejścia, miejsca dla uczestników i brak niezamierzonych softlocków; liczby i efekty ustalać na podstawie danych projektu lub z użytkownikiem.
  - Zatwierdzone: tylko katakumby; pięć układów także w normalnej puli lochów, obecni przeciwnicy i balans. Implementacja: kotwy z opcjonalną konkretną definicją; wspólne szablony produkcyjne/debugowe. DONE: smoke użytkownika PASS; zaakceptowano drugi pass rozmieszczenia. Dalsze ręczne dopracowanie przy pierwszym content passie. Build PASS, regresja 55/55 PASS, testy contentu 9/9 PASS. Raport: `COMBAT_V4_CONTENT_VERIFICATION.md`.

- `[P1] Domknąć prezentację areny i informacje taktyczne v1` - `Combat`, `UI / UX`, `Content / Data`
  - Docelowy pixel-art pass highlightów: zachować zaakceptowaną czytelność pojedynczych kwadratów, granicy zasięgu, ścieżki i gradientu kosztów; dopasować kontury oraz poświatę do pixel artu. Obecny styl zaakceptowany jako rozwiązanie tymczasowe.
  - Dodać stały podgląd aktywnych statusów poza walką (efekt i pozostały czas/ticki), spójny z prezentacją bojową. Odłożone z karty pułapek za zgodą użytkownika; komunikaty floating nie zastępują podglądu. Przed implementacją uzgodnić układ UI.
  - Czytelne granice walki, ściany, przeszkody i stany pułapek; tooltipy, wskazanie blokady ruchu/celowania oraz feedback aktywacji.
  - Naprawić zgłoszoną regresję Combat InspectWindow: biały panel i bardzo jasny tekst powodują brak kontrastu. Zweryfikować faktyczną przyczynę w prefabie/stylowaniu.
  - W Combat v4 wykonać przegląd tego okna i dostosować do istniejących prefabów/stylów UIW: kontrast, hierarchia statystyk, czytelność pasków, spójność języka, odstępy oraz sekcje efektów/umiejętności. Przed zmianami przedstawić układ użytkownikowi; static UI w prefabie/scenie. Regresja domknięta w Combat v4.
  - Wspólne widoki lochu i walki, bez dublowania geometrii. Statyczne hierarchy, layout, styl i referencje zapisać w prefabach/scenach; kod aktualizuje dane i dynamiczne stany.
  - Przed nowym UI przedstawić układ użytkownikowi. Sprawdzić czytelność dużej areny, zasłanianie jednostek i długie teksty.
  - DONE: wdrożone statusy eksploracyjne, informacje taktyczne, Inspect i wspólny HUD UIW, podgląd AP, rail z liczbami, ikonowe akcje, siatka skilli 2×5 z przewijaniem, wskazanie kolejki i poprawka tooltipa.
  - Smoke użytkownika PASS (2026-09-14), zamknięcie zaakceptowane. Build PASS; ostatni wykonany zestaw EditMode 18/18 PASS. Test regresyjny ostatniej poprawki tooltipa skompilowany; wykonanie w Unity NOT RUN przy otwartym Editorze. Self-review PASS. Raport: `COMBAT_V4_PRESENTATION_VERIFICATION.md`.

- `[P1] Dodać sterowanie kamerą większych aren v1` - `Combat`, `UI / UX`
  - Rozwinąć obecne automatyczne kadrowanie o przesuwanie, zoom, ograniczenie kadru, skupienie na jednostce i powrót do czytelnego widoku.
  - Zachować ciągłość kamery przy przełączeniu trybu; bez wymuszonego ekranu przejścia. Uzgodnić gesty/klawisze i zasady automatycznego śledzenia.
  - Input kamery nie może kolidować z UI, wyborem celu ani ruchem; uwzględnić zasłanianie areny przez HUD i różne proporcje ekranu.
  - Zatwierdzone: MMB drag, WASD/strzałki, kółko zoom, F na jednostkę mającą turę, Home na arenę. Przesuwanie po całym piętrze; oglądanie innych walk nie zmienia aktywnej sesji. Automatyczne przesunięcie tylko przy rozpoczęciu tury poza widokiem, bez śledzenia kroków i bez zmiany zoomu. Kadr zapamiętywany osobno dla sesji. Zoom ma stały zakres 50–200% (100% = size 5), również Home respektuje granice 2.5–10; poprzednie testy limitów 12/12 PASS; zakres 50–200% zaakceptowany w smoke.
  - DONE: konfiguracja zapisana w DungeonSandbox; 58/58 EditMode PASS, build PASS, self-review PASS. Smoke użytkownika PASS. Raport: `COMBAT_V4_CAMERA_VERIFICATION.md`.

- `[P1] Zapewnić trwałość stanu obiektów areny v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Zachować tożsamość i stan drzwi, przeszkód oraz pułapek między eksploracją, walką, ucieczką i kolejnym encounterem. Rozszerzyć istniejący mechanizm persistence zamiast tworzyć niezależne kopie stanu.
  - Obsłużyć zapis/odczyt w ramach aktualnie wspieranych momentów zapisu i kompatybilność starszych save'ów. Nie rozszerzać automatycznie zakresu o zapis w środku walki.
  - Testy obejmują zużytą pułapkę, otwarte drzwi, stan przeciwników i brak resetu po powrocie do eksploracji.
  - Uwzględnić aktualizacje puli contentu: obecne odtwarzanie lochu regeneruje obiekty z seeda i bieżących definicji. Nowe sarkofagi/gruz/filary są na razie w osobnym demo; przełączenie produkcyjnej puli wymaga zachowania tożsamości i układu obiektów istniejących zapisów.
  - DONE: snapshot wygenerowanej geometrii i tożsamości obiektów, dotychczasowe delty stanu, zachowanie przygotowanych nagród. Starszy zapis odtwarza loch dotychczasowo i migruje przy następnym zapisie (zatwierdzone przez użytkownika). Automatyczne testy 82/82 PASS, build PASS, self-review PASS. Smoke użytkownika PASS. Raport: `COMBAT_V4_PERSISTENCE_VERIFICATION.md`.

- `[P1] Teren taktyczny i koszty przemieszczania v1` - `Combat`, `Content / Data`, `Design / Systems`
  - Combat v4, przed grywalnym contentem i acceptance: typ podłoża, dodatkowy koszt ruchu, czytelny podgląd AP i integracja z AI.
  - Zatwierdzone: trudny teren +1 AP za wejście na pole, sumowane z +2 AP murka. Nie zmienia licznika zwykłych kroków. Dash/charge/push/pull/teleport bez dopłaty AP podłoża.
  - Eksploracja: trudny teren 50% prędkości drużyny i przeciwników; niski murek — jednostajne przejście 0,6 s z blokadą sterowania ruchem. W eksploracji postacie nie rezerwują pól; obecność towarzysza nie blokuje przejścia przez murek ani natychmiastowego powrotu.
  - Ścieżka gracza: bezpieczeństwo → koszt AP → najmniej zakrętów; gdy brak bezpiecznej drogi, można wybrać nawet śmiertelną z ostrzeżeniem. AI: bezpieczeństwo → AP; odrzuca przewidywane powalenie.
  - Czytelność: odmienne wypełnienie trudnego terenu i kolor kosztownych odcinków preview; pułapki zachowują czerwone ostrzeżenie.
  - DONE — smoke użytkownika PASS: demo dungeon.catacombs.terrain_demo. Produkcyjny content bez zmian.
  - Unikalne przeszkody i efekty rozwijać wspólnie z istniejącymi kartami przeszkód/pułapek; nie dodawać w tej karcie rozbudowanego systemu reakcji środowiskowych.
  - Końcowy styl: delikatna siatka pojedynczych kwadratów, gradient żółty → brązowo-rudy, ścieżka nad pułapkami. Docelowy pixel-art pass odłożony do karty prezentacji Combat v4.
  - Testy: regresja 108/108 PASS, uzupełnienie terenu 8/8 PASS; końcowy retest terenu/seamless 57/57 PASS.

- `[P1] Dodać pułapki bojowe i integrację z ruchem v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Wykorzystać istniejący model pułapek eksploracyjnych tam, gdzie pasuje; dodać brakujące przeniesienie do runtime walki, stan aktywacji i wykonanie efektów.
  - Przed implementacją uzgodnić wyzwalanie przez ruch, przemieszczenia wymuszone i teleport, podatne strony, widoczność, jednorazowość/odnawianie oraz pierwsze efekty.
  - Preview i wykonanie nie mogą uruchamiać pułapki podwójnie; walidacja i podgląd nie mutują jej stanu. Wspierać przerwanie ruchu przez skutki pułapki.
  - Zatwierdzone: kolce (fizyczne + krwawienie), jednorazowy gaz na wskazanych polach (zatrucie), pieczęć grobowa (magiczne + osłabienie). Pule według motywu lochu; współdzielona mechanika i warianty wyglądu. Pierwszy content: katakumby.
  - Wyzwalanie: zwykły ruch/dash/charge/push/pull na przekroczonych polach, teleport wyłącznie na celu; obie strony. Jednorazowe grupy lokalne dla pokoju, bez trwałej chmury. Same obrażenia nie zatrzymują ruchu, utrata możliwości działania zatrzymuje.
  - Skalowanie poziomem lochu, standardowa obrona i odporności. AI preferuje bezpieczne obejście; przy braku bezpiecznej drogi wybiera najmniejsze zagrożenie i odrzuca przewidywane powalenie.
  - Zatwierdzone: poza walką tick statusów co 6 sekund aktywnej gry; w walce według tur jednostki, bez resetowania efektu przy zmianie trybu. Osłabienie: −20% zadawanych obrażeń przez 4 tury. Pułapki bez rzutu trafienia/uniku/krytyka, z normalną obroną i odpornościami. Minigra rozbrajania poza zakresem.
  - Wzrost obrażeń: 6,5% bazy × skumulowany wzrost schodkowy postaci.
  - Zatwierdzony balans poziomu 1: kolce 30 fizycznych + krwawienie 10 × 3; gaz zatrucie 12 × 5; pieczęć 40 magicznych + osłabienie na 4 tury.
  - DONE: smoke użytkownika PASS; testy 105/105 PASS, kompilacja poprawki feedbacku PASS; demo `dungeon.catacombs.traps_demo`, seed 4205. Produkcyjna pula pozostaje bez zmian do karty persistence.

- `[P1] Rozwinąć przeszkody taktyczne areny v1` - `Combat`, `Content / Data`, `Design / Systems`
  - Rozwinąć istniejące przeszkody z niezależnym blokowaniem ruchu i linii widzenia/efektu; nie tworzyć drugiego modelu tych samych właściwości.
  - Zweryfikować zwykły ruch, dash/charge, push/pull, teleport i kolizje według istniejących zasad. Dodać reprezentatywne warianty contentu oraz walidację danych.
  - Niszczenie obiektów nie jest jeszcze zatwierdzoną mechaniką: uzgodnić zakres przed dodaniem HP, obrażeń lub nagród obiektów.
  - Zatwierdzone: sarkofag i duży gruz blokują ruch, przepuszczają LOS; filar blokuje oba. Każdy obiekt zajmuje jedno pole. Bez niszczenia.
  - Status: DONE — 96/96 EditMode PASS, zapis prefabów/referencji i demo seed 4205 PASS, self-review PASS; smoke użytkownika PASS — funkcjonalność zaakceptowana.
  - Tymczasowe grafiki zaakceptowane; użytkownik zastąpi je docelowymi sprite’ami w przyszłości.

- `[P1] Dodać niskie ściany i balustrady jako krawędzie pokoju v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Rozwinąć model rzeczywistych krawędzi pokoju: pełna ściana, niski murek, balustrada. Nie zastępować ich semantic anchors ani wolnostojącymi przeszkodami na polach.
  - Rozdzielić blokowanie ruchu i LOS/efektu na krawędziach; używać tych samych właściwości w eksploracji, walce i prezentacji. Balustrada blokuje ruch i przepuszcza strzały.
  - Zachować płaską logikę podłogi Combat v4. Dostępne balkony, schody i różne wysokości należą do Dungeon Exploration v3.
  - Weryfikacja: krawędź zewnętrzna i wewnętrzna, narożniki, pokój O/U, brak przejścia przez balustradę, zgodność widoku i LOS. Wykonać przed kartą przeszkód taktycznych.
  - Zatwierdzone: niski murek przepuszcza zwykły ruch za koszt kroku +2 AP (trzeci krok = 5 AP); eksploracja bez opłaty AP. Oba typy przepuszczają LOS i blokują dash/charge/push/pull; teleport zachowuje istniejące zasady.
  - Brak podłogi za krawędzią wyklucza przejście; wizualnie może oznaczać szyb, przepaść lub niedostępną niższą kondygnację. Generator nie zastępuje zadeklarowanych murków/balustrad drzwiami.
  - Status: DONE — 119/119 EditMode PASS, prefaby i referencje zapisane, walidacja i self-review PASS; smoke użytkownika PASS — zaakceptowany przez użytkownika.

- `[P1] Ustalić i wdrożyć pozycje oraz formację przy wejściu i wyjściu z walki v1` - `Combat`, `World / Dungeon / Exploration`, `Design / Systems`
  - Seamless już przenosi pozycje eksploracyjne; rozstawianie na przeciwnych krawędziach pozostaje w starszej ścieżce/sandboxie. Domknąć istniejącego resolvera bez nowego szyku.
  - Uzgodnić snap do siatki, przypadki kolizji, brak miejsca, pozycje przeciwników i powrót po ucieczce. Rozwiązywanie konfliktów ma być deterministyczne i nie umieszczać jednostek w ścianach ani na zajętych polach.
  - Zachować pozycje po zwycięstwie i obsłużyć kolejne walki w tej samej scenie bez duplikowania jednostek.
  - Zatwierdzone: snap do środka zajmowanego pola, jednakowo dla obu stron; bez ustawiania nowej formacji. Zachować końcowe pozycje po zwycięstwie i miejsce przekroczenia granicy przy wyjściu.
  - Rezerwować najpierw wszystkie poprawne pozycje, potem rozwiązywać konflikty deterministycznie na najbliższym wolnym polu dostępnym bez przekraczania ścian/zamkniętych drzwi.
  - Brak miejsca to zabezpieczenie błędnego układu: atomowo odrzucić start bez zmiany pozycji; przy dołączeniu pozostawić postać przed wejściem i utrzymać walkę. Komunikat dla gracza.
  - Status: DONE — 47/47 EditMode PASS; prefab komunikatu UIW zapisany i podpięty, weryfikacja zapisu i self-review PASS. Manualny smoke PASS — zaakceptowany przez użytkownika.

- `[P1] Combat v4 — nawigacja przeciwników wokół przeszkód i obsługa drzwi v1` - `Combat`, `Core / Architecture`
  - Karta zatwierdzona przez użytkownika; wykonać po domknięciu granic i geometrii areny.
  - Rozwinąć istniejące AI i mechanizmy ruchu: planować pełną trasę ponad budżet jednej tury, a wykonywać odcinek za dostępne AP. Obsłużyć obejście wymagające chwilowego oddalenia się od celu.
  - Uwzględnić otwieranie dozwolonych drzwi przez istniejący system interakcji oraz koszt AP. Respektować blokady i zatwierdzone granice sesji; nie dodawać pościgu ani pathfindingu eksploracyjnego.
  - Przeliczać trasę po zmianie przeszkód, stanu drzwi lub zajętości pól. Brak dostępnej drogi nie może powodować zapętlenia tury ani powtarzania bezskutecznych akcji.
  - Weryfikacja: obejście ściany w wielu turach, początkowe oddalenie od celu, wąskie przejście, zajęte pola, drzwi otwarte/zamknięte/niedostępne oraz cel nieosiągalny. Sprawdzić rzeczywistą ścieżkę, koszty AP i poprawne zakończenie tury.
  - DONE — implementacja i self-review zakończone; 65/65 EditMode PASS, smoke użytkownika PASS. Commit i push zatwierdzone.

- `[P1] Domknąć granice i geometrię areny w lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Wykorzystać rzeczywisty footprint lochu, ściany zewnętrzne i wewnętrzne, drzwi i przejścia. Zapewnić spójność widocznych granic z ruchem, celowaniem i linią efektu.
  - Zatwierdzone: arena obejmuje jeden pokój i nie rozszerza się; indywidualne wejście/wyjście przez granicę, istniejące równoległe sesje. Każde opuszczenie pokoju, także przejściem bez drzwi, jest wyjściem. Linia strzału może przechodzić poza pokojem; blokuje ją rzeczywista geometria i właściwości obiektów.
  - Acceptance obejmuje wąski korytarz, nieregularny pokój, filar i przejście między przestrzeniami.
  - DONE — smoke użytkownika PASS: kontekst zbiera także ściany graniczne; LOS sprawdza ściany oraz żywy stan geometrii poza areną bez dodawania tam pól walki. Wyjście respektuje również przeszkody ruchowe za progiem.
  - PASS: 56/56 EditMode (seamless, LOS, grid, integracja, AI). PASS: manualny smoke scenariuszy geometrii zaakceptowany przez użytkownika. Bez zmian serialized state i commit/push zatwierdzone przez użytkownika.

- `[P1] Wdrożyć reperkusje porażki i odzyskiwanie towarzyszy v1` - `Combat`, `Core / Architecture`, `NPC / Quest / Dialogue`
  - Status: DONE — smoke funkcjonalny i UI zaakceptowane przez użytkownika.
  - Porażka kampanii po śmierci głównej postaci albo braku żywych aktywnych członków: najnowszy poprawny zapis tej kampanii / inny zapis / menu. Brak zapisów blokuje oba przyciski wczytywania. Żywy członek poza pokojem nadal może ratować powalonych.
  - Lokalna przegrana pokoju zachowuje stan lochu; martwi towarzysze trafiają do odzyskiwania u kapłana. StoryLocked pozostaje zablokowany.
  - Odzyskanie: ceil(300 * 4^((level-1)/10)), pełne HP i zasoby, zachowanie tożsamości/ekwipunku/progresji, aktywny slot lub rezerwa. Tylko honor rozmówcy; bez lojalności kapłana.
  - Nowe złoto przeciwnika: baza ceil(10 * 4^((dungeonLevel-1)/10)), niezależny równomierny rozrzut 50–150% na przeciwnika; wcześniej wylosowane nagrody w zapisach pozostają bez zmian.
  - Dziewięć zatwierdzonych tierów honoru bez ograniczenia do −100…100. Progi i zniżki w Game Defines; zniżki domyślnie 0% do balansu, tier −75…−50: Niechlubny.
  - UI zapisane w scenie/prefabach za zgodą użytkownika. Regresja EditMode 89/89 PASS. Manualny smoke i wizualny UI pass: PASS użytkownika. Końcowe testy odzyskiwania 43/43 PASS, konsoli 51/51 PASS; kolejne poprawki UI skompilowane i zweryfikowane.

- `[P1] Zintegrować walkę seamless w scenie lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture` - `DONE`
  - Zastąpić produkcyjne przełączanie scen eksploracja/walka przełączaniem trybu na istniejącej mapie i widokach jednostek. Nie budować drugiej kopii mapy ani równolegle aktywnych kontrolerów tych samych postaci.
  - Rozdzielić lifecycle eksploracji i walki: zatrzymanie eksploracyjnego sterowania, start tur, podpięcie HUD/input, zakończenie walki i przywrócenie eksploracji. Uwzględnić zwycięstwo, ucieczkę, porażkę i nieudany start bez pozostawienia częściowego stanu.
  - Zachować sandbox/debug walki oraz istniejące systemy combat; nie przepisywać core combat bez potrzeby. Przed implementacją uzgodnić konieczne zasady pozycji i granic, od których zależy integracja.
  - Zatwierdzone rozszerzenie: wiele równoległych sesji przypisanych do pokojów, indywidualne uczestnictwo i przełączanie sterowania między walką a eksploracją. Osoby poza pokojem dołączają po wejściu; wyjście za próg za zwykły koszt ruchu zastępuje losową ucieczkę i może rozdzielić drużynę.
  - Snap do środka komórki; deterministyczny wybór dostępnego pola bez przekraczania ścian/zamkniętych drzwi; nieudany start nie pozostawia aktywnej sesji.
  - Dołączenie czeka pełny odstęp Action Value według szybkości (bez wprowadzania wspólnych rund). Ponowne wejście nie odnawia zasobów ani nie skraca oczekiwania.
  - Powaleni nie kończą automatycznie walki, gdy żywy członek drużyny poza pokojem może przyjść na ratunek. Reperkusje porażki są osobną następną kartą.
  - Smoke użytkownika zaakceptowany. Regresja 71/71 PASS; końcowy zestaw po poprawkach widoczności i kotwiczenia napisów 22/22 PASS. Karta zamknięta na polecenie użytkownika.

- `[P1] City Progression and Specialist Services v1 - UI acceptance pass` - `UI / UX`, `Bug` - `DONE`
  - Poprawki na podstawie dziewieciu screenow: podglady, materialy alchemii, lokalizacja, stany puste i zaznaczenia, prog gniazda przy 20% ulepszenia.
  - Statyczny layout zapisany w prefabie; bez runtime builderow. Smoke zaakceptowany przez uzytkownika; 53/53 testy PASS i koncowy retest prefabu 7/7 PASS.

- `[P1] City Progression and Specialist Services v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Combat`, `UI / UX`, `Tools / Editor / Debug` - `DONE`
  - Domknieto acceptance i poprawki po code review: ksiegowanie oplat, buyback, walidacja wlasnosci, bonus trafienia, zachowanie postepu ulepszania i statyczne UI w prefabie.
  - Testy regresyjne 189/189 PASS; koncowy retest 29/29 PASS. Smoke zaakceptowany przez uzytkownika. Raport: CITY_SERVICES_ACCEPTANCE.md.

- `[P1] Dodac trwala lojalnosc NPC i postep wydatkow v1` - `NPC / Quest / Dialogue`, `Core / Architecture` - `DONE`
  - Dodac osobny, zapisywalny stan lojalnosci oraz skumulowanych wydatkow dla kazdego NPC. Zdefiniowac dane progresji i przygotowac je pod pozniejsze progi odblokowan oraz rabatow.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.
- `[P1] Dodac progi lojalnosci, odblokowania uslug i rabaty v1` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX` - `DONE`
  - Dodac data-driven progi lojalnosci, odblokowywanie uslug i rabaty. UI ma pokazywac dostepnosc uslugi oraz czytelny powod blokady.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.
- `[P1] Dodac ekonomie NPC i wyplaty gildii v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Core / Architecture` - `DONE`
  - Dodac osobny, trwaly portfel golda dla kazdego NPC bez maksymalnego limitu. NPC otrzymuje skonfigurowana wyplate po okreslonej liczbie w pelni ukonczonych lochow; kwota skaluje sie z poziomem kampanii (najwyzszym poziomem aktywnej druzyny). Zakupy zwiekszaja portfel NPC, sprzedaz przy niewystarczajacym saldzie wymaga potwierdzenia i wyplaca saldo NPC, a saldo i postep do wyplaty sa zapisywane. UI handlu pokazuje saldo i powod odrzucenia transakcji.
  - Smoke zaakceptowany przez uzytkownika 2026-09-13.

- `[P2] Dodac wyciaganie galdurytow/run z broni i pancerzy v1` - `Items / Inventory / Economy`, `Combat`, `Core / Architecture` - `DONE`
  - Osobna zakladka Zaklinacza. Szansa 70%, od lojalnosci 6: 80%. Kazda proba kosztuje 120% wartosci galdurytu; zniszczony kosztuje 180%, bez kumulacji doplat. Niepowodzenie pozostawia zniszczony galduryt bez efektow w sockecie. Powodzenie zwraca sprawny galduryt do wlasciciela/magazynu albo calkowicie usuwa zniszczony. Stan zapisuje sie w save/load.
  - Poprawiony layout osadzania i wyciagania: zawijane ostrzezenie, osobna szansa przy koszcie, wysokosc tytulu zaleznа od tekstu i brak miejsca rezerwowanego przez pusty status. Testy EditMode przeszly; smoke zaakceptowany przez uzytkownika 2026-09-13.

- `[P2] Dodac socketowanie galdurytow/run w bronie i pancerze v1` - `Items / Inventory / Economy`, `Combat`, `Core / Architecture` - `DONE`
  - Osobna zakladka Zaklinacza: wybor wyposazenia i galdurytu, lista zajetych i wolnych socketow, koszt i osadzanie. Walidacja celu, poziomu, rzadkosci, identyfikacji i pojemnosci; koszt konfigurowany jako procent wartosci galdurytu; efekty oraz save/load osadzonych instancji.
  - Poprawione prefaby, interakcje, tooltipy wyposazenia i autouzupelnianie nazwanych argumentow konsoli. Smoke zaakceptowany przez uzytkownika; kompilacja przeszla. Dodane testy konsoli wymagaja uruchomienia EditMode. Mieszanie tierow czesci pozostaje do przyszlej decyzji.

### Milestone: City Progression and Specialist Services v1

- `[P2] Dodac crafting bizuterii v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX` - `DONE`
  - Osobny model craftingu biżuterii oparty o metal + klejnot, bez automatycznego modelu trzech części equipmentu i bez socketów na galduryty. Ujednolicono preview, nazewnictwo, tooltipy, materiały oraz save/load.
- `[P2] Dodac crafting i identyfikowanie galdurytow/run v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data` - `DONE`
  - Galduryty/runy jako wspólny system: Enchanter tworzy i identyfikuje galduryty, a niezidentyfikowany i zidentyfikowany stan jest zapisywany. Dodano materiały, tiering, losowanie efektów, tooltipy oraz komendę debugową.

- `[P1] Dodac role Alchemika, crafting i napełnianie potionow v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data` - `DONE`
  - Data-driven crafting potionow i bomb, materialy Alchemika, alkohole/prochy/katalizator, crafting materialow i napełnianie consumables.
  - Panel zostal ujednolicony z UI Kowala; tooltipy, sloty materialow i refille przechodza przez wspolne reguly UI Workspace.
  - Zweryfikowano testy EditMode dla craftingu i napełniania.
- `[P1] Dodac Kapłana z darmowym leczeniem drużyny v1` - `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX` - `DONE`
  - Dodano role i definicję Kapłana oraz dialog z opcją darmowego pełnego leczenia aktywnej drużyny.
  - Leczenie jest jawną akcją dialogową `HealActiveParty`; nie tworzy osobnej zakładki usług i nie wskrzesza powalonych postaci.
  - Zweryfikowano wybór dialogowy testem EditMode.

### Milestone: Combat Passives and Affixes v1

- `[P1] Combat Passives and Affixes v1 - acceptance pass` - `Combat`, `Items / Inventory / Economy`, `Skills / Progression`, `Tools / Editor / Debug` - `DONE`
  - Zweryfikowac warunki, stackowanie, tooltipy, save/load oraz wspoldzialanie pasywek z drzewek, bizuterii i unikatowego equipmentu.

- `[P1] Dodac pierwszy pass wykonawczy pasywek node'ow drzewek skilli v1` - `Combat`, `Skills / Progression`, `Content / Data` - `READY_FOR_SMOKE`
  - Podpiac node'y Scouta do wspolnego wykonawczego frameworku: materializacja rank runtime, warunki i triggery combatowe, modyfikatory obrazen i szansy krytycznej, zamiana chybienia w trafienie oraz aplikowanie statusu. Dodac czytelne metadane warunku i aktywacji w tooltipie node'a.
- `[P1] Dodac tagi przeciwnikow i warunkowe modyfikatory v1` - `Combat`, `Content / Data`, `Core / Architecture` - `READY_FOR_SMOKE`
  - Dodac tagi `Human`, `Beast`, `Undead` i `Demon` na definicjach przeciwnikow oraz warunki pasywek dla typu celu i theme lochu. Uzyc ich m.in. dla obrazen przeciwko okreslonym przeciwnikom i bonusow terenowych klejnotow.
- `[P1] Zbudowac rdzen wspolnego frameworku pasywek v1` - `Combat`, `Content / Data`, `Core / Architecture`, `UI / UX`
  - Zdefiniowac wspolny model static definition i runtime state dla pasywek drzewek skilli, equipmentu oraz przyszlych klejnotow: zrodlo, warunek, trigger, target, efekt, stacking, lifetime, tooltip i persistence. Zapewnic rozszerzalne kontrakty dla zlozonych warunkow i efektow bez dodawania nowego contentu combatowego. Zaimplementowano; kompilacja i pelny EditMode suite przechodza.
- `[P1] Zmigrowac dzialajace pasywki do wspolnego frameworku v1` - `Combat`, `Items / Inventory / Economy`, `Skills / Progression`, `Core / Architecture`
  - Przeniesc do wspolnego modelu istniejace dzialajace globalne pasywki drzewek, modyfikatory statystyk i odpornosci equipmentu, pasywki bizuterii dotyczace XP, zlota i theme lochu oraz obecne runtime modifiers. Zachowac dotychczasowe zachowanie, kompatybilnosc runtime item instances i save/load; nie dodawac nowych pasywek contentowych.
- `[P1] Dodac prymitywy ewaluacji combatowej dla pasywek v1` - `Combat`, `Core / Architecture`, `Skills / Progression`, `Items / Inventory / Economy` - `DONE`
  - Dodac wspolne konteksty i punkty integracji dla przyszlych warunkow oraz modyfikatorow combatowych: typ ataku, pozycja, dystans, status celu, ruch, zasoby, unik, kolejnosc aplikacji, stacking i liczniki. Pokryc je testowymi definicjami i testami integracyjnymi, ale nie aktywowac nowych pasywek Scouta, unikatow bossowych ani innych nowych zachowan contentowych. Zweryfikowano recznie w Encounter i Debug; pelny EditMode suite przechodzi.

### Milestone: Developer Workflow and Content Authoring v1

- `[P1] Rozszerzyć nawigację rekomendacji konsoli v1` - `Tools / Editor / Debug`, `UI / UX`
- `[P1] Dodać schematy komend i opcjonalne argumenty konsoli v1` - `Tools / Editor / Debug`, `Core / Architecture`, `UI / UX`
- `[P1] Rozszerzyć zestaw komend content/debug v1` - `Tools / Editor / Debug`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`
- `[P1] Dodać Content Authoring Window dla itemów i equipmentu v1` - `Tools / Editor / Debug`, `Content / Data`, `Items / Inventory / Economy`
- `[P1] Dodać import-preview danych z arkuszy contentowych v1` - `Tools / Editor / Debug`, `Content / Data`
- `[P1] Dodać walidator contentu i raport referencji v1` - `Tools / Editor / Debug`, `Content / Data`
- `[P1] Dodać Game Defines Window v1` - `Tools / Editor / Debug`, `Core / Architecture`, `Design / Systems`, `Content / Data`
- `[P1] Developer Workflow and Content Authoring v1 — acceptance pass` - `Tools / Editor / Debug`, `Content / Data`, `UI / UX`

### Milestone: Crafting, Upgrading and Consumables v1

- `[P1] Crafting, Upgrading and Consumables v1 - acceptance pass` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Combat`, `Tools / Editor / Debug`
- `[P1] Dodac ulepszanie quality i rarity equipmentu u kowala v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Design / Systems`, `UI / UX`
- `[P1] Dodac consumables: potiony, bomby i return scroll v1` - `Items / Inventory / Economy`, `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P2] Przenieść handel NPC do UI Workspace Services v1` - `NPC / Quest / Dialogue`, `UI / UX`, `Items / Inventory / Economy`
- `[P1] Dodac storage w Arungardzie v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `UI / UX`, `Core / Architecture`
- `[P2] Dodac crafting półproduktów materiałów v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`, `Core / Architecture`
- `[P1] Dodac crafting broni i pancerzy z czesci oraz materialow v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`

### Milestone: Dungeon Content and Layout v2

- `[P1] Dungeon Content and Layout v2 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
- `[P1] Dodac przejscia miedzy pietrami lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
- `[P1] Zachowac stan czesciowo pokonanego encountera po ucieczce v1` - `Bug`, `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Dodac testowy content bizuterii do lootbagow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
- `[P1] Dodac UI skrzynek oraz zamki, klucze i lockpicking v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Content / Data`
- `[P1] Dodac boss encountery i boss lootbagi v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac lootbagi oraz theme i level-bracket loot tables v1` - `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac anchory pojedynczych przeciwnikow w pokojach v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`
- `[P1] Zablokowac interakcje z obiektami przez sciane v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`
- `[P1] Dodac rotacje instancji pokoi v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac petle grafu lochu i wymagane odnogi progresji v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
- `[P2] Dodac DungeonRoomEditor v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dopracowac persistence cial przeciwnikow: pozycja smierci v2` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac semantic content anchors i recznie projektowane sytuacje pokoi v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
- `[P1] Dodac nieregularne ksztalty pokoi lochu v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
- `[P1] Dodac pulapki, przeszkody, klucze/dzwignie i proste zagadki lochowe v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`, `UI / UX`
- `[P2] Rozszerzyc DungeonRoomEditor o legalne krawedzie przejsc i resize footprintu v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`

### Milestone: Save and Load v1

- `[P1] Dodac GameSaveData DTO i JSON serialization v1` - `Core / Architecture`
- `[P1] Dodac snapshoty runtime dla postaci, inventory i itemow v1` - `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Zapisywac questy, world flags oraz stan NPC v1` - `Core / Architecture`, `NPC / Quest / Dialogue`
- `[P1] Dodac GameContentCatalog v1` - `Core / Architecture`, `Content / Data`
- `[P1] Dodac API restore dla Quest i Dungeon runtime v1` - `Core / Architecture`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac GameSession restore factory v1` - `Core / Architecture`, `Content / Data`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac SaveManager oraz manual save/load v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zapisywac party, inventory, quest/world flags i dungeon deltas v1` - `Core / Architecture`, `World / Dungeon / Exploration`
- `[P2] Dodac autosave po wejsciu/wyjsciu z lochu i po ukonczeniu questa v1` - `Core / Architecture`
- `[P1] Dodac CampaignId i strukture katalogow zapisow v1` - `Core / Architecture`
- `[P1] Dodac liste oraz zarzadzanie zapisami kampanii v1` - `Core / Architecture`, `UI / UX`
- `[P1] Dodac menu pauzy i UI zapisow v1` - `UI / UX`, `Core / Architecture`
- `[P1] Save and Load v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`

### Milestone: Combat Core v2 — Closed

- `[P1] Combat Core v2 - acceptance pass` - `Combat`, `Tools / Editor / Debug`, `UI / UX`

### Milestone: Combat v3 — Closed

- `[P1] Combat v3 - acceptance pass` - `Combat`, `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P2] Zrefaktoryzowac payloady PassiveEffectRuntime v1` - `Core / Architecture`, `Combat`, `Design / Systems`

- `[P1] Combat v3 - tooltipy, inspect i feedback bojowy v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Combat v3 - penetracja oraz DealDamage z penetracja` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc targetowanie` - `Combat`, `Core / Architecture`, `Content / Data`

- `[P1] Combat v3 - rzeczywista sciezka ruchu AI i preview kosztu v1` - `Combat`, `UI / UX`

- `[P1] Combat v3 - czytelnosc feedbacku i sekwencjonowanie akcji v1` - `UI / UX`, `Combat`

- `[P1] Combat v3 - dzialalnosc skilli toggle` - `Combat`, `Core / Architecture`, `UI / UX`, `Content / Data`
- `[P1] Combat v3 - mechanika sztyletow Scouta` - `Combat`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc execution pipeline skilli` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

### Milestone: UI Workspace v1 — Closed

- `[P2] Dodac glossary i linki do UI Workspace tooltip v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P2] Ujednolicic stack count i zloto w slotach UI Workspace v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] UI Workspace v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Podpiac generowany equipment do LootTable i tooltipow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
- `[P1] Dodac floating loot window ciala v1` - `UI / UX`, `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Core / Architecture`

- `[P1] Przeniesc drzewka umiejetnosci do UI Workspace i dopracowac UX v2` - `UI / UX`, `Design / Systems`, `Core / Architecture`

- `[P1] Przeniesc skill loadout do UI Workspace v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Dodac wspolne tooltipy UI Workspace v1` - `UI / UX`, `Core / Architecture`, `Items / Inventory / Economy`, `Combat`

- `[P1] Dodac transfer drag-and-drop miedzy Character Info v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] Dodac Character Info floating window v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`

- `[P1] Dodac fundament floating windows v1` - `UI / UX`, `Core / Architecture`

- `[P1] Ustandaryzowac design system i template paneli v1` - `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P1] Rewards and Progression v2 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Combat`, `Items / Inventory / Economy`, `UI / UX`

- `[P1] Dodac generowanie broni i pancerzy z czesci v1` - `Items / Inventory / Economy`, `Content / Data`, `Design / Systems`
- `[P1] Dodac runtime oraz zapis inwestycji w skill tree v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
- `[P1] Dodac trwale ciala i interakcje z lootem w lochu v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Core / Architecture`
- `[P1] Rozbic combat rewards na indywidualny loot przeciwnikow v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Core / Architecture`
- `[P1] Dodac modal loadoutu i zamiane aktywnych skilli v1` - `UI / UX`, `Combat`
- `[P1] Dodac skalowalny model loadoutu aktywnych skilli v1` - `Core / Architecture`, `Combat`, `Design / Systems`
- `[P1] Dodac UI progresji i wydawania punktow v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`
- `[P1] Dodac GrowthData klas i automatyczny wzrost statystyk v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
- `[P1] Dodac status indicators i tooltipy v1` - `UI / UX`, `Combat`
- `[P2] Dodac wizualne stany obiektow i jednostek v1` - `UI / UX`, `Combat`, `World / Dungeon / Exploration`
- `[P1] Przebudowac feedback ruchu i targetowania v1` - `UI / UX`, `Combat`
- `[P1] Rozszerzyc Floating Combat Feedback v2` - `UI / UX`, `Combat`
- `[P1] Przebudowac istniejacy CombatHUD do ukladu docelowego` - `UI / UX`, `Combat`
- `[P2] Dodac personality AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac role AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac reakcje oraz podstawowe counterattack v1` - `Combat`, `Content / Data`
- `[P1] Dodac scoring dostepnych skilli AI v1` - `Combat`
- `[P2] Dodac scheduled effects i wieloturowe skille v1` - `Combat`, `Content / Data`
- `[P2] Dodac CreateObject i Summon v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac zrodla i wygasanie modifierow v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac Poison i Burn v1` - `Combat`, `Content / Data`
- `[P1] Przeniesc Stun i Immobilize do PassiveEffectRuntime oraz dodac Sleep, Freeze i Paralysis v1` - `Combat`, `Content / Data`
- `[P2] Dodac Taunt i Invisibility v1` - `Combat`, `Content / Data`
- `[P1] Dodac Pull i Fallback v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac Charge i Teleport v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac runtime ModifyVar v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac runtime ModifyStat v2` - `Combat`, `Core / Architecture`
- `[P1] Przeniesc Bleed do PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac aplikowanie i usuwanie statusow pod PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac warunki efektow na effectId/index v2` - `Combat`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac lifecycle passive effectow v2` - `Combat`, `Core / Architecture`
- `[P1] Ujednolicic PassiveEffectRuntime i TimedPassiveEffect dla statusow oraz modifierow v2` - `Combat`, `Core / Architecture`
- `[P2] Dopracowac inspector SkillData v2` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P2] Dodac podglad runtime jednostki w narzedziach debugowych v1` - `Tools / Editor / Debug`, `Combat`
- `[P2] Dodac autouzupelnianie i sugestie Dev Console v1` - `Tools / Editor / Debug`, `UI / UX`
- `[P1] Dodac walidatory danych combatowych v1` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P1] Dodac Combat Scenario Runner v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Rozszerzyc Dev Console o komendy gameplayowe v2` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Rozszerzyc Dev Console o komendy globalne i targetowanie po characterId v1` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`
- `[P1] Dodac Dev Console v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Dodac generowanie drzwi z kotwic pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Combat`
- `[P1] Dodac strukturalne kotwice pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac wykrywanie, poscig i rozpoczecie walki przez encounter v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac DungeonCombatContext i snapshot obszaru walki v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Zbudowac BattleGrid z DungeonCombatContext v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac spawn uczestnikow walki w obszarze lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Wyswietlic obszar lochu jako scene walki v1` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Wyswietlic encountery jako przeciwnikow na mapie lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Polaczyc wynik walki lochowej z DungeonInstance v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Dodac testy Dungeon Combat Integration v1` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Dodac interakcje z drzwiami w walce lochowej v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dungeon Combat Integration v1 - acceptance pass` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Arungard, NPC and Quests v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Tools / Editor / Debug`
- `[P2] Dodac okno handlu NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `UI / UX`
- `[P2] Dodac TradeService i bazowe ceny v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P2] Dodac TraderRoleData oraz prosta oferte NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac pierwszy quest "Zbadaj Katakumby" v1` - `Content / Data`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac dziennik zadan i sledzone zadanie v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac akcje wyborow dialogowych v1` - `NPC / Quest / Dialogue`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac odbieranie nagrod questowych i world flags v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P1] Dodac cele questa: rozmowa i wyjscie z konkretnego lochu v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac QuestService oraz eventy postepu v1` - `NPC / Quest / Dialogue`, `Core / Architecture`
- `[P1] Dodac QuestDefinition, QuestInstance i cele etapow v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac dialogi NPC v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac DialogueDefinition oraz DialogueService v1` - `NPC / Quest / Dialogue`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac interakcje z NPC i rozpoczecie rozmowy v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac NpcDefinition, NpcInstance i NpcView v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac Arungard jako statyczna lokacje i powrot z lochu v1` - `World / Dungeon / Exploration`
- `[P1] Dodac punkt wejscia do lochu powiazany z DungeonDefinition v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Procedural Dungeon Loop v1 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
- `[P2] Dodac podstawowe obiekty lochu: skrzynia i przeszkoda v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Powrot do aktywnego lochu po walce v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac generowanie encounterow i triggerow walki w lochu v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`
- `[P1] Dodac grywalna mape podlogi lochu: podlogi, sciany, drzwi i kolizje v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
- `[P1] Dodac wejscie do lochu, spawn druzyny i wyjscie z lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Utworzyc scene DungeonSandbox i widok wygenerowanego lochu v1` - `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac DungeonGeneratorService dla seedowanego layoutu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `Tools / Editor / Debug`
- `[P1] Dodac katalog szablonow pokoi i korytarzy v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac runtime graph pokoi, polaczen i pieter lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Rozszerzyc DungeonDefinition o konfiguracje proceduralnego lochu v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Rewards and Progression v1 - acceptance pass` - `Items / Inventory / Economy`, `Combat`, `Tools / Editor / Debug`
- `[P1] Dodac combat rewards i LootTable v1` - `Items / Inventory / Economy`, `Combat`
- `[P2] Dodac Inventory i Equipment UI v1` - `UI / UX`, `Items / Inventory / Economy`
- `[P1] Dodac pipeline statystyk z equipment i passive effects v1` - `Core / Architecture`, `Items / Inventory / Economy`, `Combat`
- `[P1] Dodac EquipmentSet oraz walidacje slotow i wymagan v1` - `Items / Inventory / Economy`
- `[P1] Dodac ItemDefinition, ItemInstance i Inventory v1` - `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac zdobywanie XP, level i skill points v1` - `Core / Architecture`, `Design / Systems`
- `[P1] Gameplay Loop v1 - acceptance pass` - `Core / Architecture`, `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Polaczyc wynik walki ze stanem lochu i druzyny v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac EncounterDefinition, trigger walki i CombatSessionFactory v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac DungeonDefinition i DungeonInstance dla lochu statycznego v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Utworzyc ExplorationSandbox z ruchem po statycznej mapie v1` - `World / Dungeon / Exploration`
- `[P1] Dodac Party runtime oraz przelaczanie lidera poza walka v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zrefaktoryzowac GameBootstrap i SceneLoader v1` - `Core / Architecture`
- `[P1] Wprowadzic GameSession i GameModeState v1` - `Core / Architecture`
- `[P1] Combat Sandbox - acceptance pass` — `Combat`, `Tools / Editor / Debug`
- `[P2] Dodać line of sight skilli v1` — `Combat`, `World / Dungeon / Exploration`
- `[P2] Dodać Escape v1` — `Combat`
- `[P2] Dodać interakcję z obiektami bojowymi v1` — `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodać ShieldPool z warstwami tarczy v1` — `Combat`
- `[P2] Dodać używanie skilli przez AI v1` — `Combat`
- `[P2] Dodać hard CC statusy v1: Stun i Immobilize` — `Combat`
- `[P1] Dodać wykonanie wielu TargetMode i targetRef v1` — `Combat`
- `[P1] Dodać Heal i RestoreResource effect v1` — `Combat`
- `[P1] Dodać hit chance, crit i podstawowe DamageType v1` — `Combat`, `Design / Systems`
- `[P1] Dodać ResourceRules i generację zasobów v1` — `Combat`, `Design / Systems`
- `[P2] Dopracować zasięgi i kształty targetowania skilli` — `Combat`
- `[P1] Dodać warunkowe gałęzie efektów skilli v1` — `Combat`
- `[P1] Dodać docelową kolejkę tur opartą o AV` — `Combat`
- `[P1] Dodać pełniejsze targetowanie skilli v1` — `Combat`
- `[P1] Dodać obsługę Downed i Dead` — `Combat`
- `[P1] Dodać akcję Revive v1 w CombatSandbox` — `Combat`
- `[P1] Dodać testy StatusEffectSystem i kumulacji Bleed v1` — `Combat`
- `[P1] Dodać testy reguł obrażeń i odporności` — `Combat`
- `[P1] Dodać wspólny CombatDamageSystem v1` — `Combat`
- `[P2] Dodać ApplyStatus effect v1` — `Combat`
- `[P2] Doprecyzować uproszczone zasady statusu Bleed v1` — `Design / Systems`
- `[P1] Dodać debug button Start Combat` — `Tools / Editor / Debug`
- `[P1] Przygotować bazowy bootstrap i game flow` — `Core / Architecture`
- `[P1] Utworzyć scenę Bootstrap` — `Core / Architecture`
- `[P2] Dodać inspect celu v1` — `UI / UX`
- `[P2] Dodać highlight celu umiejętności` — `UI / UX`
- `[P1] Dodać CombatSession` — `Combat`
- `[P1] Dodać BattleUnit` — `Combat`
- `[P1] Dodać BattleGrid` — `Combat`
- `[P1] Dodać GridPosition` — `Combat`
- `[P1] Podpiąć ResistanceSet i ShieldPool do CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ShieldPool` — `Core / Architecture`
- `[P1] Dodać ResistanceSet` — `Core / Architecture`
- `[P1] Dodać CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ResourcePool` — `Core / Architecture`
- `[P1] Dodać CharacterStats` — `Core / Architecture`
- `[P1] Dodać klasę Stat` — `Core / Architecture`
- `[P1] Dodać CharacterClassData` — `Content / Data`
- `[P1] Dodać CharacterView v1` — `Core / Architecture`
- `[P1] Dodać podstawowy prefab jednostki bojowej` — `World / Dungeon / Exploration`
- `[P1] Podłączyć prefab jednostki do CombatSession i synchronizować widok z BattleUnit` — `Combat`
- `[P1] Dodać execution pipeline skilli v1` — `Combat`
- `[P1] Dodać drugi skill testowy z prostym dodatkiem, np. bleed albo push` — `Combat`
- `[P2] Dodać status runtime v1` — `Combat`
- `[P1] Dodać prosty restart walki testowej` — `Tools / Editor / Debug`
- `[P2] Dodać 2-3 przeszkody na planszy testowej` — `World / Dungeon / Exploration`
- `[P2] Dodać blokowanie ruchu przez przeszkody` — `Combat`
- `[P2] Doprecyzować minimalny ruleset ruchu v1` — `Design / Systems`
- `[P2] Doprecyzować AP economy dla vertical slice` — `Design / Systems`
- `[P1] Dodać CharacterDefinition` — `Content / Data`

- `[P1] Ustawić strukturę folderów projektu Unity` — `Core / Architecture`
- `[P1] Ustawić assembly definitions i zależności między modułami` — `Core / Architecture`
- `[P1] Utworzyć scenę CombatSandbox` — `Combat`
- `[P1] Spisać konwencje ID, nazw i podziału Data / Runtime / View / System` — `Design / Systems`
- `[P1] Dodać bazowe enumy i typy wspólne` — `Core / Architecture`
- `[P2] Dodać klikany ruch po gridzie i highlight osiągalnych pól` — `UI / UX`

## Git checkpoints

- Commit po zamknieciu jednej karty: kod, assety, testy i zmiana dokumentacji stanowia jeden spojny commit.
- Nie commitujemy pojedynczych checklist itemow ani niedzialajacego stanu posredniego.
- Po zakonczeniu milestone'u wykonujemy acceptance pass, push na GitHub i tag wersji.
- Aktualny checkpoint: `v0.1.0-combat-sandbox` po uporzadkowaniu lokalnej nazwy galezi na `main`.
- Kolejny checkpoint: `v0.2.0-gameplay-loop` po zamknieciu Gameplay Loop v1.


### Późniejszy content pass — authoring encounterów

- `[P2] Rozwinąć dopasowanie składów encounterów do kotew pokoju`
  - Odłożone na polecenie użytkownika. Jeden encounter na pokój; pokój powinien obsługiwać różne składy encounterów bez ręcznego przygotowania każdego wariantu. Obecne kotwy Enemy pozostają. Dopasowanie przez role do rozważenia w przyszłości, bez implementacji teraz. Kierunek patrzenia należy do patroli; dodatkowy pass walidacji nie jest obecnie potrzebny.
- `[P2] Uzupełnić wspólny model kotew przestrzennych o wysokość`
  - Dotyczy wszystkich kotew wymagających położenia przestrzennego, nie tylko przeciwników. Zachować istniejące układy. Zatwierdzony termin 2026-09-16: przed authoringiem wielopoziomowego contentu; poza domknięciem v3. Szczegóły modelu i migracji uzgodnić przed implementacją.

### Follow-up FPS 2026-10-10 — READY_FOR_SMOKE
- Cache siatki cieni kontaktowych z invalidacją geometrii/kamery/mapy/fog; pooling slotów skill bara, pojedyncze pobieranie aktywnych skilli, mniej formatowania UI i zbędnych zapytań podglądu leczenia; indeks drzwi/przeszkód przy odświeżaniu wyjść. Bez zmiany zasad walki/patroli, bez serialized state i bez zatrzymywania Play.
- 137 unikalnych focused testów PASS; 1 istniejący FAIL pustej ikony consumable odtworzony bez optymalizacji slota. Końcowe cache/pooling 2/2 PASS. Szczegóły: UNITY_HANDOFF.md i out/fps-optimization-final.xml / fps-contact-pool-final.xml.
- Pomiar po optymalizacji UNVERIFIED; zlecenie czeka na naturalne uruchomienie gry i fokus. Raport odróżnia klatki gry od Application.Idle oraz progi 60/30 FPS. Bez deklaracji rozwiązania wszystkich spadków.

### Follow-up FPS — cooperative fog 2026-10-10
- Widoczność mgły liczona porcjami z miękkim budżetem 2 ms na klatkę; publikacja kompletnego wyniku, anulowanie po zmianie obserwatorów/geometrii/drzwi/przeszkód/debugu i retry. Bez zmiany zasięgu widzenia, discovery, save schema ani serialized state.
- PASS: 100/100 regresji fog/blend/transition/seamless oraz 31/31 końcowych testów mgły (zestawy częściowo się pokrywają). Końcowy import scenowego retry weryfikowany osobno. Live FPS i opóźnienie odświeżania podczas ruchu UNVERIFIED; pomiar v5 czeka na naturalny import i skupiony Game View w lochu. Szczegóły w UNITY_HANDOFF.md.
- READY_FOR_SMOKE: końcowa kompilacja i retest anulowania/ponawiania PASS 12/12 (out/fps-fog-sliced-review.xml); self-review PASS. Pomiar runtime nadal UNVERIFIED, bez commita/pusha.
- 2026-10-10 — poprawka regresji odświeżania fog: test scenowego ownera odtworzył wieczne anulowanie obliczeń przy ruchu (out/fog-refresh-repro.xml). Ruch teraz pozwala dokończyć snapshot pozycji, kolejne zadanie pobiera aktualne; zmiany żywych członków drużyny/geometrii nadal anulują. PASS 104/104 (out/fog-refresh-final.xml), z ciągłym ruchem, dogonieniem pozycji po zatrzymaniu i śmiercią podczas zadania. READY_FOR_SMOKE; kod zaimportowany w głównej instancji, runtime FPS UNVERIFIED.

- 2026-10-10 — fog/AI latency: fog runs on an immutable numeric snapshot worker, AI uses its CPU budget without the 128-step throttle and skips empty single-target tiles. PASS 204/204 (out/fog-ai-latency-verified.xml). Synthetic fog 160–194 ms; AI 30x30 scheduling 36 -> 11–17 slices. READY_FOR_SMOKE. Main Play is preserved; natural import and v7 live capture pending. Details in UNITY_HANDOFF.md.

- 2026-10-10 — Blocking presentation optimization READY_FOR_SMOKE: batched battle log without global forced canvas rebuilds; cached fog weights/topology and highlight pixels/meshes. Final workload benchmark fog 75/101 -> 46/52 ms and repeated highlights 58 -> 10 ms (10 refreshes); NOT live frame timings. 254 PASS, 1 identical pre-existing icon test failure. See UNITY_HANDOFF.md and out/blocking-presentation-final.xml. Live capture v9 pending natural import; no forced Play changes.

- 2026-10-10 — Enemy door navigation READY_FOR_SMOKE: patrol planning/execution opens direct doors on approach; combat search continues through direct doors in last-observed direction using paid interactions. Lever-only doors, walls and room boundaries preserved. 11 new cases PASS; final regression 193/195, two unrelated failures also reproduced without the behavior change (out/enemy-doors-unrelated-baseline.xml). Details: UNITY_HANDOFF.md.

- 2026-10-10 — Combat camera/feedback READY_FOR_SMOKE: frame caster + visible targets + floating-number clearance, dynamic banner below turn queue, restore prior view after action; coalesce repeated multi-effect buff names without merging damage numbers. Final 113/113 PASS (out/action-camera-complete.xml); self-review passed. Main import completed automatically in natural Edit Mode. Live visual smoke remains; no serialized-state edits or manual setup. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Scout diagonal melee READY_FOR_SMOKE: all eight neighboring cells count as point-blank for damage, knife exemption, preview and attack presentation. 108/108 PASS (out/scout-diagonal-melee.xml). No serialized-state edits; import queued for natural return from active Play. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Enemy movement camera READY_FOR_SMOKE: follow enemy movement even when hidden, using actual animated position or inactive borrowed-view step; retain zoom and fog rules. 159/159 PASS (out/enemy-movement-camera-final.xml). Main import queued for natural return from Play; no serialized-state edits. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Tighter skill camera frame READY_FOR_SMOKE: remove stacked excessive margins, fit compact/self-cast actions closer, keep banner/damage clearance and restore prior zoom. 88/88 PASS (out/action-camera-tight-frame.xml). Natural import queued while main Play continues. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Dungeon loading overlay READY_FOR_SMOKE: render 'Generowanie lochu…' before synchronous generation, persist through scene startup; resume/general loading messages, duplicate/input protection and failure cleanup. 100/100 PASS (out/dungeon-loading-overlay-final.xml). No serialized-state changes/manual setup; natural import queued. Generation itself remains synchronous. Details: UNITY_HANDOFF.md.
- 2026-10-10 — Pasek postępu na wspólnym ekranie ładowania READY_FOR_SMOKE: gotycka ramka/wypełnienie i font z istniejącej palety, postęp przygotowania + AsyncOperation + aktywacja sceny, reset przy kolejnym przejściu i ukrycie przy błędzie. Bez procentów i udawanego przyrostu czasowego; generowanie nadal synchroniczne. Paleta przypisana przez Unity do istniejącego SceneLoader w Bootstrap. Render i 18/18 EditMode PASS (`output/loading-progress`), hash/GUID/fileID audit PASS; manualny Play Mode NOT RUN.

- 2026-10-10 — Torch lighting cadence experiment READY_FOR_SMOKE: light flicker/shadow preparation at flame animation's 12 Hz, with immediate camera/map/new-presentation refresh. 15/15 PASS (out/torch-lighting-cadence-verified.xml). Main code imported automatically; live look/FPS unverified. No serialized-state edits. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Fog idle cadence experiment READY_FOR_SMOKE: 400 ms visibility job starts and visual uploads (2.5 Hz, imported party idle cadence); forced job refreshes/in-flight polling and immediate hiding preserved. 60/60 PASS (out/fog-idle-cadence.xml). Main code imported automatically. Live look/FPS unverified. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Fog/lighting 200 ms cadence READY_FOR_SMOKE: replaces 400 ms fog / 12 Hz lighting with 5 Hz refresh; immediate invalidations preserved. 62/62 PASS (out/fog-lighting-200ms.xml). Natural main import queued while Play continues. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Camera-motion lighting cache READY_FOR_SMOKE: reuse world-space mask/lighting in padded viewport at 5 Hz; prepare incoming fixtures/casters/contact bands, retain immediate coverage-escape/map/new-presentation invalidation. Added Godmist.Lighting.Refresh marker. 10/10 PASS (out/lighting-camera-cache.xml), including 10 preparations for 120 continuous-pan frames. Live visual/FPS smoke remains. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Flicker/silhouette shadow cache READY_FOR_SMOKE: reuse mask during brightness/color-only updates; geometric light/caster/coverage changes rebuild, hidden casters invalidate ghost shadows. Separate ShadowInputs/ShadowMasks profiler markers. 27/27 PASS (out/lighting-flicker-shadow-cache-final.xml); fixed-scene 50 flicker ticks require one mask preparation. Natural import queued while Play continues; live GPU/FPS unverified. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Static/actor silhouette separation READY_FOR_SMOKE: retain static base, copy and redraw only actors for actor-only changes; preserve existing combined receiver mask with no shader sampling increase. 32 MiB extra GPU memory; original renderer fallback without texture-copy support. 35/35 PASS including D3D11 GPU pixel equivalence (out/lighting-static-base-verified.xml). Main imported automatically; live FPS unmeasured. Details: UNITY_HANDOFF.md.

- 2026-10-10 — Affected-light shadow slice updates READY_FOR_SMOKE: update/copy/draw only old/new affected torch layers for caster changes; preserve unrelated layers, full invalidation for projection/source changes. 39/39 PASS including three-source D3D11 GPU pixel equivalence (out/lighting-local-slices.xml). Existing 200 ms cadence/static base retained; live FPS unmeasured. Details: UNITY_HANDOFF.md.

- 2026-10-10 — selectable-cell corner batching READY_FOR_SMOKE: one cached mesh per target-marker layer replaces per-cell SpriteRenderers; original texture, tint, fog filtering and floor ordering preserved. Same headless 1024-cell fixture cold 62.822 -> 1.725 ms, ten repeats 188.224 -> 3.971 ms. Final D3D11 22/22 PASS, no skips, exact GPU pixel comparison with original Sprite geometry (out/target-markers-final.xml). Coordinator gets phase markers for unattributed 106 ms spike. Live FPS after change NOT RUN; no serialized edits, commit or push. Details: UNITY_HANDOFF.md.

- 2026-10-10 — follower obstacle indexing READY_FOR_SMOKE: replace repeated full-floor object scans in follower footprint checks with revision-scoped cell candidates; keep live active state, physical footprints, elevated-surface rules and non-batch queries. Synthetic 2000 queries/501 obstacles: 356.162 -> 1.942 ms, identical answers. EditMode 61/64 PASS (out/follower-obstacle-index.xml); all three failures reproduced without indexed queries (out/follower-obstacle-index-baseline-failures.xml). Self-review passed; live FPS unverified. No serialized edits, commit or push. Details: UNITY_HANDOFF.md.

- 2026-10-10 — batched target-range LOS READY_FOR_SMOKE: per-query blocker-cell/door-edge indices replace repeated full map-object scans for every ray step; preserve layered sight, occupied-object suppression, destructible self-occlusion and fresh state per query. Synthetic 1024 destinations/500 blockers 1056.098 -> 21.616 ms with identical answers. Final 70/70 EditMode PASS (out/targeting-sight-batch-verified.xml), including driver/scout/interactions/pose integration. Main runtime imported; live FPS/targeting cost after change NOT RUN. Self-review passed; no serialized edits/commit/push. Details: UNITY_HANDOFF.md.

- 2026-10-10 — READY_FOR_SMOKE: drzwi w walce: E korzysta z aktywnej jednostki fokusowanej sesji i płatnej interakcji; kliknięcie progu rozpoznaje drzwi przed zaokrągleniem pozycji/filtrem fog. AI otwiera wskazane drzwi po ID. Zmiany drzwi na wspólnym piętrze odświeżają kopie runtime i blokady krawędzi równoległych walk tylko po zmianie rewizji mapy. TestMirror EditMode 86/86 PASS (out/combat-door-verified.xml), w tym integracja widok → owner → driver, oba końce drzwi, koszt AP, LeverOnly, ściany, AI/pursuit i zmiany drzwi w mapie warstwowej. Bez zmian serialized state i commit/push. Smoke Play Mode pozostaje do sprawdzenia.

- 2026-10-11 — Patrol collider anchor READY_FOR_SMOKE: odtworzono zatrzymanie przy scianie o produkcyjnej grubosci 0.25 (3 przystanki, dalszy ruch zablokowany). Cel ruchu i warunek dotarcia uwzgledniaja teraz MovementColliderCenterOffset, jak nawigacja druzyny. 14/14 EditMode PASS, out/patrol-anchor-final.xml; wielokrotne okrazenia z produkcyjnym prefabem oraz regresja drzwi. Bez zmian serialized state/save. Biezacy Play pozostawiony; naturalny import oczekuje na tryb edycji. Szczegoly: UNITY_HANDOFF.md.

- 2026-10-11 — Sarkofagi 1x2 READY_FOR_SMOKE: wspolny footprint (kotwa + pole na polnoc) w definicji, rozmieszczaniu, eksploracji/NavMesh i walce. Poprawiono 7 szablonow pokoi za zgoda uzytkownika oraz blueprinty/authoring (rozszerzanie pokoju nie duplikuje kotew sarkofagow). Generator sprawdza caly footprint, granice pokoju, zajetosc i sciane miedzy polami. 205/207 EditMode PASS (out/sarcophagus-1x2-final.xml); dwa pozostale bledy odtworzone przy wylaczonej zmianie 1x2 (out/sarcophagus-unrelated-baseline.xml). Testy zajetosci, wszystkich dozwolonych obrotow, 12 seedow wypraw, walki i snapshotow PASS. Self-review PASS. Bez commit/push; aktualny Play nieprzerwany. Nowe rozmieszczenie dotyczy nowych lochow. UNITY_HANDOFF.md.

- 2026-10-11 — Minimap sarcophagus footprint READY_FOR_SMOKE: minimapa korzysta z EnumerateOccupiedCells zamiast pojedynczego Dot dla przeszkod; ciagly prostokat 1x2 z zachowaniem dotychczasowego marginesu i zasad pamieci mgly. 14/14 DungeonMinimapTests PASS (out/minimap-sarcophagus-footprint.xml), w tym kontrola pikseli obu pol, pamieci/dezaktywacji i regresja 1x1. Self-review/whitespace PASS; main Views.dll zaimportowany 22:52:47Z. Bez serialized edits/manual wiring. Smoke NOT RUN.

- 2026-10-11: Catacombs dedicated corpse art READY_FOR_SMOKE — 8 transparent native-size PNGs, current humanoid ghoul, no rotation/red tint for dedicated sprites, explicit DeadSprite priority; DungeonEnemyBodyTests PASS 22/22 (out/corpse-sprites-final.xml), main import confirmed. Art provenance and smoke steps in UNITY_HANDOFF.md. Tooltip formatters located; redesign not yet applied.

- 2026-10-11: Tooltip duplication/stacked DoT READY_FOR_SMOKE — reviewed combat skill descriptions now use generated mechanics once with melee/knife rules intact; bleed/poison/burn presentation groups independent instances and sums true upcoming ticks with original expiry data. Generic rule/numeric duplication reduced. Final 69/69 EditMode PASS (out/tooltip-dedup-final-state.xml); main import confirmed. No gameplay or serialized changes; manual smoke pending.
- 2026-10-11: Full tooltip content pass READY_FOR_SMOKE — all 92 skills use shared data-driven mechanics once across combat/loadout/tree; complete effect/condition coverage, special directional rules, typed buff and passive activation deduplication. Final 206/206 content regression PASS (out/tooltip-catalog-verified.xml); seven separately developed rarity-border failures documented in handoff. No gameplay/serialized changes; visual smoke pending after normal Editor import.
- 2026-10-11: Buff/debuff player-facing wording READY_FOR_SMOKE — shared short stat/variable labels in both status tooltip views and generated skills, removed internal stacking/base-stat text and exploration tick counts; signs/units/durations/zero limits retained. 70/70 EditMode PASS (out/tooltip-plain-language-final.xml); visual smoke pending.
- 2026-10-11: Item/potion tooltip content pass READY_FOR_SMOKE — audited 245 definitions; compact potion/bomb/utility status effects, no internal percent-modifier label or redundant catalyst adjustments, readable jewelry resource generation. 129/129 EditMode PASS (out/item-tooltip-content-final.xml); no gameplay/serialized changes; visual smoke pending.

- 2026-10-11: Passive skill-tree tooltip content pass READY_FOR_SMOKE — all 133 passive nodes in 8 class-linked trees audited at every rank; current/next values share one rules block, internal notation removed, shared condition/activation formatter avoids repetitions. Final focused content regression 31/31 PASS (out/passive-tooltip-content-final-state.xml); tree presentation acceptance previously 51/52 with only the subsequently fixed catalog-copy assertion failing. No gameplay/serialized changes; visual smoke pending.

- 2026-10-11: Skill targeting tooltip style READY_FOR_SMOKE — range/line-of-sight/target share an 85% font-size block separated by a blank line from effects, in combat and loadout/tree skill details. Existing catalog/loadout regression 22/22 PASS (out/skill-tooltip-targeting-style.xml). No serialized changes; visual smoke pending.

- 2026-10-11: Release cleanup READY_FOR_SMOKE — 6169 unused asset/meta files, 42 empty folders/meta, retired Crypt3D/Review/Recovery/SampleScene code/art and historical local artifacts removed; recoverable 22.9 GB copy outside repo. Active Sandbox/Arungard authoring dependencies preserved. Zero new broken GUID references; clean Windows x64 build PASS, runtime Arungard smoke PASS, cleanup-related EditMode 38/38 PASS. Comparative regression 462/501 PASS with 39 failures also reproduced before cleanup; release acceptance still open. Portable editor art contracts replace output dependencies. No commit/push. Details: RELEASE_CLEANUP.md and UNITY_HANDOFF.md.

- 2026-10-11: Release regression READY_FOR_SMOKE — all 39 baseline failures resolved and traced to final passing cases. Full EditMode 2406/2406 PASS, zero skips; no tests removed/disabled. Runtime fixes cover original obstacle barriers during multi-effect charges, precise-endpoint blocked-start recovery, unequip-capacity preflight, destroyed Unity tooltip/animation objects and duplicate North/Side corners; repaired UTF-8 blueprints and current UI/art fixtures. Three lifecycle regression cases added. Windows x64 release build PASS, 0 errors; five scenes have no missing scripts. Self-review/whitespace PASS; no serialized-state changes, commit/push or publication. Manual gameplay smoke and prior serialization-depth warnings remain separate release work. Details: RELEASE_REGRESSION.md and UNITY_HANDOFF.md.

Individual-trunk follow-up (2026-10-11): the safe first band is traversable floor, with separate circular root colliders on selected former boundary edges. TreeTrunk markers preserve placement through saves, allow tactical cell-centre crossings, and do not become full edges in native navigation; native routes use the physical root bounds. The continuous boundary is deeper in the forest. Existing saved/generated geometry is unchanged; generate a NEW prototype for smoke. Check approaching roots from all directions and walking between trunks. Automated verification: out/forest-trunk-final.xml (ForestPrototypeTests, DungeonRoomFootprintTests, DungeonNavigationMeshTests, DungeonLayoutSnapshotTests), including physical passage/blocking and TreeTrunk JSON restore. Live visual smoke remains pending; no manual wiring needed. This supersedes the earlier shallow-pocket-only collision approach.
Final verification: 69/69 EditMode PASS, no skips (out/forest-trunk-final.xml). Targeted source/whitespace self-review PASS; visual smoke pending.

Forest presentation cleanup (2026-10-11, READY_FOR_SMOKE after focused verification): minimap excludes TreeTrunk markers from wall rasterization, retaining Full/Low/Railing semantics. Decorative underlay shares the strongest known fog state of adjacent underlay owners on the visible level (precomputed local edge sets, no new visibility rays), avoiding isolated missing squares along an already visible canopy. No playable floor/LoS/save geometry is revealed or changed. First-row art scale varies deterministically from 85–110%; root colliders stay unchanged. Verify underlay around a partially visible glade, minimap passages and approach to trunks. No manual wiring, no computer use, live visual smoke pending.
Verification: 35/35 EditMode PASS, no skips (out/forest-presentation-final.xml), including minimap trunk-versus-full-wall raster regression, forest physical roots/passages and native navigation. Targeted whitespace/source self-review PASS. No live GPU visual verification.

Interior cover — first slice (READY_FOR_SMOKE, 2026-10-11): four wide glade templates now contain up to two required, off-centre tree anchors. Narrow clearings, entrance, corridors and the Ent arena remain clear. Shared obstacle placement/occupancy/LoS/save lookup are used; exploration opts into physical circular roots (radius 0.18), while tactical occupancy and LoS use one cell. Optional definition-owned sprite/tint/width/root radius leave existing obstacle prefabs unchanged unless opted in. Tree art uses the temporary city sprite at width 1.5. No new enemy/loot content or offer activation. Generate a NEW prototype to obtain these saved objects; existing saves are unchanged. Automated: 69/69 EditMode PASS, no skips (out/forest-interior-final.xml), including ten seeded placements and all-floor connectivity after excluding trunks, LoS, actual prefab geometry/rebind and placement/navigation/layout regressions. Forest assets authored in the validation mirror and copied back by exact file, no Editor wiring needed. Live visual smoke is pending. Remaining interior slice: multi-cell rocks, LoS brush and crown occlusion.

Forest prototype entry/import follow-up (2026-10-11): main YAML pool/anchor references match the shipped tree GUID; isolated tests that load shipped assets without Configure pass. A stale in-memory pool was reproduced with the same placement exception; ForceUpdate reimport alone does not restore it. The Inspector prototype entry now imports the tree/definition, adds and saves only the missing owned tree pool reference when necessary, validates, then enters. It does not reauthor rooms or relax runtime placement validation. Serialized wiring is within the user-authorized pass 1b scope. Added standalone ForestImportedAssetsTests, independent of authoring setup; includes entry with a deliberately stale in-memory pool and on-disk content equality. Live original exception was not reproduced in the validation mirror, so final live entry remains smoke verification.

Verified: 2/2 independent imported-asset tests PASS (out/forest-pool-repair-final.xml), including deliberately emptied loaded pool, automatic reference repair, unchanged final dungeon asset bytes and successful placement. Source/whitespace review PASS. READY_FOR_SMOKE: retry the existing prototype entry button after compilation; no authoring-menu action required.

Forest ground polish (READY_FOR_SMOKE, 2026-10-11): corrected the bottom-pivot underlay anchoring so visible sprite bounds, rather than its pivot, are centred on each void cell. Forest-only underlays overlap at 2.4 cells and use a per-renderer world-bounds opt-in in Silhouette Floor to feather circular edges with pixel-scale variation; ordinary floor has zero opt-in and retains its alpha. Existing lighting property blocks preserve the bounds. Skip masonry-only side-void black occluder generation for vegetation maps. Ground cover uses the existing transparent grass sprite, deterministic sparse clustered placement, muted tint, shared ground lighting/fog, and skips occupied presentation cells; no new colliders or map/save/navigation changes. GroundCover reference added to DVP_Forest_Prototype and authoring setup. Tests: 48/48 EditMode PASS, no skips (out/forest-polish-verified.xml), including real GPU alpha readback for feathered centre/rim/corners and unchanged ordinary floor, deterministic cover/occupied-cell exclusion, actual underlay bounds, navigation, minimap and fog blending. Full live Forest visual smoke and FPS measurement remain pending; this is not a claim of visual acceptance. Reopen/rebuild the prototype presentation to see changes; no manual wiring.

Forest floor/fog alignment and crown occlusion (READY_FOR_SMOKE, 2026-10-11): the playable dirt tile also uses a bottom pivot. Its previous scale-only transform displaced its rendered bounds half a cell above the logical floor/soft-fog mesh. CenteredGroundTransform now normalizes sprite size AND centres sprite bounds; both authoring and vegetation-only per-cell presentation use it, including already loaded tile assets. Tile_Forest_Prototype updated. This corrects the uncovered rectangular strip without revealing unknown floor cells or modifying map geometry. Boundary and interior tree crowns use per-renderer _ForestCrown parameters in their existing wall/body shaders; lower trunk remains opaque, upper crown fades towards 22% when overlapping a visible actor behind its root, then restores after the actor leaves. Visible party/enemies are considered; unseen enemies do not drive fade. Runs in existing lighting cadence with cached obstacle renderers, no extra traversal/LoS work. Tests: 72/72 EditMode PASS, no skips (out/forest-crown-final.xml), including GPU composed tilemap+fog rendering and absence of northern spill, real wall/body crown alpha and retained trunk, overlap/front/side rules, fog transitions/blending and lighting cadence regressions. Render inspected: out/forest-floor-fog-alignment.png. Live full Forest visual/movement smoke remains pending. Reopen the prototype presentation after compilation; no manual wiring.

Forest crown fade rollback (READY_FOR_SMOKE, 2026-10-11): reverted at user request after reported runtime cost. Removed all tree/actor overlap scans, crown property-block updates and crown alpha code from wall/body shaders; trees retain ordinary opacity. Preserved centred floor/fog alignment, feathered underlay, ground cover and physical trunk navigation. Verification: 71/71 focused EditMode tests PASS, no skips (out/forest-crown-revert.xml); targeted diff whitespace check PASS. No manual wiring required. Live FPS not measured in this rollback. This supersedes the crown-fade portion of the previous entry.

Standing-object depth query optimization (READY_FOR_SMOKE, 2026-10-11): eliminated captured List.Exists predicates from per-frame standing-object and door depth queries. Registration lists stay sorted; binary lookup checks both epsilon neighbours and inserts only new baselines. Existing ordering/rebuild semantics remain intact, including trees, pillars and doors; no serialized/save changes or manual wiring. Verified 72/72 EditMode tests PASS, no skips (out/forest-depth-queries.xml): real actor/obstacle occlusion, forest presentation and a zero-managed-allocation regression over 4,000 repeated exact/nearby baseline queries with unordered initial registration. Targeted whitespace/self-review PASS. Live FPS impact is not measured.

Forest visual variety (READY_FOR_SMOKE, 2026-10-11): boundary/fringe tree silhouettes vary in horizontal aspect while grounded roots and colliders stay fixed. Ground cover uses continuous low-frequency Perlin clustering instead of four-cell block exclusions, muted olive/brown tint variation and small rotations; placement stays deterministic and excludes occupied cells. Depth brightness now follows smooth extinction to retain readable nearby tree layers and darken the distant forest. All variation is established during presentation construction; no additional per-frame work, renderers, shaders, colliders or serialized/save changes. Verification: 36/36 focused ForestPrototypeTests and fog tests PASS, no skips (out/forest-visual-variety.xml); targeted diff check/self-review PASS. Full live art acceptance remains pending. Reopen/rebuild the prototype presentation after compilation; no new dungeon generation or manual asset wiring required.

Forest ground/interior-tree variety (READY_FOR_SMOKE, 2026-10-11): playable dirt uses deterministic quarter-turns and horizontal mirrors around normalized sprite bounds centre, reducing repeated texture orientation while preserving exact cell/fog coverage. Positive-TrunkRadius tree presentations vary width/height by at most 10%, mirror and shade at binding; grounded origins and physical roots remain unchanged. No added renderers, shaders, frame loops or save fields. Verified 72/72 ForestPrototypeTests/DungeonSimpleVisualTests PASS, no skips (out/forest-ground-variety.xml), including per-cell centre/corner bounds, GPU composed floor/fog alignment, repeated tree bind stability, root dimensions and actor occlusion. Targeted whitespace/self-review PASS. Live art acceptance remains pending. Re-enter prototype after compilation; no new generation/manual wiring needed.

Forest trunk clearance fix (READY_FOR_SMOKE, 2026-10-11): direct movement swept the full actor collider correctly, then incorrectly treated its centre/perimeter topology samples as separate actor feet positions. CanEnterTraversalAtWorld reapplied both collider radius and feet offset at these samples, artificially enlarging physical props. Footprint samples now check only real floor/topology and logical full-cell obstacles; the preceding real collider sweep and endpoint checks retain physical trunk protection. Native route start validation uses the actual actor world footprint so standing beside a trunk inside its occupied logical cell does not disable NavMesh. Full-cell obstacles, closed edges, levels and tactical occupancy stay unchanged; no collider shrinking or save/asset edits. Verification: 77/77 ForestPrototypeTests, DungeonNavigationMeshTests and DungeonExplorationPathfinderTests PASS, no skips (out/forest-trunk-clearance-final.xml). Regression approaches the actual tree prefab from four cardinal directions with a nonzero feet offset, verifies near clearance and penetration rejection, and proves native route ownership when leaving the tree logical cell. Whitespace/self-review PASS. Live screenshot case remains smoke pending; re-enter prototype after compilation, no new generation/wiring needed.

Forest undergrowth base (READY_FOR_SMOKE, 2026-10-11): replaced the brown city dirt reference with a dedicated generated forest-floor-v1 texture: muted moss, decaying leaves, fine roots and damp earth gaps. GroundTint updated to (0.78,0.80,0.70), retaining subdued rotten greens. Shared GroundTile updates both playable floor and feathered boundary underlay; existing sparse grass remains supplementary. New texture imported Point/no mipmaps/uncompressed with max size 128; no additional renderers, colliders, frame loops or save fields. Authoring points at the new asset, and verified Tile_Forest_Prototype/DVP_Forest_Prototype serialized outputs copied back from the validation mirror. Tests: 37/37 ForestPrototypeTests and fog tests PASS, no skips (out/forest-undergrowth.xml), including GPU composed floor/fog alignment, imported ground geometry, stable placement and trunk approach/native routing. Inspected generated source and actual GPU floor/fog render. Live full Forest visual acceptance pending; re-enter prototype after compilation, no new generation/manual wiring required. Built-in image_gen used; provenance/prompt in FOREST_UNDERGROWTH_ART.md.

Forest broad ground patches and visual paths (READY_FOR_SMOKE, 2026-10-12): forest-only floor shader now uses continuous world-space litter at four-cell scale instead of independently rotated high-frequency tile textures. Baked low-frequency fields vary moss/litter colours and damp earth coverage. A presentation-only mask traces room connections to internal room centres on the real irregular footprint; contiguous doorway cells form one portal to avoid a fan of bare soil. Paths blend dark, desaturated existing dirt with moss through soft irregular rims. Mask is built during map presentation rebuild, reused across occupancy/camera changes, capped at 2048 per axis, and disposed with the view; broad/edge noise is baked on CPU rather than evaluated per fragment. Shared floor lighting binds the textures only when the mask changes; ordinary indoor/body shaders stay unchanged. No new scene renderers, colliders, navigation or save fields. SoilSprite is wired in the vegetation profile and repeatable authoring. Verification: 73/73 ForestPrototypeTests and fog/lighting cadence tests PASS, no skips (out/forest-paths-verified.xml). GPU preview inspected and copied to out/forest-paths-preview.png; corrected overly wide/light soil after first render. Regression covers connected-room rendering, mask reuse on occupancy refresh, unchanged traversal revision and indoor opt-out; prior floor/fog/trunk tests remain green. Targeted diff/self-review PASS. Live expedition appearance and FPS remain unmeasured. Re-enter prototype after compilation; no new generation or manual wiring. This slice covers broad undergrowth/paths; remaining black fog rectangles and large forest props are separate follow-ups.

Natural trail correction (READY_FOR_SMOKE, 2026-10-12): visual paths are now routed after fixed obstacle placement through a separate SetForestPathObstacles input supplied by DungeonSandboxBootstrap. BFS excludes active movement-blocking obstacle footprints; actors/bodies and ordinary presentation occupancy do not rebuild paths. Same obstacle set reuses the mask. Trail radius varies deterministically (0.32–0.56 cells plus a small junction widening), with jittered centres, worn/litter-filled sections and less bare mud. Corrected Unity SmoothStep usage via normalized InverseLerp so brush width/round root exclusion have the intended thresholds. Root exclusion fades radially rather than punching square holes. Litter texture world scale changed from quarter to half repeats per cell, halving apparent detail size while retaining broad colour patches. Forest fog gets a wider cached inward feather (0.95 vs 0.6 cells), never exposing unexplored cells; original indoor fog is unchanged. No frame-loop rebakes or traversal/save/collider changes. Verification: 74/74 ForestPrototypeTests and fog/lighting tests PASS, no skips (out/forest-natural-trails-verified.xml). GPU regression reads the actual mask to assert no exposed trail under trees, narrow coverage and obstacle-set reuse; forest fog test checks stronger inward fading and zero unknown brightness. Final preview inspected at out/forest-paths-preview.png. Whitespace/self-review PASS. Re-enter prototype after compilation; no fresh dungeon generation/manual wiring required. Live screenshot acceptance, exact disappearance of exterior black cutoffs and FPS remain unverified.


## Forest edge details — READY_FOR_SMOKE (2026-10-12)
- Added muted low shrub and small rotten wood sprites at clearing fringes. Deterministic placement omits trails and their adjacent cells, and occupied cells and their adjacent cells.
- Uses existing decoration fog, light and depth sorting. No colliders, navigation/persistence changes or per-frame behaviour.
- Profile and repeatable ForestPrototypeSetup wire existing shrub and new transparent forest-deadwood-v1.png. Prompt/provenance: FOREST_EDGE_DETAILS_ART.md.
- Verification: out/forest-edge-details-verified.xml, 74/74 EditMode tests passed, including new sprite references, fringe placement, trail exclusion, deterministic rebuild and unchanged traversal/collision checks. Targeted diff whitespace check passed.
- Smoke: re-enter forest prototype; inspect subdued small edge decorations, clear paths, fog and depth sorting. Live appearance and FPS remain unmeasured. No new seed required.


## Forest prop material correction — READY_FOR_SMOKE (2026-10-12)
- Root cause: ApplyGroundLighting assigned the forest floor material to sprite decorations. Its world-space forest blend replaced their RGB with soil/litter, hiding the intended deadwood/shrub appearance despite correct floor ordering.
- Per-renderer property now disables forest surface blending for props, preserving their sprite and shared lighting. Forest underlay patches retain blending via their existing ForestPatchBounds marker. Scale and sorting unchanged.
- Verification: 74 existing forest/fog/lighting tests passed in out/forest-prop-material.xml. Two added tests initially failed due to a missing fixture sprite; after correcting fixture, all 37 lighting tests passed in out/forest-prop-material-retest.xml. Both prop and forest underlay material modes covered; shared material remains unchanged. Targeted diff check passed.
- Smoke: re-enter forest and inspect actual wood/shrub silhouettes. Live appearance/FPS unverified.


## Forest party light — READY_FOR_SMOKE (2026-10-12)
- Forest presentation builds one carried torch using the existing torch sprite/material and shared light/shadow arrays. Source follows current party leader (including leader changes) on existing 0.2s lighting refresh; fixture is offset beside/above the feet and scaled to 65%.
- Indoor wall torches unchanged. Uses existing radius, warm palette and flicker; ambient and fog discovery unchanged. Existing lighting teardown owns the fixture. No serialized or persistence changes.
- Verification: out/forest-party-light.xml, 60/60 forest and lighting EditMode tests passed; targeted diff check passed. Live carried-fixture placement, visual brightness, combat visibility and FPS remain unverified. A moving source invalidates affected shadow slices at the existing cadence; no claim of performance improvement.
- Smoke: re-enter forest; check local light, wood/shrub colours, leader cycling, movement and camera focus during combat. This is a prototype visual source, not an equipment/fuel mechanic.


## Invisible forest party light — READY_FOR_SMOKE (2026-10-12)
- User requested removal of the visible carried torch while keeping light. Forest source renderer now forceRenderingOff; normal visibility refresh cannot make it draw again. Existing source position, flame anchor, radius, shadows and cadence unchanged. Indoor torches unchanged.
- Verification: targeted diff check and inspection of visibility-refresh path. No test rerun for this presentation-only toggle; live appearance remains for smoke. Re-enter forest after scripts reload. Tree-mounted torches remain optional future work.


## Forest tree silhouettes — READY_FOR_SMOKE (2026-10-12)
- Added transparent dead and crooked sparse-crown tree art via built-in imagegen; original outputs preserved, full prompts and asset paths in FOREST_TREE_VARIANTS_ART.md.
- Stable cell-based selection: original 9/12, dead 1/12, crooked 2/12. Boundary trees, individual fringe trunks and authored interior tree obstacles use variants. Missing optional variants fall back to original. Profile and repeatable setup wire both new sprites.
- Art pivots align asymmetric roots; imported Point/no mipmaps/max128. Existing circular root collision, tactical occupancy, LoS and save identities unchanged. No runtime update loop or crown fade added.
- Verification: out/forest-tree-variants.xml 60/60 forest/lighting EditMode tests passed, including physical approach, rebind stability and ten-seed reachability. Allowed-sprite assertions now include authored variants. Targeted diff check passed. Live visual alignment and FPS require smoke.
- Smoke: re-enter forest prototype; check mixed silhouettes and root approach, especially flipped crooked trees. Fresh seed unnecessary. Large rocks/stumps and landmarks are later slices.


## Forest boulders and stumps — READY_FOR_SMOKE (2026-10-12)
- Added two generated transparent sprites (built-in imagegen); sources/prompts saved in FOREST_ROCK_STUMP_ART.md. Point/no mipmaps/max128, pivots account for transparent padding.
- New owned definitions object.forest.boulder.prototype and object.forest.stump.prototype. One logical cell each; circular exploration bases radius .32/.22; boulder blocks LoS, knee-high stump does not. Existing obstacle placement/view/save references used; no new per-frame behaviour.
- Up to one boulder and one stump near existing trees in roomy off-centre areas, full 3x3 floor clearance and central cross/approaches preserved. Authored anchors added to hollow, long_glade and roots; other templates remain clear where no suitable space exists. Dungeon pool and imported-prototype stale-pool repair include both. No unrelated content assets copied.
- Verification: 60/60 initial forest/lighting tests passed (out/forest-rock-stump.xml). Expanded forest/imported-asset suite 27/27 passed (out/forest-rock-stump-final.xml), including real prefab radius/grounding/rebind for both props, distinct LoS flags, shipped pool repair, ten-seed exact placement and all-cell reachability. Intermediate test overrequired props in every seed; corrected to require actual coverage across seeds while retaining exact per-template anchor counts and reachability. Targeted diff check passed.
- Smoke: generate a fresh forest expedition to obtain new saved objects (existing saved instance unchanged); inspect grounded art, collider approach and clear trails. Live visuals/FPS unverified. Mob/icon asset work left untouched.


## Forest prop distribution correction — READY_FOR_SMOKE (2026-10-12)
- Previous authoring required an existing tree anchor and a wide central cross; only three templates received rocks/stumps and Start never did. This explains empty entrance/other clearings, independent of sprite import. Screen alone does not prove current room identity.
- Allow Start/Normal/Optional props without prerequisite tree anchors. Retain full 3x3 floor clearance, 2-cell prop separation, difficult-terrain exclusions and a clear central cross (1.25-cell exclusion). Corridors and boss arena remain clear. Entrance now always has one boulder and one stump.
- Verification: out/forest-prop-coverage.xml 27/27 forest/imported-asset tests passed, including explicit two-prop entrance, real colliders and ten-seed all-cell reachability. Targeted diff check passed. Updated owned room assets only. No rendering-scale changes; live visual smoke pending.
- Generate a NEW forest instance; saved old object placements do not acquire these anchors.


## Swallowed forest ruins — READY_FOR_SMOKE (2026-10-12)
- New broken moss-covered column sprite generated through built-in imagegen, inspected and saved as forest-ruined-pillar-v1.png. Original and full prompt documented in FOREST_RUINS_ART.md; Point/no mipmaps/max128, grounded pivot accounts for padding.
- New object.forest.ruined_pillar.prototype with one-cell tactical footprint, .3-radius physical base, LoS blocking and shared obstacle lighting. Required pillar + existing boulder/stump cluster authored only in optional overgrown_ruins template. Dungeon pool/import repair updated; normal glades, corridors and arena unchanged.
- Verification: out/forest-ruins-final.xml 28/28 ForestPrototypeTests/ForestImportedAssetsTests PASS, including dedicated real-prefab collider/rebind case, landmark confined to ruins, shipped pool repair and ten-seed all-cell reachability. Initial imported-pool test ran before authoring the new asset and failed; repeat against authored shipped state passed. Targeted diff check passed.
- During implementation an encoding error truncated the untracked authoring source. Recovered prior behaviour from its preserved compiled Editor assembly with dependencies resolved, rewrote UTF-8 and revalidated the entire forest/import suite; recovery outputs remain under out/forest-recovery*. Runtime source/other artwork untouched.
- Smoke: generate a fresh forest instance; optional room Pochłonięte ruiny now contains the landmark. It is not in every entrance or necessarily every generated seed. Live visual alignment and FPS pending. This is an initial ruin landmark, not a finished ruin set or gameplay interaction.


## Abandoned forest camp — READY_FOR_SMOKE (2026-10-12)
- Added cold stone-ring campfire and broken supply crate sprites with built-in imagegen; true alpha inspected, originals/prompts in FOREST_CAMP_ART.md. Point/no mipmaps/max128; grounded pivots account for padding.
- New owned obstacle definitions object.forest.cold_campfire.prototype and object.forest.broken_crate.prototype, one-cell logical footprints and .26/.28 circular bases. Neither blocks LoS. No fire light, loot/rest interaction or new per-frame work.
- Optional blind_glade keeps its ID, display name now Porzucone obozowisko. Exactly one fire ring and one crate replace its generic edge props. Central cross exclusion narrowed to 1 cell for this template only, retaining full 3x3 floor clearance and 2-cell spacing. Other rooms and boss arena unchanged. Pool/import repair includes both definitions.
- Verification: out/forest-camp.xml 28/28 forest tests PASS including both real-prefab collision/rebind cases, camp-only anchors and ten-seed all-cell reachability. out/forest-camp-imported.xml 2/2 shipped asset/pool tests PASS. Targeted diff check passed. Copied only two new definitions, owning room and forest dungeon definition. Mob/icon work untouched.
- Smoke: generate NEW forest; optional camp room may vary by seed. Check ground alignment, paths and visual readability. Live appearance/FPS remain unverified; this slice is environmental content only.


## Forest roots and fungi — READY_FOR_SMOKE (2026-10-12)
- Added exposed roots and pale woodland fungi sprites; originals and prompts recorded in FOREST_ROOTS_FUNGI_ART.md. Forest presentation profile and authoring bind both sprites.
- Both reuse existing edge-decoration slots, replacing part of the shrub/deadwood distribution without increasing its count. Existing path/occupied-cell exclusions, fog and lighting remain in use; no colliders or per-frame update added.
- Verification: out/forest-roots-fungi.xml — 65/65 ForestPrototypeTests and DungeonLightingCadenceTests PASS. Copied the authored forest presentation profile from validation mirror. Live visual scale/readability and FPS not measured.
- Smoke: leave and re-enter the forest to rebuild decoration; a new seed is not required for this slice.


## Woodland fern — READY_FOR_SMOKE (2026-10-12)
- Added fern sprite and WoodlandFern presentation reference, authored by ForestPrototypeSetup. Built-in imagegen original and full prompt: FOREST_FERN_ART.md. Inspected sprite and verified RGBA alpha; Point/no mipmaps/max128 matches existing props.
- Fifth deterministic edge-detail variant reuses existing sparse slots; rendered width .9–1.15 cells. No additional decoration slots, collision, LoS or per-frame update. Existing path and occupied-cell exclusions retained.
- Verification: out/forest-fern.xml — 65/65 forest and lighting tests PASS; targeted diff check PASS. Synced only owned files to validation mirror and copied authored presentation profile back. Live visual scale/readability and FPS remain unmeasured.
- Smoke: leave and re-enter forest; fresh seed unnecessary for this presentation-only change.


## Forest visual scale review — READY_FOR_SMOKE (2026-10-12)
- Rendered and inspected all ten ground props alongside Jerzy sprite using actual exploration prefab body scale on a one-unit grid. Before/after renders exposed transparent padding differences: fungi occupy ~49% of image width, campfire ~59%, stump ~66%. Source art is not uniformly 32x32: forest props use max128 Point import; PPU32 alone does not define rendered world size.
- Increased baseline full-sprite widths: deadwood .7→1.1, shrub .7→1, fungi 1.05→1.4, fern .9→1.1 (existing +0–.25 variation); roots remain 1.05. Object presentation widths: boulder 1.15→1.4, stump 1→1.2, cold campfire 1.3→1.7, crate 1.05→1.25; pillar unchanged. All collision radii and tactical footprints retained.
- Added editor menu Godmist/Content/Pass 1b/Forest Scale Preview and saved Assets/_Project/Scenes/Prototypes/ForestScalePreview.unity with owned preview materials. Menu asks to save modified scenes before replacing the open scene. Preview uses unlit neutral background to isolate proportion; actual forest lighting appearance still needs live smoke.
- Verification: out/forest-scale-tests.xml 67/67 forest, imported assets and lighting tests PASS. Final screenshot out/forest-scale-preview.png inspected. Copied only owned preview assets and four resized definitions from validation mirror; no icon/mob files touched.
- Re-enter forest for decoration rebuild and new object visuals. No new seed required. Future props should be compared in this scene before acceptance.


## Forest native 32 PPU correction — READY_FOR_SMOKE (2026-10-12)
- User requirement: one artwork pixel = 1/32 world unit, matching characters. Previous importer-only PPU plus arbitrary Transform scaling violated this and is superseded.
- Added 14 non-destructive -32ppu PNG derivatives with Aseprite pixel resize, keeping source originals. Native canvas sizes: deadwood/roots 40x20, shrub40x18, fungi48x36, fern40x27, grass28x9, boulder48x36, stump/crate40x40, column56x75, fire56x35, dense tree48x68, dead/crooked trees48x77. Imports: PPU32, Point, no NPOT rescaling; references authored to derivatives. Source provenance remains in respective FOREST_*_ART.md documents.
- Removed forest decoration and boundary-tree size/aspect randomness and ground-cover rotation. Opt-in NativePixelScale on forest obstacle definitions compensates inherited parent scale and bypasses PresentationWidth and random size variation. Other obstacles preserve old behavior. All physical collider radii and tactical footprint rules retained. Shader terrain already quantizes its world samples to 32 pixels/unit; feathered underlay geometry remains deliberately overlapped.
- Rendered and inspected updated ForestScalePreview beside actual Jerzy prefab body; screenshot out/forest-scale-preview.png. Test results out/forest-32ppu-tests.xml: 67/67 forest/import/lighting PASS, including native Sprite PPU and actual lossyScale assertions for props and edge/grass decorations. Main live forest smoke pending.
- Leave/re-enter forest to rebuild all views; no new seed required. Size differences should be represented by pixel dimensions/variants rather than non-unit Transform scale.


## Forest damp soil and moss — READY_FOR_SMOKE (2026-10-12)
- Floor shader reuses existing baked broad and detail mask channels to add irregular darker damp pockets and muted moss areas. Paths suppress moss, remain readable, and retain current route geometry.
- World sampling remains quantized to 32 pixels/unit. No new textures, texture samples, renderers or CPU refresh loops; a small amount of fragment arithmetic added. FPS impact has not been measured.
- Verification: out/forest-moisture.xml 67/67 forest/import/lighting PASS, including rendered floor shader regression checks; targeted diff check PASS. Live visual judgement of strength remains pending. No serialized assets or other chats' art modified.
- Smoke: view forest ground under party light and in dim areas; no new seed required.


## Forest undergrowth grouping — READY_FOR_SMOKE (2026-10-12)
- Edge decoration selection now uses the same world-space broad moisture noise as the forest floor mask: fungi/ferns favored in damp pockets, roots/deadwood in drier ones, shrubs shared. Smooth probability transition avoids hard biome borders.
- Reuses exactly the existing candidate slots and path/occupied-cell exclusions. Native 32 PPU/unit world scale unchanged. Noise sampled only during decoration build; no extra renderers or per-frame CPU work.
- Verification: out/forest-undergrowth-clusters.xml 28/28 ForestPrototypeTests PASS, including deterministic rebuild, collision/path constraints, reachability and native pixel scale checks. Targeted diff check PASS. Visual effect in live forest pending.
- Smoke: leave/re-enter forest; fresh seed unnecessary.


## Fallen mossy log — READY_FOR_SMOKE (2026-10-12)
- Generated and inspected mossy hollow log source; production Aseprite derivative 64x24 px at native32PPU = 2x.75 units. Source preserved. Exact original, paths and prompt in FOREST_FALLEN_LOG_ART.md.
- Sixth edge-detail variant replaces part of dry deadwood slots. Same candidate count, occupied/path exclusions and lighting/fog registration; no collider, LoS blocker, animation or new per-frame CPU update. Profile/authoring wired.
- Added log to saved ForestScalePreview; rendered and visually checked alongside actual Jerzy body, matching pixel scale. Screenshot out/forest-scale-preview.png. out/forest-fallen-log.xml 30/30 forest/import tests PASS, including native32PPU checks, deterministic rebuild and navigation constraints. Targeted diff check PASS. Live dark forest visibility/FPS unmeasured.
- Re-enter forest to rebuild decorations; fresh seed unnecessary.


## Small mossy stones — READY_FOR_SMOKE (2026-10-12)
- Added forest-small-stone-32ppu.png, Aseprite 24x18 derivative of existing forest-boulder-v1.png. Source unchanged, provenance in FOREST_ROCK_STUMP_ART.md. Native32PPU, no world scaling; visible stone ~ankle height when rendered beside Jerzy.
- Seventh sparse edge-detail variant favors damp patches, replacing existing candidates. Same count/path/occupancy exclusion; no collision, LoS or per-frame updates. Profile/authoring wired.
- Updated saved ForestScalePreview and inspected actual render alongside character. out/forest-small-stones.xml 30/30 forest/import tests PASS including native density and navigation constraints. Targeted diff check PASS. Live forest appearance/FPS pending.
- Re-enter forest to rebuild decoration; no new seed necessary.


## Forest contact grounding — READY_FOR_SMOKE (2026-10-12)
- Existing forest surface mask alpha now bakes small elliptical contact shade at static path-obstacle bases (interior trees, rocks, stumps and other registered props). Minimum .7 multiplier, smooth falloff, overlapping areas use minimum rather than multiplying into black holes. RGB path/moisture channels unchanged.
- Floor shader consumes alpha from its existing mask lookup; no added textures, samples or renderers. Bake only when existing static obstacle topology changes. Dynamic actor occupancy does not rebake. Existing world32 sampling retained; collisions/navigation unchanged.
- Verification: out/forest-contact-shade.xml 67/67 forest/import/lighting PASS. GPU readback checks shaded bases and bounds on shaded area, existing cache invariants and shader render test retained. Inspected test render; saved out/forest-contact-shade-preview.png. Actual forest props under torchlight still require live smoke; FPS not measured. Targeted diff check PASS.
- Re-enter forest to rebuild mask; no new seed necessary.


## Overgrown ruins gravestone — READY_FOR_SMOKE (2026-10-12)
- Generated and inspected mossy weathered gravestone; Aseprite production derivative32x40, native32PPU, alpha-base grounded pivot. Original and exact prompt in FOREST_GRAVESTONE_ART.md. Compared rendered sprite beside actual Jerzy body in updated ForestScalePreview.
- Added object.forest.gravestone.prototype / DO_Forest_Gravestone_Prototype. NativePixelScale, circular .2-unit base, one logical cell, no LoS blocking, indestructible; no loot/rest or interaction behavior. Replaces ruins boulder slot beside existing pillar/stump, retaining total obstacle count. Other room templates unchanged.
- Pool importer includes the new definition in matching authoring order. First run found import pool ordering drift (30/31); fixed ordering and reran full forest/import suite: out/forest-gravestone-final.xml31/31 PASS. Real prefab collider/rebind/native density test, ruins-only required anchor and ten-seed all-cell reachability PASS. Targeted diff check PASS.
- Copied only new definition+meta, owned ruins template, forest definition, and preview assets from validation mirror. Live torchlight appearance/FPS unmeasured.
- Smoke: generate NEW forest instance; optional Pochloniete ruiny room contains one gravestone. Existing generated dungeon placements remain unchanged.


## Hollow old tree variant — READY_FOR_SMOKE (2026-10-12)
- Added sparse gnarled hollow tree sprite, generated source preserved and Aseprite64x96 production derivative at native32PPU, Point, grounded alpha base. Exact source/prompt in FOREST_HOLLOW_TREE_ART.md.
- Third presentation variant bound to both forest profile and actual tree object definition. Deterministic selector: hollow1/12, dead1/12, crooked2/12, dense8/12. Missing third sprite falls back to dense tree, preserving older profiles. No extra trees/renderers, size randomization, collider or LoS changes.
- Updated layout of saved ForestScalePreview to accommodate separate tree comparison; inspected render beside Jerzy. Lower visible trunk remains narrow, spreading decorative roots do not define collision. Live forest alignment/torchlight appearance remains pending.
- out/forest-hollow-tree.xml32/32 forest/import tests PASS, including new rarity/native texture dimensions/fallback test, real obstacle binding and navigation tests. Targeted diff check PASS. Copied only owned tree definition/profile and preview assets from validation mirror. No FPS measurement.
- Re-enter forest to rebuild views; no fresh seed needed.


## Interior prop undergrowth — READY_FOR_SMOKE (2026-10-12)
- Existing boundary detail slots define a fixed renderer budget. Roughly one third may relocate to clear neighboring cells around static obstacles inside polans. Stable hash ordering and minimum two-cell distance between relocated details; same layout after rebuild.
- Paths retain one-cell neighbor clearance; occupied cells excluded. Adjacent static obstacle bases may now host foliage, but unrelated occupied neighbors remain excluded. Long fallen logs revert to compact roots beside obstacles to avoid visual overlap with trunks. Native32PPU and colliders unchanged.
- No new art/assets, renderers or per-frame updates. Candidate selection happens during existing decoration rebuild; no measured FPS result. Live appearance pending.
- Verification: out/forest-interior-undergrowth.xml32/32 forest/import tests PASS. Extended regression checks nonzero interior placement, identical detail count before/after and deterministic repeat, alongside existing navigation/pixel density tests. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary.


## Forest camp bedroll — READY_FOR_SMOKE (2026-10-12)
- Added one torn bedroll to the abandoned camp beside cold campfire and broken crate. Camp-only required authored anchor; no movement/LoS blocking, destruction, loot or interaction.
- Built-in imagegen source preserved; Aseprite production derivative 40×48 at native 32 PPU, scale 1. Prompt/provenance: FOREST_BEDROLL_ART.md.
- Ground sorting uses the existing flat-obstacle presentation band, below actors. Scale preview inspected beside Jerzy; updated saved ForestScalePreview scene and material. No new per-frame effects or FPS claim.
- Verification: out/forest-bedroll-verified.xml 33/33 forest/import tests PASS, including camp-only placement, native scale, disabled colliders, ground sorting flag and ten-seed reachability. Scale render inspected beside Jerzy; targeted diff check PASS. Live forest visual smoke remains pending.
- Generate a fresh forest instance to see new authored camp placement. No changes to existing saved instances.


## Forest base undergrowth — READY_FOR_SMOKE (2026-10-12)
- Existing detail slots beside static obstacles now select compact plants: fungi/ferns in wet patches, roots/low shrubs in dry patches. Boundary slots retain the existing wider assortment.
- Centers pulled 0.18 world-cell widths toward neighboring bases, capped to 0.3 widths from their own cell center. Fixed neighbor iteration preserves deterministic layouts. Scale remains native 32 PPU; no new sprites, colliders or renderer budget. Runs during existing decoration rebuild, no per-frame effect.
- out/forest-base-undergrowth.xml: 33/33 forest/import tests PASS. Extended existing regression checks compact species near bases, proximity and unchanged native scale, with existing count/determinism/path clearance and ten-seed reachability checks. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary. Live visual smoke and FPS measurement not performed.


## Forest moisture ground cover — READY_FOR_SMOKE (2026-10-12)
- Grass sprites now omit trodden visual path cells. Existing low-frequency clustering remains; the shared soil/undergrowth moisture field controls density and tint. Dry patches expose more litter and use muted ochre, wet patches use subdued green.
- Dry cluster threshold 0.48, wet 0.36 (previous universal 0.36), so no extra candidate slots or renderers. Native 32 PPU, scale 1, no new assets, shader layers, colliders or per-frame effects. Calculated during existing decoration rebuild.
- Verification: out/forest-moisture-cover.xml 33/33 forest/import tests PASS. Extended ground-cover regression checks nonzero sparse grass, no grass on path cells and retained alpha range, alongside existing determinism, occupancy, native scale and ten-seed reachability checks. Targeted diff check PASS.
- Re-enter forest to rebuild visuals; fresh seed unnecessary. Live visual smoke and FPS measurement pending.


## Forest rotten stump variant — READY_FOR_SMOKE (2026-10-12)
- Added alternate hollow rotten stump with split rim and moss to the existing stump definition. Base and alternate selected evenly by stable position; tree-specific weighted rarity remains unchanged. Missing prop variant falls back to its base sprite.
- Built-in imagegen original preserved; Aseprite 40×40 production sprite, Point, native 32 PPU/scale 1. Prompt/provenance FOREST_ROTTEN_STUMP_ART.md. Same 0.22 collider radius, tactical footprint, LoS flags and renderer count.
- Saved ForestScalePreview includes alternate beside Jerzy; render inspected and copied to out/forest-scale-preview.png. Synced only owned stump definition and preview scene/material from validation mirror.
- out/forest-rotten-stump.xml: 34/34 forest/import tests PASS. New test checks both appearances across twelve positions, repeat binding, native scale, texture density and identical collider width. Existing path/placement regression suite passes. Targeted diff check PASS. Live forest visual smoke/FPS measurement not performed.
- Re-enter forest to rebuild views; no fresh seed required.


## Forest fractured boulder variant — READY_FOR_SMOKE (2026-10-12)
- Added lower, flat cracked mossy boulder as an alternate on the existing boulder definition. Stable equal base/alternate choice uses the existing prop selector. No additional obstacles or renderers; radius 0.32, footprint and LoS blocking unchanged.
- Built-in imagegen original preserved; Aseprite production 48×36, Point, native 32 PPU/world scale 1. Ground pivot fitted to alpha base. Prompt/provenance FOREST_FRACTURED_BOULDER_ART.md.
- Updated saved ForestScalePreview and inspected comparison beside Jerzy and original boulder. Synced only owned boulder definition and preview scene/material from validation mirror.
- out/forest-fractured-boulder.xml: 35/35 forest/import tests PASS. Existing variant regression parameterized for stump and boulder, checking both appearances, repeat binding, source dimensions, native scale and identical collision. Targeted diff check PASS.
- Re-enter forest to rebuild views; no fresh seed needed. Live forest appearance and FPS measurement pending.


## Forest dry shrub — READY_FOR_SMOKE (2026-10-12)
- Added low dry thorn shrub as optional DryShrub vegetation-profile sprite. Roughly half of low shrub selections in dry moisture patches use it with muted brown tint; wet patches retain green shrubs. Existing slot budget, path clearance and collider-free ground sorting retained. Missing new sprite falls back to previous green shrub behavior.
- Built-in imagegen original preserved; Aseprite production 40×24, Point, native 32 PPU/scale 1. Prompt/provenance FOREST_DRY_SHRUB_ART.md. No additional renderers or per-frame effects.
- Updated saved ForestScalePreview and inspected new shrub beside Jerzy; synced only owned vegetation profile and preview scene/material from validation mirror.
- out/forest-dry-shrub.xml: 35/35 forest/import tests PASS. Extended existing ground-cover regression checks authored sprite dimensions/PPU; existing count, deterministic layout, path/occupancy clearance, no-collider and native renderer scale checks pass. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary. Live visual smoke and FPS measurement pending.


## Forest ten-variant visual pass — READY_FOR_SMOKE (2026-10-12)
- Completed ten new appearances in one continuous pass: shelf fungi, broad fern, tangled roots, forked branch, hollow fallen log, angular small stone, green bramble, sedge grass, charred stump and lichen boulder.
- Eight optional ground-detail variant arrays preserve old profiles through empty/null fallback; independently mixed stable hash selects base or alternate without changing slot selection, renderer budget, moisture zones, path clearance or native scale. Dry shrub retains its own dry-patch selection.
- Stump/boulder now each have two alternates plus base; existing stable prop selector, circular radii 0.22/0.32, footprint and LoS flags unchanged. No new objects or per-frame effects; crown fading remains disabled.
- Built-in imagegen originals retained. Aseprite crop/resize pipeline removes excess transparent framing while preserving aspect ratio, produces 24–64px-wide sprites at Point/native 32 PPU/world scale 1. Exact ten prompts, crops, dimensions and provenance: FOREST_TEN_VARIANTS_ART.md.
- Created saved ForestTenVariantsPreview scene and menu Godmist/Content/Pass 1b/Forest Ten Variants Preview; render out/forest-ten-variants-preview.png inspected with Jerzy beside every new sprite. Only owned profile, stump/boulder definitions and new preview scene/materials synced from validation mirror. Verified 10/10 importer GUIDs referenced by shipped assets.
- out/forest-ten-variants.xml: 36/36 forest/import tests PASS. New checks cover all eight detail bindings/dimensions/Point/32PPU, selectable base/alternates and null/empty/missing-entry fallback. Actual map rebuild uses new variants. Extended prop checks exercise all three appearances at 36 positions with identical native scale, collider width and repeat binding; existing occupancy, paths, deterministic budget and ten-seed reachability pass. Targeted diff check PASS.
- Re-enter forest to rebuild presentation; no fresh seed required. Live forest visual smoke and FPS measurement not performed.


## Forest moisture detail palette — READY_FOR_SMOKE (2026-10-12)
- Existing shrubs, ferns, fungi, roots, logs and stones now blend between muted dry ochre/brown and their cool wet palette using the same broad moisture field as soil/ground cover. Thorn shrubs retain their brown appearance. No per-sprite random colour noise.
- Rebuild-only colour calculation; unchanged native 32 PPU, scale 1, placement, paths, collision and renderer count. No new serialized assets or save changes.
- Verification: out/forest-moisture-details.xml 36/36 forest/import tests PASS; targeted diff check and final self-review PASS. Live visual appearance and FPS measurement not performed.
- Re-enter forest to rebuild presentation; no new seed required. Smoke: compare undergrowth against adjacent wet/dry ground and check path readability.


## Forest upright plant grounding — READY_FOR_SMOKE (2026-10-12)
- Shrubs, ferns and fungi now align sprite bottom bounds to a shared local ground baseline (slot centre minus 0.25 cell width). Their height no longer changes where the base is placed. Flat wood/roots/stones retain centre alignment; native 32 PPU, scale 1, sprite variants and moisture palette retained.
- Existing slot selection, collider-free decorations, paths and renderer count unchanged. Rebuild-only positioning; no per-frame updates, assets or save changes.
- Verification: out/forest-plant-baseline.xml 36/36 forest/import tests PASS; targeted diff check and self-review PASS. Live visual verification/FPS not performed.
- Re-enter forest; no fresh seed required. Smoke: compare plant bases beside trunks and check path readability.


## Forest glade compositions — READY_FOR_SMOKE (2026-10-12)
- Roots glade now authors two stumps; hollow glade two mossy boulders. Same two-prop budget. Existing abandoned camp and ruins retain their unique sets. Other glades keep mixed props.
- Authoring selects a first focus with room for another prop where possible, then places subsequent props near that fixed focus. Existing centre-cross, difficult-terrain, tree spacing and 3x3 footprint clearance retained. Updated only four room assets: roots, hollow, long_glade and overgrown_ruins.
- Existing interior detail slots prefer one stable obstacle focus per room; same relocation limit and renderer budget. Broad patches share dominant fungi/fern or roots/deadwood/log species, with occasional accents; moisture tint, native 32 PPU/scale 1, path exclusions and collider-free decorations retained. Rebuild-only work; no crown fading or per-frame work added.
- Added menu Godmist/Content/Pass 1b/Forest Composition Audit: renders four authored room layouts through actual map decoration builder, plus native prop sprites. Temporary audit objects/materials cleaned up. Unlit placement audit deliberately excludes gameplay fog, ground shader and lighting; it is not a playable preview or FPS measurement.
- Verification: out/forest-composition-verified.xml 36/36 forest/import tests PASS. Expanded existing multi-seed test checks exact themed prop count, definitions and pair spacing; ten-seed reachability and deterministic render budget regressions pass. All four placement renders inspected. Targeted diff check and final self-review PASS.
- A newly generated forest instance is needed for changed authored placements. Existing instances receive presentation/species changes on re-entry. Live camera/fog/light appearance and FPS remain manual smoke: enter a new forest, inspect roots/hollow/ruins/camp and traversal around props.


## 2026-10-12 — pierwszy przeciwnik Lasu: Wściekły Wilk

Dodano definicję wilka, skalowanie statystyk, trzy umiejętności (ugryzienie z krwawieniem, doskok ze wzrostem szybkości, leczenie od średniego ataku), encounter dwóch wilków i punkty patrolowe w pokojach normalnych oraz opcjonalnych. Korytarze wykluczono z losowych encounterów. Kieł wilka wypada pojedynczo z szansą 10%. Las nadal nie jest dostępny w ofertach.

Grafiki, animacje i ikony pozostawiono do podłączenia przez osobny czat. Pozostałych sześciu przeciwników, pełny lootbag Lasu i bossa nie obejmuje ten etap. Autorowanie: menu `Godmist/Content/Pass 1b/Configure Forest Wolf Gameplay`; narzędzie zachowuje pola prezentacji. Spawny wymagają wygenerowania nowego lochu.

Walidacja w izolowanej kopii Unity: 62/62 testy EditMode (ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, CombatRecoverySystemTests), w tym 10 seedów generatora. Do sprawdzenia w PlayMode: tempo walki, zachowanie patrolu i prezentacja po dostarczeniu grafik. Nie wykonano pomiaru FPS.


## 2026-10-12 — Bandyta Rzezimieszek — READY_FOR_SMOKE

Drugi przeciwnik Lasu: statystyki i skalowanie z content_pass_1b.xlsx (Przeciwnicy, A4:V4), Bandycki Nóż (krwawienie 25%, 75% ataku przez 3 tury po trafieniu), Zasadzka (niewidzialność i +10 pp. krytyka przez 2 tury), Cios w Plecy (+50% obrażeń od tyłu). Koszty/celność według X16:AA16. Dodano parametr efektu rearAttackDamageBonus (domyślnie zero), uwzględniany przez wykonanie, ocenę AI i tooltip. Zachowano istniejącą regułę znikania niewidzialności po ofensywnym skillu.

Encounter dwóch bandytów losuje się obok par wilków, korzystając z istniejących dwóch punktów Enemy na polanach. Grafiki, animacje, ikony i ich pola nie były ustawiane. Nie dodano zastępczych dropów; pełny leśny lootbag jest nadal osobnym etapem. Las pozostaje wyłączony z ofert.

Autorowanie: Godmist/Content/Pass 1b/Configure Forest Bandit Gameplay. Wymagany nowy loch dla nowych encounterów. 178/178 testów EditMode w izolowanej kopii Unity: ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, ScoutContentTests, SkillExecutionSystemTests i SimpleCombatAi. Sprawdzono premię od tyłu, ujawnienie po ataku, koszt i brak zasobu, pominięcie krwawienia po pudle, zachowanie AI oraz 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode i FPS nie sprawdzane; smoke: patrole, walka z bandytami i prezentacja po podłączeniu artu. Pozostało pięciu zwykłych przeciwników, pełny lootbag i boss.


## 2026-10-12 — Bandyta Strzelec — READY_FOR_SMOKE

Dodano trzeciego zwykłego przeciwnika Lasu według content_pass_1b.xlsx (Przeciwnicy A5:V5, X17:AA17): Wystrzał z Kuszy (0 MP/65 AP, zasięg 9, 20% -7 szybkości przez 2 tury po trafieniu), Bełt Przebijający (25 MP/65 AP, 70% obrażeń, kierunkowy pas 2×8, 80% krwawienia 90% ataku przez 2 tury po trafieniu), Flara (50 MP/35 AP, 70% -15 celności przez 2 tury i 30% ogłuszenia na 1 turę wrogów, usunięcie ogłuszenia sojuszników). AI wycenia zdjęcie istniejącego statusu z sojusznika. Dodano encounter mieszany strzelec + rzezimieszek, wspólne punkty patrolowe na polanach; pozostałe encountery zachowane.

Grafiki, animacje i ikony pozostawiono osobnemu czatowi; pól prezentacji nie nadpisywano. Nie dodano zastępczego lootu. Las nadal niedostępny z ofert. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Shooter Gameplay. Wymagana nowa generacja lochu.

Walidacja w izolowanej kopii Unity: 132/132 testy EditMode (ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi), w tym trafianie kilku celów bez sojusznika i celu poza kierunkiem, flara rozdzielająca strony, AI ratujące ogłuszonego sojusznika, AI strzelające bez podejścia oraz 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode/FPS nie mierzono. Smoke: nowy Las, patrol mieszany, LoS i prezentacja po dostarczeniu artu. Pozostało czterech zwykłych przeciwników, pełny lootbag i boss.


## 2026-10-12 — Elfi Łucznik — READY_FOR_SMOKE

Dodano czwartego zwykłego przeciwnika Lasu: statystyki/skaling według content_pass_1b.xlsx (Przeciwnicy A6:V6), Elficka Strzała (0 MP/60 AP, celność 98, zasięg 9), Deszcz Strzał (30 MP/70 AP, celność 77, 60% obrażeń na kwadrat 3×3 w zasięgu 7, 50% szansy na -8% uniku przez 3 tury po trafieniu), Sokole Oko (50 MP/30 AP, przez 3 tury ataki nie chybiają i +5% obrażeń za pole odległości). Odległość liczona jako maksimum różnic osi, zgodnie z kwadratowym zasięgiem. Dodano UniqueBuffType.Hawkeye na końcu enum (9), zachowując wcześniejsze wartości. Istniejący payload i lifecycle obsługują zapis/czas działania bez nowego formatu save. Premia działa na obrażenia fizyczne/magiczne, nie na DoT; zgodne wykonanie, podgląd i ocena obrażeń AI. AI wycenia buff, gdy nie jest aktywny.

Nowy encounter dwóch łuczników i 15% dropu 1–2 uszu elfa (alchemia, pospolity, wartość 17 według Spis innych przedmiotów.xlsx A319:H319). Pozostałe encountery zachowane, oferta Lasu nadal wyłączona. Grafiki, animacje i ikony pozostawiono osobnemu czatowi. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Archer Gameplay; potrzebna nowa generacja lochu.

163/163 testy EditMode w izolowanej kopii Unity: ForestArcherTests, ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi, CombatSkillPreviewCalculatorTests, CombatDamageSystemTests i GameSessionSave. Sprawdzono realny atak i podgląd przy dystansie 1/5/9, gwarantowane trafienie, wygaśnięcie po 3 turach, brak premii DoT, kwadrat 3×3 bez sojusznika, przygotowanie buffa przez AI, brak ponownego nakładania oraz 10 seedów generatora. Ustabilizowano ziarno losowania istniejącego testu trafień Bełtu Przebijającego. Self-review i targeted diff check zakończone. PlayMode/FPS nie weryfikowane. Pozostało trzech zwykłych przeciwników (drzewo, kłusownik, druid), pełny lootbag i boss.


## 2026-10-12 — Elfi Kłusownik — READY_FOR_SMOKE

Piąty zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A7:V7, X19:AA19): Sztylet z Zębami (0 MP/60 AP, celność 69, 40% krwawienia 60% ataku przez 2 tury po trafieniu), Pułapka na Niedźwiedzie (25 MP/30 AP, zasięg 1, po wejściu wroga unieruchomienie przez 2 tury oraz 70% krwawienia 50% ataku przez 3 tury), Worek z Trutką (55 MP/70 AP, zasięg 3, obszar 2×2, 80% zatrucia 60% ataku przez 3 tury). Drop wspólnego LT_Forest_Elf: 15% na 1–2 uszu elfa. Nowy encounter kłusownik + łucznik.

Dodano opt-in TriggersOnEntry w SummonedObjectDefinition (domyślnie false). Takie obiekty nie blokują ruchu i nie dostają własnej tury; pozostają do aktywacji, zniszczenia lub końca walki. Osobny nieserializowany callback CombatSession współistnieje z powiadomieniami pułapek lochu i run bossa. Wejście przeciwnika uruchamia efekty tylko na nim i usuwa obiekt; sojusznicy nie aktywują. Podstawowy ruch zatrzymuje ścieżkę po unieruchomieniu; istniejące powiadomienie wejścia obsługuje również przemieszczenia. Umieszczanie pod jednostką zabronione. Przyjęto limit jednej aktywnej pułapki na kłusownika; AI stawia ją na wolnym sąsiednim polu w kierunku widocznego przeciwnika i nie zastępuje aktywnej własnej pułapki.

Poprawiono Deszcz Strzał łucznika: relativeRange 0–2 oznacza bok 3 i pełne 3×3 (wcześniej 0–1 dawało 2×2). Regresja sprawdza obie przeciwległe krawędzie. Grafiki, animacje i ikony pozostawiono osobnemu czatowi. Las nadal wyłączony z ofert. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Poacher Gameplay. Nowe encountery wymagają nowego lochu.

152/152 testy EditMode w izolowanej kopii Unity: ForestPoacherTests, ForestArcherTests, ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi, SummonedObject, CombatMovement, CombatCharge, CombatTeleport i GameSessionSave. Pokrycie: przerwanie ścieżki na pułapce, jednorazowe zużycie, przepuszczenie sojusznika, brak automatycznych efektów tury, odrzucenie zajętego/zbyt dalekiego pola bez pobrania kosztu, obszar 2×2, AI i 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode/FPS nie weryfikowane; smoke: nowy Las, mieszany patrol elfów, stawianie i aktywowanie pułapki, prezentacja po dostarczeniu artu. Pozostało dwóch zwykłych przeciwników (drzewo, druid), pełny lootbag i boss.


## 2026-10-12 — Zarażone Drzewo — READY_FOR_SMOKE

Szósty zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A2:V2, X14:AA14). HP 450, atak 30–35, szybkość 30, tag Nieumarły; wzrost i odporności z arkusza. Ciemne Korzenie: 5 MP/70 AP, magiczne obrażenia, zasięg 5, 35% zatrucia (55% ataku, 3 tury). Zniewalające Korzenie: 45 MP/65 AP, zasięg 5, unieruchomienie 2 tury oraz 70% na -12% uniku przez 3 tury. Rozwój Plagi: 25 MP/30 AP, celność 88, fizyczne obrażenia x0.6 wszystkim zatrutym przeciwnikom w zasięgu 9, przedłużenie każdego aktywnego zatrucia o 1 turę. Nowy encounter dwóch drzew, bez indywidualnego dropu zgodnie z arkuszem; pełny lootbag pozostaje osobnym zadaniem.

Dodano opcjonalny RequiredStatusId w trybie targetowania (domyślnie pusty), filtr wspólny dla AI/targetowania oraz walidację wykonania. ExtendStatusDuration dopisany na końcu enumu (42); przedłuża istniejące aktywne efekty czasowe bez zmiany obrażeń, źródeł i stosów oraz bez wskrzeszania wygasłych efektów. AI uwzględnia wartość przedłużenia zatrucia. Grafiki, animacje i ikony pozostawiono osobnemu czatowi, istniejących pól prezentacji nie nadpisano. Las nadal wyłączony z ofert.

183/183 testy EditMode w izolowanej kopii Unity (out/forest-tree-final.xml): ForestTree/Poacher/Archer/Shooter/Bandit/Wolf, ForestPrototype/ImportedAssets, SimpleCombatAi, CombatTargeting, StatusEffect, GameSessionSave. Sprawdzono koszty, dane, selekcję zatrutych, pomijanie zdrowych/sojuszników/zbyt dalekich celów, brak kosztu przy błędnym celu, przedłużanie stosów, brak przywracania wygasłej trucizny, dwie tury unieruchomienia i użycie Plagi przez AI bez podchodzenia. Autorowanie zakończone kodem 0, self-review i targeted diff check wykonane. Smoke: świeżo wygenerowany Las, encounter drzew, korzenie i Plaga; PlayMode/FPS nie weryfikowane. Menu: Godmist/Content/Pass 1b/Configure Forest Tree Gameplay. Pozostał Elfi Druid, pełny lootbag i boss.


## 2026-10-12 — Elfi Druid — READY_FOR_SMOKE

Siódmy zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A8:V8, X20:AC20): HP 200, atak 20–30, szybkość 42, wzrost i odporności z arkusza. Gniew Natury: 5 MP/50 AP, zasięg 6, magiczne obrażenia x0.5 za każdą własną żywą narośl; bez narośli nie można wykonać i nie pobiera kosztu. Trująca Narośl: 40 MP/50 AP, zasięg 6, 15% maks. HP rzucającego, co własną turę 80% zatrucia 60% ataku przez 3 tury przeciwnikom wokół. Łagodzące Korzenie: 30 MP/50 AP, zasięg 6, narośl z 15% maks. HP, co własną turę leczenie pobliskich sojuszników za 90% średniego ataku. Przyjęto „wokół” jako osiem sąsiednich pól (zasięg 1); pełny koszt AP pulsu pozwala na jedną aktywację w turze. Brak czasu życia i sztucznego limitu liczby narośli; pozostają do zniszczenia, usunięcia przywołań właściciela lub końca walki.

Narośle używają istniejącego systemu przywołanych jednostek: są nieruchome, sterowane przez AI, mają normalne HP i można je normalnie atakować. Opt-in pola SkillEffectData dla trwałości, nieruchomości, dziedziczenia HP/ataku/szybkości i grupy przywołań mają domyślnie wyłączone działanie. Gniew Natury liczy grupę forest.growth wyłącznie właściciela, pomija zniszczone narośle i zachowuje zgodność realnych obrażeń, AI i preview. Statystyki narośli kopiowane są w chwili przywołania. AI druida wybiera wolne pole obok widocznych przeciwników lub rannych sojuszników; AI narośli nie szuka ścieżki, gdy nie ma celu. Drop LT_Forest_Elf: 15% na 1–2 uszu elfa. Nowy encounter druid + kłusownik. Dotychczasowe assety prezentacji nie nadpisane; sprite'y/animacje/ikony nadal wykonuje osobny czat. Las nadal wyłączony z ofert.

212/212 testy EditMode w izolowanej kopii Unity (out/forest-druid-final.xml): ForestDruid/Tree/Poacher/Archer/Shooter/Bandit/Wolf, ForestPrototype/ImportedAssets, SimpleCombatAi, CombatTargeting, StatusEffect, Summon, CombatMovement, CombatSkillPreviewCalculator, GameSessionSave. Sprawdzono dane i koszty, 15% HP i jego zachowanie po odświeżeniu statystyk, brak ruchu/wygaśnięcia, normalne niszczenie narośli, liczenie własnych żywych narośli i zgodność preview z obrażeniami, truciznę/leczenie tylko właściwej strony w zasięgu 1, użyteczne stawianie przez AI i Gniew Natury po przywołaniu, bezczynność nieruchomej narośli bez celu, odrzucenie zajętego/zbyt dalekiego pola bez kosztów. Poprawiono zaokrąglenie HP (400 x 15% = 60). Autorowanie zakończone kodem 0, self-review i targeted diff check wykonane. Menu: Godmist/Content/Pass 1b/Configure Forest Druid Gameplay. Smoke wymaga świeżego Lasu; sprawdzić mieszany patrol, przywołania i ich tury, niszczenie narośli oraz prezentację po podłączeniu artu. PlayMode/FPS nie weryfikowane. Zwykli przeciwnicy passa 1b są gotowi; pozostaje pełny lootbag i boss.


## 2026-10-12 — Loot Lasu — READY_FOR_SMOKE

Podłączono cztery torby Lasu (zasoby, broń, pancerze, galduryty), osobny DLC_Forest_Loot oraz tabele przeciwników i skrzyń dla poziomów 1–10 i 11–20. Źródło: content_pass_1b.xlsx / Lootbagi D4:E32. Trzy pule surowców 100/70/30% wybierają różne materiały; szyszki 100% + niezależne 40%. Żywicę usunięto z puli zgodnie z korektą użytkownika: stary przedmiot już nie dropi. Ostatnia pula daje 40% na 1–2 uszu elfa. Indywidualne dropy wilków/elfów zachowane. Nowe czerwony/żółty pył energetyczny: ceny 13/74 i rzadkość pospolita/niepospolita według Spis innych przedmiotów.xlsx A307:H308; istniejąca Szyszka i jej ikona zachowane. Brak zmian grafiki/ikon.

Sprzęt korzysta ze wspólnego generatora i części tierów 5/15, z jakością Słaba/Zwykła/Doskonała; finalny wymagany poziom zachowuje dotychczasową korektę jakości (-3/0/+3). Galduryty leśnej torby generują się na poziomie 20: nowe opcjonalne FixedGalduryteLevel w LootBagPoolEntryData (0 domyślnie) zachowuje działanie istniejących tabel i save'ów. Szanse wypadania toreb przyjęto jak w istniejącym lochu: przeciwnik 50% zasoby, 2% broń, 2% pancerz, na 11–20 dodatkowo 1% galduryty; skrzynie 2–3 torby zasobów i 4/4/2% pozostałe torby. ForestEnemiesSetup ponownie podłącza leśny katalog po reautorowaniu prototypu. Boss references w katalogu zachowywane; torba bossa i unikaty pozostają etapem implementacji Enta. Las nadal wyłączony z ofert.

137/137 testów EditMode w izolowanej kopii Unity (out/forest-loot-verified.xml): ForestLootTests, LootTableSystemTests, LootCollectionServiceTests, ForestPrototypeTests, ForestImportedAssetsTests, DungeonEncounterReward, Galduryte i GameSessionSave. Kontrola tabel i referencji katalogu, 800 rzeczywistych losowań torby zasobów na granicach 1/10/11/20, zakresy ilości i różne surowce, szyszki, brak żywicy, kompletne 3 części i jakość sprzętu tieru 5/15, rzeczywiste galduryty poziomu 20 z efektami. Autorowanie kod 0; self-review i targeted diff check zakończone. Menu: Godmist/Content/Pass 1b/Configure Forest Loot. Smoke: świeżo wygenerowany Las, nagrody po walce i otwieranie toreb, skrzynie po dodaniu ich kontentu; PlayMode nie sprawdzano. Prerolowane nagrody starego lochu nie zmieniają się. Pozostał Zarażony Ent 2.0, jego torba i unikaty.

## 2026-10-12 — Zarażony Ent 2.0 — READY_FOR_SMOKE

Podłączono Enta jako bossa ostatniego pokoju Lasu, dwa kompletne zestawy umiejętności i przemianę przy <=50% HP po zakończeniu całego skilla (łącznie z kontratakami/powtórzeniami). Przemiana zmienia definicję, aktywny zestaw skilli i prezentację, usuwa własną Twardą Korę, zachowuje debuffy i nie odnawia HP/MP/PA. Korzenie: unieruchomienie i osłabienie wszystkich odporności na 2 tury; Zapłon: linia 6 pól. Twarda Kora: -40 szybkości. Demoniczna Zaraza: limit 5, raz na użycie skilla na cel; obrażenia okresowe/teren raz na turę celu; 0,5% maks. HP i 0,5 MP dla Enta za stos, do końca walki. Sanktuarium: połowa dostępnych pól areny, ostrzeżenie do następnej tury Enta, potem ogień przez 3 jego tury; zachowane osiągalne schronienia, brak ponownego rzucenia podczas ostrzeżenia/ognia, obrażenia terenu najwyżej raz na turę celu. Stan fazy, zarazy i terenu przeżywa łączenie walk; bez osobnej tury dla każdego pola ognia i bez przeliczania co klatkę.

Boss dropi własną torbę, z czterema unikatami poziomów 10/20: Praelficka Glewia Wojenna, Liściasta Tunika, Palec Enta, Elficki Młot Życia. Podłączono ich pasywki, Żywioł lasu, zasoby/galduryty i tabele obu przedziałów poziomów. Zarażona żywica pozostaje wycofana. Menu odtwarzania danych: Godmist/Content/Pass 1b/Configure Forest Ent Gameplay. Las nadal wyłączony z normalnych ofert.

Weryfikacja: 245/245 testów EditMode w izolowanej kopii Unity, out/forest-ent-verified.xml. Obejmuje pełne generowanie i placement Lasu, boss loot i rzeczywiste otwieranie toreb, cztery pasywki, dwie fazy i aktywny loadout AI, linię Zapłonu (6. pole trafione, 7. i boczne pominięte), koszty, zarazę, ostrzeżenie/ogień/schronienia, śmierć bossa, sprzątanie i łączenie walk, istniejących bossów katakumb, wykonanie skilli, status UI, loot oraz save. Final self-review i targeted git diff --check zakończone. PlayMode/FPS nie weryfikowane.

Smoke: wygenerować świeży Las, dotrzeć do końcowej areny, przekroczyć 50% HP, sprawdzić skille II fazy, ostrzeżenie i trzy tury ognia, zarazę oraz nagrodę po zabiciu. Stare instancje lochu/prerolowany loot nie aktualizują się. Art wykonuje osobny czat: CD_Forest_InfectedEnt oraz CD_Forest_InfectedEntBurning, sześć ikon skilli i cztery ikony unikatów. Opcjonalne TransformationFrames na definicji I fazy odtwarzane są jednokrotnie przy przemianie (8 FPS). Istniejących pól prezentacji nie nadpisano; nowe definicje wymagają podłączenia artu. Ostrzeżenie i ogień mają tymczasowe znaczniki 32x32/32 PPU. Podpalanie drzew otaczających arenę pozostaje osobnym etapem wizualnym.
## 2026-10-12 — Fallback grafiki ciał — READY_FOR_SMOKE

DungeonEnemyBodyView zachowuje pierwszeństwo DeadSprite/importowanej grafiki corpse; przy jej braku korzysta z importowanej animacji idle, pierwszej niepustej IdleAnimationFrames, następnie VisualSprite. Usunięto przypadek niewidocznych zwłok postaci mającej wyłącznie klatki animacji. Tymczasowy fallback zachowuje dotychczasowy obrót/tint; docelowe DeadSprite zastępuje go automatycznie. Bez jakiejkolwiek podłączonej grafiki definicja nadal wymaga artu; aktualne leśne definicje na dysku mają puste pola prezentacji. Nie zmieniano assetów/artu drugiego czatu. 23/23 DungeonEnemyBodyTests EditMode, out/forest-corpse-fallback.xml: animowany fallback, priorytet dedykowanych zwłok ghula, interakcja, loot/persistence ciał. Self-review/diff check zakończone, PlayMode nie sprawdzano. Smoke: zabić moba z podłączonym idle i pustym DeadSprite, sprawdzić widoczność oraz zebranie łupu. Docelowe grafiki ciał podłączyć do DeadSprite każdej definicji.
## 2026-10-12 — Integracja zapisu Lasu — READY_FOR_SMOKE

Użytkownik potwierdził działanie Lasu w Play Mode; zgłoszony osobno brak prezentacji ciał obsłużono fallbackiem idle. Dodano integracyjny test rzeczywistego wygenerowanego Lasu dla poziomów 10 i 20: pokonany Ent, wyczyszczony encounter, flaga wygenerowania zwłok, pozycja i złoto ciała, ID torby bossa i dokładne ID jej zawartości zachowują się przez dwa kolejne cykle snapshot -> JSON -> restore z głównym katalogiem. Bez duplikowania ciał lub ponownego losowania zawartości. 77/77 EditMode PASS: ForestEntBossTests, DungeonEnemyBodyTests, DungeonOfferTests (out/forest-integration.xml). Nie wykryto błędu wymagającego zmiany runtime; zmieniono tylko testy i dokumentację. Self-review oraz targeted diff check zakończone. Nie włączono ofert Lasu; finalny art i podpalanie drzew areny pozostają kolejnymi etapami. Nie wykonano nowego pomiaru FPS.
## 2026-10-12 — Zielony ogień na arenie Enta — READY_FOR_SMOKE

Przemiana Enta rejestruje jego pierwotny pokój areny (przed przemieszczeniem do innych pomieszczeń); identyfikator przechodzi razem ze stanem bossa przez merge walk. DungeonCombatCoordinator odświeża stan prezentacji co 0,2 s. Po rozpoczęciu II fazy dwa najbliższe pasy drzew granicznych oraz indywidualne pnie przy arenie otrzymują zielony ogień; inne pokoje i inne piętra/mapy są pomijane. Animacja shaderowa jest skwantowana do 5 FPS, w skali 32 PPU. Jeden wspólny materiał, nakładki tworzone tylko przy pierwszym zapłonie, bez skanowania wszystkich drzew co klatkę. Nakładki należą do tych samych właścicieli fog/elevation co drzewa; nie ujawniają niezbadanych obszarów. Wykluczone z odbiorników oświetlenia i cieni kontaktowych; brak nowych Light2D/particle systems, colliderów, obrażeń i zmian nawigacji. Obrażenia nadal należą do Sanktuarium. Efekt gaśnie po zakończeniu/usunięciu walki; przebudowa mapy odtwarza go z aktywnego stanu II fazy. Nie jest trwałym spaleniem geometrii zapisywanym po ekspedycji. Shader ładowany z Resources/Godmist/EntArenaFire, aby był dostępny także w buildzie.

97/97 EditMode PASS w izolowanej kopii Unity: EntArenaFireTests, ForestEntBossTests, ForestPrototypeTests i DungeonLighting (out/ent-arena-fire.xml). Sprawdzono przejście fazy, zachowanie pierwotnego pokoju, ograniczenie do areny, brak zmian liczby colliderów/casterów i rewizji nawigacji, brak duplikatów, ukrywanie przez fog, zakończenie walki, ponowną aktywację oraz rebuild. Shader bez błędów kompilacji; render kontrolny obejrzany (out/ent-arena-fire-preview.png). Przy okazji rebuild w EditMode używa DestroyImmediate dla starych ścian zamiast niepoprawnego Destroy. Self-review i targeted diff check zakończone. Nie zmieniano artu mobków/ikon ani assetów drugiego czatu. Smoke: w świeżym lub istniejącym Lasie doprowadzić Enta do II fazy, obejrzeć zielony ogień na granicy areny i jego wygaszenie po walce. Nowego pomiaru FPS ani PlayMode nie wykonano. Ten wpis zastępuje wcześniejszy status podpalania drzew jako niezrealizowanego etapu.

## 2026-10-12 — Las: oferty i Danger Level — READY_FOR_SMOKE

Podłączono do GC_MainContentCatalog dwie produkcyjne definicje: dungeon.forest.expedition (zwykłe wyprawy, bez Enta, poziom względem drużyny) i dungeon.forest.boss_expedition (Zarażony Ent, poziomy 10/20, odblokowanie przez istniejący Danger Level). Obie korzystają z obecnego layoutu, prezentacji, przeciwników, obiektów i tabel łupu Lasu. Prototyp zachowuje ID i pozostaje poza tablicą ofert, więc stare zapisy nie wymagają migracji. Zwykła wyprawa Lasu obniża jego Danger o 30 i podnosi inne dostępne biomy o 10–20 od poziomu 8; ukończenie wyprawy Enta resetuje Danger Lasu do zera. Ponowne 100 odblokowuje nową wyprawę na Enta. Kamienie milowe 10/20 losują także Las.

Menu odtworzenia: Godmist/Content/Pass 1b/Configure Forest Expeditions. Ponowne autorowanie prototypu odświeża istniejące warianty, zachowując zwykłą wyprawę bez bossa. Bez zmiany formatu zapisu i bez nowych systemów runtime. Test starej kampanii poprawiono tak, aby weryfikował zapis faktycznie wylosowanego biomu zamiast zakładać wyłącznie Katakumby.

111/111 EditMode PASS w izolowanej kopii Unity: ForestExpeditionTests, DungeonOfferTests, ForestImportedAssetsTests, ForestPrototypeTests, ForestEntBossTests, ForestLootTests (out/forest-offers-final.xml). Zweryfikowano rzeczywiste assety katalogu, generowanie obu wariantów na poziomach 10/20, brak Enta w zwykłej wyprawie, ofertę przy 100 Danger, zapis/odczyt oferty z prawdziwą drużyną, reset i ponowne odblokowanie bossa, idempotencję autorowania. Self-review zakończony. PlayMode oraz nowy pomiar FPS: NOT RUN.

Smoke: nowa tablica ofert powinna zawierać Las obok Katakumb. Zachowane oferty starego zapisu pozostają do następnego odświeżenia po powrocie z wyprawy. W konsoli setdanger forest 100 powinno udostępnić osobną ofertę Enta; zwykła wyprawa nie ma bossa, a bossowa ma Enta na końcu. Sprawdzić nagrodę i reset Danger po ukończeniu oraz zachowanie oferty po save/load. Ręczne podpinanie w Inspectorze nie jest potrzebne. Ten wpis zastępuje wcześniejszy status Lasu jako wyłączonego z ofert.


## 2026-10-12 — Las: skrzynie i skrytki — READY_FOR_SMOKE

Dodano DO_Forest_Chest i DO_Forest_Cache do puli prototypu oraz obu produkcyjnych wypraw. Oba pojemniki używają istniejących tabel LT_Forest_Chest_1_10_Production / LT_Forest_Chest_11_20_Production poprzez DLC_Forest_Loot; dobór następuje według poziomu instancji lochu. Loot pozostaje zgodny z tabelami: zawartość 2–3 toreb zasobów oraz szanse na wyposażenie/galduryty, wydawane bezpośrednio zgodnie z RollChestLoot. Bez przywracania wycofanej żywicy. Pojemniki są odblokowane i korzystają z istniejącej prezentacji/interakcji skrzyni.

Jedna kotwa skrzyni na szablon zwykłej polany (35%, wspólna dystrybucja forest.chests); jedna skrytka w każdym bocznym pokoju Optional (100%). Bez pojemników na starcie, w korytarzach i na arenie Enta. Kotwy są przy obrzeżu, z dojściem, odsunięte od dekoracji i kotew przeciwników. Przeniesiono wyłącznie definicje pojemników, referencje pul i nowe kotwy; art mobków oraz ich prezentacja nie były modyfikowane. Menu: Godmist/Content/Pass 1b/Configure Forest Containers. Ponowne autorowanie prototypu odtwarza kotwy i odświeża wyprawy; loader prototypu uwzględnia nowe definicje.

65/65 EditMode PASS w izolowanej kopii Unity: ForestContainerTests, ForestExpeditionTests, ForestImportedAssetsTests, ForestPrototypeTests, ForestLootTests (out/forest-containers-verified.xml). Nowe testy obejmują 18 wygenerowanych lochów na poziomach 1/10/11/20, obie wyprawy, łup i wolne pole dojścia, idempotencję autorowania oraz dwa kolejne cykle JSON save/load zachowujące zawartość i opróżnione pojemniki. Istniejący test liczby drzew ograniczono do kotew przeszkód, aby nie traktował skrzyń jako drzew. Self-review i porównanie przeniesionych assetów z testowaną kopią zakończone. PlayMode: NOT RUN.

Smoke: wejść do świeżego Lasu, sprawdzić skrzynie na polanach i skrytki w bocznych pokojach, podejść i otworzyć E, odebrać łup, zapisać/wczytać i potwierdzić brak odnowienia zawartości. Dotychczas zapisany loch zachowuje istniejący zestaw obiektów; nowe kotwy dotyczą nowo wygenerowanych wypraw. Bez ręcznego podpinania w Inspectorze.


### 2026-10-13 — Duch Umarłego Króla 2.0 — READY_FOR_SMOKE

Dwie fazy i mechaniki z bossy2.0.xlsx podłączone do produkcyjnego Króla; zachowane oferty L10/L20 i nagrody. 181 unikalnych testów EditMode PASS; szczegóły i smoke w UNITY_HANDOFF.md. Grafiki fazy 2/rycerza zastępcze, PlayMode NOT RUN.


## 2026-10-13 — Grafiki Skażonego Lasu — READY_FOR_SMOKE

9 wariantów aktorów, 8 kierunków idle/walk, reakcje, ciała, przemiana Enta, ikony i VFX podłączone do istniejących systemów. Pozy v4 prowizoryczne; kompletna korekta ikon ognia podłączona. Bez zmian generatora/mechanik/balansu. Szczegóły w UNITY_HANDOFF.md. Wizualny smoke wyprawy pozostaje do wykonania.

Końcowa weryfikacja integracji: 116/116 PASS (EditMode + automatyczny EnterPlayMode), out/forest-art-integrated2.xml. Self-review zakończony; zgodność głównych assetów i GUID-ów z testowaną kopią potwierdzona. Bez commit/push.


## 2026-10-13 — Finalny import Lasu finish-v1 — READY_FOR_SMOKE

Finalne poses-v5/VFX-v2, przeliczone reakcje/ciała/przemiana podłączone. Większe canvasy i pivoty zachowane; przemiana S chroniona przed nadpisaniem przez pozę ataku. 149/149 PASS, także automatyczne odtwarzanie w PlayMode. Self-review i zgodność z testowaną kopią potwierdzone. Wizualny smoke wyprawy pozostaje do wykonania; szczegóły UNITY_HANDOFF.md. Poprzedni status prowizorycznych v4 nieaktualny.


## 2026-10-13 — Statyczne światła Lasu — READY_FOR_SMOKE

Pierwszy pass: istniejące paleniska i pochodnie przy pniach/ruinach. 32 PPU, wspólny limit8, 5 Hz/cache/fog zachowane; bez zmiany nawigacji. 42/42 EditMode PASS, self-review zakończony. Lampiony wymagają osobnego artu. Smoke i pomiar FPS jeszcze niewykonane; szczegóły UNITY_HANDOFF.md.


### Ent encounter presentation — 2026-10-13
READY_FOR_SMOKE. Boss encounters use visible living participant silhouettes for initial/Home framing, including actual imported sprite bounds. Automatic boss framing is not pushed toward the centre of the entire floor; manual pan still uses floor limits. Home refreshes participant positions; skill focus restores the preceding view. Ordinary encounter framing is preserved.
CombatHudView owns a runtime boss panel below the timeline using the bound HUD typography. It displays name, HP/shield and Ent phase I/II, updates on health/definition/state changes, hides on death/leaving combat/hidden unit, and has no raycast targets. Skill banner sits below it, and action/boss framing reserves this HUD space. No serialized HUD setup required.
Ent arena gains up to 20 static native-32-PPU ground root/deadwood accents in broken arcs, leaving the centre/entrance clear. They follow existing fog, sorting and lighting ownership; no collision, shadow casters or traversal revisions are added. Forest exits retain their usable stone marker, now moss-coloured with two native root accents rather than a blue dungeon appearance. No new artwork was generated and no boss balance, phase thresholds or green fire rules changed.
Verification: isolated Unity compilation and 108/108 combat/camera/Ent regression tests PASS (out/ent-presentation-check.xml); final UI lifecycle/exit-decoration verification 57/57 PASS (out/ent-hud-final.xml). Boss HUD render inspected separately; full gameplay framing/arena remains a manual smoke check. No new FPS measurement. Smoke: enter a fresh Ent fight, inspect centered view and HP, press Home after movement, cast at range and check restore, cross 50% HP, verify phase II plus green arena fire, defeat boss and confirm bar disappears. Existing saves do not need migration.

## Forest gateway and Ent presentation follow-up — 2026-10-13
Status: READY_FOR_SMOKE. Supersedes the earlier tinted stair-marker presentation.
- Forest transitions use two existing dead-tree sprites at native 32 PPU, with the stair marker hidden. The exit/start interaction is relocated to a clear north/south edge of its room; interaction rules remain unchanged. Catacombs retain their original transitions.
- Ent presentation recognizes its definition and phase state even without the expedition IsBoss flag, for the boss HUD and camera framing.
- Movement-range fill opacity reduced; outer boundary retained. Skill targeting is unchanged.
- Arena roots/deadwood grouped into three patches, leaving the central lane clear.
Validation: isolated Unity EditMode run, 55/55 passed (out/forest-gateway.xml), no compiler errors. Covers production HUD binding without IsBoss, native gateway scale and boundary placement, arena decoration lifecycle, camera framing, grid feedback and Ent mechanics.
Game smoke still required: fresh forest expedition, gateway visibility/interaction at both room edges, Ent HUD and camera, movement overlay readability, phase-II transition. No manual scene/prefab wiring required. Gateway is a natural passage between dead trees using existing artwork.
## Ent canopy clearance / retired boss HP panel — 2026-10-13
Supersedes the separate boss health HUD: removed its refresh, layout reservation and generated UI. Unit health inspection remains unchanged.
Arena boundary vegetation now receives a one-time outward correction against the union of arena cell silhouette envelopes (4 units wide, from -0.5 to +3.5 units vertically at unit cell size). Checked the final south idle art: both phases have about 3.1 units visible height at 32 PPU. Original collision, navigation, fog edge ownership and green-fire registration remain unchanged; no per-frame canopy fading.
Smoke: re-enter/rebuild the forest arena, move Ent to its north/south/side edges and check both phases. Existing instantiated maps require rebuilding to receive this presentation change.
Validation: READY_FOR_SMOKE; final isolated EditMode run 88/88 passed (out/ent-clearance-final.xml), no C# compiler errors; cardinal canopy clearance, distant tree stability, HUD, camera, arena details and fire regressions covered. Full in-game visual smoke pending.

## Forest readability pass — 2026-10-13
- Visual paths now blend segments between adjacent path cells instead of stamping isolated circles; variable width and obstacle clearance retained.
- Native-size low shrubs mark impassable boundary seams, using existing boundary fog ownership and sorting. No blockers or traversal topology added.
- Boundary crowns receive a build-time clearance correction around authored enemy exploration homes. No per-frame character/canopy fading. This protects initial homes, not arbitrary later patrol/combat positions behind individual in-room trees.
- Forest props use the authored pivot for projected contact rather than transparent canvas bottom; existing batched contact-shadow mesh adds stepped translucent black base patches for forest objects and registered low props. Catacombs keep their existing treatment.
Smoke required: rebuild/re-enter forest; inspect rock base/shadow, small props on grass, connected trail forks, boundary gaps and enemy homes. Existing native 32 PPU scale unchanged. No manual prefab wiring, commits or source-art modifications.
Validation: READY_FOR_SMOKE; final isolated EditMode run 90/90 passed (out/forest-readability-final.xml), no C# compiler errors. Full game visual verification pending.


## Warrior Rage on miss — 2026-10-13
READY_FOR_SMOKE. Missed and dodged attacks grant base Rage through the existing resource rules and feedback, including the full-pool decay flag. Damage-triggered talent bonuses still require damage; normal hits are not counted twice, resource blocks/caps and DoT exclusions remain. Both damage-resolution overloads covered; resource-dependent warrior stats refresh. PROJECT_CONTEXT and default configuration wording updated without changing persisted IDs or serialized state. Baseline repro: seven failures; final compilation and EditMode regression 176/176 PASS, zero skips, including 15 new cases and production Cut execution. Self-review/whitespace PASS. Manual game smoke pending; no commit/push. Evidence: LocalHandoffs/rage-on-miss-20261013; UNITY_HANDOFF.md.

## Forest enemy navigation and dedicated light art — 2026-10-13
Implementation replaces earlier crown-only mitigation.
- Confirmed initialization bug: vegetation originally consulted FogFloor before the first fog binding. Bootstrap now supplies the current presentation floor before Rebuild; arena and enemy-home clearance work on first entry.
- Forest spawn owns full floor data. Unsafe legacy positions and excessive saved offsets are repaired into free interior cells; Ent starts as a stationary arena guard. No save schema change.
- Ent movement and forced displacement share the arena clearance rule, using its stored ArenaRoomId when available; player movement is unchanged. Regular forest enemy patrol planning checks cell clearance and physical standing space; saved routes are revalidated and unreachable stops are skipped.
- A single optional silhouette renderer per visible living enemy stays readable through a foreground canopy. Canopy coverage is indexed when the map is built; per-frame work is a cell lookup, not scanning/fading tree renderers. Fog/actor visibility and death suppress the silhouette.
- Replaced forest tree-mounted Catacombs fixtures with dedicated freestanding forest brazier art; hearth flame comes from the forest artwork too. Native 32x56 canvas, 32 PPU, point filter, grounded 4px pivot. Forest flame masking uses its own artwork; Catacombs keep the authored mask.
- Art generated using imagegen and resized/exported with Aseprite. Final resource: Assets/Resources/Godmist/ForestArt/Fixtures/forest-brazier-32ppu.png. Native source and prompt: output/forest-brazier-native.aseprite and output/forest-brazier-handoff.txt. Scale preview: out/forest-brazier-scale.png (checked beside Jerzy).
Smoke required: re-enter a saved forest (especially Ent previously outside arena), test both boss phases at all arena edges, chase/patrol near trees and obstacles, check hidden enemies do not show silhouettes, and inspect brazier/hearth art in actual lighting. No FPS benchmark or manual full-game smoke has been performed. No manual prefab wiring required. No commits or pushes.Validation: READY_FOR_SMOKE. Main targeted run 151/151 passed; navigation/combat compatibility rerun 68/68 passed; final fixture/mask/contact/navigation checks 15/15 passed. No C# or shader compilation errors in these runs; focused diff checks clean. Runtime scale preview inspected. Earlier interim failures were fixed: Runtime assembly dependency, test-only missing sort baselines, missing encounter placement in the legacy-position fixture, and the updated private spawn signature in an existing regression test. Actual in-game appearance and FPS still require smoke verification.
## Masked canopy contours and forest contact ordering — 2026-10-13
Supersedes the full gold silhouette from the preceding pass.
- Enemy overlay clips against alpha from up to four indexed foreground canopy sprites. Uncovered pixels receive no overlay. Covered interior is faint (0.12 of tint alpha), with a stronger one-pixel contour; neutral tint replaces gold. Fog/death eligibility is retained.
- Canopy index stores renderers and their effective sort groups; runtime query checks actual body/canopy bounds and actual foreground order. Textures and registration matrices upload only when the foreground set changes. No whole-forest scan, canopy fading or mask-camera render pass.
- Native forest sprites are unpacked (the only project SpriteAtlas is the UI atlas); registration handles their world transform and flip without changing sprite scale/pivots.
- Forest contact-shadow mesh is above the ground-cover lane, below standing objects. Rounded scanline bands replace rectangular pads; unchanged geometry still reuses the mesh.
- GPU regression renders a half-covered actor: exposed pixels retain their original red, canopy-covered interior remains faint. Contact regression checks ordering over litter, rounded geometry and cache reuse. Rendering test compares readback colours in linear space.
Smoke: rebuild/re-enter forest; inspect partial/full tree occlusion, ordinary exposed enemies, rock bases on grass, fog transitions and both Ent phases. No sprite/art regeneration or manual prefab wiring required. Full-game visual/FPS smoke remains pending.
Validation: READY_FOR_SMOKE; 87/87 targeted EditMode tests passed (out/forest-canopy-verified.xml), including actual GPU pixel readback, enemy visibility, contact mesh cache and forest regressions. No C# or shader compilation errors; focused diff clean. New shader/test metas retained from Unity import.

## Ent transformation on next own turn — 2026-10-13
Status: READY_FOR_SMOKE. Supersedes the immediate post-damage/skill phase switch.
At <=50% HP, CheckPhases latches PhaseTwoPending after the whole resolving skill. Definition, model and skillset stay in phase I until Ent's next BeginTurn. Other units' turns cannot consume the pending change. Crossing the threshold during Ent's current turn waits for its following turn. Healing after crossing does not cancel the latch; death does. Pending state survives runtime owner rebind/merge without a save-format change.
At the next own turn, phase/skillset change once and the driver explicitly starts the existing authored transformation on the borrowed exploration view or regular combat view. Six frames at the authored 5 FPS reserve 1.2 seconds of the existing action-presentation gate before AI can act. Old poses are cleared and movement/idle writes cannot overwrite the clip; S-only transformation keeps logical facing for the phase-II directional idle. No HP, resource, skill balance or transformation artwork changes; normal turn-start AP refill is retained.
Validation: initial combat/presentation regressions 64/64 PASS; final Ent/art suite 43/43 PASS including two EnterPlayMode runtime tests. The new runtime test uses production-style map visibility, borrowed PF_ExplorationCharacter, real turn-start handler and presentation gate; checks old phase during another unit's turn, immediate frame 0 at own turn, all-frame duration, movement/pose protection and phase-II W idle. Also checks multi-hit completion, pending healing/death/rebind, once-only change, buffs, plague and arena mechanics. No compiler errors; focused diff checks clean.
Smoke: start a fresh Ent fight; cross 50% HP during a party action, confirm phase I persists until Ent's turn, then observe full transformation before its next action and green arena fire. Existing already-transformed battles are not rolled back. No manual prefab/scene wiring or art reimport needed. Full gameplay smoke remains pending; no commit/push.

## Forest combat/HUD polish — 2026-10-13
Status: READY_FOR_SMOKE.
- Resource and vitals health bars correct the existing slider_track slice multiplier to 2 at runtime. The authored 12/6px border now fits the 6/3px fill-area insets; resource colour/value, neutral bars and other frame artwork stay unchanged. Checked the real CombatVitals prefab with Rage, Mana and Momentum at empty/partial/full values, plus resource rebinding.
- Bottom vitals and fallback inspect preserve the fractional MaxHealth until the shared formatter, removing the 492/563 versus 492/562 discrepancy. Slider maximum remains the exact 562.5 value; no stat/balance change.
- Connected range contours use a narrower, quieter falloff without internal cell seams. Fill opacity, target pixel corners and logical selection remain unchanged.
- Forest soft fog now feathers at missing terrain, as well as unknown neighbours, removing an unfeathered rectangular floor edge between sparse boundary trees. Masonry fallback and discovered/visible cell sets remain unchanged. This targets the terrain perimeter, not a blanket claim that every possible occlusion artifact is eliminated.
- Forest ordinary fights use the existing participant-bounds camera overview, as boss fights already did. Initial frame/Home fit visible living bodies with HUD-safe margins; manual view persistence and action restore remain.
- Combat enemies gain a small static lower red foot arc, sharing visual-state ownership. Hidden/dead/player units and actors leaving combat have no marker. LateUpdate rechecks after awareness/fog/depth updates; geometry is created once and reused, without per-frame mesh rebuilding.
Validation: first focused HUD/camera/fog/lighting run 155/155 PASS (out/forest-hud-pass.xml); final forest/navigation/seamless/visual-state regression 125/125 PASS (out/forest-hud-regression.xml). These suites overlap and are not a unique total. No C# compiler errors. Actual prefab render inspected: out/forest-hud-vitals-preview.png. Owned-file mirror comparison and final self-review complete; no save schema, art, scene/prefab edits, commits or pushes.
Smoke: reopen/rebuild forest, compare both HP labels, inspect Rage at 0/20/50 and switching leader, check thin range outline and connected edges, follow known/unknown terrain perimeter, start an ordinary bandit fight and use Home/manual zoom, verify enemy arcs hide in fog/on death/after combat. Full-game visual and FPS verification remains pending. No manual Editor wiring required.


## Loot window presentation — 2026-10-13
READY_FOR_SMOKE. Current gold-32 artwork and compact currency tooltip replace the legacy coin and generic reward wording. Fixed entirely ellipsized header text and restored the shared gothic slot background beneath item rarity accents. Gold collects on left click only; right/middle clicks do not collect it. Scoped Unity authoring updates only PF_UIW_DungeonLootWindow, with Godmist/UI/Configure loot window for reauthoring; no shared base-prefab or loot/save changes. Editor renders checked before/after; compilation and EditMode regression 163/163 PASS, zero skips, including four new cases. Own diff/self-review PASS. Fresh Play Mode required for the prefab instance; manual gameplay smoke and player build NOT RUN. Evidence: LocalHandoffs/loot-ui-20261013; UNITY_HANDOFF.md. No commit/push.
- Final compact-currency tooltip follow-up: width follows content instead of reserving the full generic tooltip width; final tooltip regression 109/109 PASS (compact-final.xml).


## Ground corpse layering and restrained Ent arena fire — 2026-10-13
Status: READY_FOR_SMOKE.
Confirmed the wolf uses the final dedicated corpse PNG. The visual defect was the fixed corpse sorting order 9, which overpainted actors in northern/large rooms where actor depth orders are negative. Bound bodies now use the map ground-object lane and layer; hover outlines stay one lane below them. Bodies register with the existing ground-lighting stream, including its reserved outline band, and unregister when rebound/destroyed. They therefore receive terrain lighting instead of remaining brightly unlit. Native 32-PPU dedicated corpse sprites use scale 1 instead of the legacy fallback scale 0.7; their original centered pivots, rotation and colour remain unchanged. Missing-art fallback is retained only for existing definitions without dedicated corpse art.
The arena-fire shader now renders narrower native-pixel tongues near roots/trunks and sparse lower-branch patches, with reduced brightness/opacity so original trees remain visible. Core2D sprite setup correctly applies flip and SpriteRenderer colour, including remembered-fog attenuation. The existing shared material, 5 FPS animation, phase/arena/fog ownership, overlay count and termination remain. No new lights, particles, colliders, terrain damage or gameplay/balance changes.
Validation: initial focused suite 75/75 PASS; final art/Ent/body/lighting/canopy suite 118/118 PASS (out/forest-corpses-fire-accepted.xml), including the existing two EnterPlayMode transformation tests. Strengthened corpse GPU regression 1/1 PASS (out/corpse-layer-render-final.xml): native Jerzy/wolf art actually overlaps at negative actor depth; old order 9 is an explicit failing visual control, while the new ground lane matches the proper alpha blend. Native hero artwork has near-opaque alpha and test readbacks account for sRGB8/alpha quantization; no hero art was altered. Fire GPU regressions verify normal/flipped trees, retained original pixels, restrained green and dimmed fog tint. Preview images inspected in out/forest-corpses-fire-previews (corpse-legacy-layer, corpse-native-ground-layer, ent-tree-baseline, ent-tree-burning and arena preview). No C# or shader compilation errors; final owned-file mirror/hash and whitespace checks passed; self-review completed.
Smoke: re-enter/rebuild the forest to recreate body views. Walk over/beside killed wolves and other corpses, check native scale, grounded lighting, hover/loot and fog visibility. Trigger Ent phase II in a new fight, inspect green flames on roots/branches and the remaining canopy texture, then end combat and verify fire disappears. Full-game visual/FPS smoke remains pending. No manual prefab/scene wiring, art regeneration, source-pack edits, save migration, commit or push.


## Enemy awareness indicator follows sprite height — 2026-10-13
Status: READY_FOR_SMOKE.
Replaced the fixed feet-relative awareness offset with placement above the current BodyRenderer world bounds. The whole diamond clears the sprite by 2 world-art pixels (32 PPU), accounting for sprite pivot, body/actor scale, animation frame and elevation offset. Horizontal centering follows the body bounds; existing canvas depth is preserved. Updates immediately when shown and on the existing LateUpdate visibility refresh. Existing fog, life-state, combat hiding, awareness timing and save behavior remain unchanged; no serialized prefab/scene changes or new wiring.
Validation: old code failed all six new positioning cases; final EditMode regression 75/75 PASS (LocalHandoffs/awareness-head-anchor-20261013/accepted.xml), covering varied heights/pivots/scales and native ghoul, rabid-wolf and Dead King walk frames across all eight directions. Self-review, scoped whitespace and mirror/source checks PASS. Initial retest exposed an EditMode-only SendMessage lifecycle assertion in the new test harness; replaced it with the project's existing direct reflection invocation pattern, without suppressing logs. Actual native-art preview inspected (preview.png): ghoul, rabid wolf and Dead King at their distinct sprite heights. No commit/push.
Smoke: let a ghoul, wolf and a taller enemy notice the party; verify the diamond stays fully above their sprite while walking/turning and still hides when awareness ends, the enemy is hidden/dead or combat starts. No manual Editor setup; gameplay smoke pending.


## Catacombs imported art across expedition variants — 2026-10-13
Status: READY_FOR_SMOKE.
Confirmed imported trap/low-wall/railing art was already available, but DungeonSandboxBootstrap and DungeonObjectSpawner enabled it only for the exact ID dungeon.catacombs.expedition. Boss, procedural and other Catacombs definitions therefore retained old prefab visuals. Both owners now select the existing art pipeline from DungeonTheme.Catacombs. Bootstrap ground-prop lighting uses that same biome decision; map configuration remains centralized before Rebuild, with structural preview/diagnostics restricted to actual asset-audit instances. Other themes keep their own art even if assigned the legacy expedition ID. No new resource lookup, source-art import, prefab/scene/definition edits or geometry algorithm changes.
Existing imported presentation covers trap states (spikes/gas/grave seal), low masonry, horizontal/vertical railings, stair/balcony presentation and the already-supported Catacombs props. No map/layout/save schema or trap/low-wall gameplay changes. Spawner cleanup uses DestroyImmediate outside Play Mode, supporting preview-scene rebuild/unload without illegal deferred destruction.
Validation: first integration 5/5 PASS; final focused regression 65/65 PASS, zero skips (out/catacombs-art-theme-verified.xml). Integration opens the shipped DungeonSandbox as a preview scene and exercises the real bootstrap configuration/map rebuild/spawner with normal, boss, procedural and traps-demo definitions; verifies imported textures, native 32 PPU/point filtering, one active trap visual, triggered gas-base state, low/railing wall types and unchanged traversal revision. Negative control is a Forest-themed definition using the legacy Catacombs expedition ID. Regression covers traps/conditions/combat, lighting cadence, low-wall actor sorting, authored collider profiles and surface foundations. Main/mirror scene, representative prefabs/resources and owned code hashes match. No C# or shader compiler errors; focused diff/whitespace/self-review complete.
Smoke: leave and re-enter Katakumby, especially the boss expedition. Inspect spikes, gas vent and grave seal before/after activation, low masonry and both railing directions, prop lighting and hover/fog. Saved geometry is retained; new dungeon generation is unnecessary for this presentation fix. Full-game visual smoke remains pending. No manual Editor wiring, commit or push.


## Gothic awareness indicator presentation — 2026-10-13
Status: READY_FOR_SMOKE.
The runtime marker uses the existing icon_button_normal Gothic artwork, rotated inside its unchanged 64×64 canvas, and the shared palette font, text and warning/negative accents. Progress is inset using the frame's authored sprite borders and drawn as a restrained tint; the full-colour frame stays untinted. Current sprite-height positioning, awareness/save logic and visibility gates remain. After explicit user approval, native PrefabUtility authoring assigned awarenessStyle on PF_ExplorationCharacter; the guarded copy verified exactly one added reference and no concurrent source change. Scoped menu Godmist/UI/Configure enemy awareness indicator reproduces the assignment. No new art, commit or push.
Validation: final production-prefab integration and focused suite 24/24 PASS (LocalHandoffs/awareness-gothic-style-20261013/integrated-final.xml), including shared artwork/font, inset geometry, tint, draw order, noninteractive graphics and sprite-height regressions. Other 54 visual/canopy regressions passed in the first run. Native-art preview inspected; source/mirror comparison, scoped whitespace check and final self-review PASS. Earlier new-test harness ambiguity in inherited OnPopulateMesh was fixed by selecting the VertexHelper overload, without suppressing logs. No manual Editor setup remains. Restart Play Mode for fresh prefab instances; gameplay smoke/player build pending; exact checks in UNITY_HANDOFF.md.


## Runtime TMP shortcut material fix — 2026-10-13
READY_FOR_SMOKE. Reproduced the exact MaterialReferenceManager.cs:526 exception on both icon-only E/Q combat buttons: shortcut creation overwrote a valid font material with null. Combat/save/contextual runtime labels now fall back to the selected font material while retaining valid authored shared materials; save style lookup excludes the newly-created hint. Before fix 5/9 new regression cases failed; final targeted TMP/HUD/action-layout/save-browser suite 81/81 PASS (out/tmp-shortcut-material-verified.xml). Scoped mirror/source hashes, whitespace and final self-review PASS. No serialized Unity state/package/font assets changed, commit or push. Restart Play Mode and inspect combat shortcuts/contextual hints/save-load hints and Console; manual smoke pending. Details in UNITY_HANDOFF.md.


## Dead King 2.0 art pack — 2026-10-13
ART_READY_FOR_REVIEW. output/dead-king-2-art-v1 + ZIP: phaseII/royal knight, 8-dir idle/movement/reactions, seven skill culmination poses/effects, dedicated corpses, portraits, 9 icons, ground markers and six-frame S transformation. Original King image_gen→Aseprite/shared256 palette method recovered and reused; 32PPU/standing scale preserved with explicit larger margins/pivots. 254 native PNGs (251 new,3 reused),84 exact mirrors, repeated PNG hash equality and134-frame Aseprite roundtrip PASS; ZIP verified. Source assets/runtime/Unity state unchanged. Integration and in-game smoke pending; mapping/checklist in pack UNITY_HANDOFF.md and project handoff.


## Dead King 2.0 art v2 — spectral redesign and shipped style — 2026-10-13
ART_READY_FOR_REVIEW, runtime integration pending. New separate output/dead-king-2-art-v2 + ZIP supersedes v1 for review, preserving every v1 native hash. PhaseII loses its burgundy cape and exposes luminous cold spectral ribs/arms/tails; Knight has ghost body/gaps between sparse armor and no solid boots. Redesign covers all directions/motion/poses/reactions/corpses/portraits and six-frame cape-dissolving transition. Recovered actual shipped party/catacombs method from original Jerzy/Alison/Wraith generation logs: builtin image_gen with native identity and shipped VFX references, proportional premultiplied BOX, alpha150, shared actor96 palette/no dithering, exact mirrors, separate compact VFX on measured current poses and Aseprite editable export. Nine icons corrected from cutout64 symbols to whole-canvas32x32 matching shipped skill icons. Transition uses both-phase palette to preserve burgundy until it dissolves. METHOD.md records exact source evidence.
Validation PASS:254 native RGBA sprites,84 exact mirrors,32PPU, no output clipping, distinct motion frames/baseline<=1px, dedicated corpses and exact transition endpoints; repeatable PNG hashes,134 Aseprite frames pixel-identical,12 production reference hashes unchanged,7 skill/2 character mappings complete, skill/status icons32, old v1 preserved and ZIP verified. Visual review covered native-scale comparison with Jerzy/Wraith/P1, shipped icons beside v2, reactions, bodies, portraits, transition and all VFX composites. Crowded motion sheets and old-tabard portrait rejected/corrected. No Unity code/serialized state/source-pack edits, gameplay/balance changes, CU, commit or push. Runtime import and in-game smoke NOT RUN; parameters/mapping/facing policy and checklist in output/dead-king-2-art-v2/UNITY_HANDOFF.md; review.html/preview/overview.png ready.


### Dead King v2 icon crop correction — 2026-10-13
Fixed unequal source-grid segmentation: actual icon rows end at398/797px on1254px sheet, not uniform thirds. Measured source layout/checksum and one-pixel internal-boundary inset prevent neighbour background bleed; corrected all9 icons, preserved approved VFX palette and all245 non-icon PNG hashes. Negative control reproduces old red strip; corrected wave has0 red pixels. Updated previews/manifest/review/ZIP; no Unity/runtime/source-image edits.


## Dead King 2.0 art v2 integration — 2026-10-14
READY_FOR_SMOKE. Final corrected v2 art is now imported and wired in production:254 native mappings/275 import profiles, phaseII and SpectralKnight8-dir idle/movement/hit/dodge, seven skills/compact separate VFX, separate corpses/portraits,32px icons/passive glossary entries and safe/dark ground markers. Existing P1 locomotion/legacy mappings retained. Runtime catalog supports optional per-layer pivots/offsets; target-head Command is attached to visible targets without freezing their animation and clears with presentation. S-only6x200ms transformation uses the existing one-shot frenzy event, preserves logical facing and blocks presentation until the complete clip; no HP threshold/phase timing/skillset/balance/reward/save changes. Native Unity authoring in isolated mirror, selectively merged presentation fields in13 assets plus real import profiles; unowned fields, GUIDs and concurrent hero-special catalog retained.
Final verified.xml and integrated.xml:64/64 PASS each, zero skips (LocalHandoffs/dead-king-art-import-20261013), including actual EnterPlayMode phase/target-marker tests and existing boss/pose/forest regressions. Main275 PNG/profile hashes,254 manifest mappings and nine owned sources match tested mirror; recent primary Editor log no C#/shader compile errors. Test-discovered corpse resolver naming fixed; test fixture closure/front-only invocation issues corrected without gameplay changes. Self-review/whitespace PASS. No CU, commit or push. Stop/restart Play Mode for fresh resource caches, then full in-game King fight smoke. No Inspector wiring needed. Details: LocalHandoffs/dead-king-art-import-20261013/HANDOFF.md; reproducible menu Godmist/Content/Dead King 2.0/Configure Approved Art.


## AI watchdog i Umarły Król — 2026-10-14

Zaimplementowano limit 8 s planowania/utkniętego oczekiwania AI z normalnym zakończeniem tury, ocenę warunków efektów i poprawkę spamu Zapachu Krwi. Próg 50% HP Króla jest zapamiętywany; faza II, istniejąca animacja i usunięcie własnych rycerzy następują na początku jego następnej tury. Aura Rozpaczy/Widmowy Szał są widoczne w efektach, z ikonami/opisem i odświeżaniem podglądu. Profile 1880 tekstur Lasu/Króla poprawne — bez zmian bitmap i serialized assets.

Kompilacja PASS; 186/186 testów PASS, 0 skip (`LocalHandoffs/combat-ai-king-fixes-20261014/acceptance.xml`). Self-review PASS. Bez wiring; uruchom świeży Play Mode. Smoke: umarlak buff→ruch→atak, Król 50%→własna tura→jednorazowy klip/nowe skille/zniknięcie rycerzy, otwarta lista efektów zmienia pasyw. Szczegóły: `LocalHandoffs/combat-ai-king-fixes-20261014/HANDOFF.md`. Manualny smoke produkcyjny NOT RUN. READY_FOR_SMOKE.


## Wskaźnik powalenia — 2026-10-14

Na życzenie użytkownika limit powalenia zmieniony z 5 na 3 własne tury. Czaszka nad trzema segmentami w stylu Godmist UI pokazuje rzeczywiste DownedTurnsRemaining, nad postacią i przy jej portrecie; ostatni segment czerwony, trafienia skracają licznik, podniesienie/śmierć i wyjście z walki ukrywają wskaźnik. Respektuje fog/elevation, bez raycastów w świecie i zmian prefabów. Natywny Editor zapisał wyłącznie DefaultIntegerValue/CurrentIntegerValue limitu na 3.

Kompilacja i 153/153 testów PASS, render UI sprawdzony (`LocalHandoffs/downed-three-segment-20261014/verified.xml`, `portrait-countdown-preview.png`). Self-review PASS. Bez ręcznego wiring; uruchomić świeży Play Mode i sprawdzić powalenie→3→2→1→śmierć oraz podniesienie. Szczegóły: `LocalHandoffs/downed-three-segment-20261014/HANDOFF.md`. READY_FOR_SMOKE, produkcyjny manualny smoke NOT RUN.


## Sprite czaszki w Godmist UI — 2026-10-14

Dodano nową ikonę `status_downed_skull` 32×32 do katalogu, atlasu i palety skina GodmistGothic (99 sprite’ów). Built-in image_gen z referencjami obecnego skina → Aseprite Lua → 23 kolory istniejącej palety / alpha 0–255. Wskaźnik powalenia używa Image zamiast własnego rysunku proceduralnego. Pozostałe 98 grafik, prefaby i limit 3 tur zachowane. Natywny authoring/import i kompilacja PASS; 19/19 testów PASS, render oceniony. Bez wiring — świeży Play Mode. READY_FOR_SMOKE; ręczna scena produkcyjna NOT RUN. Szczegóły i podgląd: `LocalHandoffs/downed-skull-sprite-20261014/HANDOFF.md`.

## Czytelność odliczania przy portrecie — 2026-10-14

Wskaźnik powalenia powiększony: panel do 104×60, kościana czaszka do 40×40, trzy większe segmenty. Dodano nieprzezroczyste tło Godmist UI i dopasowanie do portretów 64×64 oraz 128×128. Stary czerwony lifeStateMarker zasłaniający segmenty jest ukrywany wyłącznie podczas widocznego odliczania; znacznik śmierci działa nadal. Bez zmian zasad 3 tur, grafik, świata i serialized assets. Kompilacja i 65/65 testów PASS, render pełnych kart produkcyjnych sprawdzony. Bez wiring, świeży Play Mode; produkcyjny manualny smoke NOT RUN. READY_FOR_SMOKE. Szczegóły: LocalHandoffs/downed-portrait-readability-20261014/HANDOFF.md.

## Las: granice, światło Enta i odliczanie — 2026-10-14

READY_FOR_SMOKE. Paleniska lasu wybierają odsłonięte osiągalne położenie i respektują rzeczywistą kolejność koron. Ścieżki nie prowadzą do void/przez twarde ściany; wszystkie twarde krawędzie mają natywne poszycie. Podłoże uwzględnia odsunięte drzewa. Ogień areny dopasowany do żółtozielonego ognia grafiki fazy II; daje światło przez istniejący pipeline (maks. 8, 5 Hz, rozstaw źródeł, fog/lifecycle zachowane). Produkcyjna etykieta Powalony nie nachodzi na segmenty; wskaźniki świata mniejsze, z nieprzezroczystym tłem i kotwą przy aktorze. Bez zmian generacji, nawigacji, zasad walki, sprite’ów i serialized assets. Finalna kompilacja C#/shaderów i 113/113 EditMode PASS, render GPU i self-review PASS. Bez wiring; ponownie uruchomić Play Mode / wczytać las. Manualny smoke i FPS NOT RUN. Szczegóły: LocalHandoffs/forest-boundary-fire-downed-20261014/HANDOFF.md.

## Czytelność obiektów lasu — 2026-10-14

READY_FOR_SMOKE. Regularny pas krzaków zastąpiony natywną mieszanką krzaków/paproci z deterministyczną zmiennością położenia i odcienia. Drobne dekoracje i obiekty interaktywne lasu otrzymują istniejące cienie kontaktowe, z poprawioną kolejnością pod sprite’ami. Obecne shadery Floor/Body/Wall dodają oznaczonym propom dwupikselową, ciemną otoczkę z malejącą alpha i lekkie odcięcie tonu. Aktorzy, Katakumby, grunt i ściółka bez opt-in zachowują dotychczasowy rendering. Bez nowych rendererów obwódki, bitmap, zmian PPU/skali/colliderów/mapy i serialized assets. Kompilacja C#/shaderów i finalne 89/89 EditMode PASS; render natywnych grafik, cache cieni i self-review PASS. Bez wiring; ponownie uruchomić Play Mode/wczytać las. Produkcyjny smoke i FPS NOT RUN. Szczegóły: LocalHandoffs/forest-prop-readability-20261014/HANDOFF.md.

## Powalenie: tooltip i pozycja wskaźnika — 2026-10-14

READY_FOR_SMOKE. Jeden owner hovera portretu i odliczania; pojedynczy kompaktowy tooltip „Powalony / Pozostałe tury: N/3”, live update licznika i zachowanie przypiętych kart. Wskaźnik świata kotwiony do widocznej sylwetki zamiast canvasu. Dodano cztery zmierzone prostokąty alpha do istniejącego katalogu front-knockdown i odczyt bounds w CombatPoseResolver; bez runtime pixel readback, zmian PNG/canvasu/pivotów/32 PPU czy zasad powalenia. Kompilacja i finalne 144/144 testów PASS, render właściwej pozy powalenia i pojedynczego tooltipa sprawdzony. Bez wiring; świeży Play Mode dla cache póz. Manualny smoke w grze NOT RUN. Szczegóły: LocalHandoffs/downed-tooltip-anchor-20261014/HANDOFF.md.

## Szkielet Zabójca — dwie bazowe fazy S — 2026-10-14

ART_READY_FOR_REVIEW. output/skeleton-slayer-base-v1 zawiera dwie statyczne pozy S, PNG i edytowalne Aseprite, prompty, manifest i planszę ×1/×2 obok drużyny oraz Króla. Odtworzony proces drużyny/Katakumb: built-in image_gen z natywnymi referencjami, proporcjonalny premultiplied BOX, alfa150, wspólna paleta96 bez ditheringu. Faza II traci duże osłony barków i odsłania bursztynowy rdzeń; pierwszy zbyt podobny wariant poprawiono i zachowano jako WIP. Ciało100px/32PPU w obu fazach, canvas160×160 mieści młot bez zmniejszania postaci, wspólny pivot (80,16). Kontrole obu PNG PASS: brak ucięć, alfa0/255, wspólna skala/kotwa, roundtrip Aseprite identyczny, odtwarzalny eksport. Nie importowano do Unity; nie są to animacje. Ikony/pozostałe kierunki/gameplay należą do kolejnych etapów, kontrakt ikon zapisany według shipped32px skilli. Szczegóły: output/skeleton-slayer-base-v1/HANDOFF.md. Bez zmian runtime/serialized assets, CU, commit/push.

## Szkielet Zabójca — kierunki, ruch i reakcje — 2026-10-14

ART_READY_FOR_REVIEW. output/skeleton-slayer-motion-v1: obie fazy ×8 kierunków, 4-klatkowe idle/chód oraz osobne pozy trafienia/uniku; 176 natywnych PNG, 4 atlasy, 80 edytowalnych Aseprite i podglądy GIF/review.html. Zachowane bajtowo zaakceptowane bazy S/idle00 i zamrożona paleta96, 32PPU/canvas160/pivot(80,16). Stara metoda image_gen z referencjami → proporcjonalny premultiplied BOX/alfa150 → Aseprite; 17 pełnych promptów w prompts/index.json. Poprawiony SE fazy II, faktyczne przezroczyste granice komórek i szerszy pomiar stóp w uniku. PASS: wszystkie176 klatek bez ucięć, alfa0/255, podeszwy na y144, 66 dokładnych odbić, 4 różne klatki każdej pętli, wszystkie roundtripy/czasy/liczby klatek Aseprite i powtarzalne hashe PNG. Przegląd wizualny tablic kierunków/ruchu/reakcji zakończony. Nie importowano; gameplay, ataki/VFX/ikony/ciała/przemiana pozostają kolejnymi etapami. Kontrole artu nie zastępują przyszłego sprawdzenia w grze. Szczegóły: output/skeleton-slayer-motion-v1/HANDOFF.md. Bez zmian Unity/runtime, CU, commit/push.

## Szkielet Zabójca — skille, osobne VFX i ikony — 2026-10-14

ART_READY_FOR_REVIEW. output/skeleton-slayer-skills-v1: 56 kierunkowych póz kulminacyjnych dla sześciu skilli (Młot osobno przygotowanie/uderzenie), 36 osobnych VFX/markerów i8 pełnych ikon32×32 skilli/pasywów. Built-in image_gen ze shipped referencjami Katakumb/drużyny, proporcjonalny premultiplied BOX/alfa150, zamrożona paleta96 aktorów i wspólna256 VFX/ikon, bez ditheringu. Canvas256/pivot(128,64)/32PPU zachowuje skalę ciała i zapas na podniesiony młot. 56 par jawnej rejestracji VFX, pola/efekty podłoża z osobną kolejnością. Poprawiony NE Ciosu, osłona corrected bez czerwonych pikseli, łuk Rozgromu dopasowany odbiciem; ikony wycięte po zmierzonych granicach z inset2px.

PASS:100 natywnych PNG/100 edytowalnych Aseprite, wszystkie roundtripy identyczne i czasy zgodne, właściwe alfa/canvasy/kotwy, brak ucięć, 33 dokładne odbicia, 8 odrębnych ikon, prawidłowe56 przesunięć, pusty środek markera i0 czerwonych pikseli osłony. Pełny powtórny eksport zachowuje100 hashy; 178 wcześniejszych zaakceptowanych bitmap i3 shipped referencje niezmienione. Przegląd natywnych ikon i wszystkich kierunków kompozytów zakończony. review.html jest samodzielny, z wyborem skilla/kierunku/zoomu i przełączaniem aktor/VFX bez zmiany kotwy. Prompty: prompts/index.json; parametry/mapowania: manifest.json i vfx-registration.json; szczegóły: HANDOFF.md.

To pojedyncze kulminacje, nie pełne animacje zamachu/powrotu. Brak importu do Unity i zmian gameplayu/balansu/serialized state; testy kompilacji/runtime nie były uruchamiane dla paczki artu. Pozostają portret, ciała/przemiana, gameplay/oferty/nagrody i integracja oraz smoke w grze. Bez CU, commit/push.

## Szkielet Zabójca — portrety, ciała i przemiana — 2026-10-14

ART_READY_FOR_REVIEW_NOT_IMPORTED. output/skeleton-slayer-finish-v1: dwa portrety128×128, dwa osobne leżące ciała192×192 oraz pełna przemiana S na canvasie256×256, 6×200ms =1,2s, z atlasem i pięcioma edytowalnymi Aseprite. Cztery nowe klatki pośrednie i dokładne zaakceptowane bazy jako endpointy. Faza II portretu poprawiona: ciężkie naramienniki usunięte, odsłonięte kości i bursztynowy rdzeń. Ciała nie powstają przez obrót/zabarwienie żywego sprite'a. Zachowana stara metoda built-in image_gen z referencjami → proporcjonalny premultiplied BOX/alfa150 → zamrożona paleta96 świata / wspólna256 portretów → Aseprite. Świat32PPU/scale1; przemiana pivot(128,64), stopy y192; ciała pivot(96,96) przy ziemi.

PASS:10 rekordów PNG + atlas, alfa/palety/canvasy i brak ucięć, sześć odrębnych klatek ze stabilną kotwą stóp, endpointy pixel-perfect, atlas i czasy zgodne. Wszystkie5 Aseprite otwarto ponownie i10 renderów odpowiada PNG. Pełny powtórny eksport zachowuje11 hashy PNG;290 plików źródeł/referencji/poprz. paczek pozostało niezmienione. Podglądy native/nearest obejrzane; review.html samodzielny z odtwarzaniem jednokrotnym i ręcznym wyborem klatki. Szczegóły: output/skeleton-slayer-finish-v1/HANDOFF.md, manifest.json, validation.json i prompts/index.json.

Nie importowano do Unity, nie zmieniono gameplayu/serialized state, testów kompilacji/runtime nie uruchamiano dla samego artu. Kontrakt przyszłej integracji: zachować logiczny facing, podczas przemiany prezentować S, odtworzyć raz w następnej własnej turze po progu, zsynchronizować fazę/skillset/portret i przywrócić właściwe idle; śmierć/anulowanie ma pierwszeństwo. Pozostają gameplay/oferty/nagrody, import wszystkich czterech paczek oraz smoke w grze. Bez CU, commit/push.

## Szkielet Zabójca — gameplay i integracja grafik — 2026-10-14

READY_FOR_SMOKE. Dwie fazy i sześć skilli z bossy2.0.xlsx; statystyki z Przeciwnicy NPC.xlsx. Obrona kumuluje obrażenia przed mitygacją przez2 własne tury, odwet/ogłuszenie; Zemsta regeneruje do160% AP bez zmiany podstawy kosztów; wybrane Pole Impetu3×3, pięć trafień po2 turach, przerwanie przy10% MaxHP, blokada ruchu przy zachowaniu innych skilli; Wykonać Zadanie przyspiesza jedną następną turę bez spamu. Przemiana po progu50% oczekuje na własną turę i chroni pełny sześcioklatkowy klip. Pasywy/odliczanie/odwet i ikony widoczne w UI; własne efekty fazy I czyszczone, debuffy zachowane.

Unity batch import/configure wykonany dwukrotnie idempotentnie.325 PNG z czterech zatwierdzonych paczek,32PPU/Point/brak kompresji i mipmap, zachowane canvasy/pivoty. Obie fazy×8 kierunków, idle/chód, trafienie/unik,56 kulminacji i par rejestracji VFX,8 ikon,2 portrety,2 osobne ciała, przemiana6×200ms. Osobne rozszerzenia istniejących katalogów; żaden nowy system animacji. Śmierć zatrzymuje przemianę w obu istniejących widokach; Pole Impetu ma scale1. Nie podmieniono encounteru Króla ani sceny.

Kompilacja/configure PASS; końcowe200/200 testów PASS, w tym19 nowych dla Szkieleta. Sprawdzone wszystkie325 importerów, wymiary/hash/kopie i290 niezmienionych plików wcześniejszego artu; regresja Króla/Enta/obrażeń/podglądu/timeline/AI i realna prezentacja w Play Mode PASS. Manualny smoke kamery/tempa/ciał NOT RUN. Encounter testowy zarejestrowany: Assets/_Project/ScriptableObjects/Catacombs/Encounters/ED_Catacombs_skeleton_slayer.asset. Oferty wyprawy i loot pozostają oddzielnym etapem; nie kopiowano nagród Króla. Szczegóły i dokładny opcjonalny wiring ScenarioRunner: LocalHandoffs/skeleton-slayer-gameplay-art-20261014/HANDOFF.md; dowody validation.json i out/skeleton-slayer-regression-final.xml. Bez nowych grafik, zmian generatora/lasu/save formatu, CU, commit/push.

## Szkielet Zabójca — oferty wyprawy i łup — 2026-10-14

READY_FOR_SMOKE. dungeon.catacombs.skeleton_slayer_expedition dostępny na poziomach10/20 w istniejącym systemie Danger/kamieni milowych, z własnym encounterem, katalogiem lootu i komnatą. Zgodnie z decyzją użytkownika jeden slot bossowy losuje Króla lub Szkieleta na wybranym poziomie; wybór pozostaje do ukończenia i przetrwa odświeżenie/kolejność puli/zapis. Istniejących zapisanych okazji nie zmienia. Po ukończeniu rozliczenie Danger jak dotychczas. Bez zmiany formatu save.

Torba Szkieleta Zabójcy to dokładna kopia aktualnej torby Króla, włącznie z pulą/szansą unikatów; jedyna podmiana zawartości to Królewska ektoplazma → Kość Zabójcy. Parametry ekonomiczne kości odziedziczone z ektoplazmy, ikona z istniejącej kości szkieleta. Osobne zewnętrzne tabele bossa z własną torbą; zwykłe moby/skrzynie używają wspólnych tabel. Brak gatunkowej tabeli na bossie, aby nie losować torby drugi raz. Puste pule1–10 i pomijanie pustych toreb zachowano dokładnie jak u Króla. Nie dodawano nowych grafik.

Unity batch configure wykonany, ponowne pełne Configure Skeleton Slayer także zachowuje wiring/idempotencję. Kompilacja i końcowe163/163 testów PASS, w tym14 nowych: pełna zgodność pul i seedowanych losowań10/20, jedna torba/brak rerollu, sześć generacji/place bossa we własnej komnacie, oba warianty bossów w32 kampaniach na poziomach8/10/20, trwały zapis/oferty, rozliczenie Danger. Regresja loot/katalog/save/ofert/walki bossów PASS. Pięć źródłowych assetów Króla niezmienionych po reconfigure, diff check PASS. Manualny smoke NOT RUN. Bez ręcznego wiring, generator/las niezmienione, bez CU/commit/push. Handoff: LocalHandoffs/skeleton-slayer-offers-loot-20261014/HANDOFF.md; dowody validation.json i out/skeleton-slayer-offers-loot-final.xml.

## Zatwierdzone unikaty bossów — 2026-10-14

READY_FOR_SMOKE. Korekta wcześniejszej wspólnej puli: Szkielet Zabójca ma własne cztery unikaty (Kolczuga Furiata, Żebrowana Tunika, Szaty Runiczne, Młot Zabójcy), bez przedmiotów Króla. Zwykłe pule/szanse zachowane. Żebrowana Tunika25/30/35/40/50%; Płaszcz Przemytnika30/35/40/45/55%, progi10–50. Oba efekty i statystyki pozostałych pancerzy wykonują się w istniejącym pipeline walki/statystyk; preview nie zużywa ochrony i obsługuje wielokrotne trafienia. Płaszcz katalogowany, drop czeka na przyszłą torbę/encounter Przywódcy Bandytów. Dedykowane ikony niegotowe; użyto istniejących klas/slotów. Kompilacja PASS,232 unikalne testy PASS (regresja i końcowe25 nowych),7 assetów idempotentnych,5 źródeł Króla bez zmian. Manualny smoke NOT RUN; bez zmian generatora/lasu/arkuszy/save formatu/CU/commit/push. Handoff i dowody: LocalHandoffs/approved-boss-uniques-20261014/HANDOFF.md.


### Przywódca Bandytów 2.0 — gameplay i wyprawa — 2026-10-14

READY_FOR_SMOKE dla etapu mechanik. Wdrożono obie fazy, sześć skilli, pasywy, AI, ukryte pułapki 3×3, przejście po pokonaniu własnej obstawy na początku następnej własnej tury i detonację. Obstawa: 2 Rzezimieszków + 1 Strzelec. Rozkaz przyspiesza następną turę o 50% AV; Ukryte Przebicie jest jednym ciosem ×5. Oddzielna wyprawa Lasu 10/20 i torba; 100 Danger losuje jeden slot Ent/Przywódca z utrzymaniem wyboru po save/load. Własne cztery unikaty, w tym działający już drop Płaszcza Przemytnika. Gruboskórność Kłusownika: odnowienie 5 własnych tur, dowolna liczba aktywacji. Krew przywódcy zastępuje roboczy Sygnet: 6258 złota, starożytny; w puli 11–20 ×1/75%.

Kompilacja PASS;317 odrębnych przypadków EditMode PASS po regresji i końcowym retestcie41/41. Powtarzalność24 assetów PASS,16 źródeł Enta i5 Króla bez zmian. Ręczne wiring nie jest wymagane. Manualny smoke/PlayMode/FPS NOT RUN. Brak dedykowanego artu Przywódcy: obie fazy mają zastępczą prezentację Rzezimieszka; skille/portrety/pozy/VFX/ciała/animacja przemiany i finalne ikony unikatów czekają na art pass. Handoff i dowody: LocalHandoffs/bandit-leader-gameplay-20261014/HANDOFF.md i validation.json. Bez nowych grafik, zmian generatora/rendererów lasu, formatu save, CU, commit/push.


## Manual revive limited to adjacent range — 2026-10-14
Status: READY_FOR_SMOKE.
CombatReviveSystem validates maximum Chebyshev range 1 before changing the downed target, timer or battle log. Same-cell and all eight adjacent directions are allowed; targets two or more cells away are rejected. Different levels/stair positions require a real adjacent traversal connection. Rejection returns before the execution driver's pose and EndActiveTurn calls. Exploration uses the same range shape with extent 1 world unit per axis; existing automatic approach, obstruction validation and cancellation remain. Approach goals retain their existing .15-unit inset so arrival tolerance stays inside the legal range. Healing skills and automatic post-victory recovery keep their existing execution paths. No new UI copy, serialized asset edits, save migration, commit or push.
Validation: baseline regression reproduced 17 failures among 31 range cases on the old code (distant combat/manual exploration rescue and diagonal exploration refusal). Final compilation and EditMode recovery/exploration/trap/outcome suites 123/123 PASS, zero skips (LocalHandoffs/revive-adjacent-range-20261014/final.xml), including 33 new range/level cases and the existing approach/cancellation/wall/post-combat checks. Scoped diff, source/mirror hashes and whitespace checks PASS; self-review complete. No manual Editor wiring. Smoke pending: revive along an edge/diagonal, reject distance 2 in combat without ending the revive action's turn, and click a distant downed party member outside combat to approach before rescue.


## Movement AP label above world occluders — 2026-10-14
Status: READY_FOR_SMOKE.
The movement AP text has an owned root-sorted group at the existing world-UI overlay order 32000, escaping the floor-feedback parent's group. Walls, short walls, props and actors can no longer cover the price through world depth sorting; the route/range/corner geometry remains in its original floor lane. The text group follows floor-layer rebinding, existing destination visibility, preview clearing and hierarchy cleanup. Position, native Gothic font, text colour, formatting, AP computation and gameplay are unchanged. The local temporary-component cleanup now uses the existing Release helper, preserving deferred Destroy in Play Mode and using DestroyImmediate in EditMode.
Validation: final compilation and combat presentation/HUD regression 66/66 PASS, zero skips (LocalHandoffs/movement-ap-occlusion-20261014/accepted.xml). Two new GPU cases cover sorting set before/after preview creation, render actual Gothic AP glyphs above an opaque world occluder, explicitly prove the nested floor-group control hides them, and verify visibility/cleanup. Manual Camera.Render tests refresh native sorting groups like the normal player loop and use the current locale's AP separator. Scoped diff/source-mirror/whitespace/self-review PASS. No serialized-state edits, new art, manual wiring, commit or push. In-game smoke/player build NOT RUN; hover a move behind/beside a short wall, then change/cancel preview and check fog visibility.












## Sprzęt ze skrzyń odnóg zamiast regularnych przeciwników — 2026-10-23 — READY_FOR_SMOKE

Wdrożono i podłączono tabele Katakumb/Lasu (1–10, 11–20). Usunięto zwykłe torby broni/pancerzy z przeciwników oraz skrzynie/skrytki z produkcyjnych pokoi poza celami odnóg. Jedna gwarantowana skrzynia na cel Optional; bazowo średnio 5 sztuk sprzętu na CAŁĄ wyprawę niezależnie od długości/liczby pięter. Bonus łupu zachowany. Authoring obu biomów i odtwarzanie produkcyjnych szablonów utrzymują nowe zasady. Brak zmiany bossów/unikatów i zapisanej zawartości istniejących wypraw.

157 focused EditMode PASS, 0 FAIL, 7 explicit NOT RUN; w tym 26 nowych przypadków: 12 symulacji po 1000 wypraw (średnie 4,827–5,063), 12 wygenerowanych map (2 biomy x 3 długości x 2 seedy), rzeczywiste nagrody przeciwników, idempotencja placement i pełny JSON save/restore skrzyń. Poprawiono testowy AuditMap lasu o brakujące rzeczywiste prefaby Low/Railing: trzy wcześniejsze błędy testów po wprowadzeniu niskich murków zniknęły bez tłumienia logów. Self-review/scope/kompilacja w otwartym Unity PASS. Równoległe zmiany DifficultTerrainCells/StructuralAnchors zachowane i wyłączone z zakresu tej karty. Ręczny setup niepotrzebny; smoke użytkownika na NOWEJ wyprawie. Raport: ../LocalHandoffs/branch-equipment-loot-20261023/README.md. Bez commit/push.
