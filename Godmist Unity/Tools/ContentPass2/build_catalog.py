"""Read-only XLSX/Unity catalog extraction for CP2-02; never authors Unity assets.

Run with the bundled Python (openpyxl). Output: JSON manifests and a coverage report.
The proposed-ID registry is written only with --reserve-ids; existing IDs always win.
"""
from __future__ import annotations

import argparse
import ast
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unicodedata

import openpyxl

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT.parent
HERE = Path(__file__).resolve().parent
ASSETS = PROJECT / "Assets/_Project/ScriptableObjects"
WORKBOOK = ROOT / "design/content_pass_2.xlsx"
AVAILABLE = {"Katakumby", "Las", "Elfie Ruiny", "Morze", "Bagno"}
BAGS = {"Koścista": "Katakumby", "Leśna": "Las", "Demoniczna": "Elfie Ruiny",
        "Morska": "Morze", "Bagienna": "Bagno", "Pustynna": "Pustynia",
        "Świątynna": "Świątynia", "Górska": "Góry"}
ALIASES = {"sztabka złotu": "sztabka złota", "włókno konopii": "włókno konopi",
           "nitka z wielbładziej nitki": "nitka z wielbłądziej wełny",
           "sztuczny zab": "sztuczny ząb", "skóra skorpiona": "skóra wielbłąda"}
PREFERRED = {"ucho elfa": "item.vs10.ucho_elfa"}
LATE_FALLBACKS = {"Krew berserka", "Zaczarowana dłoń", "Energia Malada", "Łuski wiwerny"}


def norm(value):
    value = " ".join(str(value or "").split()).casefold()
    return ALIASES.get(value, value)


def slug(value):
    value = str(value).replace("ł", "l").replace("Ł", "L")
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode().lower()
    return re.sub(r"[^a-z0-9]+", "_", value).strip("_")


def decode(value):
    value = value.strip()
    if value.startswith('"'):
        return ast.literal_eval(re.sub(r"\s*\n\s*", " ", value))
    return value


def field(text, key, default=""):
    # Root fields only. Nested node/recipe fields must not replace the owning ID.
    match = re.search(r"^  (?:<" + re.escape(key) + r">k__BackingField|" + re.escape(key)
                      + r"): (\"(?:[^\"\\]|\\.)*\"|[^\n]*)", text, re.M)
    return decode(match[1]) if match else default


def block(text, key):
    match = re.search(r"^  <" + re.escape(key) + r">k__BackingField:(.*?)(?=^  [^ \n-].*?:|\Z)", text, re.M | re.S)
    return match[1] if match else ""


def guids(text):
    return re.findall(r"guid: ([a-f0-9]+)", text)


def ingredients(text):
    return [{"guid": m[1], "amount": int(m[2])} for m in re.finditer(
        r"<Material>k__BackingField: \{[^\n]*guid: ([a-f0-9]+)[^\n]*\}\s*\n\s*<Amount>k__BackingField: (\d+)", text)]


