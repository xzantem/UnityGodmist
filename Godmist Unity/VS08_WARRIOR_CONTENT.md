# VS-08 — Wladimir: Dowódca i Awanturnik

Status: DONE (2026-09-22). Implementacja, konfiguracja assetów, testy automatyczne i self-review zakończone. Ręczny test pozostaje do acceptance. Użytkownik zaakceptował commit/push i przejście do VS-09.
VS-07 zamknięta i wypchnięta osobno: `db70555a`; jej ręczny test również odłożony do acceptance na polecenie użytkownika.

## Content

- Nowa kampania daje Wladimirowi pięć starterów: Cięcie, Kopnięcie, Okrzyk Wojenny, Prawy Sierpowy, Bandaż Polowy.
- Dowódca i Awanturnik mają po 20 węzłów w pierwszych czterech tierach, z rangami, wymaganiami, opisami i odblokowaniem aktywnych skilli.
- Osiem aktywnych skilli z drzewek: Rozkaz Unicestwienia, Unieruchomienie, Rozkaz Ochrony, Rozkaz Przegrupowania, Krwawiące Zniszczenie, Krzyk, Młyniec i Pragnienie Śmierci.
- Reuse SD_BattleCry z zachowanym identyfikatorem. Dwanaście nowych definicji skilli oraz dwa drzewka; CD_Wladimir_Test, CCD_Warrior i katalog podłączone.
- Authoring przez Unity API: `Godmist/Content/Configure VS-08 Warrior` / VS08WarriorSetup.Configure. Wykonany w kopii testowej, wyłącznie zatwierdzone assety i ich meta przeniesione do projektu. Nie wymaga uruchamiania przez użytkownika.
- Źródła: aktualny content_pass_1.xlsx (Startowe skille A1:E7, Drzewka A1:G21 i A32:G52), VS01_CONTENT_CATALOG.md oraz decyzje użytkownika z 2026-09-22.

## Zatwierdzone reguły i implementacja

- Niedobór: skill we wściekłości o koszcie większym od maksimum może być użyty tylko przy pełnej wściekłości. Zużywa dostępną ilość, a każdy brakujący punkt daje -2% obrażeń na 3 tury; węzeł Wściekłość obniża karę do -1%. Kara odświeża się bez stackowania, mnożnik nie spada poniżej zera. Koszty, UI, AI i podgląd używają tej samej reguły.
- Wściekłe Uderzenia obniżają koszt ofensywnych skilli o 10/20/30%. Krzyk zużywa całą aktualną wściekłość; długość ogłuszenia wynika z rzeczywiście wydanej ilości (próg 50), a nie wartości po opróżnieniu puli.
- Pasyw Dowódcy bierze obrażenia trafienia oraz mnoży bonus przez punkty drzewka. Wartość arkusza 0,002% jest traktowana dosłownie; procent ulepszenia broni odpowiada poziomowi ulepszenia w obecnym modelu 0–100%. Małe porcje mają resztę ułamkową zamiast gwarantowanego zaokrąglania każdego trafienia do 1.
- Pasyw Awanturnika leczy od faktycznie wydanej wściekłości, również podtrzymania; reszty ułamkowe nie przepadają. Pragnienie Zemsty używa rzeczywiście utraconego HP, bez overkillu ani obrażeń tarczy.
- Pragnienie Śmierci pozostawia 1 HP. Wymaga zasobu na podtrzymanie przy aktywacji; potem istniejący system toggle pobiera większe z 20 PW i 25% maksimum na turę i wyłącza oba modyfikatory przy braku zasobu. Desperacja zwiększa oba bonusy. Nie ignoruje całego trafienia.
- Riposta: +50% oceny atrakcyjności ataku AI i +25% za przyległego sojusznika, cap +125%. Odbicie używa 30% wartości trafienia sprzed obrony; nie wywołuje kolejnej Riposty ani drugiej generacji zasobu od zadania obrażeń.
- Rozkaz Ochrony działa 3 tury, dynamicznie liczy sąsiadów i ogranicza redukcję do 60%. Flankowanie również reaguje na aktualne sąsiedztwo. Oznaczenie jest względnym bonusem szansy kryta dla atakujących sojuszników.
- Przegrupowanie zeruje AV nawet ponad normalnym maksimum. Strategia daje dwa darmowe kroki na turę; odświeżenie buffa nie odnawia wykorzystanych kroków. Podgląd ścieżki i faktyczny koszt są zgodne.
- Amok i Trauma odświeżają się bez kumulowania. Do Trzech Razy Sztuka śledzi kolejne użycia Cięcia na tym samym celu; inna umiejętność lub cel resetują serię.
- Odnowienie Sił sprawdzane przy skutecznym nałożeniu ogłuszenia, przed zastosowaniem CC/tolerancji; Wściekłe Powstanie odbija osobny magiczny atak. Standardowa wspólna odporność na hard CC pozostaje aktywna.
- Adrenalina i Zemsta są przeliczane przy zmianach zasobu i odświeżaniu PA, również po zewnętrznym drenażu. Blokada generowania surowca obejmuje nowe pasywy.

