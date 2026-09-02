# Canonicalny format CSV contentu

`Godmist/Content/Import Preview` przyjmuje pliki `.csv`, `.tsv` i `.xlsx`. Dla
nowego contentu rekomendowany jest CSV UTF-8 z nagłówkiem w pierwszym wierszu.
Separator CSV może być przecinkiem albo średnikiem; pola zawierające separator,
nową linię lub cudzysłów należy otoczyć cudzysłowami.

## Najważniejsze zasady

- Kolumna `ID` z arkuszy projektowych nie jest używana do wyznaczania ID assetu.
- ID jest generowane z nazwy zgodnie z konwencją projektu i można je zmienić w
  tabeli preview przed zastosowaniem importu.
- Rekord z takim samym ID jest traktowany jako `UPDATE` tylko wtedy, gdy asset
  istnieje w aktualnie wybranym `GameContentCatalog`.
- Asset spoza aktywnego katalogu nie jest automatycznie aktualizowany.
- `CREATE` i `UPDATE` są zatwierdzane osobno. Import nie zapisuje rekordów
  oznaczonych jako `ERROR` lub `CONFLICT`.
- Stabilne ID można podać jawnie w kolumnie `id` (alternatywnie
  `content_id`, `canonical_id` albo `asset_id`). Numeryczna kolumna `ID` ze
  starych arkuszy pozostaje ignorowana jako numer wiersza.

## Obsługiwane rekordy v1

Każdy plik może mieć prosty, tabelaryczny układ. `name` jest nazwą wyświetlaną;
aliasy używane w istniejących arkuszach (np. `Nazwa przedmiotu`, `Cena`, `Klasa`)
również są rozpoznawane.

```csv
id,name,item_type,price,weight,rarity,stackable
item.material.smithing.iron_ore,Iron Ore,Material,5,0.5,Common,true
item.consumable.healing_potion,Healing Potion,Potion,25,0.2,Common,true
```

Dla consumabli można dodać `use_type` (`Potion`, `Bomb`, `Utility`) i
`max_charges`. Dla craftowalnych: `crafting_discipline`, `output_amount` oraz
jedną lub wiele par kolumn `ingredient_N` i `ingredient_N_amount`, na przykład
`ingredient_1`, `ingredient_1_amount`, `ingredient_2`, `ingredient_2_amount`.
Są też obsługiwane aliasy `ingredient` i `ingredient_amount`. Każdy składnik
musi wskazywać materiał przez ID albo jednoznaczną nazwę assetu obecnego w
aktywnym katalogu.

```csv
id,name,item_type,use_type,max_charges,price
item.consumable.healing_potion,Healing Potion,Potion,1,25
```

```csv
name,item_type,crafting_discipline,ingredient_1,ingredient_1_amount,ingredient_2,ingredient_2_amount,output_amount
Iron Chain,Material,Smithing,item.smithing.iron_ingot,3,item.smithing.coal,1,1
```

Dla części equipmentu używane są pola `class`, `item`, `part`, `material`,
`level_bracket`, `attack`, `crit_chance`, `crit_damage`, `accuracy` i
`max_resource`. Wartości zakresowe, np. `8-12`, są mapowane na minimalną i
maksymalną statystykę.

Dla skilli używane są `name`, `description`, `skill_accuracy`, `action_cost`,
`resource_cost` i `resource_type`. Wiersze z istniejącego arkusza
`Umiejętności aktywne / Current` są automatycznie przekształcane z układu
macierzowego.

Dla postaci używane są `name`, `class`, `hp`, `min_attack`, `max_attack`,
`physical_defense`, `magical_defense`, `dodge`, `speed`, `max_resource`, `xp` i
`gold`.

## Dane relacyjne i przyszłe rozszerzenia

Efekty skilli, tryby celu, drzewa skilli, loot table i lootbagi nie powinny być
upychane w polu tekstowym. Dla tych danych należy przygotować osobne CSV z
referencjami, np.:

```text
skills.csv             skill_id,display_name,action_cost,resource_cost
skill_effects.csv      skill_id,order,effect_type,base_value,status_id
skill_targets.csv      skill_id,order,target_type,targeting_type
skill_trees.csv        tree_id,display_name
skill_tree_nodes.csv   tree_id,skill_id,order,required_skill_id
characters.csv         character_id,display_name,class_id
character_skills.csv   character_id,skill_id,order
loot_tables.csv        loot_table_id,display_name
loot_entries.csv       loot_table_id,item_id,min_amount,max_amount,chance
lootbags.csv           lootbag_id,display_name
lootbag_entries.csv    lootbag_id,loot_table_id,order
```

Wersja v1 pokazuje takie nieobsługiwane relacje jako ostrzeżenie zamiast
interpretować swobodny opis i tworzyć nieprzewidywalny runtime state. Ten sam
kontrakt pozwala później dodać adaptery dla kolejnych typów bez zmiany modeli
ScriptableObjectów.
