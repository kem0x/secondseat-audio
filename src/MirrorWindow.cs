using NAudio.Wave;

namespace SpeakerAudioMirror;

internal sealed class MirrorWindow : Form
{
    private static readonly bool HighContrast = SystemInformation.HighContrast;
    private static readonly Color Canvas = HighContrast ? SystemColors.Window : Color.FromArgb(244, 247, 245);
    private static readonly Color Card = HighContrast ? SystemColors.Window : Color.White;
    private static readonly Color Ink = HighContrast ? SystemColors.WindowText : Color.FromArgb(25, 44, 37);
    private static readonly Color Muted = HighContrast ? SystemColors.WindowText : Color.FromArgb(77, 98, 88);
    private static readonly Color Border = HighContrast ? SystemColors.WindowText : Color.FromArgb(211, 223, 216);
    private static readonly Color Green = HighContrast ? SystemColors.Highlight : Color.FromArgb(21, 96, 69);
    private static readonly Color OnGreen = HighContrast ? SystemColors.HighlightText : Color.White;
    private static readonly Color Active = HighContrast ? SystemColors.Window : Color.FromArgb(226, 241, 232);
    private static readonly Color Waiting = HighContrast ? SystemColors.WindowText : Color.FromArgb(145, 92, 28);
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 1200 };
    private readonly Label sourceText;
    private readonly ComboBox headphonePicker;
    private readonly Button shareButton;
    private readonly Button testButton;
    private readonly Panel statusPanel;
    private readonly Label statusTitle;
    private readonly Label statusDetail;
    private readonly ToolTip deviceTip = new();
    private readonly Font titleFont = new("Segoe UI", 22, FontStyle.Bold);
    private readonly Font strongFont = new("Segoe UI", 10, FontStyle.Bold);
    private readonly Font smallFont = new("Segoe UI", 9);
    private readonly Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
    private readonly HeadphonePreference preferences;
    private Bitmap? headerIcon;

    private PlaybackDevices devices = new(null, []);
    private PlaybackDevice? remembered;
    private AudioMirror? mirror;
    private WasapiOut? testPlayer;
    private string? testResult;
    private bool shareRequested;
    private bool refreshFailed;
    private bool saveFailed;
    private bool editingList;
    private Color dotColor = Muted;

    internal MirrorWindow(HeadphonePreference? preferences = null)
    {
        this.preferences = preferences ?? new HeadphonePreference();
        remembered = this.preferences.Load();
        Text = Program.AppName;
        if (appIcon is not null)
            Icon = appIcon;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 449);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        SuspendLayout();

        if (appIcon is not null)
        {
            using var largeIcon = new Icon(appIcon, 64, 64);
            headerIcon = largeIcon.ToBitmap();
            Controls.Add(new PictureBox
            {
                Image = headerIcon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new Rectangle(28, 23, 51, 51),
                TabStop = false
            });
        }

        Controls.Add(TextLabel(92, 18, 420, 38, Program.AppName, titleFont, Ink));
        Controls.Add(TextLabel(93, 56, 419, 25, "Share your Windows audio with a friend.", Font, Muted));

        var setupCard = new Panel
        {
            Bounds = new Rectangle(28, 101, 484, 164),
            BackColor = Card,
            BorderStyle = BorderStyle.FixedSingle,
            TabIndex = 0
        };
        Controls.Add(setupCard);
        setupCard.Controls.Add(TextLabel(17, 11, 444, 22, "Windows output", smallFont, Muted));
        sourceText = TextLabel(17, 34, 444, 25, "", strongFont, Ink);
        setupCard.Controls.Add(sourceText);
        setupCard.Controls.Add(new Panel { Bounds = new Rectangle(17, 69, 448, 1), BackColor = Border });
        var headphoneLabel = TextLabel(17, 80, 444, 22, "&Friend’s headphones", strongFont, Ink);
        headphoneLabel.UseMnemonic = true;
        headphoneLabel.TabIndex = 0;
        setupCard.Controls.Add(headphoneLabel);
        headphonePicker = new ComboBox
        {
            Bounds = new Rectangle(17, 110, 448, 32),
            TabIndex = 1,
            AccessibleName = "Friend’s headphones",
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 25,
            IntegralHeight = false,
            MaxDropDownItems = 8,
            BackColor = Card,
            ForeColor = Ink
        };
        headphonePicker.DrawItem += DrawDevice;
        headphonePicker.SelectedIndexChanged += ChooseHeadphones;
        headphonePicker.DropDownClosed += (_, _) => UpdatePicker();
        setupCard.Controls.Add(headphonePicker);

        shareButton = ActionButton(28, 282, 235, "Share audio", primary: true, tabIndex: 1);
        shareButton.Click += (_, _) => ToggleShare();
        testButton = ActionButton(276, 282, 236, "Test both", primary: false, tabIndex: 2);
        testButton.Click += (_, _) => PlayTest();
        Controls.Add(shareButton);
        Controls.Add(testButton);

        statusPanel = new Panel { Bounds = new Rectangle(28, 347, 484, 59), BackColor = Card };
        statusPanel.Paint += PaintStatus;
        statusTitle = TextLabel(34, 7, 437, 22, "", strongFont, Ink);
        statusTitle.AccessibleRole = AccessibleRole.StatusBar;
        statusDetail = TextLabel(34, 28, 437, 23, "", smallFont, Muted);
        statusPanel.Controls.Add(statusTitle);
        statusPanel.Controls.Add(statusDetail);
        Controls.Add(statusPanel);

        Controls.Add(TextLabel(29, 415, 370, 22, "Bluetooth audio can lag behind your speakers.", smallFont, Muted));
        var version = TextLabel(415, 415, 96, 22, $"v{Program.Version} beta", smallFont, Muted);
        version.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(version);
        deviceTip.SetToolTip(testButton, "Play two quiet notes through the Windows output and the mirror.");

        refreshTimer.Tick += (_, _) => RefreshAudio();
        RefreshAudio();
        ResumeLayout(false);
        Shown += (_, _) => refreshTimer.Start();
    }

    private Label TextLabel(int left, int top, int width, int height, string text, Font font, Color color)
        => new()
        {
            Bounds = new Rectangle(left, top, width, height),
            Text = text,
            Font = font,
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            UseMnemonic = false
        };

    private Button ActionButton(int left, int top, int width, string text, bool primary, int tabIndex)
    {
        var button = new Button
        {
            Bounds = new Rectangle(left, top, width, 46),
            Text = text,
            Font = strongFont,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Green : Card,
            ForeColor = primary ? OnGreen : Ink,
            UseVisualStyleBackColor = false,
            TabIndex = tabIndex
        };
        button.FlatAppearance.BorderColor = primary ? Green : Border;
        return button;
    }

    private void DrawDevice(object? sender, DrawItemEventArgs args)
    {
        if (args.Index < 0 || headphonePicker.Items[args.Index] is not DeviceChoice choice)
            return;

        args.DrawBackground();
        var selected = (args.State & DrawItemState.Selected) != 0;
        var color = selected ? SystemColors.HighlightText : choice.Device is null ? Muted : Ink;
        var bounds = Rectangle.Inflate(args.Bounds, -7, 0);
        TextRenderer.DrawText(args.Graphics, choice.Label, Font, bounds, color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        args.DrawFocusRectangle();
    }

    private void PaintStatus(object? sender, PaintEventArgs args)
    {
        using var pen = new Pen(Border);
        args.Graphics.DrawRectangle(pen, 0, 0, statusPanel.Width - 1, statusPanel.Height - 1);
        args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(dotColor);
        float scale = DeviceDpi / 96f;
        args.Graphics.FillEllipse(brush, 15 * scale, 14 * scale, 8 * scale, 8 * scale);
    }

    private void RefreshAudio()
    {
        try
        {
            devices = PlaybackDevices.Read();
            refreshFailed = false;
        }
        catch (Exception error) when (AudioMirror.IsDeviceError(error))
        {
            devices = new PlaybackDevices(null, []);
            refreshFailed = true;
        }

        string outputName = devices.Primary?.Name ?? "No output available";
        if (sourceText.Text != outputName)
        {
            sourceText.Text = outputName;
            deviceTip.SetToolTip(sourceText, outputName);
        }
        UpdatePicker();
        UpdateSharing();
    }

    private void UpdatePicker()
    {
        if (headphonePicker.DroppedDown)
            return;

        var choices = new List<DeviceChoice> { new(null, "Choose headphones…") };
        choices.AddRange(devices.Available.Where(device => device.Id != devices.Primary?.Id)
            .Select(device => new DeviceChoice(device, device.Name)));

        if (remembered is { } saved && choices.All(choice => choice.Device?.Id != saved.Id))
        {
            string suffix = saved.Id == devices.Primary?.Id ? "Windows output" : "disconnected";
            choices.Add(new DeviceChoice(saved, $"{saved.Name} ({suffix})"));
        }

        if (choices.SequenceEqual(headphonePicker.Items.Cast<DeviceChoice>()))
            return;

        editingList = true;
        headphonePicker.BeginUpdate();
        try
        {
            headphonePicker.Items.Clear();
            headphonePicker.Items.AddRange(choices.ToArray());
            headphonePicker.SelectedIndex = choices.FindIndex(choice => choice.Device?.Id == remembered?.Id);
        }
        finally
        {
            headphonePicker.EndUpdate();
            editingList = false;
        }
    }

    private void ChooseHeadphones(object? sender, EventArgs args)
    {
        if (editingList || headphonePicker.SelectedItem is not DeviceChoice choice)
            return;
        if (choice.Device is null)
        {
            // The placeholder is useful on first launch. It must not erase a saved choice.
            if (remembered is { } saved)
            {
                editingList = true;
                headphonePicker.SelectedItem = headphonePicker.Items.Cast<DeviceChoice>()
                    .First(item => item.Device?.Id == saved.Id);
                editingList = false;
            }
            return;
        }

        remembered = choice.Device;
        saveFailed = !preferences.Save(choice.Device);
        testResult = null;
        UpdateSharing();
    }

    private void ToggleShare()
    {
        shareRequested = !shareRequested;
        testResult = null;
        RefreshAudio();
    }

    private PlaybackDevice? ConnectedHeadphones => devices.Available.FirstOrDefault(device =>
        device.Id == remembered?.Id && device.Id != devices.Primary?.Id);

    private void UpdateSharing()
    {
        var source = devices.Primary;
        var target = ConnectedHeadphones;
        if (mirror is { } current && (!shareRequested || !current.IsRunning
            || source?.Id != current.SourceId || target?.Id != current.TargetId))
        {
            StopAudio();
        }

        if (shareRequested && mirror is null && source is not null && target is not null)
        {
            try { mirror = new AudioMirror(source.Id, target.Id); }
            catch (Exception error) when (AudioMirror.IsDeviceError(error)) { }
        }

        headphonePicker.Enabled = !shareRequested;
        shareButton.Text = shareRequested ? "Stop sharing" : "Share audio";
        shareButton.Enabled = shareRequested || (source is not null && target is not null);
        shareButton.BackColor = shareButton.Enabled ? Green : Card;
        shareButton.FlatAppearance.BorderColor = shareButton.Enabled ? Green : Border;
        testButton.Enabled = mirror is not null && testPlayer is null;
        testButton.Text = testPlayer is null ? "Test both" : "Testing…";
        ShowStatus(source, target);
    }

    private void ShowStatus(PlaybackDevice? source, PlaybackDevice? target)
    {
        if (mirror is not null)
        {
            SetStatus("Sharing audio", testPlayer is not null ? "Listen for two quiet notes on both outputs."
                : testResult ?? "Both outputs are playing the same Windows audio.", Green, Active);
        }
        else if (refreshFailed || source is null)
            SetStatus("Windows audio unavailable", "Connect an output or check Windows sound settings.", Waiting, Card);
        else if (remembered is null)
            SetStatus("Choose headphones", "Connect your friend’s headphones in Windows first.", Muted, Card);
        else if (remembered.Id == source.Id)
            SetStatus("Choose a different Windows output", "Set your speakers as the output in Windows sound settings.", Waiting, Card);
        else if (target is null)
            SetStatus("Headphones disconnected", shareRequested ? "Sharing will resume when they reconnect."
                : "Connect your saved headphones or pick another device.", Waiting, Card);
        else if (shareRequested)
            SetStatus("Trying to connect", "Check that both outputs are available in Windows.", Waiting, Card);
        else
            SetStatus("Ready to share", saveFailed ? "Your choice could not be saved for next time."
                : "Start sharing to play the same sound on both devices.", Muted, Card);
    }

    private void SetStatus(string title, string detail, Color color, Color background)
    {
        statusTitle.Text = title;
        statusDetail.Text = detail;
        if (dotColor != color || statusPanel.BackColor != background)
        {
            dotColor = color;
            statusPanel.BackColor = background;
            statusPanel.Invalidate();
        }
    }

    private void PlayTest()
    {
        if (mirror is null || testPlayer is not null)
            return;
        try
        {
            testResult = null;
            testPlayer = TestTone.Start(mirror.SourceId, FinishTest);
        }
        catch (Exception error) when (AudioMirror.IsDeviceError(error))
        {
            testResult = "Couldn’t play the test. Check Windows sound settings.";
        }
        UpdateSharing();
    }

    private void FinishTest(object? sender, StoppedEventArgs args)
    {
        if (testPlayer is not { } player || !ReferenceEquals(player, sender))
            return;
        testPlayer = null;
        player.Dispose();
        testResult = args.Exception is null ? "Test finished. Did you hear both notes on each output?"
            : "The test audio was interrupted. Try again.";
        UpdateSharing();
    }

    private void StopAudio()
    {
        var tone = testPlayer;
        testPlayer = null;
        tone?.Dispose();
        mirror?.Dispose();
        mirror = null;
        testResult = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs args)
    {
        refreshTimer.Stop();
        StopAudio();
        base.OnFormClosed(args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            deviceTip.Dispose();
        }
        base.Dispose(disposing);
        if (disposing)
        {
            Font.Dispose();
            titleFont.Dispose();
            strongFont.Dispose();
            smallFont.Dispose();
            headerIcon?.Dispose();
            appIcon?.Dispose();
        }
    }

    private sealed record DeviceChoice(PlaybackDevice? Device, string Label)
    {
        public override string ToString() => Label;
    }
}
