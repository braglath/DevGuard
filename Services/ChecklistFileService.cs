using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SuperDev.Models;

namespace SuperDev.Services;

public sealed class ChecklistFileService
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false),
            new UtcDateTimeJsonConverter()
        }
    };

    private static readonly string[] RequiredProperties =
    [
        "FormatVersion",
        "Id",
        "Title",
        "WorkType",
        "CreatedOn",
        "UpdatedOn",
        "IsCompleted",
        "Items"
    ];

    public async Task<ChecklistDocument> LoadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ChecklistFileException("That checklist file could not be found.");

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ChecklistFileException("That checklist file could not be found.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ChecklistFileException("That checklist file could not be read.", ex);
        }

        return Parse(json);
    }

    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="path"/>.
    /// A file that already belongs to a different document is left unchanged
    /// unless <paramref name="replaceDifferentDocument"/> is true.
    /// </summary>
    public async Task SaveAsync(
        string path,
        ChecklistDocument document,
        bool replaceDifferentDocument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        if (File.Exists(path))
            await EnsureCanReplaceAsync(path, document, replaceDifferentDocument);

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write beside the destination, then replace it, so a failed save
        // leaves the previous checklist file intact.
        var temporaryPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ChecklistFileException("The checklist could not be saved.", ex);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private async Task EnsureCanReplaceAsync(
        string path,
        ChecklistDocument document,
        bool replaceDifferentDocument)
    {
        ChecklistDocument existing;
        try
        {
            existing = await LoadAsync(path);
        }
        catch (ChecklistFileException) when (replaceDifferentDocument)
        {
            return;
        }

        if (existing.Id != document.Id && !replaceDifferentDocument)
        {
            throw new ChecklistFileException(
                "That file belongs to a different checklist.",
                isDifferentDocument: true);
        }
    }

    private static ChecklistDocument Parse(string json)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ChecklistFileException(
                "This file isn't a valid Super Dev checklist.",
                ex);
        }

        using (parsed)
        {
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ChecklistFileException(
                    "This file isn't a valid Super Dev checklist.");
            }

            foreach (var name in RequiredProperties)
            {
                if (!parsed.RootElement.TryGetProperty(name, out var property) ||
                    property.ValueKind == JsonValueKind.Null)
                {
                    throw new ChecklistFileException(
                        $"This checklist file is missing {name}.");
                }
            }
        }

        ChecklistDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ChecklistDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ChecklistFileException(
                "This file isn't a valid Super Dev checklist.",
                ex);
        }

        if (document is null)
        {
            throw new ChecklistFileException(
                "This file isn't a valid Super Dev checklist.");
        }

        Validate(document);
        return document;
    }

    private static void Validate(ChecklistDocument document)
    {
        if (document.FormatVersion != ChecklistDocument.CurrentFormatVersion)
        {
            throw new ChecklistFileException(
                $"This file uses checklist format version {document.FormatVersion}. " +
                $"Super Dev opens version {ChecklistDocument.CurrentFormatVersion}.");
        }

        if (document.Id == Guid.Empty)
            throw new ChecklistFileException("This checklist file is missing a document id.");

        if (string.IsNullOrWhiteSpace(document.Title))
            throw new ChecklistFileException("This checklist file is missing a title.");

        if (!Enum.IsDefined(document.WorkType))
            throw new ChecklistFileException("This checklist file has an unknown workflow type.");

        if (document.CreatedOn == default)
            throw new ChecklistFileException("This checklist file is missing CreatedOn.");

        if (document.UpdatedOn == default)
            throw new ChecklistFileException("This checklist file is missing UpdatedOn.");

        if (document.Items is null)
            throw new ChecklistFileException("This checklist file is missing its checks.");

        foreach (var item in document.Items)
        {
            if (item is null)
                throw new ChecklistFileException("This checklist file contains an empty check.");

            if (item.Id == Guid.Empty)
                throw new ChecklistFileException("A checklist item is missing an id.");

            if (string.IsNullOrWhiteSpace(item.Title))
                throw new ChecklistFileException("A checklist item is missing a title.");

            if (!Enum.IsDefined(item.Priority))
                throw new ChecklistFileException("A checklist item has an unknown priority.");
        }
    }

    private sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}

public sealed class ChecklistFileException : Exception
{
    public ChecklistFileException(string message, bool isDifferentDocument = false)
        : base(message)
    {
        IsDifferentDocument = isDifferentDocument;
    }

    public ChecklistFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public bool IsDifferentDocument { get; }
}
