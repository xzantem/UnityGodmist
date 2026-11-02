# CP2-10 — Czarnoksiężnik i Mag Ognia, T5

Zakres: po pięć nowych węzłów w istniejących drzewach, 18 rang,
dwa aktywne zaklęcia. Pozostaje dotychczasowe 40 węzłów Maga,
pięć początkowych umiejętności Alison i obecne progi inwestowania SP.
Nie zmieniamy limitu poziomu postaci ani nie dodajemy T6/T7.

## Przyjęte zasady

| Węzeł | Rangi | Efekt |
|---|---:|---|
| Nadludzkie Bodźce | 2 | Próg Adaptacji 15/10% maksymalnego HP; liczy rzeczywistą utratę HP. |
| Siła Wyładowania | 1 | Globalnie +0,5 do mnożnika obrażeń krytycznych. |
| Absorpcja Mocy | 3 | Po rzeczywistym uniku 50% szansy na zgromadzenie 10/35/60% hipotetycznych obrażeń po obronie i redukcjach, przed tarczą. |
| Teleportacja | 1 | Sojusznik lub rzucający w zasięgu 7; wolne pole do 6 od przemieszczanej postaci; 75 many, 65% PA. |
| Magiczna Gotowość | 2 | Udana Teleportacja przyspiesza następną turę przemieszczonej postaci o 25/50%. |
| Strzała Bólu | 2 | Ognista Strzała zmniejsza zadawane obrażenia o 25/40% zamiast 15%, przez dotychczasowe 4 tury. |
| Unieszkodliwienie | 1 | Trafienie Ognistą Strzałą wymusza dolną granicę ataku przez 4 tury; pierwszeństwo przed wymuszaniem maksimum, zachowane krytyki. |
| Podtrzymywanie Ognia | 2 | 30/60% szansy po trafieniu na dodatkową turę istniejącego podpalenia, z pełnym tickiem jego obecnej siły. |
| Słup Lawy | 3 | +15/30/50% do bezpośrednich obrażeń Słupa Ognia i jego podpalenia. |
| Słup Ognia | 1 | Obszar 3×3, zasięg środka 8, tylko wrogowie; 100% ataku magicznego, 85% bazowej szansy podpalenia na 4 tury po 90% średniego ataku; 80 many, 80% PA. |

Absorpcja sumuje bank do następnego własnego użycia ataku. Dodaje go raz,
do pierwszego skutecznego trafienia; cały bank przepada także przy pudle
wszystkich trafień. Nie uruchamia się z DoT, duplikatów ani reakcji.
Podgląd hipotetycznych obrażeń nie zużywa krytyka, zamrożenia, ekspozycji,
Adaptacji ani tarczy i nie wykonuje dodatkowego losowania krytyka.

Podtrzymywanie Ognia podejmuje jedną próbę na trafiony cel i użycie ataku,
także gdy podpalenie pochodzi od innej postaci. Nie zmienia właściciela
ani ponownie nie skaluje siły podpalenia. DoT i reakcje nie uruchamiają proca.
Unieszkodliwienie nie zmienia siły DoT obliczanej ze średniego ataku.

Teleportacja korzysta z istniejącego wyboru pola, zajętości, przeszkód i LOS.
Nielegalny cel lub miejsce nie pobierają kosztu. Przy teleportowaniu siebie
przyspieszenie jest odroczone do następnej tury, bez dodatkowych PA teraz.

## Integracja

Manifest: `Tools/ContentPass2/mage-t5-content.json` — źródłowe komórki,
uzgodnienia, wymagania, opisy rang i pełne zasady. Runtime rozszerza
istniejący `MageCombatSystem` oraz wspólne wykonanie i podgląd obrażeń.
Bank i próby proców są stanem walki, bez zmiany formatu zapisu gry.

Authoring: `Godmist/Content Pass 2/Apply Mage Tier Five`, przez Unity API.
Dodaje węzły i umiejętności, zachowuje istniejące węzły i kolejność katalogu.
Debuff Ognistej Strzały wymaga skutecznego trafienia, podobnie jak podpalenie.
Stary authoring Maga ponownie stosuje T5, jeżeli manifest jest dostępny.

Tooltipy używają dotychczasowych „Następne”/„Aktualne”, wspólnego obliczania
wartości i dodatkowego odnośnika „Zasady działania”. Nazwane odwołania
prowadzą do właściwych węzłów i ikon Maga.

## Grafika

Dziesięć ikon: źródło ImageGen, pełny kadr przeskalowany nearest do 32×32
przez Aseprite, PNG i natywny plik `.aseprite`. Bez kadrowania i ditheringu.
Zachowane wspólne bazy oryginalnych promptów Maga i surowa referencja B-v2.
Ognista Strzała jest zaklęciem: Strzała Bólu pokazuje magiczną lancę ognia,
bez fizycznej strzały do łuku. Pierwsze odrzucone generacje są w LocalHandoffs.

Prompty, referencje, pochodzenie, QA i plansza porównawcza:
`Tools/Art/MageT5Icons20261102/`. Palety: `Tools/Art/ICON_COLOR_CONVENTIONS.md`.

Dowody weryfikacji: `Tools/ContentPass2/mage-t5-validation.json`.
Manualny smoke wykonuje użytkownik przed commit/push.
