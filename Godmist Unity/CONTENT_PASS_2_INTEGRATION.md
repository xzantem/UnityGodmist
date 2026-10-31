# Godmist CP2 01 Integracja bazy v0.1.2

Stan integracji: 31 października 2026. Zgodnie z decyzją użytkownika bazą v0.2.0 jest spójny pakiet siedmiu commitów kończących v0.1.1 oraz gotowe poprawki Windows z niezacommitowanych prac v0.1.2. Obsługa macOS i pada X360 jest odłożona do późniejszej integracji z v0.1.3. Nie blokuje bieżącej karty.

**Status karty: ZAAKCEPTOWANA.** Automatyczna weryfikacja zakończona; użytkownik potwierdził smoke PASS i polecił commit/push 31 października 2026. Integracja obejmuje wyłącznie zakres tej karty i jest zapisywana jako commit z zachowaniem powiązania do źródłowego `a35f09ec8`. Branch źródłowy pozostaje aktywny i nie został zmodyfikowany. Numer aplikacji bez zmian.

## Wykonany zakres integracji

- Przeniesiono zapisany pakiet `89159e69f..a35f09ec8` bez stage/commit. Projektowy Kanban scalono trójstronnie z zachowaniem decyzji v0.2.0 o opcjonalnym RNG.
- Pominięto release-only zmiany `ProjectSettings.asset`, `Bootstrap.unity` i `EarlyAccessReleaseBuild.cs`. Aplikacja pozostaje na dotychczasowej wersji 0.1.0; bazą mechanik i danych są przeniesione poprawki. Historyczny `RELEASE_0.1.1.txt` nie oznacza nowego wydania tego brancha.
- Z niezacommitowanej migawki przeniesiono cztery kompletne pakiety: ściany Katakumb, ciągły ruch prezentacyjny walki, jawne pakowanie shadera obrysów w Resources oraz wspólne sortowanie przedmiotów. Obejmują 20 zmienianych plików, 20 nowych ścieżek kodu/testów/shadera i ich `.meta`, a także przeniesienie starej ścieżki shadera. Uzupełniono odpowiadający kontekst projektowy.
- W plikach współdzielonych wyłączono 15 hunków dotyczących sterowania padem. Nie przeniesiono klas ControllerInput/nawigacji, ich wywołań, zmian assembly references do inputu, `MacOSTestBuild` ani ustawień platformy.
- Shader zachowuje źródłową treść i GUID; różnica surowych bajtów między starą i nową kopią wynikała wyłącznie z LF/CRLF. Nie tworzono nowego identyfikatora shadera.
- Wszystkie operacje korzystały z lokalnej migawki, nie z dalszych zmian aktywnego czatu v0.1.2. Dotychczasowe zmiany użytkownika są zabezpieczone i kontrolowane hashami; źródłowego worktree nie edytowano.

Dowody i manifest do przyszłej integracji v0.1.3: `../LocalHandoffs/cp2-01-integration-20261031/`. `snapshot.json` zawiera źródłowe hashe i stan plików; `committed-integration.json` wykluczenia; `windows-wip-integration.json` wybrane pliki i pominięte fragmenty pada; oba patche pozwalają odtworzyć zakres bez pobierania ruchomego stanu źródła. Historia Git pozostaje niezmieniona do odbioru użytkownika; niniejsze przeniesienie nie jest jeszcze commitem merge.

### Wyniki wykonanej weryfikacji

