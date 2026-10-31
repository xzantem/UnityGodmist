"""Independent checks of the CP2 planning graph; does not launch or modify Unity."""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def earliest_levels(sources, recipes, selectors):
    """OR between recipe variants, AND between inputs, monotone availability by level."""
    result = {}
    for ident, level in sources:
        result[ident] = min(result.get(ident,999),level)
    changed = True
    while changed:
        changed = False
        for recipe in recipes:
            if not recipe['inputs']:
                continue
            levels = []
            for value in recipe['inputs']:
                if 'id' in value:
                    levels.append(result.get(value['id'],999))
                else:
                    levels.append(min((result.get(i,999) for i in selectors[value['selector']]),default=999))
            level = max(recipe['level'],*levels)
            if level <= 40 and level < result.get(recipe['output'],999):
                result[recipe['output']] = level
                changed = True
    return result


def validate(m):
    checks = []
    def check(name, condition):
        checks.append({'check':name,'passed':bool(condition)})
    entities = {e['id']:e for e in m['entities']}
    check('no duplicate item IDs',len(entities)==len(m['entities']))
    all_records = m['entities']+m['other_content']+m['equipment_parts']+m['jewelry_metals']+m['bags']
    ids = [e['id'] for e in all_records if e.get('id')]
    check('no duplicate IDs across all content categories',len(ids)==len(set(ids)))
    check('stable ID syntax',all(re.fullmatch(r'[a-z0-9_.]+',i) for i in ids))
    check('no unresolved blocking extraction issues',not any(i['severity']=='blocker' for i in m['issues']))
    check('all sources reference known items',all(s['id'] in entities for s in m['planned_sources']))
    check('all explicit source level ranges inside 1-40',all(1<=s['min_level']<=s['max_level']<=40 for s in m['planned_sources']))
    roots = [(s['id'],s['min_level']) for s in m['planned_sources']
             if s['owner'].split(' - ')[0] in {'Katakumby','Las','Elfie Ruiny','Morze','Bagno','Handel NPC'}]
    roots += [(e['id'],s['minimum_level']) for e in m['entities'] for s in e['runtime_stock'] if s['minimum_level']<=40]
    reachable = earliest_levels(roots,m['active_recipes'],m['selectors'])
    active = [e for e in m['entities'] if e['scope']=='pass_2']
    check('all in-scope items reachable in planning graph',all(e['id'] in reachable for e in active))
    check('stored levels equal independently recomputed levels',all(e['earliest_planned_level']==reachable.get(e['id']) for e in m['entities']))
    inputs = [v for r in m['active_recipes'] for v in r['inputs']]
    check('every ingredient and selector resolves',all(v.get('id') in entities if 'id' in v else v['selector'] in m['selectors'] for v in inputs))
    check('no missing source for any recipe alternative',all(
        v['id'] in reachable if 'id' in v else all(i in reachable for i in m['selectors'][v['selector']])
        for r in m['active_recipes'] if entities[r['output']]['scope']=='pass_2' for v in r['inputs']))
    check('all equipment-part inputs reachable before L25/L35',all(p['material'] in reachable and reachable[p['material']]<=p['level'] for p in m['equipment_parts']))
    check('complete equipment grid: 4 classes x 2 sides x 2 levels x 12 parts',len(m['equipment_parts'])==192 and
          set(collections.Counter((p['class'],p['side'],p['level']) for p in m['equipment_parts']).values())=={12})
    check('all B/C alchemy inputs reachable at their tier',all(v in reachable and reachable[v]<=(31 if k=='V' else 21)
        for a in m['alchemy'] for k,v in a.items() if k in 'TUV'))
    late={'Krew berserka','Zaczarowana dłoń','Energia Malada','Łuski wiwerny'}
    check('four late fallback boss materials only L31-40',all(s['min_level']==31 and s['max_level']==40
        for s in m['planned_sources'] if entities[s['id']]['name'] in late))
    check('no new sources for legacy dust/ear/blood',all(not e['planned_sources'] for e in m['entities'] if e['scope']=='legacy'))
    check('forest ear points at crafting canonical ID',all(s['id']=='item.vs10.ucho_elfa' for s in m['planned_sources'] if entities[s['id']]['name']=='Ucho elfa'))
    check('both forest bosses have forest element',all(any(s['owner']==owner and entities[s['id']]['name']=='Żywioł lasu'
        for s in m['planned_sources']) for owner in ['Las - Zarażony Ent','Las - Przywódca Bandytów']))
    check('user decision: leopard only in Ruins',any(s['owner']=='Elfie Ruiny' and entities[s['id']]['name']=='Skóra leoparda' for s in m['planned_sources']) and
        not any(s['owner']=='Elfie Ruiny' and entities[s['id']]['name']=='Skóra aligatora' for s in m['planned_sources']))
    check('user decision: high-tier gunpowder in five biomes',all(len({s['owner'] for s in m['planned_sources'] if entities[s['id']]['name']==name and s['kind']=='Torba zasobów'})==5
        for name in ['Dobry proch strzelniczy','Mocny proch strzelniczy']))
    for name in ['Obsydian','Jadeit']:
        sources=[s for s in m['planned_sources'] if entities[s['id']]['name']==name and s['kind']=='Torba bossa']
        check(f'{name}: preserve both original boss brackets and extend plan to 40',len(sources)==6 and
              set((s['min_level'],s['max_level']) for s in sources)=={(1,10),(11,20),(21,40)})
    check('user decision: Jasper/Pearl/Malachite from both biome bosses L1-40',all(
        len([s for s in m['planned_sources'] if entities[s['id']]['name']==name and s['kind']=='Torba bossa' and s['min_level']==1 and s['max_level']==40])==2
        for name in ['Jaspis','Perła','Malachit']))
    check('40 unique slots including 5 explicitly deferred',len(m['unique_slots'])==40 and sum(s['id'] is None for s in m['unique_slots'])==5)
    check('Paladin placeholders remain deferred without fabricated IDs',sum(e['status']=='design_deferred_paladin' and e['id'] is None for e in m['other_content'])==31)
    check('21 ordinary enemies and 6 two-phase bosses',sum(e['kind']=='CharacterDefinition' for e in m['other_content'])==33)
    check('galduryte and jewelry tiers retained',m['galduryte_rule']['level']==21 and len(m['jewelry_metals'])==8 and {e['level'] for e in m['jewelry_metals']}=={30,40})
    check('jewelry uses existing material-based identities',all(j['material_id'] in entities and j['material_id'] in reachable and reachable[j['material_id']]<=j['level'] for j in m['jewelry_metals'])
          and all(j['material_id'] in entities for j in m['jewelry_gems']))
    check('natural sources preserved except explicit user replacement',not any(i['code']=='HISTORICAL_NATURAL_SOURCE_CHANGED' for i in m['issues']))
    required_fallbacks={'Skóra warana','Drewno cedrowe','Włókno wasminy','Ruda złota','Klejnot pustyni',
        'Ametyst','Cytryn','Diament','Aloe meda','Owoc kaktusa','Pnącza alzif','Spaczone serce','Kwiat byliny',
        'Dmuchawiec tregandzki','Skruszona czaszka','Nerka trolla','Kwiat rissa','Żywioł wiatru','Żniwiarz pustynny',
        'Starożytna skóra','Lodowe prętki','Motyl królewski','Histyna','Pióro ruka'}|late
    actual_fallbacks={entities[s['id']]['name'] for s in m['planned_sources'] if s['classification']=='fallback'
        and s['owner'].split(' - ')[0] in {'Katakumby','Las','Elfie Ruiny','Morze','Bagno'} and 'torba' in s['kind'].casefold()}
    check('all 28 future-biome ingredients have fallback drops, not only shops',required_fallbacks<=actual_fallbacks)
    check('workbook unchanged since generation',hashlib.sha256((ROOT/m['source_workbook']).read_bytes()).hexdigest()==m['workbook_sha256'])
    return checks


