using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace GutoCrosshair;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal enum CrosshairShape { Cross, Dot, Circle, CrossDot }

internal sealed class AppSettings
{
    public CrosshairShape Shape { get; set; } = CrosshairShape.Cross;
    public string ColorHex { get; set; } = "#00F58A";
    public int Size { get; set; } = 13;
    public int Gap { get; set; } = 5;
    public int Thickness { get; set; } = 2;
    public int Opacity { get; set; } = 95;
    public bool Enabled { get; set; } = true;
}

internal sealed class MainForm : Form
{
    private const int HotkeyId = 0x4755;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int WmSetRedraw = 0x000B;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GutoCrosshair", "settings.json");
    private AppSettings settings = new();
    private readonly OverlayForm overlay;
    private readonly ComboBox shapeBox = new();
    private readonly TrackBar sizeBar = MakeTrack(2, 40, 13);
    private readonly TrackBar gapBar = MakeTrack(0, 25, 5);
    private readonly TrackBar thicknessBar = MakeTrack(1, 8, 2);
    private readonly TrackBar opacityBar = MakeTrack(20, 100, 95);
    private readonly Label colorSwatch = new();
    private readonly Label statusLabel = new();
    private readonly Button toggleButton = new();
    private bool loaded;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainForm()
    {
        settings = LoadSettings();
        overlay = new OverlayForm(settings);
        Text = "Guto Crosshair";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(490, 650);
        BackColor = Color.FromArgb(14, 18, 27);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        BuildInterface();
        ApplySettingsToControls();
        loaded = true;
        ApplySettings();
    }

