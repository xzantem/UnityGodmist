# CP2-05 — T5 Dowódcy i Awanturnika

Status: **ZAAKCEPTOWANA**, 2026-11-01. Użytkownik polecił commit/push i przejście doCP2-06. Pierwszy etap wykonany2026-10-31. Dziesięć efektów zatwierdzonych, kod i authoring przygotowane. Nie przywracamy starej kary celności od furii.

## Źródło i granice

`../design/content_pass_2.xlsx`, arkusz `Drzewka!A1:E12`, SHA256 `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`. UwagaA1 ostrzega przed przenoszeniem dawnych wartości bez uwzględnienia polishu. ID z rejestru CP2-02 pozostają obowiązujące, także po zmianie nazwy/efektu węzła.

CP2-04: T5 wymaga20 punktów wydanych w konkretnym drzewie; zakup każdej rangi kosztuje1SP. Pierwsza ranga wymaga zatem co najmniej21SP. Limit postaci50, content opracowywany do40. T1–T4 zachowują opublikowane ID, rangi, prerequisite i zatwierdzony balans. Bez nowej postaci, trzeciego drzewa lub kontrolowanego RNG.

## Węzły

Wszystkie poniższe ID mają prefiks `node.warrior.`. Podane dodatkowe prerequisite wymagają co najmniej jednej rangi; oprócz nich zawsze obowiązuje bramka20SP.

| Drzewo / slot | Końcówka ID | Nazwa | Rangi | Dodatkowe prerequisite | Efekt |
|---|---|---|---:|---|---|
| Dowódca T5:1 | commander.t5_1 | Egida | 2 | commander.regroup, T4:1 | Cel Rozkazu Gotowości otrzymuje +15%/30% zadawanych obrażeń przez3 tury. |
| Dowódca T5:2 | commander.t5_2 | Egzekucja | 1 | commander.t5_3 | Krytyk:10% szansy na egzekucję zwykłego celu kwalifikującego się do śmierci od pięciokrotności obrażeń. Na bossie zamiast zabicia podwójne obrażenia. |
| Dowódca T5:3 | commander.t5_3 | Pojedynek | 2 | — | +10%/20% szansy krytycznej przeciw przyległemu wrogowi, jeśli jest jedynym wrogiem w odległości1. |
| Dowódca T5:4 | commander.t5_4 | Szermierka | 2 | commander.t5_3 | Każde użycie ataku w ten sam cel dodaje ładunek +2%/4% obrażeń, maksymalnie5 ładunków. Zasady niżej. |
| Dowódca T5:5 | commander.t5_5 | Oko Dowódcy | 1 | — | +2 do zasięgu istniejących rozkazów. Nie zwiększa AoE ani zasięgu dowolnego skilla Wojownika. |
| Awanturnik T5:1 | brawler.t5_1 | Trening Skupienia | 1 | — | Każdy punkt aktualnej wściekłości daje +0,5 celności umiejętności; snapshot przed opłaceniem ataku. |
| Awanturnik T5:2 | brawler.t5_2 | Szał Bitewny | 2 | brawler.t5_3 | +30% obrażeń poniżej10%MaxHP / +50% poniżej20%MaxHP. Próg jest ścisły: równe10%/20% nie spełnia warunku. |
| Awanturnik T5:3 | brawler.t5_3 | Furia Bitewna | 2 | — | +0,003x/0,005x mnożnika krytyka za każdy brakujący punkt procentowy HP. Nie zwiększa szansy krytycznej. |
| Awanturnik T5:4 | brawler.t5_4 | Powrót do Żywych | 2 | brawler.death_wish, T4:4 | Po wyłączeniu Pragnienia Śmierci leczy25%/50% obrażeń zatrzymanych wyłącznie przez jego ochronę przed śmiercią. |
| Awanturnik T5:5 | brawler.t5_5 | Nieśmiertelna Furia | 1 | brawler.t5_4 | Każda osobna porcja obrażeń śmiertelnych zatrzymanych przez aktywne Pragnienie Śmierci generuje10 wściekłości, z zachowaniem blokady generacji i limitu zasobu. |

## Decyzje użytkownika i zgodność z bieżącą grą

