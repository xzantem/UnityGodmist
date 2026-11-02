# Audyt odwołań w tooltipach drzewek — 2026-11-02

Zakres: 10 produkcyjnych drzewek czterech klas, 230 węzłów.
Wojownik 75, Zwiadowca 75, Mag 40, Paladyn 40. Strażnik objęty
regresją wraz z pozostałymi dziewięcioma drzewkami.

## Znalezione luki i poprawka

Rejestr odwołań do węzłów był ograniczony do Strażnika. Pozostałe nazwy
mogły pozostać bez odwołania albo dopasować krótszy wpis słownikowy,
np. końcówkę Odwet w Gniewnym Odwecie. Oprócz pełnych nazw brakowało
odmian używanych w opisach: Młyńca, Rozkazem Unicestwienia, Rykoszetu,
Szybkich Ciosów, Ognistej Aury, Przejęcia Mocy, Pola Osądu itd.

Wspólny rejestr pobiera węzły i ich przyznane skille z aktualnej klasy
postaci. Korzysta z istniejących ID i ikon. Przewidywanie u Zbrojnego
oraz Przewidywanie u Skrytobójcy pozostają odrębnymi odwołaniami.
Zmiana postaci/klasy zastępuje kontekst nazw.

Opis odwołania jest rozstrzygany dopiero przy jego otwieraniu, przez
zwykły renderer węzła, z aktualną inwestycją SP i podglądem postaci.
Nie dubluje tabel wartości i nie pozostawia kopii opisu poprzedniej rangi.
Aktywne zachowują standardowy opis kosztu/celu/efektów, pasywne
Aktualne/Następne. Strażnik zachowuje link do pełnych zasad.

Odwołania do nazw umiejętności respektują wielką literę. Zwykłe
„unieruchomienie” nadal oznacza status; nazwa skilla Unieruchomienie
prowadzi do węzła Wojownika. Ogólne określenie „Leczenie krytyczne”
nie odsyła przypadkiem do podstawowej umiejętności Leczenie.
Pełne nazwy nadal mają pierwszeństwo przed krótszymi fragmentami;
znaczenie samego Odwetu bossa pozostaje zachowane.

## Weryfikacja i granice

Testy obejmują tytuł, dokładną referencję ikony i opis każdego węzła
przed zakupem i na maksymalnej randze — 460 wariantów. Osobno są testy
odmian nazw, zmiany klasy, zwykłych statusów i poprzednie regresje
Strażnika. Wyniki i hashe: Tools/ContentPass2/talent-reference-audit-validation.json.
Zestawienia w LocalHandoffs/skill-tree-reference-audit-20261102/.

Bez zmian efektów, statystyk, inwestycji SP, ikon PNG, serialized assets
lub schematu zapisu. Nie uruchamiano pulpitu ani otwartego Editora.
Odbiór w grze: lokalny UNITY_HANDOFF.md.
