# Forest boulder and stump art

Built-in imagegen, 2026-10-12; both transparent outputs visually inspected. Originals preserved in C:/Users/Patryczku/.codex/generated_images/01a0a7db-0cd8-75d2-8f08-625231ded602/exec-1cf57265-be2d-4cb2-b7f3-10695be370e7.png and exec-5bfd2978-ad9e-4d48-8221-e35492b4e916.png.

Project files: Assets/_Project/Art/ForestPrototype/forest-boulder-v1.png; forest-stump-v1.png. Point/no mipmaps/max128, true alpha preserved. Pivots account for source transparent padding at grounded base. Boulder presentation width 1.15, circular collision radius .32; stump width1, radius .22. Logical occupancy one cell each. Boulder blocks LoS; low stump does not.

## Boulder prompt

Single isolated mossy boulder sprite for dark fantasy pixel art top-down 2D RPG. Low squat asymmetrical grey rock, cracked weathered facets, dark rotten olive moss on upper faces and roots of moss along base. Three-quarter overhead view matching classic RPG environment sprites, crisp intentional pixel clusters at about 64x48 logical detail. Full silhouette centred, base at bottom centre; neutral subdued lighting. No glow, no border, no text, no ground tile or cast shadow outside object. True alpha transparent background. This is a solid obstacle one grid cell wide, not a mountain. Save local PNG.

## Stump prompt

Single isolated rotten tree stump sprite for dark fantasy pixel art top-down 2D RPG. Short broken dark grey brown stump with irregular splintered rim, hollow interior, a few short exposed roots and sparse dark olive moss. Low squat silhouette, three-quarter overhead view, crisp intentional pixel clusters at about 48x48 logical detail. Full silhouette centred with grounded root base at bottom centre; neutral subdued lighting. No glow, no border, no text, no ground tile or cast shadow outside object. True alpha transparent background. This is a small solid knee-high obstacle. Save local PNG.



Small stone derivative: `Assets/_Project/Art/ForestPrototype/forest-small-stone-32ppu.png`, resized from the existing boulder source with Aseprite to 24x18 px; imported32PPU at native world scale. No new image-generation prompt.