1. **Powrót do Żywych:** leczenie przy każdym wyłączeniu Pragnienia Śmierci, także ręcznym. Bank obejmuje tylko faktycznie zanegowaną część obrażeń już po obronie, przekierowaniach i tarczach. Nie liczy całej wartości ataku ani szkody pochłoniętej tarczą. Zamknięcie cyklu wypłaca go raz i zeruje stan; wyjście z walki nie przenosi banku do następnej instancji. Nie jest to wskrzeszenie martwego aktora.
2. **Szermierka:** jeden ładunek na użycie ataku, nie na hit; pierwszy atak daje pierwszy ładunek i korzysta z niego. Pudło liczy się jako atak. Zmiana celu resetuje serię. Atak wielocelowy resetuje serię. Wsparcie, ruch i sam tickDOT nie są nowym użyciem ataku. Preview nie może zużywać ani zwiększać stosu, AI i wykonanie muszą korzystać z tej samej prognozy.
3. **Egzekucja:** na bossie krytyk ma10% szansy na podwójne obrażenia zamiast zabicia; zwykły przeciwnik zachowuje warunek pięciokrotności. Analogia: obecne Podcięcie Gardła zwiadowcy. Rozliczyć jeden kontakt, bez sztucznego drugiego zabójstwa, kolejnego hit rolla lub dodatkowego lootu. Zwykły próg oceniać względem stanu celu przed kontaktem, nie dopiero HP pomniejszonego o ten sam atak.
4. **Trening Skupienia:** zatwierdzone +0,5 celności umiejętności za punkt aktualnej wściekłości, przed zapłatą kosztu. Przy50PW daje+25 do celności skilla; nie jest to+25 punktów procentowych finalnej szansy trafienia. Snapshot obejmuje cały cast i wszystkie hity, odtwarza stan po zagnieżdżonym caście i nie pozostaje po zakończeniu akcji. Nie wprowadzamy bazowej kary −0,2 celności/punkt furii. Manifest zachowuje osobno stary opis zD8 i jawną decyzję użytkownika; XLSX niezmieniony.

Arkusz nazywa cel Egidy „Rozkazem Przegrupowania”. W grze odpowiada mu istniejące `skill.warrior.regroup`, obecnie **Rozkaz Gotowości**, node `node.warrior.commander.regroup`. Nie tworzyć drugiej umiejętności ani nie wracać do starej nazwy. Aktualne Pragnienie Śmierci jest toggle z upkeepem max(20PW,25%MaxPW), bez określonego limitu tur; nie zamieniać go w timed skill, aby dopasować stary opis arkusza.

## Punkty integracji

Ścieżki względem `Assets/_Project/Scripts/`:

| Wymaganie | Obecny owner | Realizacja w tej karcie |
|---|---|---|
| Rangi i dynamiczne bonusy Wojownika | `Systems/Combat/WarriorCombatSystem.cs` i `.Effects.cs`, `Runtime/Combat/WarriorCombatState.cs` | Nowe rangi czytać po zarezerwowanych ID. Osobny encounter-local stan Szermierki/banku Pragnienia Śmierci; nie używać licznika ConsecutiveCuts jako drugiego zastosowania. |
| Egida | `SkillExecutionSystem`, wykonanie ModifyActionValue Rozkazu Gotowości | Nakładać na rzeczywisty cel rozkazu, niezależnie od posiadania Marszu Taktycznego. Użyć istniejącego lifecycle i źródłowanego modyfikatora damage_dealt; ponowne nałożenie według standardowego ReplaceDurationAndValues zamiast nieskończonego sumowania. |
| Zasięg rozkazów | `PassiveEffectCombatExecutionSystem.Parameters.ResolveMode` | Bonus do AbsoluteRange trzech istniejących rozkazów: destruction/protection/regroup, poID. Nie do Unieruchomienia lub dowolnej umiejętności zawierającej słowo w opisie. Klon parametrów, bez mutacji wspólnego assetu. |
| Szermierka | `SkillExecutionSystem` punkt rozpoczęcia jednej akcji i `WarriorCombatSystem.BeginSkill` | Jedna aktualizacja na akcję; rzeczywiste cele, nie liczba efektów/hitów. Klonowanie/dodatkowa pasywna akcja wymaga ochrony przed podwójnym naliczaniem tej samej ActionId. |
| Pojedynek i Furia Bitewna | `CombatDamageSystem` wspólna szansa/mnożnik krytyka oraz istniejące preview | Ten sam bonus i próg w preview, AI i wykonaniu. Stan HP odczytywany aktualnie; leczenie natychmiast usuwa niezasłużony bonus. Pojedynek korzysta z obecnej legalnej przyległości i nie liczy martwych aktorów. Rozszerzenie footprintu pozostaje w25A. |
| Egzekucja | `CombatDamageSystem.ResolveSuccessfulHit` | Jedno losowanie10% dla kwalifikującego się krytyka, ochrona przed ponownym procem. Uwzględnić tarczę i faktyczne redukcje przy kwalifikacji zwykłego celu; boss używa zatwierdzonego wyjątku. Deterministyczny punkt testowy zamiast zmiany RNG gry. |
| Szał Bitewny | `CombatVariableSystem` / wspólne outgoing modifiers | Zachować wspólny model składania bonusów damage_dealt i wyjaśnienia wartości; przetestować także DOT, gdy korzysta z tego samego outgoing pipeline. |
| Zanegowane obrażenia i10PW | `CombatDamageSystem.ApplyDamage` przy ograniczeniu doHP−1 | Zmierzyć różnicę przed/po ograniczeniu Pragnienia Śmierci, już po tarczy. Jedno źródło prawdy dla banku i śmiertelnego proca. Jest to wymagany fragment współdzielonego kontraktu06A; nie tworzyć osobnej alternatywnej kopii obliczania obrażeń w każdym node. |
| Zakończenie Pragnienia Śmierci | `SkillExecutionSystem.DeactivateToggle` i wywołania upkeep/manual/cleanup | Wypłata po usunięciu toggle i jego modyfikatorów, wyzerowanie banku przed heal i zabezpieczenie przed ponownym wywołaniem. Brak leczenia z usuniętego talentu albo po finalnej śmierci. |

