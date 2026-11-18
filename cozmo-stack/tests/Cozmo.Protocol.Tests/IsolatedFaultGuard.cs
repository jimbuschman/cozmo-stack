using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cozmo.Robot;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("Cozmo.Protocol.Tests.FaultGuardFramework", "Cozmo.Protocol.Tests")]

namespace Cozmo.Protocol.Tests;

/// <summary>An exact, counted declaration of an intentionally isolated exception. Assertions can never be allowed.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExpectedIsolatedFaultAttribute(Type exceptionType, string message, int count = 1) : Attribute
{
    internal Type ExceptionType { get; } = exceptionType;
    internal string Message { get; } = message;
    internal int Count { get; } = count;
}

internal static class IsolatedFaultGuard
{
    internal sealed class Owner { internal Scope? Current; }
    internal static readonly AsyncLocal<Owner?> Ambient = new();
    internal static readonly ConcurrentBag<Scope> Scopes = new();
    private static readonly Scope Orphan = new("outside a test (fixture initialization/disposal or unowned work)", []);

    [ModuleInitializer]
    internal static void Install()
    {
        IsolatedFaultProbe.Capture = () =>
        {
            var scope = Ambient.Value?.Current ?? Orphan;
            return scope.Record;
        };
        // Assertions must also fail when an independent isolation seam swallows them
        // without reaching Engine.ReportFault/Robot.Fault. Ordinary first-chance
        // exceptions are ignored; those two fault paths own their counted declarations.
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (IsAssertion(e.Exception)) (Ambient.Value?.Current ?? Orphan).Record(e.Exception);
        };
    }

    internal sealed class Scope(string name, IEnumerable<ExpectedIsolatedFaultAttribute> expected)
    {
        private readonly object _gate = new();
        private readonly HashSet<Exception> _seen = new(ReferenceEqualityComparer.Instance);
        private readonly List<Exception> _faults = [];
        private readonly List<Exception> _late = [];
        private readonly ExpectedIsolatedFaultAttribute[] _expected = expected.ToArray();
        private bool _closed;
        internal void Record(Exception error)
        {
            lock (_gate)
            {
                // A previously seen object thrown again after completion is still a late fault.
                if (_closed) _late.Add(error);
                else if (_seen.Add(error)) _faults.Add(error);
            }
        }

        internal Exception? Close()
        {
            lock (_gate)
            {
                _closed = true;
                var remaining = _expected.Select(e => e.Count).ToArray();
                var errors = new List<Exception>();
                foreach (var fault in _faults)
                {
                    int match = IsAssertion(fault) ? -1 : Array.FindIndex(_expected,
                        e => e.ExceptionType == fault.GetType() && e.Message == fault.Message && remaining[Array.IndexOf(_expected, e)] > 0);
                    if (match >= 0) remaining[match]--;
                    else errors.Add(fault);
                }
                for (int i = 0; i < remaining.Length; ++i)
                    if (remaining[i] != 0)
                        errors.Add(new InvalidOperationException($"Expected {_expected[i].ExceptionType.Name}: '{_expected[i].Message}' {_expected[i].Count} time(s); missing {remaining[i]} (invalid negative counts also fail)."));
                return errors.Count == 0 ? null : new AggregateException($"Isolated fault guard: {name}", errors);
            }
        }

        internal Exception? LateFailure()
        {
            lock (_gate) return _late.Count == 0 ? null : new AggregateException($"Fault after test completion: {name}", _late);
        }

        internal Exception? Finish()
        {
            lock (_gate)
            {
                var pending = _closed ? null : Close();
                var late = LateFailure();
                return pending is null ? late : late is null ? pending : new AggregateException(pending, late);
            }
        }
    }

    internal static bool IsAssertion(Exception e) =>
        e.GetType().GetInterfaces().Any(t => t.FullName == "Xunit.Sdk.IAssertionException") ||
        HasAssertionBase(e.GetType()) ||
        (e.InnerException is { } inner && IsAssertion(inner)) ||
        (e is AggregateException aggregate && aggregate.InnerExceptions.Any(IsAssertion));

    private static bool HasAssertionBase(Type? type) => type is not null &&
        (type.Namespace == "Xunit.Sdk" || HasAssertionBase(type.BaseType));

    internal static Exception? FinishAssembly()
    {
        var errors = Scopes.Select(s => s.Finish()).OfType<Exception>().ToList();
        if (Orphan.Finish() is { } orphan) errors.Add(orphan);
        return errors.Count == 0 ? null : new AggregateException("Unattributed or late isolated faults", errors);
    }
}

