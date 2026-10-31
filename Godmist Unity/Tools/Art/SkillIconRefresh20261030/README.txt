Godmist skill icon refresh, 2026-10-30

ORIGINAL method recovered from Godmist-cleanup-backup-20261011/output/asset-review:
- One built-in image_gen call per source, original style-only raw reference.
- Brawler: burgundy #30151d / #511f2c; Blade Dancer: teal #173033 / #254b50.
- Aseprite: flatten the full opaque square canvas; sample source pixels at
  floor(x*sourceWidth/32), floor(y*sourceHeight/32). No crop, BOX filtering,
  palette quantization or dithering. Save32 PNG and editable Aseprite.
- Unity: Sprite/Single,32PPU,Point,Clamp,no mipmaps,uncompressed.

Run: python build.py (Aseprite1.3.17 at executable path in script).
Sources and references are retained here, outside Assets; no dependency on
ignored output/asset-review or LocalHandoffs for re-export.
Original export scripts and prompts are preserved read-only under originals/.
New prompts and generation provenance are in prompts.json; hashes records.json.
The subjects adapt to the current mechanics; they do not reuse obsolete bleed
subjects. No mechanics, costs, ranks or IDs change.

Apply imported references only with Godmist/Content/Apply dedicated Discharge
and Recovery icons in Edit Mode (SkillIconRefreshSetup).
