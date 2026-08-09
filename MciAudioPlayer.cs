using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace LoonClipper;

internal sealed class MciAudioPlayer : IDisposable
{
    private readonly string _alias = $"loonclipper{Guid.NewGuid():N}";
    private bool _isOpen;

    public TimeSpan Position
    {
        get
        {
            var value = Query($"status {_alias} position");
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
                ? TimeSpan.FromMilliseconds(milliseconds)
                : TimeSpan.Zero;
        }
    }

    public bool IsPlaying =>
        _isOpen
        && Query($"status {_alias} mode").Equals("playing", StringComparison.OrdinalIgnoreCase);

    public void Open(string path)
    {
        Close();

        Send($"open \"{path}\" type mpegvideo alias {_alias}");
        _isOpen = true;

        try
        {
            Send($"set {_alias} time format milliseconds");
        }
        catch
        {
            Close();
            throw;
        }
    }

    public void Play(TimeSpan start, TimeSpan end)
    {
        EnsureOpen();
        Send($"play {_alias} from {ToMilliseconds(start)} to {ToMilliseconds(end)}");
    }

    public void Pause()
    {
        EnsureOpen();
        Send($"pause {_alias}");
    }

    public void Stop()
    {
        if (_isOpen)
        {
            Send($"stop {_alias}");
        }
    }

    public void Close()
    {
        if (!_isOpen)
        {
            return;
        }

        MciSendString($"stop {_alias}", null, 0, IntPtr.Zero);
        MciSendString($"close {_alias}", null, 0, IntPtr.Zero);
        _isOpen = false;
    }

    public void Dispose() => Close();

    private string Query(string command)
    {
        EnsureOpen();
        var result = new StringBuilder(128);
        Send(command, result);
        return result.ToString().Trim();
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("The audio preview is not ready yet.");
        }
    }

    private static long ToMilliseconds(TimeSpan value) =>
        Math.Max(0, (long)Math.Round(value.TotalMilliseconds));

    private static void Send(string command, StringBuilder? result = null)
    {
        var errorCode = MciSendString(
            command,
            result,
            result?.Capacity ?? 0,
            IntPtr.Zero);

        if (errorCode == 0)
        {
            return;
        }

        var errorText = new StringBuilder(256);
        var message = MciGetErrorString(errorCode, errorText, errorText.Capacity)
            ? errorText.ToString()
            : $"Windows audio error {errorCode}";

        throw new InvalidOperationException($"Could not play the preview. {message}");
    }

    [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)]
    private static extern int MciSendString(
        string command,
        StringBuilder? returnValue,
        int returnLength,
        IntPtr callback);

    [DllImport("winmm.dll", EntryPoint = "mciGetErrorStringW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MciGetErrorString(
        int errorCode,
        StringBuilder errorText,
        int errorTextSize);
}
