# VS-02 — review gotowości pod content pass

2026-09-22. Status: DONE — raport zaakceptowany przez użytkownika 2026-09-22; bez wymaganego Play Mode dla samego review. VS-01: commit f8915c35. Zakres: zatwierdzony VS01_CONTENT_CATALOG.md, bieżący workspace wraz z niecommitowanymi pakietami VS-18. Raport nie jest potwierdzeniem działania niezaimplementowanego contentu.

## Wniosek

Istniejący podział Data/Runtime/Systems/Views oraz wspólne klasy, zasoby, progresja i efekty aktywnych skilli nadają się do rozbudowy. Nie ma podstaw do przepisywania core combat, modelu klas ani save/load. Przed authoringiem drzewek potrzebne są wspólne rozszerzenia pasywów, kosztów i walidacji w VS-03. Pozostałe braki mają konkretne karty właścicielskie.

Ścieżki kodu poniżej są względne do Assets/_Project/Scripts, assetów do Assets/_Project/ScriptableObjects. Priorytet P1 oznacza blocker wskazanego contentu, nie błąd każdej obecnej walki. P2 oznacza ryzyko danych lub konieczną kontrolę integracyjną.

## Znaleziska i działania

### R01 — P1: pasywy deklarują więcej operacji niż wykonuje runtime

Dowód: Core/Enums/PassiveEffectOperationType.cs deklaruje Heal, RestoreResource, DealDamage, ModifyResourceCost i ModifyStatus. Systems/Combat/PassiveEffectCombatExecutionSystem.cs / ExecuteOperation obsługuje ApplyStatus, ModifyStat, SetRuntimeFlag; DuplicateAction i PreventDamage mają osobne ścieżki. Systems/Combat/PassiveEffectRuntimeFactory.cs materializuje statyczne ModifyStat/ModifyResistance/ModifyGameplayVariable. Nie ma równoważnego wykonania wymienionych reaktywnych Heal/RestoreResource/DealDamage w tych ścieżkach.

Skutek: np. odzysk surowca przy ataku, leczenie po wydaniu surowca lub obrażenia uruchamiane pasywem nie stają się działające przez wybranie wartości enuma w Inspectorze. Nie mylić z działającymi Heal/RestoreResource jako efektami aktywnych skilli.

Właściciel: VS-03. Uzupełnić wymagany przez katalog zestaw operacji w obecnym wykonawcy, używając istniejących systemów obrażeń/leczenia/surowca. Określić kontekst i kolejność zdarzeń, zapobiec rekurencji, rozróżnić raz na akcję od każdego trafienia. Nie implementować wszystkich hipotetycznych enumów bez konsumenta.

### R02 — P1: koszt surowca nie przechodzi przez wspólny resolver

Dowód: Systems/Combat/SkillExecutionSystem.cs wydaje ResourcePool.Drain(skill.ResourceCost), a walidacja porównuje pulę z skill.ResourceCost. Systems/Combat/SimpleCombatAiSystem.cs / CanPayResource również używa tej surowej wartości. ModifyResourceCost nie ma wykonania w zbadanych systemach combat/characters. CombatActionCostCalculator rozwiązuje koszt PA, nie ten koszt surowca.

Skutek: Wściekłe Uderzenia i redukcja many z Czarnoksiężnika wymagają pracy programistycznej. Sama zmiana statycznego SkillData albo samo zmniejszenie pobrania kosztu da rozjazd dostępności, AI i UI.

Właściciel: VS-03. Jeden resolver efektywnego kosztu dla sprawdzenia, wydania, AI i podglądu. Obsłużyć selekcję ofensywnych skilli bez mutowania współdzielonego assetu. Testy kilku postaci z tym samym skillem, kosztu 0, redukcji, braku zasobu oraz toggle upkeep zgodnie z zakresem danej pasywki.

### R03 — P1: modyfikatory skilli i walidacja są za wąskie na cały katalog

