namespace LoonClipper;

internal sealed class TrimForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(248, 248, 250);
    private static readonly Color AccentColor = Color.FromArgb(145, 70, 255);
    private static readonly Color AccentHoverColor = Color.FromArgb(126, 55, 230);
    private static readonly Color MutedTextColor = Color.FromArgb(92, 92, 102);
    private static readonly Color ErrorColor = Color.FromArgb(180, 35, 35);
    private static readonly Color SuccessColor = Color.FromArgb(25, 115, 60);

    private readonly ClipDraft _draft;
    private readonly MciAudioPlayer _player = new();
    private readonly WaveformSelectionControl _waveform;
    private readonly Label _startLabel;
    private readonly Label _selectionLabel;
    private readonly Label _endLabel;
    private readonly Label _statusLabel;
    private readonly Button _playButton;
    private readonly Button _normalizeButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly System.Windows.Forms.Timer _playTimer;

    private bool _loaded;
    private bool _playing;
    private bool _busy;

    public TrimForm(ClipDraft draft)
    {
        _draft = draft;

        Text = "Fine-tune clip — LoonClipper";
        ClientSize = new Size(780, 414);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        BackColor = BackgroundColor;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;

        var titleLabel = new Label
        {
            Text = "Fine-tune your clip",
            Location = new Point(24, 18),
            Size = new Size(732, 28),
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(30, 30, 34),
            UseMnemonic = false
        };

        var subtitleLabel = new Label
        {
            Text = "Drag the purple handles, preview the selection, then save the MP3.",
            Location = new Point(24, 48),
            Size = new Size(732, 22),
            ForeColor = MutedTextColor,
            UseMnemonic = false
        };

        _waveform = new WaveformSelectionControl
        {
            Location = new Point(24, 78),
            Size = new Size(732, 170),
            Enabled = false,
            TabIndex = 0
        };
        _waveform.SelectionChanged += Waveform_SelectionChanged;
        _waveform.SeekRequested += Waveform_SeekRequested;

        var timeFont = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _startLabel = CreateTimeLabel("Start  00:00:00.0", 24, 258, 220, ContentAlignment.MiddleLeft, timeFont);
        _selectionLabel = CreateTimeLabel("Selected  00:00:00.0", 280, 258, 220, ContentAlignment.MiddleCenter, timeFont);
        _endLabel = CreateTimeLabel("End  00:00:00.0", 536, 258, 220, ContentAlignment.MiddleRight, timeFont);

        var keyboardHint = new Label
        {
            Text = "Tip: click inside the selection to seek. Arrow keys move the active handle.",
            Location = new Point(24, 282),
            Size = new Size(732, 22),
            ForeColor = MutedTextColor,
            UseMnemonic = false
        };

        _playButton = CreateButton("▶  Play", 24, 316, 112, primary: false, 1);
        _playButton.Click += PlayButton_Click;

        _normalizeButton = CreateButton("Normalize volume", 148, 316, 164, primary: false, 2);
        _normalizeButton.Click += NormalizeButton_Click;

        _cancelButton = CreateButton("Cancel", 492, 316, 112, primary: false, 4);
        _cancelButton.DialogResult = DialogResult.Cancel;

        _saveButton = CreateButton("Save MP3", 616, 316, 140, primary: true, 3);
        _saveButton.Click += SaveButton_Click;

        _statusLabel = new Label
        {
            Text = "Preparing waveform…",
            Location = new Point(24, 372),
            Size = new Size(732, 26),
            ForeColor = MutedTextColor,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AccessibleName = "Editor status"
        };

        Controls.AddRange(
        [
            titleLabel,
            subtitleLabel,
            _waveform,
            _startLabel,
            _selectionLabel,
            _endLabel,
            keyboardHint,
            _playButton,
            _normalizeButton,
            _cancelButton,
            _saveButton,
            _statusLabel
        ]);

        _playTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _playTimer.Tick += PlayTimer_Tick;

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Shown += TrimForm_Shown;

        SetEditorEnabled(false);
    }

    public string? SavedPath { get; private set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            SetStatus("Wait for the current step to finish.", ErrorColor);
            return;
        }

        _playTimer.Stop();
        _player.Close();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _playTimer.Dispose();
            _player.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void TrimForm_Shown(object? sender, EventArgs e)
    {
        await LoadPreviewAsync(preserveSelection: false);
    }

    private async Task LoadPreviewAsync(bool preserveSelection)
    {
        var previousStart = preserveSelection ? _waveform.SelectionStart : TimeSpan.Zero;
        var previousEnd = preserveSelection ? _waveform.SelectionEnd : (TimeSpan?)null;

        SetBusy(true, "Building waveform…");
        try
        {
            var durationTask = ClipperRunner.GetDurationAsync(_draft);
            var waveformTask = ClipperRunner.CreateWaveformAsync(_draft);
            await Task.WhenAll(durationTask, waveformTask);

            var duration = await durationTask;
            var selectionStart = previousStart < duration
                ? previousStart
                : TimeSpan.Zero;
            var selectionEnd = previousEnd is not null && previousEnd.Value <= duration
                ? previousEnd.Value
                : duration;
            var image = LoadImageWithoutLock(await waveformTask);

            _waveform.SetWaveform(image, duration, selectionStart, selectionEnd);
            _player.Open(_draft.SourcePath);
            _loaded = true;
            UpdateTimeLabels();
            SetStatus(
                _draft.IsNormalized ? "Volume normalized. Waveform updated." : "Ready to preview and trim.",
                _draft.IsNormalized ? SuccessColor : MutedTextColor);
        }
        catch (Exception exception)
        {
            _loaded = false;
            SetStatus(exception.Message, ErrorColor);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PlayButton_Click(object? sender, EventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            if (_playing)
            {
                _waveform.Playhead = _player.Position;
                _player.Pause();
                SetPlaying(false);
                return;
            }

            var playFrom = _waveform.Playhead;
            if (playFrom < _waveform.SelectionStart
                || playFrom >= _waveform.SelectionEnd - TimeSpan.FromMilliseconds(50))
            {
                playFrom = _waveform.SelectionStart;
                _waveform.Playhead = playFrom;
            }

            _player.Play(playFrom, _waveform.SelectionEnd);
            SetPlaying(true);
            SetStatus("Playing selection…", MutedTextColor);
        }
        catch (Exception exception)
        {
            SetPlaying(false);
            SetStatus(exception.Message, ErrorColor);
        }
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var position = _player.Position;
            _waveform.Playhead = position;

            if (position >= _waveform.SelectionEnd - TimeSpan.FromMilliseconds(50)
                || !_player.IsPlaying)
            {
                _waveform.Playhead = _waveform.SelectionEnd;
                SetPlaying(false);
                SetStatus("Ready to preview and trim.", MutedTextColor);
            }
        }
        catch (Exception exception)
        {
            SetPlaying(false);
            SetStatus(exception.Message, ErrorColor);
        }
    }

    private void Waveform_SelectionChanged(object? sender, EventArgs e)
    {
        StopPlayback();
        _waveform.Playhead = _waveform.SelectionStart;
        UpdateTimeLabels();
    }

    private void Waveform_SeekRequested(TimeSpan position)
    {
        StopPlayback();
        _waveform.Playhead = position;
    }

    private async void NormalizeButton_Click(object? sender, EventArgs e)
    {
        if (!_loaded || _draft.IsNormalized)
        {
            return;
        }

        StopPlayback();
        _player.Close();
        SetBusy(true, "Normalizing volume…");

        try
        {
            await ClipperRunner.NormalizeAsync(_draft);
            await LoadPreviewAsync(preserveSelection: true);
        }
        catch (Exception exception)
        {
            TryReopenPlayer();
            SetStatus(exception.Message, ErrorColor);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        StopPlayback();
        _player.Close();
        SetBusy(true, "Saving MP3…");

        try
        {
            SavedPath = await ClipperRunner.SaveAsync(
                _draft,
                _waveform.SelectionStart,
                _waveform.SelectionEnd);
            _busy = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            TryReopenPlayer();
            SetStatus(exception.Message, ErrorColor);
            SetBusy(false);
        }
    }

    private void StopPlayback()
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            _player.Stop();
        }
        catch (InvalidOperationException)
        {
            // The source can be closed briefly while it is normalized or saved.
        }

        SetPlaying(false);
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        _playButton.Text = playing ? "Ⅱ  Pause" : "▶  Play";

        if (playing)
        {
            _playTimer.Start();
        }
        else
        {
            _playTimer.Stop();
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        SetEditorEnabled(!busy && _loaded);
        _cancelButton.Enabled = !busy;
        UseWaitCursor = busy;

        if (status is not null)
        {
            SetStatus(status, MutedTextColor);
        }
    }

    private void SetEditorEnabled(bool enabled)
    {
        _waveform.Enabled = enabled;
        _playButton.Enabled = enabled;
        _normalizeButton.Enabled = enabled && !_draft.IsNormalized;
        _saveButton.Enabled = enabled;
    }

    private void TryReopenPlayer()
    {
        try
        {
            _player.Open(_draft.SourcePath);
            _loaded = true;
        }
        catch (InvalidOperationException)
        {
            _loaded = false;
        }
    }

    private void UpdateTimeLabels()
    {
        _startLabel.Text = $"Start  {FormatPreviewTime(_waveform.SelectionStart)}";
        _selectionLabel.Text = $"Selected  {FormatPreviewTime(_waveform.SelectionEnd - _waveform.SelectionStart)}";
        _endLabel.Text = $"End  {FormatPreviewTime(_waveform.SelectionEnd)}";
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private static string FormatPreviewTime(TimeSpan value)
    {
        var hours = value.Ticks / TimeSpan.TicksPerHour;
        return $"{hours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 100}";
    }

    private static Image LoadImageWithoutLock(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Label CreateTimeLabel(
        string text,
        int x,
        int y,
        int width,
        ContentAlignment alignment,
        Font font) =>
        new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 22),
            Font = font,
            ForeColor = Color.FromArgb(45, 45, 50),
            TextAlign = alignment,
            UseMnemonic = false
        };

    private static Button CreateButton(
        string text,
        int x,
        int y,
        int width,
        bool primary,
        int tabIndex)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 44),
            BackColor = primary ? AccentColor : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(45, 45, 50),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TabIndex = tabIndex
        };

        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(215, 215, 220);
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHoverColor
            : Color.FromArgb(242, 242, 246);
        button.FlatAppearance.MouseDownBackColor = primary
            ? AccentHoverColor
            : Color.FromArgb(232, 232, 238);
        return button;
    }
}
