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
    private readonly Label _endTimeLabel;
    private readonly CheckBox _useDurationCheckBox;
    private readonly Button _clipButton;
    private readonly Label _statusLabel;
    private bool _isBusy;
    private bool _updatingTimeInputs;
    private string? _lastAppliedUrlTimestamp;

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
        _urlTextBox.TextChanged += UrlTextBox_TextChanged;

        var startLabel = CreateLabel("Start time", 24, 96, 248);
        _startTimeInput = new TimeInput("Start time", TimeSpan.Zero)
        {
            Location = new Point(24, 119),
            TabIndex = 1
        };

        _endTimeLabel = CreateLabel("End time", 288, 96, 96);
        _endTimeInput = new TimeInput("End time", TimeSpan.FromSeconds(30))
        {
            Location = new Point(288, 119),
            TabIndex = 2
        };

        _useDurationCheckBox = new CheckBox
        {
            Text = "Use duration",
            Location = new Point(392, 89),
            Size = new Size(144, 30),
            CheckAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = MutedTextColor,
            Cursor = Cursors.Hand,
            AccessibleDescription = "Switch the right-hand time input between an end time and a duration.",
            TabIndex = 3
        };

        _startTimeInput.ValueChanged += StartTimeInput_ValueChanged;
        _endTimeInput.ValueChanged += EndTimeInput_ValueChanged;
        _useDurationCheckBox.CheckedChanged += UseDurationCheckBox_CheckedChanged;

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
            Text = "Download & trim",
            Location = new Point(24, 190),
            Size = new Size(512, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = AccentColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            AccessibleName = "Download and trim MP3",
            TabIndex = 4
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
            _endTimeLabel,
            _useDurationCheckBox,
            _endTimeInput,
            timeHint,
            _clipButton,
            _statusLabel
        ]);

        AcceptButton = _clipButton;
    }

    private void UrlTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!TryGetTwitchMedia(
                _urlTextBox.Text,
                out var canonicalUrl,
                out _,
                out var linkedStart)
            || linkedStart is null)
        {
            _lastAppliedUrlTimestamp = null;
            return;
        }

        var timestampKey = $"{canonicalUrl}|{linkedStart.Value.Ticks}";
        if (timestampKey == _lastAppliedUrlTimestamp)
        {
            return;
        }

        _lastAppliedUrlTimestamp = timestampKey;
        _updatingTimeInputs = true;
        try
        {
            _startTimeInput.Value = linkedStart.Value;
            if (!_useDurationCheckBox.Checked)
            {
                _endTimeInput.Value = linkedStart.Value;
            }
        }
        finally
        {
            _updatingTimeInputs = false;
        }

        SetStatus("Start time loaded from the Twitch link.", MutedTextColor);
    }

    private void StartTimeInput_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingTimeInputs || _useDurationCheckBox.Checked)
        {
            return;
        }

        KeepEndAtOrAfterStart();
    }

    private void EndTimeInput_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingTimeInputs || _useDurationCheckBox.Checked)
        {
            return;
        }

        KeepEndAtOrAfterStart();
    }

    private void KeepEndAtOrAfterStart()
    {
        if (_endTimeInput.Value >= _startTimeInput.Value)
        {
            return;
        }

        _updatingTimeInputs = true;
        try
        {
            _endTimeInput.Value = _startTimeInput.Value;
        }
        finally
        {
            _updatingTimeInputs = false;
        }
    }

    private void UseDurationCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _updatingTimeInputs = true;
        try
        {
            if (_useDurationCheckBox.Checked)
            {
                var duration = _endTimeInput.Value - _startTimeInput.Value;
                _endTimeInput.Value = duration > TimeSpan.Zero
                    ? duration
                    : TimeSpan.FromSeconds(30);
                _endTimeLabel.Text = "Duration";
                _endTimeInput.SetAccessibleName("Duration");
            }
            else
            {
                var end = _startTimeInput.Value + _endTimeInput.Value;
                _endTimeInput.Value = end <= TimeInput.MaximumValue
                    ? end
                    : TimeInput.MaximumValue;
                _endTimeLabel.Text = "End time";
                _endTimeInput.SetAccessibleName("End time");
            }
        }
        finally
        {
            _updatingTimeInputs = false;
        }
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
        SetStatus("Downloading preview…", MutedTextColor);

        try
        {
            using var draft = await ClipperRunner.CreateDraftAsync(url, mediaId, start, end);
            SetBusy(false);
            SetStatus("Preview ready. Fine-tune it before saving.", MutedTextColor);

            using var trimForm = new TrimForm(draft);
            if (trimForm.ShowDialog(this) == DialogResult.OK
                && trimForm.SavedPath is not null)
            {
                SetStatus($"Saved {Path.GetFileName(trimForm.SavedPath)}", SuccessColor);
            }
            else
            {
                SetStatus("Preview discarded.", MutedTextColor);
            }
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

        if (!TryGetTwitchMedia(_urlTextBox.Text, out url, out mediaId, out _))
        {
            SetStatus("Enter a public Twitch VOD or clip link.", ErrorColor);
            _urlTextBox.Focus();
            return false;
        }

        ValidateChildren();
        start = _startTimeInput.Value;
        end = _useDurationCheckBox.Checked
            ? start + _endTimeInput.Value
            : _endTimeInput.Value;

        if (end <= start)
        {
            SetStatus(
                _useDurationCheckBox.Checked
                    ? "Duration must be longer than zero."
                    : "End time must be after start time.",
                ErrorColor);
            _endTimeInput.FocusFirstField();
            return false;
        }

        return true;
    }

    private static bool TryGetTwitchMedia(
        string value,
        out string url,
        out string mediaId,
        out TimeSpan? linkedStart)
    {
        url = string.Empty;
        mediaId = string.Empty;
        linkedStart = null;

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
            linkedStart = TryGetLinkedStart(uri);
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

    private static TimeSpan? TryGetLinkedStart(Uri uri)
    {
        foreach (var queryPart in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = queryPart.Split('=', 2);
            if (keyValue.Length == 2
                && keyValue[0].Equals("t", StringComparison.OrdinalIgnoreCase)
                && TimestampText.TryParseTwitchOffset(
                    Uri.UnescapeDataString(keyValue[1]),
                    out var timestamp))
            {
                return timestamp;
            }
        }

        return null;
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
        _useDurationCheckBox.Enabled = !busy;
        _clipButton.Enabled = !busy;
        _clipButton.Text = busy ? "Downloading…" : "Download & trim";
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
