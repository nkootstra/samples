using System.Runtime.CompilerServices;

namespace KitchenTraces.Tests;

// Span listeners see every span on a source name, whichever test wrote it,
// so the classes that record spans don't run at the same time.
[CollectionDefinition(nameof(RecordsSpans))]
public sealed class RecordsSpans;

// Aspire sets this variable on the app, and the telemetry wrappers read it
// once per process, the first time one of them is built. So it is set here,
// before any test runs, as the app gets it from the AppHost. The tests that
// check what a span records set EnableSensitiveData explicitly.
static class CaptureVariable
{
    public const string Name = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";

    [ModuleInitializer]
    internal static void Set() => Environment.SetEnvironmentVariable(Name, "true");
}
