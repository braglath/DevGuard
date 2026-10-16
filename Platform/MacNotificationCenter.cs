using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SuperDev.Services;

namespace SuperDev;

/// <summary>
/// Posts banners through the UserNotifications framework so a click stays inside Super Dev.
/// osascript banners open Script Editor when clicked.
/// </summary>
internal static class MacNotificationCenter
{
    private const string LibraryName = "libSuperDevNotify";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateNative(IntPtr document, IntPtr file);

    private static readonly ActivateNative ActivateDelegate = OnActivated;
    private static Action<ActivationRequest>? _gui;
    private static ActivationRequest? _queued;
    private static int _ready;
    private static bool _available;

    public static bool GuiProcess { get; set; }

    public static void SetGuiHandler(Action<ActivationRequest> handler)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        ActivationRequest? queued;
        lock (ActivateDelegate)
        {
            _gui = handler;
            queued = _queued;
            _queued = null;
        }

        if (queued is not null)
            handler(queued);
    }

    public static void EnsureReady()
    {
        if (!OperatingSystem.IsMacOS() || Interlocked.Exchange(ref _ready, 1) == 1)
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(MacNotificationCenter).Assembly, Resolve);
            _available = sd_notify_start(
                Marshal.GetFunctionPointerForDelegate(ActivateDelegate),
                GuiProcess ? 0 : 1) != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _available = false;
        }
    }

    public static void Show(string title, string body, bool silent, Guid documentId, string? filePath)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        EnsureReady();
        if (!_available)
            return;

        sd_notify_post(
            title,
            title,
            body,
            silent ? 1 : 0,
            documentId.ToString("N"),
            filePath ?? string.Empty);
    }

    public static void RunDaemon(Action periodic)
    {
        EnsureReady();
        var next = DateTime.UtcNow;
        while (true)
        {
            if (_available)
            {
                sd_notify_bind();
                sd_notify_pump(0.25);
            }
            else
            {
                Thread.Sleep(250);
            }

            if (DateTime.UtcNow < next)
                continue;

            next = DateTime.UtcNow.AddSeconds(5);
            periodic();
        }
    }

    public static void ActivateApp()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            EnsureReady();
            if (_available)
                sd_notify_activate_app();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    private static void OnActivated(IntPtr document, IntPtr file)
    {
        var idText = document == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(document);
        var path = file == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(file);
        if (!Guid.TryParse(idText, out var documentId))
            return;

        var request = new ActivationRequest
        {
            DocumentId = documentId,
            FilePath = string.IsNullOrWhiteSpace(path) ? null : path
        };
        ThreadPool.QueueUserWorkItem(_ => Dispatch(request));
    }

    private static void Dispatch(ActivationRequest request)
    {
        Action<ActivationRequest>? gui;
        lock (ActivateDelegate)
            gui = _gui;

        if (gui is not null)
        {
            gui(request);
            return;
        }

        if (GuiProcess)
        {
            lock (ActivateDelegate)
                _queued = request;
            return;
        }

        if (!InstanceBridge.TrySend(request))
            LaunchGui(request);
    }

    private static void LaunchGui(ActivationRequest request)
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--open-document");
            start.ArgumentList.Add(request.DocumentId.ToString("N"));
            if (!string.IsNullOrWhiteSpace(request.FilePath))
                start.ArgumentList.Add(request.FilePath);
            Process.Start(start)?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
            return IntPtr.Zero;

        var path = Path.Combine(AppContext.BaseDirectory, "libSuperDevNotify.dylib");
        return NativeLibrary.Load(path);
    }

    [DllImport(LibraryName)]
    private static extern int sd_notify_start(IntPtr activate, int background);

    [DllImport(LibraryName)]
    private static extern void sd_notify_post(
        string title,
        string subtitle,
        string body,
        int silent,
        string document,
        string file);

    [DllImport(LibraryName)]
    private static extern void sd_notify_bind();

    [DllImport(LibraryName)]
    private static extern void sd_notify_pump(double seconds);

    [DllImport(LibraryName)]
    private static extern void sd_notify_activate_app();
}
