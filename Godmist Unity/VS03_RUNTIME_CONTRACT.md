# VS-03 — kontrakt runtime pod content

## Zakres

Wspólne rozszerzenia R01–R03 z VS-02. Bez authoringu 160 węzłów, zmian serialized assetów i migracji zapisów. Dotychczasowe wartości enumów zachowane; OnHeal i ModifyActionValue dopisane na końcu.

## Koszty i parametry

PassiveEffectCombatExecutionSystem.ResolveResourceCost jest wspólnym odczytem dla walidacji i wydania surowca, AI, paska skilli i tooltipu workspace z kontekstem postaci. Bez postaci tooltip pokazuje bazę. Toggle upkeep pozostaje osobnym kosztem utrzymania, nie kosztem użycia.

ModifyResourceCost oraz ModifySkillParameter przyjmują stałą lub wartości rang. Operacja kierowana na Self/Source. Trigger AlwaysActive albo OnAttack; warunki dotyczą właściciela, skilla, typu ataku, surowca lub stacków. Warunki zależne od celu/wyniku trafienia są odrzucane dla tych czystych zapytań.

Parametry ModifySkillParameter (ReferenceId):
- resource_cost — koszt surowca; wynik nieujemny, zaokrąglany w górę.
- accuracy — celność skilla.
- hit_count — liczba trafień, minimum 1.
- duration_turns — czas efektu, minimum 0.
- status_chance — szansa statusu, zakres 0–1.
- range / area_range — maksymalny zasięg bezwzględny / względny.
- damage_multiplier — mnożnik obrażeń względem bazowej wartości 1.

Flat dodaje wartość; Percent/Additive sumuje przyrosty; Percent/Multiplicative mnoży (1 + przyrost). Wynik: max(0, (baza + flat) × max(0, 1 + suma procentów) × iloczyn mnożników). Maximum nie jest obsługiwane. Hit count, czas i zasięg używają RoundToInt.

Opcjonalne Parameters: scope (StringValue = offensive), effect_index (IntegerValue, indeks od 0), target_mode_index (IntegerValue, indeks od 0). Selekcję skilla zapewnia warunek Skill. Selektory indeksów stosować tylko do odpowiednich parametrów efektu/zasięgu; bez selektora modyfikator dotyczy wszystkich efektów/trybów. Klucze parametrów zapisywać dokładnie jak powyżej.

Efekty i tryby targetingu są kopiowane na potrzeby odczytu, bez mutacji współdzielonych assetów. Resolver jest używany w wykonaniu, targetingu, AI i podglądzie.

## Reakcje

W istniejącym wykonawcy dodano Heal, RestoreResource, DealDamage, GrantShield, GrantActionPoints oraz ModifyActionValue. Działają na żywych celach; nie wskrzeszają. Damage korzysta ze wspólnego CombatDamageSystem, leczenie/surowiec z CombatRecoverySystem.

Źródła wartości: Constant (także rangi), EventValue, StackCount, Resource, Distance, Stat, MissingHealthPercent, MissingResourcePercent. Dynamiczna wartość = źródło × Scale + Offset. Procent brakującego HP/surowca to ułamek 0–1 właściciela. Stat wymaga nazwy CharacterStatType. TargetCount i RuntimeState są odrzucane dla nowych reakcji.

EventValue odwołuje się do wartości zdarzenia; ReferenceId = health_damage wybiera rzeczywiście utracone HP. DealDamage.ReferenceId wskazuje DamageType; pusty oznacza Physical. GrantActionPoints używa pełnych PA (1 = 100 jednostek runtime) i ogranicza wynik do 0–MaxActionPoints. ModifyActionValue odejmuje wartość × bazowe AV celu, ogranicza do zera i aktualizuje kolejność. GrantShield dodaje warstwę przypisaną do runtime pasywki.

