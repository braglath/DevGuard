using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SuperDev;

/// <summary>
/// Posts a banner through the operating system notification center.
/// A click opens the checklist in the running Super Dev window.
/// </summary>
internal static class SystemNotification
{
    public static void Show(string title, string body, bool silent, Guid documentId, string? filePath)
    {
        title = Clean(title);
        body = Clean(body);
        if (title.Length == 0)
            title = "Checklist";

        try
        {
            if (OperatingSystem.IsMacOS())
                MacNotificationCenter.Show(title, body, silent, documentId, filePath);
            else if (OperatingSystem.IsWindows())
                ShowWindows(title, body, silent, LaunchUri(documentId, filePath));
        }
        catch
        {
            // The in-app list still records the reminder when the system banner cannot be posted.
        }
    }

    private static void ShowWindows(string title, string body, bool silent, string launchUri)
    {
        var script = """
            $title = [System.Net.WebUtility]::HtmlEncode($env:SUPERDEV_TITLE)
            $body = [System.Net.WebUtility]::HtmlEncode($env:SUPERDEV_BODY)
            $audio = if ($env:SUPERDEV_SILENT -eq '1') { '<audio silent="true"/>' } else { '' }
            $launch = [System.Net.WebUtility]::HtmlEncode($env:SUPERDEV_LAUNCH)
            $xml = @"
            <toast activationType="protocol" launch="$launch">
              <visual>
                <binding template="ToastGeneric">
                  <text>Super Dev</text>
                  <text>$title</text>
                  <text>$body</text>
                </binding>
              </visual>
              $audio
            </toast>
            "@
            $null = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
            $null = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
            $doc = New-Object Windows.Data.Xml.Dom.XmlDocument
            $doc.LoadXml($xml)
            $toast = [Windows.UI.Notifications.ToastNotification]::new($doc)
            $id = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
            [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($id).Show($toast)
            Start-Sleep -Milliseconds 400
            """;

        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(script);
        RegisterProtocol();
        start.Environment["SUPERDEV_TITLE"] = title;
        start.Environment["SUPERDEV_BODY"] = body;
        start.Environment["SUPERDEV_SILENT"] = silent ? "1" : "0";
        start.Environment["SUPERDEV_LAUNCH"] = launchUri;
        Process.Start(start)?.Dispose();
    }

    private static string LaunchUri(Guid documentId, string? filePath)
    {
        var uri = "superdev://open/?document=" + documentId.ToString("N");
        if (!string.IsNullOrWhiteSpace(filePath))
            uri += "&file=" + Uri.EscapeDataString(filePath);
        return uri;
    }

    private static void RegisterProtocol()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return;

        using var protocol = Registry.CurrentUser.CreateSubKey(@"Software\Classes\superdev");
        protocol?.SetValue(string.Empty, "URL:Super Dev");
        protocol?.SetValue("URL Protocol", string.Empty);
        using var command = protocol?.CreateSubKey(@"shell\open\command");
        command?.SetValue(string.Empty, $"\"{exe}\" \"%1\"");
    }

    public static void RegisterFileAssociation()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return;

        try
        {
            using var extension = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.superdev.json");
            extension?.SetValue(string.Empty, "SuperDev.Checklist");
            using var type = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SuperDev.Checklist");
            type?.SetValue(string.Empty, "Super Dev checklist");
            using var command = type?.CreateSubKey(@"shell\open\command");
            command?.SetValue(string.Empty, $"\"{exe}\" \"%1\"");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }

    private static string Clean(string value)
    {
        var trimmed = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return trimmed.Length <= 140 ? trimmed : trimmed[..140];
    }
}
