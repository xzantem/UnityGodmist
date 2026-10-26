# Zakup braków a dochód — 2026-09-22

Źródła: aktualny design/content_pass_1.xlsx (Materiały A2:E123, Wyposażenie A2:S97, Lootbagi D4:E14); TradeService, BlacksmithCraftingService, MaterialCraftingService, NpcEconomyService; zapisany mnożnik sprzedaży właściwego typu 0.5 w GameDefines.asset. Rozszerzenie symulacji BALANCE_MATERIAL_SUPPLY.md: 100000 pięter na tier, seed 220926. Read-only względem arkuszy i gameplayu.

Założenia: 6 walk po 4 moby, 2–3 skrzynki po 2–3 równoważniki toreb/encounterów, 50% torby zasobów. Cena kupna surowca = cena bazowa bez rabatu lojalności, sprzedaż = floor(cena * 0.5) u właściwego NPC. Kupujemy brakujące surowce, następnie przetwarzamy w pełnych partiach; nie kupujemy droższych gotowych elementów. Kod nie nalicza osobnej opłaty złota za sam crafting w tych dwóch usługach. Zakłada się dostępny stock i receptury — nie jest to gwarantowane aktualną ofertą. Normalny set = broń+pancerz wybranej klasy z lokalnych materiałów. Każda klasa analizowana osobno, brak zapasu przed piętrem.

Średnie złoto z walk i skrzynek: L5 = 882; L15 = 3430. Nie uwzględniamy misji, bossa, consumables, upgrade/reforge, bezpośrednich dropów i sprzedaży gotowego sprzętu. Wartość sprzedaży dotyczy wyłącznie nadwyżek pięciu lokalnych surowców kowalskich (ruda/skóra/drewno/włókno/węgiel) po odłożeniu składników jednego setu. Wyłączono pył, alchemię, klejnoty i resztki półproduktów.

| Tier | Klasa | Wszystkie surowce od zera | Średni zakup braków | 90. percentyl zakupu | Złoto po zakupie bez sprzedaży | Wartość sprzedaży nadwyżek | Złoto po zakupie i pełnej sprzedaży |
|---|---|---:|---:|---:|---:|---:|---:|
| 5 | Wojownik | 756 | 112.8 | 210.0 | 769.2 | 704.1 | 1473.3 |
| 5 | Zwiadowca | 810 | 47.6 | 200.0 | 834.4 | 637.9 | 1472.3 |
| 5 | Czarodziej | 816 | 45.3 | 143.0 | 836.7 | 640.5 | 1477.2 |
| 5 | Paladyn | 800 | 75.3 | 168.0 | 806.7 | 662.6 | 1469.4 |
| 15 | Wojownik | 3356 | 115.3 | 210.0 | 3314.7 | 5279.6 | 8594.4 |
| 15 | Zwiadowca | 3834 | 34.0 | 14.0 | 3396.0 | 4986.5 | 8382.5 |
| 15 | Czarodziej | 3182 | 128.0 | 435.0 | 3302.0 | 5353.8 | 8655.7 |
| 15 | Paladyn | 3784 | 74.3 | 154.0 | 3355.7 | 5042.5 | 8398.2 |

Kolumna „po pełnej sprzedaży” jest scenariuszem płynnego rynku, nie faktyczną wypłatą. Kod ogranicza wypłatę do GoldBalance NPC; występuje również świadomie akceptowana płatność częściowa. Przy początkowym pustym budżecie NPC nie da się zrealizować całej wartości. Przy przykładowej bazie wypłaty gildii 250 budżet skaluje się do około 435 na L5 i 1741 na L15, ale realny budżet zależy od historii handlu, konfiguracji NPC i kampanii. Nie zakładano tych kwot jako rzeczywistego stanu sejfu.

Największa sprzedaż w T2 wynika z cennika i nadwyżki skór: skóra jelenia kosztuje 680, sprzedaż 340; wyprawianie daje 3 sztuki. Typowe zapotrzebowanie jednego setu to 2–4 surowe skóry wobec średniego dopływu 10.95. Nadwyżki drewna i węgla również mają wartość. Nie są naprawdę zbędne, jeżeli gracz zbiera je na kolejne postacie lub jakość Excellent.

Wnioski: cel sfinansowania jednego setu jest osiągany z dużym zapasem w tym modelu już bez sprzedaży. Problemem jest stock brakującej rudy, a nie siła nabywcza. Poprzednie 650–700/2800–3100 netto nie powinny być porównywane z samym złotem walk: materiały mają własną znaczną wartość. Nie rekomenduje się automatycznego nerfu ogólnego złota ani dropu na podstawie scenariusza likwidacji nadwyżek. Następne użyteczne sprawdzenie to cała drużyna i normalna/doskonała jakość z zachowaniem zasobów między piętrami; dopiero potem ustalenie cen skór/drewna oraz budżetów NPC. Brak zmian balansu w tej analizie.
