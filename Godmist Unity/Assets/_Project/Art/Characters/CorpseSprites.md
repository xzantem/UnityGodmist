# Catacombs corpse sprites

Generated with built-in imagegen, reference-guided edit mode, transparent background. Each reference was the current unit's south-facing idle frame from Resources/Godmist/CharacterAnimations. Exported with Aseprite to 64×64 (Dead King 128×128); originals remain in generated_images.

Prompt brief: a dedicated collapsed corpse of the referenced character, lying on the dungeon floor in a three-quarter overhead view, slack limbs and horizontal silhouette, matching the original gear and natural palette. No upright sprite rotation, red tint, gore, background, floor, cast shadow or text. Pixel art, transparent alpha.

Runtime lookup: existing CharacterAnimationResolver unit mapping, `<unit>/<unit>-corpse-<size>`, PPU 32, centered ground pivot. Explicit CharacterDefinition.DeadSprite takes priority. Unmapped enemies retain the legacy fallback until their corpse art is supplied. Unity's existing CharacterAnimationTextureImporter supplies Point, RGBA32, no compression or mipmaps.

| Unit | Final asset (relative to Assets/Resources/Godmist/CharacterAnimations) | Original generated file |
|---|---|---|
| ghoul-humanoid | ghoul-humanoid/ghoul-humanoid-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-589b51be-b12c-429a-adbb-c54f8be15eab.png |
| skeleton-defender | skeleton-defender/skeleton-defender-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-c385f237-82ac-4737-b143-06e308d85f22.png |
| skeleton-archer | skeleton-archer/skeleton-archer-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-721762b6-183a-4391-b1e0-b7a03b30468f.png |
| skeleton-swordsman | skeleton-swordsman/skeleton-swordsman-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-4c6763bb-bff9-4ad9-a255-ea01d40dda7e.png |
| skeleton-occultist | skeleton-occultist/skeleton-occultist-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-4c74a822-2e45-472d-9f10-d50c0bff8f33.png |
| undead-fleshy | undead-fleshy/undead-fleshy-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-435a07b3-2cd8-40ac-90bc-a43f79a8ee15.png |
| wraith | wraith/wraith-corpse-64.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-6747fbc6-5d71-4976-ae36-0588cecaf02a.png |
| dead-king | dead-king/dead-king-corpse-128.png | C:\Users\Patryczku\.codex\generated_images\01a0a7db-0cd8-75d2-8f08-625231ded602\exec-b1de9402-a06e-4bf3-91a7-caf57a44972b.png |

