# Katalog planowanego contentu do L40

Właściciel: CP2-02. Podsumowanie i decyzje są w `../../CONTENT_PASS_2_CATALOG.md`.

Pliki nie są katalogiem runtime i nie konfigurują Unity. Dodatni wynik walidacji oznacza osiągalność w grafie planowanych źródeł i receptur, nie gotowość wypraw ani gospodarki w grze.

## Pliki

- `content-manifest.json`: istniejące ID, proponowane dodatki, źródła, warianty receptur, odbiorcy, poziomy oraz adresy komórek.
- `proposed-ids.json`: jawne rezerwacje nowych ID. Nie są jeszcze dodane do gry. Korekta DisplayName istniejącego przedmiotu nie nadaje mu nowego ID.
- `natural-source-contract.json`: wcześniejsze naturalne przypisania, łącznie z przyszłymi biomami. Nowszy arkusz i jawne decyzje użytkownika mają pierwszeństwo.
- `material-sources.txt`: tabela przedmiotów i źródeł z separatorem `|`. Zawiera też jawnie oznaczone legacy i zakres powyżej L40.
- `validation.json`: wynik niezależnego przeliczenia grafu i kontroli inwariantów, z hashem badanego manifestu.
- `system-audit-validation.json`: kontrola integralności dokumentu CP2-03, jego odwołań do kodu i źródłowego hasha arkusza. Wynik analizy jest w `../../CONTENT_PASS_2_SYSTEM_AUDIT.md`; nie jest to test mechanik runtime.
- `progression-plan.json` i `progression-levels.txt`: zatwierdzony kontrakt CP2-04, 40 poziomów, profile bazowego wzrostu bohaterów, sekwencje istniejących rang drzew i cztery40-punktowe przykłady buildów. Kontrakt w `../../CONTENT_PASS_2_PROGRESSION.md`: content do40, postacie do50, obecna baza XP/SP, pięć biomów dostępnych od początku po authoringu. Odtwarzanie: uruchomić `build_progression_plan.py` bundled Pythonem z katalogu repo. Generator nie zapisuje Unity assets ani XLSX; wartości bazowe nie obejmują finalnego stat pipeline, a nowe T5 nadal wymagają implementacji we właściwych kartach.
- `progression-validation.json`: wynik faktycznie uruchomionych testów CP2-04, osobno przebieg pakietowy i retest, kontrola zachowania wcześniejszych zmian użytkownika oraz ograniczenia odbioru. Generator planu nie nadpisuje tego raportu wynikami obliczeń.
- `warrior-t5-validation.json`: wynik CP2-05 — authoring10 node'ów, zachowanie40 dawnych, idempotencja,335/339 w pierwszym przebiegu i454/454 w końcowym batchu;678 różnych końcowychPASS. Trening Skupienia ma jawny override+0,5 celności/PW w generatorze; stary opis zXLSX zachowany osobno w `workbook_description`.

- `man-at-arms-validation.json`: CP2-06, 25 węzłów T1–T5, 4 skille, 25 ikon; wyniki testów i hashe. T6/T7 Zbrojnego mają status `deferred_outside_v0.2.0`.

- `scout-t5-validation.json`: CP2-07, dziesięć węzłów T5, trzy skille, ikony i opisy rang; wyniki runtime/UI/tooltipów, authoringu i zachowania 40 starszych węzłów.

- `guardian-design-proposal.json`: propozycja CP2-08 v6, dokładnie 4 aktywne w całym T1–T7 (3 w T1–T5, czwarta w T7); bez Kontuzjowania. T1–T5: 25 węzłów/44 rangi; pełna propozycja: 29 węzłów/50 rang. T6/T7 nie są automatycznie zakresem v0.2.0. Dwie legalne ścieżki po 40 SP i pełne drzewo 50 SP. Raport `guardian-design-validation.json` sprawdza graf, nie runtime ani balans.

## Aktualizacja

Użyć Pythona z `openpyxl`, np. bundled runtime Codexa. Z katalogu głównego repozytorium:

```powershell
& 'C:/Users/Patryczku/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -X utf8 'Godmist Unity/Tools/ContentPass2/build_catalog.py'
& 'C:/Users/Patryczku/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -X utf8 'Godmist Unity/Tools/ContentPass2/validate_catalog.py' --self-test
```

Generator czyta `design/content_pass_2.xlsx` i rzeczywiste katalogi/asset metadata. Zachowuje ID z aktualnego katalogu. Nie stosuje fuzzy matching. Dopuszczone aliasy pisowni i rozstrzygnięcia użytkownika są jawne w kodzie i manifeście.

Bez przełącznika `--reserve-ids` generator odrzuci nieznaną nową nazwę/pozycję zamiast samodzielnie przydzielać jej tożsamość. Po przeglądzie zmiany projektu można użyć tego przełącznika i obejrzeć diff rejestru. Przenoszenie node'a lub zmiana nazwy wcześniej zarezerwowanej pozycji wymaga aktualizacji jej mapowania, nie tworzenia nowej rezerwacji.

Generator jest ekstraktorem aktualnego układu arkusza, nie uniwersalnym importerem do Unity. Po przeniesieniu tabel lub kolumn trzeba poprawić mapowanie i zweryfikować komórki. Nie zmieniać `.xlsx`, `.asset`, `.meta`, scen, prefabów ani zapisów przez te skrypty.

## Ograniczenia

Graf wylicza najwcześniejszy poziom zebrania składników w planie; dopuszcza zebranie materiałów z wcześniejszych wypraw. Nie modeluje ilości, czasu farmienia, szans, rotacji sklepu ani finalnych blokad ofert z CP2-04. `ResultLevel` starych receptur jest poziomem wyniku, nie automatycznie progiem odblokowania; nie utożsamiać go z poziomem zdobycia wejść.

Warianty receptur są rozdzielone. Wszystkie jawne warianty i każdy dopuszczony metal L21 są kontrolowane; produkt nie jest uznawany za osiągalny tylko dlatego, że ma dowolny wpis „crafting”. Przed authoringiem trzeba domknąć brakujące ilości wynikowe, balans i odłożone projekty wskazane w podsumowaniu.

Stabilne ID efektów skilli i nagród muszą być nadawane przez istniejący authoring CP2-01. Ten rejestr nie regeneruje opublikowanych ID ani historycznych aliasów zapisów.

- `guardian-preview-content.json`: redakcyjne opisy wszystkich 50 rang projektu v6 i pełne reguły. `guardian-preview-validation.json`: podpięcie 29 węzłów do klasy, blokada SP, brak efektów, testy widoku i progresji.

Strażnik v6: trzy niezależne kierunki, dwa ulepszenia Osłony, 17 krótkich połączeń, capstone za 30 SP bez prerequisite. Historyczny podgląd CP2-08 nie wdrażał mechanik; produkcyjne T1–T5 wdraża CP2-09.

V5: równoległe ulepszenia Odwetu, krótkie opisy i wspólne objaśnienia wartości/format kosztów. Jeden link pełnych zasad na węzeł.


CP2-09 zastępuje dawny podgląd Strażnika produkcyjnym T1–T5.
`guardian-content.json` jest manifestem authoringu: 25 węzłów/44 rangi,
3 SkillData, bez T6/T7. `guardian-preview-content.json` i raport preview
pozostają dowodem historycznego przeglądu, a nie konfiguracją runtime.
Menu Godmist/Content Pass 2/Guardian wywołuje authoring produkcyjny;
stary helper preview przekierowuje do niego, nie przywraca 29 węzłów.
`guardian-validation.json`: rzeczywiste wyniki CP2-09 i hashe plików.
Opis: `../../CONTENT_PASS_2_GUARDIAN.md`.
Ikony: `../Art/GuardianIcons20261102/` — źródła ImageGen, edytowalne Aseprite,
eksporty native32, manifest i kontaktowy podgląd wszystkich 25 ikon.


`talent-reference-audit-validation.json`: rozszerzenie audytu tooltipów na
10 drzewek/230 węzłów, 460 wariantów rang; właściwe ID, ikony, odmiany
nazw i izolacja kontekstu klasy. Opis: ../../CONTENT_PASS_2_TALENT_REFERENCE_AUDIT.md.