    private static TrackBar MakeTrack(int min, int max, int value) => new()
    {
        Minimum = min, Maximum = max, Value = value, TickStyle = TickStyle.None,
        Height = 34, Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 34, 47)
    };

    private void BuildInterface()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 105, BackColor = Color.FromArgb(18, 25, 36), Padding = new Padding(22, 16, 22, 10) };
        var logo = new Label { Text = "GUTO  CROSSHAIR", AutoSize = true, Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(0, 245, 138), Location = new Point(20, 14) };
        var subtitle = new Label { Text = "PRECISION OVERLAY  •  WINDOWS", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(143, 157, 177), Location = new Point(23, 54) };
        header.Controls.Add(logo); header.Controls.Add(subtitle); Controls.Add(header);

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 15, 22, 10), AutoScroll = true };
        Controls.Add(content);
        content.BringToFront(); header.BringToFront();

        var shapeLabel = MakeLabel("FORMATO DA MIRA"); shapeLabel.Location = new Point(22, 8); content.Controls.Add(shapeLabel);
        shapeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        shapeBox.Items.AddRange(new object[] { "Cruz", "Ponto", "Círculo", "Cruz + ponto" });
        StyleCombo(shapeBox); shapeBox.Location = new Point(22, 31); shapeBox.Size = new Size(420, 34); content.Controls.Add(shapeBox);
        shapeBox.SelectedIndexChanged += (_, _) => { if (loaded) { settings.Shape = (CrosshairShape)shapeBox.SelectedIndex; ApplySettings(); } };

        int y = 84;
        AddSlider(content, "TAMANHO", sizeBar, ref y, v => settings.Size = v);
        AddSlider(content, "ESPAÇAMENTO", gapBar, ref y, v => settings.Gap = v);
        AddSlider(content, "ESPESSURA", thicknessBar, ref y, v => settings.Thickness = v);
        AddSlider(content, "OPACIDADE", opacityBar, ref y, v => settings.Opacity = v);

        var colorTitle = MakeLabel("COR DA MIRA"); colorTitle.Location = new Point(22, y + 4); content.Controls.Add(colorTitle);
        var colorButton = MakeButton("ESCOLHER COR", Color.FromArgb(43, 55, 73));
        colorButton.Location = new Point(22, y + 28); colorButton.Size = new Size(180, 38); content.Controls.Add(colorButton);
        colorSwatch.Location = new Point(215, y + 28); colorSwatch.Size = new Size(44, 38); colorSwatch.BorderStyle = BorderStyle.FixedSingle; content.Controls.Add(colorSwatch);
        colorButton.Click += (_, _) => ChooseColor();
        y += 82;

        toggleButton.Text = "OCULTAR MIRA  •  F8"; toggleButton.Location = new Point(22, y + 5); toggleButton.Size = new Size(420, 46);
        toggleButton.FlatStyle = FlatStyle.Flat; toggleButton.FlatAppearance.BorderSize = 0; toggleButton.BackColor = Color.FromArgb(0, 164, 92); toggleButton.ForeColor = Color.White; toggleButton.Font = new Font("Segoe UI", 10, FontStyle.Bold); content.Controls.Add(toggleButton);
        toggleButton.Click += (_, _) => ToggleOverlay();
        y += 62;

        statusLabel.Text = "●  MIRA ATIVADA"; statusLabel.ForeColor = Color.FromArgb(0, 245, 138); statusLabel.AutoSize = true; statusLabel.Location = new Point(22, y + 4); content.Controls.Add(statusLabel);
        var hint = new Label { Text = "F8  Mostrar/ocultar   •   Feche pelo botão X da janela\nAs configurações são salvas automaticamente.", AutoSize = false, Size = new Size(420, 42), Location = new Point(22, y + 28), ForeColor = Color.FromArgb(143, 157, 177), Font = new Font("Segoe UI", 8) }; content.Controls.Add(hint);
        var disclaimer = new Label { Text = "Overlay externo: funciona melhor em modo janela sem bordas. A compatibilidade depende do jogo, do driver e das regras de cada servidor.", AutoSize = false, Size = new Size(420, 52), Location = new Point(22, y + 72), ForeColor = Color.FromArgb(112, 126, 147), Font = new Font("Segoe UI", 8) }; content.Controls.Add(disclaimer);

        foreach (var bar in new[] { sizeBar, gapBar, thicknessBar, opacityBar })
            bar.ValueChanged += (_, _) => { if (loaded) ApplySettings(); };
    }

    private void AddSlider(Control parent, string title, TrackBar bar, ref int y, Action<int> update)
    {
        var label = MakeLabel(title); label.Location = new Point(22, y); parent.Controls.Add(label);
        var value = new Label { AutoSize = true, ForeColor = Color.FromArgb(0, 245, 138), Location = new Point(405, y), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        parent.Controls.Add(value); value.Text = bar.Value.ToString();
        bar.Location = new Point(18, y + 20); bar.Size = new Size(430, 34); parent.Controls.Add(bar);
        bar.ValueChanged += (_, _) => { value.Text = bar.Value.ToString(); update(bar.Value); if (loaded) ApplySettings(); };
        y += 65;
    }

    private static Label MakeLabel(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.FromArgb(207, 217, 230), Font = new Font("Segoe UI", 8, FontStyle.Bold) };
    private static void StyleCombo(ComboBox combo) { combo.FlatStyle = FlatStyle.Flat; combo.BackColor = Color.FromArgb(27, 34, 47); combo.ForeColor = Color.White; combo.Font = new Font("Segoe UI", 9); }
    private static Button MakeButton(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };

    private void ApplySettingsToControls()
    {
        shapeBox.SelectedIndex = (int)settings.Shape;
        sizeBar.Value = Math.Clamp(settings.Size, sizeBar.Minimum, sizeBar.Maximum);
        gapBar.Value = Math.Clamp(settings.Gap, gapBar.Minimum, gapBar.Maximum);
        thicknessBar.Value = Math.Clamp(settings.Thickness, thicknessBar.Minimum, thicknessBar.Maximum);
        opacityBar.Value = Math.Clamp(settings.Opacity, opacityBar.Minimum, opacityBar.Maximum);
        colorSwatch.BackColor = ParseColor(settings.ColorHex);
        toggleButton.Text = settings.Enabled ? "OCULTAR MIRA  •  F8" : "MOSTRAR MIRA  •  F8";
    }

    private void ApplySettings()
    {
        if (!loaded) return;
        settings.Shape = (CrosshairShape)Math.Max(0, shapeBox.SelectedIndex);
        settings.Size = sizeBar.Value; settings.Gap = gapBar.Value;
        settings.Thickness = thicknessBar.Value; settings.Opacity = opacityBar.Value;
        colorSwatch.BackColor = ParseColor(settings.ColorHex);
        overlay.UpdateSettings(settings);
        if (settings.Enabled) overlay.ShowOverlay(); else overlay.HideOverlay();
        statusLabel.Text = settings.Enabled ? "●  MIRA ATIVADA" : "○  MIRA DESATIVADA";
        statusLabel.ForeColor = settings.Enabled ? Color.FromArgb(0, 245, 138) : Color.Silver;
        toggleButton.Text = settings.Enabled ? "OCULTAR MIRA  •  F8" : "MOSTRAR MIRA  •  F8";
        toggleButton.BackColor = settings.Enabled ? Color.FromArgb(0, 164, 92) : Color.FromArgb(48, 91, 150);
        SaveSettings();
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = ParseColor(settings.ColorHex), FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { settings.ColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; ApplySettings(); }
    }

    private void ToggleOverlay() { settings.Enabled = !settings.Enabled; ApplySettings(); }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HotkeyId, ModNoRepeat, Keys.F8))
            statusLabel.Text = "⚠ F8 ocupado por outro aplicativo";
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) ToggleOverlay();
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId); SaveSettings(); overlay.Close(); base.OnFormClosing(e);
    }

    private AppSettings LoadSettings()
    {
        try { if (File.Exists(settingsPath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }) ?? new AppSettings(); }
        catch { }
        return new AppSettings();
    }
    private void SaveSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } })); }
        catch { }
    }
    private static Color ParseColor(string hex) { try { return ColorTranslator.FromHtml(hex); } catch { return Color.Lime; } }
}

