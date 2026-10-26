# Dry thorn shrub

Generated with built-in imagegen, source preserved as `Assets/_Project/Art/ForestPrototype/forest-dry-shrub-v1.png`.
Production derivative `forest-dry-shrub-v1-32ppu.png`: Aseprite resize to 40×24, Point, native 32 PPU, scale 1. Ground decoration aligns visible bounds to its selected cell; no collider.

## Prompt
One isolated low dry thorn shrub for a dark fantasy forest 2D pixel RPG, three-quarter top-down view. Sprawling tangled crooked bare twigs, a few dull brown dead leaves, muted dark grey brown bark, irregular asymmetric silhouette, very low compact undergrowth no tall trunk. Crisp simple readable pixel clusters, restrained palette matching old fantasy RPG sprites. Intended final 40x24 pixel canvas at native 32 pixels per world unit, below a standing human's knees. Transparent background, small padding. No ground tile, no cast shadow, no glow, no flowers, no text, no other objects.

## Integration
DryShrub optional profile sprite. Replaces roughly half of low shrub selections in dry patches; wet patches retain green shrub. Stable hash chooses appearance, existing placement budget and path clearance retained. Missing sprite falls back to green shrub. Ground sorting/lighting unchanged. Scale preview includes sprite beside Jerzy. Re-enter forest to rebuild views; no fresh seed necessary.