def baseline():
    assets = {}
    for path in ASSETS.rglob("*.asset"):
        meta = Path(str(path) + ".meta")
        if not meta.exists():
            continue
        match = re.search(r"^guid: ([a-f0-9]+)", meta.read_text(), re.M)
        if not match:
            continue
        text = path.read_text(encoding="utf-8-sig")
        assets[match[1]] = {"guid": match[1], "path": path.relative_to(ROOT).as_posix(),
                             "id": field(text, "Id") or field(text, "SkillId"),
                             "name": field(text, "DisplayName"), "type": field(text, "m_EditorClassIdentifier"),
                             "item_type": field(text, "ItemType"), "value": field(text, "BaseValue"),
                             "recipe_level": int(field(text, "ResultLevel", "1")), "_text": text}
    text = (ASSETS / "World/GC_MainContentCatalog.asset").read_text(encoding="utf-8-sig")
    catalogs = {key: guids(block(text, key)) for key in ["ItemDefinitions", "CharacterDefinitions",
        "CharacterClasses", "EquipmentPartDefinitions", "SkillDefinitions", "DungeonDefinitions"]}
    missing = [(key, guid) for key, refs in catalogs.items() for guid in refs if guid not in assets]
    return assets, {k: [g for g in refs if g in assets] for k, refs in catalogs.items()}, missing


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--reserve-ids", action="store_true")
    args = parser.parse_args()
    assets, catalogs, missing_catalog_refs = baseline()
    workbook = openpyxl.load_workbook(WORKBOOK, read_only=False, data_only=True)
    registry_path = HERE / "proposed-ids.json"
    registry = json.loads(registry_path.read_text(encoding="utf-8")) if registry_path.exists() else {}
    unique_names = {norm(n.strip()) for row in range(107,113)
                    for n in re.split(r"[;,]", str(workbook['Lootbagi'][f'F{row}'].value or '')) if n.strip() != '?'}
    by_name = collections.defaultdict(list)
    for guid in catalogs["ItemDefinitions"]:
        item = assets[guid]
        by_name[norm(item["name"])].append(item)
    entities, sources, recipes, consumers, issues = {}, [], [], [], []
    all_ids = {a["id"] for a in assets.values() if a["id"]}

    def issue(code, message, references, severity="review"):
        entry = {"code": code, "message": message, "references": references, "severity": severity}
        if entry not in issues:
            issues.append(entry)

    def ref(sheet, cell):
        return f"{sheet}!{cell}"

    historical_tests = {"b6d1f8a3c5e74920a4d7b2f9c6e1a580":"ID_TestGalduryte", "c7e2a9b4d6f85130a5c8e3f1b7d9a260":"ID_TestPotion", "d8f3b1a6c9e24750a4d7e2f8b5c1a690":"ID_TestValuable"}
    for catalog, guid in missing_catalog_refs:
        # Only these three are verified obsolete fixtures; unknown broken refs still block.
        issue("OBSOLETE_TEST_CATALOG_GUID" if guid in historical_tests else "MISSING_RUNTIME_CATALOG_GUID", f"{catalog}: {guid}",
              ["Godmist Unity/Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset",
               f"0ae2dc088:ScriptableObjects/Items/{historical_tests.get(guid,'UNKNOWN')}.asset.meta"],
              "baseline_cleanup" if guid in historical_tests else "blocker")

    def reserve(key, proposal, name):
        if key not in registry:
            if not args.reserve_ids:
                raise ValueError(f"Unreserved entity {key}; inspect and run --reserve-ids")
            if proposal in all_ids or any(v["id"] == proposal for v in registry.values()):
                raise ValueError(f"Proposed ID collision: {proposal}")
            registry[key] = {"id": proposal, "first_name": name, "status": "proposed_not_runtime"}
        return registry[key]["id"]

    def resolve(name, provenance=None):
        canonical = norm(name)
        matches = by_name[canonical]
        preferred = PREFERRED.get(canonical)
        if preferred:
            matches = [a for a in matches if a["id"] == preferred]
        if len(matches) > 1:
            issue("AMBIGUOUS_ID", f"{name}: {[a['id'] for a in matches]}", [provenance] if provenance else [], "blocker")
            return None
        if matches:
            asset = matches[0]
            ident = asset["id"]
            if not ident:
                raise ValueError(f"Registered item without ID: {asset['path']}")
            if ident not in entities:
                entities[ident] = {k: v for k, v in asset.items() if not k.startswith("_")}
                entities[ident].update(status="existing", references=[])
        else:
            key = "item:" + canonical
            proposal = {"jadeit": "item.runic.jadeite", "jaspis": "item.runic.jasper",
                        "perła": "item.runic.pearl", "malachit": "item.runic.malachite"}.get(canonical,
                            ("item.unique." if canonical in unique_names else "item.material.") + slug(canonical))
            if canonical == "mikstura oczyszczenia":
                proposal = "item.potion.cleansing"
            ident = reserve(key, proposal, name)
            entities.setdefault(ident, {"id": ident, "name": name, "status": "proposed_not_runtime", "references": []})
        if provenance and provenance not in entities[ident]["references"]:
            entities[ident]["references"].append(provenance)
        return ident

    # Inventory every registered material, including legacy entries kept for old saves.
    for guid in catalogs["ItemDefinitions"]:
        a = assets[guid]
        if a["item_type"] == "3":
            ident = a["id"]
            entities[ident] = {k: v for k, v in a.items() if not k.startswith("_")}
            entities[ident].update(status="existing", references=[])
            if "pył energetyczny" in norm(a["name"]) or ident in {"item.material.elf_ear", "item.material.bandit_leader_blood"}:
                entities[ident]["status"] = "legacy_keep_saves_no_new_sources"

    s = workbook["Nowe przedmioty"]
    for row in range(2, s.max_row + 1):
        name, level = s[f"C{row}"].value, s[f"E{row}"].value
        if not name or not isinstance(level, (int, float)) or not 1 <= level <= 40:
            continue
        ident = resolve(name, ref(s.title, f"C{row}"))
        if not ident:
            continue
        entities[ident].update(planned_level=int(level), planned_value=s[f"G{row}"].value,
            planned_category=s[f"F{row}"].value, source_text=s[f"D{row}"].value)
        if s[f"I{row}"].value:
            entities[ident]['planned_effect_description'] = s[f"I{row}"].value
        recipe_text = str(s[f"D{row}"].value or "")
        if "->" in recipe_text or re.match(r"\d+x ", recipe_text):
            text = s[f"D{row}"].value
            for index, variant in enumerate(text.split(" / ")):
                left, right = (variant.split("->", 1) + [""])[:2] if "->" in variant else (variant, "")
                inputs = []
                for term in left.split(" + "):
                    m = re.fullmatch(r"\s*(\d+)x\s+(.+?)\s*", term)
                    if not m:
                        issue("RECIPE_SYNTAX", term, [ref(s.title, f"D{row}")], "blocker")
                        continue
                    if "Sproszkowany metal lvl" in m[2]:
                        inputs.append({"selector": "powdered_metal_l21", "amount": int(m[1])})
                    else:
                        inputs.append({"id": resolve(m[2], ref(s.title, f"D{row}")), "amount": int(m[1])})
                amount = re.match(r"\s*(\d+)x", right)
                recipes.append({"output": ident, "variant": index, "level": int(level), "inputs": inputs,
                    "amount": int(amount[1]) if amount else None, "origin": "workbook", "reference": ref(s.title, f"D{row}")})

    # Existing recipes retain their variant structure, rather than requiring all alternatives at once.
    for guid in catalogs["ItemDefinitions"]:
        a = assets[guid]
        if "CraftableItemDefinition" not in a["type"]:
            continue
        variants = [block(a["_text"], "Ingredients")]
        variants += re.split(r"^  - Label:.*$", block(a["_text"], "AlternativeRecipes"), flags=re.M)[1:]
        for index, variant in enumerate(variants):
            inputs = [{"id": assets.get(i["guid"], {}).get("id"), "amount": i["amount"]} for i in ingredients(variant)]
            recipes.append({"output": a["id"], "variant": index, "level": a["recipe_level"],
                "inputs": inputs, "amount": int(field(a["_text"], "OutputAmount", "1")), "origin": "runtime", "reference": a["path"]})

    s = workbook["Lootbagi"]
    for row in range(3, s.max_row + 1):
        owner, levels, category, kind, content = [s.cell(row, c).value for c in range(2, 7)]
        if not owner or not content or not isinstance(levels, str):
            continue
        match = re.fullmatch(r"(\d+)[–-](\d+)", levels)
        if not match or int(match[1]) > 40 or category == "Unikaty":
            continue
        for name in content.split(";"):
            name = name.strip()
            if row == 42 and name == "Skóra aligatora":
                name = "Skóra leoparda"  # Explicit user decision, 2026-10-31.
            ident = resolve(name, ref(s.title, f"F{row}"))
            if not ident:
                continue
            low, high = map(int, match.groups())
            if name in LATE_FALLBACKS:
                low = 31  # Explicit prior CP2 decision, not inferred from item level.
            sources.append({"id": ident, "owner": owner, "kind": kind, "min_level": low,
                "max_level": min(40, high), "origin": "workbook", "reference": ref(s.title, f"B{row}:F{row}"),
                "classification": "fallback" if "Zastępcza" in kind or name in LATE_FALLBACKS else "natural",
                "runtime_verified": False})

    # Explicit user decision: high-tier gunpowder is available in all five biomes.
    for name, low, high in [("Dobry proch strzelniczy", 21, 30), ("Mocny proch strzelniczy", 31, 40)]:
        for owner in sorted(AVAILABLE - {"Katakumby"}):
            sources.append({"id": resolve(name), "owner": owner, "kind": "Torba zasobów", "min_level": low,
                "max_level": high, "origin": "user_decision_2026_10_31", "classification": "natural",
                "reference": "Nowe przedmioty!D158:D159 + odpowiedź użytkownika 2026-10-31", "runtime_verified": False})

    # The integrated, accepted boss gems are retained even where the workbook omits them.
    for name, owners in [("Obsydian", ["Katakumby - Szkielet Zabójca", "Katakumby - Duch Umarłego Króla"]),
                         ("Jadeit", ["Las - Zarażony Ent", "Las - Przywódca Bandytów"]),
                         ("Jaspis", ["Elfie Ruiny - Demoniczny Jarl", "Elfie Ruiny - Cerber"]),
                         ("Perła", ["Morze - Alfa-Rekin", "Morze - Kapitan Piratów"]),
                         ("Malachit", ["Bagno - Bagnisty Ent", "Bagno - Ludożerna Roślina"])]:
        ident = resolve(name)
        for owner in owners:
          for low,high in ([(1,10),(11,20),(21,40)] if name in {"Obsydian","Jadeit"} else [(1,40)]):
            sources.append({"id": ident, "owner": owner, "kind": "Torba bossa", "min_level": low,
                "max_level": high, "classification": "natural", "origin": "accepted_cp2_01" if name in {"Obsydian", "Jadeit"} else "workbook_biome_bosses",
                "reference": "CP2-01/BossGemLootSetup" if name in {"Obsydian", "Jadeit"} else "Biżuterie i Galduryty!O3:Q5 + decyzja użytkownika 2026-10-31: bossy L1–40",
                "drop_range_status":"preserved_runtime" if name in {"Obsydian","Jadeit"} and high<=20 else "planned_extension" if name in {"Obsydian","Jadeit"} else "user_confirmed_2026_10_31",
                "runtime_verified": name in {"Obsydian","Jadeit"} and high<=20})

    # Actual runtime bags: inventory provenance, not a declaration that L21-40 works today.
    runtime_sources = []
    for guid in catalogs["ItemDefinitions"]:
        a = assets[guid]
        if "LootBagItemDefinition" not in a["type"]:
            continue
        for segment in re.split(r"^  - <Bracket>k__BackingField: ", block(a["_text"], "Brackets"), flags=re.M)[1:]:
            bracket = int(segment.splitlines()[0])
            for material in re.findall(r"<ItemDefinition>k__BackingField: \{[^\n]*guid: ([a-f0-9]+)", segment):
                runtime_sources.append({"id": assets.get(material, {}).get("id"), "bag_id": a["id"],
                    "bracket": bracket, "reference": a["path"]})

    # Consumers include every specified equipment-part material and B/C alchemy ingredient.
    s = workbook["Nowe wyposażenie"]
    parts = []
    for row in range(3, s.max_row + 1):
        for side, name_col, part_col, mat_col, lvl_col in [("weapon", "C", "D", "E", "F"), ("armor", "L", "M", "N", "O")]:
            if s[f"{lvl_col}{row}"].value not in (25, 35):
                continue
            material = resolve(s[f"{mat_col}{row}"].value, ref(s.title, f"{mat_col}{row}"))
            part = {"class": s[f"B{row}"].value, "side": side, "name": s[f"{name_col}{row}"].value,
                "part": s[f"{part_col}{row}"].value, "level": s[f"{lvl_col}{row}"].value, "material": material,
                "reference": ref(s.title, f"{mat_col}{row}")}
            parts.append(part)
            class_token = {"Wojownik":"warrior", "Zwiadowca":"scout", "Czarodziej":"mage", "Paladyn":"paladin"}[part["class"]]
            kind = {"weapon": {"Głownia":"head", "Katalizator":"head", "Jelec":"binder", "Osłona":"binder", "Owicie":"binder", "Rękojeść":"handle", "Trzon":"handle"},
                    "armor": {"Napierśnik":"plate", "Materiał":"plate", "Kolcza":"binder", "Rzemienie":"binder", "Pas":"binder", "Skóra":"base", "Umocnienia":"base", "Koszula":"base", "Naplecznik":"base"}}[side][part["part"]]
            key = f"part:{class_token}:{side}:{kind}:{part['level']}:{material}"
            part["id"] = reserve(key, f"part.{class_token}.{side}.{kind}.l{part['level']}.{material.split('.')[-1]}", f"{part['name']} / {part['part']} / {s[f'{mat_col}{row}'].value}")
            consumers.append({"id": material, "consumer": "equipment_part", "level": part["level"], "reference": part["reference"]})
    s = workbook["Nowe przedmioty"]
    alchemy = []
    for row in list(range(4, 16)) + list(range(19, 31)):
        if not s[f"Q{row}"].value:
            continue
        item = {"kind": "potion" if row < 16 else "bomb", "effect": s[f"Q{row}"].value, "reference": ref(s.title, f"Q{row}:W{row}")}
        for column in "TUV":
            value = s[f"{column}{row}"].value
            if value:
                ident = resolve(value, ref(s.title, f"{column}{row}"))
                item[column] = ident
                consumers.append({"id": ident, "consumer": "alchemy_B" if column != "V" else "alchemy_C", "level": 21 if column != "V" else 31, "reference": ref(s.title, f"{column}{row}")})
        alchemy.append(item)

    # Current consumers, needed to flag removal of legacy source-only materials.
    for recipe in recipes:
        for value in recipe["inputs"]:
            if "id" in value:
                consumers.append({"id": value["id"], "consumer": recipe["output"], "level": recipe["level"], "reference": recipe["reference"]})

    # Preserve natural future-biome ownership from the previously accepted material contract.
    # It never supplies fallback drops or replaces the current workbook.
    historical_path = HERE / "natural-source-contract.json"
    historical = json.loads(historical_path.read_text(encoding="utf-8"))["assignments"]
    # Runtime trade references are concrete stock entries, not inferred from material discipline.
    stock = []
    for path in (ASSETS / "NPC/Roles").glob("TRD_*.asset"):
        text = path.read_text(encoding="utf-8-sig")
        for m in re.finditer(r"Item: \{[^\n]*guid: ([a-f0-9]+)[^\n]*\}\s*\n\s+Limit: (\d+)\s*\n\s+MinimumLevel: (\d+)\s*\n\s+MaximumLevel: (\d+)", text):
            ident = assets.get(m[1], {}).get("id")
            if ident in entities:
                stock.append({"id": ident, "minimum_level": int(m[3]), "maximum_level": int(m[4]),
                    "limit": int(m[2]), "role": path.stem, "reference": path.relative_to(ROOT).as_posix()})

    # Non-material catalog entries and designed additions have separate identity records.
    additions = []
    def design_record(key, proposal, name, kind, reference, **extra):
        existing = [a for a in assets.values() if a["id"] and norm(a["name"]) == norm(name)
                    and kind in a["type"]]
        ident = existing[0]["id"] if len(existing) == 1 else reserve(key, proposal, name)
        result = {"id": ident, "name": name, "kind": kind, "reference": reference,
                  "status": "existing_definition" if len(existing) == 1 else "proposed_not_runtime", **extra}
        additions.append(result)
        return ident

    actor_biomes = ["Elfie Ruiny"] * 7 + ["Morze"] * 7 + ["Bagno"] * 7
    actor_ids = {"Niewolnik Demonów": "demon_slave", "Demoniczny Zbrojny": "demon_soldier",
        "Demoniczny Czarownik": "demon_warlock", "Chochlik": "imp", "Hawia'kalb": "hawia_kalb",
        "Demoniczny Iluzjonista": "demon_illusionist", "Eayan'masakh": "eayan_masakh",
        "Pirat Kombatant": "pirate_combatant", "Pirat Harpunnik": "pirate_harpooner",
        "Piaszczysty Feniks": "sand_phoenix", "Papuga": "parrot", "Człowiek-rekin": "sharkman",
        "Pirat Konsyliarz": "pirate_counsellor", "Pirat Kanonier": "pirate_gunner",
        "Mięsożerna Roślina": "carnivorous_plant", "Aligator": "alligator",
        "Pasożytnicza Roślina": "parasitic_plant", "Topielec": "drowned", "Dezerter": "deserter",
        "Strażnik Bagien": "swamp_guardian", "Plująca Roślina": "spitting_plant"}
    biome_ids = {"Elfie Ruiny": "elven_ruins", "Morze": "sea", "Bagno": "swamp"}
    s = workbook["Przeciwnicy"]
    for row, owner in zip(range(3, 24), actor_biomes):
        name = s[f"B{row}"].value
        actor = design_record("actor:" + name, f"enemy.{biome_ids[owner]}.{actor_ids[name]}", name,
                      "CharacterDefinition", ref(s.title, f"B{row}:AA{row}"), biome=owner)
        for slot,column in enumerate("XYZ",1):
            text = s[f"{column}{row}"].value
            if not text:
                issue("MISSING_ACTOR_SKILL", name, [ref(s.title,f"{column}{row}")], "blocker")
                continue
            ident = reserve(f"actor_skill:{actor}:{slot}", actor.replace('enemy.','skill.',1)+f".action{slot}", str(text).split(' - ',1)[0])
            additions.append({"id":ident,"kind":"SkillData","owner":actor,"slot":slot,
                "status":"specified_rebalance_required","description":text,"reference":ref(s.title,f"{column}{row}")})
    s = workbook["Bossowie"]
    for row, owner, token in [(4,"Elfie Ruiny","demonic_jarl"),(5,"Elfie Ruiny","cerberus"),
        (6,"Morze","alpha_shark"),(7,"Morze","pirate_captain"),(8,"Bagno","swamp_ent"),(9,"Bagno","carnivorous_plant")]:
        name = s[f"B{row}"].value
        first = design_record("boss:" + name, f"boss.{biome_ids[owner]}.{token}", name,
            "CharacterDefinition", ref(s.title, f"B{row}:AJ{row}"), biome=owner, phases=2)
        second = reserve("boss_phase2:"+name, first+".phase2", name+" F2")
        additions.append({"id": second, "name": name+" F2", "kind": "CharacterDefinition",
            "status": "proposed_not_runtime", "reference": ref(s.title, f"AF{13+(row-4)*2}:AJ{14+(row-4)*2}"), "parent": first, "biome": owner})
        for phase,phase_owner in [(1,first),(2,second)]:
            action_row = 13 + (row-4)*2 + phase-1
            for slot,column in enumerate(["AH","AI","AJ"],1):
                text = s[f"{column}{action_row}"].value
                ident = reserve(f"boss_skill:{first}:{phase}:{slot}",f"skill.{biome_ids[owner]}.{token}.phase{phase}.action{slot}",str(text).split(' - ',1)[0])
                additions.append({"id":ident,"kind":"SkillData","owner":phase_owner,"slot":slot,
                    "status":"specified_rebalance_required","description":text,"reference":ref(s.title,f"{column}{action_row}")})

    trees = {"Dowódca": "tree.warrior.commander", "Awanturnik": "tree.warrior.brawler",
        "Zbrojny": "tree.warrior.man_at_arms", "Skrytobójca": "tree.scout.assassin",
        "Zabójca": "tree.scout.assassin", "Tancerz Ostrzy": "tree.scout.blade_dancer",
        "Strażnik": "tree.scout.guardian", "Czarnoksiężnik": "tree.mage.warlock",
        "Mag Ognia": "tree.mage.fire", "Mag Mrozu": "tree.mage.frost", "Kapłan": "tree.paladin.priest",
        "Krzyżowiec": "tree.paladin.crusader", "Crusader": "tree.paladin.crusader"}
    s = workbook["Drzewka"]
    for row in range(2, 162):
        label, slot, name = [s[f"{c}{row}"].value for c in "ABC"]
        if not slot:
            continue
        label = str(label or "").strip()
        if row >= 136:
            tree = "plan.tree.paladin.third"
        else:
            tree = trees.get(label)
        if not tree:
            issue("UNKNOWN_TREE_LABEL", label, [ref(s.title,f"A{row}")], "blocker")
            continue
        key = tree + ":" + str(slot)
        if name:
            clean = re.sub(r"\s*\[[^]]*\]\s*$", "", str(name))
            is_global = slot == "Pasyw"
            node_id = reserve("node:"+key, ("passive." if is_global else "node.")
                              + tree.removeprefix("tree.") + "." + slug(slot), clean)
            status = "design_review_guardian" if tree == "tree.scout.guardian" else "scope_decision_t6_t7" if str(slot).startswith(("T6", "T7")) else "specified"
            additions.append({"id": node_id, "name": clean, "kind": "GlobalPassive" if is_global else "SkillTreeNode", "tree": tree,
                "slot": slot, "status": status, "reference": ref(s.title,f"A{row}:G{row}"), "declared_rank_or_cost": name,
                "description": s[f"D{row}"].value, "requirements": s[f"E{row}"].value})
            if re.search(r"\d+\s+P[MWP]",str(name)):
                skill = reserve("node_skill:"+key,"skill."+tree.removeprefix('tree.')+'.'+slug(slot),clean)
                additions[-1]['granted_skill'] = skill
                additions.append({"id":skill,"name":clean,"kind":"SkillData","owner":node_id,
                    "status":status,"reference":ref(s.title,f"C{row}:G{row}")})
        else:
            additions.append({"plan_key": key, "id": None, "kind": "GlobalPassive" if slot == "Pasyw" else "SkillTreeNode", "tree": tree,
                "slot": slot, "status": "design_deferred_paladin", "reference": ref(s.title,f"A{row}:G{row}")})
    # Tree identity is separate from individual node labels; an existing unassigned Guardian is reused.
    for label in ["Zbrojny", "Strażnik", "Mag Mrozu"]:
        tree = trees[label]
        current = [a for a in assets.values() if a["id"] == tree]
        if not current:
            tree = reserve("tree:"+label, tree, label)
        additions.append({"id": tree, "name": label, "kind": "SkillTreeDefinition",
            "status": "existing_definition_review_assignment" if current else "proposed_not_runtime"})

    unique_slots = []
    s = workbook["Lootbagi"]
    for row in range(103, 113):
        names = re.split(r"[;,]", s[f"F{row}"].value)
        for number, name in enumerate(names, 1):
            name = name.strip()
            entry = {"owner": s[f"B{row}"].value, "slot": number, "reference": ref(s.title,f"F{row}"),
                     "min_level": 1, "max_level": 40}
            if name == "?":
                entry.update(id=None, status="design_deferred_unique", plan_key=f"unique:{slug(entry['owner'])}:slot{number}")
            else:
                ident = resolve(name, entry["reference"])
                entry.update(id=ident, name=name, status="existing" if entities[ident]["status"] == "existing" else "effect_design_deferred")
                sources.append({"id":ident, "owner":entry["owner"], "kind":"Torba bossa - unikat",
                    "min_level":1,"max_level":40,"origin":"workbook","classification":"natural",
                    "reference":entry["reference"],"runtime_verified":False})
            unique_slots.append(entry)

    jewelry = []
    s = workbook["Biżuterie i Galduryty"]
    metal_materials = ["Sztabka annodytu","Sztabka srebra","Sztabka kobaltu","Sztabka złota",
                       "Sztabka kryferytu","Sztabka mosiądzu","Sztabka sułtańskiej stali","Sztabka tytanu"]
    for row in range(2,10):
        name = s[f"H{row}"].value
        level = 30 if row < 6 else 40
        material = resolve(metal_materials[row-2],ref(s.title,f"H{row}:M{row}"))
        jewelry.append({"plan_key":"jewelry_metal:"+material,"name":name,"material_id":material,"level":level,
            "runtime_identity":"material ID + amount, existing JewelryMetalData contract; no separate metal definition ID",
            "effects":{c:s[f"{c}{row}"].value for c in "JKLM"},"reference":ref(s.title,f"H{row}:M{row}")})
        consumers.append({"id":material,"consumer":"jewelry_metals_four_slots","level":level,"reference":ref(s.title,f"H{row}:M{row}")})
    jewelry_gems = []
    for row in range(2,6):
        name = s[f"O{row}"].value; material=resolve(name,ref(s.title,f"O{row}:Q{row}"))
        existing=[a for a in assets.values() if 'JewelryGemDefinition' in a['type'] and entities[material].get('guid') in guids(field(a['_text'],'CraftingMaterial'))]
        jewelry_gems.append({'plan_key':'jewelry_gem:'+material,'material_id':material,'name':name,
            'effect':s[f'P{row}'].value,'asset_path':existing[0]['path'] if existing else None,
            'runtime_identity':'GemId is CraftingMaterial.Id; preserve existing JewelryGemDefinition model',
            'reference':ref(s.title,f'O{row}:Q{row}')})

    bags = []
    for owner in sorted({s["owner"] for s in sources if s["owner"].split(" - ")[0] in AVAILABLE}):
        current = {"Katakumby":"item.lootbag.catacombs.resources", "Las":"item.lootbag.forest.resources",
            "Katakumby - Szkielet Zabójca":"item.lootbag.catacombs.skeleton_slayer",
            "Katakumby - Duch Umarłego Króla":"item.lootbag.catacombs.dead_king",
            "Las - Zarażony Ent":"item.lootbag.forest.infected_ent", "Las - Przywódca Bandytów":"item.lootbag.forest.bandit_leader"}.get(owner)
        proposal = 'item.lootbag.'+({'Elfie Ruiny':'elven_ruins','Morze':'sea','Bagno':'swamp'}.get(owner.split(' - ')[0],slug(owner)))+'.'+(slug(owner.split(' - ')[1]) if ' - ' in owner else 'resources')
        bags.append({"id":current or reserve('bag:'+owner,proposal,owner),'owner':owner,
            'status':'existing' if current else 'proposed_not_runtime'})

    # Canonical aliases are explicit and auditable, never fuzzy matches or silent ID merges.
    for ident, entity in entities.items():
        entity["planned_sources"] = [s for s in sources if s["id"] == ident]
        entity["runtime_bags"] = [s for s in runtime_sources if s["id"] == ident]
        entity["consumers"] = [c for c in consumers if c["id"] == ident]
        entity["recipes"] = [r for r in recipes if r["output"] == ident]
        entity["runtime_stock"] = [s for s in stock if s["id"] == ident]
        entity["historical_natural_sources"] = [r for r in historical if norm(r["name"]) == norm(entity["name"])]
        natural = {x['owner'].split(' - ')[0] for x in entity['planned_sources'] if x['classification']=='natural' and x['owner']!='Handel NPC'}
        natural.update(x['owner'] for x in entity['historical_natural_sources'] if x['owner'] not in AVAILABLE)
        # Preserve future biome ownership without enabling those dungeons in this pass.
        source_text = str(entity.get('source_text',''))
        natural.update(owner for adjective,owner in BAGS.items() if adjective+' torba zasobów' in source_text)
        if entity['name']=='Skóra aligatora':
            natural.discard('Elfie Ruiny')
        entity['natural_biomes'] = sorted(natural)
        entity['natural_boss_source_text'] = source_text if source_text.startswith('Torba ') else None
        if entity["status"].startswith("legacy") and entity["planned_sources"]:
            issue("LEGACY_PLANNED_SOURCE", entity["name"], entity["references"], "blocker")

    # Fixed-point minimum planning level. Every alternative recipe is validated separately.
    planned_recipe_outputs = {r["output"] for r in recipes if r["origin"] == "workbook"}
    active_recipes = [r for r in recipes if r["origin"] == "workbook" or r["output"] not in planned_recipe_outputs]
    selectors = {"powdered_metal_l21": [resolve(n) for n in ["Sproszkowany annodyt", "Sproszkowane srebro", "Sproszkowany kobalt", "Sproszkowane złoto"]]}
    accessible = {}
    for source in sources:
        owner = source["owner"].split(" - ")[0]
        if owner in AVAILABLE or owner == "Handel NPC":
            accessible[source["id"]] = min(accessible.get(source["id"], 999),source["min_level"])
    for offer in stock:
        if offer["minimum_level"] <= 40:
            accessible[offer["id"]] = min(accessible.get(offer["id"],999), offer["minimum_level"])
    def input_level(value):
        if "selector" in value:
            return min((accessible.get(i,999) for i in selectors[value["selector"]]),default=999)
        return accessible.get(value["id"],999)
    changed = True
    while changed:
        changed = False
        for recipe in active_recipes:
            if not recipe["inputs"]:
                continue
            level = max(recipe["level"], *(input_level(v) for v in recipe["inputs"]))
            if level <= 40 and level < accessible.get(recipe["output"],999):
                accessible[recipe["output"]] = level
                changed = True
    out_of_scope = {resolve(n) for n in ["Lity marmur", "Perfekcyjny alkohol", "Perfekcyjny proch strzelniczy", "Marmurowy kamień galdurytu"]}
    for ident, entity in entities.items():
        entity["earliest_planned_level"] = accessible.get(ident)
        entity["scope"] = "legacy" if entity["status"].startswith("legacy") else "above_40" if ident in out_of_scope else "pass_2"
        if entity["scope"] == "pass_2" and ident not in accessible:
            issue("UNREACHABLE_PLANNED_ITEM", entity["name"], entity["references"], "blocker")
    for recipe in active_recipes:
        if recipe["output"] in out_of_scope:
            continue
        for inp in recipe["inputs"]:
            if recipe["origin"] == "workbook" and input_level(inp) > recipe["level"]:
                issue("RECIPE_LEVEL_OR_INPUT", f"{entities[recipe['output']]['name']} variant {recipe['variant']}: {inp}", [recipe["reference"]], "blocker")
        if recipe["amount"] is None:
            issue("YIELD_TO_CONFIRM_IN_AUTHORING", entities[recipe["output"]]["name"], [recipe["reference"]], "later_card_detail")

    for entry in historical:
        if entry["owner"] not in AVAILABLE:
            continue
        matching = [s for s in sources if s["owner"] == entry["owner"] and s["classification"] == "natural"
                    and norm(entities[s["id"]]["name"]) == norm(entry["name"]) and s["min_level"] <= entry["level"] <= s["max_level"]]
        if not matching:
            explicit_replacement = norm(entry["name"]) == "skóra aligatora" and entry["owner"] == "Elfie Ruiny"
            issue("HISTORICAL_NATURAL_SOURCE_REPLACED_BY_USER" if explicit_replacement else "HISTORICAL_NATURAL_SOURCE_CHANGED",
                  str(entry), [historical_path.relative_to(ROOT).as_posix()], "resolved_decision" if explicit_replacement else "review")

    report = {"schema_version": 1, "source_workbook": WORKBOOK.relative_to(ROOT).as_posix(),
        "workbook_sha256": hashlib.sha256(WORKBOOK.read_bytes()).hexdigest(),
        "natural_contract_sha256":hashlib.sha256(historical_path.read_bytes()).hexdigest(),
        "baseline_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "runtime_changed": False, "entities": list(entities.values()), "planned_sources": sources,
        "recipes": recipes, "active_recipes": active_recipes, "selectors": selectors,
        "equipment_parts": parts, "alchemy": alchemy, "other_content": additions,
        "unique_slots": unique_slots, "jewelry_metals":jewelry, "jewelry_gems":jewelry_gems, "bags":bags,
        "galduryte_rule":{"level":21,"effect_count":[3,4],"new_effect_kinds":False,
            "host_level_at_least_rune_level":True,"reference":"Biżuterie i Galduryty!B2:B6"}, "issues": issues,
        "decisions": ["Elfie Ruiny: tylko skóra leoparda, nie aligatora (użytkownik 2026-10-31)",
                      "Dobry/Mocny proch strzelniczy: wszystkie pięć biomów (użytkownik 2026-10-31)",
                      "Jaspis, Perła, Malachit: drop z bossów L1–40, niezależnie od poziomu materiału 11 (użytkownik 2026-10-31)",
                      "Cztery materiały zastępcze bossów dopiero L31–40 (przyjęty plan CP2-02)",
                      "Ucho elfa: nowe dropy canonical item.vs10.ucho_elfa; zachować legacy ID", "Usunięte pyły i Krew przywódcy pozostają wyłącznie dla zapisów"],
        "runtime_catalog_counts": {k: len(v) for k, v in catalogs.items()}}
    if args.reserve_ids:
        registry_path.write_text(json.dumps(registry, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    (HERE / "content-manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    def safe(value):
        return str(value or '').replace('|','/').replace('\n',' ')
    lines = ['ID|Nazwa|Stan|Zakres|Poziom w arkuszu|Dostęp w planie od|Biomy naturalne także przyszłe|Naturalne torby bossów z arkusza|Źródła bezpośrednie w pass 2|Receptury - składniki|Odbiorcy|Referencje']
    for e in sorted(entities.values(),key=lambda e:(e['scope'],norm(e['name']))):
        src = '; '.join(f"{x['owner']}: {x['kind']} L{x['min_level']}-{x['max_level']}" for x in e['planned_sources'])
        ingredients_text = '; '.join(' + '.join(entities[i['id']]['name'] if 'id' in i else 'Jeden ze sproszkowanych metali L21' for i in r['inputs']) for r in active_recipes if r['output']==e['id'])
        lines.append('|'.join(safe(v) for v in [e['id'],e['name'],e['status'],e['scope'],e.get('planned_level'),e.get('earliest_planned_level'),'; '.join(e['natural_biomes']),e['natural_boss_source_text'],src,ingredients_text,
            '; '.join(sorted({str(c['consumer']) for c in e['consumers']})), '; '.join(e['references'])]))
    (HERE/'material-sources.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8-sig')
    print(json.dumps({"entities": len(entities), "statuses": dict(collections.Counter(e["status"] for e in entities.values())),
        "parts": len(parts), "sources": len(sources), "recipes": len(recipes),
        "other_content": len(additions), "issue_counts":dict(collections.Counter((i['severity']+':'+i['code']) for i in issues))}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
