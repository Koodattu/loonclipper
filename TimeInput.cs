namespace LoonClipper;

internal sealed class TimeInput : UserControl
{
    private readonly NumericUpDown _hours;
    private readonly NumericUpDown _minutes;
    private readonly NumericUpDown _seconds;

    public TimeInput(string accessibleName, TimeSpan initialValue)
    {
        Size = new Size(248, 34);
        BackColor = Color.Transparent;
        AccessibleName = accessibleName;
        AccessibleDescription = "Hours, minutes, and seconds.";
        TabStop = false;

        _hours = CreateNumberInput(0, 62, 999, initialValue.Hours, $"{accessibleName} hours", 0);
        _minutes = CreateNumberInput(82, 62, 59, initialValue.Minutes, $"{accessibleName} minutes", 1);
        _seconds = CreateNumberInput(168, 62, 59, initialValue.Seconds, $"{accessibleName} seconds", 2);

        Controls.AddRange(
        [
            _hours,
            CreateUnitLabel("h", 64, 14),
            _minutes,
            CreateUnitLabel("m", 146, 18),
            _seconds,
            CreateUnitLabel("s", 232, 16)
        ]);
    }

    public TimeSpan Value => TimeSpan.FromSeconds(
        ((long)_hours.Value * 3600)
        + ((long)_minutes.Value * 60)
        + (long)_seconds.Value);

    public void FocusFirstField() => _hours.Focus();

    private static NumericUpDown CreateNumberInput(
        int x,
        int width,
        int maximum,
        int value,
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
            Value = value,
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
