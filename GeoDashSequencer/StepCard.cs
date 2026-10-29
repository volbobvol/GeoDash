namespace GeoDashSequencer;

internal sealed class StepCard : Panel
{
    private readonly SequenceStep _step;
    private readonly Label _title;

    public StepCard(SequenceStep step, Action<SequenceStep> select)
    {
        _step = step;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = step is LoopStep ? Color.FromArgb(229, 244, 240) : Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(6);
        Padding = new Padding(8);
        Width = 440;

        _title = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(25, 82, 77) };
        Controls.Add(_title);
        Click += (_, _) => select(_step);
        _title.Click += (_, _) => select(_step);
        UpdateText();
    }

    public void UpdateText() => _title.Text = _step.Summary;
}