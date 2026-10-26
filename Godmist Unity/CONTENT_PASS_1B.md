# Content pass 1b — Skażony Las

Source: `../design/content_pass_1b.xlsx`; boss mechanics: `../design/bossy2.0.xlsx` plus the decisions made in chat. One dungeon, one boss in this pass. Other bosses/dungeons remain outside scope.

## Visual direction

Dark fantasy forest inspired by the oppressive density of Darkest Dungeon's Weald: crooked trunks, tangled roots, closed canopies, muddy paths, diseased brush and swallowed ruins. Muted rotten greens, brown wood and cold greys. Strong poison/corrupted-fire accents are reserved for mechanics. The city greenery reused by the prototype is temporary, not final forest art.

## Implementation slices

1. Forest traversal prototype: irregular glades, long/winding paths, optional branches, shared structural collision/LoS, separate outdoor presentation assets.
2. Interior cover: trees, multi-cell rocks, LoS brush, footprint-safe placement and crown occlusion. No randomly scattered gaps in the logical boundary.
3. Forest enemies: seven workbook archetypes, encounters, patrol anchors and skills. Resolve growth activation timing and Flare debuff duration before those mechanics are authored.
4. Boss 2.0 foundation and Ent: one transition at 50% HP after the resolving ability; complete phase kit/model switch. Phase two ignites arena boundary trees. Plague persists with a five-stack cap; Sanctuary has a one-turn warning. Dead King update is tracked separately from the forest traversal fixture.
5. Loot/materials/unique equipment: workbook pools and four uniques; renamed Żywioł Lasu. Reuse shared equipment/galduryte bags where specified.
6. Final art, offers and integration: production art, danger/save-load checks, full expedition smoke/performance. Enable offer only when the expedition has actual enemies and rewards.

## Traversal fixture

ID: `dungeon.forest.prototype`. Registered in the main content catalog for explicit loading/restoration, not available on the offer board. It deliberately contains no Catacombs encounters, loot, doors or progression gates. Eleven authored footprints include two long paths, optional clearings and an Ent arena; ten to thirteen rooms, three to four optional branches, one outdoor floor. Wide glades now include sparse interior tree obstacles.

Authoring: `Godmist/Content/Pass 1b/Configure Forest Prototype`, or batch execute method `Godmist.Editor.ForestPrototypeSetup.Configure`. Repeatable; updates only its own assets and adds its catalog reference if absent.

Presentation replaces boundary masonry with vegetation. Fog/lighting consume those same edge renderers. Interior tree obstacles use the shared object pipeline; multi-cell rocks, LoS brush and crown fading remain subsequent work.

Boundary depth update: five layers occupy non-walkable cells outside the map, deduplicated per cell and shared with the nearest edge's fog state. Stable position/size/mirroring variation breaks the regular hedge. RGB brightness falls with depth; the last layer is nearly black. No new traversal cells or colliders. Catacombs wall art stays disabled across occupancy refreshes; outdoor vegetation disables automatic wall torches. Difficult terrain keeps its logical cost without bright rectangular tint patches in this prototype.

Tree art alignment: Forest's bottom-pivot prototype sprite is projected half a cell down (`TreeVisualOffsetInCells = (0, -0.5)`) so the visible vegetation matches the blocked strip. Depth ordering remains anchored to the ground footprint, not the shifted sprite transform. Navigation, LoS and colliders stay unchanged.

## Organic boundary follow-up

Forest catalogs opt into a deterministic, single-cell traversal fringe. Shallow pockets extend the actual floor surface and retain glade ownership, so grid movement, combat, AI, native navigation and saved floor geometry use the same shape. The fringe cannot bridge different room owners or fill enclosed tree islands; it does not recursively grow into the forest.

The nearest trunks now ground directly at the actual boundary with small tangential variation, independent of edge orientation. Deeper layers retain the art offset. Forest-only physical boundaries are 0.06 units wide with rounded ends; native navigation uses the same thickness and a 0.02-unit safety margin instead of the masonry margin of 0.10. Deep forest remains impassable.

Decorative ground extends under all five tree layers, overlaps to hide black tile seams, inherits its nearest edge's fog state and the existing ground lighting, and darkens towards black. It adds no floor cells/colliders and stays below the playable floor; ordinary ground props retain their existing ordering.

Fresh generation is required for fringe pockets; saved or already generated prototypes retain their saved geometry. Re-entering an existing saved floor does not regenerate its map.

Individual-trunk follow-up (2026-10-11): the safe first band is traversable floor, with separate circular root colliders on selected former boundary edges. TreeTrunk markers preserve placement through saves, allow tactical cell-centre crossings, and do not become full edges in native navigation; native routes use the physical root bounds. The continuous boundary is deeper in the forest. Existing saved/generated geometry is unchanged; generate a NEW prototype for smoke. Check approaching roots from all directions and walking between trunks. Automated verification: out/forest-trunk-final.xml (ForestPrototypeTests, DungeonRoomFootprintTests, DungeonNavigationMeshTests, DungeonLayoutSnapshotTests), including physical passage/blocking and TreeTrunk JSON restore. Live visual smoke remains pending; no manual wiring needed. This supersedes the earlier shallow-pocket-only collision approach.
Final verification: 69/69 EditMode PASS, no skips (out/forest-trunk-final.xml). Targeted source/whitespace self-review PASS; visual smoke pending.

Forest presentation cleanup (2026-10-11, READY_FOR_SMOKE after focused verification): minimap excludes TreeTrunk markers from wall rasterization, retaining Full/Low/Railing semantics. Decorative underlay shares the strongest known fog state of adjacent underlay owners on the visible level (precomputed local edge sets, no new visibility rays), avoiding isolated missing squares along an already visible canopy. No playable floor/LoS/save geometry is revealed or changed. First-row art scale varies deterministically from 85–110%; root colliders stay unchanged. Verify underlay around a partially visible glade, minimap passages and approach to trunks. No manual wiring, no computer use, live visual smoke pending.
Verification: 35/35 EditMode PASS, no skips (out/forest-presentation-final.xml), including minimap trunk-versus-full-wall raster regression, forest physical roots/passages and native navigation. Targeted whitespace/source self-review PASS. No live GPU visual verification.

Interior cover — first slice (READY_FOR_SMOKE, 2026-10-11): four wide glade templates now contain up to two required, off-centre tree anchors. Narrow clearings, entrance, corridors and the Ent arena remain clear. Shared obstacle placement/occupancy/LoS/save lookup are used; exploration opts into physical circular roots (radius 0.18), while tactical occupancy and LoS use one cell. Optional definition-owned sprite/tint/width/root radius leave existing obstacle prefabs unchanged unless opted in. Tree art uses the temporary city sprite at width 1.5. No new enemy/loot content or offer activation. Generate a NEW prototype to obtain these saved objects; existing saves are unchanged. Automated: 69/69 EditMode PASS, no skips (out/forest-interior-final.xml), including ten seeded placements and all-floor connectivity after excluding trunks, LoS, actual prefab geometry/rebind and placement/navigation/layout regressions. Forest assets authored in the validation mirror and copied back by exact file, no Editor wiring needed. Live visual smoke is pending. Remaining interior slice: multi-cell rocks, LoS brush and crown occlusion.

Forest prototype entry/import follow-up (2026-10-11): main YAML pool/anchor references match the shipped tree GUID; isolated tests that load shipped assets without Configure pass. A stale in-memory pool was reproduced with the same placement exception; ForceUpdate reimport alone does not restore it. The Inspector prototype entry now imports the tree/definition, adds and saves only the missing owned tree pool reference when necessary, validates, then enters. It does not reauthor rooms or relax runtime placement validation. Serialized wiring is within the user-authorized pass 1b scope. Added standalone ForestImportedAssetsTests, independent of authoring setup; includes entry with a deliberately stale in-memory pool and on-disk content equality. Live original exception was not reproduced in the validation mirror, so final live entry remains smoke verification.

Verified: 2/2 independent imported-asset tests PASS (out/forest-pool-repair-final.xml), including deliberately emptied loaded pool, automatic reference repair, unchanged final dungeon asset bytes and successful placement. Source/whitespace review PASS. READY_FOR_SMOKE: retry the existing prototype entry button after compilation; no authoring-menu action required.

Forest ground polish (READY_FOR_SMOKE, 2026-10-11): corrected the bottom-pivot underlay anchoring so visible sprite bounds, rather than its pivot, are centred on each void cell. Forest-only underlays overlap at 2.4 cells and use a per-renderer world-bounds opt-in in Silhouette Floor to feather circular edges with pixel-scale variation; ordinary floor has zero opt-in and retains its alpha. Existing lighting property blocks preserve the bounds. Skip masonry-only side-void black occluder generation for vegetation maps. Ground cover uses the existing transparent grass sprite, deterministic sparse clustered placement, muted tint, shared ground lighting/fog, and skips occupied presentation cells; no new colliders or map/save/navigation changes. GroundCover reference added to DVP_Forest_Prototype and authoring setup. Tests: 48/48 EditMode PASS, no skips (out/forest-polish-verified.xml), including real GPU alpha readback for feathered centre/rim/corners and unchanged ordinary floor, deterministic cover/occupied-cell exclusion, actual underlay bounds, navigation, minimap and fog blending. Full live Forest visual smoke and FPS measurement remain pending; this is not a claim of visual acceptance. Reopen/rebuild the prototype presentation to see changes; no manual wiring.

Forest floor/fog alignment and crown occlusion (READY_FOR_SMOKE, 2026-10-11): the playable dirt tile also uses a bottom pivot. Its previous scale-only transform displaced its rendered bounds half a cell above the logical floor/soft-fog mesh. CenteredGroundTransform now normalizes sprite size AND centres sprite bounds; both authoring and vegetation-only per-cell presentation use it, including already loaded tile assets. Tile_Forest_Prototype updated. This corrects the uncovered rectangular strip without revealing unknown floor cells or modifying map geometry. Boundary and interior tree crowns use per-renderer _ForestCrown parameters in their existing wall/body shaders; lower trunk remains opaque, upper crown fades towards 22% when overlapping a visible actor behind its root, then restores after the actor leaves. Visible party/enemies are considered; unseen enemies do not drive fade. Runs in existing lighting cadence with cached obstacle renderers, no extra traversal/LoS work. Tests: 72/72 EditMode PASS, no skips (out/forest-crown-final.xml), including GPU composed tilemap+fog rendering and absence of northern spill, real wall/body crown alpha and retained trunk, overlap/front/side rules, fog transitions/blending and lighting cadence regressions. Render inspected: out/forest-floor-fog-alignment.png. Live full Forest visual/movement smoke remains pending. Reopen the prototype presentation after compilation; no manual wiring.

Forest crown fade rollback (READY_FOR_SMOKE, 2026-10-11): reverted at user request after reported runtime cost. Removed all tree/actor overlap scans, crown property-block updates and crown alpha code from wall/body shaders; trees retain ordinary opacity. Preserved centred floor/fog alignment, feathered underlay, ground cover and physical trunk navigation. Verification: 71/71 focused EditMode tests PASS, no skips (out/forest-crown-revert.xml); targeted diff whitespace check PASS. No manual wiring required. Live FPS not measured in this rollback. This supersedes the crown-fade portion of the previous entry.

Standing-object depth query optimization (READY_FOR_SMOKE, 2026-10-11): eliminated captured List.Exists predicates from per-frame standing-object and door depth queries. Registration lists stay sorted; binary lookup checks both epsilon neighbours and inserts only new baselines. Existing ordering/rebuild semantics remain intact, including trees, pillars and doors; no serialized/save changes or manual wiring. Verified 72/72 EditMode tests PASS, no skips (out/forest-depth-queries.xml): real actor/obstacle occlusion, forest presentation and a zero-managed-allocation regression over 4,000 repeated exact/nearby baseline queries with unordered initial registration. Targeted whitespace/self-review PASS. Live FPS impact is not measured.

Forest visual variety (READY_FOR_SMOKE, 2026-10-11): boundary/fringe tree silhouettes vary in horizontal aspect while grounded roots and colliders stay fixed. Ground cover uses continuous low-frequency Perlin clustering instead of four-cell block exclusions, muted olive/brown tint variation and small rotations; placement stays deterministic and excludes occupied cells. Depth brightness now follows smooth extinction to retain readable nearby tree layers and darken the distant forest. All variation is established during presentation construction; no additional per-frame work, renderers, shaders, colliders or serialized/save changes. Verification: 36/36 focused ForestPrototypeTests and fog tests PASS, no skips (out/forest-visual-variety.xml); targeted diff check/self-review PASS. Full live art acceptance remains pending. Reopen/rebuild the prototype presentation after compilation; no new dungeon generation or manual asset wiring required.

Forest ground/interior-tree variety (READY_FOR_SMOKE, 2026-10-11): playable dirt uses deterministic quarter-turns and horizontal mirrors around normalized sprite bounds centre, reducing repeated texture orientation while preserving exact cell/fog coverage. Positive-TrunkRadius tree presentations vary width/height by at most 10%, mirror and shade at binding; grounded origins and physical roots remain unchanged. No added renderers, shaders, frame loops or save fields. Verified 72/72 ForestPrototypeTests/DungeonSimpleVisualTests PASS, no skips (out/forest-ground-variety.xml), including per-cell centre/corner bounds, GPU composed floor/fog alignment, repeated tree bind stability, root dimensions and actor occlusion. Targeted whitespace/self-review PASS. Live art acceptance remains pending. Re-enter prototype after compilation; no new generation/manual wiring needed.

