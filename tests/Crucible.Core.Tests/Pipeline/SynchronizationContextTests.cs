namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// Library code must not resume on the caller's <see cref="SynchronizationContext"/>.
/// Under the console CLI and ASP.NET Core there is no context to capture, so this is
/// invisible — until the day Crucible.Core is embedded in WPF, WinForms, or MAUI, where
/// capturing it is a responsiveness and deadlock hazard.
/// </summary>
public sealed class SynchronizationContextTests : IDisposable
{
    private readonly string _source =
        Path.Combine(Path.GetTempPath(), "crucible-sync-" + Guid.NewGuid().ToString("N"));
    private readonly string _output =
        Path.Combine(Path.GetTempPath(), "crucible-sync-out-" + Guid.NewGuid().ToString("N"));

    public SynchronizationContextTests()
    {
        Directory.CreateDirectory(_source);
        File.WriteAllText(Path.Combine(_source, "index.md"),
            "---\ntitle: Home\n---\n\nBody.\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_source)) Directory.Delete(_source, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Counts continuations posted back to it, then runs them on the thread pool so the
    /// build still completes rather than deadlocking the test.
    /// </summary>
    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    /// <summary>
    /// Runs the build on its own thread with <paramref name="context"/> installed.
    /// </summary>
    /// <remarks>
    /// Deliberately blocking, and deliberately not in the test method: awaiting would post
    /// the test's own continuation to the context and count a resumption the library is not
    /// responsible for.
    /// </remarks>
    private static BuildResult RunUnder(
        SynchronizationContext context, BuildPipeline pipeline, CancellationToken ct)
    {
        // Capturing the whole run as a Task lets the thread body stay exception-free and
        // surfaces any failure to the caller with its stack intact.
        Task<BuildResult>? run = null;

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            run = Task.FromResult(pipeline.ExecuteAsync(ct).GetAwaiter().GetResult());
        });

        thread.Start();
        thread.Join();

        return run!.GetAwaiter().GetResult();
    }

    [Fact]
    public void Build_DoesNotResumeOnTheCallersSynchronizationContext()
    {
        var config = new CrucibleConfig
        {
            Source = _source,
            Output = _output,
            Title = "Sync Site",
            BaseUrl = "/",
        };
        var pipeline = new BuildPipeline(config, [], new BuildOptions());
        var context = new CountingSynchronizationContext();

        var result = RunUnder(context, pipeline, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        context.Posts.Should().Be(0,
            "every await inside the library should be ConfigureAwait(false)");
    }
}