// Execution-only wrappers preserve xUnit's discovered cases (including deferred theories).
// The bus observes completion after the test instance's synchronous/async disposal, and
// RunSummary is corrected as well as messages so a guarded failure changes the runner exit code.
public sealed class FaultGuardFramework(IMessageSink sink) : XunitTestFramework(sink)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName name) => new Executor(name, SourceInformationProvider, DiagnosticMessageSink);

    private sealed class Executor(AssemblyName name, ISourceInformationProvider source, IMessageSink diagnostic)
        : XunitTestFrameworkExecutor(name, source, diagnostic)
    {
        protected override async void RunTestCases(IEnumerable<IXunitTestCase> cases, IMessageSink sink, ITestFrameworkExecutionOptions options)
        {
            using var runner = new AssemblyRunner(TestAssembly, cases.Select(c => new GuardedCase(c)), diagnostic, sink, options);
            await runner.RunAsync();
        }
    }

    private sealed class AssemblyRunner(ITestAssembly assembly, IEnumerable<IXunitTestCase> cases, IMessageSink diagnostic,
        IMessageSink sink, ITestFrameworkExecutionOptions options) : XunitTestAssemblyRunner(assembly, cases, diagnostic, sink, options)
    {
        protected override async Task<RunSummary> RunTestCollectionsAsync(IMessageBus bus, CancellationTokenSource cancellation)
        {
            var summary = await base.RunTestCollectionsAsync(bus, cancellation);
            if (IsolatedFaultGuard.FinishAssembly() is { } error)
            {
                bus.QueueMessage(new ErrorMessage(TestCases, error));
                summary.Failed++;
            }
            return summary;
        }
    }

    // Created only after discovery/deserialization; never crosses xUnit's serialization boundary.
#pragma warning disable xUnit3001
    private sealed class GuardedCase(IXunitTestCase original) : Xunit.LongLivedMarshalByRefObject, IXunitTestCase
    {
        public string DisplayName => original.DisplayName;
        public string SkipReason => original.SkipReason;
        public ISourceInformation SourceInformation { get => original.SourceInformation; set => original.SourceInformation = value; }
        public ITestMethod TestMethod => original.TestMethod;
        public object[] TestMethodArguments => original.TestMethodArguments;
        public Dictionary<string, List<string>> Traits => original.Traits;
        public string UniqueID => original.UniqueID;
        public Exception InitializationException => original.InitializationException;
        public IMethodInfo Method => original.Method;
        public int Timeout => original.Timeout;
        public void Serialize(IXunitSerializationInfo info) => original.Serialize(info);
        public void Deserialize(IXunitSerializationInfo info) => original.Deserialize(info);
        public async Task<RunSummary> RunAsync(IMessageSink diagnostic, IMessageBus bus, object[] arguments, ExceptionAggregator aggregator, CancellationTokenSource cancellation)
        {
            var previous = IsolatedFaultGuard.Ambient.Value;
            var owner = new IsolatedFaultGuard.Owner();
            IsolatedFaultGuard.Ambient.Value = owner;
            using var guardedBus = new GuardedBus(bus, owner);
            try
            {
                var summary = await original.RunAsync(diagnostic, guardedBus, arguments, aggregator, cancellation);
                summary.Failed += guardedBus.Converted;
                summary.Skipped -= guardedBus.ConvertedSkips;
                return summary;
            }
            finally { IsolatedFaultGuard.Ambient.Value = previous; }
        }
    }
#pragma warning restore xUnit3001

    private sealed class GuardedBus(IMessageBus inner, IsolatedFaultGuard.Owner owner) : IMessageBus
    {
        internal int Converted;
        internal int ConvertedSkips;
        public void Dispose() { } // the assembly runner owns the underlying bus
        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (message is ITestStarting start)
            {
                var method = ((IReflectionMethodInfo)start.Test.TestCase.TestMethod.Method).MethodInfo;
                owner.Current = new(start.Test.DisplayName, method.GetCustomAttributes<ExpectedIsolatedFaultAttribute>());
                IsolatedFaultGuard.Scopes.Add(owner.Current);
                // Deferred-theory rows share this bus. Give work spawned by this row a
                // separate ambient holder so a later row cannot relabel its assertions.
                IsolatedFaultGuard.Ambient.Value = new() { Current = owner.Current };
            }
            if (message is ITestResultMessage result && owner.Current?.Close() is { } error)
            {
                if (message is ITestFailed failed)
                    error = new AggregateException(new Exception(string.Join("\n", failed.Messages) + "\n" + string.Join("\n", failed.StackTraces)), error);
                else { Converted++; if (message is ITestSkipped) ConvertedSkips++; }
                return inner.QueueMessage(new TestFailed(result.Test, result.ExecutionTime, result.Output, error));
            }
            if (message is ITestCaseFinished finished && Converted != 0)
                return inner.QueueMessage(new TestCaseFinished(finished.TestCase, finished.ExecutionTime, finished.TestsRun,
                    finished.TestsFailed + Converted, finished.TestsSkipped - ConvertedSkips));
            return inner.QueueMessage(message);
        }
    }
}
