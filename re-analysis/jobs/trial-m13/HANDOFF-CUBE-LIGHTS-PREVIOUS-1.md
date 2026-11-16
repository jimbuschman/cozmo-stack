# CUBE-LIGHTS-PREVIOUS-1

The connected ObjectConnectionState handler uses the report's ObjectID at 0x00639D46–0x00639D4C to emplace the light map node and reloads it at 0x00639D52 for WakeUp. Disconnection exits at 0x00639CE4–0x00639CE8. The stack now passes the complete E2G 0x0D report into the light component and retains that ID with the stack entry.

Update takes the ObjectID from the tree node at 0x0063795C and 0x00637A8A. The prior-pattern log at 0x00637AA4 and the transition and light calls at 0x00637AD6 and 0x00637AE4 use that same node key. The empty-layer log also reads it at 0x00637BBC. Both retained-ID logs and the update's sends now use that ID. The active radio slot is selected only during the existing SetObjectLights lookup and packet send. For ordinary patterns, a missing connected object produces the native no-active-object result at 0x00637E96–0x00637EFC.

The warning observed in the robot bundle came from the log's fresh connected-object lookup. That warning alone does not establish a skipped resend. The offline regression test expires an authored Flash over Connected both with the cube present and after a real disconnect message. It checks the 0x00637AA4 log ID in both cases and the CubeID/CubeLights group and payload when connected. Claude review and a robot rerun are pending.
