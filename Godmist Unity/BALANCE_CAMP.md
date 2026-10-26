> Aktualizacja 2026-09-22: poniższa analiza dotyczy poprzedniego modelu jednego obozu na piętro. Obecnie na starcie wyprawy przyznawane są zużywalne zestawy w liczbie pięter, do dowolnego wykorzystania (także kilka na jednym piętrze); reszta znika przy każdym wyjściu, bez odnowienia przy wznowieniu. Moc jednego odpoczynku pozostaje 40% HP i 40% many. Mana naturalnie odnawia się co 6 sekund, więc główną wartością obozu jest HP. To analiza syntetyczna, nie test walk runtime.

# Obóz: test bilansu 40% — 2026-09-22

Zatwierdzony model: drużyna może rozbić obóz w dowolnym miejscu poza walką, po zebraniu się; jedno użycie przypisane do każdego piętra, bez przenoszenia niewykorzystanych użyć na inne piętra. Odnowienie 40% maksymalnego HP i many żywych postaci, ograniczone maksimum. Brak wskrzeszania oraz przyrostu wściekłości/impetu. Zużycie powinno być zapisane i nie resetować się przez miasto/load. Brak implementacji w tej analizie.

## Metoda i ograniczenia

Test matematyczny i syntetyczna symulacja wrażliwości, NIE test runtime walki. Nie używa docelowych AI, statystyk przeciwników ani paladyna. 100000 prób dla każdego scenariusza strat, 4 postacie, 6 walk. Indywidualne straty gamma o średniej 10/15/20/25% maksymalnego HP na walkę i CV 35%, niezależne między postaciami i walkami. Cel: nikt nie spada do 0 HP. Nie modeluje downed/rescue, potions, celowania AI i skupionego ognia. Wariant pomocniczy +5 pp HP po każdej walce abstrahuje leczenie w trakcie walki, nie pozwala leczyć paladynem w eksploracji i nie jest kalibracją jego skilla. Sprawdzenie zera HP poprzedza ten abstrakcyjny zwrot, więc przy braku rozkładu ciosów w turze jest konserwatywne. Mana ma osobny bilans algebraiczny, nie powiązano kosztów leczenia z testem HP.

## Równe straty, obóz po trzeciej walce

| Strata HP na walkę | Bez obozu po 6 walkach | Z obozem |
|---|---|---|
| 10% | 40% | 70% (odzysk tylko 30%) |
| 15% | 10% | 50% |
| 20% | brak przeżycia wszystkich walk | 20% |
| 25% | brak przeżycia wszystkich walk | brak przeżycia wszystkich walk |

Maksymalny łączny budżet to 140% HP, pod warunkiem braku zmarnowania regeneracji i przeżycia do obozu. Oznacza teoretycznie mniej niż 23.33% średniej straty na walkę przez sześć walk; osiągnięcie zera nie jest przeżyciem. To nie docelowa trudność, bo realne straty są nierówne.

## Losowość bez leczenia

Przy syntetycznej średniej stracie 15% na walkę szansa, że nikt nie spadnie do zera, wynosi 38.6% bez obozu i 99.9% z obozem po trzeciej walce. Przy 20%: około 0% i 58.6%; przy 25%: około 0% i 1.2%. Te liczby służą wyłącznie pokazaniu wrażliwości modelu, nie prognozowaniu śmiertelności w Godmist.

Porównano obóz po walce 2/3/4 oraz reakcję na spadek dowolnej postaci do 60% HP. Ostatnia strategia może marnować część leczenia pozostałych członków drużyny: przy średniej stracie 20% odzyskuje średnio 36.2% HP na postać, szansa ukończenia 53.1%. Przyjęta swoboda rozbicia obozu daje realny wybór między bezpieczeństwem najsłabszej postaci a pełniejszym odzyskiem pozostałych. Pełne wyniki lokalnie w .codex/camp_balance_results.json.

## Mana

Obóz oddaje 0.4 * maxMP, niezależnie od wielkości puli i tempa regeneracji na turę. Dla 100/150/200 MP: 40/60/80 MP, czyli budżet odpowiadający 1.33/2/2.67 kosztu Uzdrowienia po 30 MP (liczba natychmiastowych użyć zależy od pozostałej many). Przy regeneracji 15–20 MP/t obóz z pulą 150 MP odpowiada 3–4 turom regeneracji. To umiarkowany jednorazowy bonus; nie dowodzi rozwiązania gospodarki many dla pełnego zestawu skilli.

## Wniosek

Zachować 40% do pierwszego playtestu. Obóz pomaga przejść serię umiarkowanych walk bez uzdrowiciela, ale nie zastępuje leczenia przy wysokich stratach ani nie chroni przed śmiercią w pojedynczym starciu. Nie gwarantuje niezależności od paladyna w docelowych encounterach. Nie rozwiązuje stallowania na ostatnim przeciwniku. Testy zapisu, jednorazowości i wymagań użycia muszą powstać przy implementacji; nie są tu oznaczone PASS.
