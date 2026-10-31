# Katalog planowanego contentu do L40

Właściciel: CP2-02. Podsumowanie i decyzje są w `../../CONTENT_PASS_2_CATALOG.md`.

Pliki nie są katalogiem runtime i nie konfigurują Unity. Dodatni wynik walidacji oznacza osiągalność w grafie planowanych źródeł i receptur, nie gotowość wypraw ani gospodarki w grze.

## Pliki

- `content-manifest.json`: istniejące ID, proponowane dodatki, źródła, warianty receptur, odbiorcy, poziomy oraz adresy komórek.
- `proposed-ids.json`: jawne rezerwacje nowych ID. Nie są jeszcze dodane do gry. Korekta DisplayName istniejącego przedmiotu nie nadaje mu nowego ID.
- `natural-source-contract.json`: wcześniejsze naturalne przypisania, łącznie z przyszłymi biomami. Nowszy arkusz i jawne decyzje użytkownika mają pierwszeństwo.
- `material-sources.txt`: tabela przedmiotów i źródeł z separatorem `|`. Zawiera też jawnie oznaczone legacy i zakres powyżej L40.
- `validation.json`: wynik niezależnego przeliczenia grafu i kontroli inwariantów, z hashem badanego manifestu.

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
