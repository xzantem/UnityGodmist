"""Read-only CP2-04 baseline analysis; does not author Unity or XLSX data."""
import hashlib
import json
import math
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
ROOT = PROJECT.parent
ASSETS = PROJECT / 'Assets/_Project'


def fields(text):
    return {m[1]: float(m[2]) for m in re.finditer(
        r'^\s*<([^>]+)>k__BackingField: (-?\d+(?:\.\d+)?)\s*$', text, re.M)}


def main():
    source = ASSETS / 'Resources/GameDefines.asset'
    raw = source.read_text(encoding='utf-8')
    config = {}
    for block in raw.split('  - Id: ')[1:]:
        key = block.splitlines()[0]
        value_type = int(re.search(r'ValueType: (\d+)', block)[1])
        if value_type not in (0, 1):
            continue
        kind = 'Integer' if value_type == 0 else 'Float'
        config[key] = float(re.search(rf'Current{kind}Value: ([^\n]+)', block)[1])
    xp = lambda level: round(config['progression.experience.formula_coefficient']
        * math.pow(level, config['progression.experience.formula_exponent'])
        + config['progression.experience.formula_constant'])
    points = lambda level: 0 if level == 1 else int(
        config['progression.skill_points.level_two']
        + (level - 2) * config['progression.skill_points.other_levels'])
    band_keys = [(10, 'level_2_10'), (20, 'level_11_20'),
                 (30, 'level_21_30'), (40, 'level_31_40')]
    # Reject changed band endpoints rather than silently apply the old model.
    assert [int(config[f'progression.growth.{k}_end_level']) for k in
            ['early', 'mid', 'late', 'endgame']] == [10, 20, 30, 40]
    bands = lambda level: 0 if level == 1 else next(
        int(config[f'progression.growth.{key}']) for end, key in band_keys if level <= end)
    rows = []
    cumulative_xp = cumulative_growth = 0
    for level in range(1, 41):
        cumulative_growth += bands(level)
        rows.append({'level': level, 'xp_to_next_at_current_cap': xp(level),
            'cumulative_xp_from_level_1': cumulative_xp,
            'total_earned_sp': points(level), 'growth_band_coefficient': bands(level),
            'cumulative_growth_units': cumulative_growth})
        cumulative_xp += xp(level)

    heroes = []
    for name in ['CD_Jerzy', 'CD_Wladimir_Test', 'CD_Mage', 'CD_Paladin']:
        path = ASSETS / f'ScriptableObjects/Characters/{name}.asset'
        text = path.read_text(encoding='utf-8')
        base = text.split('  <BaseStats>k__BackingField:\n')[1].split('  <BaseResistances>')[0]
        growth = text.split('  <GrowthData>k__BackingField:\n')[1].split('  <SecondPhaseDefinition>')[0]
        b, g = fields(base), fields(growth)
        assert '<HealthCurve>k__BackingField: []' in growth
        assert '<DefenseCurve>k__BackingField: []' in growth
        assert all(value == 0 for key, value in g.items() if key.startswith(('Late', 'Early')))
        checkpoints = []
        for row in rows:
            if row['level'] not in [1, 20, 21, 25, 30, 31, 35, 40]:
                continue
            u = row['cumulative_growth_units']
            checkpoints.append({'level': row['level'], 'unmodified_base_stats': {
                stat: round(b[stat] + g.get(stat, 0) * u, 4) for stat in
                ['MaxHealth', 'MinAttack', 'MaxAttack', 'PhysicalDefense', 'MagicalDefense', 'Dodge']}})
        heroes.append({'definition': str(path.relative_to(ROOT)),
            'character_id': re.search(r'^  <Id>k__BackingField: (.+)$', text, re.M)[1],
            'base': b, 'growth': g, 'checkpoints': checkpoints})

    checks = []
    for path in sorted((ASSETS / 'ScriptableObjects/Skills/SkillTrees').glob('*.asset')):
        text = path.read_text(encoding='utf-8')
        if '<Nodes>k__BackingField:' not in text:
            continue
        tree_id = re.search(r'^  <Id>k__BackingField: (.+)$', text, re.M)[1]
        nodes = []
        node_section = text.split('  <Nodes>k__BackingField:', 1)[1]
        for block in re.split(r'^  - <Id>k__BackingField: ', node_section, flags=re.M)[1:]:
            f = fields(block)
            nodes.append({'id': block.splitlines()[0], 'tier': int(f['Tier']),
                'column': int(f['Column']), 'max_ranks': int(f['MaxRanks']),
                'requirements': [(m[1], int(m[2])) for m in re.finditer(
                    r'<NodeId>k__BackingField: (\S+)\s+<MinimumRanks>k__BackingField: (\d+)', block)]})
        ranks, sequence = {}, []
        for _ in range(29):
            ready = [n for n in nodes if n['tier'] <= 4
                and sum(ranks.values()) >= (0 if n['tier'] <= 2 else
                    config['progression.skill_tree.tier_3_4_required_points'])
                and ranks.get(n['id'], 0) < n['max_ranks']
                and all(ranks.get(id, 0) >= count for id, count in n['requirements'])]
            if not ready:
                break
            n = min(ready, key=lambda n: (n['tier'], n['column'], n['id']))
            ranks[n['id']] = ranks.get(n['id'], 0) + 1
            sequence.append(n['id'])
        checks.append({'tree': tree_id, 'legal_existing_rank_sequence': sequence[:20],
            'legal_existing_29_rank_sequence': sequence,
            'twenty_points_reachable_without_T5': len(sequence) >= 20})
    assert len(checks) == 9 and all(c['twenty_points_reachable_without_T5'] for c in checks)
    assert len(rows) == 40 and points(21) == 21 and points(40) == 40
    assert all(rows[i+1]['cumulative_xp_from_level_1'] ==
        rows[i]['cumulative_xp_from_level_1'] + rows[i]['xp_to_next_at_current_cap']
        for i in range(39))
    manifest = json.loads((HERE/'content-manifest.json').read_text(encoding='utf-8'))
    paths = []
    examples = [('warrior', 'tree.warrior.commander', 'tree.warrior.brawler', 'T5:3'),
                ('scout', 'tree.scout.assassin', 'tree.scout.blade_dancer', 'T5:2'),
                ('mage', 'tree.mage.fire', 'tree.mage.warlock', 'T5:5'),
                ('paladin', 'tree.paladin.priest', 'tree.paladin.crusader', 'T5:2')]
    by_tree = {c['tree']: c for c in checks}
    for class_id, primary, secondary, slot in examples:
        target = next(n for n in manifest['other_content'] if n.get('kind') == 'SkillTreeNode'
            and n.get('tree') == primary and n.get('slot') == slot)
        assert target['status'] == 'specified' and not target.get('requirements')
        a = by_tree[primary]['legal_existing_29_rank_sequence']
        b = by_tree[secondary]['legal_existing_rank_sequence']
        assert len(a) == 29 and len(b) >= 10
        schedule = [(primary, n) for n in a[:20]] + [(primary, target['id'])]
        schedule += [(secondary, n) for n in b[:9]]
        schedule += [(primary, n) for n in a[20:29]] + [(secondary, b[9])]
        assert len(schedule) == 40
        paths.append({'class': class_id, 'primary': primary, 'secondary': secondary,
            'new_T5': target, 'scope': 'Planning feasibility; T5 requires its owning implementation card.',
            'investments': [{'point': i, 'earliest_level': max(2, i), 'tree': tree, 'node': node}
                for i, (tree, node) in enumerate(schedule, 1)]})
    output = {'status': 'APPROVED_CONTRACT', 'runtime_changed': False,
        'decisions': {'content_scope_max_level': 40, 'character_cap': 50,
            'keep_existing_xp_sp_growth_and_tier_thresholds': True,
            'all_five_biomes_eligible_from_start_when_authored': True,
            'new_biome_assets_owned_by_cards': [31, 36, 41],
            'new_expedition_level_cap_introduced_by_this_card': False},
        'workbook_sha256': hashlib.sha256((ROOT/'design/content_pass_2.xlsx').read_bytes()).hexdigest(),
        'config_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'source_config': str(source.relative_to(ROOT)),
        'current_character_cap': int(config['progression.character.maximum_level']),
        'config': {key: value for key, value in config.items() if key.startswith('progression.')},
        'levels': rows, 'heroes': heroes, 'existing_tree_budget_checks': checks,
        'example_build_paths': paths,
        'limitations': ['Values extracted from current serialized config, not a Unity runtime test.',
            'Hero stats exclude equipment, passives, rounding and final stat pipeline.',
            'Example build paths combine existing legal ranks with specified prerequisite-free T5 entries, not runtime T5 assets.',
            'XP on L40 uses the approved existing L50 character cap.',
            'Level table is not an estimate of expedition duration.']}
    (HERE/'progression-plan.json').write_text(json.dumps(output, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    keys = list(rows[0])
    (HERE/'progression-levels.txt').write_text('|'.join(keys)+'\n'+'\n'.join(
        '|'.join(str(row[key]) for key in keys) for row in rows)+'\n', encoding='utf-8')
    print('CP2-04 contract: 40 levels, 4 hero profiles, 9 tree budgets and 4 example build paths PASS.')
    for row in rows:
        if row['level'] in [1, 20, 21, 25, 30, 31, 35, 40]:
            print(row)


if __name__ == '__main__':
    main()