Parameters: frequency (StringValue = once_per_action) ogranicza operację do jednej próby na ActionId, niezależnie od liczby trafień/celów. Wymaga niepustego ActionId od publikującego. chance (wartość liczbowa 0–1) losowana raz na próbę. Bez frequency reakcja działa na każde opublikowane zdarzenie. Zdarzenia utworzone przez nową reakcję są oznaczone IsPassiveReaction i nie uruchamiają kolejnych reakcji pasywnych.

OnHeal publikowane jest przez aktywne efekty Heal/HealPercent po faktycznym odzyskaniu HP; EventValue = odzyskane HP, nie żądana wartość ani overheal. Wywołania recovery bez kontekstu (np. istniejące inne źródła leczenia) nie publikują tego zdarzenia. Ich ewentualne włączenie wymaga świadomego podłączenia kontekstu w karcie konsumenta.

## Granice

Nieobsługiwane pasywne ModifyStatus, RedirectDamage i ReflectDamage są zgłaszane przez walidator; aktywne odpowiedniki pozostają bez zmian. Walidacja nie zastępuje testów konkretnych węzłów. Propagacja podpaleń, stan na cel, wyjątkowe kryty, Pole Osądu i authoring klas pozostają w VS-07–09. Stary tooltip bez kontekstu postaci nadal pokazuje bazowe wartości.

## Weryfikacja

Wynik końcowy: **155/155 PASS**, `vs03-acceptance.xml` (2026-09-22). Self-review zakończony; brak znanych krytycznych problemów w zakresie karty. Kontrola whitespace kodu i testów bez błędów. Play Mode smoke jeszcze niewykonany.

Testy EditMode w izolowanej kopii projektu: pasywy, AI, wykonanie skilli, preview, recovery, HUD/presentation i progresja drzewek. Nowe przypadki obejmują koszty trzech surowców, niezależność postaci współdzielących skill, zerowy koszt, raz na akcję, wartości zdarzeń, rzeczywiste leczenie, AV, ochronę przed rekurencją, kopie efektów/zasięgów i selekcję indeksu efektu.

Smoke: uruchomić istniejącą walkę Scouta, porównać koszt w tooltipie i na pasku z wydanym surowcem, sprawdzić wybór celu/zasięg i wykonanie wielokrotnego trafienia. Brak wymaganego nowego wiring Inspector. Produkcyjne definicje nowych węzłów będą integrowane i testowane przy authoringu klas.

## Poprawka po smoke AI

Ocena opłacalności skilla używa pełnych PA (ActionPointUnits.ToPoints), podczas gdy wydanie i walidacja nadal używają setnych. Wcześniejsza kara była 100 razy za duża i odrzucała ataki. Testy płatnego ataku (3 i 3,5 PA) odtworzyły błąd przed poprawką. Końcowa regresja AI/pasywów/wykonania: 70/70 PASS, vs03-ai-verified.xml.

## Poprawka wydajności na piętrze

AI pathfinding nie sortuje całej kolejki z ponownym przeliczeniem pełnych ścieżek. SortedSet przechowuje ryzyko/koszt i stabilny numer wpisu; rozwijanie pomija wpisy zastąpione lepszą drogą. Koszt kolejnego kroku uwzględnia narastanie PA oraz terrain surcharge. Zasady przejść, pułapek, drzwi i warunek dotarcia zachowane.

Test odłączonego górnego pokoju 48×48: 7710 ms przed i 188 ms po poprawce. Regresja: 63/63 PASS (`vs03-upper-verified.xml`), w tym rzeczywiste schody i brak drogi między poziomami. Nie odtworzono ręcznie konkretnego pokoju użytkownika; wymagany powtórny smoke.

## Izolacja commita

Samodzielny snapshot VS-03 zweryfikowany: 161/161 PASS (`vs03-commit.xml`). Commit nie zawiera wcześniejszych pakietów VS-18. ActionPointUnits stanowi wspólny adapter; w samym commicie pozostaje dotychczasowa skala 1, a przejście na 100 jednostek/PA należy do pozostawionego w workspace pakietu VS-18. Opis setnych i pomiary smoke powyżej dotyczą zintegrowanego workspace. Nowe reakcje i punktacja AI korzystają z adaptera w obu skalach.
