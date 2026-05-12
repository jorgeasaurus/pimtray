using System.Reflection;

namespace PIMTray;

internal static class AppIcon
{
    private const string ResourceName = "PIMTray.Resources.tray.ico";

    public static Icon Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        return new Icon(stream);
    }
}
