using System.Reflection;
using Cozmo.Robot;
using Xunit;
using Xunit.Sdk;

namespace Cozmo.Protocol.Tests;

public sealed class IsolatedFaultGuardTests
{
    public static IEnumerable<object[]> DeferredRows => [new object[]{1},new object[]{2}];

    [Theory,MemberData(nameof(DeferredRows),DisableDiscoveryEnumeration=true)]
    [ExpectedIsolatedFault(typeof(InvalidOperationException),"intentional deferred row")]
    public void EachDeferredTheoryRowHasItsOwnDeclaration(int row)
    {
        using var robot=CozmoRobot.CreateOffline();
        robot.Engine.StackLogLine+=line=>{if(line=="probe")throw new InvalidOperationException("intentional deferred row");};
        Assert.InRange(row,1,2);robot.Engine.EmitStackLog("probe");
    }

    [Fact]
    public void OrdinaryFaultMustMatchExactTypeMessageAndCount()
    {
        var expected=new ExpectedIsolatedFaultAttribute(typeof(InvalidOperationException),"intentional",2);
        var scope=new IsolatedFaultGuard.Scope("counted",[expected]);
        var one=new InvalidOperationException("intentional");
        scope.Record(one);scope.Record(one); // the same exception traversing Engine -> Robot counts once
        Assert.NotNull(scope.Close()); // still missing the second distinct throw
        scope.Record(one);Assert.NotNull(scope.LateFailure());
        var accepted=new IsolatedFaultGuard.Scope("accepted",[expected]);
        accepted.Record(new InvalidOperationException("intentional"));accepted.Record(new InvalidOperationException("intentional"));
        Assert.Null(accepted.Close());
        var wrong=new IsolatedFaultGuard.Scope("wrong",[expected]);
        wrong.Record(new Exception("intentional"));wrong.Record(new InvalidOperationException("different"));
        Assert.NotNull(wrong.Close());
    }

    [Fact]
    public void AssertionsAndWrappedAssertionsCannotBeDeclaredExpected()
    {
        var assertion=new XunitException("assertion probe");
        foreach(var error in new Exception[]{assertion,new AggregateException(new Exception("ordinary"),assertion)})
        {
            var scope=new IsolatedFaultGuard.Scope("assertion",[new(error.GetType(),error.Message)]);
            scope.Record(error);Assert.NotNull(scope.Close());
        }
    }

    [Fact]
    public void IndependentIsolationCannotHideAnAssertion()
    {
        var scope=new IsolatedFaultGuard.Scope("independent isolation",[]);
        var old=IsolatedFaultGuard.Ambient.Value;
        IsolatedFaultGuard.Ambient.Value=new(){Current=scope};
        try
        {
            try { Assert.True(false,"swallowed outside both fault methods"); }
            catch(Exception) { }
        }
        finally { IsolatedFaultGuard.Ambient.Value=old; }
        Assert.Contains("swallowed outside both fault methods",scope.Close()!.ToString());
    }

    [Fact]
    public void BothFaultPathsAndTheirThrowingFaultSubscribersAreObserved()
    {
        var scope=new IsolatedFaultGuard.Scope("both",[]);
        var old=IsolatedFaultGuard.Ambient.Value;
        IsolatedFaultGuard.Ambient.Value=new(){Current=scope};
        try
        {
            using var robot=CozmoRobot.CreateOffline();
            var original=new InvalidOperationException("original");
            robot.Engine.Faulted+=_=>Assert.True(false,"engine fault subscriber");
            robot.HandlerFaulted+=_=>Assert.True(false,"robot fault subscriber");
            typeof(CozmoEngine).GetMethod("ReportFault",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(robot.Engine,[original]);
            typeof(CozmoRobot).GetMethod("Fault",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(robot,[original]);
        }
        finally { IsolatedFaultGuard.Ambient.Value=old; }
        var error=Assert.IsType<AggregateException>(scope.Close());
        Assert.Contains(error.InnerExceptions,e=>e.Message=="original");
        Assert.Contains(error.InnerExceptions,e=>e.Message.Contains("engine fault subscriber"));
        Assert.Contains(error.InnerExceptions,e=>e.Message.Contains("robot fault subscriber"));
    }

    [Fact]
    public void InstanceKeepsOwnerOnAThreadWithoutExecutionContextAndLateFaultsFail()
    {
        var scope=new IsolatedFaultGuard.Scope("owner",[]);
        var old=IsolatedFaultGuard.Ambient.Value;
        IsolatedFaultGuard.Ambient.Value=new(){Current=scope};
        CozmoRobot robot;
        try { robot=CozmoRobot.CreateOffline(); }
        finally { IsolatedFaultGuard.Ambient.Value=old; }
        using(robot)
        {
            Assert.Null(scope.Close());
            robot.Engine.StackLogLine+=_=>throw new InvalidOperationException("late on detached thread");
            Thread thread;
            using(ExecutionContext.SuppressFlow())thread=new Thread(()=>robot.Engine.EmitStackLog("probe"));
            thread.Start();thread.Join();
            Assert.Contains("late on detached thread",scope.LateFailure()!.ToString());
        }
    }
}

// These are integration checks of the real xUnit lifetime: the declaration is verified
// only after IDisposable/IAsyncLifetime cleanup, not by a BeforeAfterTestAttribute.
public sealed class FaultGuardDisposalTests : IDisposable
{
    private readonly CozmoRobot _robot=CozmoRobot.CreateOffline();
    [Fact,ExpectedIsolatedFault(typeof(InvalidOperationException),"intentional synchronous disposal")]
    public void CountsFaultFromTestClassDisposal() => _robot.Engine.StackLogLine+=line=>{if(line=="cleanup")throw new InvalidOperationException("intentional synchronous disposal");};
    public void Dispose() { _robot.Engine.EmitStackLog("cleanup");_robot.Dispose(); }
}

public sealed class FaultGuardAsyncDisposalTests : IAsyncLifetime
{
    private readonly CozmoRobot _robot=CozmoRobot.CreateOffline();
    public Task InitializeAsync()=>Task.CompletedTask;
    [Fact,ExpectedIsolatedFault(typeof(InvalidOperationException),"intentional asynchronous disposal")]
    public void CountsFaultFromAsyncTestClassDisposal() => _robot.Engine.StackLogLine+=line=>{if(line=="cleanup")throw new InvalidOperationException("intentional asynchronous disposal");};
    public async Task DisposeAsync() { await Task.Yield();_robot.Engine.EmitStackLog("cleanup");_robot.Dispose(); }
}