| Kontrola | Wynik |
| --- | --- |
| Import i kompilacja projektu/testów w Unity 6000.4.0f1 | PASS, siedem assembly projektu/testów |
| Pierwszy przekrojowy przebieg EditMode | 1338 przypadków: 1333 PASS i 5 FAIL; przyczyny opisane poniżej |
| Testy zwykłe w pierwszym przebiegu | 1319 PASS i 4 nieaktualne kontrakty tekstów pasywów |
| Retest poprawionych kontraktów i zwykłych kontroli masek | 19/19 PASS, zero FAIL/skipów |
| Końcowe pokrycie zwykłych testów regresji | 1323 różne przypadki PASS; 15 z retestu powtarza wcześniej zaliczone kontrole pasków |
| Narzędzia/testy Explicit w pierwszym przebiegu | 14 PASS; jeden helper początkowego authoringu nie ma zastosowania do już przygotowanych prefabów |
| Końcowe porównanie treści 644 plików źródłowych poza zamierzonymi wyjątkami | Brak nieoczekiwanych różnic; jeden prefab zapisany przez Editor z czterema jawnymi domyślnymi nullami |
| Wcześniejsze zmiany użytkownika | Dziewięć istniejących zmian zachowanych bajt w bajt lub jako wcześniejsze usunięcie; dokumenty planu zmienione świadomie |
| Indeks i historia Git | Indeks pusty; HEAD nadal `98b68bde6`; bez commita/pusha |
| Manualny gameplay smoke | PASS, potwierdzony przez użytkownika 2026-10-31 |
| Nowy build Windows | NOT RUN; shader sprawdzony w Editorze przez właściwe testy zależności i obrysów |
| macOS i X360 | DEFERRED zgodnie z decyzją użytkownika |

**Cztery nieaktualne testy:** `SkillTreeProgressionSystemTests.GlobalPassiveSummary_ShowsBothCombatEffects` nadal sprawdzały dawne sformułowania oraz wymagały opisu obu bonusów dla zera wydanych punktów. Przeniesiony, zaakceptowany UI już w źródłowym `a35f09ec8` rozdziela „Premia za punkt” od „Aktualnie” i przy zerze zwraca „brak premii.”. Zaktualizowano oczekiwania do bieżącego słownika; dodano jawne sprawdzenie zera, a oba efekty sprawdzane są teraz przy 1, 10 i 19 punktach, z zachowaniem kontroli zmiany wartości. Nie zmieniano produkcyjnych zasad ani nie usuwano asercji obu efektów. Własna poprawka testów jest zachowana w `test-contract-fix.patch`.

**Jednorazowy helper:** filtr klas Unity uruchomił również metody `Explicit`. `BarFillMaskTests.ApplyAuthoredMasks` oczekuje dodatniej liczby zmienionych prefabów podczas ich pierwszego authoringu. Na już przeniesionych, poprawnie przygotowanych maskach `BarFillMaskSetup.Apply` zwrócił zero, więc helper zakończył się FAIL. Nie zmieniano tej asercji ani prefabów, aby wymusić nową zmianę. W retestach wybrano właściwe 15 zwykłych kontroli prefabów, clippingu, layoutu, tarczy i renderowania: wszystkie PASS. Oryginalny wynik helpera pozostaje w XML i nie jest przedstawiany jako PASS.

Unity przy ponownym zapisie `PF_UIW_SkillTreeWindow Variant.prefab` przez istniejące narzędzie authoringu dopisało cztery domyślne referencje `shieldOverlay: null`. To jedyna dodatkowa normalizacja istniejących danych źródłowych; bez zmiany docelowych referencji czy zachowania. Nie edytowano YAML ręcznie. Kontrola whitespace wykrywa odziedziczone puste pola w zapisach Unity, wyłącznie w plikach serialized; nie czyszczono ich ręcznie.

Wygenerowany render masek `../LocalHandoffs/bar-fill-mask-20261030/after.png` obejrzano: puste, częściowe i pełne wypełnienia pozostają pod ramkami dla trzech wysokości. Wyniki runnerów są w `integration-tests.xml` i `targeted-retest.xml`; końcowe inwarianty w `verification.json`. `integration-paths.txt` wyznacza 691 ścieżek integracji do późniejszego scoped review/stage, z wyłączeniem wcześniejszych zmian użytkownika.

## Porównane stany

