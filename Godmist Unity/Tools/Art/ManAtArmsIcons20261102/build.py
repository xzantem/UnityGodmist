"""Original opaque full-canvas nearest32 Aseprite export and exact roundtrip QA."""
from pathlib import Path
import hashlib,json,subprocess,sys
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parent
ASE="C:/Program Files/Aseprite.v1.3.17/game/Aseprite.exe"
entries=json.loads((ROOT/"prompts.json").read_text(encoding="utf-8"))["entries"]
review_only="--review-only" in sys.argv
if not review_only:subprocess.run([ASE,"-b","--script-param","root="+ROOT.as_posix()+"/","--script",str(ROOT/"export.lua")],check=True)
records=[];sheet=Image.new("RGB",(1100,1300),(23,25,31));d=ImageDraw.Draw(sheet)
d.font=ImageFont.truetype("C:/Windows/Fonts/arial.ttf",14)
for i,e in enumerate(entries):
 slug=e["slot"];src=ROOT/"sources"/(slug+".png");png=ROOT/"native"/(slug+"-32.png")
 s=Image.open(src).convert("RGBA");n=Image.open(png).convert("RGBA")
 assert n.size==(32,32) and n.getextrema()[3]==(255,255)
 assert all(n.getpixel((x,y))==s.getpixel((x*s.width//32,y*s.height//32)) for y in range(32) for x in range(32)),slug
 if not review_only:
  rt=ROOT/"native"/(slug+"-roundtrip.png")
  subprocess.run([ASE,"-b",str(ROOT/"native"/(slug+"-32.aseprite")),"--save-as",str(rt)],check=True)
  assert n.tobytes()==Image.open(rt).convert("RGBA").tobytes();rt.unlink()
 records.append({"id":e["id"],"slot":slug,"destination":"Assets/_Project/Art/Icons/Passives/VS22/WarriorManAtArms/"+slug+"-32.png","source_sha256":hashlib.sha256(src.read_bytes()).hexdigest(),"png_sha256":hashlib.sha256(png.read_bytes()).hexdigest(),"nearest_sampling":"PASS","aseprite_roundtrip":"PASS"})
 x=(i%5)*220;y=(i//5)*260
 d.text((x+12,y+8),slug+" "+e["name"],fill="white");sheet.paste(n.convert("RGB"),(x+16,y+38))
 sheet.paste(n.convert("RGB").resize((128,128),Image.Resampling.NEAREST),(x+16,y+82))
sheet.save(ROOT/"review.png")
if not review_only:(ROOT/"records.json").write_text(json.dumps(records,indent=2),encoding="utf-8")
print("Preview refreshed;native sampling25/25 PASS;Aseprite roundtrip not rerun." if review_only else "25/25 exact nearest32 + opaque RGBA + editable Aseprite roundtrip PASS")
