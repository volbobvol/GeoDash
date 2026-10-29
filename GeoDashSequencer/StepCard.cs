namespace GeoDashSequencer;

internal sealed class StepCard : TableLayoutPanel
{
    private readonly SequenceStep _step;
    private readonly Label _title;
    private readonly bool _isSelected;
    private bool _isExecuting;
    private readonly Action<SequenceStep> _startDrag;
    private Point? _dragOrigin;

    public StepCard(SequenceStep step, int cardWidth, bool isSelected, bool isExecuting, Action<SequenceStep> select, Action<SequenceStep> startDrag, Action<SequenceStep> delete)
    {
        _step = step;
        _isSelected = isSelected;
        _isExecuting = isExecuting;
        _startDrag = startDrag;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = step switch
        {
            DelayStep => Color.FromArgb(255, 247, 220),
            WaitForPixelChangeStep => Color.FromArgb(239, 232, 255),
            KeyboardCommandStep => Color.FromArgb(225, 240, 255),
            MouseCommandStep => Color.FromArgb(255, 232, 224),
            LoopStep => Color.FromArgb(229, 244, 240),
            _ => Color.White
        };
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(6);
        Padding = new Padding(8);
        MinimumSize = new Size(cardWidth, 0);
        ColumnCount = isSelected ? 3 : 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (isSelected)
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));

        _title = new Label { AutoSize = true, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), ForeColor = GetAccentColor(step), Text = step.Summary };
        Controls.Add(new StepTypeIcon(step, GetAccentColor(step)) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) }, 0, 0);
        Controls.Add(_title, 1, 0);
        if (isSelected)
        {
            Button deleteButton = new()
            {
                AccessibleDescription = "Delete selected step",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(150, 40, 35),
                Margin = new Padding(2),
                Text = "X",
                UseVisualStyleBackColor = true
            };
            deleteButton.Click += (_, _) => delete(_step);
            Controls.Add(deleteButton, 2, 0);
        }
        Click += (_, _) => select(_step);
        _title.Click += (_, _) => select(_step);
        MouseDown += CardMouseDown;
        MouseMove += CardMouseMove;
        MouseUp += (_, _) => _dragOrigin = null;
        _title.MouseDown += CardMouseDown;
        _title.MouseMove += CardMouseMove;
        _title.MouseUp += (_, _) => _dragOrigin = null;
        Cursor = Cursors.SizeAll;
    }

    public void UpdateText() => _title.Text = _step.Summary;

    public void SetExecuting(bool isExecuting)
    {
        if (_isExecuting == isExecuting)
            return;
        _isExecuting = isExecuting;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (!_isSelected && !_isExecuting)
            return;

        Rectangle border = ClientRectangle;
        border.Inflate(-1, -1);
        if (_isExecuting)
        {
            using Pen executionPen = new(Color.FromArgb(221, 110, 0), 3);
            eventArgs.Graphics.DrawRectangle(executionPen, border);
        }
        if (_isSelected)
        {
            using Pen selectionPen = new(Color.FromArgb(0, 112, 93), 2);
            border.Inflate(-3, -3);
            eventArgs.Graphics.DrawRectangle(selectionPen, border);
        }
    }

    public void AddChildSurface(Control surface)
    {
        int row = RowCount++;
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        SetColumnSpan(surface, ColumnCount);
        Controls.Add(surface, 0, row);
    }

    private void CardMouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
            _dragOrigin = eventArgs.Location;
    }

    private void CardMouseMove(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left || _dragOrigin is not Point origin)
            return;

        Size dragSize = SystemInformation.DragSize;
        Rectangle dragBounds = new(origin.X - dragSize.Width / 2, origin.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
        if (dragBounds.Contains(eventArgs.Location))
            return;

        _dragOrigin = null;
        _startDrag(_step);
    }

    private static Color GetAccentColor(SequenceStep step) => step switch
    {
        DelayStep => Color.FromArgb(130, 92, 0),
        WaitForPixelChangeStep => Color.FromArgb(92, 55, 155),
        KeyboardCommandStep => Color.FromArgb(0, 76, 153),
        MouseCommandStep => Color.FromArgb(155, 55, 25),
        LoopStep => Color.FromArgb(25, 105, 78),
        _ => Color.Black
    };
}