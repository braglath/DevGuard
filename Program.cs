using Avalonia;
using System;
using System.Collections.Generic;
using SuperDev.Services;

namespace SuperDev;

sealed class Program
{
    internal static IReadOnlyList<ActivationRequest> StartupActivations { get; private set; } = [];

    internal static bool OwnsActivationServer { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--reminder-daemon") >= 0)
        {
            ReminderDaemon.Run();
            return;
        }

        MacNotificationCenter.GuiProcess = true;
        var activations = InstanceBridge.Collect(args);
        if (activations.Count > 0 && activations.TrueForAll(activation => InstanceBridge.TrySend(activation)))
            return;

        var primary = InstanceBridge.TryAcquirePrimary();
        if (activations.Count > 0 && !primary)
        {
            foreach (var activation in activations)
                InstanceBridge.TrySend(activation, attempts: 40, timeoutMilliseconds: 200);
            return;
        }

        OwnsActivationServer = primary;
        StartupActivations = activations;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(InstanceBridge.WithoutActivationArgs(args));
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
