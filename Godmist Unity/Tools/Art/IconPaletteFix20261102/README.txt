Korekta palet CP2-05/06/07 — 2026-11-02.

Odtworzona konwencja: ../ICON_COLOR_CONVENTIONS.md.
Oryginalne prompty sprzed passu: convention-sources/ (kopie read-only).

Audyt Wojownika:
- Dowódca T5: 5/5 zgodne z paletą granatową; bez zmian.
- Awanturnik T5: 5/5 zgodne z paletą bordową; bez zmian.
- Zbrojny: 21 pasywek zachowuje paletę brązową nowego drzewa CP2-06.
  Cztery AKTYWNE skille poprawione na bordowe tło aktywnego Wojownika;
  zachowane złoto ochrony, błękit kontroli oraz zieleń odnowienia.

Skaut: cztery pasywki Skrytobójcy — śliwka #281c30/#3c2a47;
trzy pasywki Tancerza — teal #173033/#254b50; Przewrót — paleta ruchu
jak Wymiana; Jad/Klątwa — oliwkowa paleta trucizny jak Zatruty Sztylet.
Akcent rany pozostaje karmazynowy, ogłuszenie przygaszenie niebieskie.

prompts.json — 14 docelowych ikon i instrukcje korekty.
generation-provenance.json — początkowe 14 osobnych wywołań image_gen.
selected-generation-provenance.json — finalne źródło każdej z 14 ikon,
po czterech punktowych refinements. before/ zachowuje pierwotne źródła,
PNG32, Aseprite oraz oryginalne meta; iterations/v1/ — wejścia poprawek.
before-after.png — porównanie przed/po/wzorzec.
review.png — finalne ikony w32×32 i powiększeniu nearest4x.
validation.json — kontrola palet, hashy, zachowania referencji i importu Unity.

Edycja: wbudowany image_gen, bez CLI/API. Eksport: dotychczasowy Aseprite
full-canvas nearest32, bez crop, BOX, kwantyzacji i ditheringu. Przebudowano
paczki ManAtArmsIcons20261102 i ScoutT5Icons20261102; źródła oraz prompty
bieżące znajdują się w tych paczkach. Native sampling i roundtrip 35/35 PASS.
W produkcji zastąpiono tylko14 PNG, bez edycji .meta, GUID, assetów i kodu.
