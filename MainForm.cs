namespace LoonClipper;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(248, 248, 250);
    private static readonly Color AccentColor = Color.FromArgb(145, 70, 255);
    private static readonly Color AccentHoverColor = Color.FromArgb(126, 55, 230);
    private static readonly Color MutedTextColor = Color.FromArgb(92, 92, 102);
    private static readonly Color ErrorColor = Color.FromArgb(180, 35, 35);
    private static readonly Color SuccessColor = Color.FromArgb(25, 115, 60);

    private readonly TextBox _urlTextBox;
    private readonly TimeInput _startTimeInput;
    private readonly TimeInput _endTimeInput;
    private readonly Button _clipButton;
    private readonly Label _statusLabel;
    private bool _isBusy;

    public MainForm()
    {
        Text = "LoonClipper";
        ClientSize = new Size(560, 304);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ShowIcon = false;
        BackColor = BackgroundColor;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;

        var urlLabel = CreateLabel("Twitch VOD or clip link", 24, 20, 512);
        _urlTextBox = CreateTextBox(24, 43, 512, 0);
        _urlTextBox.PlaceholderText = "https://www.twitch.tv/videos/... or .../clip/...";
        _urlTextBox.AccessibleName = "Twitch VOD or clip link";

        var startLabel = CreateLabel("Start time", 24, 96, 248);
        _startTimeInput = new TimeInput("Start time", TimeSpan.Zero)
        {
            Location = new Point(24, 119),
            TabIndex = 1
        };

        var endLabel = CreateLabel("End time", 288, 96, 248);
        _endTimeInput = new TimeInput("End time", TimeSpan.FromSeconds(30))
        {
            Location = new Point(288, 119),
            TabIndex = 2
        };

        var timeHint = new Label
        {
            Text = "Hours  •  minutes  •  seconds    Type a number or use ↑ ↓",
            Location = new Point(24, 158),
            Size = new Size(512, 20),
            ForeColor = MutedTextColor,
            UseMnemonic = false
        };

        _clipButton = new Button
        {
            Text = "Clip MP3",
            Location = new Point(24, 190),
            Size = new Size(512, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = AccentColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            AccessibleName = "Clip MP3",
            TabIndex = 3
        };
        _clipButton.FlatAppearance.BorderSize = 0;
        _clipButton.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        _clipButton.FlatAppearance.MouseDownBackColor = AccentHoverColor;
        _clipButton.Click += ClipButton_Click;

        _statusLabel = new Label
        {
            Text = "Ready",
            Location = new Point(24, 248),
            Size = new Size(512, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = MutedTextColor,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AccessibleName = "Status"
        };

        Controls.AddRange(
        [
            urlLabel,
            _urlTextBox,
            startLabel,
            _startTimeInput,
            endLabel,
            _endTimeInput,
            timeHint,
            _clipButton,
            _statusLabel
        ]);

        AcceptButton = _clipButton;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isBusy)
        {
            e.Cancel = true;
            SetStatus("Wait for the current clip to finish.", ErrorColor);
            return;
        }

        base.OnFormClosing(e);
    }

    private async void ClipButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetRequest(out var url, out var mediaId, out var start, out var end))
        {
            return;
        }

        SetBusy(true);
        SetStatus("Clipping…", MutedTextColor);

        try
        {
            var outputPath = await ClipperRunner.CreateMp3Async(url, mediaId, start, end);
            SetStatus($"Saved {Path.GetFileName(outputPath)}", SuccessColor);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, ErrorColor);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool TryGetRequest(
        out string url,
        out string mediaId,
        out TimeSpan start,
        out TimeSpan end)
    {
        url = string.Empty;
        mediaId = string.Empty;
        start = default;
        end = default;

        if (!TryGetTwitchMedia(_urlTextBox.Text, out url, out mediaId))
        {
            SetStatus("Enter a public Twitch VOD or clip link.", ErrorColor);
            _urlTextBox.Focus();
            return false;
        }

        ValidateChildren();
        start = _startTimeInput.Value;
        end = _endTimeInput.Value;

        if (end <= start)
        {
            SetStatus("End time must be after start time.", ErrorColor);
            _endTimeInput.FocusFirstField();
            return false;
        }

        return true;
    }

    private static bool TryGetTwitchMedia(string value, out string url, out string mediaId)
    {
        url = string.Empty;
        mediaId = string.Empty;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort)
        {
            return false;
        }

        var pathParts = uri.AbsolutePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (IsMainTwitchHost(uri.Host)
            && pathParts.Length == 2
            && pathParts[0].Equals("videos", StringComparison.OrdinalIgnoreCase)
            && pathParts[1].Length > 0
            && pathParts[1].All(character => character is >= '0' and <= '9'))
        {
            mediaId = pathParts[1];
            url = $"https://www.twitch.tv/videos/{mediaId}";
            return true;
        }

        if (IsMainTwitchHost(uri.Host)
            && pathParts.Length == 3
            && IsSafePathPart(pathParts[0])
            && pathParts[1].Equals("clip", StringComparison.OrdinalIgnoreCase)
            && IsSafePathPart(pathParts[2]))
        {
            mediaId = pathParts[2];
            url = $"https://www.twitch.tv/{pathParts[0]}/clip/{mediaId}";
            return true;
        }

        if (uri.Host.Equals("clips.twitch.tv", StringComparison.OrdinalIgnoreCase)
            && pathParts.Length == 1
            && IsSafePathPart(pathParts[0]))
        {
            mediaId = pathParts[0];
            url = $"https://clips.twitch.tv/{mediaId}";
            return true;
        }

        return false;
    }

    private static bool IsMainTwitchHost(string host) =>
        host.Equals("twitch.tv", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.twitch.tv", StringComparison.OrdinalIgnoreCase)
        || host.Equals("m.twitch.tv", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafePathPart(string value) =>
        value.Length > 0
        && value.All(character =>
            character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_');

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _urlTextBox.Enabled = !busy;
        _startTimeInput.Enabled = !busy;
        _endTimeInput.Enabled = !busy;
        _clipButton.Enabled = !busy;
        _clipButton.Text = busy ? "Clipping…" : "Clip MP3";
        UseWaitCursor = busy;
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private static Label CreateLabel(string text, int x, int y, int width) =>
        new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 20),
            ForeColor = Color.FromArgb(45, 45, 50),
            UseMnemonic = false
        };

    private static TextBox CreateTextBox(int x, int y, int width, int tabIndex) =>
        new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            TabIndex = tabIndex
        };

}
