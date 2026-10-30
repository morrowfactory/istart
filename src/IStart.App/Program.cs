using System.Runtime.CompilerServices;

namespace IStart;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.FirstOrDefault() == InstanceChannel.TestArgument)
        {
            if (args.Length != 3 || !Guid.TryParseExact(args[1], "N", out _) || args[2] is not ("show" or "enable" or "exit")) return 64;
            string[] command = args[2] == "show" ? [] : [args[2] == "exit" ? "--exit" : "--enable-menu"];
            var result = InstanceChannel.TrySignal("QA-" + args[1], command, false) ?? 3;
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is "PresentationFramework" or "PresentationCore" or "System.Windows.Forms") ? 4 : result;
        }
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        using (identity)
            if (InstanceChannel.TrySignal(identity.User!.Value, args) is int result) return result;
        return RunApplication();
    }

    // Keep WPF type loading and resource initialization out of the resident handoff path.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication()
    {
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
