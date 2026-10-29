namespace GeoDashSequencer;

public sealed class MainForm : Form
{
    private readonly List<SequenceStep> _steps = [];
    private readonly FlowLayoutPanel _sequenceSurface = CreateSurface();
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = false };
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(8), Text = "Ready" };
    private SequenceStep? _selectedStep;
    private CancellationTokenSource? _runCancellation;

    public MainForm()
    {
        Text = "GeoDash Sequencer";
        Width = 1150;
        Height = 750;
        MinimumSize = new Size(850, 550);
        StartPosition = FormStartPosition.CenterScreen;

        ToolStrip tools = new() { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add("Save", null, (_, _) => SaveSequence());
        tools.Items.Add("Load", null, (_, _) => LoadSequence());
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add("Run", null, async (_, _) => await RunSequenceAsync());
        tools.Items.Add("Stop", null, (_, _) => _runCancellation?.Cancel());
        tools.Items.Add("Delete selected", null, (_, _) => DeleteSelected());

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(CreatePalette(), 0, 0);
        layout.Controls.Add(_sequenceSurface, 1, 0);
        layout.Controls.Add(_properties, 2, 0);
        layout.Controls.Add(_status, 0, 1);
        layout.SetColumnSpan(_status, 3);

        Controls.Add(layout);
        Controls.Add(tools);
        tools.Dock = DockStyle.Top;
        _properties.PropertyValueChanged += (_, _) => RebuildSurface();
        RebuildSurface();
    }

    private Control CreatePalette()
    {
        FlowLayoutPanel palette = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), BackColor = Color.FromArgb(244, 247, 246) };
        palette.Controls.Add(new Label { Text = "Components", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) });
        palette.Controls.Add(CreateDragButton("Delay", "delay"));
        palette.Controls.Add(CreateDragButton("Keyboard command", "keyboard"));
        palette.Controls.Add(CreateDragButton("Mouse command", "mouse"));
        palette.Controls.Add(CreateDragButton("Loop", "loop"));
        palette.Controls.Add(new Label { Text = "Drag a component onto the sequence or a loop.", AutoSize = true, MaximumSize = new Size(130, 0), Margin = new Padding(3, 18, 3, 3) });
        return palette;
    }

    private Button CreateDragButton(string text, string type)
    {
        Button button = new() { Text = text, Width = 130, Height = 36, FlatStyle = FlatStyle.Flat };
        button.MouseDown += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
                button.DoDragDrop(type, DragDropEffects.Copy);
        };
        return button;
    }

    private static FlowLayoutPanel CreateSurface()
    {
        FlowLayoutPanel surface = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AllowDrop = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(250, 252, 251)
        };
        return surface;
    }

    private void RebuildSurface()
    {
        BuildSurface(_sequenceSurface, _steps);
        _properties.SelectedObject = _selectedStep;
    }

    private void BuildSurface(FlowLayoutPanel surface, List<SequenceStep> steps)
    {
        surface.Controls.Clear();
        surface.DragEnter -= SurfaceDragEnter;
        surface.DragDrop -= SurfaceDragDrop;
        surface.DragEnter += SurfaceDragEnter;
        surface.DragDrop += SurfaceDragDrop;
        surface.Tag = steps;
        if (steps.Count == 0)
            surface.Controls.Add(new Label { Text = "Drag components here", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(8) });

        foreach (SequenceStep step in steps)
        {
            StepCard card = new(step, SelectStep);
            surface.Controls.Add(card);
            if (step is not LoopStep loop)
                continue;

            FlowLayoutPanel children = CreateSurface();
            children.Dock = DockStyle.None;
            children.Width = 405;
            children.MaximumSize = new Size(405, 0);
            children.Margin = new Padding(16, 8, 0, 0);
            BuildSurface(children, loop.Steps);
            card.Controls.Add(children);
        }
    }

    private void SurfaceDragEnter(object? sender, DragEventArgs eventArgs) => eventArgs.Effect = eventArgs.Data?.GetDataPresent(typeof(string)) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void SurfaceDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (sender is not FlowLayoutPanel surface || surface.Tag is not List<SequenceStep> target || eventArgs.Data?.GetData(typeof(string)) is not string type)
            return;

        target.Add(type switch
        {
            "delay" => new DelayStep(),
            "keyboard" => new KeyboardCommandStep(),
            "mouse" => new MouseCommandStep(),
            "loop" => new LoopStep(),
            _ => throw new InvalidOperationException("Unknown component type.")
        });
        RebuildSurface();
    }

    private void SelectStep(SequenceStep step)
    {
        _selectedStep = step;
        _properties.SelectedObject = step;
        _status.Text = $"Selected: {step.Summary}";
    }

    private void DeleteSelected()
    {
        if (_selectedStep is null || !RemoveStep(_steps, _selectedStep))
            return;
        _selectedStep = null;
        _properties.SelectedObject = null;
        RebuildSurface();
    }

    private static bool RemoveStep(List<SequenceStep> steps, SequenceStep target)
    {
        if (steps.Remove(target))
            return true;
        return steps.OfType<LoopStep>().Any(loop => RemoveStep(loop.Steps, target));
    }

    private void SaveSequence()
    {
        using SaveFileDialog dialog = new() { Filter = "Sequence files (*.gds)|*.gds|Text files (*.txt)|*.txt", DefaultExt = "gds" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        SequenceTextFile.Save(dialog.FileName, _steps);
        _status.Text = $"Saved {dialog.FileName}";
    }

    private void LoadSequence()
    {
        using OpenFileDialog dialog = new() { Filter = "Sequence files (*.gds;*.txt)|*.gds;*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _steps.Clear();
            _steps.AddRange(SequenceTextFile.Load(dialog.FileName));
            _selectedStep = null;
            RebuildSurface();
            _status.Text = $"Loaded {dialog.FileName}";
        }
        catch (Exception exception) when (exception is IOException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Could not load sequence", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunSequenceAsync()
    {
        if (_runCancellation is not null)
            return;
        _runCancellation = new CancellationTokenSource();
        _status.Text = "Starting in 3 seconds. Focus the target application.";
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), _runCancellation.Token);
            _status.Text = "Running. Inputs are sent to the active application.";
            await InputRunner.RunAsync(_steps, _runCancellation.Token);
            _status.Text = "Sequence complete.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Sequence stopped.";
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }
}