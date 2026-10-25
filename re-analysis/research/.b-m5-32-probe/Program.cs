using Cozmo.Conformance;
using Cozmo.Robot.Animation;
using System.Text.Json.Nodes;
var wire = new List<M5Hw.WireMessage> {
 new(10,true,new byte[]{0x8F},1,0), new(11,true,new byte[]{0x9B,1},2,0),
 new(12,true,new byte[]{(byte)Cozmo.Protocol.RobotMessageId.AnimBackpackLights,0,0,0,0,0,0},3,0),new(13,true,new byte[]{0x97,0,0},4,0),new(14,true,new byte[]{0x9A},5,0)};
var hooks = new List<M5HwAnalysis.Diagnostic> {
 new(9,new LayerFrameObservation(0,1,0,true,true,1,4)),
 new(15,new LayerFrameObservation(0,2,33,false,false,99,null))};
for(int i=0;i<wire.Count;i++) hooks.Insert(i,new(wire[i].AtMs-0.1,new SendGateObservation(0,i+1,1,wire[i].Payload[0],wire[i].Payload.Length, i==0,8192,14,true,true,i==4)));
var join=M5HwAnalysis.JoinSends(wire,hooks,0,20);
var activity=M5HwAnalysis.LayerActivity(join,hooks,0,20);
var layers=M5HwAnalysis.FiniteLayers(wire,0,20,true,activity.Activity,activity.Problem);
Console.WriteLine("LAYER join problem="+join.Problem+"; activity problem="+activity.Problem+"; verdict="+layers["verdict"]+"; missing="+layers["missing"]);
var h = new List<M5HwAnalysis.Diagnostic> {
 new(0,new BudgetRefreshObservation(0,1,0,0,0,0,8192,14)),
 new(1,new SendGateObservation(0,1,1,0x8F,1,true,8192,14,true,true,false)),
 new(3,new SendGateObservation(0,2,1,0x8F,1,true,8191,13,true,true,false))};
var all=M5HwAnalysis.BudgetSamples(M5HwAnalysis.Admissions(h,0,4),M5HwAnalysis.Refreshes(h));
var cut=M5HwAnalysis.BudgetSamples(M5HwAnalysis.Admissions(h,2,4),M5HwAnalysis.Refreshes(h));
Console.WriteLine("BUDGET full="+all["coherentSampleVerdict"]+"; window="+cut["coherentSampleVerdict"]+"; mismatches="+cut["allowanceAccountingMismatches"]);