Forest trunk clearance fix (READY_FOR_SMOKE, 2026-10-11): direct movement swept the full actor collider correctly, then incorrectly treated its centre/perimeter topology samples as separate actor feet positions. CanEnterTraversalAtWorld reapplied both collider radius and feet offset at these samples, artificially enlarging physical props. Footprint samples now check only real floor/topology and logical full-cell obstacles; the preceding real collider sweep and endpoint checks retain physical trunk protection. Native route start validation uses the actual actor world footprint so standing beside a trunk inside its occupied logical cell does not disable NavMesh. Full-cell obstacles, closed edges, levels and tactical occupancy stay unchanged; no collider shrinking or save/asset edits. Verification: 77/77 ForestPrototypeTests, DungeonNavigationMeshTests and DungeonExplorationPathfinderTests PASS, no skips (out/forest-trunk-clearance-final.xml). Regression approaches the actual tree prefab from four cardinal directions with a nonzero feet offset, verifies near clearance and penetration rejection, and proves native route ownership when leaving the tree logical cell. Whitespace/self-review PASS. Live screenshot case remains smoke pending; re-enter prototype after compilation, no new generation/wiring needed.

Forest undergrowth base (READY_FOR_SMOKE, 2026-10-11): replaced the brown city dirt reference with a dedicated generated forest-floor-v1 texture: muted moss, decaying leaves, fine roots and damp earth gaps. GroundTint updated to (0.78,0.80,0.70), retaining subdued rotten greens. Shared GroundTile updates both playable floor and feathered boundary underlay; existing sparse grass remains supplementary. New texture imported Point/no mipmaps/uncompressed with max size 128; no additional renderers, colliders, frame loops or save fields. Authoring points at the new asset, and verified Tile_Forest_Prototype/DVP_Forest_Prototype serialized outputs copied back from the validation mirror. Tests: 37/37 ForestPrototypeTests and fog tests PASS, no skips (out/forest-undergrowth.xml), including GPU composed floor/fog alignment, imported ground geometry, stable placement and trunk approach/native routing. Inspected generated source and actual GPU floor/fog render. Live full Forest visual acceptance pending; re-enter prototype after compilation, no new generation/manual wiring required. Built-in image_gen used; provenance/prompt in FOREST_UNDERGROWTH_ART.md.

Forest broad ground patches and visual paths (READY_FOR_SMOKE, 2026-10-12): forest-only floor shader now uses continuous world-space litter at four-cell scale instead of independently rotated high-frequency tile textures. Baked low-frequency fields vary moss/litter colours and damp earth coverage. A presentation-only mask traces room connections to internal room centres on the real irregular footprint; contiguous doorway cells form one portal to avoid a fan of bare soil. Paths blend dark, desaturated existing dirt with moss through soft irregular rims. Mask is built during map presentation rebuild, reused across occupancy/camera changes, capped at 2048 per axis, and disposed with the view; broad/edge noise is baked on CPU rather than evaluated per fragment. Shared floor lighting binds the textures only when the mask changes; ordinary indoor/body shaders stay unchanged. No new scene renderers, colliders, navigation or save fields. SoilSprite is wired in the vegetation profile and repeatable authoring. Verification: 73/73 ForestPrototypeTests and fog/lighting cadence tests PASS, no skips (out/forest-paths-verified.xml). GPU preview inspected and copied to out/forest-paths-preview.png; corrected overly wide/light soil after first render. Regression covers connected-room rendering, mask reuse on occupancy refresh, unchanged traversal revision and indoor opt-out; prior floor/fog/trunk tests remain green. Targeted diff/self-review PASS. Live expedition appearance and FPS remain unmeasured. Re-enter prototype after compilation; no new generation or manual wiring. This slice covers broad undergrowth/paths; remaining black fog rectangles and large forest props are separate follow-ups.

Natural trail correction (READY_FOR_SMOKE, 2026-10-12): visual paths are now routed after fixed obstacle placement through a separate SetForestPathObstacles input supplied by DungeonSandboxBootstrap. BFS excludes active movement-blocking obstacle footprints; actors/bodies and ordinary presentation occupancy do not rebuild paths. Same obstacle set reuses the mask. Trail radius varies deterministically (0.32–0.56 cells plus a small junction widening), with jittered centres, worn/litter-filled sections and less bare mud. Corrected Unity SmoothStep usage via normalized InverseLerp so brush width/round root exclusion have the intended thresholds. Root exclusion fades radially rather than punching square holes. Litter texture world scale changed from quarter to half repeats per cell, halving apparent detail size while retaining broad colour patches. Forest fog gets a wider cached inward feather (0.95 vs 0.6 cells), never exposing unexplored cells; original indoor fog is unchanged. No frame-loop rebakes or traversal/save/collider changes. Verification: 74/74 ForestPrototypeTests and fog/lighting tests PASS, no skips (out/forest-natural-trails-verified.xml). GPU regression reads the actual mask to assert no exposed trail under trees, narrow coverage and obstacle-set reuse; forest fog test checks stronger inward fading and zero unknown brightness. Final preview inspected at out/forest-paths-preview.png. Whitespace/self-review PASS. Re-enter prototype after compilation; no fresh dungeon generation/manual wiring required. Live screenshot acceptance, exact disappearance of exterior black cutoffs and FPS remain unverified.


## Forest edge details — READY_FOR_SMOKE (2026-10-12)
- Added muted low shrub and small rotten wood sprites at clearing fringes. Deterministic placement omits trails and their adjacent cells, and occupied cells and their adjacent cells.
- Uses existing decoration fog, light and depth sorting. No colliders, navigation/persistence changes or per-frame behaviour.
- Profile and repeatable ForestPrototypeSetup wire existing shrub and new transparent forest-deadwood-v1.png. Prompt/provenance: FOREST_EDGE_DETAILS_ART.md.
- Verification: out/forest-edge-details-verified.xml, 74/74 EditMode tests passed, including new sprite references, fringe placement, trail exclusion, deterministic rebuild and unchanged traversal/collision checks. Targeted diff whitespace check passed.
- Smoke: re-enter forest prototype; inspect subdued small edge decorations, clear paths, fog and depth sorting. Live appearance and FPS remain unmeasured. No new seed required.


## Forest prop material correction — READY_FOR_SMOKE (2026-10-12)
- Root cause: ApplyGroundLighting assigned the forest floor material to sprite decorations. Its world-space forest blend replaced their RGB with soil/litter, hiding the intended deadwood/shrub appearance despite correct floor ordering.
- Per-renderer property now disables forest surface blending for props, preserving their sprite and shared lighting. Forest underlay patches retain blending via their existing ForestPatchBounds marker. Scale and sorting unchanged.
- Verification: 74 existing forest/fog/lighting tests passed in out/forest-prop-material.xml. Two added tests initially failed due to a missing fixture sprite; after correcting fixture, all 37 lighting tests passed in out/forest-prop-material-retest.xml. Both prop and forest underlay material modes covered; shared material remains unchanged. Targeted diff check passed.
- Smoke: re-enter forest and inspect actual wood/shrub silhouettes. Live appearance/FPS unverified.


## Forest party light — READY_FOR_SMOKE (2026-10-12)
- Forest presentation builds one carried torch using the existing torch sprite/material and shared light/shadow arrays. Source follows current party leader (including leader changes) on existing 0.2s lighting refresh; fixture is offset beside/above the feet and scaled to 65%.
- Indoor wall torches unchanged. Uses existing radius, warm palette and flicker; ambient and fog discovery unchanged. Existing lighting teardown owns the fixture. No serialized or persistence changes.
- Verification: out/forest-party-light.xml, 60/60 forest and lighting EditMode tests passed; targeted diff check passed. Live carried-fixture placement, visual brightness, combat visibility and FPS remain unverified. A moving source invalidates affected shadow slices at the existing cadence; no claim of performance improvement.
- Smoke: re-enter forest; check local light, wood/shrub colours, leader cycling, movement and camera focus during combat. This is a prototype visual source, not an equipment/fuel mechanic.


## Invisible forest party light — READY_FOR_SMOKE (2026-10-12)
- User requested removal of the visible carried torch while keeping light. Forest source renderer now forceRenderingOff; normal visibility refresh cannot make it draw again. Existing source position, flame anchor, radius, shadows and cadence unchanged. Indoor torches unchanged.
- Verification: targeted diff check and inspection of visibility-refresh path. No test rerun for this presentation-only toggle; live appearance remains for smoke. Re-enter forest after scripts reload. Tree-mounted torches remain optional future work.


## Forest tree silhouettes — READY_FOR_SMOKE (2026-10-12)
- Added transparent dead and crooked sparse-crown tree art via built-in imagegen; original outputs preserved, full prompts and asset paths in FOREST_TREE_VARIANTS_ART.md.
- Stable cell-based selection: original 9/12, dead 1/12, crooked 2/12. Boundary trees, individual fringe trunks and authored interior tree obstacles use variants. Missing optional variants fall back to original. Profile and repeatable setup wire both new sprites.
- Art pivots align asymmetric roots; imported Point/no mipmaps/max128. Existing circular root collision, tactical occupancy, LoS and save identities unchanged. No runtime update loop or crown fade added.
- Verification: out/forest-tree-variants.xml 60/60 forest/lighting EditMode tests passed, including physical approach, rebind stability and ten-seed reachability. Allowed-sprite assertions now include authored variants. Targeted diff check passed. Live visual alignment and FPS require smoke.
- Smoke: re-enter forest prototype; check mixed silhouettes and root approach, especially flipped crooked trees. Fresh seed unnecessary. Large rocks/stumps and landmarks are later slices.


## Forest boulders and stumps — READY_FOR_SMOKE (2026-10-12)
- Added two generated transparent sprites (built-in imagegen); sources/prompts saved in FOREST_ROCK_STUMP_ART.md. Point/no mipmaps/max128, pivots account for transparent padding.
- New owned definitions object.forest.boulder.prototype and object.forest.stump.prototype. One logical cell each; circular exploration bases radius .32/.22; boulder blocks LoS, knee-high stump does not. Existing obstacle placement/view/save references used; no new per-frame behaviour.
- Up to one boulder and one stump near existing trees in roomy off-centre areas, full 3x3 floor clearance and central cross/approaches preserved. Authored anchors added to hollow, long_glade and roots; other templates remain clear where no suitable space exists. Dungeon pool and imported-prototype stale-pool repair include both. No unrelated content assets copied.
- Verification: 60/60 initial forest/lighting tests passed (out/forest-rock-stump.xml). Expanded forest/imported-asset suite 27/27 passed (out/forest-rock-stump-final.xml), including real prefab radius/grounding/rebind for both props, distinct LoS flags, shipped pool repair, ten-seed exact placement and all-cell reachability. Intermediate test overrequired props in every seed; corrected to require actual coverage across seeds while retaining exact per-template anchor counts and reachability. Targeted diff check passed.
- Smoke: generate a fresh forest expedition to obtain new saved objects (existing saved instance unchanged); inspect grounded art, collider approach and clear trails. Live visuals/FPS unverified. Mob/icon asset work left untouched.


## Forest prop distribution correction — READY_FOR_SMOKE (2026-10-12)
- Previous authoring required an existing tree anchor and a wide central cross; only three templates received rocks/stumps and Start never did. This explains empty entrance/other clearings, independent of sprite import. Screen alone does not prove current room identity.
- Allow Start/Normal/Optional props without prerequisite tree anchors. Retain full 3x3 floor clearance, 2-cell prop separation, difficult-terrain exclusions and a clear central cross (1.25-cell exclusion). Corridors and boss arena remain clear. Entrance now always has one boulder and one stump.
- Verification: out/forest-prop-coverage.xml 27/27 forest/imported-asset tests passed, including explicit two-prop entrance, real colliders and ten-seed all-cell reachability. Targeted diff check passed. Updated owned room assets only. No rendering-scale changes; live visual smoke pending.
- Generate a NEW forest instance; saved old object placements do not acquire these anchors.


## Swallowed forest ruins — READY_FOR_SMOKE (2026-10-12)
- New broken moss-covered column sprite generated through built-in imagegen, inspected and saved as forest-ruined-pillar-v1.png. Original and full prompt documented in FOREST_RUINS_ART.md; Point/no mipmaps/max128, grounded pivot accounts for padding.
- New object.forest.ruined_pillar.prototype with one-cell tactical footprint, .3-radius physical base, LoS blocking and shared obstacle lighting. Required pillar + existing boulder/stump cluster authored only in optional overgrown_ruins template. Dungeon pool/import repair updated; normal glades, corridors and arena unchanged.
- Verification: out/forest-ruins-final.xml 28/28 ForestPrototypeTests/ForestImportedAssetsTests PASS, including dedicated real-prefab collider/rebind case, landmark confined to ruins, shipped pool repair and ten-seed all-cell reachability. Initial imported-pool test ran before authoring the new asset and failed; repeat against authored shipped state passed. Targeted diff check passed.
- During implementation an encoding error truncated the untracked authoring source. Recovered prior behaviour from its preserved compiled Editor assembly with dependencies resolved, rewrote UTF-8 and revalidated the entire forest/import suite; recovery outputs remain under out/forest-recovery*. Runtime source/other artwork untouched.
- Smoke: generate a fresh forest instance; optional room Pochłonięte ruiny now contains the landmark. It is not in every entrance or necessarily every generated seed. Live visual alignment and FPS pending. This is an initial ruin landmark, not a finished ruin set or gameplay interaction.