def self_test():
    # Reject a closed cycle; keep OR variants independent; propagate levels through AND inputs.
    recipes=[{'output':'x','level':1,'inputs':[{'id':'y'}]}, {'output':'y','level':1,'inputs':[{'id':'x'}]}]
    assert earliest_levels([],recipes,{})=={}
    recipes.append({'output':'x','level':21,'inputs':[{'id':'raw'}]})
    assert earliest_levels([('raw',11)],recipes,{})=={'raw':11,'x':21,'y':21}
    recipes=[{'output':'z','level':21,'inputs':[{'id':'a'},{'id':'b'}]}]
    assert 'z' not in earliest_levels([('a',1)],recipes,{})
    assert earliest_levels([('a',1),('b',31)],recipes,{})['z']==31


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--self-test',action='store_true');args=parser.parse_args()
    if args.self_test:
        self_test()
    raw=(HERE/'content-manifest.json').read_bytes()
    m=json.loads(raw.decode('utf-8'))
    checks=validate(m);result={'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks),
        'checks':checks,'graph_unit_checks':'PASS' if args.self_test else 'NOT RUN',
        'manifest_sha256':hashlib.sha256(raw).hexdigest(),
        'planning_only':True,'runtime_reachability':'NOT IMPLEMENTED / NOT VERIFIED'}
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f"Planning validation: {result['passed']} PASS, {result['failed']} FAIL")
    for c in checks:
        if not c['passed']:print('FAIL:',c['check'])
    raise SystemExit(bool(result['failed']))


if __name__=='__main__':
    main()
