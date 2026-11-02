CP2-06: 25 dedykowanych ikon Zbrojnego (T1–T5), 2026-11-02.
prompts.json: opisy rang, graf, parametry skilli, prompty i referencja stylu.
generation-provenance.json: wyniki osobnych 25 wywołań image_gen.
sources/: oryginalne nieprzezroczyste obrazy generatora.
native/: PNG 32x32; aseprite/: edytowalne źródła.
review.png: podgląd ikon w rozmiarze natywnym i powiększeniu nearest.
records.json: hashe oraz docelowe ścieżki Unity.

Pipeline: Aseprite flatten, pełne płótno próbkowane floor(x*width/32),
floor(y*height/32), bez kadrowania, BOX, kwantyzacji i ditheringu.
Pełny build.py wykonuje kontrolę dokładnego RGBA nearest oraz roundtrip
Aseprite (25/25 PASS). build.py --review-only odświeża sam podgląd,
nie nadpisuje records.json i nie deklaruje ponownego roundtripu.
Unity import: Sprite Single, 32 PPU, Point, Clamp, bez mipmap i kompresji;
przez ContentPass2ManAtArmsSetup oraz AssetDatabase, z zachowaniem GUID.
Nie uruchamiaj pełnego przebudowywania dawnych drzew dla tej karty.
