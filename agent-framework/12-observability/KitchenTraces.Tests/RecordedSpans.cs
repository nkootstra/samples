using System.Diagnostics;

namespace KitchenTraces.Tests;

// Records the spans that finish on the named activity sources, in the order
// they finish, the way an OpenTelemetry tracer subscribed to them would.
public sealed class RecordedSpans : IDisposable
{
    readonly ActivityListener _listener;
    readonly List<Activity> _spans = [];

    public RecordedSpans(params string[] sourceNames)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => sourceNames.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_spans)
                {
                    _spans.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> All
    {
        get
        {
            lock (_spans)
            {
                return [.. _spans];
            }
        }
    }

    public Activity Named(string displayNameStart) =>
        Assert.Single(All, span => span.DisplayName.StartsWith(displayNameStart, StringComparison.Ordinal));

    public void Dispose() => _listener.Dispose();
}
