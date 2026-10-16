using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SuperDev.Services;

public sealed class ActivationRequest
{
    public Guid DocumentId { get; set; }

    public string? FilePath { get; set; }
}

/// <summary>
/// Hands a notification click to the Super Dev window that is already open.
/// A second launch sends the checklist here and exits.
/// </summary>
public static class InstanceBridge
{
    private const string PipeName = "SuperDev.Instance";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static int _serverStarted;
    private static Mutex? _primary;

    public static bool TryAcquirePrimary()
    {
        try
        {
            _primary = new Mutex(false, "SuperDev.Gui");
            return _primary.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    public static bool TrySend(ActivationRequest request, int attempts = 4, int timeoutMilliseconds = 150)
    {
        var line = JsonSerializer.Serialize(request, Json);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(timeoutMilliseconds);
                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                writer.WriteLine(line);
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException)
            {
                if (attempt + 1 < attempts)
                    Thread.Sleep(80);
            }
        }

        return false;
    }

    public static void StartServer(Action<ActivationRequest> onRequest)
    {
        if (Interlocked.Exchange(ref _serverStarted, 1) == 1)
            return;

        var thread = new Thread(() => Listen(onRequest))
        {
            IsBackground = true,
            Name = "Super Dev activation"
        };
        thread.Start();
    }

    public static ActivationRequest? FromArgs(string[] args)
    {
        foreach (var arg in args)
        {
            var parsed = FromText(arg);
            if (parsed is not null)
                return parsed;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--open-document" || i + 1 >= args.Length || !Guid.TryParse(args[i + 1], out var documentId))
                continue;

            string? filePath = null;
            if (i + 2 < args.Length && !args[i + 2].StartsWith('-'))
                filePath = args[i + 2];

            return new ActivationRequest
            {
                DocumentId = documentId,
                FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath
            };
        }

        return null;
    }

    public static List<ActivationRequest> Collect(string[] args)
    {
        var list = new List<ActivationRequest>();
        var named = FromArgs(args);
        if (named is not null)
            list.Add(named);

        foreach (var arg in args)
        {
            var path = ChecklistFileArg(arg);
            if (path is null)
                continue;

            if (named?.FilePath is { } namedPath && SamePath(namedPath, path))
                continue;

            list.Add(new ActivationRequest { FilePath = path });
        }

        return list;
    }

    public static string[] WithoutActivationArgs(string[] args)
    {
        var kept = new System.Collections.Generic.List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (FromText(args[i]) is not null)
                continue;

            if (args[i] == "--open-document")
            {
                if (i + 1 < args.Length && Guid.TryParse(args[i + 1], out _))
                {
                    i++;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        i++;
                }

                continue;
            }

            if (ChecklistFileArg(args[i]) is not null)
                continue;

            kept.Add(args[i]);
        }

        return kept.ToArray();
    }

    private static ActivationRequest? FromText(string raw)
    {
        var text = raw.Trim().Trim('"');
        if (!text.StartsWith("superdev:", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = text.IndexOf('?');
        if (query < 0 || query + 1 >= text.Length)
            return null;

        Guid documentId = Guid.Empty;
        string? filePath = null;
        foreach (var part in text[(query + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.IndexOf('=');
            if (split <= 0)
                continue;

            var key = Uri.UnescapeDataString(part[..split]);
            var value = Uri.UnescapeDataString(part[(split + 1)..]);
            if (key == "document")
                Guid.TryParse(value, out documentId);
            else if (key == "file" && !string.IsNullOrWhiteSpace(value))
                filePath = value;
        }

        if (documentId == Guid.Empty)
            return null;

        return new ActivationRequest
        {
            DocumentId = documentId,
            FilePath = filePath
        };
    }

    private static string? ChecklistFileArg(string raw)
    {
        var text = raw.Trim().Trim('"');
        if (text.Length == 0
            || text.StartsWith('-')
            || text.StartsWith("superdev:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!text.EndsWith(".superdev.json", StringComparison.OrdinalIgnoreCase)
            && !text.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            if (!File.Exists(text))
                return null;

            return Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void Listen(Action<ActivationRequest> onRequest)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 4);
                server.WaitForConnection();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var request = JsonSerializer.Deserialize<ActivationRequest>(line, Json);
                if (request is null)
                    continue;
                if (request.DocumentId == Guid.Empty && string.IsNullOrWhiteSpace(request.FilePath))
                    continue;

                onRequest(request);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or ObjectDisposedException)
            {
                Thread.Sleep(200);
            }
        }
    }
}
