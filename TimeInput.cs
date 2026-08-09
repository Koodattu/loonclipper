namespace LoonClipper;

internal sealed class TimeInput : UserControl
{
    public static readonly TimeSpan MaximumValue = new(999, 59, 59);

    private readonly NumericUpDown _hours;
    private readonly NumericUpDown _minutes;
    private readonly NumericUpDown _seconds;
    private bool _settingValue;

    public event EventHandler? ValueChanged;

    public TimeInput(string accessibleName, TimeSpan initialValue)
    {
        Size = new Size(248, 34);
        BackColor = Color.Transparent;
        AccessibleName = accessibleName;
        AccessibleDescription = "Hours, minutes, and seconds.";
        TabStop = false;

        _hours = CreateNumberInput(0, 62, 999, $"{accessibleName} hours", 0);
        _minutes = CreateNumberInput(82, 62, 59, $"{accessibleName} minutes", 1);
        _seconds = CreateNumberInput(168, 62, 59, $"{accessibleName} seconds", 2);

        _hours.ValueChanged += NumberInput_ValueChanged;
        _minutes.ValueChanged += NumberInput_ValueChanged;
        _seconds.ValueChanged += NumberInput_ValueChanged;

        Controls.AddRange(
        [
            _hours,
            CreateUnitLabel("h", 64, 14),
            _minutes,
            CreateUnitLabel("m", 146, 18),
            _seconds,
            CreateUnitLabel("s", 232, 16)
        ]);

        Value = initialValue;
    }

    public TimeSpan Value
    {
        get => TimeSpan.FromSeconds(
            ((long)_hours.Value * 3600)
            + ((long)_minutes.Value * 60)
            + (long)_seconds.Value);
        set
        {
            if (value < TimeSpan.Zero || value > MaximumValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            var totalSeconds = value.Ticks / TimeSpan.TicksPerSecond;

            _settingValue = true;
            try
            {
                _hours.Value = totalSeconds / 3600;
                _minutes.Value = (totalSeconds / 60) % 60;
                _seconds.Value = totalSeconds % 60;
            }
            finally
            {
                _settingValue = false;
            }

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void FocusFirstField() => _hours.Focus();

    public void SetAccessibleName(string accessibleName)
    {
        AccessibleName = accessibleName;
        _hours.AccessibleName = $"{accessibleName} hours";
        _minutes.AccessibleName = $"{accessibleName} minutes";
        _seconds.AccessibleName = $"{accessibleName} seconds";
    }

    private void NumberInput_ValueChanged(object? sender, EventArgs e)
    {
        if (!_settingValue)
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static NumericUpDown CreateNumberInput(
        int x,
        int width,
        int maximum,
        string accessibleName,
        int tabIndex)
    {
        var input = new NumericUpDown
        {
            Location = new Point(x, 0),
            Size = new Size(width, 34),
            AutoSize = false,
            Minimum = 0,
            Maximum = maximum,
            DecimalPlaces = 0,
            ThousandsSeparator = false,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            AccessibleName = accessibleName,
            TabIndex = tabIndex
        };

        input.Enter += (_, _) => input.BeginInvoke(() => input.Select(0, input.Text.Length));
        return input;
    }

    private static Label CreateUnitLabel(string text, int x, int width) =>
        new()
        {
            Text = text,
            Location = new Point(x, 0),
            Size = new Size(width, 34),
            ForeColor = Color.FromArgb(92, 92, 102),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };
}
