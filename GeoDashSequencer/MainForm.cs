namespace GeoDashSequencer;

public sealed class MainForm : Form
{
    private readonly List<SequenceStep> _steps = [];
    private readonly FlowLayoutPanel _sequenceSurface = CreateSurface();
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = false, AllowDrop = true };
    private readonly Label _pointerCoordinates = new() { AutoSize = true, BackColor = Color.FromArgb(245, 245, 245), Dock = DockStyle.Bottom, ForeColor = Color.DimGray, Padding = new Padding(8), Visible = false };
    private readonly System.Windows.Forms.Timer _pointerTimer = new() { Interval = 100 };
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(8), Text = "Ready" };
    private readonly Label _deleteTarget = new() { AllowDrop = true, AutoSize = true, BackColor = Color.FromArgb(255, 232, 232), BorderStyle = BorderStyle.FixedSingle, ForeColor = Color.FromArgb(140, 35, 35), Padding = new Padding(8), Text = "Drop here to delete", Visible = false };
    private readonly Panel _dropIndicator = new() { BackColor = Color.FromArgb(0, 112, 93), Height = 4, Margin = new Padding(6, 2, 6, 2) };
    private readonly Dictionary<SequenceStep, StepCard> _cards = [];
    private readonly List<ToolStripItem> _editingToolItems = [];
    private readonly Control _palette;
    private SequenceStep? _selectedStep;
    private SequenceStep? _executingStep;
    private CancellationTokenSource? _runCancellation;
    private FlowLayoutPanel? _indicatorSurface;
    private int _indicatorIndex = -1;

    public MainForm()
    {
        Text = "GeoDash Sequencer";
        Width = 1150;
        Height = 750;
        MinimumSize = new Size(850, 550);
        StartPosition = FormStartPosition.CenterScreen;

        ToolStrip tools = new() { GripStyle = ToolStripGripStyle.Hidden };
        _editingToolItems.Add(tools.Items.Add("Save", null, (_, _) => SaveSequence()));
        _editingToolItems.Add(tools.Items.Add("Load", null, (_, _) => LoadSequence()));
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add("Run", null, async (_, _) => await RunSequenceAsync());
        tools.Items.Add("Stop", null, (_, _) => _runCancellation?.Cancel());
        _editingToolItems.Add(tools.Items.Add("Delete selected", null, (_, _) => DeleteSelected()));

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _palette = CreatePalette();
        layout.Controls.Add(_palette, 0, 0);
        layout.Controls.Add(_sequenceSurface, 1, 0);
        layout.Controls.Add(CreatePropertiesPanel(), 2, 0);
        FlowLayoutPanel footer = new() { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0) };
        footer.Controls.Add(_status);
        footer.Controls.Add(_deleteTarget);
        layout.Controls.Add(footer, 0, 1);
        layout.SetColumnSpan(footer, 3);

        Controls.Add(layout);
        Controls.Add(tools);
        tools.Dock = DockStyle.Top;
        _properties.PropertyValueChanged += (_, _) => RebuildSurface();
        _deleteTarget.DragEnter += DeleteTargetDragEnter;
        _deleteTarget.DragDrop += DeleteTargetDragDrop;
        _palette.DragEnter += DeleteTargetDragEnter;
        _palette.DragDrop += DeleteTargetDragDrop;
        _properties.DragEnter += DeleteTargetDragEnter;
        _properties.DragDrop += DeleteTargetDragDrop;
        _pointerTimer.Tick += (_, _) => UpdatePointerCoordinates();
        _pointerTimer.Start();
        RebuildSurface();
    }

    private Control CreatePropertiesPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill };
        panel.Controls.Add(_properties);
        panel.Controls.Add(_pointerCoordinates);
        return panel;
    }

    private Control CreatePalette()
    {
        FlowLayoutPanel palette = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), BackColor = Color.FromArgb(244, 247, 246) };
        palette.Controls.Add(new Label { Text = "Components", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) });
        palette.Controls.Add(CreateDragButton("Delay", "delay"));
        palette.Controls.Add(CreateDragButton("Wait for pixel change", "pixel-change"));
        palette.Controls.Add(CreateDragButton("Keyboard command", "keyboard"));
        palette.Controls.Add(CreateDragButton("Mouse command", "mouse"));
        palette.Controls.Add(CreateDragButton("Loop", "loop"));
        palette.Controls.Add(new Label { Text = "Drag cards to reorder, into loops, or to the delete target.", AutoSize = true, MaximumSize = new Size(130, 0), Margin = new Padding(3, 18, 3, 3) });
        return palette;
    }

    private Button CreateDragButton(string text, string type)
    {
        Button button = new() { Text = text, Width = 130, Height = 36, FlatStyle = FlatStyle.Flat };
        button.MouseDown += (_, eventArgs) =>
        {
            if (_runCancellation is null && eventArgs.Button == MouseButtons.Left)
                button.DoDragDrop(type, DragDropEffects.Copy);
        };
        return button;
    }

    private static FlowLayoutPanel CreateSurface()
    {
        FlowLayoutPanel surface = new SequenceSurface
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
        SuspendLayout();
        _sequenceSurface.SuspendLayout();
        try
        {
            _cards.Clear();
            BuildSurface(_sequenceSurface, _steps, 440);
            _properties.SelectedObject = _selectedStep;
            UpdatePointerCoordinates();
        }
        finally
        {
            _sequenceSurface.ResumeLayout(true);
            ResumeLayout(true);
        }
    }

    private void BuildSurface(FlowLayoutPanel surface, List<SequenceStep> steps, int cardWidth)
    {
        surface.SuspendLayout();
        surface.Controls.Clear();
        surface.DragEnter -= SurfaceDragEnter;
        surface.DragOver -= SurfaceDragOver;
        surface.DragLeave -= SurfaceDragLeave;
        surface.DragDrop -= SurfaceDragDrop;
        surface.DragEnter += SurfaceDragEnter;
        surface.DragOver += SurfaceDragOver;
        surface.DragLeave += SurfaceDragLeave;
        surface.DragDrop += SurfaceDragDrop;
        surface.Tag = steps;
        if (steps.Count == 0)
            surface.Controls.Add(new Label { Text = "Drag components here", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(8) });

        foreach (SequenceStep step in steps)
        {
            StepCard card = new(step, cardWidth, ReferenceEquals(step, _selectedStep), ReferenceEquals(step, _executingStep), SelectStep, draggedStep => BeginStepDrag(draggedStep, steps), DeleteStep);
            _cards[step] = card;
            card.AllowDrop = true;
            card.DragEnter += (_, eventArgs) => HandleSurfaceDragEnter(surface, eventArgs);
            card.DragOver += (_, eventArgs) => HandleSurfaceDragOver(surface, eventArgs);
            card.DragLeave += (_, _) => HandleSurfaceDragLeave(surface);
            card.DragDrop += (_, eventArgs) => HandleSurfaceDragDrop(surface, eventArgs);
            surface.Controls.Add(card);
            if (step is not LoopStep loop)
                continue;

            int childSurfaceWidth = Math.Max(220, cardWidth - 50);
            int childCardWidth = Math.Max(180, childSurfaceWidth - 36);
            FlowLayoutPanel children = CreateSurface();
            children.Dock = DockStyle.None;
            children.AutoScroll = false;
            children.AutoSize = true;
            children.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            children.Width = childSurfaceWidth;
            children.MinimumSize = new Size(childSurfaceWidth, 70);
            children.MaximumSize = new Size(childSurfaceWidth, 0);
            children.Margin = new Padding(16, 8, 0, 0);
            children.BorderStyle = BorderStyle.FixedSingle;
            BuildSurface(children, loop.Steps, childCardWidth);
            card.AddChildSurface(children);
        }
        surface.ResumeLayout(true);
    }

    private void SurfaceDragEnter(object? sender, DragEventArgs eventArgs)
    {
        if (sender is FlowLayoutPanel surface && surface.Tag is List<SequenceStep> target)
            HandleSurfaceDragEnter(surface, eventArgs);
    }

    private void SurfaceDragOver(object? sender, DragEventArgs eventArgs)
    {
        if (sender is not FlowLayoutPanel surface || surface.Tag is not List<SequenceStep> target)
            return;

        HandleSurfaceDragOver(surface, eventArgs);
    }

    private void SurfaceDragLeave(object? sender, EventArgs eventArgs)
    {
        if (sender is FlowLayoutPanel surface && ReferenceEquals(surface, _indicatorSurface))
            HandleSurfaceDragLeave(surface);
    }

    private void SurfaceDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (sender is not FlowLayoutPanel surface || surface.Tag is not List<SequenceStep> target)
            return;

        HandleSurfaceDragDrop(surface, eventArgs);
    }

    private void HandleSurfaceDragEnter(FlowLayoutPanel surface, DragEventArgs eventArgs)
    {
        if (_runCancellation is not null)
        {
            eventArgs.Effect = DragDropEffects.None;
            return;
        }
        if (surface.Tag is List<SequenceStep> target)
            eventArgs.Effect = GetDropEffect(eventArgs.Data, target);
    }

    private void HandleSurfaceDragOver(FlowLayoutPanel surface, DragEventArgs eventArgs)
    {
        if (_runCancellation is not null)
        {
            eventArgs.Effect = DragDropEffects.None;
            return;
        }
        if (surface.Tag is not List<SequenceStep> target)
            return;

        eventArgs.Effect = GetDropEffect(eventArgs.Data, target);
        if (eventArgs.Effect != DragDropEffects.None)
            ShowDropIndicator(surface, eventArgs.X, eventArgs.Y);
    }

    private void HandleSurfaceDragLeave(FlowLayoutPanel surface)
    {
        if (ReferenceEquals(surface, _indicatorSurface))
            RemoveDropIndicator();
    }

    private void HandleSurfaceDragDrop(FlowLayoutPanel surface, DragEventArgs eventArgs)
    {
        if (_runCancellation is not null || surface.Tag is not List<SequenceStep> target)
            return;

        int insertIndex = GetInsertIndex(surface, eventArgs.X, eventArgs.Y);
        RemoveDropIndicator();
        if (eventArgs.Data?.GetData(typeof(string)) is string type)
        {
            target.Insert(insertIndex, CreateStep(type));
        }
        else if (eventArgs.Data?.GetData(typeof(StepDragData)) is StepDragData payload && CanMove(payload, target))
        {
            int sourceIndex = payload.Source.IndexOf(payload.Step);
            if (sourceIndex < 0)
                return;
            payload.Source.RemoveAt(sourceIndex);
            if (ReferenceEquals(payload.Source, target) && sourceIndex < insertIndex)
                insertIndex--;
            target.Insert(Math.Clamp(insertIndex, 0, target.Count), payload.Step);
        }
        else
        {
            return;
        }
        RebuildSurface();
    }

    private static SequenceStep CreateStep(string type) => type switch
    {
        "delay" => new DelayStep(),
        "pixel-change" => new WaitForPixelChangeStep(),
        "keyboard" => new KeyboardCommandStep(),
        "mouse" => new MouseCommandStep(),
        "loop" => new LoopStep(),
        _ => throw new InvalidOperationException("Unknown component type.")
    };

    private static DragDropEffects GetDropEffect(IDataObject? data, List<SequenceStep> target)
    {
        if (data?.GetDataPresent(typeof(string)) == true)
            return DragDropEffects.Copy;
        return data?.GetData(typeof(StepDragData)) is StepDragData payload && CanMove(payload, target)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private static bool CanMove(StepDragData payload, List<SequenceStep> target) =>
        payload.Source.Contains(payload.Step) &&
        (payload.Step is not LoopStep loop || !ContainsList(loop.Steps, target));

    private static bool ContainsList(List<SequenceStep> steps, List<SequenceStep> target) =>
        ReferenceEquals(steps, target) || steps.OfType<LoopStep>().Any(loop => ContainsList(loop.Steps, target));

    private void BeginStepDrag(SequenceStep step, List<SequenceStep> source)
    {
        if (_runCancellation is not null)
            return;
        _deleteTarget.Visible = true;
        _status.Text = "Drop on a teal line to move, inside a loop to nest, or on delete to remove.";
        try
        {
            DoDragDrop(new StepDragData(step, source), DragDropEffects.Move);
        }
        finally
        {
            RemoveDropIndicator();
            _deleteTarget.Visible = false;
        }
    }

    private void ShowDropIndicator(FlowLayoutPanel surface, int screenX, int screenY)
    {
        int index = GetInsertIndex(surface, screenX, screenY);
        if (ReferenceEquals(surface, _indicatorSurface) && _indicatorIndex == index)
            return;

        RemoveDropIndicator();
        surface.SuspendLayout();
        _dropIndicator.Width = Math.Max(100, surface.ClientSize.Width - surface.Padding.Horizontal - 24);
        List<StepCard> cards = surface.Controls.OfType<StepCard>().ToList();
        int controlIndex = index < cards.Count
            ? surface.Controls.IndexOf(cards[index])
            : cards.Count == 0 ? surface.Controls.Count : surface.Controls.IndexOf(cards[^1]) + 1;
        surface.Controls.Add(_dropIndicator);
        surface.Controls.SetChildIndex(_dropIndicator, controlIndex);
        _indicatorSurface = surface;
        _indicatorIndex = index;
        surface.ResumeLayout(true);
    }

    private void RemoveDropIndicator()
    {
        if (_indicatorSurface is not null)
        {
            _indicatorSurface.SuspendLayout();
            _indicatorSurface.Controls.Remove(_dropIndicator);
            _indicatorSurface.ResumeLayout(true);
        }
        _indicatorSurface = null;
        _indicatorIndex = -1;
    }

    private static int GetInsertIndex(FlowLayoutPanel surface, int screenX, int screenY)
    {
        int y = surface.PointToClient(new Point(screenX, screenY)).Y;
        List<StepCard> cards = surface.Controls.OfType<StepCard>().ToList();
        for (int index = 0; index < cards.Count; index++)
        {
            StepCard card = cards[index];
            if (y < card.Top + card.Height / 2)
                return index;
        }
        return cards.Count;
    }

    private static void DeleteTargetDragEnter(object? sender, DragEventArgs eventArgs) =>
        eventArgs.Effect = eventArgs.Data?.GetData(typeof(StepDragData)) is StepDragData ? DragDropEffects.Move : DragDropEffects.None;

    private void DeleteTargetDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(typeof(StepDragData)) is not StepDragData payload || !payload.Source.Remove(payload.Step))
            return;

        if (ReferenceEquals(_selectedStep, payload.Step))
        {
            _selectedStep = null;
            _properties.SelectedObject = null;
        }
        _status.Text = "Step deleted.";
        RebuildSurface();
    }

    private void SelectStep(SequenceStep step)
    {
        if (_runCancellation is not null)
            return;
        _selectedStep = step;
        _status.Text = $"Selected: {step.Summary}";
        RebuildSurface();
    }

    private void UpdatePointerCoordinates()
    {
        bool showCoordinates = _selectedStep is WaitForPixelChangeStep;
        _pointerCoordinates.Visible = showCoordinates;
        if (showCoordinates)
        {
            Point position = Cursor.Position;
            _pointerCoordinates.Text = $"Current mouse position: X {position.X}, Y {position.Y}";
        }
    }

    private void DeleteSelected()
    {
        if (_selectedStep is not null)
            DeleteStep(_selectedStep);
    }

    private void DeleteStep(SequenceStep step)
    {
        if (_runCancellation is not null)
            return;
        if (!RemoveStep(_steps, step))
            return;
        _selectedStep = null;
        _properties.SelectedObject = null;
        _status.Text = "Step deleted.";
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
            List<SequenceStep> loadedSteps = SequenceTextFile.Load(dialog.FileName);
            _steps.Clear();
            _steps.AddRange(loadedSteps);
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
        SetEditingEnabled(false);
        try
        {
            _ = SequenceValidator.CreateExecutionSnapshot(_steps);
            _status.Text = "Starting in 3 seconds. Focus the target application.";
            await Task.Delay(TimeSpan.FromSeconds(3), _runCancellation.Token);
            _status.Text = "Running. Inputs are sent to the active application.";
            await Task.Run(() => InputRunner.RunAsync(_steps, _runCancellation.Token, ReportExecutingStep));
            _status.Text = "Sequence complete.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Sequence stopped.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            _status.Text = "Sequence failed.";
            MessageBox.Show(this, exception.Message, "Could not run sequence", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetExecutingStep(null);
            SetEditingEnabled(true);
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        _palette.Enabled = enabled;
        _properties.Enabled = enabled;
        _sequenceSurface.AllowDrop = enabled;
        foreach (ToolStripItem item in _editingToolItems)
            item.Enabled = enabled;
    }

    private void ReportExecutingStep(SequenceStep step)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(() =>
        {
            if (_runCancellation is not null)
                SetExecutingStep(step);
        });
    }

    private void SetExecutingStep(SequenceStep? step)
    {
        if (ReferenceEquals(_executingStep, step))
            return;
        if (_executingStep is not null && _cards.TryGetValue(_executingStep, out StepCard? previousCard))
            previousCard.SetExecuting(false);
        _executingStep = step;
        if (_executingStep is not null && _cards.TryGetValue(_executingStep, out StepCard? currentCard))
            currentCard.SetExecuting(true);
    }

    private sealed record StepDragData(SequenceStep Step, List<SequenceStep> Source);
}