using System.Text.Json;

namespace LunaPlayer.Configuration;

internal sealed class SettingsStore
{
    private readonly string _jsonPath;
    private bool _writeBlocked;

    /// <summary>Why the last read or write failed, or an empty string when it did not. Kept because a
    /// message telling the user only that something could not be saved leaves them nothing to act on.
    /// </summary>
    internal string LastError { get; private set; } = string.Empty;

    internal SettingsStore(string jsonPath) => _jsonPath = jsonPath;

    internal string Path => _jsonPath;

    /// <summary>Reads only the saved theme from the settings file, without the full <see cref="Load"/>. The
    /// complete load validates the settings, which builds the translated action tables and so needs a
    /// running <c>App</c>; the theme is needed earlier than that, because it is applied as a wxWidgets system
    /// option before the App is created. Falls back to <see cref="AppTheme.SystemDefault"/> when the file is
    /// absent, unreadable, or has no valid theme.</summary>
    internal static AppTheme PeekTheme(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
                return AppTheme.SystemDefault;
            using var stream = File.OpenRead(jsonPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("general", out var general)
                && general.TryGetProperty("theme", out var theme)
                && theme.ValueKind == JsonValueKind.String
                && Enum.TryParse<AppTheme>(theme.GetString(), ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
                return parsed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return AppTheme.SystemDefault;
    }

    internal PlayerSettings Load()
    {
        if (!File.Exists(_jsonPath))
        {
            LastError = string.Empty;
            _writeBlocked = false;
            return new PlayerSettings();
        }
        if (TryReadFile(_jsonPath, out var settings, out var error))
        {
            LastError = string.Empty;
            _writeBlocked = false;
            return settings;
        }
        LastError = error;
        _writeBlocked = true;
        return new PlayerSettings();
    }

    internal bool SaveExplicit(PlayerSettings settings)
    {
        if (_writeBlocked)
            return false;
        settings.Validate();
        return Write(settings);
    }

    /// <summary>Replaces a settings file that failed to load after the user imports a valid file or confirms
    /// a reset.</summary>
    internal bool ReplaceExplicit(PlayerSettings settings)
    {
        settings.Validate();
        var written = Write(settings);
        if (written)
            _writeBlocked = false;
        return written;
    }

    internal bool SaveSession(PlayerSettings settings)
        => !settings.General.SaveOnClose || SaveExplicit(settings);

    internal bool TryRead(string path, out PlayerSettings settings)
    {
        var read = TryReadFile(path, out settings, out var error);
        LastError = error;
        return read;
    }

    private static bool TryReadFile(string path, out PlayerSettings settings, out string error)
    {
        try
        {
            using var stream = File.OpenRead(path);
            settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.PlayerSettings)
                ?? throw new JsonException("The settings file is empty.");
            settings.ValidateStored();
            settings.Validate();
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            settings = new PlayerSettings();
            error = exception.Message;
            return false;
        }
    }

    private bool Write(PlayerSettings settings)
    {
        try
        {
            Paths.EnsureDirectoryFor(_jsonPath);
            var temporary = Paths.TemporaryFor(_jsonPath);
            using (var stream = File.Create(temporary))
                JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.PlayerSettings);
            File.Move(temporary, _jsonPath, overwrite: true);
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastError = exception.Message;
            return false;
        }
    }

}
