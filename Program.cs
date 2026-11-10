using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Collections.Generic;
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
    public string ColorHex { get; set; } = "#B45CFF";
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

    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GutoCrosshair", "settings.json");

    private AppSettings settings = new();
    private readonly OverlayForm overlay;
    private readonly Panel pageHost = new();
    private readonly Label statusLabel = new();
    private readonly Button toggleButton = new();
    private readonly Panel preview = new();
    private readonly ComboBox shapeBox = new();
    private readonly TrackBar sizeBar = MakeTrack(2, 40, 13);
    private readonly TrackBar gapBar = MakeTrack(0, 25, 5);
    private readonly TrackBar thicknessBar = MakeTrack(1, 8, 2);
    private readonly TrackBar opacityBar = MakeTrack(20, 100, 95);
    private readonly Label colorSwatch = new();
    private readonly Label sizeValue = new();
    private readonly Label gapValue = new();
    private readonly Label thicknessValue = new();
    private readonly Label opacityValue = new();
    private readonly Button homeNav = new();
    private readonly Button customizeNav = new();
    private readonly Button settingsNav = new();
    private bool loaded;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static readonly Color Bg = Color.FromArgb(13, 15, 23);
    private static readonly Color Sidebar = Color.FromArgb(18, 20, 31);
    private static readonly Color Card = Color.FromArgb(25, 28, 42);
    private static readonly Color Card2 = Color.FromArgb(31, 34, 50);
    private static readonly Color Purple = Color.FromArgb(180, 92, 255);
    private static readonly Color Muted = Color.FromArgb(147, 151, 173);

    public MainForm()
    {
        settings = LoadSettings();
        overlay = new OverlayForm(settings);
        Text = "Guto Crosshair";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(980, 680);
        ClientSize = new Size(1200, 800);
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        BuildShell();
        BuildHomePage();
        BuildCustomizePage();
        BuildSettingsPage();
        ApplySettingsToControls();
        loaded = true;
        ApplySettings();
        ShowPage("home");
        Resize += (_, _) => ApplyResponsiveScale();
    }

    private readonly Dictionary<Control, (Rectangle Bounds, float FontSize)> originalLayout = new();
private int originalWidth;
private int originalHeight;
private bool layoutCaptured;

protected override void OnShown(EventArgs e)
{
    base.OnShown(e);

    originalWidth = pageHost.ClientSize.Width;
    originalHeight = pageHost.ClientSize.Height;

    CaptureLayout(pageHost);

    layoutCaptured = true;
    WindowState = FormWindowState.Maximized;
    ApplyResponsiveScale();
}

private void CaptureLayout(Control parent)
{
    foreach (Control c in parent.Controls)
    {
        originalLayout[c] = (c.Bounds, c.Font.Size);
        CaptureLayout(c);
    }
}