## Abandoned forest camp — READY_FOR_SMOKE (2026-10-12)
- Added cold stone-ring campfire and broken supply crate sprites with built-in imagegen; true alpha inspected, originals/prompts in FOREST_CAMP_ART.md. Point/no mipmaps/max128; grounded pivots account for padding.
- New owned obstacle definitions object.forest.cold_campfire.prototype and object.forest.broken_crate.prototype, one-cell logical footprints and .26/.28 circular bases. Neither blocks LoS. No fire light, loot/rest interaction or new per-frame work.
- Optional blind_glade keeps its ID, display name now Porzucone obozowisko. Exactly one fire ring and one crate replace its generic edge props. Central cross exclusion narrowed to 1 cell for this template only, retaining full 3x3 floor clearance and 2-cell spacing. Other rooms and boss arena unchanged. Pool/import repair includes both definitions.
- Verification: out/forest-camp.xml 28/28 forest tests PASS including both real-prefab collision/rebind cases, camp-only anchors and ten-seed all-cell reachability. out/forest-camp-imported.xml 2/2 shipped asset/pool tests PASS. Targeted diff check passed. Copied only two new definitions, owning room and forest dungeon definition. Mob/icon work untouched.
- Smoke: generate NEW forest; optional camp room may vary by seed. Check ground alignment, paths and visual readability. Live appearance/FPS remain unverified; this slice is environmental content only.


## Forest roots and fungi — READY_FOR_SMOKE (2026-10-12)
- Added exposed roots and pale woodland fungi sprites; originals and prompts recorded in FOREST_ROOTS_FUNGI_ART.md. Forest presentation profile and authoring bind both sprites.
- Both reuse existing edge-decoration slots, replacing part of the shrub/deadwood distribution without increasing its count. Existing path/occupied-cell exclusions, fog and lighting remain in use; no colliders or per-frame update added.
- Verification: out/forest-roots-fungi.xml — 65/65 ForestPrototypeTests and DungeonLightingCadenceTests PASS. Copied the authored forest presentation profile from validation mirror. Live visual scale/readability and FPS not measured.
- Smoke: leave and re-enter the forest to rebuild decoration; a new seed is not required for this slice.


## Woodland fern — READY_FOR_SMOKE (2026-10-12)
- Added fern sprite and WoodlandFern presentation reference, authored by ForestPrototypeSetup. Built-in imagegen original and full prompt: FOREST_FERN_ART.md. Inspected sprite and verified RGBA alpha; Point/no mipmaps/max128 matches existing props.
- Fifth deterministic edge-detail variant reuses existing sparse slots; rendered width .9–1.15 cells. No additional decoration slots, collision, LoS or per-frame update. Existing path and occupied-cell exclusions retained.
- Verification: out/forest-fern.xml — 65/65 forest and lighting tests PASS; targeted diff check PASS. Synced only owned files to validation mirror and copied authored presentation profile back. Live visual scale/readability and FPS remain unmeasured.
- Smoke: leave and re-enter forest; fresh seed unnecessary for this presentation-only change.


## Forest visual scale review — READY_FOR_SMOKE (2026-10-12)
- Rendered and inspected all ten ground props alongside Jerzy sprite using actual exploration prefab body scale on a one-unit grid. Before/after renders exposed transparent padding differences: fungi occupy ~49% of image width, campfire ~59%, stump ~66%. Source art is not uniformly 32x32: forest props use max128 Point import; PPU32 alone does not define rendered world size.
- Increased baseline full-sprite widths: deadwood .7→1.1, shrub .7→1, fungi 1.05→1.4, fern .9→1.1 (existing +0–.25 variation); roots remain 1.05. Object presentation widths: boulder 1.15→1.4, stump 1→1.2, cold campfire 1.3→1.7, crate 1.05→1.25; pillar unchanged. All collision radii and tactical footprints retained.
- Added editor menu Godmist/Content/Pass 1b/Forest Scale Preview and saved Assets/_Project/Scenes/Prototypes/ForestScalePreview.unity with owned preview materials. Menu asks to save modified scenes before replacing the open scene. Preview uses unlit neutral background to isolate proportion; actual forest lighting appearance still needs live smoke.
- Verification: out/forest-scale-tests.xml 67/67 forest, imported assets and lighting tests PASS. Final screenshot out/forest-scale-preview.png inspected. Copied only owned preview assets and four resized definitions from validation mirror; no icon/mob files touched.
- Re-enter forest for decoration rebuild and new object visuals. No new seed required. Future props should be compared in this scene before acceptance.


## Forest native 32 PPU correction — READY_FOR_SMOKE (2026-10-12)
- User requirement: one artwork pixel = 1/32 world unit, matching characters. Previous importer-only PPU plus arbitrary Transform scaling violated this and is superseded.
- Added 14 non-destructive -32ppu PNG derivatives with Aseprite pixel resize, keeping source originals. Native canvas sizes: deadwood/roots 40x20, shrub40x18, fungi48x36, fern40x27, grass28x9, boulder48x36, stump/crate40x40, column56x75, fire56x35, dense tree48x68, dead/crooked trees48x77. Imports: PPU32, Point, no NPOT rescaling; references authored to derivatives. Source provenance remains in respective FOREST_*_ART.md documents.
- Removed forest decoration and boundary-tree size/aspect randomness and ground-cover rotation. Opt-in NativePixelScale on forest obstacle definitions compensates inherited parent scale and bypasses PresentationWidth and random size variation. Other obstacles preserve old behavior. All physical collider radii and tactical footprint rules retained. Shader terrain already quantizes its world samples to 32 pixels/unit; feathered underlay geometry remains deliberately overlapped.
- Rendered and inspected updated ForestScalePreview beside actual Jerzy prefab body; screenshot out/forest-scale-preview.png. Test results out/forest-32ppu-tests.xml: 67/67 forest/import/lighting PASS, including native Sprite PPU and actual lossyScale assertions for props and edge/grass decorations. Main live forest smoke pending.
- Leave/re-enter forest to rebuild all views; no new seed required. Size differences should be represented by pixel dimensions/variants rather than non-unit Transform scale.


## Forest damp soil and moss — READY_FOR_SMOKE (2026-10-12)
- Floor shader reuses existing baked broad and detail mask channels to add irregular darker damp pockets and muted moss areas. Paths suppress moss, remain readable, and retain current route geometry.
- World sampling remains quantized to 32 pixels/unit. No new textures, texture samples, renderers or CPU refresh loops; a small amount of fragment arithmetic added. FPS impact has not been measured.
- Verification: out/forest-moisture.xml 67/67 forest/import/lighting PASS, including rendered floor shader regression checks; targeted diff check PASS. Live visual judgement of strength remains pending. No serialized assets or other chats' art modified.
- Smoke: view forest ground under party light and in dim areas; no new seed required.


## Forest undergrowth grouping — READY_FOR_SMOKE (2026-10-12)
- Edge decoration selection now uses the same world-space broad moisture noise as the forest floor mask: fungi/ferns favored in damp pockets, roots/deadwood in drier ones, shrubs shared. Smooth probability transition avoids hard biome borders.
- Reuses exactly the existing candidate slots and path/occupied-cell exclusions. Native 32 PPU/unit world scale unchanged. Noise sampled only during decoration build; no extra renderers or per-frame CPU work.
- Verification: out/forest-undergrowth-clusters.xml 28/28 ForestPrototypeTests PASS, including deterministic rebuild, collision/path constraints, reachability and native pixel scale checks. Targeted diff check PASS. Visual effect in live forest pending.
- Smoke: leave/re-enter forest; fresh seed unnecessary.


## Fallen mossy log — READY_FOR_SMOKE (2026-10-12)
- Generated and inspected mossy hollow log source; production Aseprite derivative 64x24 px at native32PPU = 2x.75 units. Source preserved. Exact original, paths and prompt in FOREST_FALLEN_LOG_ART.md.
- Sixth edge-detail variant replaces part of dry deadwood slots. Same candidate count, occupied/path exclusions and lighting/fog registration; no collider, LoS blocker, animation or new per-frame CPU update. Profile/authoring wired.
- Added log to saved ForestScalePreview; rendered and visually checked alongside actual Jerzy body, matching pixel scale. Screenshot out/forest-scale-preview.png. out/forest-fallen-log.xml 30/30 forest/import tests PASS, including native32PPU checks, deterministic rebuild and navigation constraints. Targeted diff check PASS. Live dark forest visibility/FPS unmeasured.
- Re-enter forest to rebuild decorations; fresh seed unnecessary.


## Small mossy stones — READY_FOR_SMOKE (2026-10-12)
- Added forest-small-stone-32ppu.png, Aseprite 24x18 derivative of existing forest-boulder-v1.png. Source unchanged, provenance in FOREST_ROCK_STUMP_ART.md. Native32PPU, no world scaling; visible stone ~ankle height when rendered beside Jerzy.
- Seventh sparse edge-detail variant favors damp patches, replacing existing candidates. Same count/path/occupancy exclusion; no collision, LoS or per-frame updates. Profile/authoring wired.
- Updated saved ForestScalePreview and inspected actual render alongside character. out/forest-small-stones.xml 30/30 forest/import tests PASS including native density and navigation constraints. Targeted diff check PASS. Live forest appearance/FPS pending.
- Re-enter forest to rebuild decoration; no new seed necessary.


## Forest contact grounding — READY_FOR_SMOKE (2026-10-12)
- Existing forest surface mask alpha now bakes small elliptical contact shade at static path-obstacle bases (interior trees, rocks, stumps and other registered props). Minimum .7 multiplier, smooth falloff, overlapping areas use minimum rather than multiplying into black holes. RGB path/moisture channels unchanged.
- Floor shader consumes alpha from its existing mask lookup; no added textures, samples or renderers. Bake only when existing static obstacle topology changes. Dynamic actor occupancy does not rebake. Existing world32 sampling retained; collisions/navigation unchanged.
- Verification: out/forest-contact-shade.xml 67/67 forest/import/lighting PASS. GPU readback checks shaded bases and bounds on shaded area, existing cache invariants and shader render test retained. Inspected test render; saved out/forest-contact-shade-preview.png. Actual forest props under torchlight still require live smoke; FPS not measured. Targeted diff check PASS.
- Re-enter forest to rebuild mask; no new seed necessary.


## Overgrown ruins gravestone — READY_FOR_SMOKE (2026-10-12)
- Generated and inspected mossy weathered gravestone; Aseprite production derivative32x40, native32PPU, alpha-base grounded pivot. Original and exact prompt in FOREST_GRAVESTONE_ART.md. Compared rendered sprite beside actual Jerzy body in updated ForestScalePreview.
- Added object.forest.gravestone.prototype / DO_Forest_Gravestone_Prototype. NativePixelScale, circular .2-unit base, one logical cell, no LoS blocking, indestructible; no loot/rest or interaction behavior. Replaces ruins boulder slot beside existing pillar/stump, retaining total obstacle count. Other room templates unchanged.
- Pool importer includes the new definition in matching authoring order. First run found import pool ordering drift (30/31); fixed ordering and reran full forest/import suite: out/forest-gravestone-final.xml31/31 PASS. Real prefab collider/rebind/native density test, ruins-only required anchor and ten-seed all-cell reachability PASS. Targeted diff check PASS.
- Copied only new definition+meta, owned ruins template, forest definition, and preview assets from validation mirror. Live torchlight appearance/FPS unmeasured.
- Smoke: generate NEW forest instance; optional Pochloniete ruiny room contains one gravestone. Existing generated dungeon placements remain unchanged.


## Hollow old tree variant — READY_FOR_SMOKE (2026-10-12)
- Added sparse gnarled hollow tree sprite, generated source preserved and Aseprite64x96 production derivative at native32PPU, Point, grounded alpha base. Exact source/prompt in FOREST_HOLLOW_TREE_ART.md.
- Third presentation variant bound to both forest profile and actual tree object definition. Deterministic selector: hollow1/12, dead1/12, crooked2/12, dense8/12. Missing third sprite falls back to dense tree, preserving older profiles. No extra trees/renderers, size randomization, collider or LoS changes.
- Updated layout of saved ForestScalePreview to accommodate separate tree comparison; inspected render beside Jerzy. Lower visible trunk remains narrow, spreading decorative roots do not define collision. Live forest alignment/torchlight appearance remains pending.
- out/forest-hollow-tree.xml32/32 forest/import tests PASS, including new rarity/native texture dimensions/fallback test, real obstacle binding and navigation tests. Targeted diff check PASS. Copied only owned tree definition/profile and preview assets from validation mirror. No FPS measurement.
- Re-enter forest to rebuild views; no fresh seed needed.


## Interior prop undergrowth — READY_FOR_SMOKE (2026-10-12)
- Existing boundary detail slots define a fixed renderer budget. Roughly one third may relocate to clear neighboring cells around static obstacles inside polans. Stable hash ordering and minimum two-cell distance between relocated details; same layout after rebuild.
- Paths retain one-cell neighbor clearance; occupied cells excluded. Adjacent static obstacle bases may now host foliage, but unrelated occupied neighbors remain excluded. Long fallen logs revert to compact roots beside obstacles to avoid visual overlap with trunks. Native32PPU and colliders unchanged.
- No new art/assets, renderers or per-frame updates. Candidate selection happens during existing decoration rebuild; no measured FPS result. Live appearance pending.
- Verification: out/forest-interior-undergrowth.xml32/32 forest/import tests PASS. Extended regression checks nonzero interior placement, identical detail count before/after and deterministic repeat, alongside existing navigation/pixel density tests. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary.


## Forest camp bedroll — READY_FOR_SMOKE (2026-10-12)
- Added one torn bedroll to the abandoned camp beside cold campfire and broken crate. Camp-only required authored anchor; no movement/LoS blocking, destruction, loot or interaction.
- Built-in imagegen source preserved; Aseprite production derivative 40×48 at native 32 PPU, scale 1. Prompt/provenance: FOREST_BEDROLL_ART.md.
- Ground sorting uses the existing flat-obstacle presentation band, below actors. Scale preview inspected beside Jerzy; updated saved ForestScalePreview scene and material. No new per-frame effects or FPS claim.
- Verification: out/forest-bedroll-verified.xml 33/33 forest/import tests PASS, including camp-only placement, native scale, disabled colliders, ground sorting flag and ten-seed reachability. Scale render inspected beside Jerzy; targeted diff check PASS. Live forest visual smoke remains pending.
- Generate a fresh forest instance to see new authored camp placement. No changes to existing saved instances.


