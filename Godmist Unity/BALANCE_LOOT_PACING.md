# Tempo dropu i wkład skrzynek — analiza 2026-09-22

Model analityczny, nie test runtime ani zatwierdzona konfiguracja. Referencja to jedno piętro: 6 walk po 4 zwykłych przeciwników, 2–3 skrzynki. Nie utożsamiać piętra z całą wyprawą. Bez bossa, misji, sprzedaży łupów i wydatków. Szanse dropu zwykłych mobów zatwierdzone przez użytkownika 2026-09-22 jako baza do dalszych obliczeń.

## Złoto

Potwierdzone zapisane GameDefines: gold_base=10, growth_base=4, level_span=10. Średnia z RollEnemyGold to ceil(10 * 4^((L-1)/10)). Model skrzynki według decyzji użytkownika: złoto za 2–3 encountery. Całe piętro daje więc złoto za 10–15 encounterów, chociaż stacza się 6. Skrzynki podnoszą dochód z walk o 67–150%.

| Poziom | Średnio na moba | 6 walk | Walki i skrzynki |
|---|---:|---:|---:|
| 5 | 18 | 432 | 720–1080 |
| 15 | 70 | 1680 | 2800–4200 |

Zakres ostatniej kolumny dotyczy średniej przy różnych liczbach/wartościach skrzynek, nie skrajnych rzutów RNG. Roboczy wcześniejszy cel netto za wyprawę 650–700 T1 / 2800–3100 T2 nie jest równoważny temu przychodowi brutto za piętro. Bez ustalenia liczby pięter, nagród misji i kosztów nie należy automatycznie zmieniać gold_base. Jeśli wyprawa ma więcej takich pięter, przychód rośnie proporcjonalnie.

## Torby materiałów

Przy niezależnej szansie p na torbę z każdego zwykłego moba oraz skrzynce zawierającej składniki za 2–3 lokalne torby, średnia liczba równoważników toreb wynosi 24*p + 4 do 9.

| p | Mobki | Łącznie ze skrzynkami |
|---|---:|---:|
| 40% | 9.6 | 13.6–18.6 |
| 50% | 12 | 16–21 |
| 60% | 14.4 | 18.4–23.4 |

Zatwierdzona baza do obliczeń: 50% na torbę zasobów, niezależnie 2% broni, 2% pancerza i 1% galdurytu (ostatnie tylko od L11). Nie są to wyniki pełnej wyceny zawartości torby ani gwarancja tempa tworzenia setów. Same 24 moby dają średnio 0.48 torby broni, 0.48 pancerza, 0.24 galdurytu.

Proporcjonalny model skrzynek do rozważenia: na każdy równoważnik torby zasobów dodatkowe szanse sprzętu = p_sprzętu / p_zasobów, czyli 4%/4%/2%. Daje to łącznie na referencyjne piętro około 0.64–0.84 torby broni, tyle samo pancerza i 0.32–0.42 galdurytu od L11. Oddzielne losowania mogą dać więcej niż jedną kategorię. Model nie jest wdrożony ani zatwierdzony.

## Status

Sprawdzono również XP: zapisany GameDefines.asset ma już CurrentFloatValue=1200 dla współczynnika i stałej; wcześniejsza rozbieżność konfiguracji zamknięta. Nie edytowano Unity assets ani szans dropu. Szanse zwykłych mobów zatwierdzone 2026-09-22. Następny krok: wycena zawartości toreb względem aktualnych receptur, przed uznaniem ekonomii za zbalansowaną. Zatwierdzenie nie obejmuje automatycznie dodatkowego modelu szans sprzętu ze skrzynek 4%/4%/2%.
