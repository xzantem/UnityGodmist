# Rotten stump variant

Source generated using built-in imagegen and preserved at `Assets/_Project/Art/ForestPrototype/forest-stump-rotten-v1.png`.
Aseprite production derivative `forest-stump-rotten-v1-32ppu.png`: 40×40 pixels, Point, native 32 PPU, scale 1; pivot (0.5,0.2), alpha bounds (6,9,34,32).

## Prompt
A single isolated rotten tree stump game sprite, dark fantasy woodland RPG pixel art, three-quarter top-down view matching classic 2D party RPG characters. Short broad stump with jagged split rim, black hollow rotten center, irregular exposed roots and a little desaturated olive moss around base. No mushrooms, no leaves, no background, no ground tile, no cast shadow, no text. Simple readable low-resolution pixel clusters, restrained dark brown wood, clear silhouette. Intended final production canvas 40 by 40 pixels at native 32 pixels per world unit, stump below human knee height. Fill canvas with small transparent padding. Truly transparent background.

## Integration
Existing stump definition PresentationVariants; stable equal choice between base and rotten sprite. Same radius 0.22, logical footprint, LoS flags and renderer count. Saved scale preview includes both original and alternate beside Jerzy. Re-enter forest to rebuild views; no new seed needed.