## Forest base undergrowth — READY_FOR_SMOKE (2026-10-12)
- Existing detail slots beside static obstacles now select compact plants: fungi/ferns in wet patches, roots/low shrubs in dry patches. Boundary slots retain the existing wider assortment.
- Centers pulled 0.18 world-cell widths toward neighboring bases, capped to 0.3 widths from their own cell center. Fixed neighbor iteration preserves deterministic layouts. Scale remains native 32 PPU; no new sprites, colliders or renderer budget. Runs during existing decoration rebuild, no per-frame effect.
- out/forest-base-undergrowth.xml: 33/33 forest/import tests PASS. Extended existing regression checks compact species near bases, proximity and unchanged native scale, with existing count/determinism/path clearance and ten-seed reachability checks. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary. Live visual smoke and FPS measurement not performed.


## Forest moisture ground cover — READY_FOR_SMOKE (2026-10-12)
- Grass sprites now omit trodden visual path cells. Existing low-frequency clustering remains; the shared soil/undergrowth moisture field controls density and tint. Dry patches expose more litter and use muted ochre, wet patches use subdued green.
- Dry cluster threshold 0.48, wet 0.36 (previous universal 0.36), so no extra candidate slots or renderers. Native 32 PPU, scale 1, no new assets, shader layers, colliders or per-frame effects. Calculated during existing decoration rebuild.
- Verification: out/forest-moisture-cover.xml 33/33 forest/import tests PASS. Extended ground-cover regression checks nonzero sparse grass, no grass on path cells and retained alpha range, alongside existing determinism, occupancy, native scale and ten-seed reachability checks. Targeted diff check PASS.
- Re-enter forest to rebuild visuals; fresh seed unnecessary. Live visual smoke and FPS measurement pending.


## Forest rotten stump variant — READY_FOR_SMOKE (2026-10-12)
- Added alternate hollow rotten stump with split rim and moss to the existing stump definition. Base and alternate selected evenly by stable position; tree-specific weighted rarity remains unchanged. Missing prop variant falls back to its base sprite.
- Built-in imagegen original preserved; Aseprite 40×40 production sprite, Point, native 32 PPU/scale 1. Prompt/provenance FOREST_ROTTEN_STUMP_ART.md. Same 0.22 collider radius, tactical footprint, LoS flags and renderer count.
- Saved ForestScalePreview includes alternate beside Jerzy; render inspected and copied to out/forest-scale-preview.png. Synced only owned stump definition and preview scene/material from validation mirror.
- out/forest-rotten-stump.xml: 34/34 forest/import tests PASS. New test checks both appearances across twelve positions, repeat binding, native scale, texture density and identical collider width. Existing path/placement regression suite passes. Targeted diff check PASS. Live forest visual smoke/FPS measurement not performed.
- Re-enter forest to rebuild views; no fresh seed required.


## Forest fractured boulder variant — READY_FOR_SMOKE (2026-10-12)
- Added lower, flat cracked mossy boulder as an alternate on the existing boulder definition. Stable equal base/alternate choice uses the existing prop selector. No additional obstacles or renderers; radius 0.32, footprint and LoS blocking unchanged.
- Built-in imagegen original preserved; Aseprite production 48×36, Point, native 32 PPU/world scale 1. Ground pivot fitted to alpha base. Prompt/provenance FOREST_FRACTURED_BOULDER_ART.md.
- Updated saved ForestScalePreview and inspected comparison beside Jerzy and original boulder. Synced only owned boulder definition and preview scene/material from validation mirror.
- out/forest-fractured-boulder.xml: 35/35 forest/import tests PASS. Existing variant regression parameterized for stump and boulder, checking both appearances, repeat binding, source dimensions, native scale and identical collision. Targeted diff check PASS.
- Re-enter forest to rebuild views; no fresh seed needed. Live forest appearance and FPS measurement pending.


## Forest dry shrub — READY_FOR_SMOKE (2026-10-12)
- Added low dry thorn shrub as optional DryShrub vegetation-profile sprite. Roughly half of low shrub selections in dry moisture patches use it with muted brown tint; wet patches retain green shrubs. Existing slot budget, path clearance and collider-free ground sorting retained. Missing new sprite falls back to previous green shrub behavior.
- Built-in imagegen original preserved; Aseprite production 40×24, Point, native 32 PPU/scale 1. Prompt/provenance FOREST_DRY_SHRUB_ART.md. No additional renderers or per-frame effects.
- Updated saved ForestScalePreview and inspected new shrub beside Jerzy; synced only owned vegetation profile and preview scene/material from validation mirror.
- out/forest-dry-shrub.xml: 35/35 forest/import tests PASS. Extended existing ground-cover regression checks authored sprite dimensions/PPU; existing count, deterministic layout, path/occupancy clearance, no-collider and native renderer scale checks pass. Targeted diff check PASS.
- Re-enter forest to rebuild decoration; fresh seed unnecessary. Live visual smoke and FPS measurement pending.


## Forest ten-variant visual pass — READY_FOR_SMOKE (2026-10-12)
- Completed ten new appearances in one continuous pass: shelf fungi, broad fern, tangled roots, forked branch, hollow fallen log, angular small stone, green bramble, sedge grass, charred stump and lichen boulder.
- Eight optional ground-detail variant arrays preserve old profiles through empty/null fallback; independently mixed stable hash selects base or alternate without changing slot selection, renderer budget, moisture zones, path clearance or native scale. Dry shrub retains its own dry-patch selection.
- Stump/boulder now each have two alternates plus base; existing stable prop selector, circular radii 0.22/0.32, footprint and LoS flags unchanged. No new objects or per-frame effects; crown fading remains disabled.
- Built-in imagegen originals retained. Aseprite crop/resize pipeline removes excess transparent framing while preserving aspect ratio, produces 24–64px-wide sprites at Point/native 32 PPU/world scale 1. Exact ten prompts, crops, dimensions and provenance: FOREST_TEN_VARIANTS_ART.md.
- Created saved ForestTenVariantsPreview scene and menu Godmist/Content/Pass 1b/Forest Ten Variants Preview; render out/forest-ten-variants-preview.png inspected with Jerzy beside every new sprite. Only owned profile, stump/boulder definitions and new preview scene/materials synced from validation mirror. Verified 10/10 importer GUIDs referenced by shipped assets.
- out/forest-ten-variants.xml: 36/36 forest/import tests PASS. New checks cover all eight detail bindings/dimensions/Point/32PPU, selectable base/alternates and null/empty/missing-entry fallback. Actual map rebuild uses new variants. Extended prop checks exercise all three appearances at 36 positions with identical native scale, collider width and repeat binding; existing occupancy, paths, deterministic budget and ten-seed reachability pass. Targeted diff check PASS.
- Re-enter forest to rebuild presentation; no fresh seed required. Live forest visual smoke and FPS measurement not performed.


## Forest moisture detail palette — READY_FOR_SMOKE (2026-10-12)
- Existing shrubs, ferns, fungi, roots, logs and stones now blend between muted dry ochre/brown and their cool wet palette using the same broad moisture field as soil/ground cover. Thorn shrubs retain their brown appearance. No per-sprite random colour noise.
- Rebuild-only colour calculation; unchanged native 32 PPU, scale 1, placement, paths, collision and renderer count. No new serialized assets or save changes.
- Verification: out/forest-moisture-details.xml 36/36 forest/import tests PASS; targeted diff check and final self-review PASS. Live visual appearance and FPS measurement not performed.
- Re-enter forest to rebuild presentation; no new seed required. Smoke: compare undergrowth against adjacent wet/dry ground and check path readability.


## Forest upright plant grounding — READY_FOR_SMOKE (2026-10-12)
- Shrubs, ferns and fungi now align sprite bottom bounds to a shared local ground baseline (slot centre minus 0.25 cell width). Their height no longer changes where the base is placed. Flat wood/roots/stones retain centre alignment; native 32 PPU, scale 1, sprite variants and moisture palette retained.
- Existing slot selection, collider-free decorations, paths and renderer count unchanged. Rebuild-only positioning; no per-frame updates, assets or save changes.
- Verification: out/forest-plant-baseline.xml 36/36 forest/import tests PASS; targeted diff check and self-review PASS. Live visual verification/FPS not performed.
- Re-enter forest; no fresh seed required. Smoke: compare plant bases beside trunks and check path readability.


## Forest glade compositions — READY_FOR_SMOKE (2026-10-12)
- Roots glade now authors two stumps; hollow glade two mossy boulders. Same two-prop budget. Existing abandoned camp and ruins retain their unique sets. Other glades keep mixed props.
- Authoring selects a first focus with room for another prop where possible, then places subsequent props near that fixed focus. Existing centre-cross, difficult-terrain, tree spacing and 3x3 footprint clearance retained. Updated only four room assets: roots, hollow, long_glade and overgrown_ruins.
- Existing interior detail slots prefer one stable obstacle focus per room; same relocation limit and renderer budget. Broad patches share dominant fungi/fern or roots/deadwood/log species, with occasional accents; moisture tint, native 32 PPU/scale 1, path exclusions and collider-free decorations retained. Rebuild-only work; no crown fading or per-frame work added.
- Added menu Godmist/Content/Pass 1b/Forest Composition Audit: renders four authored room layouts through actual map decoration builder, plus native prop sprites. Temporary audit objects/materials cleaned up. Unlit placement audit deliberately excludes gameplay fog, ground shader and lighting; it is not a playable preview or FPS measurement.
- Verification: out/forest-composition-verified.xml 36/36 forest/import tests PASS. Expanded existing multi-seed test checks exact themed prop count, definitions and pair spacing; ten-seed reachability and deterministic render budget regressions pass. All four placement renders inspected. Targeted diff check and final self-review PASS.
- A newly generated forest instance is needed for changed authored placements. Existing instances receive presentation/species changes on re-entry. Live camera/fog/light appearance and FPS remain manual smoke: enter a new forest, inspect roots/hollow/ruins/camp and traversal around props.


## 2026-10-12 — pierwszy przeciwnik Lasu: Wściekły Wilk

Dodano definicję wilka, skalowanie statystyk, trzy umiejętności (ugryzienie z krwawieniem, doskok ze wzrostem szybkości, leczenie od średniego ataku), encounter dwóch wilków i punkty patrolowe w pokojach normalnych oraz opcjonalnych. Korytarze wykluczono z losowych encounterów. Kieł wilka wypada pojedynczo z szansą 10%. Las nadal nie jest dostępny w ofertach.

Grafiki, animacje i ikony pozostawiono do podłączenia przez osobny czat. Pozostałych sześciu przeciwników, pełny lootbag Lasu i bossa nie obejmuje ten etap. Autorowanie: menu `Godmist/Content/Pass 1b/Configure Forest Wolf Gameplay`; narzędzie zachowuje pola prezentacji. Spawny wymagają wygenerowania nowego lochu.

Walidacja w izolowanej kopii Unity: 62/62 testy EditMode (ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, CombatRecoverySystemTests), w tym 10 seedów generatora. Do sprawdzenia w PlayMode: tempo walki, zachowanie patrolu i prezentacja po dostarczeniu grafik. Nie wykonano pomiaru FPS.


## 2026-10-12 — Bandyta Rzezimieszek — READY_FOR_SMOKE

Drugi przeciwnik Lasu: statystyki i skalowanie z content_pass_1b.xlsx (Przeciwnicy, A4:V4), Bandycki Nóż (krwawienie 25%, 75% ataku przez 3 tury po trafieniu), Zasadzka (niewidzialność i +10 pp. krytyka przez 2 tury), Cios w Plecy (+50% obrażeń od tyłu). Koszty/celność według X16:AA16. Dodano parametr efektu rearAttackDamageBonus (domyślnie zero), uwzględniany przez wykonanie, ocenę AI i tooltip. Zachowano istniejącą regułę znikania niewidzialności po ofensywnym skillu.

Encounter dwóch bandytów losuje się obok par wilków, korzystając z istniejących dwóch punktów Enemy na polanach. Grafiki, animacje, ikony i ich pola nie były ustawiane. Nie dodano zastępczych dropów; pełny leśny lootbag jest nadal osobnym etapem. Las pozostaje wyłączony z ofert.

Autorowanie: Godmist/Content/Pass 1b/Configure Forest Bandit Gameplay. Wymagany nowy loch dla nowych encounterów. 178/178 testów EditMode w izolowanej kopii Unity: ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, ScoutContentTests, SkillExecutionSystemTests i SimpleCombatAi. Sprawdzono premię od tyłu, ujawnienie po ataku, koszt i brak zasobu, pominięcie krwawienia po pudle, zachowanie AI oraz 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode i FPS nie sprawdzane; smoke: patrole, walka z bandytami i prezentacja po podłączeniu artu. Pozostało pięciu zwykłych przeciwników, pełny lootbag i boss.


## 2026-10-12 — Bandyta Strzelec — READY_FOR_SMOKE

Dodano trzeciego zwykłego przeciwnika Lasu według content_pass_1b.xlsx (Przeciwnicy A5:V5, X17:AA17): Wystrzał z Kuszy (0 MP/65 AP, zasięg 9, 20% -7 szybkości przez 2 tury po trafieniu), Bełt Przebijający (25 MP/65 AP, 70% obrażeń, kierunkowy pas 2×8, 80% krwawienia 90% ataku przez 2 tury po trafieniu), Flara (50 MP/35 AP, 70% -15 celności przez 2 tury i 30% ogłuszenia na 1 turę wrogów, usunięcie ogłuszenia sojuszników). AI wycenia zdjęcie istniejącego statusu z sojusznika. Dodano encounter mieszany strzelec + rzezimieszek, wspólne punkty patrolowe na polanach; pozostałe encountery zachowane.

