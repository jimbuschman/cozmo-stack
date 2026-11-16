# WHITEBOARD-WORLD-ORIGIN-1

AIWhiteboard fetches its Robot at +4 and calls GetWorldOrigin at 0x0056AC92. The getter at 0x005128FC–0x00512902 returns Robot+0x294's PoseOriginList current pose at +0x14. Redraw fetches it again at 0x0056A6E4. Bind the actual Robot's entry when its whiteboard is constructed, before the first map update. Each read follows the current entry through origin changes. Delocalization now uses the same binding.

The three direct ARM callers are UpdateRobotPose (0x0067E47E), ClearRobotToMarkers (0x0067F708), and AddVisionOverheadEdges (0x006801B0). The footprint and overhead paths already reach this whiteboard. The visual-confirmation marker-corner producer at 0x00506A98 → 0x0067F708 remains a separate logged gap.

The native function copies the pose twice at 0x0056ACA2 and 0x0056ACC2. The copy constructor at 0x008458F0 allocates a fresh node. GetWithRespectTo at 0x0056CD36 checks root identity at 0x00845A62 and 0x00844CD6. Thus a normal parentless current origin retains entries. With a shared root, the actual footprint producer erases the matching entry and preserves the entries outside its quad or frame. Tests cover those outcomes and the shared possible-object storage.

The native cube-search getter 0x0056C2BC has one direct caller, ExploreVisitPossibleMarker's runnable gate at 0x005E3D7A. That behavior is deferred in SCOPE.md D. The test observes the changed shared memory through the existing whiteboard handlers. Claude review and a robot rerun are pending.
