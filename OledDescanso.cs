// OledDescanso - apaga (tela preta) o monitor OLED quando ele fica sem uso,
// mesmo que voce continue usando o outro monitor.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Reflection;

// Nome que aparece no Gerenciador de Tarefas e em "Aplicativos de inicializacao"
[assembly: AssemblyTitle("OLED Descanso")]
[assembly: AssemblyProduct("OLED Descanso")]
[assembly: AssemblyDescription("Apaga o monitor OLED quando ele fica sem uso")]
[assembly: AssemblyCopyright("Licença MIT")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);

    [STAThread]
    static void Main(string[] args)
    {
        // Coordenadas reais em cada monitor, mesmo com escalas diferentes (ex: 100% e 175%)
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4) /*PER_MONITOR_AWARE_V2*/)) SetProcessDPIAware(); }
        catch { try { SetProcessDPIAware(); } catch { } }

        if (args.Length == 2 && args[0] == "--ico") { Icone.SalvarIco(args[1]); return; }
        if (args.Length == 2 && args[0] == "--logo") { using (var b = Icone.Desenhar(256, false)) b.Save(args[1]); return; }
        if (args.Length == 1 && args[0] == "--desinstalar") { Application.EnableVisualStyles(); Instalador.Desinstalar(); return; }
        // aberto de fora da pasta de programas (ex.: Downloads): oferece instalar
        if (args.Length == 0 && !Instalador.EstaInstalado)
        {
            Application.EnableVisualStyles();
            if (!Instalador.Oferecer(Instalador.TemaEscuroSistema())) return;
        }
        bool novo;
        using (var m = new System.Threading.Mutex(true, "OledDescanso_Unico", out novo))
        {
            if (!novo)
            {
                // ja esta rodando: abre a janela de configuracoes da copia aberta
                AllowSetForegroundWindow(-1);
                PostMessage(new IntPtr(0xFFFF), RegisterWindowMessage("OledDescanso_Abrir"), IntPtr.Zero, IntPtr.Zero);
                return;
            }
            // prioridade baixa: jogos e outros programas sempre tem preferencia
            try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
                    System.Diagnostics.ProcessPriorityClass.BelowNormal; } catch { }
            Application.EnableVisualStyles();
            Application.Run(new App());
        }
    }
}