Dowód: PassiveEffectCombatExecutionSystem.ApplyDamageMultiplier rozpoznaje ModifySkillParameter z parametrem damage_multiplier. Nie jest to ogólny resolver zasięgu, liczby trafień, czasu statusu czy szansy. PassiveEffectDefinitionValidator.ValidateOperationReference sprawdza niepustą referencję, a ValidateTriggers głównie null; poprawna struktura definicji nie dowodzi obsługi jej kombinacji przez wykonawcę.

Skutek: ulepszenia aktywnych skilli z ośmiu drzewek wymagają jawnego pokrycia parametrów. Można dziś opisać pasywkę, której runtime nie wykona.

Właściciel: VS-03. Rozszerzyć obecny model tylko o parametry wymagane przez T1–T4; spójny wynik dla wykonania/targetingu/podglądu/AI. Walidator ma odrzucać nieobsługiwane operacje i parametry z czytelnym komunikatem. Zachować ID skilli i węzłów.

### R04 — P1: specjalne mechaniki drzewek wymagają dedykowanej integracji

Agresywny Ogień: DamageOverTimePassiveEffectProcessor i runtime efektów nie zapewniają jawnego modelu numeru pokolenia/rodzica podpalenia; nie znaleziono ścieżki propagacji wymaganej przez katalog. VS-09: zachować siłę 50% rodzica i numer pokolenia przy odświeżaniu/kopiowaniu, maksimum trzy pokolenia; leczenie z Żywienia Ogniem na podstawie faktycznych obrażeń i właściwej własności efektu.

Pole Osądu: SummonedObjectSystem.RemoveOldestObjectsOverLimit liczy limit po creatorCharacterId + skillId + effectIndex. Limit 1 w tym systemie nie jest gwarancją jednego pola na całej mapie. Obiekty mają własną szybkość i udział w kolejce AV. VS-09: jawna grupa wyłączności pola na mapie, zastąpienie, właściwe tykanie i efekty przedostatniej/ostatniej tury; sprawdzić semantykę zastąpienia wobec efektu końcowego przed authoringiem.

Złość, narastanie efektów na konkretnym celu i modyfikatory zależne od aktualnego zasobu: obecne runtime state/stacks są bazą, lecz nie dowodem gotowości całych mechanik. VS-07–09: wdrożyć konkretnych konsumentów po wspólnych rozszerzeniach VS-03, z testami zmiany celu, śmierci i końca walki.

### R05 — P1: boss nie jest osiągalny samą konfiguracją istniejącego summon

Dowód: SkillEffectData wskazuje jedną SummonedCharacterDefinition i klasę. SummonSystem używa CharacterInstanceFactory.Create bez jawnego poziomu, czyli domyślnie L1. Limit usuwa najstarszy summon danego źródła. SummonedObjectDefinition zapewnia własny czas/szybkość, zasięg i automatyczne efekty, ale nie opisuje pięciu losowych run ze skasowaniem przez wejście i wyborem najbliższego celu przy detonacji. OnDamageThresholdReached jest zadeklarowany, lecz nie znaleziono publikacji tego zdarzenia w systemach walki.

Właściciel: VS-16. Dodać sekwencję progów 75/50/25% odpalanych raz na próg, losowanie szkieleta z puli, jawny poziom oraz runy deaktywowane wejściem/detonujące po właściwych dwóch turach. Reuse SummonSystem, obiektów, zdarzeń wejścia na pole i kolejki efektów. Przy authoringu ustalić, czy przekroczenie kilku progów jednym ciosem odpala kilka fal i czy limit 3 blokuje przywołanie czy zastępuje starego. To brakujące szczegóły wykonania VS-16, nie powód do przebudowy core teraz.

### R06 — P1: generowany loot nie respektuje wszystkich parametrów passu

Dowód: LootTableSystem.CreateItem przekazuje do EquipmentGenerationService.Generate pełne equipmentParts bez filtrowania lootLevel. Generator losuje spośród wszystkich kompletnych LevelBracket danej klasy. LootBagPoolEntryData ma wyłącznie ItemDefinition i Amount, bez specyfikacji jakości/tieru. Zatem po dodaniu T2 zwykła torba L1–10 może wylosować T2. Nie da się samą obecną pozycją puli wyrazić odrębnych wariantów Słaby/Normalny/Doskonały z tabeli.

