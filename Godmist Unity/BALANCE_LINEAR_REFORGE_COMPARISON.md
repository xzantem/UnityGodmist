# Porównanie liniowego reforgowania

Model projektowy, nie test Unity ani zatwierdzenie współczynników cennika upgrade. Zapis 2026-09-21.

## Metoda

30 000 przebiegów na wariant; deterministyczny generator LCG (seed 6215, a=1664525, c=1013904223, modulo 2^32), warianty w kolejności tabel. Każdy badany przedmiot ma umowną bazową wartość B=100. Nie jest to rzeczywista cena wybranych części T2.

Mnożniki rzadkości: 0.7/0.8/0.9/1/1.1/1.25/1.45/1.75/2.15/2.7. Wartość dla reforge: B*M*(1+4*p). Stawka: 0.25+0.15*max(0,r-3), gdzie Common ma indeks 3. Cena zaokrąglana w górę. Szanse awansu i spadku jak w rejestrze decyzji.

Upgrade: F=B*M*(0.6*p+6.4*p^3)*h(0.7)/h(0.6), h(c)=(57-37*c)/(76-75*c). Koszt próby: max(1,ceil(F(cel))-ceil(F(obecny))). Szansa 70%, maksymalnie +20 pkt proc., porażka losowo odejmuje 10..20 całkowitych punktów, minimum 0. Przy niewystarczającym budżecie wybierany największy osiągalny cel co 1 pkt proc. Jest to zachowanie modelowanej strategii, nie optymalizacja wszystkich możliwych polityk.

Strategie: upgrade wyłącznie do 100%; reforge wyłącznie do Godly; mieszana Rare/20%, Ancient/40%, Legendary/60%, Mythic/80%, Godly/100% (najpierw rarity, następnie progress w każdej parze). Po porażce powrót do celu bieżącego etapu. Koniec po osiągnięciu celu albo braku środków na kolejny wybrany ruch. Reszta budżetu pozostaje niewydana.

Statystyki jak w poprzednim porównaniu: poziom 15, normalny sprzęt T2 z pierwszych wariantów części content_pass_1.xlsx. Bez drzewek, biżuterii i galdurytów. Cel ataku: obrona 100, unik 20. Przeżywalność: incoming damage 100, celność 80, bez krytów, leczenia i efektów. Celność i unik skalowane 1+0.3*(M-1). Średni atak zamiast całkowania zakresu obrażeń; pominięto zaokrąglenia combatowe.

Zwiadowca: bazowy atak 45.05, broń atak 79, szansa kryta .072, bonus kryta 1.554, celność 78. Kryt mnoży obrażenia przed obroną. Wojownik: bazowe HP 612.5, pancerz HP 873.6; baza obrony 20.55, pancerz obrona 106.4; baza uniku 10.95, pancerz unik 28. Wynik obrażeń i przeżywalności dotyczy oddzielnego wydania budżetu na odpowiednio broń i pancerz, nie jednego zakupu obu.

## Wyniki budżetowe

| Budżet | Strategia | Śr. wydatek | Śr. postęp | DMG zwiadowcy | Przeżywalność wojownika | Śr. sloty |
|---:|---|---:|---:|---:|---:|---:|
| 300 | Upgrade | 297 | 51% | +47% | +24% | 2.14 |
| 300 | Rzadkość | 253 | 0% | +37% | +30% | 0 |
| 300 | Mieszana | 270 | 22% | +50% | +34% | 0.82 |
| 700 | Upgrade | 695 | 71% | +67% | +33% | 3.06 |
| 700 | Rzadkość | 626 | 0% | +84% | +57% | 0 |
| 700 | Mieszana | 619 | 37% | +87% | +56% | 1.70 |
| 1400 | Upgrade | 1170 | 89% | +84% | +42% | 4.21 |
| 1400 | Rzadkość | 1181 | 0% | +146% | +88% | 0 |
| 1400 | Mieszana | 1259 | 50% | +134% | +81% | 2.38 |

## Koszt celu od Common, postęp 0%

| Cel | Średnia | Percentyl 90 |
|---|---:|---:|
| Rare | 118 | 210 |
| Ancient | 255 | 436 |
| Legendary | 485 | 813 |
| Mythic | 948 | 1637 |
| Godly | 1900 | 3428 |

Dla odniesienia wcześniejszy wynik tego samego testowego cennika upgrade Common 0->100 przy szansie 70%: średnio 1497, p90 2496. B=100; liczby nie są uniwersalnym kosztem każdej broni/pancerza. Przeliczenie przez 650–700 zł netto/wyprawę dotyczy wcześniejszego budżetu T1, nie automatycznie wypraw T2.

## Koszt galdurytów — zakres zweryfikowany i ograniczenia

Aktualnie odczytany content_pass_1.xlsx, Materiały E173 i E184: wapienny kamień 135, czerwony proszek 288. Wartość składników jednego kamienia 423. Kod GalduryteCraftingService sumuje wartości kamienia i proszku; GalduryteSocketingService domyślnie pobiera 50% wartości: 212 zł. Zatem materiałowa wartość ekonomiczna + osadzenie = 635, pięć takich kamieni = 3175, bez pozostałych opłat i kosztu selekcji efektów. Nie utożsamiać 635 z potwierdzoną ceną zakupu od NPC: marża, dostępność, własne materiały, losowość craftingu oraz identyfikacja dropów zmieniają przepływy złota.

Aktualny arkusz efektów czerwonych: ogólny damage 2–5%, fizyczny/magiczny 3–6%, HP 2–5%. Craft gwarantuje kategorię/kolor, nie dowolnie wybrany konkretny efekt S. Pełnego rozkładu pozyskiwania, rzadkości, efektów i jakości kamieni nie symulowano. Nie twierdzić, że tabelka strategii zawiera optymalnie dobrane galduryty.

Wniosek: dla własnych, już znalezionych kompatybilnych kamieni upgrade ma dodatkową użyteczność; dla kupowania materiałów i selekcjonowania efektów od zera kamienie są osobną drogą inwestycją. Nie wyceniać wszystkich pustych slotów tak, jakby zawierały darmowe efekty S.

## Rekomendacja

Zachować liniową stawkę reforge. Strategia mieszana stała się konkurencyjna dla statystyk przy budżecie 300–700, a najwyższa rzadkość jest drogim celem. Testowy cennik upgrade pozostawić jako kandydata; nie podnosić dalej cen na podstawie samych pustych slotów. Przed finalną kalibracją pełnego zakupu galdurytów potrzebny jest model ich dostępności, losowania i cen NPC; rozdzielić wariant posiadanych dropów od zakupów od zera.

Zatwierdzono zachowanie kamienia po utracie slotu: pozostaje osadzony, ale nieaktywny do odzyskania pojemności. To zapis decyzji, jeszcze nie implementacja mechaniki.