internal sealed class OverlayForm : Form
{
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private AppSettings settings;
    private const int WmDpiChanged = 0x02E0;

    public OverlayForm(AppSettings initial)
    {
        settings = initial;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        TopMost = true;
        DoubleBuffered = true;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    public void UpdateSettings(AppSettings value) { settings = value; Invalidate(); }
    public void ShowOverlay()
    {
        if (!Visible) Show();
        Bounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
        TopMost = true; Invalidate();
    }
    public void HideOverlay() { if (Visible) Hide(); }

    protected override bool ShowWithoutActivation => true;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!settings.Enabled) return;
        var x = ClientSize.Width / 2f; var y = ClientSize.Height / 2f;
        var color = ColorTranslator.FromHtml(settings.ColorHex);
        color = Color.FromArgb(Math.Clamp(settings.Opacity * 255 / 100, 1, 255), color);
        using var pen = new Pen(color, settings.Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var brush = new SolidBrush(color);
        float s = settings.Size, g = settings.Gap;
        switch (settings.Shape)
        {
            case CrosshairShape.Dot:
                float r = Math.Max(1.5f, s / 3f); e.Graphics.FillEllipse(brush, x-r, y-r, r*2, r*2); break;
            case CrosshairShape.Circle:
                e.Graphics.DrawEllipse(pen, x-s, y-s, s*2, s*2); break;
            case CrosshairShape.CrossDot:
                DrawCross(e.Graphics, pen, x, y, s, g); float d = Math.Max(1.5f, settings.Thickness); e.Graphics.FillEllipse(brush, x-d, y-d, d*2, d*2); break;
            default:
                DrawCross(e.Graphics, pen, x, y, s, g); break;
        }
    }

    private static void DrawCross(Graphics graphics, Pen pen, float x, float y, float s, float g)
    {
        graphics.DrawLine(pen, x-g-s, y, x-g, y);
        graphics.DrawLine(pen, x+g, y, x+g+s, y);
        graphics.DrawLine(pen, x, y-g-s, x, y-g);
        graphics.DrawLine(pen, x, y+g, x, y+g+s);
    }
}
