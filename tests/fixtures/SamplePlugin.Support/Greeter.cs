namespace SamplePlugin.Support;

/// <summary>
/// Called by the sample plugin. If the plugin loader cannot resolve this assembly, the
/// plugin still loads and then throws <c>FileNotFoundException</c> on first use — which is
/// the failure this fixture exists to reproduce.
/// </summary>
public static class Greeter
{
    public static string Describe() => "sample-plugin (support loaded)";
}