Grafiki, animacje i ikony pozostawiono osobnemu czatowi; pól prezentacji nie nadpisywano. Nie dodano zastępczego lootu. Las nadal niedostępny z ofert. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Shooter Gameplay. Wymagana nowa generacja lochu.

Walidacja w izolowanej kopii Unity: 132/132 testy EditMode (ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi), w tym trafianie kilku celów bez sojusznika i celu poza kierunkiem, flara rozdzielająca strony, AI ratujące ogłuszonego sojusznika, AI strzelające bez podejścia oraz 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode/FPS nie mierzono. Smoke: nowy Las, patrol mieszany, LoS i prezentacja po dostarczeniu artu. Pozostało czterech zwykłych przeciwników, pełny lootbag i boss.


## 2026-10-12 — Elfi Łucznik — READY_FOR_SMOKE

Dodano czwartego zwykłego przeciwnika Lasu: statystyki/skaling według content_pass_1b.xlsx (Przeciwnicy A6:V6), Elficka Strzała (0 MP/60 AP, celność 98, zasięg 9), Deszcz Strzał (30 MP/70 AP, celność 77, 60% obrażeń na kwadrat 3×3 w zasięgu 7, 50% szansy na -8% uniku przez 3 tury po trafieniu), Sokole Oko (50 MP/30 AP, przez 3 tury ataki nie chybiają i +5% obrażeń za pole odległości). Odległość liczona jako maksimum różnic osi, zgodnie z kwadratowym zasięgiem. Dodano UniqueBuffType.Hawkeye na końcu enum (9), zachowując wcześniejsze wartości. Istniejący payload i lifecycle obsługują zapis/czas działania bez nowego formatu save. Premia działa na obrażenia fizyczne/magiczne, nie na DoT; zgodne wykonanie, podgląd i ocena obrażeń AI. AI wycenia buff, gdy nie jest aktywny.

Nowy encounter dwóch łuczników i 15% dropu 1–2 uszu elfa (alchemia, pospolity, wartość 17 według Spis innych przedmiotów.xlsx A319:H319). Pozostałe encountery zachowane, oferta Lasu nadal wyłączona. Grafiki, animacje i ikony pozostawiono osobnemu czatowi. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Archer Gameplay; potrzebna nowa generacja lochu.

163/163 testy EditMode w izolowanej kopii Unity: ForestArcherTests, ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi, CombatSkillPreviewCalculatorTests, CombatDamageSystemTests i GameSessionSave. Sprawdzono realny atak i podgląd przy dystansie 1/5/9, gwarantowane trafienie, wygaśnięcie po 3 turach, brak premii DoT, kwadrat 3×3 bez sojusznika, przygotowanie buffa przez AI, brak ponownego nakładania oraz 10 seedów generatora. Ustabilizowano ziarno losowania istniejącego testu trafień Bełtu Przebijającego. Self-review i targeted diff check zakończone. PlayMode/FPS nie weryfikowane. Pozostało trzech zwykłych przeciwników (drzewo, kłusownik, druid), pełny lootbag i boss.


## 2026-10-12 — Elfi Kłusownik — READY_FOR_SMOKE

Piąty zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A7:V7, X19:AA19): Sztylet z Zębami (0 MP/60 AP, celność 69, 40% krwawienia 60% ataku przez 2 tury po trafieniu), Pułapka na Niedźwiedzie (25 MP/30 AP, zasięg 1, po wejściu wroga unieruchomienie przez 2 tury oraz 70% krwawienia 50% ataku przez 3 tury), Worek z Trutką (55 MP/70 AP, zasięg 3, obszar 2×2, 80% zatrucia 60% ataku przez 3 tury). Drop wspólnego LT_Forest_Elf: 15% na 1–2 uszu elfa. Nowy encounter kłusownik + łucznik.

Dodano opt-in TriggersOnEntry w SummonedObjectDefinition (domyślnie false). Takie obiekty nie blokują ruchu i nie dostają własnej tury; pozostają do aktywacji, zniszczenia lub końca walki. Osobny nieserializowany callback CombatSession współistnieje z powiadomieniami pułapek lochu i run bossa. Wejście przeciwnika uruchamia efekty tylko na nim i usuwa obiekt; sojusznicy nie aktywują. Podstawowy ruch zatrzymuje ścieżkę po unieruchomieniu; istniejące powiadomienie wejścia obsługuje również przemieszczenia. Umieszczanie pod jednostką zabronione. Przyjęto limit jednej aktywnej pułapki na kłusownika; AI stawia ją na wolnym sąsiednim polu w kierunku widocznego przeciwnika i nie zastępuje aktywnej własnej pułapki.

Poprawiono Deszcz Strzał łucznika: relativeRange 0–2 oznacza bok 3 i pełne 3×3 (wcześniej 0–1 dawało 2×2). Regresja sprawdza obie przeciwległe krawędzie. Grafiki, animacje i ikony pozostawiono osobnemu czatowi. Las nadal wyłączony z ofert. Autorowanie: Godmist/Content/Pass 1b/Configure Forest Poacher Gameplay. Nowe encountery wymagają nowego lochu.

152/152 testy EditMode w izolowanej kopii Unity: ForestPoacherTests, ForestArcherTests, ForestShooterTests, ForestBanditTests, ForestWolfTests, ForestPrototypeTests, ForestImportedAssetsTests, SimpleCombatAi, SummonedObject, CombatMovement, CombatCharge, CombatTeleport i GameSessionSave. Pokrycie: przerwanie ścieżki na pułapce, jednorazowe zużycie, przepuszczenie sojusznika, brak automatycznych efektów tury, odrzucenie zajętego/zbyt dalekiego pola bez pobrania kosztu, obszar 2×2, AI i 10 seedów generatora. Self-review i targeted diff check zakończone. PlayMode/FPS nie weryfikowane; smoke: nowy Las, mieszany patrol elfów, stawianie i aktywowanie pułapki, prezentacja po dostarczeniu artu. Pozostało dwóch zwykłych przeciwników (drzewo, druid), pełny lootbag i boss.


## 2026-10-12 — Zarażone Drzewo — READY_FOR_SMOKE

Szósty zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A2:V2, X14:AA14). HP 450, atak 30–35, szybkość 30, tag Nieumarły; wzrost i odporności z arkusza. Ciemne Korzenie: 5 MP/70 AP, magiczne obrażenia, zasięg 5, 35% zatrucia (55% ataku, 3 tury). Zniewalające Korzenie: 45 MP/65 AP, zasięg 5, unieruchomienie 2 tury oraz 70% na -12% uniku przez 3 tury. Rozwój Plagi: 25 MP/30 AP, celność 88, fizyczne obrażenia x0.6 wszystkim zatrutym przeciwnikom w zasięgu 9, przedłużenie każdego aktywnego zatrucia o 1 turę. Nowy encounter dwóch drzew, bez indywidualnego dropu zgodnie z arkuszem; pełny lootbag pozostaje osobnym zadaniem.

Dodano opcjonalny RequiredStatusId w trybie targetowania (domyślnie pusty), filtr wspólny dla AI/targetowania oraz walidację wykonania. ExtendStatusDuration dopisany na końcu enumu (42); przedłuża istniejące aktywne efekty czasowe bez zmiany obrażeń, źródeł i stosów oraz bez wskrzeszania wygasłych efektów. AI uwzględnia wartość przedłużenia zatrucia. Grafiki, animacje i ikony pozostawiono osobnemu czatowi, istniejących pól prezentacji nie nadpisano. Las nadal wyłączony z ofert.

183/183 testy EditMode w izolowanej kopii Unity (out/forest-tree-final.xml): ForestTree/Poacher/Archer/Shooter/Bandit/Wolf, ForestPrototype/ImportedAssets, SimpleCombatAi, CombatTargeting, StatusEffect, GameSessionSave. Sprawdzono koszty, dane, selekcję zatrutych, pomijanie zdrowych/sojuszników/zbyt dalekich celów, brak kosztu przy błędnym celu, przedłużanie stosów, brak przywracania wygasłej trucizny, dwie tury unieruchomienia i użycie Plagi przez AI bez podchodzenia. Autorowanie zakończone kodem 0, self-review i targeted diff check wykonane. Smoke: świeżo wygenerowany Las, encounter drzew, korzenie i Plaga; PlayMode/FPS nie weryfikowane. Menu: Godmist/Content/Pass 1b/Configure Forest Tree Gameplay. Pozostał Elfi Druid, pełny lootbag i boss.


## 2026-10-12 — Elfi Druid — READY_FOR_SMOKE

Siódmy zwykły przeciwnik Lasu według content_pass_1b.xlsx (Przeciwnicy A8:V8, X20:AC20): HP 200, atak 20–30, szybkość 42, wzrost i odporności z arkusza. Gniew Natury: 5 MP/50 AP, zasięg 6, magiczne obrażenia x0.5 za każdą własną żywą narośl; bez narośli nie można wykonać i nie pobiera kosztu. Trująca Narośl: 40 MP/50 AP, zasięg 6, 15% maks. HP rzucającego, co własną turę 80% zatrucia 60% ataku przez 3 tury przeciwnikom wokół. Łagodzące Korzenie: 30 MP/50 AP, zasięg 6, narośl z 15% maks. HP, co własną turę leczenie pobliskich sojuszników za 90% średniego ataku. Przyjęto „wokół” jako osiem sąsiednich pól (zasięg 1); pełny koszt AP pulsu pozwala na jedną aktywację w turze. Brak czasu życia i sztucznego limitu liczby narośli; pozostają do zniszczenia, usunięcia przywołań właściciela lub końca walki.

Narośle używają istniejącego systemu przywołanych jednostek: są nieruchome, sterowane przez AI, mają normalne HP i można je normalnie atakować. Opt-in pola SkillEffectData dla trwałości, nieruchomości, dziedziczenia HP/ataku/szybkości i grupy przywołań mają domyślnie wyłączone działanie. Gniew Natury liczy grupę forest.growth wyłącznie właściciela, pomija zniszczone narośle i zachowuje zgodność realnych obrażeń, AI i preview. Statystyki narośli kopiowane są w chwili przywołania. AI druida wybiera wolne pole obok widocznych przeciwników lub rannych sojuszników; AI narośli nie szuka ścieżki, gdy nie ma celu. Drop LT_Forest_Elf: 15% na 1–2 uszu elfa. Nowy encounter druid + kłusownik. Dotychczasowe assety prezentacji nie nadpisane; sprite'y/animacje/ikony nadal wykonuje osobny czat. Las nadal wyłączony z ofert.

212/212 testy EditMode w izolowanej kopii Unity (out/forest-druid-final.xml): ForestDruid/Tree/Poacher/Archer/Shooter/Bandit/Wolf, ForestPrototype/ImportedAssets, SimpleCombatAi, CombatTargeting, StatusEffect, Summon, CombatMovement, CombatSkillPreviewCalculator, GameSessionSave. Sprawdzono dane i koszty, 15% HP i jego zachowanie po odświeżeniu statystyk, brak ruchu/wygaśnięcia, normalne niszczenie narośli, liczenie własnych żywych narośli i zgodność preview z obrażeniami, truciznę/leczenie tylko właściwej strony w zasięgu 1, użyteczne stawianie przez AI i Gniew Natury po przywołaniu, bezczynność nieruchomej narośli bez celu, odrzucenie zajętego/zbyt dalekiego pola bez kosztów. Poprawiono zaokrąglenie HP (400 x 15% = 60). Autorowanie zakończone kodem 0, self-review i targeted diff check wykonane. Menu: Godmist/Content/Pass 1b/Configure Forest Druid Gameplay. Smoke wymaga świeżego Lasu; sprawdzić mieszany patrol, przywołania i ich tury, niszczenie narośli oraz prezentację po podłączeniu artu. PlayMode/FPS nie weryfikowane. Zwykli przeciwnicy passa 1b są gotowi; pozostaje pełny lootbag i boss.


## 2026-10-12 — Loot Lasu — READY_FOR_SMOKE

Podłączono cztery torby Lasu (zasoby, broń, pancerze, galduryty), osobny DLC_Forest_Loot oraz tabele przeciwników i skrzyń dla poziomów 1–10 i 11–20. Źródło: content_pass_1b.xlsx / Lootbagi D4:E32. Trzy pule surowców 100/70/30% wybierają różne materiały; szyszki 100% + niezależne 40%. Żywicę usunięto z puli zgodnie z korektą użytkownika: stary przedmiot już nie dropi. Ostatnia pula daje 40% na 1–2 uszu elfa. Indywidualne dropy wilków/elfów zachowane. Nowe czerwony/żółty pył energetyczny: ceny 13/74 i rzadkość pospolita/niepospolita według Spis innych przedmiotów.xlsx A307:H308; istniejąca Szyszka i jej ikona zachowane. Brak zmian grafiki/ikon.

Sprzęt korzysta ze wspólnego generatora i części tierów 5/15, z jakością Słaba/Zwykła/Doskonała; finalny wymagany poziom zachowuje dotychczasową korektę jakości (-3/0/+3). Galduryty leśnej torby generują się na poziomie 20: nowe opcjonalne FixedGalduryteLevel w LootBagPoolEntryData (0 domyślnie) zachowuje działanie istniejących tabel i save'ów. Szanse wypadania toreb przyjęto jak w istniejącym lochu: przeciwnik 50% zasoby, 2% broń, 2% pancerz, na 11–20 dodatkowo 1% galduryty; skrzynie 2–3 torby zasobów i 4/4/2% pozostałe torby. ForestEnemiesSetup ponownie podłącza leśny katalog po reautorowaniu prototypu. Boss references w katalogu zachowywane; torba bossa i unikaty pozostają etapem implementacji Enta. Las nadal wyłączony z ofert.

