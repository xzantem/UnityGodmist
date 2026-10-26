# Godmist — project instructions

## Sources

Na początku karty przeczytaj `WORKFLOW.md`.

Pozostałe źródła czytaj selektywnie:
- `KANBAN.md` — aktualna karta/status;
- `PROJECT_CONTEXT.md` — tylko relewantne gameplay/UX rules;
- `CONVENTIONS.md` — normatywna architektura i konwencje;
- `ARCHITECTURE_SKETCH.md` — starszy reference, tylko gdy potrzebny;
- `IMPLEMENTATION_ROADMAP.md` — historyczny reference, nie backlog;
- `TESTING.md` — zasady automatycznej weryfikacji;
- `UNITY_HANDOFF_RULES.md` — tylko dla kart wymagających ręcznej pracy w Unity Editorze.

Workspace jest źródłem prawdy dla aktualnej implementacji.

Context ma być targeted: najpierw search/find, potem tylko relewantne fragmenty.
Nie czytaj ponownie tych samych dużych dokumentów w obrębie jednej karty bez nowej konkretnej potrzeby.

## Single owner

Główny wątek / Terra wykonuje całą kartę sam:
- zbiera context;
- szuka analogów i lokalnych patternów;
- zadaje blokujące pytania;
- projektuje;
- implementuje;
- testuje/debugguje;
- robi final self-review;
- projektuje UI;
- aktualizuje handoff.

Nie używaj subagentów.

## Unity boundary

AI może edytować kod i testy.

Bez jawnej zgody użytkownika NIE edytuj serialized Unity state:
- `.unity`
- `.prefab`
- serializowanych `.asset`
- `.meta`
- `ProjectSettings`
- Unity YAML
- GUID/fileID
- Inspector references zapisanych w scenach/prefabach
- hierarchy/GameObjectów/komponentów zapisanych w scenach/prefabach
- Transformów
- Tags/Layers
- Input Actions asset
- physics settings
- Build Settings

Nie obchodź brakującego Editor wiring przez `GameObject.Find`,
`FindObjectOfType`, service locator, reflection albo nowy globalny singleton/state.

Jeśli potrzebny jest ręczny Editor setup, przygotuj `UNITY_HANDOFF.md`.

## Handoff UI

Przed przygotowaniem handoffu UI:
- sprawdź istniejące prefaby, warianty i analogiczne widoki w projekcie;
- nie wymyślaj struktury ani domyślnych ustawień kontrolek UGUI;
- gdy prefab jest wariantem, opisuj wyłącznie różnicę względem odziedziczonego prefabu;
- opisuj pełne wartości każdego ustawienia, które użytkownik ma zmienić; nie używaj skrótów ścieżek ani niejednoznacznych opisów;
- nie podawaj ręcznie RectTransform dziecka sterowanego przez Layout Group: zamiast tego opisz ustawienia grupy oraz Layout Element; RectTransform podawaj tylko tam, gdzie layout nim nie zarządza;
- wyraźnie rozdziel statyczny styl prefabu od wartości zmienianych dynamicznie przez kod.

## Opisy mechanik w UI

Kategoryczny zakaz powielania pełnych zasad mechaniki w oknach, podglądach,
stopkach i stałych tekstach pomocniczych — powoduje to clutter.

Efekty pokazuj jako krótką nazwę i wartość, jednakowo w każdym oknie i tooltipie,
korzystając ze wspólnego formattera. Pełne zasady udostępniaj wyłącznie w dodatkowym
tooltipie/glosariuszu otwieranym świadomie przez użytkownika.

Okna mogą pokazywać koszt, szansę, aktualny stan, powód blokady i wynik wykonanej
akcji. Ryzyko akcji nie jest powodem do dodawania stałego objaśnienia mechaniki.

## Local-only

`WORKFLOW.md`, `UNITY_HANDOFF.md` i `UNITY_HANDOFF_RULES.md` są lokalne.

## Git acceptance

Przed smoke testem nie commituj i nie pushuj.

Wiadomość użytkownika `git` oznacza:
„smoke test bieżącej karty przeszedł; commit + push wyłącznie zmian tej karty”.

Nie stage'uj unrelated zmian użytkownika.