| Element | Stan |
| --- | --- |
| Wspólna baza | `89159e69fca45e96dc56a3bf5ff0c7de9ba066a0`, wydane v0.1.0 |
| Branch docelowy | `codex/v0.2.0-content-pass-2` |
| Docelowy HEAD | `98b68bde6be54f22898fd177c759ea2d9fd52901` |
| Branch źródłowy | `codex/v0.1.2-polish-and-fixes` |
| Źródłowy HEAD | `a35f09ec87ee5b859c11df1e16d6c30a8f32462f` |
| Źródłowy worktree | `C:/Users/Patryczku/.codex/worktrees/v0-1-1-polish-and-balance/Godmist` |

Źródłowy HEAD nadal opisuje zakończenie v0.1.1 i ma w ustawieniach/Bootstrap etykietę 0.1.1. Nazwa brancha v0.1.2 nie dowodzi wydania v0.1.2. Lokalnie istnieje tag v0.1.0; raport nie sprawdzał aktualnego stanu zdalnych wydań.

Zapisany zakres commitów obejmuje **657 plików**, 28 734 dodane i 3 284 usunięte linie względem wspólnej bazy. Podział obejmuje także pliki `.meta`: 217 w Scripts, 197 w ScriptableObjects, 117 w Tests, 101 w Art, 13 prefabów i 12 pozostałych plików.

Surowy spis zmienionych ścieżek, stan obu worktree i hashe porównania: `../LocalHandoffs/content-pass-2-20261027/cp2-01-snapshot.json`. To migawka, nie automatycznie odświeżany manifest.

## Pakiet zapisany w commitach do przeniesienia

Zachować kolejność i zależności tych commitów. Podane wyniki dawnych testów są zapisami w historii commitów/dokumentacji, a nie nowymi testami wykonanymi w branchu v0.2.0.

| Commit | Zakres | Dlaczego przenosimy |
| --- | --- | --- |
| `f086c3eb4` | Wspólny słownik `UiTerms`, opisy i prezentacja efektów bohaterów, kontekst zasobu | Punkt wyjścia dla wszystkich nowych tooltipów, statusów i klas; eliminuje rozjazd nazw i jednostek |
| `f17a99280` | Zatwierdzone opisy skilli/talentów/pasywek, kontekstowe wartości, linki do obliczeń, korekty Odsłaniającego Żaru i talentów bezpośredniego leczenia jednego celu | Nowy content ma korzystać z gotowego sposobu prezentacji i poprawionych efektów; commit zależy od poprzedniego |
| `477ca7114` | Rozbicie statystyk wyposażenia na źródła, jakość, rzadkość, części i ulepszenie | Baza prezentacji nowego sprzętu L25/L35; wspólne zapytania o obliczenia, bez oddzielnej matematyki UI |
| `b0b41e15d` | Pakiet 25 napraw obliczeń, kontraktów danych, AI, craftingu, własności przedmiotów i save/load | Najważniejsza zależność techniczna passu: nowe dane muszą powstawać na poprawionych kontraktach |
| `1f5367211` | Maskowanie wypełnień pasków pod ramkami, kod, authoring i zmiany prefabów | Naprawa prezentacji HUD/UI musi wejść razem z wiringiem |
| `d1d8c6d97` | Dedykowane ikony Wyładowania, jego talentów i Sprawnego Odzysku; powiązania i źródła artystyczne | Nie cofać zaakceptowanych ikon ani referencji przy rozszerzaniu drzewek |
| `a35f09ec8` | Domknięcie v0.1.1: obliczenia statystyk postaci, Jadeit, kamienie bossów, pozostałe poprawki prezentacji; także pliki wydania | Domknięcie funkcjonalnej bazy; elementy release wymagają oddzielnego potraktowania opisanym niżej sposobem |

### Walka statystyki i AI

Przenieść wspólnie kod wykonania, preview, AI, dane i testy:

- Wspólne rozstrzyganie trafienia oraz kolejność obliczeń obrażeń. Kryt, bonus przełamania Freeze i bonusy biomowe mają poprawione miejsce względem obrony. `CombatDamageSystem`, preview i kalkulacje tooltipów muszą opisywać ten sam wynik.
- Poprawki etykietowania Demonów, końcowego limitu szybkości/Chill, zaokrąglania maksymalnego HP oraz stabilności referencji przy recovery.
- Dane bazowego ataku przeciwników. Usunięto `CharacterDefinition.AttackMultiplier`; wcześniejszy mnożnik ×0,85 jest uwzględniony w bazach/przyrostach assetów. Runtime baza ataku dopuszcza ułamki. **Nie przenosić usunięcia pola bez przeliczonych definicji i odwrotnie; nowym przeciwnikom nie dodawać ponownie globalnego ×0,85.**
- Authoring Lasu/Katakumb zachowujący bieżący atak zamiast nadpisywania historyczną tabelą. Nowe importery muszą respektować ten kontrakt.
- Poprawki prognozy i oceny multi-hit oraz leczenia/odzyskiwania zasobów Paladyna w AI; korekty kolejności obrażeń i szansy zabicia.
- Wspólne reguły Wyładowania, współczynniki talentów Rozdzielenia Energii w danych i pozostałe zaakceptowane poprawki efektów bohaterów.

Źródła techniczne: `Scripts/Systems/Combat`, `CharacterStatPipelineSystem`, `CharacterLevelGrowthSystem`, `Data/Characters`, definicje jednostek i skilli w `ScriptableObjects` oraz zmienione narzędzia Editor. Testy odniesienia obejmują m.in. `EnemyBaseAttackScalingTests`, `EnemyAttackDataMigrationTests`, `CombatDamageResolutionTests.SharedStage`, `ChillSpeedLimitTests`, `MaximumHealthRoundingTests`, testy AI i `WarriorDischargeTests.SharedRules`.

### Efekty pasywne identyfikatory i zapisy

Przenieść jako całość:

- Kontrakty dozwolonych targetów/wartości operacji pasywek, ocenę ranków, codec payloadów i reconciliation. To fundament nowych node'ów, a nie wyłącznie poprawki istniejących drzewek.
- Stabilne `SkillEffectData.StableId`, inicjalizację w authoringu i odpowiednio zapisane dane skilli, także efektów zagnieżdżonych. Nie generować nowych ID przy zwykłym odczycie/importowaniu istniejącego contentu.
- Stabilne ID nagród questów, dane jakości/rarity oraz generowanie nagrody według poziomu questu i `Amount`.
- `LegacyContentIdentities` wraz z wygenerowaną tabelą aliasów i zamrożonym manifestem v0.1.0. Nowe ID nie mogą zerwać odczytu wcześniej zapisanych efektów i odebranych nagród.
- Przeniesienie migracji starych przedmiotów/SP/skillów/tarcz do restore, szczegółową diagnostykę błędów restore oraz zgodne snapshot/restore i utrwalanie ofert dungeonów. Nie przywracać migracji uruchamianych przy zwykłym przeliczaniu gameplayu.

Istotne pliki: `SkillEffectIdentity`, `StableContentIdentitySetup`, `LegacySaveMigrations`, `LegacyContentIdentities.Generated`, `SaveRestoreFailure`, `GameSessionRestoreFactory`, `GameSessionSaveSnapshotFactory`, `QuestRewardItemGenerator` oraz `Tools/ContentMigration/legacy-authoring-v0.1.0.json` i generator manifestu. Zachować testy stabilnych ID, migracji i diagnostyki.

**Wpływ na pass 2:** CP2-02 ma budować manifest na tych kontraktach, a CP2-04 nie może ponownie wypłacać legacy SP. Nowe nagrody i skille authorować od razu z trwałymi identyfikatorami.

### Crafting handel i przedmioty

Przenieść:

- Izolację mikstur sandboxu od właściwych instancji inventory i snapshoty consumables.
- Walidację własności wyposażenia w usługach, w tym ulepszaniu/socketingu. Nie wystarczy przenieść samego UI blokującego obcy item.
- `CraftingValueCalculator`: wykrywanie błędnych/cyklicznych zależności wyceny oraz wspólne quote/validation dla craftingu i kupowania brakujących materiałów.
- Powiązane poprawki alchemii, craftingu materiałów, wyposażenia, biżuterii, galdurytów i handlu. Poprawna wycena/transakcja nie może kończyć się utratą złota lub składników po odmowie.