## Authoring i prezentacja

- Celowane narzędzie Editor API aktualizuje wyłącznie istniejące `ST_Warrior_Commander.asset` i `ST_Warrior_Brawler.asset`, dodając lub aktualizując slotyT5. Bez ręcznej edycji YAML, GUID i scen.
- Nie uruchamiać pełnego `VS08WarriorSetup.ConfigureTrees`: czyści całą listęNodes i odtwarza wyłącznieT1–T4. `GodmistSkillTreeSetup` jest ownerem istniejącego UI, a nie bezpiecznym importerem nowych danych drzewa.
- Zachować pierwsze20 węzłów każdego drzewa i ich pełne dane. Zaktualizować powiązane źródła authoringu tak, aby późniejszy świadomy rebuild nie usuwałT5; nie nadpisywać przy tej okazji nowych zmian kitu Wyładowania.
- Piąty rząd korzysta z istniejącej obsługi tierów1–7 w widoku. Najpierw sprawdzić rzeczywisty layout i połączenia na aktualnym prefabie. Nie dodawać nowego UI ani stałego tekstu tłumaczącego zasady.
- Ikony dobrać z istniejących ikon i analogicznych efektów, bez pustych referencji; jawnie zapisać mapę źródeł. Jeżeli nowe ilustracje okażą się potrzebne, traktować je jako świadomy zakres artu, nie ukrytą zależność kodu.
- Trening Skupienia ma już zatwierdzony i wdrożony efekt+0,5/PW; wszystkie10 węzłów są authorowane.

## Testy i zgodność zapisów — plan odbioru

1. Dane:10 nowych nodeID, po5 na drzewo; właściwe rangi, prerequisite i ikony; idempotentny focused authoring. Żadnej niezamierzonej zmianyT1–T4 ani innych drzew.
2. Progresja:19/20 wydanychSP, brak wolnegoSP, prerequisiteT4/T5, każda ranga i reset; istniejące inwestycje z save zachowane. Test danych zaktualizowano z40 do50 node'ów, zachowując osobną kontrolę dawnych40.
3. Egida:oba ranki, właściwy sojusznik, brak zależności od Marszu Taktycznego,3tury i odświeżenie, reset źródła.
4. Pojedynek:0/1/2przyległych wrogów, cel poza dystansem, martwy sąsiad i ściana. Furia Bitewna:0/50/prawie100% brakującegoHP i oba ranki, zgodność podglądu z wykonaniem.
5. Szermierka:1–5ładunków i limit, zmiana celu, AoE, multi-hit, pudło, wsparcie i preview; reset/utrata talentu nie zostawia bonusu.
6. Egzekucja:crit/noncrit, oba wyniki rzutu, próg tuż przed/na granicy, tarcza i redukcje, boss, cel zabity zwykłym ciosem, jedna nagroda.
7. Szał:oba progi, dokładnie10/20% i ułamki tuż poniżej, leczenie ponad próg, składanie z DeathWish i innym buffem.
8. Powrót/Nieśmiertelna Furia:częściowo śmiertelny hit, HP1, pełna/częściowa tarcza, DOT i multi-hit, blokada generacji, limitPW, manual/off z braku upkeepu, dwukrotne wyłączenie i ponowna aktywacja. Śmierć/reset/koniec walki bez przenoszenia banku.
9. Regressions:WarriorContent/Discharge, rage economy, statusy, toggle/upkeep, damage/preview, skill tree, save/load. Nie poszerzać całego suite bez nowej przyczyny.
10. Build obu drzew w walce na legalnie zdobytychSP i renderT5/tooltipów. Test PlayMode potwierdził zakupT5 po legalnym zdobyciu21SP i wydaniu20 w poprzednich tierach oraz inspectability obu nowych rzędów. Render pikselowy i ręczny gameplay pozostają smoke użytkownika.

