CP2-07 — dziesięć dedykowanych ikon T5 skauta, 2026-11-02.
Prompty, ID i redakcyjne opisy rang: prompts.json.
10 osobnych wywołań image_gen; wyniki: generation-provenance.json.
sources/ — oryginały, native/ — PNG 32x32 i edytowalne Aseprite.
review.png — podgląd natywny oraz 4x nearest.
records.json — hashe i docelowe ścieżki Unity.

Oryginalny zatwierdzony pipeline: Aseprite flatten, pełne płótno próbkowane
floor(x*width/32), floor(y*height/32). Bez kadrowania, BOX, kwantyzacji,
ditheringu i podmieniania źródła małym proceduralnym rysunkiem.
build.py sprawdza pełną zgodność RGBA z najbliższym próbkowaniem oraz
ponownym eksportem Aseprite (10/10 PASS). --review-only nie powtarza
roundtripu ani nie nadpisuje records.json.
Unity import przez ContentPass2ScoutT5Setup: Sprite Single, 32 PPU, Point,
Clamp, bez mipmap i kompresji. Nie edytować meta/GUID ręcznie.

Korekta palet 2026-11-02: finalne źródła/PNG/Aseprite i records.json zaktualizowane. Oryginalne źródła, edycje image_gen i wzorce: ../IconPaletteFix20261102. Konwencja: ../ICON_COLOR_CONVENTIONS.md. Początkowy prompt pozostaje w initial_prompt; finalne wejścia edycji w referenced_image_paths.