Nie wykryto w tym pakiecie osobnej zamiany sił bazowych mikstur B/C ani projektu nowych katalizatorów L21–40. Zmiany `AlchemyCraftingService` w porównanym zakresie dotyczą m.in. walidacji wyceny i zakupu składników. Przyszłe wartości i jednostki alchemii nadal trzeba uzgodnić z aktualną bazą przy CP2-19; nie zakładać, że commit napraw craftingu wdrożył content pass 2.

### Jadeit Obsydian i tabele łupu

To część przyjętej bazy gospodarki, nie opcjonalny dodatek kosmetyczny:

- `item.runic.jadeite`: rzadki, cena bazowa 2847, +3,5% zadawanych obrażeń w Lesie za sztukę w biżuterii. Cztery sztuki dają +14%; poza Lasem bez tej premii.
- Jadeit w katalogu klejnotów, materiale, wszystkich czterech typach biżuterii, generacji, ikonach i 32 wariantach metal/typ biżuterii.
- Każdy boss Katakumb: osobna pula 50% na 1–2 Obsydianu; każdy boss Lasu: 50% na 1–2 Jadeitu. Oba dotychczasowe przedziały toreb: 1–10 oraz 11–20. To szansa przed istniejącymi bonusami dropu i odfiltrowaniem pustej torby.
- Usunięcie tych kamieni z toreb zasobów wraz z kompensacją szans wspólnej puli, aby nie obciąć bazowej podaży pozostałych materiałów.
- `BossGemLootSetup`, zabezpieczenia starszych setupów, biblioteka ikon i zaktualizowane tabele w assetach. Nie uruchamiać ponownie starego authoringu nad nowymi tabelami.

Pokrycie: `BossGemLootTests` oraz istniejące testy materiałów/biżuterii i zgodności zapisów. Przedziały L21–40 i ostateczny balans pięciu biomów należą do kolejnych kart; nie dopisywać ich przy samym przenoszeniu bazy.

### UI i grafika

Przenieść nowe klasy formatowania/prezentacji oraz ich referencje w prefabach. Dotyczy to opisów skilli i statusów, linkowanych obliczeń skilli/sprzętu/statystyk postaci, kontekstu many/furii/impetu, `pp` względem `%`, masek pasków, ikon oraz usunięcia zbędnego tooltipu licznika sztyletów z zachowaniem hover.

Przenieść również nowe reguły `CONVENTIONS.md` i `PROJECT_CONTEXT.md`; projektowy Kanban scalić z zachowaniem obu historii. Nie przepisywać root `KANBAN.md` z drugiego worktree.

## Prace niezacommitowane na v0.1.2

Poniższe grupy są widoczne w plikach roboczych, ale **nie należą do siedmiu powyższych commitów**. Nie kopiować aktywnego worktree zbiorczo: część nowych klas i testów jest untracked, shader zmienia ścieżkę, a pliki są w trakcie pracy. Stan staged/unstaged zmieniał się podczas inspekcji; trwałym punktem odniesienia pozostaje commit.