Stan walki pozostaje encounter-local: brak zapisu w połowie walki i brak potrzeby utrwalania liczników w DTO. Nowe nodeID są addytywne; stary save zachowuje wolneSP i inwestycjeT1–T4. Nowe węzły są dostępne do zakupienia po spełnieniu istniejącego kontraktu.


## Wykonany authoring

`ContentPass2WarriorT5Setup.Apply` dodał po5 węzłów do istniejących assetów. Editor potwierdził zgodność pełnej serializacji40 wcześniejszych node'ów przed/po i idempotencję powtórnego authoringu. Początkowo użyte ikonyT1–T4 zastąpiono2026-11-01 dziesięcioma dedykowanymi ikonami opisanymi poniżej. Wspólne narzędzie VS08 dołącza tę samą definicjęT5 i dedykowane ikony po swoim świadomym pełnym rebuildzie.


## Wynik weryfikacji

Pierwszy przebieg:335/339PASS. Cztery failure dotyczyły licznika pasywek133→143, dwóch nieaktualnych oczekiwań tekstu i założenia, że realny opis zawsze jest krótki. Zachowano ścisłe asercje nowego formatu i powiązano widoczność scrollbara z rzeczywistym overflow. Dodatkowo formatter list rang obsługuje teraz jednostkęx; Furia Bitewna pokazuje tylko właściwą wartość aktualnej/następnej rangi.

Końcowy izolowany batch: **454/454PASS**. Po połączeniu najnowszych wyników per przypadek z pierwszym przebiegiem: **678 różnych testówPASS,0FAIL**. W tym34 nowe przypadki mechanikT5 oraz nowy test całego przepływu zakupu/inspekcji wUI. PlanowanieAI kopiuje stan Szermierki, banku i reszty leczenia, a cache uwzględnia ich zmianę. Podgląd progówHP uwzględnia leczenie pasywu Awanturnika przy zapłacie kosztu; snapshot celności nadal pochodzi sprzed zapłaty.

Pełne porównanie plikówC# źródła i kopii testowej: zgodne.40 dawnych node'ów porównano także bajtowo jako rekordy serializowane: bez zmian. Dziewięć wcześniejszych zmienionych/usuniętych plików użytkownika zachowanych według hashy. Narzędzie lokalnego Editora usunięto przezAssetDatabase. Po uwadze użytkownika nie wykonywano dalszych operacji aktywujących okna ani skrótów klawiaturowych; dalsze testy uruchamiano w ukrytej kopii batch.

Dowody: `../LocalHandoffs/cp2-05-warrior-t5-20261031/verification.json`, `tests.xml`, `batch-final.xml`, `content-check.json`, `authoring.txt`. Wersjonowany skrót: `Tools/ContentPass2/warrior-t5-validation.json`. Walidator katalogu30/30PASS, XLSX niezmieniony. Bez zmiany schematu save, enumów, numeru aplikacji i migracji istniejących inwestycji. Bez commita i pusha. Nie wykonywano nowego builduWindows, testuFPS ani ręcznego/pikselowego odbioru widoku.

Nie ma ręcznego tworzenia obiektów i wiring. Smoke w lokalnym `UNITY_HANDOFF.md`; po akceptacji obowiązuje zwykły workflow `git`.


## Dedykowane ikony — 2026-11-01

