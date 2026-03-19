namespace Crucible.Core.Pipeline;

public enum InputType
{
    MarkdownSource,
    XmlIntermediate,
}

public static class InputDetector
{
    public static InputType Detect(string directory) =>
        File.Exists(Path.Combine(directory, "site-manifest.xml"))
            ? InputType.XmlIntermediate
            : InputType.MarkdownSource;
}