// Icone: monitor com uma lua (descanso). Pausado = lua cinza.
static class Icone
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public static Bitmap Desenhar(int n, bool pausado)
    {
        // monitor com tela azul-noite, lua crescente e estrelas (grade 64x64). Pausado: tudo cinza, sem estrelas.
        var bmp = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            float u = n / 64f;
            var moldura = pausado ? Color.FromArgb(130, 134, 145) : Color.FromArgb(150, 160, 190);
            var telaCima = pausado ? Color.FromArgb(48, 50, 58) : Color.FromArgb(40, 48, 90);
            var telaBaixo = pausado ? Color.FromArgb(22, 23, 28) : Color.FromArgb(14, 16, 34);
            var lua = pausado ? Color.FromArgb(150, 154, 165) : Color.FromArgb(255, 200, 80);
            var rt = new RectangleF(4 * u, 8 * u, 56 * u, 38 * u);
            using (var p = Caminho(rt, 7 * u))
            {
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(0, 8 * u), new PointF(0, 46 * u + 1), telaCima, telaBaixo)) g.FillPath(br, p);
                using (var pn = new Pen(moldura, Math.Max(1f, 2.5f * u))) g.DrawPath(pn, p);
            }
            // pe
            using (var br = new SolidBrush(moldura))
            {
                g.FillRectangle(br, 29 * u, 46 * u, 6 * u, 6 * u);
                using (var p = Caminho(new RectangleF(20 * u, 51 * u, 24 * u, 4.5f * u), 2.2f * u)) g.FillPath(br, p);
            }
            // lua crescente (o "recorte" usa a cor da tela)
            using (var br = new SolidBrush(lua)) g.FillEllipse(br, 21 * u, 16 * u, 22 * u, 22 * u);
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(0, 8 * u), new PointF(0, 46 * u + 1), telaCima, telaBaixo)) g.FillEllipse(br, 28 * u, 12 * u, 20 * u, 20 * u);
            if (!pausado && n >= 20)
                using (var br = new SolidBrush(lua)) { g.FillEllipse(br, 44 * u, 16 * u, 3 * u, 3 * u); g.FillEllipse(br, 48 * u, 26 * u, 2 * u, 2 * u); }
        }
        return bmp;
    }

    static System.Drawing.Drawing2D.GraphicsPath Caminho(RectangleF r, float raio)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath(); float d = raio * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }

    static void Arredondado(Graphics g, Brush b, float x, float y, float w, float h, float r)
    {
        using (var p = new System.Drawing.Drawing2D.GraphicsPath())
        {
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    public static Icon Criar(bool pausado)
    {
        int n = SystemInformation.SmallIconSize.Width * 2; // nitido em telas com zoom
        using (var bmp = Desenhar(n, pausado))
        {
            IntPtr h = bmp.GetHicon();
            var ic = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return ic;
        }
    }

    // Gera um .ico com varios tamanhos (PNG dentro), usado como icone do .exe
    public static void SalvarIco(string caminho)
    {
        int[] tams = { 16, 20, 24, 32, 48, 64, 256 };
        var pngs = new System.Collections.Generic.List<byte[]>();
        foreach (var t in tams)
            using (var bmp = Desenhar(t, false))
            using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (var w = new BinaryWriter(File.Create(caminho)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)tams.Length);
            int ofs = 6 + 16 * tams.Length;
            for (int i = 0; i < tams.Length; i++)
            {
                w.Write((byte)(tams[i] >= 256 ? 0 : tams[i])); w.Write((byte)(tams[i] >= 256 ? 0 : tams[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(ofs); ofs += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
    }
}

class Cortina : Form
{
    readonly Timer fade = new Timer { Interval = 40 };
    public event Action Escureceu; // tela 100% preta (apos esmaecer, se ativo)

    // Cursor transparente: em cima da tela preta o mouse fica invisivel
    [DllImport("user32.dll")] static extern IntPtr CreateCursor(IntPtr inst, int x, int y, int w, int h, byte[] and, byte[] xor);
    static Cursor invisivel;
    static Cursor Invisivel()
    {
        if (invisivel == null)
        {
            var and = new byte[32 * 4]; for (int i = 0; i < and.Length; i++) and[i] = 0xFF; // 0xFF = transparente
            var xor = new byte[32 * 4];
            invisivel = new Cursor(CreateCursor(IntPtr.Zero, 0, 0, 32, 32, and, xor));
        }
        return invisivel;
    }

    public Cortina()
    {
        Cursor = Invisivel();
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        // 50 passos x 40 ms = 2 s de esmaecimento
        fade.Tick += (s, e) =>
        {
            Opacity = Math.Min(1.0, Opacity + 0.02);
            if (Opacity >= 1.0) { fade.Stop(); if (Escureceu != null) Escureceu(); }
        };
    }

    public void Aparecer(Rectangle b, bool suave)
    {
        if (Visible) return;
        Bounds = b;
        Opacity = suave ? 0.02 : 1.0;
        Show();
        if (suave) fade.Start();
        else if (Escureceu != null) Escureceu();
    }

    public void Sumir() { fade.Stop(); if (Visible) Hide(); Opacity = 1.0; }

    public bool Completa { get { return Visible && !fade.Enabled; } }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x00000080 /*TOOLWINDOW*/ | 0x00000008 /*TOPMOST*/;
            return cp;
        }
    }
}

// Esconde o ponteiro do mouse trocando os cursores do Windows por um transparente.
// Restaurar = recarregar o esquema de cursores do usuario (o mesmo do Painel de Controle).
static class PonteiroMouse
{
    [DllImport("user32.dll")] static extern IntPtr CreateCursor(IntPtr inst, int x, int y, int w, int h, byte[] and, byte[] xor);
    [DllImport("user32.dll")] static extern bool SetSystemCursor(IntPtr cur, uint id);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    static readonly uint[] ids = { 32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646,
                                   32648, 32649, 32650, 32651, 32671, 32672 };
    public static bool Escondido { get; private set; }

    public static void Esconder()
    {
        if (Escondido) return;
        var and = new byte[32 * 4]; for (int i = 0; i < and.Length; i++) and[i] = 0xFF;
        var xor = new byte[32 * 4];
        foreach (var id in ids) SetSystemCursor(CreateCursor(IntPtr.Zero, 0, 0, 32, 32, and, xor), id); // o Windows assume o cursor
        Escondido = true;
    }

    public static void Mostrar(bool forcar = false)
    {
        if (!Escondido && !forcar) return;
        SystemParametersInfo(0x0057 /*SPI_SETCURSORS*/, 0, IntPtr.Zero, 0);
        Escondido = false;
    }
}

// Visual do menu: claro ou escuro, no estilo do Windows 11
class TemaMenu : ToolStripProfessionalRenderer
{
    public readonly Color Fundo, Borda, Destaque, Texto, Fraco, Linha;

    public TemaMenu(bool escuro) : base(new Cores(escuro))
    {
        RoundedEdges = false;
        Fundo = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
        Borda = escuro ? Color.FromArgb(69, 69, 69) : Color.FromArgb(213, 213, 213);
        Destaque = escuro ? Color.FromArgb(61, 61, 61) : Color.FromArgb(229, 229, 229);
        Texto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        Fraco = escuro ? Color.FromArgb(154, 154, 154) : Color.FromArgb(112, 112, 112);
        Linha = escuro ? Color.FromArgb(71, 71, 71) : Color.FromArgb(220, 220, 220);
    }

    class Cores : ProfessionalColorTable
    {
        readonly Color f, b, d;
        public Cores(bool e)
        {
            UseSystemColors = false;
            f = e ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
            b = e ? Color.FromArgb(69, 69, 69) : Color.FromArgb(213, 213, 213);
            d = e ? Color.FromArgb(61, 61, 61) : Color.FromArgb(229, 229, 229);
        }
        public override Color ToolStripDropDownBackground { get { return f; } }
        public override Color ImageMarginGradientBegin { get { return f; } }
        public override Color ImageMarginGradientMiddle { get { return f; } }
        public override Color ImageMarginGradientEnd { get { return f; } }
        public override Color MenuBorder { get { return b; } }
        public override Color MenuItemBorder { get { return d; } }
        public override Color MenuItemSelected { get { return d; } }
        public override Color MenuItemSelectedGradientBegin { get { return d; } }
        public override Color MenuItemSelectedGradientEnd { get { return d; } }
        public override Color MenuItemPressedGradientBegin { get { return d; } }
        public override Color MenuItemPressedGradientEnd { get { return d; } }
        public override Color CheckBackground { get { return f; } }
        public override Color CheckSelectedBackground { get { return d; } }
        public override Color CheckPressedBackground { get { return d; } }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = (!e.Item.Enabled || (e.Item.Tag as string) == "fraco") ? Fraco : Texto;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Texto;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, e.ImageRectangle, Texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (var p = new Pen(Linha)) e.Graphics.DrawLine(p, 6, y, e.Item.Width - 6, y);
    }
}

// Janelinha para escolher o atalho: so le as teclas enquanto ela esta aberta e com foco
class JanelaAtalho : Form
{
    [DllImport("user32.dll")] static extern short GetKeyState(int k);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int t, out uint x, out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint f);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    public uint Mod, Vk;          // resultado
    uint modAoVivo;               // modificadores segurados agora (ainda sem a tecla final)
    bool valido, livre;

    readonly float esc;
    readonly bool escuro;
    readonly Color cFundo, cCartao, cBorda, cTexto, cFraco, cTecla, cTeclaBorda, cAcento, cTextoAcento, cOk, cAviso, cErro;
    readonly Font fTitulo, fNormal, fPequena, fTecla;
    readonly Painel teclas;
    readonly Label lblStatus;
    readonly Button bSalvar;

    int E(float v) { return (int)Math.Round(v * esc); }

    public JanelaAtalho(uint mod, uint vk, bool temaEscuro)
    {
        Mod = mod; Vk = vk; escuro = temaEscuro;
        esc = 1f;
        try { uint dx, dy; if (GetDpiForMonitor(MonitorFromPoint(Cursor.Position, 2), 0, out dx, out dy) == 0) esc = dx / 96f; } catch { }

        // Paleta (Windows 11)
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cBorda = escuro ? Color.FromArgb(58, 58, 58) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cTecla = escuro ? Color.FromArgb(58, 58, 58) : Color.White;
        cTeclaBorda = escuro ? Color.FromArgb(85, 85, 85) : Color.FromArgb(200, 200, 200);
        cAcento = CorDeDestaque(escuro);
        cTextoAcento = (cAcento.R * 0.299 + cAcento.G * 0.587 + cAcento.B * 0.114) > 150 ? Color.Black : Color.White;
        cOk = escuro ? Color.FromArgb(108, 203, 95) : Color.FromArgb(15, 123, 15);
        cAviso = escuro ? Color.FromArgb(252, 225, 0) : Color.FromArgb(157, 93, 0);
        cErro = escuro ? Color.FromArgb(255, 153, 164) : Color.FromArgb(196, 43, 28);

        fTitulo = new Font("Segoe UI Semibold", 14f * esc);
        fNormal = new Font("Segoe UI", 10f * esc);
        fPequena = new Font("Segoe UI", 9f * esc);
        fTecla = new Font("Segoe UI Semibold", 13f * esc);

        // Janela
        Text = "OLED Descanso";
        Icon = Icone.Criar(false);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = cFundo;
        ForeColor = cTexto;
        Font = fNormal;
        KeyPreview = true;
        ClientSize = new Size(E(460), E(330));

        int x = E(24), w = ClientSize.Width - E(48), y = E(20);

        var lblTitulo = new Label { Text = "Atalho para apagar agora", Font = fTitulo, AutoSize = true, Location = new Point(x, y), ForeColor = cTexto };
        Controls.Add(lblTitulo);
        y += E(36);
        var lblSub = new Label { Text = "Pressione a combinação de teclas que você quer usar.", Font = fNormal, AutoSize = true, Location = new Point(x, y), ForeColor = cFraco };
        Controls.Add(lblSub);
        y += E(34);

        teclas = new Painel(this) { Location = new Point(x, y), Size = new Size(w, E(96)) };
        Controls.Add(teclas);
        y += E(96) + E(12);

        lblStatus = new Label { Font = fNormal, AutoSize = false, Location = new Point(x, y), Size = new Size(w, E(24)), TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(lblStatus);
        y += E(26);
        var lblDica = new Label
        {
            Font = fPequena, AutoSize = false, Location = new Point(x, y), Size = new Size(w, E(40)), ForeColor = cFraco,
            Text = "Use Ctrl, Alt ou Win com outra tecla. Sozinhas: F1–F24, Insert, Pause e Scroll Lock (deixam de funcionar nos outros programas)."
        };
        Controls.Add(lblDica);

        // Rodape com botoes
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = E(64), BackColor = cCartao };
        rodape.Paint += (s, e) => { using (var p = new Pen(cBorda)) e.Graphics.DrawLine(p, 0, 0, rodape.Width, 0); };
        Controls.Add(rodape);

        bSalvar = Botao("Salvar", true);
        var bCancelar = Botao("Cancelar", false);
        var bRemover = Botao("Remover atalho", false);
        bRemover.FlatAppearance.BorderSize = 0;
        bRemover.BackColor = cCartao;
        bRemover.ForeColor = cFraco;
        int by = (rodape.Height - E(34)) / 2;
        bSalvar.Location = new Point(rodape.Width - E(24) - bSalvar.Width, by);
        bCancelar.Location = new Point(bSalvar.Left - E(8) - bCancelar.Width, by);
        bRemover.Location = new Point(E(16), by);
        rodape.Controls.AddRange(new Control[] { bSalvar, bCancelar, bRemover });
        bSalvar.Click += (s, e) => { if (valido && livre) DialogResult = DialogResult.OK; };
        bCancelar.Click += (s, e) => DialogResult = DialogResult.Cancel;
        bRemover.Click += (s, e) => { Mod = 0; Vk = 0; DialogResult = DialogResult.OK; };

        Validar();
    }

    static Color CorDeDestaque(bool escuro)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = k == null ? null : k.GetValue("AccentColor"); // formato ABGR
                if (v is int)
                {
                    int c = (int)v;
                    var cor = Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                    if (escuro) // no escuro o Windows usa uma versao mais clara do destaque
                        cor = Color.FromArgb(Math.Min(255, cor.R + (255 - cor.R) / 2), Math.Min(255, cor.G + (255 - cor.G) / 2), Math.Min(255, cor.B + (255 - cor.B) / 2));
                    return cor;
                }
            }
        }
        catch { }
        return escuro ? Color.FromArgb(76, 194, 255) : Color.FromArgb(0, 103, 192);
    }

    Button Botao(string texto, bool principal)
    {
        var b = new Button
        {
            Text = texto, Font = fNormal, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Size = new Size(Math.Max(E(96), TextRenderer.MeasureText(texto, fNormal).Width + E(28)), E(34)),
            BackColor = principal ? cAcento : cTecla, ForeColor = principal ? cTextoAcento : cTexto,
            TabStop = false, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = principal ? cAcento : cTeclaBorda;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = principal ? ControlPaint.Light(cAcento, 0.15f) : (escuro ? Color.FromArgb(68, 68, 68) : Color.FromArgb(240, 240, 240));
        b.FlatAppearance.MouseDownBackColor = principal ? ControlPaint.Dark(cAcento, 0.05f) : (escuro ? Color.FromArgb(52, 52, 52) : Color.FromArgb(230, 230, 230));
        return b;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref round, 4); } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var a = Screen.FromPoint(Cursor.Position).WorkingArea; // abre no monitor onde esta o mouse
        Location = new Point(a.X + (a.Width - Width) / 2, a.Y + (a.Height - Height) / 2);
        Activate();
    }

    // Combinacao valida e livre? (testa registrando e liberando na hora)
    void Validar()
    {
        valido = Vk != 0;
        livre = false;
        if (valido)
        {
            livre = RegisterHotKey(Handle, 77, Mod | 0x4000, Vk);
            if (livre) UnregisterHotKey(Handle, 77);
        }
        if (!valido) { lblStatus.ForeColor = cFraco; lblStatus.Text = "Nenhum atalho definido"; }
        else if (livre) { lblStatus.ForeColor = cOk; lblStatus.Text = "✓  Disponível"; }
        else { lblStatus.ForeColor = cAviso; lblStatus.Text = "⚠  Já usado pelo Windows ou por outro programa"; }
        bSalvar.Enabled = valido && livre;
        bSalvar.BackColor = bSalvar.Enabled ? cAcento : (escuro ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 220, 220));
        bSalvar.ForeColor = bSalvar.Enabled ? cTextoAcento : cFraco;
        teclas.Invalidate();
    }

    // Lista de teclas a desenhar (ao vivo, se estiver segurando modificadores)
    List<string> TeclasAtuais(out bool parcial)
    {
        parcial = modAoVivo != 0;
        uint mod = parcial ? modAoVivo : Mod;
        var l = new List<string>();
        if (!parcial && Vk == 0) return l;
        if ((mod & 2) != 0) l.Add("Ctrl");
        if ((mod & 1) != 0) l.Add("Alt");
        if ((mod & 4) != 0) l.Add("Shift");
        if ((mod & 8) != 0) l.Add("Win");
        if (parcial) l.Add("…");
        else
        {
            var t = App.TextoAtalho(0, Vk); // so o nome da tecla
            l.Add(t);
        }
        return l;
    }

    static uint ModsAgora(Keys keyData)
    {
        uint m = 0;
        if ((keyData & Keys.Control) != 0 || (GetKeyState(0x11) & 0x8000) != 0) m |= 2;
        if ((keyData & Keys.Alt) != 0 || (GetKeyState(0x12) & 0x8000) != 0) m |= 1;
        if ((keyData & Keys.Shift) != 0 || (GetKeyState(0x10) & 0x8000) != 0) m |= 4;
        if ((GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0) m |= 8;
        return m;
    }

    // Captura todas as teclas (inclusive Alt+letra) enquanto a janela esta aberta
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var k = keyData & Keys.KeyCode;
        uint m = ModsAgora(keyData);

        if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
        {
            modAoVivo = m; teclas.Invalidate();
            return true;
        }
        if (m == 0 && k == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        if (m == 0 && k == Keys.Enter) { if (valido && livre) DialogResult = DialogResult.OK; return true; }

        bool sozinhaOk = (k >= Keys.F1 && k <= Keys.F24) || k == Keys.Insert || k == Keys.Pause || k == Keys.Scroll;
        if ((m & (1 | 2 | 8)) == 0 && !sozinhaOk)
        {
            modAoVivo = 0;
            lblStatus.ForeColor = cErro;
            lblStatus.Text = "✕  Combine com Ctrl, Alt ou Win";
            teclas.Invalidate();
            return true;
        }
        Mod = m; Vk = (uint)k; modAoVivo = 0;
        Validar();
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // soltou os modificadores sem completar: volta a mostrar o atalho escolhido
        modAoVivo = ModsAgora(Keys.None) & modAoVivo;
        if (modAoVivo == 0) teclas.Invalidate();
        base.OnKeyUp(e);
    }

    // Area que desenha as teclas como "teclas de verdade"
    class Painel : Control
    {
        readonly JanelaAtalho j;
        public Painel(JanelaAtalho dono)
        {
            j = dono;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        static System.Drawing.Drawing2D.GraphicsPath Arredondado(RectangleF r, float raio)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = raio * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(j.cFundo);

            // cartao
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var p = Arredondado(card, j.E(8)))
            {
                using (var b = new SolidBrush(j.cCartao)) g.FillPath(b, p);
                using (var pen = new Pen(j.modAoVivo != 0 ? j.cAcento : j.cBorda, j.modAoVivo != 0 ? 2f : 1f)) g.DrawPath(pen, p);
            }

            bool parcial;
            var lista = j.TeclasAtuais(out parcial);
            if (lista.Count == 0)
            {
                TextRenderer.DrawText(g, "Pressione as teclas…", j.fNormal, ClientRectangle, j.cFraco,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // mede tudo para centralizar
            int padX = j.E(16), alt = j.E(44), gap = j.E(10);
            var larg = new List<int>();
            int plus = TextRenderer.MeasureText("+", j.fTecla).Width;
            int total = 0;
            foreach (var t in lista)
            {
                int lw = Math.Max(alt, TextRenderer.MeasureText(t, j.fTecla, Size.Empty, TextFormatFlags.NoPadding).Width + padX * 2);
                larg.Add(lw); total += lw;
            }
            total += (lista.Count - 1) * (plus + gap * 2);
            float x = (Width - total) / 2f, y = (Height - alt) / 2f;

            for (int i = 0; i < lista.Count; i++)
            {
                if (i > 0)
                {
                    var rp = new Rectangle((int)x, (int)y, plus + gap * 2, alt);
                    TextRenderer.DrawText(g, "+", j.fTecla, rp, j.cFraco, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    x += plus + gap * 2;
                }
                bool reticencias = parcial && i == lista.Count - 1;
                var r = new RectangleF(x, y, larg[i], alt);
                // "sombra" embaixo, como tecla fisica
                using (var ps = Arredondado(new RectangleF(r.X, r.Y + j.E(3), r.Width, r.Height), j.E(7)))
                using (var bs = new SolidBrush(j.cTeclaBorda)) if (!reticencias) g.FillPath(bs, ps);
                using (var pk = Arredondado(r, j.E(7)))
                {
                    using (var bk = new SolidBrush(reticencias ? j.cCartao : j.cTecla)) g.FillPath(bk, pk);
                    using (var pen = new Pen(j.cTeclaBorda) { DashStyle = reticencias ? System.Drawing.Drawing2D.DashStyle.Dash : System.Drawing.Drawing2D.DashStyle.Solid })
                        g.DrawPath(pen, pk);
                }
                TextRenderer.DrawText(g, lista[i], j.fTecla, Rectangle.Round(r), reticencias ? j.cFraco : j.cTexto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += larg[i];
            }
        }
    }
}

class App : ApplicationContext
{
    // Hora da ultima atividade (teclado ou mouse) - sem gancho, so pergunta ao Windows
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);
    uint ultimoInput;

    // Nome real do monitor (ex: XG27ACDMS), a partir do \\.\DISPLAYn
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string dev, uint i, ref DISPLAY_DEVICE dd, uint flags);
    static readonly System.Collections.Generic.Dictionary<string, string> nomes =
        new System.Collections.Generic.Dictionary<string, string>();
    static string NomeMonitor(string gdi)
    {
        string n;
        if (nomes.TryGetValue(gdi, out n)) return n;
        n = null;
        try
        {
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
            if (EnumDisplayDevices(gdi, 0, ref dd, 1 /*GET_DEVICE_INTERFACE_NAME*/))
            {
                // \\?\DISPLAY#AUS27A1#5&abc&0&UID4352#{guid}  ->  DISPLAY\AUS27A1\5&abc&0&UID4352
                string id = dd.DeviceID;
                if (id.StartsWith(@"\\?\")) id = id.Substring(4);
                int g = id.IndexOf("#{"); if (g > 0) id = id.Substring(0, g);
                id = id.Replace('#', '\\').ToUpperInvariant();
                using (var q = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorID"))
                    foreach (System.Management.ManagementObject mo in q.Get())
                    {
                        if (!((string)mo["InstanceName"]).ToUpperInvariant().StartsWith(id)) continue;
                        var sb = new System.Text.StringBuilder();
                        foreach (ushort c in (ushort[])mo["UserFriendlyName"]) if (c != 0) sb.Append((char)c);
                        n = sb.ToString();
                    }
            }
        }
        catch { }
        nomes[gdi] = n;
        return n;
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("powrprof.dll")] static extern uint CallNtPowerInformation(int lvl, IntPtr i, uint il, out uint o, uint ol);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int n);

    // Algum app (navegador/player tocando video) pediu para manter a tela ligada?
    static bool AlguemPediuTelaLigada()
    {
        uint s; CallNtPowerInformation(16 /*SystemExecutionState*/, IntPtr.Zero, 0, out s, 4);
        return (s & 0x2 /*ES_DISPLAY_REQUIRED*/) != 0;
    }

    // Existe uma janela em tela cheia (video, jogo) ocupando o OLED inteiro?
    bool TemTelaCheia(Rectangle mon)
    {
        bool achou = false;
        var cls = new System.Text.StringBuilder(64);
        EnumWindows((h, l) =>
        {
            if (EhCortina(h) || !IsWindowVisible(h) || IsIconic(h)) return true;
            int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            cls.Length = 0; GetClassName(h, cls, 64);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return true;
            RECT r; GetWindowRect(h, out r);
            // tela cheia = exatamente o monitor (janela maximizada passa 8px pra fora, nao conta)
            if (r.L == mon.Left && r.T == mon.Top && r.R == mon.Right && r.B == mon.Bottom) { achou = true; return false; }
            return true;
        }, IntPtr.Zero);
        return achou;
    }

    readonly NotifyIcon icone = new NotifyIcon();
    readonly Icon iconeNormal = Icone.Criar(false);
    readonly Icon iconePausado = Icone.Criar(true);
    readonly Timer timer = new Timer();
    readonly string arqConfig = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OledDescanso.ini");

    // Monitores escolhidos para apagar, pelo nome (ex: XG27ACDMS)
    readonly HashSet<string> escolhidos = new HashSet<string>();
    readonly Dictionary<string, Cortina> cortinas = new Dictionary<string, Cortina>();
    readonly Dictionary<string, DateTime> ultimoUso = new Dictionary<string, DateTime>();
    readonly HashSet<string> forcados = new HashSet<string>(); // "Apagar agora" ignora video ate voltar ao monitor
    int segundos = 60; // tempo padrao
    readonly Dictionary<string, int> tempos = new Dictionary<string, int>(); // tempo proprio de cada monitor
    int TempoDe(string k) { int t; return tempos.TryGetValue(k, out t) ? t : segundos; }
    bool pausado;
    Point ultimoCursor;
    bool respeitarVideo = true;

    // Opcoes extras (desligadas por padrao)
    bool esmaecer;          // escurece em 2 s em vez de cortar
    readonly HashSet<string> ddcMonitores = new HashSet<string>(); // monitores que entram em espera de verdade (DDC/CI)
    int limiteMin = 0;      // limite sem uso: apos X min sem mouse/teclado/controle, apaga mesmo com video (0 = desligado, padrao)
    bool detectarFotos = true; // usa "fotos" pequenas para saber em qual monitor esta o video
    DateTime ultimaPresenca = DateTime.Now;

    // ---- Controle de videogame (XInput): conta como presenca ----
    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_STATE { public uint pacote; public ushort botoes; public byte lt, rt; public short lx, ly, rx, ry; }
    [DllImport("xinput1_4.dll")] static extern int XInputGetState(int i, out XINPUT_STATE s);
    readonly uint[] xPacote = new uint[4];
    readonly bool[] xConectado = new bool[4];
    DateTime xUltimaVarredura = DateTime.MinValue, xUltimaChecagem = DateTime.MinValue;
    bool xinputOk = true;

    // true se algum controle mudou de estado desde a ultima checagem
    bool ControleMexeu()
    {
        if (!xinputOk) return false;
        var agora = DateTime.Now;
        if ((agora - xUltimaChecagem).TotalSeconds < 1) return false;
        xUltimaChecagem = agora;
        bool varrer = (agora - xUltimaVarredura).TotalSeconds >= 10; // portas vazias: so a cada 10 s (e mais lento)
        if (varrer) xUltimaVarredura = agora;
        bool mexeu = false;
        try
        {
            for (int i = 0; i < 4; i++)
            {
                if (!xConectado[i] && !varrer) continue;
                XINPUT_STATE s;
                bool ok = XInputGetState(i, out s) == 0;
                if (ok && xConectado[i] && s.pacote != xPacote[i]) mexeu = true;
                xConectado[i] = ok;
                if (ok) xPacote[i] = s.pacote;
            }
        }
        catch { xinputOk = false; }
        return mexeu;
    }
    bool mostrarContador;   // mostra no menu quanto tempo ficou apagado hoje
    int mouseSegundos;      // esconde o ponteiro parado apos X s (0 = desligado)
    DateTime mouseUltimoMov = DateTime.Now;
    readonly Timer timerMouse = new Timer { Interval = 30 }; // so roda com o ponteiro escondido
    Point mousePosEscondido;
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    void EsconderPonteiro()
    {
        mousePosEscondido = Cursor.Position;
        PonteiroMouse.Esconder();
        timerMouse.Start();
    }

    void MostrarPonteiro()
    {
        timerMouse.Stop();
        PonteiroMouse.Mostrar();
        mouseUltimoMov = DateTime.Now;
    }

    // Com o ponteiro escondido: checa a cada 30 ms se mexeu ou clicou, para mostrar na hora
    void ChecarMouse()
    {
        bool clicou = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0 ||
                      (GetAsyncKeyState(0x04) & 0x8000) != 0;
        if (Cursor.Position != mousePosEscondido || clicou) MostrarPonteiro();
    }

    // Contador de protecao (segundos apagado hoje, por monitor)
    readonly Dictionary<string, double> apagadoHoje = new Dictionary<string, double>();
    string diaContador = DateTime.Today.ToString("yyyy-MM-dd");
    DateTime ultimoTick = DateTime.Now, ultimoSalvo = DateTime.Now;

    // DDC/CI: monitores que mandamos para a espera e quando
    readonly Dictionary<string, DateTime> ddcDesligado = new Dictionary<string, DateTime>();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR { public IntPtr h; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string desc; }
    [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hmon, out uint n);
    [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hmon, uint n, [Out] PHYSICAL_MONITOR[] a);
    [DllImport("dxva2.dll")] static extern bool SetVCPFeature(IntPtr h, byte code, uint val);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitors(uint n, PHYSICAL_MONITOR[] a);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint f);

    // Liga (1) ou poe em espera (4) o monitor fisico, em segundo plano para nao travar
    static void Ddc(Rectangle b, uint valor)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var hmon = MonitorFromPoint(new Point(b.X + b.Width / 2, b.Y + b.Height / 2), 2);
                uint n;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out n) || n == 0) return;
                var pm = new PHYSICAL_MONITOR[n];
                if (!GetPhysicalMonitorsFromHMONITOR(hmon, n, pm)) return;
                foreach (var p in pm) SetVCPFeature(p.h, 0xD6, valor);
                DestroyPhysicalMonitors(n, pm);
            }
            catch { }
        });
    }

    // ---- Em qual monitor o video esta? Compara "fotos" minusculas (64x36) de cada tela ----
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc, int mode);
    const int AW = 64, AH = 36;
    readonly Dictionary<string, byte[]> amostras = new Dictionary<string, byte[]>();
    readonly Dictionary<string, DateTime> ultimaAmostra = new Dictionary<string, DateTime>();
    readonly Dictionary<string, DateTime> ultimaMudanca = new Dictionary<string, DateTime>();

    public static byte[] Amostra(Rectangle b)
    {
        using (var bmp = new Bitmap(AW, AH, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr src = GetDC(IntPtr.Zero), dst = g.GetHdc();
                try
                {
                    SetStretchBltMode(dst, 3 /*COLORONCOLOR, rapido*/);
                    StretchBlt(dst, 0, 0, AW, AH, src, b.X, b.Y, b.Width, b.Height, 0x00CC0020 /*SRCCOPY*/);
                }
                finally { g.ReleaseHdc(dst); ReleaseDC(IntPtr.Zero, src); }
            }
            var d = bmp.LockBits(new Rectangle(0, 0, AW, AH), System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                 System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var arr = new byte[d.Stride * AH];
            Marshal.Copy(d.Scan0, arr, 0, arr.Length);
            bmp.UnlockBits(d);
            return arr;
        }
    }

    public static bool Diferente(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return true;
        int mudou = 0;
        for (int i = 0; i + 2 < a.Length; i += 4)
        {
            int dif = Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]);
            if (dif > 24 && ++mudou >= 2) return true;
        }
        return false;
    }

    // A imagem deste monitor mudou nos ultimos 5 s? (amostra no maximo 1x por segundo)
    bool ImagemMudando(string k, Rectangle b)
    {
        var agora = DateTime.Now;
        DateTime ua;
        if (!ultimaAmostra.TryGetValue(k, out ua) || (agora - ua).TotalMilliseconds >= 1000)
        {
            ultimaAmostra[k] = agora;
            try
            {
                var s = Amostra(b);
                byte[] ant;
                if (amostras.TryGetValue(k, out ant) && Diferente(ant, s)) ultimaMudanca[k] = agora;
                amostras[k] = s;
            }
            catch { }
        }
        DateTime um;
        return ultimaMudanca.TryGetValue(k, out um) && (agora - um).TotalSeconds < 5;
    }

    Screen TelaDe(string k)
    {
        foreach (var t in Screen.AllScreens) if (Chave(t) == k) return t;
        return null;
    }

    void AoEscurecer(string k)
    {
        if (!ddcMonitores.Contains(k)) return;
        var t = TelaDe(k);
        if (t == null) return;
        Ddc(t.Bounds, 4);
        ddcDesligado[k] = DateTime.Now;
    }

    static string Duracao(double seg)
    {
        var ts = TimeSpan.FromSeconds(seg);
        return ts.TotalHours >= 1 ? string.Format("{0}h {1:00}min", (int)ts.TotalHours, ts.Minutes)
                                  : string.Format("{0} min", (int)ts.TotalMinutes);
    }

    static string Chave(Screen s) { return NomeMonitor(s.DeviceName) ?? s.DeviceName; }

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    bool menuAberto;

    bool EhCortina(IntPtr h)
    {
        foreach (var c in cortinas.Values) if (c.IsHandleCreated && c.Handle == h) return true;
        return false;
    }

    public App()
    {
        LerConfig();
        icone.Icon = iconeNormal;
        icone.Visible = true;
        // clique unico (esq.) abre a janela, mas espera o tempo de clique duplo do Windows:
        // se vier o segundo clique, so pausa/retoma (atalho antigo) e a janela nao abre
        var cliqueUnico = new System.Windows.Forms.Timer { Interval = SystemInformation.DoubleClickTime };
        cliqueUnico.Tick += (s, e) => { cliqueUnico.Stop(); AbrirConfiguracoes(); };
        icone.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) { cliqueUnico.Stop(); cliqueUnico.Start(); } };
        icone.DoubleClick += (s, e) => { cliqueUnico.Stop(); Pausar(pausado ? 0 : -1); };
        MontarMenu();

        timer.Interval = 500;
        timer.Tick += (s, e) => Verificar();
        timer.Start();
        Atualizar();

        atalho = new Atalho(AtalhoPressionado);
        AplicarAtalho(true);

        // outra copia aberta (abre esta janela) e instalador/desinstalador (pede para fechar)
        mensageiro = new Mensageiro(() => AbrirConfiguracoes(), () => Sair());
        // instalado na pasta de programas: aparece em Configuracoes > Aplicativos
        if (Instalador.EstaInstalado) System.Threading.ThreadPool.QueueUserWorkItem(_ => Instalador.GarantirRegistro());

        // Iniciar com o Windows: migra/atualiza em segundo plano (schtasks leva ~100 ms)
        System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { ConferirInicio(); } catch { } });

        // Ponteiro: garante que nunca fique escondido por engano (inicio, saida, erro, logoff)
        PonteiroMouse.Mostrar(true);
        timerMouse.Tick += (s, e) => ChecarMouse();
        Application.ApplicationExit += (s, e) => PonteiroMouse.Mostrar();
        AppDomain.CurrentDomain.UnhandledException += (s, e) => PonteiroMouse.Mostrar();
        Application.ThreadException += (s, e) => PonteiroMouse.Mostrar();
        SystemEvents.SessionEnding += (s, e) => PonteiroMouse.Mostrar();
    }

    void Verificar()
    {
        // fim da pausa com tempo: volta sozinho
        if (pausado && DateTime.Now >= pausadoAte) Pausar(0);
        else if (pausado && pausadoAte != DateTime.MaxValue && DateTime.Now.Second % 30 == 0) Atualizar(false); // atualiza a dica do icone

        var pos = Cursor.Position;
        bool mexeu = pos != ultimoCursor;
        ultimoCursor = pos;

        // Esconder o ponteiro parado (qualquer monitor)
        if (mexeu) mouseUltimoMov = DateTime.Now;
        // so esconde se o ponteiro estiver num monitor marcado em "Monitores para apagar"
        if (mouseSegundos > 0 && !pausado && !menuAberto && !PonteiroMouse.Escondido &&
            (DateTime.Now - mouseUltimoMov).TotalSeconds >= mouseSegundos &&
            escolhidos.Contains(Chave(Screen.FromPoint(pos))))
            EsconderPonteiro();
        if ((mouseSegundos == 0 || pausado) && PonteiroMouse.Escondido) MostrarPonteiro();

        // Houve atividade SEM o mouse se mexer = digitou -> conta para o monitor da janela ativa
        var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        GetLastInputInfo(ref li);
        bool atividade = li.dwTime != ultimoInput;
        ultimoInput = li.dwTime;
        string telaDigitando = null;
        // logo apos o "apagar agora" (ex: soltar Ctrl+Alt+B), ignora o teclado por 1,5 s
        if (atividade && !mexeu && (DateTime.Now - apagouAgoraEm).TotalSeconds > 1.5)
        {
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero && !EhCortina(fg)) telaDigitando = Chave(Screen.FromHandle(fg));
        }

        bool video = respeitarVideo && AlguemPediuTelaLigada();

        // Presenca: mouse, teclado ou controle. Sem presenca por muito tempo = limite de seguranca
        if (atividade || mexeu || ControleMexeu()) ultimaPresenca = DateTime.Now;
        bool semPresenca = limiteMin > 0 && (DateTime.Now - ultimaPresenca).TotalMinutes >= limiteMin;

        // Jogo/app em tela cheia ativo (em qualquer monitor)? entao nao mexe na ordem das janelas
        bool telaCheiaAtiva = false;
        string telaCheiaMonitor = null; // monitor onde esta a janela em tela cheia que voce esta usando
        {
            var fgw = GetForegroundWindow();
            RECT r;
            if (fgw != IntPtr.Zero && !EhCortina(fgw) && GetWindowRect(fgw, out r))
            {
                var sc = Screen.FromHandle(fgw);
                var mb = sc.Bounds;
                telaCheiaAtiva = r.L == mb.Left && r.T == mb.Top && r.R == mb.Right && r.B == mb.Bottom;
                if (telaCheiaAtiva) telaCheiaMonitor = Chave(sc);
            }
        }

        // tempo desde o ultimo ciclo (para o contador), e virada do dia
        var agora = DateTime.Now;
        double dt = Math.Min(2.0, (agora - ultimoTick).TotalSeconds);
        ultimoTick = agora;
        string hoje = DateTime.Today.ToString("yyyy-MM-dd");
        if (hoje != diaContador) { diaContador = hoje; apagadoHoje.Clear(); }

        foreach (var tela in Screen.AllScreens)
        {
            string k = Chave(tela);
            if (!escolhidos.Contains(k) || pausado) { Esconder(k); continue; }

            Cortina c; cortinas.TryGetValue(k, out c);
            bool apagada = c != null && c.Visible;

            // Com a tela apagada, so "acorda" se o mouse se mexer nela
            // ("Apagar agora" apaga ate o monitor com o mouse parado; volta ao mexer)
            // (limite sem uso: o mouse parado em cima tambem deixa de segurar o monitor)
            bool usando = tela.Bounds.Contains(pos) && (mexeu || (!apagada && !forcados.Contains(k) && !semPresenca));
            if (k == telaDigitando) usando = true;
            if (!usando && !forcados.Contains(k) && !semPresenca)
            {
                // tela cheia neste monitor segura sempre. Video so segura o monitor onde a imagem
                // esta mudando (sem amostrar durante jogo em tela cheia, nem com a tela ja apagada)
                // so tira "fotos" nos ultimos 3 s antes de apagar (economiza processamento)
                DateTime uu;
                bool quaseApagando = !ultimoUso.TryGetValue(k, out uu) ||
                                     (agora - uu).TotalSeconds >= TempoDe(k) - 3;
                // Tela cheia so segura o monitor se: e a janela que voce esta usando (jogo),
                // ou se tem video tocando. VS/app parado em tela cheia, sem foco, nao segura mais.
                // Sem "fotos": qualquer video tocando segura todos os monitores (comportamento antigo)
                usando = (telaCheiaMonitor == k) ||
                         (video && TemTelaCheia(tela.Bounds)) ||
                         (video && !detectarFotos && !apagada) ||
                         (video && detectarFotos && !telaCheiaAtiva && !apagada && quaseApagando && ImagemMudando(k, tela.Bounds));
            }
            if (usando || apagada || !video) { amostras.Remove(k); ultimaMudanca.Remove(k); ultimaAmostra.Remove(k); }

            if (usando) { forcados.Remove(k); ultimoUso[k] = DateTime.Now; Esconder(k); continue; }

            DateTime u;
            if (!ultimoUso.TryGetValue(k, out u)) { ultimoUso[k] = u = DateTime.Now; }
            // com o menu do icone aberto, nao apaga nada novo (senao o menu fecha); espera fechar
            if ((DateTime.Now - u).TotalSeconds >= TempoDe(k) && (apagada || !menuAberto))
            {
                if (c == null)
                {
                    c = new Cortina(); cortinas[k] = c;
                    string kk = k; c.Escureceu += () => AoEscurecer(kk);
                }
                if (!c.Visible) c.Aparecer(tela.Bounds, esmaecer);
                else if (c.Bounds != tela.Bounds) c.Bounds = tela.Bounds;
                // reafirma por cima de tudo, SEM ativar (senao fecha o menu do icone)
                if (!menuAberto && !telaCheiaAtiva) SetWindowPos(c.Handle, new IntPtr(-1) /*TOPMOST*/, 0, 0, 0, 0,
                    0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);

                if (c.Completa)
                {
                    double v; apagadoHoje.TryGetValue(k, out v); apagadoHoje[k] = v + dt;
                    // garante a espera: reenvia a cada 2 min, caso o monitor tenha acordado sozinho
                    DateTime d;
                    if (ddcMonitores.Contains(k) && (!ddcDesligado.TryGetValue(k, out d) || (agora - d).TotalSeconds > 120))
                        AoEscurecer(k);
                }
            }
        }

        if ((agora - ultimoSalvo).TotalSeconds >= 60) { ultimoSalvo = agora; SalvarConfig(); }

        // Monitor desconectado: tira a cortina dele
        foreach (var k in new List<string>(cortinas.Keys))
        {
            bool existe = false;
            foreach (var t in Screen.AllScreens) if (Chave(t) == k) existe = true;
            if (!existe) Esconder(k);
        }
    }

    // Atalho = liga/desliga: se algum monitor esta apagado, acorda todos; senao, apaga
    void AtalhoPressionado()
    {
        bool algumApagado = false;
        foreach (var c in cortinas.Values) if (c.Visible) algumApagado = true;
        if (algumApagado) AcordarTodos(); else ApagarAgora();
    }

    void AcordarTodos()
    {
        forcados.Clear();
        foreach (var k in new List<string>(cortinas.Keys)) { Esconder(k); ultimoUso[k] = DateTime.Now; }
        foreach (var t in Screen.AllScreens) ultimoUso[Chave(t)] = DateTime.Now; // recomeca a contagem
        apagouAgoraEm = DateTime.Now; // ignora o teclado do proprio atalho
        if (PonteiroMouse.Escondido) MostrarPonteiro();
        mouse_event(0x0001 /*MOVE*/, 0, 0, 0, IntPtr.Zero); // atualiza o cursor (sai do invisivel da tela preta)
    }

    DateTime apagouAgoraEm = DateTime.MinValue;
    void ApagarAgora()
    {
        pausado = false;
        apagouAgoraEm = DateTime.Now;
        foreach (var k in escolhidos) { forcados.Add(k); ultimoUso[k] = DateTime.MinValue; }
        Atualizar(false);

        // Mouse: vai para um monitor que nao apaga; se todos apagam, fica invisivel na tela preta
        Screen livre = null;
        foreach (var t in Screen.AllScreens)
            if (!escolhidos.Contains(Chave(t)) && (livre == null || t.Primary)) livre = t;
        if (livre != null)
        {
            var b = livre.Bounds;
            Cursor.Position = new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
        }

        Verificar(); // apaga ja, sem esperar o proximo ciclo

        if (livre == null)
            mouse_event(0x0001 /*MOVE*/, 0, 0, 0, IntPtr.Zero); // movimento "zero": o Windows troca o cursor pelo invisivel
    }
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr x);

    // Atalho global (RegisterHotKey: recurso oficial do Windows, nao le o teclado)
    // mod: 1=Alt 2=Ctrl 4=Shift 8=Win ; vk = tecla. vk 0 = sem atalho.
    class Atalho : NativeWindow
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        readonly Action acao;
        bool registrado;
        public Atalho(Action a) { acao = a; CreateHandle(new CreateParams()); }
        public bool Registrar(uint mod, uint vk)
        {
            Remover();
            if (vk == 0) return true;
            registrado = RegisterHotKey(Handle, 1, mod | 0x4000 /*NOREPEAT*/, vk);
            return registrado;
        }
        public void Remover() { if (registrado) { UnregisterHotKey(Handle, 1); registrado = false; } }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 /*WM_HOTKEY*/) acao();
            base.WndProc(ref m);
        }
    }
    Atalho atalho;
    uint atalhoMod = 0x0001 | 0x0002, atalhoVk = (uint)Keys.B; // padrao Ctrl+Alt+B

    public static string TextoAtalho(uint mod, uint vk)
    {
        if (vk == 0) return "nenhum";
        var p = new List<string>();
        if ((mod & 2) != 0) p.Add("Ctrl");
        if ((mod & 1) != 0) p.Add("Alt");
        if ((mod & 4) != 0) p.Add("Shift");
        if ((mod & 8) != 0) p.Add("Win");
        var k = (Keys)vk;
        string nome = k >= Keys.D0 && k <= Keys.D9 ? ((int)k - (int)Keys.D0).ToString()
                    : k >= Keys.NumPad0 && k <= Keys.NumPad9 ? "Num " + ((int)k - (int)Keys.NumPad0)
                    : k == Keys.Scroll ? "Scroll Lock"
                    : k.ToString();
        p.Add(nome);
        return string.Join("+", p);
    }

    void AplicarAtalho(bool avisar)
    {
        if (!atalho.Registrar(atalhoMod, atalhoVk) && avisar)
            icone.ShowBalloonTip(4000, "OLED Descanso", "O atalho " + TextoAtalho(atalhoMod, atalhoVk) +
                " já é usado por outro programa. Troque em \"Trocar atalho...\".", ToolTipIcon.Warning);
    }

    void TrocarAtalho()
    {
        atalho.Remover(); // nao dispara enquanto escolhe
        using (var f = new JanelaAtalho(atalhoMod, atalhoVk, TemaEscuro()))
        {
            if (f.ShowDialog() == DialogResult.OK)
            {
                uint modAnt = atalhoMod, vkAnt = atalhoVk;
                atalhoMod = f.Mod; atalhoVk = f.Vk;
                if (!atalho.Registrar(atalhoMod, atalhoVk))
                {
                    MessageBox.Show("O atalho " + TextoAtalho(atalhoMod, atalhoVk) +
                        " já é usado por outro programa ou pelo Windows.\nMantive o anterior: " +
                        TextoAtalho(modAnt, vkAnt) + ".", "OLED Descanso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    atalhoMod = modAnt; atalhoVk = vkAnt;
                }
                SalvarConfig();
            }
        }
        AplicarAtalho(false);
    }

    void Esconder(string k)
    {
        Cortina c;
        if (cortinas.TryGetValue(k, out c) && c.Visible) c.Sumir();
        if (ddcDesligado.Remove(k)) { var t = TelaDe(k); if (t != null) Ddc(t.Bounds, 1); } // acorda o monitor
    }
    void EsconderTodas() { foreach (var k in new List<string>(cortinas.Keys)) Esconder(k); }

    void MontarMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };

        // ---- Cabecalho ----
        var titulo = new ToolStripLabel("OLED Descanso") { Tag = "titulo" };
        var mContador = new ToolStripLabel("") { Tag = "fraco" };
        menu.Items.Add(titulo);
        menu.Items.Add(mContador);
        menu.Items.Add(new ToolStripSeparator());

        // ---- Uso rapido ----
        var mAgora = new ToolStripMenuItem("Apagar agora");
        mAgora.Click += (s, e) => ApagarAgora();
        menu.Items.Add(mAgora);

        var mPausar = new ToolStripMenuItem("Pausar");
        var mRetomar = new ToolStripMenuItem("Retomar agora");
        mRetomar.Click += (s, e) => Pausar(0);
        var sepRetomar = new ToolStripSeparator();
        mPausar.DropDownItems.Add(mRetomar);
        mPausar.DropDownItems.Add(sepRetomar);
        foreach (var min in new[] { 15, 30, 60, 120 })
        {
            int v = min;
            var it = new ToolStripMenuItem(v < 60 ? v + " min" : (v / 60) + " h");
            it.Click += (s, e) => Pausar(v);
            mPausar.DropDownItems.Add(it);
        }
        var mSempre = new ToolStripMenuItem("Até eu retomar");
        mSempre.Click += (s, e) => Pausar(-1);
        mPausar.DropDownItems.Add(mSempre);
        menu.Items.Add(mPausar);

        menu.Items.Add(new ToolStripSeparator());

        // ---- Monitores (um submenu por monitor: apagar + tempo) ----
        var mMonitores = new ToolStripMenuItem("Monitores");
        ManterAberto(mMonitores);
        menu.Items.Add(mMonitores);

        // ---- Comportamento ----
        var mComp = new ToolStripMenuItem("Comportamento");
        ManterAberto(mComp);
        var mEsmaecer = new ToolStripMenuItem("Esmaecer suavemente");
        mEsmaecer.Click += (s, e) => { esmaecer = !esmaecer; mEsmaecer.Checked = esmaecer; SalvarConfig(); };
        var mFotos = new ToolStripMenuItem("Detectar monitor do vídeo (fotos)");
        mFotos.Click += (s, e) =>
        {
            detectarFotos = !detectarFotos; mFotos.Checked = detectarFotos;
            amostras.Clear(); ultimaMudanca.Clear(); ultimaAmostra.Clear(); // descarta qualquer foto na hora
            SalvarConfig();
        };
        var mLimite = new ToolStripMenuItem("Limite sem uso");
        ManterAberto(mLimite);
        mLimite.DropDownItems.Add(new ToolStripLabel("Apaga mesmo com vídeo após:") { Tag = "fraco" });
        foreach (var min in new[] { 0, 30, 60, 120, 180 })
        {
            int v = min;
            var it = new ToolStripMenuItem(v == 0 ? "Desligado" : v < 60 ? v + " min" : (v / 60) + " h") { Tag = v };
            it.Click += (s2, e2) =>
            {
                limiteMin = v; SalvarConfig();
                foreach (ToolStripItem x in mLimite.DropDownItems) { var mi = x as ToolStripMenuItem; if (mi != null) mi.Checked = mi == s2; }
                mLimite.Text = "Limite sem uso" + (v == 0 ? "" : " · " + ((ToolStripMenuItem)s2).Text);
            };
            mLimite.DropDownItems.Add(it);
        }
        var mVideo = new ToolStripMenuItem("Apagar com vídeo tocando");
        mVideo.Click += (s, e) => { respeitarVideo = !respeitarVideo; mVideo.Checked = !respeitarVideo; SalvarConfig(); };
        var mMouse = new ToolStripMenuItem("Esconder mouse parado");
        ManterAberto(mMouse);
        foreach (var seg in new[] { 0, 5, 10, 30, 60, 300 })
        {
            int v = seg;
            var it = new ToolStripMenuItem(v == 0 ? "Desligado" : Texto(v)) { Tag = v };
            it.Click += (s2, e2) =>
            {
                mouseSegundos = v; mouseUltimoMov = DateTime.Now; SalvarConfig();
                foreach (ToolStripMenuItem x in mMouse.DropDownItems) x.Checked = x == s2;
                mMouse.Text = "Esconder mouse parado" + (v == 0 ? "" : " · " + Texto(v));
            };
            mMouse.DropDownItems.Add(it);
        }
        mComp.DropDownItems.AddRange(new ToolStripItem[] { mEsmaecer, mVideo, mFotos, mLimite, new ToolStripSeparator(), mMouse });
        menu.Items.Add(mComp);

        // ---- Configuracoes ----
        var mConf = new ToolStripMenuItem("Configurações");
        ManterAberto(mConf);
        var mAtalho = new ToolStripMenuItem("Trocar atalho...");
        mAtalho.Click += (s, e) => { menu.Close(); TrocarAtalho(); };
        var mMostrar = new ToolStripMenuItem("Mostrar contador de proteção");
        mMostrar.Click += (s, e) => { mostrarContador = !mostrarContador; mMostrar.Checked = mostrarContador; SalvarConfig(); };
        var mIniciar = new ToolStripMenuItem("Iniciar com o Windows");
        mIniciar.Click += (s, e) => { AlternarInicio(); mIniciar.Checked = IniciaComWindows(); };
        var mNoIniciar = new ToolStripMenuItem("Atalho no menu Iniciar");
        mNoIniciar.Click += (s, e) => { AlternarAtalhoIniciar(); mNoIniciar.Checked = TemAtalhoIniciar(); };
        var mTema = new ToolStripMenuItem("Tema do menu");
        ManterAberto(mTema);
        foreach (var op in new[] { "auto", "claro", "escuro" })
        {
            string v = op;
            var it = new ToolStripMenuItem(v == "auto" ? "Automático (igual ao Windows)" : v == "claro" ? "Claro" : "Escuro") { Tag = v };
            it.Click += (s2, e2) =>
            {
                tema = v; SalvarConfig();
                foreach (ToolStripMenuItem x in mTema.DropDownItems) x.Checked = x == s2;
                AplicarTema(menu);
            };
            mTema.DropDownItems.Add(it);
        }
        mConf.DropDownItems.AddRange(new ToolStripItem[] { mAtalho, mMostrar, mIniciar, mNoIniciar, new ToolStripSeparator(), mTema });
        menu.Items.Add(mConf);

        menu.Items.Add(new ToolStripSeparator());
        var mJanela = new ToolStripMenuItem("Configurações…");
        mJanela.Click += (s, e) => { menu.Close(); AbrirConfiguracoes(); };
        menu.Items.Add(mJanela);
        var mSair = new ToolStripMenuItem("Sair");
        mSair.Click += (s, e) => Sair();
        menu.Items.Add(mSair);

        menu.Closing += (s, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && segurarMenu) e.Cancel = true;
            segurarMenu = false;
        };
        menu.Closed += (s, e) => menuAberto = false;
        menu.Opening += (s, e) =>
        {
            menuAberto = true;

            // Cabecalho e contador
            mContador.Visible = mostrarContador;
            if (mostrarContador)
            {
                var partes = new List<string>();
                foreach (var k in escolhidos)
                {
                    double v; apagadoHoje.TryGetValue(k, out v);
                    partes.Add(k + " " + Duracao(v));
                }
                mContador.Text = "Hoje apagado: " + (partes.Count == 0 ? "-" : string.Join(" · ", partes));
            }

            // Uso rapido
            mAgora.ShortcutKeyDisplayString = atalhoVk == 0 ? "" : TextoAtalho(atalhoMod, atalhoVk);
            mPausar.Checked = pausado;
            mPausar.Text = !pausado ? "Pausar"
                         : pausadoAte == DateTime.MaxValue ? "Pausado (até você retomar)"
                         : "Pausado (volta em " + Restante() + ")";
            mRetomar.Visible = sepRetomar.Visible = pausado;

            // Comportamento e configuracoes
            mEsmaecer.Checked = esmaecer;
            mFotos.Checked = detectarFotos;
            foreach (ToolStripItem x in mLimite.DropDownItems)
            { var mi = x as ToolStripMenuItem; if (mi != null) mi.Checked = (int)mi.Tag == limiteMin; }
            mLimite.Text = "Limite sem uso" + (limiteMin == 0 ? "" : " · " + (limiteMin < 60 ? limiteMin + " min" : (limiteMin / 60) + " h"));
            mVideo.Checked = !respeitarVideo; // marcado = apaga mesmo com video
            foreach (ToolStripMenuItem x in mMouse.DropDownItems) x.Checked = (int)x.Tag == mouseSegundos;
            mMouse.Text = "Esconder mouse parado" + (mouseSegundos == 0 ? "" : " · " + Texto(mouseSegundos));
            mMostrar.Checked = mostrarContador;
            mIniciar.Checked = IniciaComWindows();
            mNoIniciar.Checked = TemAtalhoIniciar();
            foreach (ToolStripMenuItem x in mTema.DropDownItems) x.Checked = (string)x.Tag == tema;

            // Monitores: principal primeiro; cada um com "Apagar este monitor" + tempos
            mMonitores.DropDownItems.Clear();
            var telas = new List<Screen>(Screen.AllScreens);
            telas.Sort((a, b) => b.Primary.CompareTo(a.Primary));
            foreach (var t in telas)
            {
                string k = Chave(t);
                string nome = (NomeMonitor(t.DeviceName) ?? t.DeviceName.Replace(@"\\.\", "")) + (t.Primary ? " (principal)" : "");
                var sub = new ToolStripMenuItem();
                Func<string> rotulo = () => nome + "  ·  " + (escolhidos.Contains(k) ? Texto(TempoDe(k)) : "não apaga");
                sub.Text = rotulo();
                sub.Checked = escolhidos.Contains(k);
                ManterAberto(sub);

                var ativo = new ToolStripMenuItem("Apagar este monitor") { Checked = escolhidos.Contains(k) };
                ativo.Click += (s2, e2) =>
                {
                    if (!escolhidos.Remove(k)) escolhidos.Add(k);
                    ativo.Checked = sub.Checked = escolhidos.Contains(k);
                    sub.Text = rotulo();
                    SalvarConfig(); Atualizar();
                };
                sub.DropDownItems.Add(ativo);

                var ddc = new ToolStripMenuItem("Desligar de verdade (espera)") { Checked = ddcMonitores.Contains(k) };
                ddc.Click += (s2, e2) =>
                {
                    if (!ddcMonitores.Remove(k)) ddcMonitores.Add(k);
                    else if (ddcDesligado.Remove(k)) Ddc(t.Bounds, 1); // desligou a opcao: acorda se estava em espera
                    ddc.Checked = ddcMonitores.Contains(k);
                    SalvarConfig();
                };
                sub.DropDownItems.Add(ddc);
                sub.DropDownItems.Add(new ToolStripSeparator());
                sub.DropDownItems.Add(new ToolStripLabel("Tempo para apagar") { Tag = "fraco" });
                foreach (var seg in new[] { 15, 30, 60, 120, 300, 600 })
                {
                    int v = seg;
                    var it = new ToolStripMenuItem(Texto(v)) { Checked = TempoDe(k) == v, Tag = "tempo" };
                    it.Click += (s2, e2) =>
                    {
                        tempos[k] = v; SalvarConfig(); Atualizar();
                        foreach (ToolStripItem x in sub.DropDownItems)
                            if ((x.Tag as string) == "tempo") ((ToolStripMenuItem)x).Checked = x == s2;
                        sub.Text = rotulo();
                    };
                    sub.DropDownItems.Add(it);
                }
                mMonitores.DropDownItems.Add(sub);
            }

            AplicarTema(menu);
        };
        icone.ContextMenuStrip = menu;
    }

    // =================== para a janela de configuracoes ===================
    // (mesmas opcoes e o mesmo arquivo do menu do icone: mudar num lugar vale no outro)
    Mensageiro mensageiro;
    Configuracoes config;
    public bool MenuLateralAberto = true;
    public bool Animacoes { get { return true; } set { } }
    public string Tema { get { return tema; } set { tema = value; SalvarConfig(); } }
    public void PainelMudou() { }
    public void ConferirPainel() { }

    public void AbrirConfiguracoes()
    {
        if (config != null && !config.IsDisposed) { config.Activate(); return; }
        config = new Configuracoes(this);
        config.FormClosed += (s, e) => { config = null; GC.Collect(); };
        config.Show();
        config.Activate();
    }

    void Sair()
    {
        PonteiroMouse.Mostrar(); EsconderTodas(); SalvarConfig(); icone.Visible = false; Application.Exit();
    }

    public class InfoMonitor { public string Chave, Nome; public bool Principal; public Rectangle Area; }
    public List<InfoMonitor> Monitores()
    {
        var l = new List<InfoMonitor>();
        var telas = new List<Screen>(Screen.AllScreens);
        telas.Sort((a, b) => b.Primary.CompareTo(a.Primary));
        foreach (var t in telas)
            l.Add(new InfoMonitor { Chave = Chave(t), Nome = NomeMonitor(t.DeviceName) ?? t.DeviceName.Replace(@"\\.\", ""), Principal = t.Primary, Area = t.Bounds });
        return l;
    }
    public bool Apaga(string k) { return escolhidos.Contains(k); }
    public void AlternarApagar(string k) { if (!escolhidos.Remove(k)) escolhidos.Add(k); SalvarConfig(); Atualizar(); }
    public int Tempo(string k) { return TempoDe(k); }
    public void DefinirTempo(string k, int v) { tempos[k] = v; SalvarConfig(); Atualizar(); }
    public bool Desliga(string k) { return ddcMonitores.Contains(k); }
    public void AlternarDesligar(InfoMonitor m)
    {
        if (!ddcMonitores.Remove(m.Chave)) ddcMonitores.Add(m.Chave);
        else if (ddcDesligado.Remove(m.Chave)) Ddc(m.Area, 1);    // desligou a opcao: acorda se estava em espera
        SalvarConfig();
    }
    public static string TextoTempo(int seg) { return Texto(seg); }
    public string ApagadoHoje(string k) { double v; apagadoHoje.TryGetValue(k, out v); return Duracao(v); }

    public bool Esmaecer { get { return esmaecer; } set { esmaecer = value; SalvarConfig(); } }
    public bool ApagarComVideo { get { return !respeitarVideo; } set { respeitarVideo = !value; SalvarConfig(); } }
    public bool DetectarFotos
    {
        get { return detectarFotos; }
        set { detectarFotos = value; amostras.Clear(); ultimaMudanca.Clear(); ultimaAmostra.Clear(); SalvarConfig(); }
    }
    public int LimiteMin { get { return limiteMin; } set { limiteMin = value; SalvarConfig(); } }
    public int MouseSegundos { get { return mouseSegundos; } set { mouseSegundos = value; mouseUltimoMov = DateTime.Now; SalvarConfig(); } }
    public bool MostrarContador { get { return mostrarContador; } set { mostrarContador = value; SalvarConfig(); } }
    public string AtalhoTexto { get { return TextoAtalho(atalhoMod, atalhoVk); } }
    public void EscolherAtalho() { TrocarAtalho(); }
    public bool Pausado { get { return pausado; } }
    public string EstadoPausa { get { return !pausado ? "Ativo" : pausadoAte == DateTime.MaxValue ? "Pausado até você retomar" : "Pausado, volta em " + Restante(); } }
    public void PausarPor(int minutos) { Pausar(minutos); }
    public void ApagarJa() { ApagarAgora(); }
    public bool IniciaComWindowsPub() { return IniciaComWindows(); }
    public void AlternarInicioPub() { AlternarInicio(); }
    public bool TemAtalhoIniciarPub() { return TemAtalhoIniciar(); }
    public void AlternarAtalhoIniciarPub() { AlternarAtalhoIniciar(); }

    public static float EscalaEm(Point p)
    {
        try { uint dx, dy; if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out dx, out dy) == 0) return dx / 96f; } catch { }
        return 1f;
    }
    public static Color CorDeDestaque(bool escuro)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = k == null ? null : k.GetValue("AccentColor");
                if (v is int)
                {
                    int c = (int)v;
                    var cor = Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                    if (escuro) cor = Color.FromArgb(Math.Min(255, cor.R + (255 - cor.R) / 2), Math.Min(255, cor.G + (255 - cor.G) / 2), Math.Min(255, cor.B + (255 - cor.B) / 2));
                    return cor;
                }
            }
        }
        catch { }
        return escuro ? Color.FromArgb(76, 194, 255) : Color.FromArgb(0, 103, 192);
    }
    public static Color TextoSobre(Color c) { return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 150 ? Color.Black : Color.White; }

    // ---- Tema do menu: "auto" segue o Windows; "claro"/"escuro" fixos ----
    string tema = "auto";
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int t, out uint x, out uint y);

    public bool TemaEscuro()
    {
        if (tema == "claro") return false;
        if (tema == "escuro") return true;
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("SystemUsesLightTheme"); // tema da barra/menus do Windows
                if (v is int) return (int)v == 0;
            }
        }
        catch { }
        return false;
    }

    static float EscalaNoMouse()
    {
        try
        {
            uint dx, dy;
            if (GetDpiForMonitor(MonitorFromPoint(Cursor.Position, 2), 0, out dx, out dy) == 0) return dx / 96f;
        }
        catch { }
        return 1f;
    }

    public void AplicarTema(ContextMenuStrip menu)
    {
        var r = new TemaMenu(TemaEscuro());
        ToolStripManager.Renderer = r;
        menu.Renderer = r;
        float esc = EscalaNoMouse();
        var fonte = new Font("Segoe UI", 9.5f * esc);
        menu.Font = fonte;
        menu.ImageScalingSize = new Size((int)(16 * esc), (int)(16 * esc));
        Arredondar(menu);
        AplicarFonte(menu.Items, fonte, esc, r);
    }

    void AplicarFonte(ToolStripItemCollection itens, Font fonte, float esc, TemaMenu r)
    {
        foreach (ToolStripItem it in itens)
        {
            it.Font = (it.Tag as string) == "titulo" ? new Font("Segoe UI Semibold", 10.5f * esc) : fonte;
            if (!(it is ToolStripSeparator)) it.Padding = new Padding(0, (int)(2 * esc), 0, (int)(2 * esc));
            var d = it as ToolStripMenuItem;
            if (d != null && d.HasDropDownItems)
            {
                d.DropDown.Renderer = r;
                d.DropDown.Font = fonte;
                d.DropDown.ImageScalingSize = new Size((int)(16 * esc), (int)(16 * esc));
                var dd = d.DropDown as ToolStripDropDownMenu;
                if (dd != null) { dd.ShowImageMargin = false; dd.ShowCheckMargin = true; }
                Arredondar(d.DropDown);
                AplicarFonte(d.DropDownItems, fonte, esc, r);
            }
        }
    }

    // Cantos arredondados como os menus do Windows 11 (sem efeito no Windows 10)
    static void Arredondar(ToolStripDropDown dd)
    {
        if ((dd.Tag as string) == "arredondado") return;
        dd.Tag = "arredondado";
        dd.Opened += (s, e) =>
        {
            int pref = 2; // DWMWCP_ROUND
            try { DwmSetWindowAttribute(dd.Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref pref, 4); } catch { }
        };
    }

    // Clicar num item deste submenu nao fecha o menu (fecha so clicando fora)
    static bool segurarMenu; // clicou num item de submenu "fixo": o menu principal tambem fica aberto
    static void ManterAberto(ToolStripMenuItem m)
    {
        m.DropDown.ItemClicked += (s, e) => { if (!(e.ClickedItem is ToolStripMenuItem) || !((ToolStripMenuItem)e.ClickedItem).HasDropDownItems) segurarMenu = true; };
        m.DropDown.Closing += (s, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
    }

    // minutos: >0 pausa por X min; -1 pausa ate retomar; 0 retoma
    DateTime pausadoAte = DateTime.MaxValue;
    void Pausar(int minutos)
    {
        pausado = minutos != 0;
        pausadoAte = minutos > 0 ? DateTime.Now.AddMinutes(minutos) : DateTime.MaxValue;
        Atualizar();
    }

    string Restante()
    {
        var r = pausadoAte - DateTime.Now;
        if (r.TotalMinutes < 1) return "menos de 1 min";
        return r.TotalHours >= 1 ? string.Format("{0}h {1:00}min", (int)r.TotalHours, r.Minutes)
                                 : string.Format("{0} min", (int)Math.Ceiling(r.TotalMinutes));
    }

    static string Texto(int seg) { return seg < 60 ? seg + " s" : (seg / 60) + " min"; }

    void Atualizar(bool reiniciarTempo = true)
    {
        if (reiniciarTempo) ultimoUso.Clear();
        var partes = new List<string>();
        foreach (var k in escolhidos) partes.Add(k + " " + Texto(TempoDe(k)));
        string txt = pausado ? (pausadoAte == DateTime.MaxValue ? "OLED Descanso - pausado"
                                                                : "OLED Descanso - pausado, volta em " + Restante())
                   : partes.Count == 0 ? "OLED Descanso - nenhum monitor"
                   : "Apaga: " + string.Join(" · ", partes);
        icone.Text = txt.Length > 63 ? txt.Substring(0, 63) : txt;
        icone.Icon = pausado ? iconePausado : iconeNormal;
        if (pausado) EsconderTodas();
    }

    void LerConfig()
    {
        bool achou = false, ddcAntigo = false;
        string diaLido = null;
        var lidos = new Dictionary<string, double>();
        try
        {
            foreach (var l in File.ReadAllLines(arqConfig))
            {
                var p = l.Split(new[] { '=' }, 2);
                if (p.Length != 2) continue;
                if (p[0] == "monitores")
                {
                    achou = true;
                    foreach (var m in p[1].Split(';')) if (m != "") escolhidos.Add(m);
                }
                if (p[0] == "monitor" && !achou) // config antiga: \\.\DISPLAYn
                {
                    foreach (var t in Screen.AllScreens) if (t.DeviceName == p[1]) { escolhidos.Add(Chave(t)); achou = true; }
                }
                if (p[0] == "segundos") int.TryParse(p[1], out segundos);
                if (p[0] == "video") respeitarVideo = p[1] != "0";
                if (p[0] == "esmaecer") esmaecer = p[1] == "1";
                if (p[0] == "desligar") ddcAntigo = p[1] == "1"; // config antiga: valia para todos
                if (p[0] == "ddc") foreach (var m in p[1].Split(';')) if (m != "") ddcMonitores.Add(m);
                if (p[0] == "limite") int.TryParse(p[1], out limiteMin);
                if (p[0] == "fotos") detectarFotos = p[1] != "0";
                if (p[0] == "contador") mostrarContador = p[1] == "1";
                if (p[0] == "contador_dia") diaLido = p[1];
                if (p[0] == "mouse") int.TryParse(p[1], out mouseSegundos);
                if (p[0] == "tema" && (p[1] == "auto" || p[1] == "claro" || p[1] == "escuro")) tema = p[1];
                if (p[0] == "menu_lateral") MenuLateralAberto = p[1] != "0";
                if (p[0] == "atalho")
                {
                    var a = p[1].Split(',');
                    uint m, v;
                    if (a.Length == 2 && uint.TryParse(a[0], out m) && uint.TryParse(a[1], out v)) { atalhoMod = m; atalhoVk = v; }
                }
                if (p[0].StartsWith("tempo_"))
                {
                    int v; if (int.TryParse(p[1], out v)) tempos[p[0].Substring(6)] = v;
                }
                if (p[0].StartsWith("apagado_"))
                {
                    double v;
                    if (double.TryParse(p[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                        lidos[p[0].Substring(8)] = v;
                }
            }
            if (diaLido == diaContador) foreach (var kv in lidos) apagadoHoje[kv.Key] = kv.Value;
        }
        catch { }
        if (!achou) escolhidos.Add(Chave(Screen.PrimaryScreen));
        if (ddcAntigo && ddcMonitores.Count == 0) foreach (var k in escolhidos) ddcMonitores.Add(k);
    }

    public void SalvarConfig()
    {
        try
        {
            var linhas = new List<string> {
                "menu_lateral=" + (MenuLateralAberto ? "1" : "0"),
                "monitores=" + string.Join(";", escolhidos),
                "segundos=" + segundos,
                "video=" + (respeitarVideo ? "1" : "0"),
                "esmaecer=" + (esmaecer ? "1" : "0"),
                "ddc=" + string.Join(";", ddcMonitores),
                "limite=" + limiteMin,
                "fotos=" + (detectarFotos ? "1" : "0"),
                "contador=" + (mostrarContador ? "1" : "0"),
                "contador_dia=" + diaContador,
                "atalho=" + atalhoMod + "," + atalhoVk,
                "mouse=" + mouseSegundos,
                "tema=" + tema };
            foreach (var kv in tempos) linhas.Add("tempo_" + kv.Key + "=" + kv.Value);
            foreach (var kv in apagadoHoje)
                linhas.Add("apagado_" + kv.Key + "=" + kv.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllLines(arqConfig, linhas);
        }
        catch { }
    }

    // ---- Atalho no menu Iniciar (para achar pesquisando "OLED Descanso") ----
    static string CaminhoAtalhoIniciar()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OLED Descanso.lnk");
    }
    static bool TemAtalhoIniciar() { return File.Exists(CaminhoAtalhoIniciar()); }
    static void AlternarAtalhoIniciar()
    {
        string lnk = CaminhoAtalhoIniciar();
        try
        {
            if (File.Exists(lnk)) { File.Delete(lnk); return; }
            var tipo = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(tipo);
            object atalho = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            var ta = atalho.GetType();
            string exe = Application.ExecutablePath;
            ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, atalho, new object[] { exe });
            ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, atalho, new object[] { Path.GetDirectoryName(exe) });
            ta.InvokeMember("Description", BindingFlags.SetProperty, null, atalho, new object[] { "Apaga o monitor OLED quando ele fica sem uso" });
            ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, atalho, new object[] { exe + ",0" });
            ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, atalho, null);
            Marshal.FinalReleaseComObject(atalho);
            Marshal.FinalReleaseComObject(shell);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não consegui criar o atalho no menu Iniciar:\n" + ex.Message, "OLED Descanso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---- Iniciar com o Windows: tarefa "ao fazer logon" no Agendador de Tarefas ----
    // (mais confiavel que a lista "Run" do registro, que gerenciadores de inicializacao
    //  como o do Norton Utilities podem atrasar ou pular)
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run"; // metodo antigo (so para migrar)
    const string NomeTarefa = "OLED Descanso";
    bool? inicioCache;

    static int Schtasks(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", args)
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit(15000);
                return p.ExitCode;
            }
        }
        catch { return -1; }
    }

    static bool TarefaExiste() { return Schtasks("/query /tn \"" + NomeTarefa + "\"") == 0; }

    static bool CriarTarefa()
    {
        string usuario = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        string exe = Application.ExecutablePath;
        Func<string, string> X = System.Security.SecurityElement.Escape;
        string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
            "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
            "  <RegistrationInfo><Description>Abre o OLED Descanso ao entrar no Windows.</Description></RegistrationInfo>\r\n" +
            "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + X(usuario) + "</UserId><Delay>PT5S</Delay></LogonTrigger></Triggers>\r\n" +
            "  <Principals><Principal id=\"Author\"><UserId>" + X(usuario) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>\r\n" +
            "  <Settings>\r\n" +
            "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
            "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
            "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
            "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" + // sem limite (o padrao fecharia apos 3 dias)
            "    <Priority>7</Priority>\r\n" +
            "  </Settings>\r\n" +
            "  <Actions Context=\"Author\"><Exec><Command>" + X(exe) + "</Command><WorkingDirectory>" + X(Path.GetDirectoryName(exe)) + "</WorkingDirectory></Exec></Actions>\r\n" +
            "</Task>\r\n";
        string tmp = Path.Combine(Path.GetTempPath(), "OledDescanso_tarefa.xml");
        try
        {
            File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
            return Schtasks("/create /tn \"" + NomeTarefa + "\" /xml \"" + tmp + "\" /f") == 0;
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    static bool TemEntradaRun()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue("OledDescanso") != null;
    }

    static void RemoverEntradaRun()
    {
        try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue("OledDescanso", false); }
        catch { }
    }

    // Ao abrir: migra do metodo antigo e garante que a tarefa aponta para este .exe (se mudou de pasta)
    void ConferirInicio()
    {
        bool run = TemEntradaRun(), tarefa = TarefaExiste();
        if (run || tarefa)
        {
            if (CriarTarefa()) RemoverEntradaRun(); // recria com o caminho atual; so remove o antigo se deu certo
        }
        inicioCache = run || tarefa;
    }

    bool IniciaComWindows()
    {
        if (inicioCache == null) inicioCache = TarefaExiste() || TemEntradaRun();
        return inicioCache.Value;
    }

    void AlternarInicio()
    {
        if (IniciaComWindows())
        {
            Schtasks("/delete /tn \"" + NomeTarefa + "\" /f");
            RemoverEntradaRun();
        }
        else if (!CriarTarefa())
        {
            MessageBox.Show("Não consegui criar a tarefa de inicialização no Agendador de Tarefas.", "OLED Descanso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        inicioCache = null;
    }
}

// =====================================================================
//  Desenho: icones com a fonte de simbolos do Windows (mesmos do sistema)
// =====================================================================
// "Clicar de novo fecha": se o clique que fechou um menu caiu no mesmo lugar que o abriu,
// nao abre de novo
static class Alterna
{
    static string chave;
    static DateTime quando;
    public static bool PodeAbrir(string k) { return !(k == chave && (DateTime.Now - quando).TotalMilliseconds < 450); }
    public static void Fechou(string k) { chave = k; quando = DateTime.Now; if (abertaChave == k) { aberto = null; abertaChave = null; } }

    // Menu aberto agora: um clique (ou clique duplo) no mesmo lugar fecha em vez de abrir outro
    static ToolStripDropDown aberto;
    static string abertaChave;
    public static void Abriu(string k, ToolStripDropDown m) { aberto = m; abertaChave = k; }
    public static bool FecharSeAberto(string k)
    {
        if (aberto != null && abertaChave == k && aberto.Visible) { aberto.Close(); return true; }
        return false;
    }
}

// Ajudantes de desenho usados pelas janelas
static class Desenho
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static string fonte;
    public static string FonteIcones
    {
        get
        {
            if (fonte == null)
            {
                fonte = "Segoe MDL2 Assets";
                try { using (var f = new Font("Segoe Fluent Icons", 10f)) if (f.Name == "Segoe Fluent Icons") fonte = f.Name; } catch { }
            }
            return fonte;
        }
    }
    public static System.Drawing.Drawing2D.GraphicsPath Arred(RectangleF r, float raio)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static Icon IconeDe(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var ic = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return ic;
    }
    public static Bitmap Logo(int n) { return Icone.Desenhar(n, false); }
}

