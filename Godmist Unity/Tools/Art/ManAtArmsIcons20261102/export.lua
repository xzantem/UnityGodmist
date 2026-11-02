local root=assert(app.params.root)
for _,id in ipairs({"t1_1","t1_2","t1_3","t1_4","t1_5","t2_1","t2_2","t2_3","t2_4","t2_5","t3_1","t3_2","t3_3","t3_4","t3_5","t4_1","t4_2","t4_3","t4_4","t4_5","t5_1","t5_2","t5_3","t5_4","t5_5"}) do
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
