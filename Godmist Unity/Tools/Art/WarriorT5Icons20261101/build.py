"""Re-export CP2-05 icons with the original Aseprite full-canvas nearest32 method."""
from pathlib import Path
import hashlib,json,subprocess
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[2]
ASE="C:/Program Files/Aseprite.v1.3.17/game/Aseprite.exe"
entries=json.loads((ROOT/"prompts.json").read_text(encoding="utf-8"))["entries"]
subprocess.run([ASE,"-b","--script-param","root="+ROOT.as_posix()+"/","--script",str(ROOT/"export.lua")],check=True)
records=[]
for e in entries:
    slug=e["slug"]
    source=ROOT/"sources"/(slug+".png")
    native=ROOT/"native"/(slug+"-32.png")
    with Image.open(source) as s,Image.open(native) as n:
        s=s.convert("RGBA");n=n.convert("RGBA")
        assert n.size==(32,32) and n.getextrema()[3]==(255,255),slug
        assert all(n.getpixel((x,y))==s.getpixel((x*s.width//32,y*s.height//32)) for y in range(32) for x in range(32)),slug
    # Re-open the editable file through Aseprite and verify exact export.
    rt=ROOT/"native"/(slug+"-roundtrip.png")
    subprocess.run([ASE,"-b",str(ROOT/"native"/(slug+"-32.aseprite")),"--save-as",str(rt)],check=True)
    assert Image.open(rt).convert("RGBA").tobytes()==Image.open(native).convert("RGBA").tobytes(),slug
    rt.unlink()
    group="WarriorCommander" if e["tree"]=="commander" else "WarriorBrawler"
    records.append({"id":e["id"],"slug":slug,"destination":"Assets/_Project/Art/Icons/Passives/VS22/"+group+"/t5_"+slug+"-32.png","source_sha256":hashlib.sha256(source.read_bytes()).hexdigest(),"png_sha256":hashlib.sha256(native.read_bytes()).hexdigest(),"nearest_sampling":"PASS","aseprite_roundtrip":"PASS"})
(ROOT/"records.json").write_text(json.dumps(records,indent=2),encoding="utf-8")
sheet=Image.new("RGB",(1000,650),(23,25,31));d=ImageDraw.Draw(sheet)
for i,e in enumerate(entries):
    x=(i%5)*200;y=(i//5)*290
    im=Image.open(ROOT/"native"/(e["slug"]+"-32.png")).convert("RGB")
    sheet.paste(im,(x+16,y+32));sheet.paste(im.resize((128,128),Image.Resampling.NEAREST),(x+16,y+80))
    d.text((x+12,y+8),e["name"],fill="white")
    d.text((x+12,y+220),e["slug"],fill=(190,195,205))
# Existing shipped reference icons for immediate style comparison.
for i,(group,slug) in enumerate([("WarriorCommander","marking"),("WarriorCommander","weapon_mastery"),("WarriorBrawler","rage"),("WarriorBrawler","amok")]):
    p=PROJECT/"Assets/_Project/Art/Icons/Passives/VS22"/group/(slug+"-32.png")
    if p.exists():
        sheet.paste(Image.open(p).convert("RGB").resize((64,64),Image.Resampling.NEAREST),(i*200+16,576))
        d.text((i*200+86,598),"existing "+slug,fill=(170,180,190))
sheet.save(ROOT/"review.png")
print("10 icons: opaque32, exact nearest sampling and editable Aseprite roundtrip PASS")