| Pakiet | Co obecnie widać | Sposób późniejszej integracji |
| --- | --- | --- |
| Połączenia ścian Katakumb | Zmiany `DungeonFloorMapView`, `DungeonWallSegmentView`, `CroppedLowWall`, `FreeCaps`, nowe `MixedLowJoins`, testy geometrii i raporty Editor | Przenieść kompletny zaakceptowany commit/pakiet razem z testami; zachować celowe obniżanie pełnych ścian i nie zmieniać przy okazji kolizji |
| Ciągła prezentacja ruchu walki | `CharacterView`, `CombatUnitViewSpawner`, `CombatExecutionDriver` i nowe `CombatContinuousMovementTests`; ścieżka konsumuje budżet jednej klatki przez kolejne waypointy bez idle na każdym kroku | Przenieść razem stan ścieżki, cleanup i testy; nie przenosić pojedynczego wywołania nowego API bez jego implementacji |
| Obrysy w buildzie | Przenoszony `SolidSpriteSilhouette.shader` do Resources, wspólne `CombatOutlineShader.Load`, zmiany kilku widoków oraz `CombatOutlineBuildValidation`/testy | Traktować shader, jego `.meta`, loader i walidator buildu jako jeden pakiet. Nie zostawić starego `Shader.Find` z brakującą zależnością w playerze |
| Kolejność przedmiotów | Nowe `ItemDisplayOrder`, użycie w inventory/ekwipunku, magazynie, łupie i handlu, wspólna wycena tooltipu i nowe testy | Przenieść wszystkie miejsca użycia; zachować regułę: typ, aktualna rzadkość malejąco, polska nazwa, wartość jednej sztuki malejąco, stabilne tie-breakery. Nie sortować trwałej listy save |
| macOS | Robocze `MacOSTestBuild.cs`: preflight, zmiana targetu, kontrola scen/builda i lokalny build testowy; część pracy nad obrysami dotyczy również pewności zależności w buildzie | Przyjąć dopiero finalny pakiet obsługi platformy, nie utożsamiać istnienia narzędzia z potwierdzonym działaniem gry na Macu |
| Kontroler X360 | Zakres potwierdzony przez użytkownika; w zapisanej migawce statusu Git brak osobnego zmienionego pliku input/gamepad/controller | Ponowić porównanie po implementacji i przyjąć komplet: input, nawigacja UI, mapowania, prezentacja przycisków, testy i ewentualny serialized setup |

W dokumentacji roboczej ścian znajdują się wpisy `READY_FOR_SMOKE` i wyniki automatycznych testów, równocześnie z zapisem, że gameplay smoke nie był wykonany w chwili ich pisania. Nie uznawać tych wpisów za aktualną akceptację użytkownika; przyjmować stan końcowego commita i jego rzeczywisty odbiór.

### Wymagania odbioru platform i kontrolera

Nie projektujemy tu ponownie rozwiązań z aktywnego czatu v0.1.2. Przed przeniesieniem ustalić z jego finalnego diffu:

- **macOS:** wspierane architektury i wersje systemu; poprawny build i shader assets; start na rzeczywistym Macu; zapis/restart/load z zachowaniem przyjętej lokalizacji danych; poprawne wyjście, input i czytelność UI. Narzędzie cross-build na Windows nie potwierdza uruchomienia na macOS.
- **X360:** które platformy i urządzenia są deklarowane; nawigacja menu/usług/inventory/drzewek, eksploracja i walka, wybór celu/anulowanie, przełączanie źródła inputu oraz rozłączenie kontrolera. Brak regresji myszy/klawiatury. Nie deklarować automatycznie obsługi X360 na macOS bez potwierdzenia przez pakiet źródłowy i test urządzenia.
- **Nowy content v0.2:** trzecie drzewka, T5, reset i nowe interakcje muszą korzystać z przyjętej nawigacji/inputu; nie tworzyć nowych ekranów dostępnych wyłącznie myszą, jeżeli wydanie zachowuje obsługę kontrolera.

## Ryzyka i konflikty integracji