Galduryt w tej ścieżce trafia do ItemInstanceFactory: konstruktor GalduryteItemInstance nie losuje zestawu efektów. Nie jest to gotowy losowy galduryt L11 z wymaganymi parametrami.

Właściciel: VS-11/15. Rozszerzyć specyfikację generowanych wpisów i przekazywać ograniczenia do istniejących generatorów; zachować zachowanie prostych materiałów. Testy izolacji T1/T2, jakości, braku kompletnej puli, pełnego payloadu galdurytu i save/load. Semantyka niezależnych kategorii oraz równego wyboru entry już istnieje i nie wymaga nowego systemu.

### R07 — P1: galduryty mają tylko sześć działających typów efektów

Dowód: GalduryteEnums, GalduryteRuntimeRules (po 3 efekty na cel), PassiveEffectReconciliationSystem (6 przypadków naliczania). Samo dopisanie wartości enuma nie wystarczy.

Właściciel: VS-11. Rozszerzyć pulę/siły, generowanie, payload, naliczanie, tooltipy i odtwarzanie zapisów. Wspólne reakcje przy ataku oprzeć na VS-03. Nie zmieniać istniejących numerów enumów; pierwszy tier nadal L11.

### R08 — P1: start kampanii nadal zależy od sceny sandboxowej

Dowód: BootstrapMenuView ma przyciski StartExploration/StartCombat. GameBootstrap tworzy pustą GameSession. ExplorationSandboxBootstrap.Start iteruje serialized partyBindings, tworzy brakujących bohaterów i dodaje ich do drużyny; GetOrCreateCharacter wymaga binding.ClassData. CharacterInstanceFactory tworzy pusty Inventory i inicjalizuje skille, nie produkcyjny startowy komplet sprzętu. CD_Wladimir_Test.ClassData jest puste (binding może to dziś maskować).

Właściciel: VS-04/05/06. Produkcyjny start ma tworzyć zatwierdzony roster/loadout raz, a wejście do miasta wiązać istniejącą sesję z widokami. Nie przyznawać startowego wyposażenia przy każdym wejściu/wczytaniu. Zachować obecne scene/save flow i mechanizm rosteru, bez nowego globalnego właściciela. Wladimir wymaga prawidłowej definicji klasy.

### R09 — P2: aktualny katalog ma trzy wiszące referencje po usuniętych testowych assetach

Dowód: World/GC_MainContentCatalog.asset:58–60 wskazuje GUID-y b6d1f8a3c5e74920a4d7b2f9c6e1a580, c7e2a9b4d6f85130a5c8e3f1b7d9a260, d8f3b1a6c9e24750a4d7e2f8b5c1a690. Bieżące Assets nie mają odpowiadających .meta. Git HEAD potwierdza odpowiednio ID_TestGalduryte, ID_TestPotion, ID_TestValuable. Są to istniejące lokalne usunięcia, nie zmiany tego review.

Właściciel: VS-05/11 — uporządkować referencje w Editorze zgodnie z decyzją o usunięciu testowego contentu; nie przywracać samowolnie assetów. Zapis zawierający usunięte ID nie może być uznany za automatycznie kompatybilny. Ustalić migrację/fallback lub jawny zakres kompatybilności przed publikacją contentu.

### R10 — P2: authoring musi zachować tożsamość danych i korzystać z istniejącego walidatora

Dowód: GameSessionRestoreFactory rozwiązuje ID bohatera, klasy i itemów przez katalog oraz sprawdza Enum.IsDefined dla efektów galdurytu. ContentValidator.ValidateProject już sprawdza duplicate IDs, dangling references, membership i kompatybilność wyposażenia. SkillTreeProgressionSystem wybiera drzewka z ClassData, bez warunku wyłącznie Scouta.

Właściciel: wszystkie karty authoringu, rozszerzenie capability validation w VS-03. Zachować ID i numery enumów; zmiana nazwy widocznej nie wymaga zmiany ID. Nowe drzewka dopisywać do właściwych klas, nie kopiować skopiowanych węzłów Assassin jako gotowego Tancerza. Na końcu każdej karty uruchomić istniejącą walidację i test odtworzenia jej danych.

