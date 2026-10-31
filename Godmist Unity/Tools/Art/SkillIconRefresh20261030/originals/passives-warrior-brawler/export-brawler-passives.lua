-- Godmist Awanturnik passive icon exporter.
--
-- Every source is sampled across its complete canvas. There is deliberately no
-- content crop: explicit getPixel/drawPixel nearest-neighbour sampling makes
-- the 32x32 result deterministic and preserves the approved tree background.
-- Missing sources keep opaque black atlas slots so the review page can be
-- generated before all ImageGen sources arrive.

local p = "C:/Users/Patryczku/Documents/Godmist/output/asset-review/passives-warrior-brawler/"
local tileSize = 32
local atlasColumns = 4
local atlasRows = 4
local atlasWidth = tileSize * atlasColumns
local atlasHeight = tileSize * atlasRows

local function fileExists(path)
  local file = io.open(path, "rb")
  if file then
    file:close()
    return true
  end
  return false
end

local function blackImage(width, height)
  local image = Image(width, height, ColorMode.RGB)
  local black = Color{r=0, g=0, b=0, a=255}
  for y = 0, height - 1 do
    for x = 0, width - 1 do
      image:drawPixel(x, y, black)
    end
  end
  return image
end

local function sampleFullCanvas(image, width, height)
  local tile = Image(tileSize, tileSize, ColorMode.RGB)
  for y = 0, tileSize - 1 do
    for x = 0, tileSize - 1 do
      local sx = math.min(width - 1, math.floor(x * width / tileSize))
      local sy = math.min(height - 1, math.floor(y * height / tileSize))
      tile:drawPixel(x, y, image:getPixel(sx, sy))
    end
  end
  return tile
end

local function saveTile(id, tile)
  local sprite = Sprite(tileSize, tileSize, ColorMode.RGB)
  sprite:newCel(sprite.layers[1], 1, tile, Point(0, 0))
  sprite:saveAs(p .. id .. "-32.png")
  sprite:saveAs(p .. id .. "-32.aseprite")
  sprite:close()
end

local atlasImage = blackImage(atlasWidth, atlasHeight)
local atlas = Sprite(atlasWidth, atlasHeight, ColorMode.RGB)
atlas:newCel(atlas.layers[1], 1, atlasImage, Point(0, 0))

local function putInAtlas(id, tile, slot)
  local column = slot % atlasColumns
  local row = math.floor(slot / atlasColumns)
  local x0 = column * tileSize
  local y0 = row * tileSize
  local layer = atlas:newLayer()
  layer.name = id
  atlas:newCel(layer, 1, tile, Point(x0, y0))
  for y = 0, tileSize - 1 do
    for x = 0, tileSize - 1 do
      atlasImage:drawPixel(x0 + x, y0 + y, tile:getPixel(x, y))
    end
  end
end

local entries = {
  { id = "wounding", source = "wounding-source-raw.png", slot = 0 },
  { id = "thirst_revenge", source = "thirst_revenge-source-raw.png", slot = 1 },
  { id = "rage", source = "rage-source-raw.png", slot = 2 },
  { id = "furious_strikes", source = "furious_strikes-source-raw.png", slot = 3 },
  { id = "larger_wound", source = "larger_wound-source-raw.png", slot = 4 },
  { id = "revenge", source = "revenge-source-raw.png", slot = 5 },
  { id = "release_rage", source = "release_rage-source-raw.png", slot = 6 },
  { id = "spiked_armor", source = "spiked_armor-source-raw.png", slot = 7 },
  { id = "third_time", source = "third_time-source-raw.png", slot = 8 },
  { id = "amok", source = "amok-source-raw.png", slot = 9 },
  { id = "unawareness", source = "unawareness-source-raw.png", slot = 10 },
  { id = "aggressive_nature", source = "aggressive_nature-source-raw.png", slot = 11 },
  { id = "conflagration", source = "conflagration-source-raw.png", slot = 12 },
  { id = "force_moment", source = "force_moment-source-raw.png", slot = 13 },
  { id = "adrenaline", source = "adrenaline-source-raw.png", slot = 14 },
  { id = "desperation", source = "desperation-source-raw.png", slot = 15 },
}

local exported = 0
local missing = 0
for _, entry in ipairs(entries) do
  local sourcePath = p .. "source/raw/" .. entry.source
  local tile = blackImage(tileSize, tileSize)
  if fileExists(sourcePath) then
    local source = app.open(sourcePath)
    assert(source, "Could not open source: " .. sourcePath)
    assert(source.width > 0 and source.height > 0, "Source has invalid dimensions: " .. sourcePath)

    local sourceWidth = source.width
    local sourceHeight = source.height
    local flat = Image(sourceWidth, sourceHeight, ColorMode.RGB)
    flat:drawSprite(source, 1)
    tile = sampleFullCanvas(flat, sourceWidth, sourceHeight)
    saveTile(entry.id, tile)
    source:close()
    exported = exported + 1
    print("Exported " .. entry.id .. " from full canvas " .. sourceWidth .. "x" .. sourceHeight)
  else
    missing = missing + 1
    print("Missing source; kept black atlas slot for " .. entry.id .. ": " .. sourcePath)
  end
  putInAtlas(entry.id, tile, entry.slot)
end

atlas:saveAs(p .. "brawler-passives-atlas-4x4-128x128.png")
atlas:saveAs(p .. "brawler-passives-atlas-4x4-128x128.aseprite")

local previewImage = Image(atlasWidth * 6, atlasHeight * 6, ColorMode.RGB)
for py = 0, atlasHeight * 6 - 1 do
  for px = 0, atlasWidth * 6 - 1 do
    previewImage:drawPixel(px, py, atlasImage:getPixel(math.floor(px / 6), math.floor(py / 6)))
  end
end
local preview = Sprite(atlasWidth * 6, atlasHeight * 6, ColorMode.RGB)
preview:newCel(preview.layers[1], 1, previewImage, Point(0, 0))
preview:saveAs(p .. "brawler-passives-atlas-4x4-nearest6x-768x768.png")
preview:close()
atlas:close()

print("Awanturnik passive export complete: " .. exported .. " exported, " .. missing .. " missing, 4x4 atlas")