1. Oba branche zmieniły od wspólnej bazy tylko jeden wspólny plik: `Godmist Unity/KANBAN.md`. Zmiana v0.2.0 dotyczy odłożenia kontrolowanego RNG, a źródło dopisuje raporty v0.1.1. Trójstronny podgląd `git merge-tree` dla porównanych commitów nie wykazał markerów konfliktu tekstowego. Nie jest to merge ani gwarancja zgodności runtime.
2. W docelowym worktree lokalnie zmieniony `Godmist Unity/ProjectSettings/ProjectSettings.asset` koliduje ze ścieżką zmienianą przez pakiet źródłowy. Nie nadpisywać istniejącego `preloadedAssets` ani obcych zmian. Pozostałe lokalne materiały miasta, usunięta meta i root Kanban także pozostają chronione.
3. Zapisany stan źródłowy zawiera release-only zmiany: `bundleVersion: 0.1.1`, tekst 0.1.1 w Bootstrap, niewielki diff RectTransform w tej scenie, `RELEASE_0.1.1.txt` i narzędzie buildu ustawione na konkretną wersję. Nie traktować ich jako zadania zmiany aplikacji na 0.2.0. Podczas integracji świadomie ustalić, które materiały zachowujemy jako historyczne; konfigurację wydania v0.2.0 wykonać dopiero w CP2-56. Bootstrap w obecnym porównaniu nie zawiera nowego funkcjonalnego wiring wymagającego przeniesienia.
4. Kopiowanie samych `.cs` pominie ID efektów, przeliczone ataki, pule łupu, ikony i prefab wiring. Kopiowanie samych assetów pozostawi stary kod interpretujący je inaczej. Przenosimy spójne pakiety wraz z `.meta` i testami; nowych serialized poprawek dokonujemy przez Editor API.
5. Alias manifestu v0.1.0 nie regenerować na podstawie przyszłego contentu v0.2.0. To zamrożona mapa historyczna, a nie bieżący katalog.
6. Gotowe, zapisane przedmioty i prerollowane nagrody nie powinny zostać ponownie wygenerowane. Natomiast baza ataku postaci jest odtwarzana z aktualnej definicji zgodnie z nowym kontraktem; nie utożsamiać zachowania statystyk bazowych z utrwalaniem rolli przedmiotów.
7. Historyczny test/report nie dowodzi stanu całej nowej bazy. W dokumentacji poprzednich kart pozostają m.in. wzmianki o niepowiązanych failure testów złota i unikatowej tuniki. Przy integracji sprawdzić ich aktualny stan; nie wyciszać failure ani nie zakładać, że wzmianka sprzed kolejnych commitów jest nadal aktualna.

## Proponowany sposób przeniesienia

**Przyjęta decyzja:** integrować teraz gotową bazę Windows, również z wybranych niezacommitowanych plików. Nie czekać na macOS/X360. Poniższe kroki opisują kontrolę zakresu oraz sposób przyszłego przenoszenia pozostałych zmian; blokada do czasu zakończenia całego v0.1.2 została zniesiona.

Po zamknięciu pakietu źródłowego:

1. Ustalić finalny hash v0.1.2, porównać zakres od `a35f09ec8`, sprawdzić status testów/smoke i zaktualizować manifest. Nie integrować wskazania branch HEAD bez zapisania konkretnego commita.
2. Zachować docelowe zmiany robocze bez resetowania lub automatycznego stasha cudzych plików. Jeśli przeszkadzają w integracji, użyć osobnego czystego worktree do przygotowania i weryfikacji scalenia; nie otwierać dodatkowego Unity bez potrzeby.
3. Preferować kontrolowaną integrację całej zatwierdzonej historii źródła zamiast ręcznego odtwarzania 657 plików. Zachować commit v0.2.0 odkładający RNG oraz lokalny plan. Gdy źródło zawiera również nieprzyjęte funkcje, przenosić zatwierdzone spójne commity w kolejności z tabeli i późniejsze pakiety osobno.
4. Rozstrzygnąć release-only pliki i lokalne ustawienia. Nie uruchamiać historycznego `ConfigureCandidate` ani Mac test-build helpera jako części content importu: mogą ustawić inną wersję/target lub wykonać build do historycznej ścieżki.
5. Przejrzeć kod+dane+referencje, wykonać poniższy zestaw weryfikacyjny i uzupełnić handoff tylko tam, gdzie integracja naprawdę wymaga czynności w Editorze. Nie uruchamiać zbiorczo wszystkich starych setupów contentu.
6. Zapisać wynikowy hash/stan bazy po odbiorze i zaktualizować CP2-01 w planie. Commit/push dopiero po `git`; brak automatycznego publikowania wersji.

