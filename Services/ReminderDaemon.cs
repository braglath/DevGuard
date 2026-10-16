using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using SuperDev;

namespace SuperDev.Services;

public static class ReminderDaemon
{
    private static Mutex? _instance;

    public static void Run()
    {
        if (!TryAcquire())
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReminderSchedule.PidPath)!);
            File.WriteAllText(ReminderSchedule.PidPath, Environment.ProcessId.ToString());
            if (OperatingSystem.IsMacOS())
            {
                MacNotificationCenter.RunDaemon(Tick);
                return;
            }

            while (true)
            {
                Tick();
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            try
            {
                if (File.Exists(ReminderSchedule.PidPath))
                    File.Delete(ReminderSchedule.PidPath);
            }
            catch (IOException)
            {
            }

            try
            {
                _instance?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }
    }

    public static bool IsRunning()
    {
        try
        {
            if (!File.Exists(ReminderSchedule.PidPath))
                return false;

            if (!TryReadPid(out var pid, out var heartbeat, out var opensChecklist))
                return false;

            if (!opensChecklist || DateTime.UtcNow - heartbeat > TimeSpan.FromSeconds(20))
                return false;

            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    public static void EnsureRunning()
    {
        if (IsRunning())
            return;

        StopDaemonThatCannotOpen();

        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var folder = Path.GetDirectoryName(ReminderSchedule.PidPath)!;
            Directory.CreateDirectory(folder);
            var start = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = folder
            };
            start.ArgumentList.Add("--reminder-daemon");
            using var process = Process.Start(start);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
        }
    }

    private static void Tick()
    {
        try
        {
            Touch();
            ReminderSchedule.DeliverDue();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private static void Touch()
    {
        File.WriteAllText(
            ReminderSchedule.PidPath,
            $"{Environment.ProcessId} {DateTime.UtcNow.Ticks} click");
    }

    private static bool TryReadPid(out int pid, out DateTime heartbeat, out bool opensChecklist)
    {
        pid = 0;
        heartbeat = DateTime.MinValue;
        opensChecklist = false;
        if (!File.Exists(ReminderSchedule.PidPath))
            return false;

        var split = File.ReadAllText(ReminderSchedule.PidPath).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0 || !int.TryParse(split[0], out pid))
            return false;

        if (split.Length > 1 && long.TryParse(split[1], out var ticks))
            heartbeat = new DateTime(ticks, DateTimeKind.Utc);

        opensChecklist = split.Length > 2 && split[2] == "click";
        return true;
    }

    private static void StopDaemonThatCannotOpen()
    {
        try
        {
            if (!TryReadPid(out var pid, out _, out var opensChecklist) || opensChecklist)
                return;

            using var process = Process.GetProcessById(pid);
            if (process.HasExited || !IsOurProcess(process))
                return;

            process.Kill(entireProcessTree: false);
            process.WaitForExit(2000);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
        }
    }

    private static bool IsOurProcess(Process process)
    {
        try
        {
            var expected = Environment.ProcessPath;
            var actual = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(expected) &&
                   !string.IsNullOrWhiteSpace(actual) &&
                   string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException or IOException)
        {
            return false;
        }
    }

    private static bool TryAcquire()
    {
        _instance = new Mutex(false, "SuperDevReminderDaemon");
        try
        {
            return _instance.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
