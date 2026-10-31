local p = "C:/Users/Patryczku/Documents/Godmist/output/asset-review/skills-complete/warrior/"
local atlas = Sprite(192, 96, ColorMode.RGB)
local atlasImage = Image(192, 96, ColorMode.RGB)

local function sampleFullCanvas(image, width, height)
  local tile = Image(32, 32, ColorMode.RGB)
  for y = 0, 31 do
    for x = 0, 31 do
      local sx = math.min(width - 1, math.floor(x * width / 32))
      local sy = math.min(height - 1, math.floor(y * height / 32))
      tile:drawPixel(x, y, image:getPixel(sx, sy))
    end
  end
  return tile
end

local function exportIcon(id, sourceName, col, row)
  local s = app.open(p .. sourceName)
  assert(s, "Could not open source: " .. sourceName)
  assert(s.width > 0 and s.height > 0, "Source has invalid dimensions: " .. sourceName)

  -- Flatten and sample the entire source canvas. Generated backgrounds are part
  -- of each composition, so never crop to an alpha bounding box.
  local im = Image(s.width, s.height, ColorMode.RGB)
  im:drawSprite(s, 1)
  local flat = sampleFullCanvas(im, s.width, s.height)

  local cleanedTile = Sprite(32, 32, ColorMode.RGB)
  cleanedTile:newCel(cleanedTile.layers[1], 1, flat, Point(0, 0))
  cleanedTile:saveAs(p .. id .. "-32.png")
  cleanedTile:saveAs(p .. id .. "-32.aseprite")

  local x = col * 32
  local y = row * 32
  local layer = atlas:newLayer()
  layer.name = id
  atlas:newCel(layer, 1, flat, Point(x, y))
  for py = 0, 31 do
    for px = 0, 31 do
      atlasImage:drawPixel(x + px, y + py, flat:getPixel(px, py))
    end
  end

  cleanedTile:close()
  im = nil
  s:close()
end

local icons = {
  { "cut", "source/raw/cut-source-raw.png", 0, 0 },
  { "bandage", "source/raw/bandage-source-raw.png", 1, 0 },
  { "bleeding-destruction", "source/raw/bleeding-destruction-source-raw.png", 2, 0 },
  { "death-wish", "source/raw/death-wish-source-raw.png", 3, 0 },
  { "destruction", "source/raw/destruction-source-raw.png", 4, 0 },
  { "immobilize", "source/raw/immobilize-source-raw.png", 5, 0 },
  { "kick", "source/raw/kick-source-raw.png", 0, 1 },
  { "protection", "source/raw/protection-source-raw.png", 1, 1 },
  { "regroup", "source/raw/regroup-source-raw.png", 2, 1 },
  { "right-hook", "source/raw/right-hook-source-raw.png", 3, 1 },
  { "shout", "source/raw/shout-source-raw.png", 4, 1 },
  { "whirlwind", "source/raw/whirlwind-source-raw.png", 5, 1 },
  { "ancient-incantation", "source/raw/ancient-incantation-source-raw.png", 0, 2 },
  { "ancient-whip", "source/raw/ancient-whip-source-raw.png", 1, 2 },
  { "battle-fervor", "source/raw/battle-fervor-source-raw.png", 2, 2 },
  { "blood-scent", "source/raw/blood-scent-source-raw.png", 3, 2 },
  { "bone-bow", "source/raw/bone-bow-source-raw.png", 4, 2 },
  { "bone-club", "source/raw/bone-club-source-raw.png", 5, 2 },
}

for _, icon in ipairs(icons) do
  exportIcon(icon[1], icon[2], icon[3], icon[4])
end

atlas:saveAs(p .. "warrior-skills-atlas-6x3.png")
atlas:saveAs(p .. "warrior-skills-atlas-6x3.aseprite")

local previewImage = Image(1152, 576, ColorMode.RGB)
for py = 0, 575 do
  for px = 0, 1151 do
    previewImage:drawPixel(px, py, atlasImage:getPixel(math.floor(px / 6), math.floor(py / 6)))
  end
end
local preview = Sprite(1152, 576, ColorMode.RGB)
preview:newCel(preview.layers[1], 1, previewImage, Point(0, 0))
preview:saveAs(p .. "warrior-skills-atlas-6x3-nearest6x.png")
preview:saveAs(p .. "warrior-skills-atlas-6x3-nearest6x.aseprite")
preview:close()

atlasImage = nil
print("Complete Godmist Warrior/Catacombs skill pack: 18 icons x 32px, 6x3 atlas")