// Base das janelinhas (atalho, nome): paleta do Windows 11 e escala do monitor
class JanelaBase : Form
{
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int t, out uint x, out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint f);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    protected readonly float esc;
    protected readonly bool escuro;
    protected readonly Color cFundo, cCartao, cBorda, cTexto, cFraco, cTecla, cTeclaBorda, cAcento, cTextoAcento, cOk, cAviso, cErro;
    protected readonly Font fTitulo, fNormal, fPequena, fTecla;

    protected int E(float v) { return (int)Math.Round(v * esc); }

    public JanelaBase(bool temaEscuro)
    {
        escuro = temaEscuro;
        esc = 1f;
        try { uint dx, dy; if (GetDpiForMonitor(MonitorFromPoint(Cursor.Position, 2), 0, out dx, out dy) == 0) esc = dx / 96f; } catch { }
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cBorda = escuro ? Color.FromArgb(58, 58, 58) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cTecla = escuro ? Color.FromArgb(58, 58, 58) : Color.White;
        cTeclaBorda = escuro ? Color.FromArgb(85, 85, 85) : Color.FromArgb(200, 200, 200);
        cAcento = App.CorDeDestaque(escuro);
        cTextoAcento = App.TextoSobre(cAcento);
        cOk = escuro ? Color.FromArgb(108, 203, 95) : Color.FromArgb(15, 123, 15);
        cAviso = escuro ? Color.FromArgb(252, 225, 0) : Color.FromArgb(157, 93, 0);
        cErro = escuro ? Color.FromArgb(255, 153, 164) : Color.FromArgb(196, 43, 28);
        fTitulo = new Font("Segoe UI Semibold", 14f * esc);
        fNormal = new Font("Segoe UI", 10f * esc);
        fPequena = new Font("Segoe UI", 9f * esc);
        fTecla = new Font("Segoe UI Semibold", 13f * esc);

        Text = "Volt";
        using (var b = Desenho.Logo(32)) Icon = Desenho.IconeDe(b);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = cFundo;
        ForeColor = cTexto;
        Font = fNormal;
        KeyPreview = true;
    }

    protected Button Botao(string texto, bool principal)
    {
        var b = new Button
        {
            Text = texto, Font = fNormal, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Size = new Size(Math.Max(E(96), TextRenderer.MeasureText(texto, fNormal).Width + E(28)), E(34)),
            BackColor = principal ? cAcento : cTecla, ForeColor = principal ? cTextoAcento : cTexto,
            TabStop = false, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = principal ? cAcento : cTeclaBorda;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = principal ? ControlPaint.Light(cAcento, 0.15f) : (escuro ? Color.FromArgb(68, 68, 68) : Color.FromArgb(240, 240, 240));
        b.FlatAppearance.MouseDownBackColor = principal ? ControlPaint.Dark(cAcento, 0.05f) : (escuro ? Color.FromArgb(52, 52, 52) : Color.FromArgb(230, 230, 230));
        return b;
    }

    protected Panel Rodape()
    {
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = E(64), BackColor = cCartao };
        rodape.Paint += (s, e) => { using (var p = new Pen(cBorda)) e.Graphics.DrawLine(p, 0, 0, rodape.Width, 0); };
        Controls.Add(rodape);
        return rodape;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref round, 4); } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var a = Screen.FromPoint(Cursor.Position).WorkingArea; // abre no monitor onde esta o mouse
        Location = new Point(a.X + (a.Width - Width) / 2, a.Y + (a.Height - Height) / 2);
        Activate();
    }
}