Dziesięć nowych ikon wygenerowano oddzielnymi wywołaniami wbudowanego image_gen. Zachowano oryginalne referencje stylu: cut-b-v2 dla Dowódcy i rage dla Awanturnika. Granatowe tło Dowódcy (#182534/#26394c), bordowe Awanturnika (#30151d/#511f2c). Cały nieprzycinany obraz próbkowany w Aseprite do32×32 przez floor(x×width/32), floor(y×height/32), bez BOX, kwantyzacji i ditheringu. Zachowano źródła, prompty i edytowalne Aseprite. Oceniono komplet w32×32 i powiększeniu obok istniejących pasywek.

| Węzeł | Dedykowana ikona |
|---|---|
| Egida | WarriorCommander/t5_aegis-32.png |
| Egzekucja | WarriorCommander/t5_execution-32.png |
| Pojedynek | WarriorCommander/t5_duel-32.png |
| Szermierka | WarriorCommander/t5_fencing-32.png |
| Oko Dowódcy | WarriorCommander/t5_commanders_eye-32.png |
| Trening Skupienia | WarriorBrawler/t5_focused_rage-32.png |
| Szał Bitewny | WarriorBrawler/t5_battle_frenzy-32.png |
| Furia Bitewna | WarriorBrawler/t5_battle_fury-32.png |
| Powrót do Żywych | WarriorBrawler/t5_return_to_life-32.png |
| Nieśmiertelna Furia | WarriorBrawler/t5_undying_fury-32.png |

Paczka: `Tools/Art/WarriorT5Icons20261101/` (prompts.json, sources/, references/, native/, records.json, review.png). Authoring używa tych ikon także przy świadomym rebuildzieVS08; nie kopiuje już ikonT1–T4. Unity batch sprawdziło10 unikalnychSprite, importSingle/32PPU/Point/Clamp/bez mipmap/bez kompresji i idempotencję.40 wcześniejszych ikon i wszystkie pola gameplayowe obu drzew zachowane. Produkcyjne PNG/meta i dwa drzewa skopiowano bajtowo z wyników Unity Editor API; bez ręcznego YAML/GUID i bez sterowania pulpitem. Dowody: `../LocalHandoffs/cp2-05-icons-20261101/`. Pełny runtime suite678PASS pochodzi z poprzedniego etapu karty; zmiana ikon nie modyfikuje mechanik.

Końcowy focused retest po zmianie ikon: **WarriorContentTests62/62PASS,0FAIL,0SKIP**. Import/kompilacja, self-review i scoped whitespace PASS. Ręczny odbiór ikon w rzeczywistym UI pozostaje smoke użytkownika; porównanie arkusza ikon nie jest renderem okna gry. Bez commit/push.


## Opisy zgodne ze standardem tooltipów — 2026-11-01

Wszystkie10T5 dopisano do SkillTreeTooltipCopy jako osobne pełne zdania dla każdej rangi. Obecna/następna ranga korzystają z istniejącego SkillTreeTooltipPresentation i annotacji wartości. Bez surowych list15%/30%, sufiksówx i szczegółów technicznych w głównym opisie. Pojedynek używa10/20pp zgodnie z addytywną szansą; Furia Bitewna podaje przyrost mnożnika0,003/0,005 w konwencji starszych talentów krytyka. Opis Szału wiąże premię i prógHP każdej rangi.

Siedem rozwijanych Zasad działania obejmuje: próg Egzekucji/tarczę/wyjątek bossa; liczenie i reset Szermierki; zasięg trzech rozkazów; furię sprzed kosztu; przykład mnożnika krytyka; leczenie zanegowanych obrażeń przy wyłączeniu; generowanie furii przy ochronieHP1. Nie zmieniono gameplayu ani serialized assetów.

Ukryty isolated batch44/44PASS,0FAIL,0SKIP: pełny katalog143 grywalnych pasywek po wszystkich rangach oraz SkillTreePresentationTests, w tym zakup/inspekcjaT5. Trzy nowe przypadki wykrywają brak redakcyjnego wpisu dla dowolnegoT5 i pomieszanie warunków rang Szału. Source/test-copy byte match, scoped whitespace i self-reviewPASS. Wygenerowane teksty: ../LocalHandoffs/cp2-05-copy-20261101/editorial-copy.md i passive-tooltip-catalog-rendered.md. Bez nowego renderu okna gry, commit/push lub sterowania pulpitem.
