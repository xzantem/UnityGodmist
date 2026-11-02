from pathlib import Path
import os,json,subprocess,hashlib
# Generation only: requires PyYAML; runtime and NUnit tests use the frozen JSON.
import yaml
os.chdir(Path(__file__).resolve().parents[3])
out=Path(__file__).parent
revision=subprocess.check_output(['git','rev-parse','v0.1.0^{commit}'],text=True).strip()
assert revision=='89159e69fca45e96dc56a3bf5ff0c7de9ba066a0',revision
paths=subprocess.check_output(['git','ls-tree','-r','--name-only','v0.1.0'],text=True).splitlines()
result={'baseCommit':revision,'skills':[],'quests':[],'aliases':[]}
def stable(kind,key):return kind+hashlib.sha256(key.encode()).hexdigest()[:24]
def effects(rows,skill,path=''):
 data=[]
 for index,row in enumerate(rows or []):
  key=path+str(index);identity=stable('e',skill+'|'+key)
  record={'index':index,'id':identity,'shapeJson':json.dumps(row,separators=(',',':')),'children':effects(row.get('counterattackEffects'),skill,key+'.')}
  data.append(record)
  if not path:
   result['aliases'].append({'oldId':skill+'.effect.'+str(index),'newId':skill+'.effect.'+identity})
   suffix={6:'modify_stat',7:'modify_var',18:'modify_resistance',19:'unique_buff',14:'action_points',20:'damage_redirect',23:'on_hit_buff',22:'resource_block',26:'periodic_recovery',27:'periodic_recovery',28:'action_points'}.get(row.get('effectType'))
   # The released executor dispatches some ApplyUniqueBuff payloads to dedicated owners.
   if row.get('effectType')==19:
    suffix={3:'action_points',4:'on_hit_buff',5:'damage_redirect',6:'aura'}.get(row.get('uniqueBuffType'),'unique_buff')
   if row.get('effectType')==21:suffix='aura'
   if suffix:
    tail='.' if suffix=='aura' else ''
    result['aliases'].append({'oldId':skill+'.'+suffix+'.'+str(index)+tail,'newId':skill+'.'+suffix+'.'+identity+tail})
 return data
for path in paths:
 if not path.endswith('.asset') or '/ScriptableObjects/' not in path or not ('/Skills/' in path or '/Quests/' in path):continue
 raw=subprocess.check_output(['git','show','v0.1.0:'+path]).decode('utf-8-sig')
 root=yaml.safe_load('\n'.join(raw.splitlines()[3:])).get('MonoBehaviour',{})
 skill=root.get('<SkillId>k__BackingField')
 if skill:result['skills'].append({'assetPath':path.removeprefix('Godmist Unity/'),'skillId':skill,'effects':effects(root.get('effects'),skill)})
 stages=root.get('<Stages>k__BackingField');quest=root.get('<Id>k__BackingField')
 if stages is not None and quest:
  stageRows=[]
  for stage in stages:
   sid=stage.get('<StageId>k__BackingField','');rewards=[]
   for index,reward in enumerate(stage.get('<CompletionRewards>k__BackingField',[]) or []):
    rid=stable('r',quest+'|'+sid+'|'+str(index))
    rewards.append({'index':index,'id':rid,'type':reward.get('rewardType',0),'amount':reward.get('amount',1),'itemGuid':(reward.get('itemDefinition') or {}).get('guid',''),'worldFlagId':reward.get('worldFlagId',''),'worldFlagValue':str(reward.get('worldFlagValue',''))})
   stageRows.append({'stageId':sid,'rewards':rewards})
  result['quests'].append({'assetPath':path.removeprefix('Godmist Unity/'),'questId':quest,'stages':stageRows})
# v0.1.0 runtime explicitly forced Crushing Blow suppression to historical slot 2,
# although its authored slot in that release is 1. Capture the released exception here only.
for skill in result['skills']:
 if skill['skillId']=='skill.paladin.crushing_blow':
  resistance=next(e for e in skill['effects'] if json.loads(e['shapeJson'])['effectType']==18)
  result['aliases'].append({'oldId':skill['skillId']+'.modify_resistance.2','newId':skill['skillId']+'.modify_resistance.'+resistance['id']})
# Duplicate aliases must have exactly the same identity, never last-write-wins.
seen={}
for entry in result['aliases']:
 if entry['oldId'] in seen:assert seen[entry['oldId']]==entry['newId'],entry
 seen[entry['oldId']]=entry['newId']
(out/'legacy-authoring-v0.1.0.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['using System.Collections.Generic;','namespace Godmist.Systems.SaveLoad','{','    public static partial class LegacyContentIdentities','    {','        private static readonly Dictionary<string,string> EffectAliases = new()','        {']
for old,new in sorted(seen.items()):lines.append('            ['+json.dumps(old)+'] = '+json.dumps(new)+',')
lines+=['        };','        private static readonly Dictionary<string,string> RewardAliases = new()','        {']
for quest in result['quests']:
 for stage in quest['stages']:
  for reward in stage['rewards']:lines.append('            ['+json.dumps(quest['questId']+'|'+stage['stageId']+':'+str(reward['index']))+'] = '+json.dumps('reward:'+reward['id'])+',')
lines+=['        };','    }','}']
Path('Godmist Unity/Assets/_Project/Scripts/Systems/SaveLoad/LegacyContentIdentities.Generated.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Skills:',len(result['skills']),'quests:',len(result['quests']),'effect aliases:',len(seen),'historical rewards:',sum(len(s['rewards']) for q in result['quests'] for s in q['stages']))
