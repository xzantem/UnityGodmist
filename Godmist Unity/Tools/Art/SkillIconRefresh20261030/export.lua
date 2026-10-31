-- Exact original sampleFullCanvas algorithm; no crop or quantization.
local p=app.params.root
assert(p, "Pass --script-param root=absolute/directory/")
local function sampleFullCanvas(image,width,height)
  local tile=Image(32,32,ColorMode.RGB)
  for y=0,31 do for x=0,31 do
    local sx=math.min(width-1,math.floor(x*width/32))
    local sy=math.min(height-1,math.floor(y*height/32))
    tile:drawPixel(x,y,image:getPixel(sx,sy))
  end end
  return tile
end
for _,id in ipairs({'discharge','wounding','larger_wound','revenge','efficient_recovery'}) do
  local s=app.open(p..'sources/'..id..'.png');assert(s)
  local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,1)
  local tile=sampleFullCanvas(im,s.width,s.height)
  local output=Sprite(32,32,ColorMode.RGB)
  output:newCel(output.layers[1],1,tile,Point(0,0))
  output:saveAs(p..'native/'..id..'-32.png')
  output:saveAs(p..'native/'..id..'-32.aseprite')
  output:close();s:close()
end