137/137 testów EditMode w izolowanej kopii Unity (out/forest-loot-verified.xml): ForestLootTests, LootTableSystemTests, LootCollectionServiceTests, ForestPrototypeTests, ForestImportedAssetsTests, DungeonEncounterReward, Galduryte i GameSessionSave. Kontrola tabel i referencji katalogu, 800 rzeczywistych losowań torby zasobów na granicach 1/10/11/20, zakresy ilości i różne surowce, szyszki, brak żywicy, kompletne 3 części i jakość sprzętu tieru 5/15, rzeczywiste galduryty poziomu 20 z efektami. Autorowanie kod 0; self-review i targeted diff check zakończone. Menu: Godmist/Content/Pass 1b/Configure Forest Loot. Smoke: świeżo wygenerowany Las, nagrody po walce i otwieranie toreb, skrzynie po dodaniu ich kontentu; PlayMode nie sprawdzano. Prerolowane nagrody starego lochu nie zmieniają się. Pozostał Zarażony Ent 2.0, jego torba i unikaty.

## 2026-10-12 — Zarażony Ent 2.0 — READY_FOR_SMOKE

Podłączono Enta jako bossa ostatniego pokoju Lasu, dwa kompletne zestawy umiejętności i przemianę przy <=50% HP po zakończeniu całego skilla (łącznie z kontratakami/powtórzeniami). Przemiana zmienia definicję, aktywny zestaw skilli i prezentację, usuwa własną Twardą Korę, zachowuje debuffy i nie odnawia HP/MP/PA. Korzenie: unieruchomienie i osłabienie wszystkich odporności na 2 tury; Zapłon: linia 6 pól. Twarda Kora: -40 szybkości. Demoniczna Zaraza: limit 5, raz na użycie skilla na cel; obrażenia okresowe/teren raz na turę celu; 0,5% maks. HP i 0,5 MP dla Enta za stos, do końca walki. Sanktuarium: połowa dostępnych pól areny, ostrzeżenie do następnej tury Enta, potem ogień przez 3 jego tury; zachowane osiągalne schronienia, brak ponownego rzucenia podczas ostrzeżenia/ognia, obrażenia terenu najwyżej raz na turę celu. Stan fazy, zarazy i terenu przeżywa łączenie walk; bez osobnej tury dla każdego pola ognia i bez przeliczania co klatkę.

Boss dropi własną torbę, z czterema unikatami poziomów 10/20: Praelficka Glewia Wojenna, Liściasta Tunika, Palec Enta, Elficki Młot Życia. Podłączono ich pasywki, Żywioł lasu, zasoby/galduryty i tabele obu przedziałów poziomów. Zarażona żywica pozostaje wycofana. Menu odtwarzania danych: Godmist/Content/Pass 1b/Configure Forest Ent Gameplay. Las nadal wyłączony z normalnych ofert.

Weryfikacja: 245/245 testów EditMode w izolowanej kopii Unity, out/forest-ent-verified.xml. Obejmuje pełne generowanie i placement Lasu, boss loot i rzeczywiste otwieranie toreb, cztery pasywki, dwie fazy i aktywny loadout AI, linię Zapłonu (6. pole trafione, 7. i boczne pominięte), koszty, zarazę, ostrzeżenie/ogień/schronienia, śmierć bossa, sprzątanie i łączenie walk, istniejących bossów katakumb, wykonanie skilli, status UI, loot oraz save. Final self-review i targeted git diff --check zakończone. PlayMode/FPS nie weryfikowane.

Smoke: wygenerować świeży Las, dotrzeć do końcowej areny, przekroczyć 50% HP, sprawdzić skille II fazy, ostrzeżenie i trzy tury ognia, zarazę oraz nagrodę po zabiciu. Stare instancje lochu/prerolowany loot nie aktualizują się. Art wykonuje osobny czat: CD_Forest_InfectedEnt oraz CD_Forest_InfectedEntBurning, sześć ikon skilli i cztery ikony unikatów. Opcjonalne TransformationFrames na definicji I fazy odtwarzane są jednokrotnie przy przemianie (8 FPS). Istniejących pól prezentacji nie nadpisano; nowe definicje wymagają podłączenia artu. Ostrzeżenie i ogień mają tymczasowe znaczniki 32x32/32 PPU. Podpalanie drzew otaczających arenę pozostaje osobnym etapem wizualnym.
## 2026-10-12 — Integracja zapisu Lasu — READY_FOR_SMOKE

Użytkownik potwierdził działanie Lasu w Play Mode; zgłoszony osobno brak prezentacji ciał obsłużono fallbackiem idle. Dodano integracyjny test rzeczywistego wygenerowanego Lasu dla poziomów 10 i 20: pokonany Ent, wyczyszczony encounter, flaga wygenerowania zwłok, pozycja i złoto ciała, ID torby bossa i dokładne ID jej zawartości zachowują się przez dwa kolejne cykle snapshot -> JSON -> restore z głównym katalogiem. Bez duplikowania ciał lub ponownego losowania zawartości. 77/77 EditMode PASS: ForestEntBossTests, DungeonEnemyBodyTests, DungeonOfferTests (out/forest-integration.xml). Nie wykryto błędu wymagającego zmiany runtime; zmieniono tylko testy i dokumentację. Self-review oraz targeted diff check zakończone. Nie włączono ofert Lasu; finalny art i podpalanie drzew areny pozostają kolejnymi etapami. Nie wykonano nowego pomiaru FPS.
## 2026-10-12 — Zielony ogień na arenie Enta — READY_FOR_SMOKE

Przemiana Enta rejestruje jego pierwotny pokój areny (przed przemieszczeniem do innych pomieszczeń); identyfikator przechodzi razem ze stanem bossa przez merge walk. DungeonCombatCoordinator odświeża stan prezentacji co 0,2 s. Po rozpoczęciu II fazy dwa najbliższe pasy drzew granicznych oraz indywidualne pnie przy arenie otrzymują zielony ogień; inne pokoje i inne piętra/mapy są pomijane. Animacja shaderowa jest skwantowana do 5 FPS, w skali 32 PPU. Jeden wspólny materiał, nakładki tworzone tylko przy pierwszym zapłonie, bez skanowania wszystkich drzew co klatkę. Nakładki należą do tych samych właścicieli fog/elevation co drzewa; nie ujawniają niezbadanych obszarów. Wykluczone z odbiorników oświetlenia i cieni kontaktowych; brak nowych Light2D/particle systems, colliderów, obrażeń i zmian nawigacji. Obrażenia nadal należą do Sanktuarium. Efekt gaśnie po zakończeniu/usunięciu walki; przebudowa mapy odtwarza go z aktywnego stanu II fazy. Nie jest trwałym spaleniem geometrii zapisywanym po ekspedycji. Shader ładowany z Resources/Godmist/EntArenaFire, aby był dostępny także w buildzie.

97/97 EditMode PASS w izolowanej kopii Unity: EntArenaFireTests, ForestEntBossTests, ForestPrototypeTests i DungeonLighting (out/ent-arena-fire.xml). Sprawdzono przejście fazy, zachowanie pierwotnego pokoju, ograniczenie do areny, brak zmian liczby colliderów/casterów i rewizji nawigacji, brak duplikatów, ukrywanie przez fog, zakończenie walki, ponowną aktywację oraz rebuild. Shader bez błędów kompilacji; render kontrolny obejrzany (out/ent-arena-fire-preview.png). Przy okazji rebuild w EditMode używa DestroyImmediate dla starych ścian zamiast niepoprawnego Destroy. Self-review i targeted diff check zakończone. Nie zmieniano artu mobków/ikon ani assetów drugiego czatu. Smoke: w świeżym lub istniejącym Lasie doprowadzić Enta do II fazy, obejrzeć zielony ogień na granicy areny i jego wygaszenie po walce. Nowego pomiaru FPS ani PlayMode nie wykonano. Ten wpis zastępuje wcześniejszy status podpalania drzew jako niezrealizowanego etapu.

## 2026-10-12 — Las: oferty i Danger Level — READY_FOR_SMOKE

Podłączono do GC_MainContentCatalog dwie produkcyjne definicje: dungeon.forest.expedition (zwykłe wyprawy, bez Enta, poziom względem drużyny) i dungeon.forest.boss_expedition (Zarażony Ent, poziomy 10/20, odblokowanie przez istniejący Danger Level). Obie korzystają z obecnego layoutu, prezentacji, przeciwników, obiektów i tabel łupu Lasu. Prototyp zachowuje ID i pozostaje poza tablicą ofert, więc stare zapisy nie wymagają migracji. Zwykła wyprawa Lasu obniża jego Danger o 30 i podnosi inne dostępne biomy o 10–20 od poziomu 8; ukończenie wyprawy Enta resetuje Danger Lasu do zera. Ponowne 100 odblokowuje nową wyprawę na Enta. Kamienie milowe 10/20 losują także Las.

Menu odtworzenia: Godmist/Content/Pass 1b/Configure Forest Expeditions. Ponowne autorowanie prototypu odświeża istniejące warianty, zachowując zwykłą wyprawę bez bossa. Bez zmiany formatu zapisu i bez nowych systemów runtime. Test starej kampanii poprawiono tak, aby weryfikował zapis faktycznie wylosowanego biomu zamiast zakładać wyłącznie Katakumby.

111/111 EditMode PASS w izolowanej kopii Unity: ForestExpeditionTests, DungeonOfferTests, ForestImportedAssetsTests, ForestPrototypeTests, ForestEntBossTests, ForestLootTests (out/forest-offers-final.xml). Zweryfikowano rzeczywiste assety katalogu, generowanie obu wariantów na poziomach 10/20, brak Enta w zwykłej wyprawie, ofertę przy 100 Danger, zapis/odczyt oferty z prawdziwą drużyną, reset i ponowne odblokowanie bossa, idempotencję autorowania. Self-review zakończony. PlayMode oraz nowy pomiar FPS: NOT RUN.

Smoke: nowa tablica ofert powinna zawierać Las obok Katakumb. Zachowane oferty starego zapisu pozostają do następnego odświeżenia po powrocie z wyprawy. W konsoli setdanger forest 100 powinno udostępnić osobną ofertę Enta; zwykła wyprawa nie ma bossa, a bossowa ma Enta na końcu. Sprawdzić nagrodę i reset Danger po ukończeniu oraz zachowanie oferty po save/load. Ręczne podpinanie w Inspectorze nie jest potrzebne. Ten wpis zastępuje wcześniejszy status Lasu jako wyłączonego z ofert.


## 2026-10-12 — Las: skrzynie i skrytki — READY_FOR_SMOKE

Dodano DO_Forest_Chest i DO_Forest_Cache do puli prototypu oraz obu produkcyjnych wypraw. Oba pojemniki używają istniejących tabel LT_Forest_Chest_1_10_Production / LT_Forest_Chest_11_20_Production poprzez DLC_Forest_Loot; dobór następuje według poziomu instancji lochu. Loot pozostaje zgodny z tabelami: zawartość 2–3 toreb zasobów oraz szanse na wyposażenie/galduryty, wydawane bezpośrednio zgodnie z RollChestLoot. Bez przywracania wycofanej żywicy. Pojemniki są odblokowane i korzystają z istniejącej prezentacji/interakcji skrzyni.

Jedna kotwa skrzyni na szablon zwykłej polany (35%, wspólna dystrybucja forest.chests); jedna skrytka w każdym bocznym pokoju Optional (100%). Bez pojemników na starcie, w korytarzach i na arenie Enta. Kotwy są przy obrzeżu, z dojściem, odsunięte od dekoracji i kotew przeciwników. Przeniesiono wyłącznie definicje pojemników, referencje pul i nowe kotwy; art mobków oraz ich prezentacja nie były modyfikowane. Menu: Godmist/Content/Pass 1b/Configure Forest Containers. Ponowne autorowanie prototypu odtwarza kotwy i odświeża wyprawy; loader prototypu uwzględnia nowe definicje.

65/65 EditMode PASS w izolowanej kopii Unity: ForestContainerTests, ForestExpeditionTests, ForestImportedAssetsTests, ForestPrototypeTests, ForestLootTests (out/forest-containers-verified.xml). Nowe testy obejmują 18 wygenerowanych lochów na poziomach 1/10/11/20, obie wyprawy, łup i wolne pole dojścia, idempotencję autorowania oraz dwa kolejne cykle JSON save/load zachowujące zawartość i opróżnione pojemniki. Istniejący test liczby drzew ograniczono do kotew przeszkód, aby nie traktował skrzyń jako drzew. Self-review i porównanie przeniesionych assetów z testowaną kopią zakończone. PlayMode: NOT RUN.

Smoke: wejść do świeżego Lasu, sprawdzić skrzynie na polanach i skrytki w bocznych pokojach, podejść i otworzyć E, odebrać łup, zapisać/wczytać i potwierdzić brak odnowienia zawartości. Dotychczas zapisany loch zachowuje istniejący zestaw obiektów; nowe kotwy dotyczą nowo wygenerowanych wypraw. Bez ręcznego podpinania w Inspectorze.


## 2026-10-13 — Grafiki Skażonego Lasu — READY_FOR_SMOKE

Podłączono 9 wariantów aktora (7 archetypów i obie fazy Enta): 576 klatek idle/chodu w 8 kierunkach, 144 osobne pozy hit/dodge, 9 leżących ciał, 6 klatek przemiany Enta (S, 1200 ms), 192 szczytowe pozy akcji i 200 osobnych warstw VFX. Istniejące CharacterAnimationResolver / CombatPoseResolver / CombatPosePlayer; bez nowego systemu animacji. Przemiana zachowuje trigger i próg HP, a jej tempo jest polem prezentacji definicji (Ent 5 FPS; pozostałe definicje domyślnie 8 FPS).

