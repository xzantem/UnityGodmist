from pathlib import Path
import subprocess,shutil
root=Path(__file__).resolve().parent
subprocess.run([r"C:/Program Files/Aseprite.v1.3.17/game/Aseprite.exe","-b","--script-param","root="+root.as_posix()+"/","--script",str(root/"export.lua")],check=True)
art=root.parents[2]/"Assets/_Project/Art/Icons/CombatActions"
for name in ("icon_combat_end_turn-32.png","icon_combat_items-32.png"):
 shutil.copy2(root/"native"/name,art/name)