private void ApplyResponsiveScale()
{
    if (!layoutCaptured || originalWidth <= 0 || originalHeight <= 0)
        return;

    float sx = (float)pageHost.ClientSize.Width / originalWidth;
    float sy = (float)pageHost.ClientSize.Height / originalHeight;
    float scale = Math.Max(1f, Math.Min(sx, sy));

    foreach (Control c in originalLayout.Keys)
    {
        var original = originalLayout[c];

        if (c.Dock == DockStyle.None)
        {
            c.Bounds = new Rectangle(
                (int)(original.Bounds.X * scale),
                (int)(original.Bounds.Y * scale),
                (int)(original.Bounds.Width * scale),
                (int)(original.Bounds.Height * scale));
        }

        float fontSize = Math.Max(8f, original.FontSize * scale);

        if (Math.Abs(c.Font.Size - fontSize) > 0.5f)
        {
            c.Font = new Font(c.Font.FontFamily, fontSize, c.Font.Style);
        }
    }
}
    private static TrackBar MakeTrack(int min, int max, int value) => new()
    {
        Minimum = min, Maximum = max, Value = value, TickStyle = TickStyle.None,
        Height = 35, Dock = DockStyle.Fill, BackColor = Card
    };

    private void BuildShell()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 205, BackColor = Sidebar, Padding = new Padding(15) };
        Controls.Add(sidebar);

        var logo = new Label
        {
            Text = "GUTO\nCROSSHAIR", AutoSize = false, Location = new Point(18, 22),
            Size = new Size(165, 65), ForeColor = Purple,
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        };
        sidebar.Controls.Add(logo);
        var sub = new Label
        {
            Text = "PRECISION OVERLAY", AutoSize = true, Location = new Point(20, 91),
            ForeColor = Muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        sidebar.Controls.Add(sub);

        MakeNav(sidebar, homeNav, "⌂   INÍCIO", 150, "home");
        MakeNav(sidebar, customizeNav, "✣   PERSONALIZAR", 202, "customize");
        MakeNav(sidebar, settingsNav, "⚙   CONFIGURAÇÕES", 254, "settings");

        var bottom = new Label
        {
            Text = "GUTO CROSSHAIR\nVersão 2.0  •  Windows",
            AutoSize = false, Size = new Size(170, 45), Location = new Point(18, 600),
            ForeColor = Muted, Font = new Font("Segoe UI", 8)
        };
        sidebar.Controls.Add(bottom);

        var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Bg, Padding = new Padding(28, 18, 25, 10) };
        Controls.Add(top);
        var title = new Label { Text = "PAINEL DE CONTROLE", AutoSize = true, Location = new Point(28, 17), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White };
        top.Controls.Add(title);
        var subtitle = new Label { Text = "Personalize sua mira do seu jeito.", AutoSize = true, Location = new Point(30, 43), ForeColor = Muted };
        top.Controls.Add(subtitle);

        pageHost.Dock = DockStyle.Fill;
        pageHost.BackColor = Bg;
        pageHost.Padding = new Padding(25, 5, 25, 20);
        Controls.Add(pageHost);
        pageHost.BringToFront();
        top.BringToFront();
        sidebar.BringToFront();
    }

    private void MakeNav(Control parent, Button button, string text, int y, string page)
    {
        button.Text = text;
        button.Location = new Point(8, y);
        button.Size = new Size(175, 42);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(12, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Sidebar;
        button.ForeColor = Color.Gainsboro;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Click += (_, _) => ShowPage(page);
        parent.Controls.Add(button);
    }

    private Panel NewPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Visible = false };
        pageHost.Controls.Add(p);
        return p;
    }

    private void BuildHomePage()
    {
        var p = NewPage();
        AddHeading(p, "Bem-vindo ao Guto Crosshair", "Controle sua mira em um só lugar.");

        var statusCard = MakeCard(p, 0, 88, 430, 125);
        AddSmallTitle(statusCard, "STATUS DA MIRA", 20, 17);
        statusLabel.Location = new Point(20, 48);
        statusLabel.AutoSize = true;
        statusLabel.Font = new Font("Segoe UI", 17, FontStyle.Bold);
        statusCard.Controls.Add(statusLabel);

        toggleButton.Text = "OCULTAR MIRA";
        toggleButton.Location = new Point(20, 78);
        toggleButton.Size = new Size(180, 34);
        StyleButton(toggleButton, Purple);
        toggleButton.Click += (_, _) => ToggleOverlay();
        statusCard.Controls.Add(toggleButton);

        var previewCard = MakeCard(p, 450, 88, 275, 300);
        AddSmallTitle(previewCard, "PRÉVIA DA MIRA", 18, 17);
        preview.Location = new Point(18, 55);
        preview.Size = new Size(239, 215);
        preview.BackColor = Color.FromArgb(11, 13, 20);
        preview.Paint += (_, e) => DrawCrosshair(e.Graphics, preview.ClientRectangle, settings);
        previewCard.Controls.Add(preview);

        var info = MakeCard(p, 0, 230, 725, 145);
        AddSmallTitle(info, "ATALHO RÁPIDO", 20, 17);
        var f8 = new Label { Text = "F8", Location = new Point(20, 52), AutoSize = true, ForeColor = Purple, Font = new Font("Segoe UI", 25, FontStyle.Bold) };
        info.Controls.Add(f8);
        var f8desc = new Label { Text = "Mostrar ou ocultar a mira sem precisar abrir o painel.", Location = new Point(85, 63), AutoSize = true, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 10) };
        info.Controls.Add(f8desc);

        var note = new Label { Text = "Dica: para melhor compatibilidade, teste em modo janela sem bordas. A disponibilidade do overlay depende do jogo, do driver e das regras do servidor.", Location = new Point(5, 405), Size = new Size(710, 55), ForeColor = Muted, Font = new Font("Segoe UI", 8) };
        p.Controls.Add(note);
    }

    private void BuildCustomizePage()
    {
        var p = NewPage();
        AddHeading(p, "Personalizar mira", "Ajuste o formato e a aparência em tempo real.");

        var left = MakeCard(p, 0, 88, 430, 460);
        AddSmallTitle(left, "FORMATO E APARÊNCIA", 20, 17);

        AddFieldLabel(left, "FORMATO", 20, 57);
        shapeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        shapeBox.Items.AddRange(new object[] { "Cruz", "Ponto", "Círculo", "Cruz + ponto" });
        shapeBox.Location = new Point(20, 80);
        shapeBox.Size = new Size(385, 34);
        shapeBox.BackColor = Card2;
        shapeBox.ForeColor = Color.White;
        shapeBox.FlatStyle = FlatStyle.Flat;
        left.Controls.Add(shapeBox);
        shapeBox.SelectedIndexChanged += (_, _) => { if (loaded) { settings.Shape = (CrosshairShape)shapeBox.SelectedIndex; ApplySettings(); } };

        AddSlider(left, "TAMANHO", sizeBar, sizeValue, 130, v => settings.Size = v);
        AddSlider(left, "ESPAÇAMENTO", gapBar, gapValue, 202, v => settings.Gap = v);
        AddSlider(left, "ESPESSURA", thicknessBar, thicknessValue, 274, v => settings.Thickness = v);
        AddSlider(left, "OPACIDADE", opacityBar, opacityValue, 346, v => settings.Opacity = v);

        var colorButton = new Button { Text = "ESCOLHER COR", Location = new Point(20, 420), Size = new Size(175, 30) };
        StyleButton(colorButton, Purple);
        colorButton.Click += (_, _) => ChooseColor();
        left.Controls.Add(colorButton);
        colorSwatch.Location = new Point(210, 420);
        colorSwatch.Size = new Size(55, 30);
        colorSwatch.BorderStyle = BorderStyle.FixedSingle;
        left.Controls.Add(colorSwatch);

        var right = MakeCard(p, 450, 88, 275, 300);
        AddSmallTitle(right, "PRÉVIA AO VIVO", 18, 17);
        var bigPreview = new Panel { Location = new Point(18, 55), Size = new Size(239, 215), BackColor = Color.FromArgb(11, 13, 20) };
        bigPreview.Paint += (_, e) => DrawCrosshair(e.Graphics, bigPreview.ClientRectangle, settings);
        right.Controls.Add(bigPreview);
        preview.Tag = bigPreview;

        var saveText = new Label { Text = "As alterações são aplicadas automaticamente e salvas neste computador.", Location = new Point(455, 410), Size = new Size(265, 55), ForeColor = Muted };
        p.Controls.Add(saveText);
    }

    private void BuildSettingsPage()
    {
        var p = NewPage();
        AddHeading(p, "Configurações", "Informações e comportamento do aplicativo.");
        var card = MakeCard(p, 0, 88, 725, 260);
        AddSmallTitle(card, "ATALHOS E INFORMAÇÕES", 20, 17);
        var lines = new[]
        {
            "F8  —  Mostrar ou ocultar a mira",
            "As configurações são salvas automaticamente.",
            "O overlay é externo e não altera arquivos do jogo.",
            "A compatibilidade pode variar conforme o modo de tela, driver e regras do servidor."
        };
        int y = 62;
        foreach (var line in lines)
        {
            var label = new Label { Text = "•  " + line, Location = new Point(22, y), AutoSize = true, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 10) };
            card.Controls.Add(label);
            y += 42;
        }
    }

    private void ShowPage(string page)
    {
        foreach (Control c in pageHost.Controls) c.Visible = false;
        int index = page switch { "customize" => 1, "settings" => 2, _ => 0 };
        if (pageHost.Controls.Count > index) pageHost.Controls[index].Visible = true;
        StyleNav(homeNav, page == "home");
        StyleNav(customizeNav, page == "customize");
        StyleNav(settingsNav, page == "settings");
        pageHost.Invalidate(true);
    }

    private static void StyleNav(Button b, bool selected)
    {
        b.BackColor = selected ? Color.FromArgb(52, 35, 75) : Sidebar;
        b.ForeColor = selected ? Purple : Color.Gainsboro;
    }

    private void AddHeading(Control parent, string title, string subtitle)
    {
        var t = new Label { Text = title, Location = new Point(0, 4), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 19, FontStyle.Bold) };
        var s = new Label { Text = subtitle, Location = new Point(2, 42), AutoSize = true, ForeColor = Muted };
        parent.Controls.Add(t);
        parent.Controls.Add(s);
    }

    private Panel MakeCard(Control parent, int x, int y, int w, int h)
    {
        var card = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Card };
        parent.Controls.Add(card);
        return card;
    }

    private static void AddSmallTitle(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8, FontStyle.Bold) });
    }

    private static void AddFieldLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 8, FontStyle.Bold) });
    }

    private void AddSlider(Control parent, string title, TrackBar bar, Label value, int y, Action<int> update)
    {
        AddFieldLabel(parent, title, 20, y);
        value.Text = bar.Value.ToString();
        value.Location = new Point(365, y - 2);
        value.AutoSize = true;
        value.ForeColor = Purple;
        value.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        parent.Controls.Add(value);
        bar.Location = new Point(15, y + 17);
        bar.Size = new Size(390, 35);
        parent.Controls.Add(bar);
        bar.ValueChanged += (_, _) =>
        {
            value.Text = bar.Value.ToString();
            update(bar.Value);
            if (loaded) ApplySettings();
        };
    }

    private static void StyleButton(Button b, Color color)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = color;
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
    }

    private void ApplySettingsToControls()
    {
        shapeBox.SelectedIndex = (int)settings.Shape;
        sizeBar.Value = Math.Clamp(settings.Size, sizeBar.Minimum, sizeBar.Maximum);
        gapBar.Value = Math.Clamp(settings.Gap, gapBar.Minimum, gapBar.Maximum);
        thicknessBar.Value = Math.Clamp(settings.Thickness, thicknessBar.Minimum, thicknessBar.Maximum);
        opacityBar.Value = Math.Clamp(settings.Opacity, opacityBar.Minimum, opacityBar.Maximum);
        colorSwatch.BackColor = ParseColor(settings.ColorHex);
    }

    private void ApplySettings()
    {
        if (!loaded) return;
        settings.Shape = (CrosshairShape)Math.Max(0, shapeBox.SelectedIndex);
        settings.Size = sizeBar.Value;
        settings.Gap = gapBar.Value;
        settings.Thickness = thicknessBar.Value;
        settings.Opacity = opacityBar.Value;
        colorSwatch.BackColor = ParseColor(settings.ColorHex);
        overlay.UpdateSettings(settings);
        if (settings.Enabled) overlay.ShowOverlay(); else overlay.HideOverlay();

        statusLabel.Text = settings.Enabled ? "●  MIRA ATIVADA" : "○  MIRA DESATIVADA";
        statusLabel.ForeColor = settings.Enabled ? Purple : Color.Silver;
        toggleButton.Text = settings.Enabled ? "OCULTAR MIRA" : "MOSTRAR MIRA";
        toggleButton.BackColor = settings.Enabled ? Color.FromArgb(116, 54, 174) : Color.FromArgb(57, 67, 100);
        preview.Invalidate();
        if (preview.Tag is Panel p) p.Invalidate();
        SaveSettings();
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = ParseColor(settings.ColorHex), FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.ColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            ApplySettings();
        }
    }

    private void ToggleOverlay()
    {
        settings.Enabled = !settings.Enabled;
        ApplySettings();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HotkeyId, ModNoRepeat, Keys.F8))
        {
            statusLabel.Text = "⚠ F8 está sendo usado por outro aplicativo";
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) ToggleOverlay();
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId);
        SaveSettings();
        overlay.Close();
        base.OnFormClosing(e);
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath),
                    new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
        }
        catch { }
    }

    private static Color ParseColor(string hex)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return Purple; }
    }

    internal static void DrawCrosshairForOverlay(Graphics g, Size clientSize, AppSettings s) => DrawCrosshair(g, new Rectangle(Point.Empty, clientSize), s);

    private static void DrawCrosshair(Graphics g, Rectangle bounds, AppSettings s)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = bounds.Width / 2f;
        float y = bounds.Height / 2f;
        Color c = ParseColor(s.ColorHex);
        c = Color.FromArgb(Math.Clamp(s.Opacity * 255 / 100, 1, 255), c);
        using var pen = new Pen(c, s.Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var brush = new SolidBrush(c);
        float size = s.Size, gap = s.Gap;
        switch (s.Shape)
        {
            case CrosshairShape.Dot:
                float r = Math.Max(1.5f, size / 3f);
                g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
                break;
            case CrosshairShape.Circle:
                g.DrawEllipse(pen, x - size, y - size, size * 2, size * 2);
                break;
            case CrosshairShape.CrossDot:
                DrawCross(g, pen, x, y, size, gap);
                float d = Math.Max(1.5f, s.Thickness);
                g.FillEllipse(brush, x - d, y - d, d * 2, d * 2);
                break;
            default:
                DrawCross(g, pen, x, y, size, gap);
                break;
        }
    }

    private static void DrawCross(Graphics g, Pen pen, float x, float y, float size, float gap)
    {
        g.DrawLine(pen, x - gap - size, y, x - gap, y);
        g.DrawLine(pen, x + gap, y, x + gap + size, y);
        g.DrawLine(pen, x, y - gap - size, x, y - gap);
        g.DrawLine(pen, x, y + gap, x, y + gap + size);
    }
}

internal sealed class OverlayForm : Form
{
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private AppSettings settings;

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

    protected override bool ShowWithoutActivation => true;
    public void UpdateSettings(AppSettings value) { settings = value; Invalidate(); }

    public void ShowOverlay()
    {
        if (!Visible) Show();
        Bounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
        TopMost = true;
        Invalidate();
    }

    public void HideOverlay() { if (Visible) Hide(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!settings.Enabled) return;
        MainForm.DrawCrosshairForOverlay(e.Graphics, ClientSize, settings);
    }
}
