from pathlib import Path
import importlib.util
p=Path('re-analysis/research/20261007-queue5-native.py')
s=importlib.util.spec_from_file_location('native',p); m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
names=['DriveStraightAction','MoveLiftToHeightAction','MoveHeadToAngleAction','TurnInPlaceAction','TurnTowardsObjectAction','DriveToPoseAction','IDockAction','AlignWithObjectAction','VisuallyVerifyNoObjectAtPoseAction','BackupOntoChargerAction','DriveOffChargerContactsAction','TurnTowardsPoseAction','PanAndTiltAction','WaitForImagesAction','VisuallyVerifyObjectAction','IVisuallyVerifyAction']
import contextlib
out=Path('re-analysis/research/20261007-q18-child-native.txt')
with out.open('w',encoding='utf-8') as h,contextlib.redirect_stdout(h):
 print('ENGINE SHA256',m.hashlib.sha256(m.raw).hexdigest())
 for f in m.index:
  if f[0]>0x500000 and f[3].startswith('Anki::Cozmo::') and any('::'+n+'::' in f[3] for n in names):
   print('\nFUNCTION',f[3]);m.dump(f[0],min(m.end(f),f[0]+f[1]) if m.end(f)-f[0]>0x10000 else m.end(f),True)
