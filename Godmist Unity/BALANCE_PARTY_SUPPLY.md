# Cała drużyna: normalna i doskonała jakość — 2026-09-22

Analiza, bez zmiany gameplayu. Dane i receptury odczytane ponownie z design/content_pass_1.xlsx przez .codex/party_supply.py. Mnożnik materiałów Excellent=2 potwierdzony w zapisanym GameDefines.asset. 100000 trajektorii na tier, seed 230926; 8 kolejnych pięter, zachowane zapasy i złoto. Każde piętro: 24 zwykłe moby, 50% toreb, 2–3 skrzynki po 2–3 równoważniki. Rozkłady skrzynek jednostajne jak w poprzednim modelu; złoto zwykłych mobów losowane według kodu, skrzynka daje średnią wartość 2–3 encounterów. Stały poziom ekonomii L5/L15, bez awansów, misji, bossa, sprzedaży, consommables, upgrade/reforge ani zastępowania craftingu znalezionym sprzętem.

Każdy cel to 4 bronie + 4 pancerze lokalnego wariantu. Normalna i doskonała jakość są ALTERNATYWNYMI celami od zera, nie kolejnymi zakupami. Półprodukty agregowane i przetwarzane całymi partiami; zachowane resztki umożliwiają wspólne wykorzystanie. Jest to test zdolności sfinansowania całego celu na końcu piętra, nie symulacja kolejności zakupów czterech postaci ani bieżących decyzji wydatkowych.

Doskonały T1 wymaga poziomu 8, T2 poziomu 18. Wynik dotyczy możliwości produkcji przy dochodach L5/L15, nie możliwości założenia go na tych poziomach. Wyższe poziomy dochodów mogą przyspieszyć finansowanie.

| Poziom | Cel | Piętra | Średnie złoto | Średni koszt braków | Sam drop: komplet | Złoto + dowolny stock NPC | Złoto + do 20 rudy/piętro |
|---|---|---:|---:|---:|---:|---:|---:|
| 5 | Normalna | 1 | 882 | 1385 | 0.0% | 8.8% | 0.0% |
| 5 | Doskonała | 1 | 882 | 3869 | 0.0% | 0.0% | 0.0% |
| 5 | Normalna | 2 | 1764 | 536 | 0.2% | 99.9% | 35.9% |
| 5 | Doskonała | 2 | 1764 | 2718 | 0.0% | 3.5% | 0.0% |
| 5 | Normalna | 3 | 2647 | 151 | 18.5% | 100.0% | 99.4% |
| 5 | Doskonała | 3 | 2647 | 1763 | 0.0% | 93.6% | 0.2% |
| 5 | Normalna | 4 | 3528 | 27 | 71.4% | 100.0% | 100.0% |
| 5 | Doskonała | 4 | 3528 | 976 | 0.0% | 100.0% | 30.7% |
| 5 | Normalna | 5 | 4411 | 2 | 96.1% | 100.0% | 100.0% |
| 5 | Doskonała | 5 | 4411 | 504 | 0.8% | 100.0% | 94.4% |
| 5 | Normalna | 8 | 7056 | 0 | 100.0% | 100.0% | 100.0% |
| 5 | Doskonała | 8 | 7056 | 25 | 79.2% | 100.0% | 100.0% |
| 15 | Normalna | 1 | 3431 | 2599 | 0.0% | 71.5% | 0.0% |
| 15 | Doskonała | 1 | 3431 | 12191 | 0.0% | 0.1% | 0.0% |
| 15 | Normalna | 2 | 6861 | 357 | 0.5% | 100.0% | 35.9% |
| 15 | Doskonała | 2 | 6861 | 4018 | 0.0% | 85.7% | 0.0% |
| 15 | Normalna | 3 | 10290 | 131 | 20.4% | 100.0% | 99.4% |
| 15 | Doskonała | 3 | 10290 | 1067 | 0.0% | 100.0% | 0.2% |
| 15 | Normalna | 4 | 13721 | 27 | 71.8% | 100.0% | 100.0% |
| 15 | Doskonała | 4 | 13721 | 641 | 0.0% | 100.0% | 30.8% |
| 15 | Normalna | 5 | 17150 | 3 | 96.1% | 100.0% | 100.0% |
| 15 | Doskonała | 5 | 17150 | 435 | 0.9% | 100.0% | 94.3% |
| 15 | Normalna | 8 | 27437 | 0 | 100.0% | 100.0% | 100.0% |
| 15 | Doskonała | 8 | 27437 | 25 | 79.4% | 100.0% | 100.0% |

Stock 20 rudy/piętro jest wyłącznie wariantem wrażliwości: zakłada zgromadzoną możliwość zakupu 20*n sztuk i dostępność wszystkich innych braków. Nie jest wdrożonym systemem uzupełniania oferty. Piętro nie oznacza wyprawy; nie zakładamy możliwości odwiedzenia NPC pomiędzy piętrami tej samej wyprawy. Wynik stocku należy interpretować dla odpowiadającej liczby odnowień/zakupów, nie jako automatyczną dostawę w lochu.

Wnioski: nieograniczony dostęp do surowców znacząco skraca progresję i osłabia znaczenie dropu, zwłaszcza T2. Sam drop daje ok. 71–72% normalnych setów dla całej drużyny po 4 piętrach, ok. 79% doskonałych po 8. Bez limitu oferty niemal każda próba finansuje normalny cel po 2 piętrach; T2 w 72% już po jednym. Doskonałe T1: 94% po 3 i niemal 100% po 4; T2: 86% po 2 i niemal 100% po 3. Zatem poprzednie wysokie nadwyżki po jednym secie częściowo mają zastosowanie, ale nieograniczony zakup usuwa ograniczenie rudą.

Rekomendacja do decyzji: najpierw ustalić ograniczony zapas i odnowienie lokalnych materiałów u kowala, bez podnoszenia globalnego dropu i bez automatycznego obniżania złota. Nie zatwierdzono jeszcze liczby sztuk ani rytmu restocku. Koszty consumables i rozwoju mogą zmniejszyć dostępny budżet. Nie jest to finalny balans tempa wypraw.
