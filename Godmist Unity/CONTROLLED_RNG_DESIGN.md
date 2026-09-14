# Kontrolowany RNG — zatwierdzony kierunek

## Status

2026-09-14: karta projektowa i wdrożenie puli k=2 dla opcjonalnej zawartości lochu zamknięte po smoke użytkownika. Migracje pozostałych mechanik zaplanowane po pierwszym content passie. Nie oznacza to zatwierdzenia wszystkich zasad RNG walki i ekonomii.

## Kontrakt

Priorytetem jest zgodność ogólnej liczby wystąpień z sumą nominalnych szans; ograniczanie serii jest drugorzędne. Projektant podaje szansę, a model dobiera parametry. UI pokazuje szansę wynikającą ze statystyk i modyfikatorów, nie historii.

Bazowy wariant: tasowana pula progów, k=2, N≈ceil(2/min(p,1−p)). Jeden jednostajny próg z każdego z N równych przedziałów [0,1), losowanie bez zwracania. Sukces gdy próg < bieżąca szansa. Nowy rozmiar wyznaczany dopiero po wyczerpaniu puli; zmiana szansy nie kasuje historii. 0/100% oraz brak kwalifikującej się próby nie zużywają puli. 33% nie jest zaokrąglane do 1/3.

Implementacja w ContinuousChanceStream zachowuje dotychczasową nazwę/API. Używa rzadkiej reprezentacji Fisher–Yates i losuje jitter przy pobraniu; rozkład równoważny wcześniejszemu przypisaniu niezależnego jittera. N ma tolerancję względną 1e-6 przy całkowitych granicach dla precyzji floatów Unity; próg sukcesu nie jest zaokrąglany. Limit indeksów int.MaxValue przy ekstremalnie małych szansach ogranicza siłę wyrównania, nie jednostajny marginalny rozkład. Nie alokuje się całej puli z góry.

## Loch

Istniejący DungeonContentDistribution izoluje historie według jawnych kluczy autora. Jedna historia przez wszystkie piętra, reset przy nowym lochu. Grupa jest jedną okazją i zachowuje kompletność; stałe wyposażenie nie podlega losowaniu. Istniejąca walidacja odrzuca zależności i konkurencję przestrzenną między okazjami tego samego klucza oraz cykle wpływu kluczy.

Zapisane układy i znacznik HasPlacedContent blokują ponowne losowanie, także pustej zawartości. Rekonstrukcja legacy pozostaje w swojej istniejącej ścieżce. Nowa generacja tego samego seedu może różnić się między wersjami algorytmu. Deterministyczność wymagana w obrębie tej samej wersji i konfiguracji.

## Ograniczenia i dowody

Stałe i z góry ustalone ciągi szans mają poprawną marginalną średnią. Nie jest to gwarancja dla szans lub wartości akcji dobieranych na podstawie wcześniejszych wyników. Nie obiecujemy twardego limitu porażek ani dokładnej proporcji w dowolnym krótkim fragmencie.

Symulacja automatycznego N: 1,82 mln przebiegów, 400 mln rzutów, k=1/2/4. Przy 25% i 20 próbach k=2 dało 25,007% sukcesów oraz P95 najdłuższej serii porażek 9 (niezależny RNG: 13). Przy buffie 60% po porażce k=2 miało błąd ogólnej średniej +0,531 punktu procentowego w przebiegach po 2000 prób; po krycie −0,135 pp. Wyniki poszczególnych stanów statystyki mogą różnić się bardziej niż średnia całości. Strategie przenoszące mocniejsze akcje po porażkach nadal wymagają kontroli.

Weryfikacja wdrożenia: 20/20 NUnitLite PASS na rzeczywistych źródłach w .NET9, build Core netstandard2.1 PASS, self-review PASS, smoke użytkownika PASS. Unity EditMode/PlayMode dla tej zmiany NOT RUN. Lokalny harness i raporty znajdują się w .codex; nie są wymagane do działania gry. Testy regresyjne samplera są w Assets/_Project/Tests/EditModeTests/ContinuousChanceStreamTests.cs.

## Po pierwszym content passie

Osobne karty migracji, kolejność robocza:
1. Trafienia i kryty: izolacja jednostek i mechanik, buffy zależne od wyników, gwarantowane rzuty, multi-hit/AOE.
2. Efekty umiejętności i pasywek: kwalifikacja, odporności, niezależność źródeł i zależne wyzwalanie.
3. Lootbagi i nagrody: źródła/kategorie, czas życia historii i odtwarzanie checkpointów, bez rerollu zapisanych nagród.
4. Ulepszanie: audyt zmiennej stawki/szansy i przygotowywania sukcesów, osobny projekt wyników wielowariantowych.

Przed migracjami ustalić właścicieli i trwałość historii, zasady resetu, wersjonowanie/checkpointy oraz akceptowalną tolerancję błędu. Bez jednej globalnej historii i bez automatycznej migracji wszystkich losowań. Nie dodawać zapisu w środku walki. Bieżący kod nie przenosi żadnej z tych mechanik.