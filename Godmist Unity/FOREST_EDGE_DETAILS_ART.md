# Forest edge details

Generated with imagegen, 2026-10-12. Original preserved at `C:/Users/Patryczku/.codex/generated_images/01a0a7db-0cd8-75d2-8f08-625231ded602/exec-dbd0f33a-3b0b-47ae-9967-120f30355837.png`.
Project asset: `Assets/_Project/Art/ForestPrototype/forest-deadwood-v1.png`. Transparent sprite imported with Point filtering, no mipmaps, max 128. Low shrubs reuse `Assets/_Project/Art/ArungardGreeneryPrototype/shrub.png`.

Prompt:
> Asset type: single game sprite, pixel-art dark fantasy woodland ground decoration, transparent background. One small low decomposed fallen wood fragment with two or three broken twigs, split rotten bark and sparse dark olive moss. Flat and partly sunk into the forest ground, can be stepped over; not a tall trunk or wall. Horizontal silhouette, shallow three-quarter top-down view consistent with classic top-down 2D RPG sprites. Crisp intentional pixel clusters with approximately 64x32 logical pixel detail. Muted dark grey brown wood and rotten olive moss, soft neutral overhead lighting, no glow, no black outline thicker than one logical pixel, no cast shadow outside object. Single isolated object centered with generous transparent padding, no ground tile or backdrop, no text, no borders, no multiple variants, no leaves scattered outside. True alpha transparency. Save to a local file and return its path.

Runtime decorations are deterministic, restricted to the clearing fringe and omit trail cells plus their neighbours and occupied cells plus their neighbours. They share existing ground decoration fog, lighting and depth sorting. No collision, navigation or persistence changes; no update loop.