// =====================================================================
//  Janela de Configuracoes (estilo Configuracoes do Windows 11)
// =====================================================================
class Configuracoes : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    readonly App app;
    float esc = 1f;
    bool escuro;
    Color cFundo, cCartao, cHover, cBorda, cTexto, cFraco, cAcento, cTextoAcento;
    Font fTitulo, fNormal, fPequena, fCab, fGlifo, fGlifoP;
    int pagina, rolagem, alturaConteudo;
    float navAtual = -1f;                       // 0 = barra lateral fechada (so icones), 1 = aberta
    readonly Timer tNav = new Timer { Interval = 15 };
    static readonly string[] Paginas = { "Geral", "Monitores", "Comportamento" };
    static readonly char[] GlifosPag = { '\uE713', '\uE7F4', '\uE945' };
    readonly List<Linha> linhas = new List<Linha>();
    readonly List<Zona> zonas = new List<Zona>();
    Zona sobre;

    class Linha
    {
        public int Tipo;
        public string Titulo, Desc;
        public char Glifo;
        public Func<bool> Ler;
        public Action<bool> Gravar;
        public Func<string> Valor;
        public Action<Rectangle> Clicar;
    }
    class Zona { public Rectangle R; public int Pagina = -1; public Linha L; }
    const int L_CAB = 0, L_CHAVE = 1, L_BOTAO = 2, L_NOTA = 3, L_GRUPO = 4;

    int E(float v) { return (int)Math.Round(v * esc); }

    public void Atualizar() { Montar(); Invalidate(); }

    public void IrPara(int p)
    {
        if (p < 0 || p >= Paginas.Length) return;
        pagina = p; rolagem = 0; Montar(); Invalidate();
    }

    public Configuracoes(App a)
    {
        app = a;
        Text = "Configurações do OLED Descanso";
        using (var b = Desenho.Logo(32)) Icon = Desenho.IconeDe(b);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        var p = Cursor.Position;
        esc = App.EscalaEm(p);
        Preparar();
        var area = Screen.FromPoint(p).WorkingArea;
        int w = Math.Min(area.Width - E(40), E(860)), h = Math.Min(area.Height - E(40), E(620));
        Bounds = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        MinimumSize = new Size(E(640), E(420));
        Montar();
    }

    void Preparar()
    {
        escuro = app.TemaEscuro();
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cHover = escuro ? Color.FromArgb(50, 50, 50) : Color.FromArgb(246, 246, 246);
        cBorda = escuro ? Color.FromArgb(29, 29, 29) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cAcento = App.CorDeDestaque(escuro);
        cTextoAcento = App.TextoSobre(cAcento);
        if (fTitulo != null) foreach (var f in new[] { fTitulo, fNormal, fPequena, fCab, fGlifo, fGlifoP }) f.Dispose();
        fTitulo = new Font("Segoe UI Semibold", E(28), GraphicsUnit.Pixel);
        fNormal = new Font("Segoe UI", E(14), GraphicsUnit.Pixel);
        fPequena = new Font("Segoe UI", E(12), GraphicsUnit.Pixel);
        fCab = new Font("Segoe UI Semibold", E(14), GraphicsUnit.Pixel);
        fGlifo = new Font(Desenho.FonteIcones, E(16), GraphicsUnit.Pixel);
        fGlifoP = new Font(Desenho.FonteIcones, E(12), GraphicsUnit.Pixel);
        BackColor = cFundo;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0 /*WM_DPICHANGED*/)
        {
            esc = (m.WParam.ToInt32() & 0xFFFF) / 96f;
            var r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            Preparar();
            SetWindowPos(Handle, IntPtr.Zero, r.L, r.T, r.R - r.L, r.B - r.T, 0x0014);
            Invalidate();
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (k == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---------------- conteudo de cada pagina ----------------
    void Cab(string t) { linhas.Add(new Linha { Tipo = L_CAB, Titulo = t }); }
    // titulo de grupo que abre/fecha (seta a direita)
    void Grupo(string t, bool aberto, Action alternar)
    {
        linhas.Add(new Linha { Tipo = L_GRUPO, Titulo = t, Ler = () => aberto, Clicar = r => alternar() });
    }
    void Nota(string t) { linhas.Add(new Linha { Tipo = L_NOTA, Titulo = t }); }
    void Chave(string t, string d, Func<bool> ler, Action<bool> gravar)
    {
        linhas.Add(new Linha { Tipo = L_CHAVE, Titulo = t, Desc = d, Ler = ler, Gravar = gravar });
    }
    void Botao(string t, string d, Func<string> valor, Action<Rectangle> clicar, char glifo = '\0')
    {
        linhas.Add(new Linha { Tipo = L_BOTAO, Titulo = t, Desc = d, Valor = valor, Clicar = clicar, Glifo = glifo });
    }

    ContextMenuStrip NovoMenu()
    {
        return new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
    }

    void MostrarMenu(ContextMenuStrip m, Rectangle r)
    {
        string chave = "cfg|" + pagina + "|" + (r.Y + rolagem);
        if (Alterna.FecharSeAberto(chave) || !Alterna.PodeAbrir(chave)) { m.Dispose(); return; } // o mesmo clique que fechou
        app.AplicarTema(m);
        m.Closed += (s, e) => { Alterna.Fechou(chave); Montar(); Invalidate(); };
        Alterna.Abriu(chave, m);
        m.Show(this, new Point(r.Right - E(12), r.Bottom - E(4)), ToolStripDropDownDirection.BelowLeft);
    }

    void Montar()
    {
        linhas.Clear();
        if (pagina == 0)
        {
            Nota("Apaga o seu monitor OLED quando ele fica sem uso, mesmo que você continue usando o outro monitor. Estas são as mesmas opções do menu do ícone.");
            Cab("Agora");
            Botao("Apagar agora", "Apaga na hora os monitores marcados. Para voltar, mexa o mouse neles ou aperte o atalho de novo", () => app.AtalhoTexto == "nenhum" ? "" : app.AtalhoTexto,
                r => app.ApagarJa(), '\uE708');
            Botao("Pausar", "Para de apagar por um tempo. Dois cliques no ícone também pausam e retomam", () => app.EstadoPausa, r =>
            {
                var m = NovoMenu();
                if (app.Pausado) { var ret = new ToolStripMenuItem("Retomar agora"); ret.Click += (s, e) => app.PausarPor(0); m.Items.Add(ret); m.Items.Add(new ToolStripSeparator()); }
                foreach (var min in new[] { 15, 30, 60, 120 })
                {
                    int v = min;
                    var it = new ToolStripMenuItem(v < 60 ? v + " min" : (v / 60) + " h");
                    it.Click += (s, e) => app.PausarPor(v);
                    m.Items.Add(it);
                }
                var sem = new ToolStripMenuItem("Até eu retomar"); sem.Click += (s, e) => app.PausarPor(-1); m.Items.Add(sem);
                MostrarMenu(m, r);
            });

            Cab("Contador de proteção");
            Chave("Mostrar no menu do ícone", "Quanto tempo cada monitor ficou apagado hoje", () => app.MostrarContador, v => app.MostrarContador = v);
            foreach (var mon in app.Monitores())
                if (app.Apaga(mon.Chave)) Botao(mon.Nome, "Apagado hoje", () => app.ApagadoHoje(mon.Chave), r => { }, '\uE7F4');

            Cab("Atalho e sistema");
            Botao("Atalho para apagar agora", "Funciona em qualquer lugar. Aperte de novo para voltar", () => app.AtalhoTexto,
                r => BeginInvoke(new Action(() => { app.EscolherAtalho(); Montar(); Invalidate(); })));
            Chave("Iniciar com o Windows", "Abre o OLED Descanso sozinho quando você entra no Windows", () => app.IniciaComWindowsPub(), v => app.AlternarInicioPub());
            Chave("Atalho no menu Iniciar", "Para achar pesquisando \"OLED Descanso\"", () => app.TemAtalhoIniciarPub(), v => app.AlternarAtalhoIniciarPub());
            Botao("Tema", "Claro, escuro ou igual ao Windows, nos menus e nesta janela", () => app.Tema == "claro" ? "Claro" : app.Tema == "escuro" ? "Escuro" : "Igual ao Windows", r =>
            {
                var m = NovoMenu();
                foreach (var t in new[] { "auto", "claro", "escuro" })
                {
                    string v = t;
                    var it = new ToolStripMenuItem(v == "auto" ? "Igual ao Windows" : v == "claro" ? "Claro" : "Escuro") { Checked = app.Tema == v };
                    it.Click += (s, e) => { app.Tema = v; Preparar(); OnHandleCreated(EventArgs.Empty); };
                    m.Items.Add(it);
                }
                MostrarMenu(m, r);
            });
            Botao("Desinstalar o OLED Descanso", "Remove o programa, o atalho, o início com o Windows e as configurações (pede confirmação)", () => "",
                r => { try { System.Diagnostics.Process.Start(Application.ExecutablePath, "--desinstalar"); } catch { } });
        }
        else if (pagina == 1)
        {
            Nota("Cada monitor tem seu próprio tempo. Para acordar um monitor apagado, mexa o mouse nele ou aperte o atalho.");
            foreach (var mon0 in app.Monitores())
            {
                var mon = mon0;
                Cab(mon.Nome + (mon.Principal ? " (principal)" : ""));
                Chave("Apagar este monitor", "Fica preto quando você não usa este monitor pelo tempo escolhido", () => app.Apaga(mon.Chave), v => app.AlternarApagar(mon.Chave));
                if (!app.Apaga(mon.Chave)) continue;
                Botao("Tempo para apagar", "Sem usar este monitor por este tempo, ele apaga", () => App.TextoTempo(app.Tempo(mon.Chave)), r =>
                {
                    var m = NovoMenu();
                    foreach (var seg in new[] { 15, 30, 60, 120, 300, 600 })
                    {
                        int v = seg;
                        var it = new ToolStripMenuItem(App.TextoTempo(v)) { Checked = app.Tempo(mon.Chave) == v };
                        it.Click += (s, e) => app.DefinirTempo(mon.Chave, v);
                        m.Items.Add(it);
                    }
                    MostrarMenu(m, r);
                });
                Chave("Desligar de verdade (espera)", "Além da tela preta, manda o monitor para o modo de espera (DDC/CI). Nem todo monitor aceita",
                    () => app.Desliga(mon.Chave), v => app.AlternarDesligar(mon));
            }
        }
        else
        {
            Cab("Ao apagar");
            Chave("Esmaecer suavemente", "Escurece aos poucos em vez de cortar de uma vez", () => app.Esmaecer, v => app.Esmaecer = v);
            Botao("Esconder o mouse parado", "Esconde o ponteiro parado em cima de um monitor marcado", () => app.MouseSegundos == 0 ? "Desligado" : App.TextoTempo(app.MouseSegundos), r =>
            {
                var m = NovoMenu();
                foreach (var seg in new[] { 0, 5, 10, 30, 60, 300 })
                {
                    int v = seg;
                    var it = new ToolStripMenuItem(v == 0 ? "Desligado" : App.TextoTempo(v)) { Checked = app.MouseSegundos == v };
                    it.Click += (s, e) => app.MouseSegundos = v;
                    m.Items.Add(it);
                }
                MostrarMenu(m, r);
            });

            Cab("Vídeos e jogos");
            Nota("Por padrão não apaga o monitor onde um vídeo está tocando, nem enquanto você joga em tela cheia (inclusive só com controle).");
            Chave("Apagar mesmo com vídeo tocando", "Ignora os vídeos e apaga pelo tempo normal", () => app.ApagarComVideo, v => app.ApagarComVideo = v);
            Chave("Detectar o monitor do vídeo (fotos)", "Compara imagens minúsculas da tela (64×36, só na memória) para saber em qual monitor o vídeo está. Desligado, qualquer vídeo segura todos os monitores",
                () => app.DetectarFotos, v => app.DetectarFotos = v);
            Botao("Limite sem uso", "Se ninguém mexer no PC por este tempo, apaga mesmo com vídeo (útil se dormir com uma playlist)", () => app.LimiteMin == 0 ? "Desligado" : app.LimiteMin < 60 ? app.LimiteMin + " min" : (app.LimiteMin / 60) + " h", r =>
            {
                var m = NovoMenu();
                foreach (var min in new[] { 0, 30, 60, 120, 180 })
                {
                    int v = min;
                    var it = new ToolStripMenuItem(v == 0 ? "Desligado" : v < 60 ? v + " min" : (v / 60) + " h") { Checked = app.LimiteMin == v };
                    it.Click += (s, e) => app.LimiteMin = v;
                    m.Items.Add(it);
                }
                MostrarMenu(m, r);
            });
        }
    }

    static void Abrir(string cmd) { try { Process.Start(cmd); } catch { } }

    // ---------------- desenho ----------------
    const TextFormatFlags TF = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                               TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    const TextFormatFlags TC = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    void Fundo(Graphics g, Rectangle r, Color c, int raio, Color? borda)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Desenho.Arred(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), raio))
        {
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            if (borda.HasValue) using (var pn = new Pen(borda.Value)) g.DrawPath(pn, p);
        }
        g.SmoothingMode = SmoothingMode.None;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(cFundo);
        zonas.Clear();
        if (navAtual < 0) navAtual = app.MenuLateralAberto ? 1f : 0f;
        int navW = E(56) + (int)Math.Round((E(240) - E(56)) * navAtual);
        bool comNomes = navAtual > 0.5f;

        // Navegacao (esquerda): botao de abrir/fechar + paginas
        int y = E(12);
        var rMenu = new Rectangle(E(8), y, E(40), E(36));
        zonas.Add(new Zona { R = rMenu, Pagina = -2 });
        if (sobre != null && sobre.Pagina == -2) Fundo(g, rMenu, cHover, E(5), null);
        TextRenderer.DrawText(g, "\uE700", fGlifo, rMenu, cTexto, TC);
        if (comNomes)
        {
            using (var logo = Desenho.Logo(E(20))) g.DrawImage(logo, E(58), y + E(8));
            TextRenderer.DrawText(g, "OLED Descanso", fCab, new Rectangle(E(86), y, navW - E(90), E(36)), cTexto, TF);
        }
        y += E(52);
        for (int i = 0; i < Paginas.Length; i++)
        {
            var r = new Rectangle(E(8), y, navW - E(16), E(40));
            zonas.Add(new Zona { R = r, Pagina = i });
            bool sel = i == pagina, hov = sobre != null && sobre.Pagina == i;
            if (sel || hov) Fundo(g, r, sel ? (escuro ? Color.FromArgb(45, 45, 45) : Color.FromArgb(234, 234, 234)) : cHover, E(5), null);
            if (sel) Fundo(g, new Rectangle(r.X, r.Y + E(12), E(3), E(16)), cAcento, E(1), null);
            TextRenderer.DrawText(g, GlifosPag[i].ToString(), fGlifo, new Rectangle(r.X + E(8), r.Y, E(24), r.Height), cTexto, TC);
            if (comNomes && r.Width - E(56) > E(30))
                TextRenderer.DrawText(g, Paginas[i], fNormal, new Rectangle(r.X + E(44), r.Y, r.Width - E(52), r.Height), cTexto, TF);
            y += E(44);
        }

        // Conteudo (direita)
        int x0 = navW + E(16), w = ClientSize.Width - x0 - E(28);
        var reg = g.Clip;
        g.SetClip(new Rectangle(navW, 0, ClientSize.Width - navW, ClientSize.Height));
        y = E(20) - rolagem;
        TextRenderer.DrawText(g, Paginas[pagina], fTitulo, new Rectangle(x0, y, w, E(44)), cTexto, TF);
        y += E(60);
        foreach (var l in linhas)
        {
            if (l.Tipo == L_GRUPO)
            {
                y += E(10);
                var rg = new Rectangle(x0 - E(6), y, w + E(12), E(36));
                zonas.Add(new Zona { R = rg, L = l });
                if (sobre != null && sobre.L == l) Fundo(g, rg, cHover, E(5), null);
                TextRenderer.DrawText(g, l.Titulo, fCab, new Rectangle(x0 + E(2), y, w - E(40), rg.Height), cTexto, TF);
                TextRenderer.DrawText(g, (l.Ler() ? '\uE70E' : '\uE70D').ToString(), fGlifoP,
                    new Rectangle(rg.Right - E(36), y, E(28), rg.Height), cFraco, TC);
                y += E(40);
                continue;
            }
            if (l.Tipo == L_CAB)
            {
                y += E(14);
                TextRenderer.DrawText(g, l.Titulo, fCab, new Rectangle(x0 + E(2), y, w, E(26)), cTexto, TF);
                y += E(32);
                continue;
            }
            if (l.Tipo == L_NOTA)
            {
                var tam = TextRenderer.MeasureText(g, l.Titulo, fNormal, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(x0 + E(2), y, w, tam.Height + E(4)), cFraco,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                y += tam.Height + E(16);
                continue;
            }
            // a explicacao quebra em mais linhas quando precisa (o cartao cresce junto)
            int txPre = l.Glifo != '\0' ? x0 + E(54) : x0 + E(20);
            int descW = Math.Max(E(80), w - (txPre - x0) - E(200));
            int descH = l.Desc == null ? 0 : TextRenderer.MeasureText(g, l.Desc, fPequena, new Size(descW, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
            int h = l.Desc != null ? E(50) + descH : E(54);
            var r = new Rectangle(x0, y, w, h);
            zonas.Add(new Zona { R = r, L = l });
            bool hov = sobre != null && sobre.L == l;
            Fundo(g, r, hov ? cHover : cCartao, E(6), cBorda);
            int tx = x0 + E(20);
            if (l.Glifo != '\0')
            {
                TextRenderer.DrawText(g, l.Glifo.ToString(), fGlifo, new Rectangle(x0 + E(14), y, E(28), h), cTexto, TC);
                tx = x0 + E(54);
            }
            int dirW = E(200);
            if (l.Desc != null)
            {
                TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(tx, y + E(13), w - (tx - x0) - dirW, E(22)), cTexto, TF);
                TextRenderer.DrawText(g, l.Desc, fPequena, new Rectangle(tx, y + E(36), descW, descH + E(2)), cFraco,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
            }
            else TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(tx, y, w - (tx - x0) - dirW, h), cTexto, TF);

            if (l.Tipo == L_CHAVE)
            {
                bool on = l.Ler();
                var rs = new Rectangle(r.Right - E(60), y + (h - E(20)) / 2, E(40), E(20));
                Interruptor(g, rs, on, hov);
                TextRenderer.DrawText(g, on ? "Ligado" : "Desligado", fNormal, new Rectangle(rs.Left - E(110), y, E(98), h), cTexto,
                    TF & ~TextFormatFlags.Left | TextFormatFlags.Right);
            }
            else
            {
                char seta = l.Glifo == '' ? '' : '';
                if (l.Glifo != '') TextRenderer.DrawText(g, seta.ToString(), fGlifoP, new Rectangle(r.Right - E(40), y, E(24), h), cFraco, TC);
                string v = l.Valor();
                if (v.Length > 0)
                    TextRenderer.DrawText(g, v, fNormal, new Rectangle(r.Right - E(44) - dirW, y, dirW, h), cFraco,
                        TF & ~TextFormatFlags.Left | TextFormatFlags.Right);
            }
            y += h + E(4);
        }
        alturaConteudo = y + rolagem + E(24);
        g.Clip = reg;

        if (alturaConteudo > ClientSize.Height)
        {
            float hb = Math.Max(E(30), ClientSize.Height * ClientSize.Height / (float)alturaConteudo);
            float yb = (ClientSize.Height - hb) * rolagem / (float)(alturaConteudo - ClientSize.Height);
            Fundo(g, new Rectangle(ClientSize.Width - E(8), (int)yb + E(4), E(4), (int)hb - E(8)), cFraco, E(2), null);
        }
    }

    void Interruptor(Graphics g, Rectangle r, bool on, bool hov)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
        using (var p = Desenho.Arred(rf, rf.Height / 2))
        {
            if (on) using (var b = new SolidBrush(cAcento)) g.FillPath(b, p);
            else using (var pn = new Pen(cFraco)) g.DrawPath(pn, p);
        }
        float d = r.Height - E(hov ? 8 : 10);
        float cx = on ? r.Right - r.Height / 2f : r.X + r.Height / 2f, cy = r.Y + r.Height / 2f;
        using (var b = new SolidBrush(on ? cTextoAcento : cFraco)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
        g.SmoothingMode = SmoothingMode.None;
    }

    // ---------------- mouse ----------------
    Zona ZonaEm(Point p)
    {
        foreach (var z in zonas) if (z.R.Contains(p)) return z;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var z = ZonaEm(e.Location);
        bool mudou = (z == null) != (sobre == null) || (z != null && (z.L != sobre.L || z.Pagina != sobre.Pagina));
        sobre = z;
        if (mudou) { Cursor = z != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (sobre != null) { sobre = null; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int max = Math.Max(0, alturaConteudo - ClientSize.Height);
        rolagem = Math.Max(0, Math.Min(max, rolagem - e.Delta * E(48) / 120));
        sobre = null;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        rolagem = Math.Max(0, Math.Min(rolagem, Math.Max(0, alturaConteudo - ClientSize.Height)));
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        var z = ZonaEm(e.Location);
        if (z == null) return;
        if (z.Pagina == -2)
        {
            app.MenuLateralAberto = !app.MenuLateralAberto;
            app.SalvarConfig();
            if (!app.Animacoes) { navAtual = app.MenuLateralAberto ? 1f : 0f; Invalidate(); return; }
            if (!tNav.Enabled)
            {
                tNav.Tick -= PassoNav; tNav.Tick += PassoNav;
                tNav.Start();
            }
            return;
        }
        if (z.Pagina >= 0) { pagina = z.Pagina; rolagem = 0; Montar(); Invalidate(); return; }
        var l = z.L;
        if (l.Tipo == L_CHAVE)
        {
            l.Gravar(!l.Ler());
            app.SalvarConfig();
            app.PainelMudou();
            Montar();      // refaz a lista na hora (opcoes que dependem desta aparecem/somem)
            sobre = null;
            Invalidate();
            return;
        }
        if ((l.Tipo == L_BOTAO || l.Tipo == L_GRUPO) && l.Clicar != null)
        {
            l.Clicar(z.R);
            if (!IsDisposed) { Montar(); Invalidate(); }
        }
    }

    void PassoNav(object s, EventArgs e)
    {
        float alvo = app.MenuLateralAberto ? 1f : 0f;
        navAtual += (alvo - navAtual) * 0.3f;
        if (Math.Abs(alvo - navAtual) < 0.02f) { navAtual = alvo; tNav.Stop(); }
        Invalidate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        app.ConferirPainel();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Montar(); // volta de uma janelinha (atalho, nome): mostra os valores novos
        Invalidate();
    }
}

// Janela invisivel: outra copia aberta pede para abrir as configuracoes; o instalador pede para fechar
class Mensageiro : NativeWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    readonly uint msgAbrir = RegisterWindowMessage("OledDescanso_Abrir"), msgSair = RegisterWindowMessage("OledDescanso_Sair");
    readonly Action abrir, sair;
    public Mensageiro(Action abrir, Action sair) { this.abrir = abrir; this.sair = sair; CreateHandle(new CreateParams { Caption = "OledDescanso.Mensageiro" }); }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)msgAbrir) abrir();
        else if (m.Msg == (int)msgSair) sair();
        base.WndProc(ref m);
    }
}

