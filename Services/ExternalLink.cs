using System;
using System.Diagnostics;

namespace SuperDev.Services;

public static class ExternalLink
{
    public static bool CanOpen(string? value) =>
        TryCreateHttpUri(value, out _);

    public static bool TryOpen(string? value)
    {
        if (!TryCreateHttpUri(value, out var uri))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool TryCreateHttpUri(string? value, out Uri uri)
    {
        uri = null!;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            return false;

        uri = parsed;
        return true;
    }
}