Ikony v5 podłączone do istniejących skilli, przedmiotów, materiałów, toreb i czterech unikatów; kompletna korekta trzech ikon spaczonego ognia v1 zastępuje ich stare wersje. Status Sokolego Oka używa ikony źródłowego skilla, Demoniczna Zaraza osobnej ikony statusu. Wycofana Zarażona żywica ma nieużywany import ikony; nie dodano jej do definicji materiałów ani lootu. Trująca i lecznicza narośl mają różne grafiki świata i VFX.

Import: 32 PPU, Point, bez mipmapów/kompresji; natywne canvasy i pivoty z manifestów. Runtime animacje/pozy korzystają z tekstur Default i Sprite.Create; osobne byte-identical kopie południowych idle/walk/hit służą wyłącznie serializowanym polom Sprite i portretom. Ciała mają pivot centralny. Zmieniono tylko pola prezentacji 54 assetów i trzy wpisy glossary; zachowano pozostałe serialized fields. Generator i wizualizacja lasu, mechaniki i balans bez zmian.

Prowizoryczne: poses-v4. Paczka poses-v5 pojawiła się podczas integracji i ma validation PASS, ale nie ma końcowego handoffu; VFX pozostają zarejestrowane do v4. Nie usunięto artystycznych różnic twarzy/detalu między pozami. Przemiana jest dostępna tylko dla S, reakcje i VFX są statycznymi szczytowymi klatkami, a nie pełnymi animacjami. Nie zastępują telegraphu Sanktuarium ani efektów płonących drzew areny.

Weryfikacja: automatyczny PlayMode w teście obejmuje faktyczne odtwarzanie przemiany i powrót do idle fazy 2 oraz warstwy VFX/pivot/ukrywanie. EditMode sprawdza Resources wszystkich kierunków, referencje Sprite/import profile, odrębne leczenie/truciznę, zgodność końców przemiany z idle i osobne ciała; wspólny zestaw obejmuje też Jerzego i katakumby. Kontaktowy podgląd skali względem Jerzego: out/forest-art-native-scale.png. Wizualnego smoke pełnej wyprawy w grze NIE wykonano.

Smoke w głównym projekcie: poczekać na import/kompilację i wejść do świeżej wyprawy Lasu z tablicy ofert. Obejrzeć kierunki idle/chodu, hit/dodge i efekty ataków; zabić przeciwników i sprawdzić osobne leżące ciała oraz ikony łupu/statusów. Dla Enta użyć konsoli `setdanger forest 100`, wybrać osobną ofertę bossową; zejść poniżej 50% HP po rozstrzygnięciu skilla, sprawdzić około 1,2 s przemiany, nowy model/skillset i żółtozielone VFX. Porównać rozmiar z drużyną, stopy, dłonie/bronie i kolejność efektów przód/tył. Bez ręcznego podpinania w Inspectorze. Powtarzalne autorowanie: Godmist/Content/Pass 1b/Configure Forest Art (poza Play).

Końcowa weryfikacja integracji: 116/116 PASS (EditMode + automatyczny EnterPlayMode), out/forest-art-integrated2.xml. Self-review zakończony; zgodność głównych assetów i GUID-ów z testowaną kopią potwierdzona. Bez commit/push.


## 2026-10-13 — Finalne grafiki finish-v1 — READY_FOR_SMOKE

Ten wpis zastępuje wcześniejszą prowizoryczność poses-v4/VFX-v1. Źródło: output/content-pass-1b-finish-v1/HANDOFF.md i manifest.json oraz finalny import prompt. Podłączono 192 pozy v5 i 200 zarejestrowanych warstw VFX v2, odświeżono 144 przeliczone reakcje hit/dodge, 9 ciał i 6 klatek przemiany. 576 idle/walk, zaakceptowane bazy, ikony v5, torba Enta v2 i trzy ikony spaczonego ognia były już poprawnie podłączone; ich native bytes porównano z finalnymi źródłami bez ponownego tworzenia mapowań. Żywica pozostaje wycofana (import ikony nie przywraca materiału/lootu).

Zachowano 32 PPU i większe canvasy póz/reakcji 96/128/192 oraz VFX 96/192/256 bez skalowania do idle. Rozszerzony branch-sweep NE/NW: aktor192 z pivotem44, VFX256 z pivotem76; actor_offset32 jest już uwzględniony przez różnicę pivotów, localPosition efektu pozostaje zerowe. Warstwy front/behind z manifestu. Wszystkie 200 hashy aktorów VFX odpowiada dokładnie poses-v5. Kompozytów nie zaimportowano; przejrzano źródłowy composite rozszerzonego zamachu. Pozy, reakcje i VFX pozostają klatkami kulminacyjnymi, nie pełnymi animacjami ataku.

Przemiana: istniejący próg 50% HP, zmiana definicji/skillsetu i zasady walki bez zmian. Oba widoki postaci wyłączają pozę kończącej się akcji przy zmianie definicji; przez przemianę nowe pozy nie nadpisują jej klatek. Dostępne wyłącznie S: odtwarzane jednokrotnie przez 6×200 ms, bez obrotu/lustrzanego odbicia, przy zachowaniu logicznego kierunku. Następnie wraca kierunkowe idle nowej fazy. Żaden pozostały kierunek przemiany nie jest udawany. Ciała korzystają z explicit DeadSprite, z Color.white i identity rotation; fallback żywego aktora nie jest wykonywany dla żadnego z 9 wariantów (potwierdzone testem widoku).

Weryfikacja: 149/149 PASS, out/forest-finish-integrated.xml. Zestaw: ForestArtIntegrationTests, CombatPoseIntegrationTests, ForestEntBossTests, ForestLootTests, CombatStatusPresentationTests, DeadKingV2Tests, ForestImportedAssetsTests, DungeonEnemyBodyTests. Obejmuje kompilację, wszystkie produkcyjne skille Lasu i kierunki, SHA native importów, zachowanie canvasów/pivotów, metadane, istniejące kierunkowe animacje Jerzego i katakumb, feedback/sekwencję akcji, osobne ciała i automatyczny EnterPlayMode: przemiana podczas aktywnego ataku, powrót do kierunkowego idle oraz VFX visibility/order. Główne pliki/metadata i selektywnie scalone pola wizualne 54 assetów są byte-equal z testowaną kopią. Self-review zakończony. Bez zmian generatora/wizualizacji lasu, balansu, źródeł output; bez commit/push.

Brakujących paczek dla tego importu nie ma. Pozostaje wizualny smoke pełnej wyprawy, którego nie wykonano przez Computer Use ani ręcznie: świeży Las z tablicy ofert, hit/dodge, przywrócenie idle, efekty łuczników/druida oraz osobne ciała i ikony łupu. `setdanger forest 100` → oferta Enta: przejść przez 50% HP, sprawdzić przemianę S i powrót do kierunku, NE/NW zamachu bez obcięcia oraz zielony ogień fazy 2. Ręczne podpinanie w Inspectorze niepotrzebne; poczekać na import/kompilację w głównym Unity. Statyczne lampiony/ogniska/pochodnie jako dekoracje lasu są osobnym przyszłym zadaniem, poza zakresem integracji grafik i zakazem zmiany jego wizualizacji.


## 2026-10-13 — Statyczne światła Lasu, pierwszy pass — READY_FOR_SMOKE

Płonące paleniska w istniejących kompozycjach obozowisk oraz rzadkie pochodnie przy pniach, ruinach i skrzyniach. Wykorzystano istniejący native art pochodni/paleniska; brak nowych tekstur lub generatora. Ognisko: trzy wycinki płomienia przy 32 PPU, jeden slot światła, bez powiększania pikseli. Nazwa obiektu „Palenisko” obejmuje wersje płonące i zimne. Pochodnie mają własny istniejący trzonek/uchwyt; nie dodano osobnej grafiki stojaka ani lampionu. Lampiony nadal wymagają dedykowanego sprite'a i pozostają przyszłym rozszerzeniem.

Rozmieszczenie deterministyczne i tylko wizualne: istniejące paleniska mają pierwszeństwo, część pozostaje wygaszona, źródła zachowują 9 unitów odstępu. Wykluczone korytarze i pokoje Final/arena Enta. Punkty pochodni na drzewach pochodzą z rzeczywistych kotew renderowanych pni. Brak nowych colliderów, łupu lub zmian zajętości/nawigacji. Pochodnie i płomienie respektują fog oraz sortowanie w głębi; światło może docierać do widocznego obszaru zza ukrytego źródła zgodnie z dotychczasowym systemem.

Wszystko korzysta z DungeonSilhouetteLighting: zachowane 5 Hz/co0,2 s, osiem aktywnych świateł, culling i cache cieni; bez nowego Update/korutyn/skanowania wszystkich drzew co klatkę. Rozmieszczenie liczone raz po przypięciu danych piętra, czyszczone przy zmianie mapy/wyłączeniu komponentu. Mocniejsze ogniska mają promień0,85 bazowego, pochodnie0,65; osobiste światło drużyny zachowane. Osiem slotów jest wspólnych dla źródeł statycznych i drużyny.

42/42 EditMode PASS w kopii walidacyjnej: ForestStaticLightTests, DungeonLightingCadenceTests, ForestImportedAssetsTests (out/forest-static-lights4.xml). Trzy seedy: sparse placements, powtarzalne budowanie bez duplikatów, bez modyfikacji obiektów, 32 PPU i sprzątanie. Istniejące testy cadence/cache sprawdzają zachowanie próbkowania i unieważniania cieni. Self-review i diff check zakończone; kod główny zgodny z testowaną kopią. Nowego pomiaru FPS/profilera ani wizualnego PlayMode smoke nie wykonano.

Smoke: uruchomić Las i znaleźć porzucone obozowisko; obejrzeć ogień na kamieniach, pochodnie przy pniach/ruinach, sortowanie postaci oraz wygaszanie fixture przez fog. Przejść między polanami i wrócić, następnie wyjść/wejść ponownie; źródła nie powinny się dublować. Bez ręcznego podpinania assetów w Inspectorze. W dalszym pomiarze porównać Godmist.Lighting.Refresh i Godmist.Lighting.ShadowMasks przy ognisku oraz na ciemnej polanie; nie deklarowano neutralnego kosztu FPS.


### 2026-10-13 — Sortowanie pochodni Lasu

Naprawiono zasłanianie pochodni przez kolumny/pnie: fixture używa aktualnego SortingGroup mocowania +1 albo renderera konkretnego pnia +1 zamiast warstwy ścian. Kolejność jest odświeżana z właścicielem podczas istniejącego refresh5 Hz. Bez zmiany rozmiaru/położenia źródła. 43/43 EditMode PASS (out/forest-light-sorting.xml), w tym regresja po zmianach głębi właściciela; self-review/diff check zakończone. Wizualny smoke: sprawdzić wcześniej pokazany filar po ponownym wejściu do Lasu.


### Forest camp and idle facing polish (2026-10-13)
Enemy patrol initialization repairs facing into blocked boundaries; patrol stops face accessible space with the most neighbouring walkable cells. Valid saved facing and LOS tracking are preserved. This runs at initialization/arrival, not every frame. Existing detection cone rules remain unchanged.
Forest carried illumination is less orange; static fixtures have a separate warm colour and hearth radius is reduced to 72% of torch radius. Hearth flame fragments are tinted orange instead of nearly white, at native 32 PPU. No extra lights, renderers, colliders or per-frame scans were added.
The blind glade bedroll moves beside the hearth (local 3,3); its nonblocking adjacency is reproduced by ForestPrototypeSetup. Chest and paths remain accessible. Existing saves retain object positions; new expeditions use the compact camp.
Visual gameplay smoke remains required: guards at the forest edge, orange visible fire, party vs hearth lighting, and camp arrangement. No FPS measurement or manual game preview was performed.

Verification: 67/67 targeted regression tests PASS (forest-polish.xml); after final camp adjustment 38/38 PASS (forest-polish-camp.xml); explicit blocked-facing and saved-facing assertions plus patrol movement/doors 5/5 PASS (forest-facing.xml). Compilation passed in isolated Unity project. Self-review complete; READY_FOR_SMOKE.



### Ent encounter presentation — 2026-10-13
READY_FOR_SMOKE. Boss encounters use visible living participant silhouettes for initial/Home framing, including actual imported sprite bounds. Automatic boss framing is not pushed toward the centre of the entire floor; manual pan still uses floor limits. Home refreshes participant positions; skill focus restores the preceding view. Ordinary encounter framing is preserved.
CombatHudView owns a runtime boss panel below the timeline using the bound HUD typography. It displays name, HP/shield and Ent phase I/II, updates on health/definition/state changes, hides on death/leaving combat/hidden unit, and has no raycast targets. Skill banner sits below it, and action/boss framing reserves this HUD space. No serialized HUD setup required.
Ent arena gains up to 20 static native-32-PPU ground root/deadwood accents in broken arcs, leaving the centre/entrance clear. They follow existing fog, sorting and lighting ownership; no collision, shadow casters or traversal revisions are added. Forest exits retain their usable stone marker, now moss-coloured with two native root accents rather than a blue dungeon appearance. No new artwork was generated and no boss balance, phase thresholds or green fire rules changed.
Verification: isolated Unity compilation and 108/108 combat/camera/Ent regression tests PASS (out/ent-presentation-check.xml); final UI lifecycle/exit-decoration verification 57/57 PASS (out/ent-hud-final.xml). Boss HUD render inspected separately; full gameplay framing/arena remains a manual smoke check. No new FPS measurement. Smoke: enter a fresh Ent fight, inspect centered view and HP, press Home after movement, cast at range and check restore, cross 50% HP, verify phase II plus green arena fire, defeat boss and confirm bar disappears. Existing saves do not need migration.