static class Instalador
{
    static Version VersaoAtual { get { var v = Assembly.GetExecutingAssembly().GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); } }
    public static bool TemaEscuroSistema()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            { object v = k == null ? null : k.GetValue("AppsUseLightTheme"); if (v is int) return (int)v == 0; }
        }
        catch { }
        return false;
    }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);

    public static string Pasta { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OledDescanso"); } }
    public static string ExeInstalado { get { return Path.Combine(Pasta, "OledDescanso.exe"); } }
    static string Atalho { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OLED Descanso.lnk"); } }
    const string ChaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OledDescanso";

    public static bool EstaInstalado { get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(ExeInstalado), StringComparison.OrdinalIgnoreCase); } }

    // aberto de fora da pasta de instalacao (ex.: Downloads): oferece instalar. true = pode seguir abrindo este exe
    public static bool Oferecer(bool escuro)
    {
        bool jaTem = File.Exists(ExeInstalado);
        Version vNova = VersaoAtual, vVelha = null;
        if (jaTem) try { var fv = FileVersionInfo.GetVersionInfo(ExeInstalado); vVelha = new Version(fv.FileMajorPart, fv.FileMinorPart, fv.FileBuildPart); } catch { }
        string titulo = !jaTem ? "Instalar o OLED Descanso" : vVelha != null && vVelha < vNova ? "Atualizar o OLED Descanso" : "Reinstalar o OLED Descanso";
        string texto = !jaTem
            ? "Ele fica na sua pasta de programas, abre sozinho com o Windows e aparece no menu Iniciar. Não precisa de administrador."
            : "O OLED Descanso já está instalado" + (vVelha != null ? " (versão " + vVelha.ToString(3) + ")" : "") + ". Instalar esta versão (" + vNova.ToString(3) + ") por cima? Suas configurações continuam.";
        using (var f = new JanelaInstalar(titulo, texto, escuro))
        {
            var r = f.ShowDialog();
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.No) return true;       // so abrir este, sem instalar
        }
        string erro = Instalar();
        if (erro != null) { MessageBox.Show("Não deu para instalar o OLED Descanso:\n" + erro, "OLED Descanso", MessageBoxButtons.OK, MessageBoxIcon.Warning); return true; }
        try { Process.Start(ExeInstalado); } catch { }
        return false;
    }

    static void FecharAberto()
    {
        uint msg = RegisterWindowMessage("OledDescanso_Sair");
        PostMessage(new IntPtr(0xFFFF), msg, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < 50; i++)
        {
            bool aberto = false;
            foreach (var p in Process.GetProcessesByName("OledDescanso")) if (p.Id != Process.GetCurrentProcess().Id) aberto = true;
            if (!aberto) return;
            System.Threading.Thread.Sleep(100);
        }
        foreach (var p in Process.GetProcessesByName("OledDescanso")) if (p.Id != Process.GetCurrentProcess().Id) try { p.Kill(); p.WaitForExit(3000); } catch { }
    }

    public static string Instalar()
    {
        try
        {
            FecharAberto();
            Directory.CreateDirectory(Pasta);
            File.Copy(Application.ExecutablePath, ExeInstalado, true);
            CriarAtalho();
            using (var k = Registry.CurrentUser.CreateSubKey(ChaveDesinstalar))
            {
                k.SetValue("DisplayName", "OLED Descanso");
                k.SetValue("DisplayIcon", ExeInstalado + ",0");
                k.SetValue("DisplayVersion", VersaoAtual.ToString(3));
                k.SetValue("Publisher", "oDethie");
                k.SetValue("InstallLocation", Pasta);
                k.SetValue("UninstallString", "\"" + ExeInstalado + "\" --desinstalar");
                k.SetValue("URLInfoAbout", "https://github.com/oDethie/oled-descanso");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(ExeInstalado).Length / 1024), RegistryValueKind.DWord);
            }
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ja instalado (ou atualizado sozinho): garante o atalho e o registro em Configuracoes > Aplicativos
    public static void GarantirRegistro()
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(ChaveDesinstalar))
            {
                if (k.GetValue("DisplayVersion") as string == VersaoAtual.ToString(3) && k.GetValue("UninstallString") != null) return;
                k.SetValue("DisplayName", "OLED Descanso");
                k.SetValue("DisplayIcon", ExeInstalado + ",0");
                k.SetValue("DisplayVersion", VersaoAtual.ToString(3));
                k.SetValue("Publisher", "oDethie");
                k.SetValue("InstallLocation", Pasta);
                k.SetValue("UninstallString", "\"" + ExeInstalado + "\" --desinstalar");
                k.SetValue("URLInfoAbout", "https://github.com/oDethie/oled-descanso");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(ExeInstalado).Length / 1024), RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    static void CriarAtalho()
    {
        var tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        object at = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Atalho });
        var ta = at.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, at, new object[] { ExeInstalado });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, at, new object[] { Pasta });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, at, new object[] { "Apaga o monitor OLED quando ele fica sem uso" });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, at, new object[] { ExeInstalado + ",0" });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, at, null);
        Marshal.FinalReleaseComObject(at);
        Marshal.FinalReleaseComObject(shell);
    }

    // OledDescanso.exe --desinstalar (tambem pelo Windows: Configuracoes > Aplicativos)
    public static void Desinstalar()
    {
        if (MessageBox.Show("Desinstalar o OLED Descanso?\n\nRemove o programa, o atalho, o início com o Windows e as configurações. Os monitores voltam ao normal.",
            "OLED Descanso", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        FecharAberto();
        try { Process.Start(new ProcessStartInfo("schtasks.exe", "/delete /tn \"OLED Descanso\" /f") { CreateNoWindow = true, UseShellExecute = false }).WaitForExit(10000); } catch { }
        try { File.Delete(Atalho); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(ChaveDesinstalar, false); } catch { }
        string dados = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        try { File.Delete(Path.Combine(dados, "OledDescanso.ini")); } catch { }
        try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) if (k != null) k.DeleteValue("OledDescanso", false); } catch { }
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
                if (k != null)
                    foreach (var n in k.GetSubKeyNames())
                    {
                        string p; using (var s = k.OpenSubKey(n)) p = s == null ? null : s.GetValue("ExecutablePath") as string;
                        if (p != null && p.EndsWith("\\OledDescanso.exe", StringComparison.OrdinalIgnoreCase)) k.DeleteSubKeyTree(n, false);
                    }
        }
        catch { }
        // a pasta do programa e apagada logo depois que este exe fechar
        string pasta = Path.GetDirectoryName(Application.ExecutablePath);
        if (string.Equals(pasta, Pasta, StringComparison.OrdinalIgnoreCase))
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + pasta + "\"")
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch { }
        MessageBox.Show("O OLED Descanso foi desinstalado.", "OLED Descanso", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// janela "Instalar o OLED Descanso" no estilo do Windows 11
class JanelaInstalar : JanelaBase
{
    public JanelaInstalar(string titulo, string texto, bool temaEscuro) : base(temaEscuro)
    {
        Text = "OLED Descanso";
        ClientSize = new Size(E(460), E(220));
        int x = E(24), w = ClientSize.Width - E(48), y = E(22);
        var logo = new PictureBox { Image = Desenho.Logo(E(40)), Size = new Size(E(40), E(40)), Location = new Point(x, y) };
        Controls.Add(logo);
        Controls.Add(new Label { Text = titulo, Font = fTitulo, AutoSize = true, Location = new Point(x + E(52), y + E(4)), ForeColor = cTexto });
        y += E(56);
        Controls.Add(new Label { Text = texto, Font = fNormal, AutoSize = false, Size = new Size(w, E(64)), Location = new Point(x, y), ForeColor = cFraco });
        var rodape = Rodape();
        var bInstalar = Botao(titulo.StartsWith("Instalar") ? "Instalar" : titulo.StartsWith("Atualizar") ? "Atualizar" : "Reinstalar", true);
        var bAbrir = Botao("Só abrir", false);
        int by = (rodape.Height - E(34)) / 2;
        bInstalar.Location = new Point(rodape.Width - E(24) - bInstalar.Width, by);
        bAbrir.Location = new Point(bInstalar.Left - E(8) - bAbrir.Width, by);
        rodape.Controls.AddRange(new Control[] { bInstalar, bAbrir });
        bInstalar.Click += (s, e) => DialogResult = DialogResult.OK;
        bAbrir.Click += (s, e) => DialogResult = DialogResult.No;
        AcceptButton = bInstalar;
    }
}