## Integracja i trwałość

Złożone reguły w WarriorCombatSystem i stanie BattleUnit; brak globalnego stanu i mutowania współdzielonych SkillData w walce. Pasywy statystyczne korzystają z istniejącego pipeline. Koszty, obrażenia, statusy, tury, movement, preview i AI mają wspólne punkty integracji.
Nowe umiejętności drzewek korzystają z GrantedSkill/GetKnownSkills z VS-07; nie są kopiowane do permanentnej listy w save. Stare zapisy zachowują wcześniej znane skille; nowy zestaw starterów sprawdzać na nowej kampanii. Liczniki i linki bojowe nie przechodzą do kolejnej walki.

## Weryfikacja

- Kompilacja i authoring Unity: PASS.
- Pierwsza regresja klas/progresji: 85/85 PASS; wykonanie skilli Wladimira: 25/25 PASS.
- Pełny EditMode: 1331/1331 PASS (`.codex/vs08-all.xml`).
- Dalszy retest: 112/112 PASS (`.codex/vs08-verified.xml`). Poprzedni szeroki filtr Toggle dobrał także test selekcji edytora pokojów, który raz nie przeszedł; retest jego pełnej klasy przeszedł.
- Końcowe poprawki odświeżania statystyk i zasobu: 95/95 PASS (`.codex/vs08-final-check.xml`), w tym 27 przypadków WarriorContentTests.
- Self-review: sprawdzone koszty przed/po wydaniu, clamp/overkill, brak stackowania i rekurencji odbić, wygasanie, blokady generacji, przydział i usuwanie skilli z drzewek, zgodność preview/movement, izolacja zmian VS-18 oraz referencje assetów.
- Manualny Play Mode/acceptance: NOT RUN, odłożony przez użytkownika. Instrukcja w UNITY_HANDOFF.md.

## Poprawki UI po zgłoszeniu

- Podsumowanie globalnych pasywów obejmuje efekty bojowe Skrytobójcy, Tancerza Ostrzy, Dowódcy i Awanturnika, nie tylko generyczne modyfikatory statystyk. Wartości rosną z liczbą wydanych punktów.
- Udana inwestycja emituje zdarzenie sesji dla postaci. Otwarte panele postaci i loadoutu odświeżają zawartość; nieudany zakup nie emituje zdarzenia, zamykane panele wypisują się z subskrypcji.
- Okno drzewka jest ograniczane do workspace po otwarciu i po przebudowie layoutu.
- Regresja UI/progresji: 22/22 PASS (`.codex/ui-progression4.xml`), w tym test prefabów uruchamiany w Play Mode: zakup, odświeżenie statystyk/listy skilli, brak zdarzenia po nieudanym zakupie, zamknięcie i pozycja okna. Testy nie zastępują ręcznego smoke.
- Korekta opisu pasywu: zapis „% Kryt”, dynamiczna wysokość nagłówka według zawiniętego tekstu i przesunięcie obszaru drzewka. Przypisanie Dowódcy/Awanturnika było prawidłowe.
