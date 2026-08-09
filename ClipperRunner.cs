using System.Diagnostics;
using System.Globalization;

namespace LoonClipper;

internal sealed class ClipDraft : IDisposable
{
    internal ClipDraft(
        string workingDirectory,
        string sourcePath,
        string mediaId,
        TimeSpan requestedStart)
    {
        WorkingDirectory = workingDirectory;
        SourcePath = sourcePath;
        MediaId = mediaId;
        RequestedStart = requestedStart;
    }

    internal string WorkingDirectory { get; }
    internal string SourcePath { get; private set; }
    internal string MediaId { get; }
    internal TimeSpan RequestedStart { get; }
    internal bool IsNormalized { get; private set; }

    internal void UseNormalizedSource(string sourcePath)
    {
        var previousSource = SourcePath;
        SourcePath = sourcePath;
        IsNormalized = true;

        if (!previousSource.Equals(sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(previousSource);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Windows can clean up a temporary preview that is still in use.
        }
        catch (UnauthorizedAccessException)
        {
            // Windows can clean up a temporary preview that is still in use.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The containing temporary directory is removed when the draft closes.
        }
        catch (UnauthorizedAccessException)
        {
            // The containing temporary directory is removed when the draft closes.
        }
    }
}

internal static class ClipperRunner
{
    private static readonly string[] RequiredTools =
    [
        "yt-dlp.exe",
        "ffmpeg.exe",
        "ffprobe.exe"
    ];

    public static async Task<ClipDraft> CreateDraftAsync(
        string url,
        string mediaId,
        TimeSpan start,
        TimeSpan end)
    {
        var toolsDirectory = GetToolsDirectory();
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "LoonClipper",
            Guid.NewGuid().ToString("N"));
        var downloadTempDirectory = Path.Combine(workingDirectory, "download");
        var previewPath = Path.Combine(workingDirectory, "preview.mp3");

        Directory.CreateDirectory(downloadTempDirectory);

        try
        {
            string[] arguments =
            [
                "--ignore-config",
                "--no-playlist",
                "--download-sections",
                $"*{TimestampText.Format(start)}-{TimestampText.Format(end)}",
                "--format",
                "bestaudio/best",
                "--extract-audio",
                "--audio-format",
                "mp3",
                "--audio-quality",
                "192K",
                "--ffmpeg-location",
                toolsDirectory,
                "--paths",
                $"home:{workingDirectory}",
                "--paths",
                $"temp:{downloadTempDirectory}",
                "--output",
                "preview.%(ext)s",
                "--windows-filenames",
                "--no-overwrites",
                "--no-progress",
                url
            ];

            await RunToolAsync(Path.Combine(toolsDirectory, "yt-dlp.exe"), arguments);

            if (!File.Exists(previewPath))
            {
                throw new InvalidOperationException(
                    "yt-dlp finished, but no MP3 preview was created.");
            }

            return new ClipDraft(workingDirectory, previewPath, mediaId, start);
        }
        catch
        {
            TryDeleteDirectory(workingDirectory);
            throw;
        }
    }

