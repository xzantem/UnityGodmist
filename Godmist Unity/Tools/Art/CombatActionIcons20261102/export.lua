-- Technical export only. Creative edits were made by built-in image_gen.
-- Normalize all candidates to the current icon's footprint, area-average in
-- premultiplied alpha, use a hard alpha edge, then quantize without dithering.
local root = assert(app.params.root, "Pass root=absolute/path/")
local manifestFile=assert(io.open(root.."manifest.json","r"))
local specs=json.decode(manifestFile:read("*a")).specs
manifestFile:close()
local files={}
for _,s in ipairs(specs) do files[s.id]=s.file end
local pc=app.pixelColor


local function bounds(image)
  local x0,y0,x1,y1=image.width,image.height,-1,-1
  for y=0,image.height-1 do
    for x=0,image.width-1 do
      if pc.rgbaA(image:getPixel(x,y))>=128 then
        x0=math.min(x0,x); y0=math.min(y0,y)
        x1=math.max(x1,x); y1=math.max(y1,y)
      end
    end
  end
  assert(x1>=x0,"Empty source")
  return x0,y0,x1-x0+1,y1-y0+1
end

local function export(id,path,suffix)
  local source=Image{fromFile=path}
  local current=Image{fromFile=root.."before/"..files[id]}
  local bx,by,bw,bh=bounds(current)
  local sx,sy,sw,sh=bounds(source)
  local scale=math.min(bw/sw,bh/sh)
  local dw=math.max(1,math.floor(sw*scale+.5))
  local dh=math.max(1,math.floor(sh*scale+.5))
  local dx=math.floor(bx+(bw-dw)/2+.5)
  local dy=math.floor(by+(bh-dh)/2+.5)
  local sprite=Sprite(32,32,ColorMode.RGB)
  local image=sprite.cels[1].image
  for y=0,dh-1 do
    local top=sy+y*sh/dh; local bottom=sy+(y+1)*sh/dh
    for x=0,dw-1 do
      local left=sx+x*sw/dw; local right=sx+(x+1)*sw/dw
      local red,green,blue,alpha,total=0,0,0,0,0
      for yy=math.floor(top),math.ceil(bottom)-1 do
        local wy=math.min(bottom,yy+1)-math.max(top,yy)
        for xx=math.floor(left),math.ceil(right)-1 do
          local w=wy*(math.min(right,xx+1)-math.max(left,xx))
          local p=source:getPixel(xx,yy); local a=pc.rgbaA(p)/255
          alpha=alpha+w*a; total=total+w
          red=red+w*a*pc.rgbaR(p)
          green=green+w*a*pc.rgbaG(p)
          blue=blue+w*a*pc.rgbaB(p)
        end
      end
      if alpha/total>=.50 then
        image:drawPixel(dx+x,dy+y,pc.rgba(math.floor(red/alpha+.5),math.floor(green/alpha+.5),math.floor(blue/alpha+.5),255))
      end
    end
  end
  app.activeSprite=sprite
  app.command.ColorQuantization{ui=false,withAlpha=false,maxColors=24,algorithm="octree"}
  app.command.ChangePixelFormat{ui=false,format="indexed",dithering="none",rgbmap="octree"}
  app.command.ChangePixelFormat{ui=false,format="rgb"}
  sprite:saveAs(root.."native/"..files[id]:gsub("%.png$", ".aseprite"))
  sprite:saveCopyAs(root.."native/"..files[id])
  sprite:close()
  print("EXPORTED "..id..suffix)
end

for _,s in ipairs(specs) do
  local source=root.."generated/"..s.id..".png"
  local revised=root.."generated/"..s.id.."-v2.png"
  local newest=root.."generated/"..s.id.."-v3.png"
  if app.fs.isFile(newest) then revised=newest end
  local hasRevision=app.fs.isFile(revised)
  if hasRevision then source=revised end
  if not s.reused and app.fs.isFile(source) and (hasRevision or not app.fs.isFile(root.."native/"..s.file)) then
    export(s.id,source,"")
  end
end
