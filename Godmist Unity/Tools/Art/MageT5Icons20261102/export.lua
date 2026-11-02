local root=assert(app.params.root)
for _,id in ipairs({"w1","w2","w3","w4","w5","f1","f2","f3","f4","f5"}) do
 local s=assert(app.open(root.."sources/"..id..".png"))
 local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,1)
 local tile=Image(32,32,ColorMode.RGB)
 for y=0,31 do for x=0,31 do
  tile:drawPixel(x,y,im:getPixel(math.floor(x*s.width/32),math.floor(y*s.height/32)))
 end end
 local out=Sprite(32,32,ColorMode.RGB);out:newCel(out.layers[1],1,tile,Point(0,0))
 out:saveAs(root.."native/"..id.."-32.png");out:saveAs(root.."native/"..id.."-32.aseprite")
 out:close();s:close()
end