    public static async Task<TimeSpan> GetDurationAsync(ClipDraft draft)
    {
        var toolsDirectory = GetToolsDirectory();
        string[] arguments =
        [
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            draft.SourcePath
        ];

        var output = await RunToolAsync(
            Path.Combine(toolsDirectory, "ffprobe.exe"),
            arguments);

        if (!double.TryParse(
                output.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds)
            || !double.IsFinite(seconds)
            || seconds <= 0)
        {
            throw new InvalidOperationException("Could not read the preview duration.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public static async Task<string> CreateWaveformAsync(ClipDraft draft)
    {
        var toolsDirectory = GetToolsDirectory();
        var waveformPath = Path.Combine(
            draft.WorkingDirectory,
            $"waveform-{Guid.NewGuid():N}.png");

        string[] arguments =
        [
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            draft.SourcePath,
            "-filter_complex",
            "[0:a:0]aformat=channel_layouts=mono,showwavespic=s=1024x160:colors=0x9146FF:scale=sqrt:filter=peak[v]",
            "-map",
            "[v]",
            "-frames:v",
            "1",
            "-y",
            waveformPath
        ];

        await RunToolAsync(Path.Combine(toolsDirectory, "ffmpeg.exe"), arguments);

        if (!File.Exists(waveformPath))
        {
            throw new InvalidOperationException("Could not create the audio waveform.");
        }

        return waveformPath;
    }

    public static async Task NormalizeAsync(ClipDraft draft)
    {
        if (draft.IsNormalized)
        {
            return;
        }

        var toolsDirectory = GetToolsDirectory();
        var normalizedPath = Path.Combine(
            draft.WorkingDirectory,
            $"normalized-{Guid.NewGuid():N}.mp3");

        string[] arguments =
        [
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            draft.SourcePath,
            "-map",
            "0:a:0",
            "-vn",
            "-map_metadata",
            "-1",
            "-af",
            "loudnorm=I=-16:LRA=11:TP=-1.5,aresample=48000",
            "-c:a",
            "libmp3lame",
            "-b:a",
            "192k",
            "-y",
            normalizedPath
        ];

        try
        {
            await RunToolAsync(Path.Combine(toolsDirectory, "ffmpeg.exe"), arguments);

            if (!File.Exists(normalizedPath))
            {
                throw new InvalidOperationException("Could not create the normalized preview.");
            }

            draft.UseNormalizedSource(normalizedPath);
        }
        catch
        {
            TryDeleteFile(normalizedPath);
            throw;
        }
    }

    public static async Task<string> SaveAsync(
        ClipDraft draft,
        TimeSpan selectionStart,
        TimeSpan selectionEnd)
    {
        if (selectionStart < TimeSpan.Zero || selectionEnd <= selectionStart)
        {
            throw new InvalidOperationException("Choose a valid part of the preview to save.");
        }

        var toolsDirectory = GetToolsDirectory();
        var outputDirectory = AppContext.BaseDirectory;
        var absoluteStart = draft.RequestedStart + selectionStart;
        var absoluteEnd = draft.RequestedStart + selectionEnd;
        var outputPath = GetAvailableOutputPath(
            outputDirectory,
            draft.MediaId,
            absoluteStart,
            absoluteEnd);
        var temporaryOutputPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp.mp3");

        string[] arguments =
        [
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            "-ss",
            FormatSeconds(selectionStart),
            "-i",
            draft.SourcePath,
            "-t",
            FormatSeconds(selectionEnd - selectionStart),
            "-map",
            "0:a:0",
            "-vn",
            "-map_metadata",
            "-1",
            "-c:a",
            "libmp3lame",
            "-b:a",
            "192k",
            "-y",
            temporaryOutputPath
        ];

        try
        {
            await RunToolAsync(Path.Combine(toolsDirectory, "ffmpeg.exe"), arguments);

            if (!File.Exists(temporaryOutputPath))
            {
                throw new InvalidOperationException("Could not create the final MP3.");
            }

            File.Move(temporaryOutputPath, outputPath);
            return outputPath;
        }
        catch
        {
            TryDeleteFile(temporaryOutputPath);
            throw;
        }
    }

    private static string GetToolsDirectory()
    {
        var toolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");
        var missingTools = RequiredTools
            .Where(fileName => !File.Exists(Path.Combine(toolsDirectory, fileName)))
            .ToArray();

        if (missingTools.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing {string.Join(", ", missingTools)} in the tools folder.");
        }

        return toolsDirectory;
    }

    private static async Task<string> RunToolAsync(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Could not start {Path.GetFileName(executablePath)}.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(
            process.WaitForExitAsync(),
            standardOutput,
            standardError);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                GetUsefulError(await standardError, await standardOutput));
        }

        return await standardOutput;
    }

    private static string GetAvailableOutputPath(
        string outputDirectory,
        string mediaId,
        TimeSpan start,
        TimeSpan end)
    {
        var baseStem = $"twitch-{mediaId}_{TimestampText.FormatFile(start)}-{TimestampText.FormatFile(end)}";
        var fileStem = baseStem;
        var suffix = 2;

        while (File.Exists(Path.Combine(outputDirectory, $"{fileStem}.mp3")))
        {
            fileStem = $"{baseStem}-{suffix}";
            suffix++;
        }

        return Path.Combine(outputDirectory, $"{fileStem}.mp3");
    }

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetUsefulError(string standardError, string standardOutput)
    {
        var lines = standardError
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(standardOutput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        var line = lines.LastOrDefault(value =>
                       value.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
                   ?? lines.LastOrDefault();

        if (string.IsNullOrWhiteSpace(line))
        {
            return "The audio tool could not finish this step.";
        }

        var errorMarker = line.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase);
        if (errorMarker >= 0)
        {
            line = line[(errorMarker + "ERROR:".Length)..].Trim();
        }

        const int maximumLength = 240;
        if (line.Length > maximumLength)
        {
            line = $"{line[..(maximumLength - 1)]}…";
        }

        return line;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary files can be cleaned up by Windows.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files can be cleaned up by Windows.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A partial temporary output can be cleaned up by Windows.
        }
        catch (UnauthorizedAccessException)
        {
            // A partial temporary output can be cleaned up by Windows.
        }
    }
}
