# Release cleanup — 2026-10-11

Aktualizacja po osobnej karcie napraw regresji: wszystkie 39 wcześniejszych błędów
rozwiązane, pełny EditMode 2406/2406 PASS, bez pominiętych testów; nowy Windows x64
build PASS, 0 błędów. Poniższe wyniki opisują historyczny stan samego cleanupu.
Aktualny raport i ograniczenia wydania: RELEASE_REGRESSION.md.

## Zakres

Usunięto z projektu nieużywane prototypy Crypt 3D, sceny Review i SampleScene,
kopie Recovery, powiązane wyłącznie z nimi skrypty, shadery, materiały i testy.
Usunięto nieużywane kopie ikon i źródłowe bitmapy paczek ThirdParty, stare
prefaby lochu/debug palette oraz zastąpione grafiki postaci i rekwizytów.
Usunięto także wyłączone wcześniej warstwy fartucha alchemiczki nad kotłem.
Pozostałe zasoby Resources zachowano, uwzględniając ładowanie po nazwie.

Poza repo przeniesiono stare mirrory Unity, wyniki testów, podglądy, propozycje
grafik, archiwa ZIP, tymczasowe logi i zakończone raporty implementacyjne.
Usunięto puste foldery Assets wraz z ich nieużywanymi meta.
Reguły `.gitignore` zapobiegają ponownemu dodaniu lokalnych artefaktów.

Testy grafik i weryfikacja pasywnych ikon działają na importowanych assetach,
bez zależności od lokalnego `output/asset-review`. Z katalogów zatwierdzonych
ikon zachowano wyłącznie dziewięć małych kontraktów ID/nazwa pliku w
`Assets/_Project/Editor/ArtCatalogs` (około 40 KB, wyłączone z buildu gracza).
Test importu animacji porównuje wszystkie pozostałe PNG z assetami Unity zamiast
wymagać historycznej liczby obejmującej usunięte nakładki. Test kompozycji mikstur
i bomb nie zapisuje już zbędnych podglądów do nieistniejącego TestResults.
Importery nadal wymagają wejściowych PNG do ponownego importowania źródłowej
grafiki; sama weryfikacja istniejących assetów nie wymaga źródeł ani archiwum.

Łącznie usunięto 6169 plików assetów/meta oraz 42 puste foldery z ich meta.
Kopia odzyskiwalna obejmuje około 22.9 GB plików, głównie dawne mirrory i cache.

Zachowano pięć scen z Build Settings, bieżące prefaby, katalogi zawartości,
narzędzia autorskie i ich zależności. Foldery Arungardu z nazwą Prototype oraz
sceny Sandbox są nadal używane przez grę. Zachowano również licencje i atrybucje
paczek, źródłowy tileset 0x72, dokumentację produktu i materiały pracy dyplomowej.
Aktywne cache Unity, logi otwartego Editora i pliki IDE pozostają lokalne/ignorowane.

## Odzyskiwanie i evidence

Odzyskiwalna kopia usuniętych plików, z zachowaniem ścieżek względnych:
`C:/Users/Patryczku/Documents/Godmist-cleanup-backup-20261011`.
To katalog poza repozytorium, nie zawartość do dystrybucji.

Manifest usunięć, sumy SHA-256 assetów, graf zależności i wyniki kontroli:
`C:/Users/Patryczku/Documents/Godmist/LocalHandoffs/release-cleanup`.
Starsze odwołania do lokalnego `output/`, `out/` i dawnych mirrorów w historycznej
dokumentacji wskazują teraz na odpowiadające ścieżki w kopii odzyskiwalnej.

Nie wykonano stage, commit, push ani publikacji.

## Weryfikacja

- Kontrola zachowanych referencji GUID: brak nowych zerwanych zależności.
- Brak zduplikowanych GUID po cleanupie.
- SHA-256 zarchiwizowanych pojedynczych assetów: zgodne z manifestem.
- Wcześniejsze zmiany poza zakresem cleanupu: zachowane.
- Czysty import Unity 6000.4.0f1 i release build Windows x64: PASS, 0 błędów,
  259 169 121 bajtów w końcowym buildzie, 874 ostrzeżenia raportowane przez Unity. Importer sam
  zaktualizował API GUID w cache ShaderGraph; końcowa kompilacja/build przeszły.
  Końcowy build powtórzono po zmianach w weryfikacji grafik i synchronizacji
  końcowych źródeł. Evidence: `final-build.log`, `final-build-result.txt`.