Wybór finalnego mechanizmu Git zależy od tego, czy v0.1.2 zostanie wcześniej scalone do main i czy wszystkie późniejsze commity zostaną przyjęte. Obecna struktura historii nie uzasadnia przepisywania zmian ręcznie ani odrzucania całej warstwy UI.

## Weryfikacja po integracji

### Automatyczne sprawdzenia

1. Import i kompilacja właściwego projektu oraz assembly testów, brak nowych missing scripts/GUID/references. Walidacja stabilnych ID, nowych materiałów/katalogów i authoringu.
2. Save/load: `StableContentIdentityTests`, `LegacySaveMigrationTests`, `SaveRestoreDiagnosticsTests`, właściwe przypadki `SaveManagerTests` i utrwalania ofert. Stare postacie, SP, skille, aktywne efekty i odebrane nagrody; zachowane rollowane instancje.
3. Walka: współdzielone hit/damage/preview, Freeze/kryt/biom względem obrony, atak przeciwników, końcowy cap szybkości, HP i AI leczenia/multi-hit. Uruchomić dotknięte zestawy wskazane wyżej, bez ponownego wykonywania całego audytu 25 kart.
4. Usługi: własność wyposażenia, izolacja sandboxu, cykle wyceny/receptur, transakcje handlu/craftingu i nagrody questowe z ilością/poziomem.
5. Kamienie bossów/biżuteria, maskowanie pasków, terminologia i obliczenia tooltipów. Dodatkowo finalne zestawy źródła dotyczące ścian, ciągłego ruchu, obrysów, sortowania i inputu.
6. Weryfikacja pakowania shadera i buildów na przyjętych platformach. Dopiero wyniki tej integracji są dowodem gotowości bazy v0.2.0; nie przepisywać liczby dawnych PASS jako nowego wyniku.

### Smoke użytkownika

- Wczytać kopię zapisu v0.1.0 i przyjętego v0.1.2: ekwipunek, złoto, SP, drzewa, pasek skilli, galduryty, aktywne efekty i nagrody. Zapisać, ponownie uruchomić i odczytać bez dodatkowych refundów ani rerollu.
- Przejść krótką wyprawę Katakumb i Lasu; sprawdzić bossów, odbiór łupu, crafting/handel i powrót do miasta. Nowe źródła kamieni oceniać na nowych rollach, nie oczekiwać zmiany już przygotowanej torby.
- Obejrzeć tooltip skilla, wyposażenia i statystyki postaci, paski HP/zasobu, ikony Wyładowania i sztyletów. Przejść przez naprawione styki ścian oraz długą ścieżkę ruchu w walce.
- Sprawdzić zbudowanego playera, w tym obrysy; dla macOS uruchomienie na Macu, dla X360 pełną podstawową pętlę i powrót do myszy/klawiatury na platformach przyjętych przez v0.1.2.

## Wynik audytu i dalsza weryfikacja

Audyt rozpoznał historię, zmienione ścieżki, różnice kodu/danych/dokumentacji i zmiany robocze źródła. Bieżąca realizacja przeniosła wskazany wyżej zakres, w tym istniejące dane i referencje przygotowane w źródłowym Editorze; nie projektowano ani nie edytowano ręcznie Unity YAML. Batch runnery projektu docelowego zakończyły weryfikację bez ingerencji w aktywny Editor źródła. Wyniki i wyjaśnienie początkowych failure znajdują się na początku raportu.

Smoke użytkownika według `UNITY_HANDOFF.md` przeszedł; polecenie `git` autoryzowało scoped commit i push tej integracji. CP2-01 jest zaakceptowana. Późniejsza integracja macOS/X360 wymaga ponownego porównania finalnych commitów v0.1.3 z manifestem tej karty; nie wolno ponownie dokładać kopii już przeniesionych zmian. Źródłowy branch v0.1.2 nadal nie jest traktowany jako zamknięte wydanie.
