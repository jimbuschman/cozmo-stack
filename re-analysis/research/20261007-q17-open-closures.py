"""Reopen outstanding Q17 functions; index names are navigation, never evidence."""
from pathlib import Path
import importlib.util, contextlib, re, json
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('n',h/'20261007-queue5-native.py')
n=importlib.util.module_from_spec(s); s.loader.exec_module(n)
groups={
 'mood':r'Anki::Cozmo::(Emotion|MoodManager)::(Add$|Update$|Init$|InitDecayGraphs|HandleActionEnded|SendEmotionsToGame|GetHistoryValueTicksAgo)',
 'framework':r'Anki::Cozmo::(IBehavior::IBehavior$|BehaviorPlayAnimSequence::|WantsToRunStrategyFactory::|BehaviorManager::(EnableReactionsWithLock|DisableReactionsWithLock)|RobotDataLoader::LoadReactionTriggerMap|BehaviorContainer::RequestAllBehaviorsList)',
 'whiteboard':r'Anki::Cozmo::AIWhiteboard::(Init$|Update$|AddBeacon|ConsiderNewPossibleObject|UpdatePossibleObjectRender|RemovePossibleObjectsMatching|UpdateBeaconRender)',
 'docktest':r'Anki::Cozmo::BehaviorDockingTestSimple::',
 'wants':r'Anki::Cozmo::(IWantsToRunStrategy|StrategyAlwaysRun|StrategyExpressNeedsTransition|StrategyGeneric|StrategyInNeedsBracket|StrategyObstacleDetected|StrategyPlacedOnCharger|StrategyRobotShaken|StrategyRobotPlacedOnSlope)::',
}
for g,p in groups.items():
 with (h/f'20261007-q17-{g}-closure-native.txt').open('w',encoding='utf-8') as o,contextlib.redirect_stdout(o):
  print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
  for f in n.index:
   if f[0]<0x4DD000 or f[0]>=0x8C0000 or not re.search(p,f[3]):continue
   z=n.end(f)
   if z-f[0]>0x10000:z=f[0]+f[1]; print('FRAGMENTED, first contiguous body only')
   print('NAVIGATION',f[3]);n.dump(f[0],z,True)
 with (h/f'20261007-q17-{g}-navigation.txt').open('w',encoding='utf-8') as o:
  for f in n.index:
   if f[0]>=0x4DD000 and f[0]<0x8C0000 and re.search(p,f[3]):
    o.write(f'{f[0]:08X} {n.end(f):08X} {f[3]}\n')
with (h/'20261007-q17-idle-callers-reopened-native.txt').open('w',encoding='utf-8') as o,contextlib.redirect_stdout(o):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 for x in json.loads((h/'20261004-procedural-live-xrefs.json').read_text(encoding='utf-8')):
  a=int(x['address'],16)
  print(x['name']); n.dump(a-0x28,a+8,True)