## Forest gateway and Ent presentation follow-up — 2026-10-13
Status: READY_FOR_SMOKE. Supersedes the earlier tinted stair-marker presentation.
- Forest transitions use two existing dead-tree sprites at native 32 PPU, with the stair marker hidden. The exit/start interaction is relocated to a clear north/south edge of its room; interaction rules remain unchanged. Catacombs retain their original transitions.
- Ent presentation recognizes its definition and phase state even without the expedition IsBoss flag, for the boss HUD and camera framing.
- Movement-range fill opacity reduced; outer boundary retained. Skill targeting is unchanged.
- Arena roots/deadwood grouped into three patches, leaving the central lane clear.
Validation: isolated Unity EditMode run, 55/55 passed (out/forest-gateway.xml), no compiler errors. Covers production HUD binding without IsBoss, native gateway scale and boundary placement, arena decoration lifecycle, camera framing, grid feedback and Ent mechanics.
Game smoke still required: fresh forest expedition, gateway visibility/interaction at both room edges, Ent HUD and camera, movement overlay readability, phase-II transition. No manual scene/prefab wiring required. Gateway is a natural passage between dead trees using existing artwork.
## Ent canopy clearance / retired boss HP panel — 2026-10-13
Supersedes the separate boss health HUD: removed its refresh, layout reservation and generated UI. Unit health inspection remains unchanged.
Arena boundary vegetation now receives a one-time outward correction against the union of arena cell silhouette envelopes (4 units wide, from -0.5 to +3.5 units vertically at unit cell size). Checked the final south idle art: both phases have about 3.1 units visible height at 32 PPU. Original collision, navigation, fog edge ownership and green-fire registration remain unchanged; no per-frame canopy fading.
Smoke: re-enter/rebuild the forest arena, move Ent to its north/south/side edges and check both phases. Existing instantiated maps require rebuilding to receive this presentation change.
Validation: READY_FOR_SMOKE; final isolated EditMode run 88/88 passed (out/ent-clearance-final.xml), no C# compiler errors; cardinal canopy clearance, distant tree stability, HUD, camera, arena details and fire regressions covered. Full in-game visual smoke pending.

## Forest readability pass — 2026-10-13
- Visual paths now blend segments between adjacent path cells instead of stamping isolated circles; variable width and obstacle clearance retained.
- Native-size low shrubs mark impassable boundary seams, using existing boundary fog ownership and sorting. No blockers or traversal topology added.
- Boundary crowns receive a build-time clearance correction around authored enemy exploration homes. No per-frame character/canopy fading. This protects initial homes, not arbitrary later patrol/combat positions behind individual in-room trees.
- Forest props use the authored pivot for projected contact rather than transparent canvas bottom; existing batched contact-shadow mesh adds stepped translucent black base patches for forest objects and registered low props. Catacombs keep their existing treatment.
Smoke required: rebuild/re-enter forest; inspect rock base/shadow, small props on grass, connected trail forks, boundary gaps and enemy homes. Existing native 32 PPU scale unchanged. No manual prefab wiring, commits or source-art modifications.
Validation: READY_FOR_SMOKE; final isolated EditMode run 90/90 passed (out/forest-readability-final.xml), no C# compiler errors. Full game visual verification pending.

## Forest enemy navigation and dedicated light art — 2026-10-13
Implementation replaces earlier crown-only mitigation.
- Confirmed initialization bug: vegetation originally consulted FogFloor before the first fog binding. Bootstrap now supplies the current presentation floor before Rebuild; arena and enemy-home clearance work on first entry.
- Forest spawn owns full floor data. Unsafe legacy positions and excessive saved offsets are repaired into free interior cells; Ent starts as a stationary arena guard. No save schema change.
- Ent movement and forced displacement share the arena clearance rule, using its stored ArenaRoomId when available; player movement is unchanged. Regular forest enemy patrol planning checks cell clearance and physical standing space; saved routes are revalidated and unreachable stops are skipped.
- A single optional silhouette renderer per visible living enemy stays readable through a foreground canopy. Canopy coverage is indexed when the map is built; per-frame work is a cell lookup, not scanning/fading tree renderers. Fog/actor visibility and death suppress the silhouette.
- Replaced forest tree-mounted Catacombs fixtures with dedicated freestanding forest brazier art; hearth flame comes from the forest artwork too. Native 32x56 canvas, 32 PPU, point filter, grounded 4px pivot. Forest flame masking uses its own artwork; Catacombs keep the authored mask.
- Art generated using imagegen and resized/exported with Aseprite. Final resource: Assets/Resources/Godmist/ForestArt/Fixtures/forest-brazier-32ppu.png. Native source and prompt: output/forest-brazier-native.aseprite and output/forest-brazier-handoff.txt. Scale preview: out/forest-brazier-scale.png (checked beside Jerzy).
Smoke required: re-enter a saved forest (especially Ent previously outside arena), test both boss phases at all arena edges, chase/patrol near trees and obstacles, check hidden enemies do not show silhouettes, and inspect brazier/hearth art in actual lighting. No FPS benchmark or manual full-game smoke has been performed. No manual prefab wiring required. No commits or pushes.Validation: READY_FOR_SMOKE. Main targeted run 151/151 passed; navigation/combat compatibility rerun 68/68 passed; final fixture/mask/contact/navigation checks 15/15 passed. No C# or shader compilation errors in these runs; focused diff checks clean. Runtime scale preview inspected. Earlier interim failures were fixed: Runtime assembly dependency, test-only missing sort baselines, missing encounter placement in the legacy-position fixture, and the updated private spawn signature in an existing regression test. Actual in-game appearance and FPS still require smoke verification.
## Masked canopy contours and forest contact ordering — 2026-10-13
Supersedes the full gold silhouette from the preceding pass.
- Enemy overlay clips against alpha from up to four indexed foreground canopy sprites. Uncovered pixels receive no overlay. Covered interior is faint (0.12 of tint alpha), with a stronger one-pixel contour; neutral tint replaces gold. Fog/death eligibility is retained.
- Canopy index stores renderers and their effective sort groups; runtime query checks actual body/canopy bounds and actual foreground order. Textures and registration matrices upload only when the foreground set changes. No whole-forest scan, canopy fading or mask-camera render pass.
- Native forest sprites are unpacked (the only project SpriteAtlas is the UI atlas); registration handles their world transform and flip without changing sprite scale/pivots.
- Forest contact-shadow mesh is above the ground-cover lane, below standing objects. Rounded scanline bands replace rectangular pads; unchanged geometry still reuses the mesh.
- GPU regression renders a half-covered actor: exposed pixels retain their original red, canopy-covered interior remains faint. Contact regression checks ordering over litter, rounded geometry and cache reuse. Rendering test compares readback colours in linear space.
Smoke: rebuild/re-enter forest; inspect partial/full tree occlusion, ordinary exposed enemies, rock bases on grass, fog transitions and both Ent phases. No sprite/art regeneration or manual prefab wiring required. Full-game visual/FPS smoke remains pending.
Validation: READY_FOR_SMOKE; 87/87 targeted EditMode tests passed (out/forest-canopy-verified.xml), including actual GPU pixel readback, enemy visibility, contact mesh cache and forest regressions. No C# or shader compilation errors; focused diff clean. New shader/test metas retained from Unity import.

## Ent transformation on next own turn — 2026-10-13
Status: READY_FOR_SMOKE. Supersedes the immediate post-damage/skill phase switch.
At <=50% HP, CheckPhases latches PhaseTwoPending after the whole resolving skill. Definition, model and skillset stay in phase I until Ent's next BeginTurn. Other units' turns cannot consume the pending change. Crossing the threshold during Ent's current turn waits for its following turn. Healing after crossing does not cancel the latch; death does. Pending state survives runtime owner rebind/merge without a save-format change.
At the next own turn, phase/skillset change once and the driver explicitly starts the existing authored transformation on the borrowed exploration view or regular combat view. Six frames at the authored 5 FPS reserve 1.2 seconds of the existing action-presentation gate before AI can act. Old poses are cleared and movement/idle writes cannot overwrite the clip; S-only transformation keeps logical facing for the phase-II directional idle. No HP, resource, skill balance or transformation artwork changes; normal turn-start AP refill is retained.
Validation: initial combat/presentation regressions 64/64 PASS; final Ent/art suite 43/43 PASS including two EnterPlayMode runtime tests. The new runtime test uses production-style map visibility, borrowed PF_ExplorationCharacter, real turn-start handler and presentation gate; checks old phase during another unit's turn, immediate frame 0 at own turn, all-frame duration, movement/pose protection and phase-II W idle. Also checks multi-hit completion, pending healing/death/rebind, once-only change, buffs, plague and arena mechanics. No compiler errors; focused diff checks clean.
Smoke: start a fresh Ent fight; cross 50% HP during a party action, confirm phase I persists until Ent's turn, then observe full transformation before its next action and green arena fire. Existing already-transformed battles are not rolled back. No manual prefab/scene wiring or art reimport needed. Full gameplay smoke remains pending; no commit/push.

## Forest combat/HUD polish — 2026-10-13
Status: READY_FOR_SMOKE.
- Resource and vitals health bars correct the existing slider_track slice multiplier to 2 at runtime. The authored 12/6px border now fits the 6/3px fill-area insets; resource colour/value, neutral bars and other frame artwork stay unchanged. Checked the real CombatVitals prefab with Rage, Mana and Momentum at empty/partial/full values, plus resource rebinding.
- Bottom vitals and fallback inspect preserve the fractional MaxHealth until the shared formatter, removing the 492/563 versus 492/562 discrepancy. Slider maximum remains the exact 562.5 value; no stat/balance change.
- Connected range contours use a narrower, quieter falloff without internal cell seams. Fill opacity, target pixel corners and logical selection remain unchanged.
- Forest soft fog now feathers at missing terrain, as well as unknown neighbours, removing an unfeathered rectangular floor edge between sparse boundary trees. Masonry fallback and discovered/visible cell sets remain unchanged. This targets the terrain perimeter, not a blanket claim that every possible occlusion artifact is eliminated.
- Forest ordinary fights use the existing participant-bounds camera overview, as boss fights already did. Initial frame/Home fit visible living bodies with HUD-safe margins; manual view persistence and action restore remain.
- Combat enemies gain a small static lower red foot arc, sharing visual-state ownership. Hidden/dead/player units and actors leaving combat have no marker. LateUpdate rechecks after awareness/fog/depth updates; geometry is created once and reused, without per-frame mesh rebuilding.
Validation: first focused HUD/camera/fog/lighting run 155/155 PASS (out/forest-hud-pass.xml); final forest/navigation/seamless/visual-state regression 125/125 PASS (out/forest-hud-regression.xml). These suites overlap and are not a unique total. No C# compiler errors. Actual prefab render inspected: out/forest-hud-vitals-preview.png. Owned-file mirror comparison and final self-review complete; no save schema, art, scene/prefab edits, commits or pushes.
Smoke: reopen/rebuild forest, compare both HP labels, inspect Rage at 0/20/50 and switching leader, check thin range outline and connected edges, follow known/unknown terrain perimeter, start an ordinary bandit fight and use Home/manual zoom, verify enemy arcs hide in fog/on death/after combat. Full-game visual and FPS verification remains pending. No manual Editor wiring required.


## Ground corpse layering and restrained Ent arena fire — 2026-10-13
Status: READY_FOR_SMOKE.
Confirmed the wolf uses the final dedicated corpse PNG. The visual defect was the fixed corpse sorting order 9, which overpainted actors in northern/large rooms where actor depth orders are negative. Bound bodies now use the map ground-object lane and layer; hover outlines stay one lane below them. Bodies register with the existing ground-lighting stream, including its reserved outline band, and unregister when rebound/destroyed. They therefore receive terrain lighting instead of remaining brightly unlit. Native 32-PPU dedicated corpse sprites use scale 1 instead of the legacy fallback scale 0.7; their original centered pivots, rotation and colour remain unchanged. Missing-art fallback is retained only for existing definitions without dedicated corpse art.
The arena-fire shader now renders narrower native-pixel tongues near roots/trunks and sparse lower-branch patches, with reduced brightness/opacity so original trees remain visible. Core2D sprite setup correctly applies flip and SpriteRenderer colour, including remembered-fog attenuation. The existing shared material, 5 FPS animation, phase/arena/fog ownership, overlay count and termination remain. No new lights, particles, colliders, terrain damage or gameplay/balance changes.
Validation: initial focused suite 75/75 PASS; final art/Ent/body/lighting/canopy suite 118/118 PASS (out/forest-corpses-fire-accepted.xml), including the existing two EnterPlayMode transformation tests. Strengthened corpse GPU regression 1/1 PASS (out/corpse-layer-render-final.xml): native Jerzy/wolf art actually overlaps at negative actor depth; old order 9 is an explicit failing visual control, while the new ground lane matches the proper alpha blend. Native hero artwork has near-opaque alpha and test readbacks account for sRGB8/alpha quantization; no hero art was altered. Fire GPU regressions verify normal/flipped trees, retained original pixels, restrained green and dimmed fog tint. Preview images inspected in out/forest-corpses-fire-previews (corpse-legacy-layer, corpse-native-ground-layer, ent-tree-baseline, ent-tree-burning and arena preview). No C# or shader compilation errors; final owned-file mirror/hash and whitespace checks passed; self-review completed.
Smoke: re-enter/rebuild the forest to recreate body views. Walk over/beside killed wolves and other corpses, check native scale, grounded lighting, hover/loot and fog visibility. Trigger Ent phase II in a new fight, inspect green flames on roots/branches and the remaining canopy texture, then end combat and verify fire disappears. Full-game visual/FPS smoke remains pending. No manual prefab/scene wiring, art regeneration, source-pack edits, save migration, commit or push.
