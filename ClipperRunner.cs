using System.Diagnostics;

namespace LoonClipper;

internal static class ClipperRunner
{
    private static readonly string[] RequiredTools =
    [
        "yt-dlp.exe",
        "ffmpeg.exe",
        "ffprobe.exe"
    ];

    public static async Task<string> CreateMp3Async(
        string url,
        string mediaId,
        TimeSpan start,
        TimeSpan end)
    {
        var outputDirectory = AppContext.BaseDirectory;
        var toolsDirectory = Path.Combine(outputDirectory, "tools");
        EnsureToolsExist(toolsDirectory);

        var fileStem = GetAvailableFileStem(outputDirectory, mediaId, start, end);
        var outputPath = Path.Combine(outputDirectory, $"{fileStem}.mp3");
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "LoonClipper",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDirectory);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(toolsDirectory, "yt-dlp.exe"),
                WorkingDirectory = outputDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            AddArguments(
                startInfo,
                toolsDirectory,
                outputDirectory,
                tempDirectory,
                fileStem,
                url,
                start,
                end);

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start yt-dlp.");
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

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    "yt-dlp finished, but no MP3 file was created.");
            }

            return outputPath;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        string toolsDirectory,
        string outputDirectory,
        string tempDirectory,
        string fileStem,
        string url,
        TimeSpan start,
        TimeSpan end)
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
            $"home:{outputDirectory}",
            "--paths",
            $"temp:{tempDirectory}",
            "--output",
            $"{fileStem}.%(ext)s",
            "--windows-filenames",
            "--no-overwrites",
            url
        ];

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static void EnsureToolsExist(string toolsDirectory)
    {
        var missingTools = RequiredTools
            .Where(fileName => !File.Exists(Path.Combine(toolsDirectory, fileName)))
            .ToArray();

        if (missingTools.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Missing {string.Join(", ", missingTools)} in the tools folder.");
    }

    private static string GetAvailableFileStem(
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

        return fileStem;
    }

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
            return "yt-dlp could not create the clip.";
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
}
