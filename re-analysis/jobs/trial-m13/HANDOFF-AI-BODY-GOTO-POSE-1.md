# AI-BODY-GOTO-POSE-1

Unity Robot.GotoPose (Robot.cs:1566) supplies the default profile, level 0 and manual false. Native 0x0052A128–0x0052A196 takes x/y from message +0/+4, wraps +8 in Radians, sets z=0 and axis Z, parents to Robot::GetWorldOrigin at 0x0052A146, and names the pose GotoPoseTarget. The handler leaves level unread and skips the custom profile setter at 0x0052A206 when +0x34 is zero.

At 0x0052A1F2 the tuple is (robot, goal by reference, lowerHead=false, manual=false, Point3f(10,10,10), Radians(0x3E32B8C2), 4.0f, 1.0f). The ctor at 0x0055A3D4 forwards the booleans to 0x0055A238 (+0x79 off, +0xA4 off, body track 4), stores the final two floats to +0xA8/+0xAC, then SetGoal at 0x0055A3FC (copies the tolerance and pose while NotStarted). The latter float and stored Z tolerance keep the shared runner's defaults and are not read by its arrival computation (0x0055ACB6–0x0055ACBA reads X/Y and Robot::GetHeight).

QueueAiGotoPoseAsync uses that owned goal and native drive runner, with its original owner and action-list destruction completing the C# request. AiGotoPoseControlTests pins the goal frame, constructor defaults, pending cancellation and nonfinite rejection. The skills round converts the published pose to an absolute goal 200 mm ahead and 90 degrees left, then records the result and poses as observations.

M12-023/M12-024/M12-032 retain their path, diagnostic and animation boundaries. Robot acceptance is pending.

Focused go-to, roll/stack and path-origin tests passed (15). The skills dry-run checked 19 records and opened no connection. The fidelity check passed.