## Pokrycie ośmiu drzewek — plan wykonania

| Drzewko | Baza do wykorzystania | Brak / karta |
| --- | --- | --- |
| Dowódca | Statusy, modyfikatory, przemieszczenia, ModifyActionValue, redirect | Reaktywne pasywy i parametry: VS-03; warunki sąsiadów/rozkazów i definicje: VS-08 |
| Awanturnik | ResourcePool Rage, buffy, zdarzenia obrażeń/surowca | Resolver kosztów i reakcje: VS-03; konkretne reguły Amoku/zemsty: VS-08 |
| Skrytobójca | Sztylety, bleed, crit, skille Scouta | Parametry skilli: VS-03; wbicie/wyjęcie i wariant bossa Podcięcia Gardła: VS-07 |
| Tancerz Ostrzy | Wielokrotne trafienia, stacki/runtime state | Złość per cel i tempo narastania: VS-07; wspólne efekty: VS-03 |
| Czarnoksiężnik | Mana, toggle, poświęcenie HP, tarcze | Koszty/odzysk i modyfikatory: VS-03; specyficzne kryty i debuffy: VS-09 |
| Mag Ognia | DoT, odporności, obrażenia magiczne | Pochodzenie/pokolenia podpaleń i leczenie: VS-09, wspólne reakcje po VS-03 |
| Kapłan | Heal, tarcza, AV, cleanse | Wyzwalacze leczenia i parametry celów: VS-03/09; pełny authoring: VS-09 |
| Krzyżowiec | Aura, summoned objects, obrażenia, statusy | Jedno Pole Osądu na mapie i lifecycle: VS-09; warunki tagów reuse |

Tabela rozdziela istniejące prymitywy od gotowości całych node’ów. Nie oznacza, że wszystkie 160 węzłów przeszły test wykonania — nie są jeszcze authored.

## Minimalny zakres VS-03

1. Wspólny resolver kosztu zasobu, używany przez walidację, wydanie, AI i UI/podgląd.
2. Uzupełnienie wymaganych operacji reaktywnych pasywów w istniejącym wykonawcy i kontekstu zdarzeń, z kontrolą rekurencji i wielokrotnych trafień.
3. Modyfikatory parametrów aktywnego skilla wymagane przez katalog, bez mutacji assetów i z jednym wynikiem dla wykonania/podglądu.
4. Walidacja realnego pokrycia operacji/parametrów; konkretne testy reprezentatywne dla czterech klas.

Poza VS-03: authoring 160 node’ów, generator lootu, runy bossa, propagacja podpaleń i Pole Osądu, pełne menu, assety i migracja usuwanych testowych przedmiotów. Nie blokować rozpoczęcia klas na przebudowie całego projektu. Nie powtarzać wdrożonych pakietów VS-18 (PA, penetracja, CC, XP, obóz, posiłki, ekonomia usług).

## Weryfikacja i self-review

- Wykonano punktową inspekcję danych, wykonawców, fabryk, AI, ścieżek kosztów/generowania, walidacji i restore.
- Automatyczna kontrola tekstowa aktualnego kodu potwierdziła sześć przesłanek R01/R02/R03/R04/R06. To kontrola statyczna, nie test runtime.
- Sprawdzono 171 odwołań GUID katalogu głównego: trzy nie mają .meta w Assets; porównanie z HEAD potwierdziło tożsamość usuniętych testowych plików.
- Końcowy review: każde znalezisko ma konkretny skutek, dowód i właściciela. Nie wyciągano gotowości mechaniki wyłącznie z istnienia enuma. Rozróżniono lokalne zmiany użytkownika i braki planowanego contentu.
- Zmiany tego etapu: wyłącznie raport i zakres/status w Kanbanie. Build/EditMode/PlayMode NOT RUN — nie zmieniono kodu, a raport nie deklaruje runtime PASS. Testy wykonania należą do implementacji przypisanych kart.
- Brak nowego Editor setupu. Istniejący UNITY_HANDOFF.md z pakietami VS-18 pozostaje aktualny i nie jest zastępowany przez ten raport. Użytkownik zatwierdził zamknięcie i commit VS-02.