- Pięć włączonych scen: otwarte bez brakujących skryptów.
- Pełny EditMode po usunięciach: 2357/2402 PASS, 45 FAIL. Kontrola z przywróconymi
  plikami sprzed cleanupu: 458/501 PASS, 43 te same błędy. Nie odtworzyły się
  tylko historyczna liczba klatek (poprawiona) i zależny od izolacji test tooltipu
  `ItemRarityBordersSurviveInteractionAndResetWhenRebound("service")`.
- Runtime smoke przez Bootstrap na osobnym profilu: PASS. Nowa gra, pięciu NPC,
  usługi/dialogi, stash, sześć wypraw, dojście do bramy, kuźni i studni, odsłanianie
  dachu i sortowanie postaci. Start gotowego gracza Windows bez wyjątków: PASS
  (sprawdzenie uruchomienia, bez wizualnego gameplay smoke gotowego EXE).
- Ponowna regresja po przeniesieniu kontraktów grafik: 462/501 PASS, 39 FAIL.
  Wszystkie 39 błędów odtworzono także przed cleanupem; brak nowych błędów
  względem kontroli. Wszystkie 38 testów klas bezpośrednio związanych z cleanupem
  (CombatPoseIntegrationTests, VS22PresentationArtTests, VS22ItemArtTests,
  VS22PassiveArtTests) PASS. Nie powtarzano całego suite po tej lokalnej poprawce.
  Evidence: `final-regression.xml`, `baseline.xml`, `final-failures.json`.

## Stan po cleanupie, przed naprawą regresji

Build nie jest równoznaczny z pełną akceptacją wydania. Regresja ujawniła starsze
błędy/nieaktualne oczekiwania m.in. w CharacterCardPresentationTests,
CombatHudPassTests, CombatV4ContentTests, nawigacji i geometrii ścian lochu,
PartyRailPresentationTests, tooltipach mikstur i dawnych testach skali sprite'ów.
Pełne nazwy, komunikaty i stack trace są w `failures.json`, `editmode.xml` oraz
`baseline.xml` w lokalnym evidence. Nie usuwano tych testów dla uzyskania zielonego wyniku.

864 komunikaty w logu czystego importu/buildu dotyczą limitu głębokości serializacji
rekurencyjnego `SkillEffectData.counterattackEffects`. Ten sam problem występuje
w logach wcześniejszej karty alchemiczki (`output/alchemist-cauldron-layers/play-after.log`
w kopii odzyskiwalnej). Pozostałe ostrzeżenia obejmują przestarzałe API i warianty
shaderów. Nie zmieniano modelu serializacji efektów w ramach cleanupu.

Walidacja odbywa się w osobnym katalogu
`C:/Users/Patryczku/Documents/Godmist-release-validation-20261011`, utworzonym
wyłącznie z Assets, Packages i ProjectSettings, bez kopiowania Library.
Pomocniczy runner walidacji istnieje wyłącznie w tej kopii i lokalnym evidence.

Końcowy build znajduje się w `Godmist-release-validation-20261011/Builds/Windows`.
Do dystrybucji potrzebna jest cała zawartość tego folderu (EXE, Godmist_Data,
UnityPlayer, MonoBleedingEdge i biblioteki), nie sam EXE. Wygenerowane symbole
`Godmist Unity_BurstDebugInformation_DoNotShip` przeniesiono do katalogu
`Godmist-release-validation-20261011/Symbols`, poza zawartość dystrybucji.

Cleanup: READY_FOR_SMOKE. Final self-review zakończony; bez ręcznego wiring
w Editorze. Starsze błędy regresji rozwiązano w kolejnej karcie opisanej w
RELEASE_REGRESSION.md. Akceptacja całego wydania pozostaje otwarta do gameplay
smoke i wyjaśnienia ostrzeżeń serializacji opisanych powyżej.
