from pathlib import Path
import importlib.util,contextlib,json
p=Path('re-analysis/research/20261007-queue5-native.py')
s=importlib.util.spec_from_file_location('native',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
terms=['DetectFiducialMarkers','ComputeBrightDarkValues','IsQuadrilateralReasonable','MarkerDetector::Detect','NearestNeighborLibrary::GetNearestNeighbor','CameraCalibration::Set', 'QuadTreeNode::ShiftRoot','QuadTreeNode::SwapChildrenAndContent','QuadTreeNode::AddSmallestDescendants','QuadTreeProcessor::AddBorderWaypoint','QuadTreeProcessor::FindBorders','QuadTreeProcessor::FillBorder','LineSegment::IntersectsWith','VisionSystem::DetectMarkers','VisionMarker::Extract','RefineCorners','RefineQuadrilateral','SortCornersClockwise','VisionComponent::UpdateToolCode','VisionComponent::UpdateComputedCalibration','VisionComponent::UpdateImageQuality','VisionComponent::UpdateLaserPoints','VisionComponent::UpdateOverheadMap']
with Path('re-analysis/research/20261007-q19-engine-native.txt').open('w',encoding='utf-8') as h,contextlib.redirect_stdout(h):
 print('ENGINE SHA256',m.hashlib.sha256(m.raw).hexdigest())
 for f in m.index:
  if f[0]>0x500000 and any(t in f[3] for t in terms):
   print('\nFUNCTION',f[3]);m.dump(f[0],min(m.end(f),f[0]+f[1]) if m.end(f)-f[0]>0x10000 else m.end(f),True)
manifest=json.loads(Path('re-analysis/fidelity_manifest.json').read_text(encoding='utf-8'))
records=manifest['records']
Path('re-analysis/research/20261007-q19-current-records.json').write_text(json.dumps([r for r in records if r['id'] in ['M11-003','M11-005','M11-006','M11-032','M11-047','M11-048','M11-054','M13-023']],indent=2),encoding='utf-8')
