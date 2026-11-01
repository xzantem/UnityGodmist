CP2-05 dedicated T5 passive icons, 2026-11-01

Ten separate built-in image_gen calls, one opaque square per passive. Prompts
and mapping: prompts.json. Original style-only references are retained under
references; every generated source is retained under sources.

Commander: dark steel navy #182534 / #26394c. Brawler: muted burgundy
#30151d / #511f2c. Subjects follow current approved mechanics, including
Trening Skupienia +0.5 skill accuracy per pre-payment Rage point.

Export: python build.py. Uses Aseprite 1.3.17 headlessly, flattening the entire
source canvas then sampling floor(x*width/32), floor(y*height/32).
No crop, BOX filter, palette quantization or dithering. The build retains
editable native/*.aseprite and native/*-32.png, checks exact source sampling
and Aseprite RGBA roundtrip, and creates review.png at native and 4x sizes.
records.json records all ten production destinations and source/output hashes.

Focused Unity authoring: ContentPass2WarriorT5Setup.Apply. Imports dedicated
sprites and binds the existing ten T5 nodes. Sprite Single,32PPU,Point,Clamp,
no mipmaps,uncompressed. No changes to existing T1-T4 icons or mechanics.
The historical VS08 rebuild uses this same T5 definition and icon mapping.

Icons were authored in an isolated hidden Unity batch copy and its generated
PNG/meta/tree outputs copied byte-for-byte to the project. No desktop focus,
keyboard control or handwritten Unity YAML/GUIDs.

Evidence: ../../../../LocalHandoffs/cp2-05-icons-20261101/.
