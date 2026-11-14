// Renderer for the Predator watch face bitmaps: a hunter's wrist display, bright red dashes on black.
// Everything is drawn procedurally. Two kinds of symbol share one stroke style (short, thin, separated dashes):
// digits laid out like a seven-segment numeral so the time can be read, and invented dash clusters for the running display.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class PredatorRender
{
    const int S = 466;

    // ---- layout shared with tools/gen-xml.ps1
    public static readonly int[] CellX = { 37, 127, 259, 349 };
    public const int CellY = 158, CellW = 80, CellH = 150, Pad = 14;
    public const float StrokeW = 6.5f;

    // ------------------------------------------------------------ float masks
    static float[] Read(Bitmap bmp, int ch)
    {
        int w = bmp.Width, h = bmp.Height;
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[w * h * 4]; Marshal.Copy(d.Scan0, buf, 0, buf.Length); bmp.UnlockBits(d);
        var m = new float[w * h]; for (int i = 0; i < w * h; i++) m[i] = buf[i * 4 + ch] / 255f; return m;
    }
    static float[] MaskOf(int w, int h, Action<Graphics> draw)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        { g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Black); draw(g); g.Flush(); return Read(bmp, 2); }
    }
    static float At(float[] s, int w, int h, int x, int y) { return (x < 0 || y < 0 || x >= w || y >= h) ? 0f : s[y * w + x]; }
    static float[] Blur(float[] src, int w, int h, int r)
    {
        var a = (float[])src.Clone(); if (r < 1) return a;
        var b = new float[w * h]; float inv = 1f / (2 * r + 1);
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < h; y++) { float acc = 0; for (int x = -r; x <= r; x++) acc += At(a, w, h, x, y);
                for (int x = 0; x < w; x++) { b[y * w + x] = acc * inv; acc += At(a, w, h, x + r + 1, y) - At(a, w, h, x - r, y); } }
            for (int x = 0; x < w; x++) { float acc = 0; for (int y = -r; y <= r; y++) acc += At(b, w, h, x, y);
                for (int y = 0; y < h; y++) { a[y * w + x] = acc * inv; acc += At(b, w, h, x, y + r + 1) - At(b, w, h, x, y - r); } }
        }
        return a;
    }
    static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
    static int Clamp(float v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }

    // light -> transparent PNG. layers: blur radius, r, g, b, gain. Red keeps a saturated core (no white-hot centre) and a tight halo.
    static readonly float[][] Red = {
        new float[]{0, 1.00f, 0.16f, 0.14f, 1.00f}, new float[]{2, 1.00f, 0.05f, 0.05f, 0.70f}, new float[]{6, 1.00f, 0.02f, 0.02f, 0.38f}, new float[]{14, 1.00f, 0.01f, 0.01f, 0.16f} };
    static Bitmap Glow(int w, int h, float[] wire, float[][] L, float edgeFade)
    {
        var R = new float[w * h]; var G = new float[w * h]; var B = new float[w * h];
        foreach (var l in L)
        {
            var m = l[0] < 1 ? wire : Blur(wire, w, h, (int)l[0]);
            for (int i = 0; i < w * h; i++) { float v = m[i] * l[4]; R[i] += v * l[1]; G[i] += v * l[2]; B[i] += v * l[3]; }
        }
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); var buf = new byte[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int i = y * w + x; float f = 1;
            if (edgeFade > 0) { float ex = Math.Min(x, w - 1 - x) / edgeFade, ey = Math.Min(y, h - 1 - y) / edgeFade; f = Smooth(Math.Min(1, Math.Min(ex, ey))); }
            float r = Math.Min(1, R[i]) * f, g = Math.Min(1, G[i]) * f, b = Math.Min(1, B[i]) * f, a = Math.Max(r, Math.Max(g, b));
            if (a > 0.004f) { buf[i * 4 + 2] = (byte)(255 * r / a); buf[i * 4 + 1] = (byte)(255 * g / a); buf[i * 4] = (byte)(255 * b / a); buf[i * 4 + 3] = (byte)(255 * a); }
        }
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(buf, 0, d.Scan0, buf.Length); bmp.UnlockBits(d); return bmp;
    }
    static void Pixels(Bitmap bmp, Func<int, int, Color, Color> f)
    {
        int w = bmp.Width, h = bmp.Height;
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var buf = new byte[w * h * 4]; Marshal.Copy(d.Scan0, buf, 0, buf.Length);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y * w + x) * 4; var c = f(x, y, Color.FromArgb(buf[i + 3], buf[i + 2], buf[i + 1], buf[i])); buf[i + 3] = c.A; buf[i + 2] = c.R; buf[i + 1] = c.G; buf[i] = c.B; }
        Marshal.Copy(buf, 0, d.Scan0, buf.Length); bmp.UnlockBits(d);
    }

    // ------------------------------------------------------------ strokes
    // Every mark is a short flat-ended dash. Coordinates are in a cell of CellW x CellH with the centre at (40, 75).
    struct Dash { public float x1, y1, x2, y2; public Dash(float a, float b, float c, float d) { x1 = a; y1 = b; x2 = c; y2 = d; } }
    static void Draw(Graphics g, Pen pen, float ox, float oy, IEnumerable<Dash> ds) { foreach (var d in ds) g.DrawLine(pen, ox + d.x1, oy + d.y1, ox + d.x2, oy + d.y2); }
    static Pen StrokePen(Color c, float w) { var p = new Pen(c, w); p.StartCap = LineCap.Flat; p.EndCap = LineCap.Flat; return p; }

    // ---- digits: a seven-segment numeral whose segments are dashes shorter than the gaps between them, leaning slightly
    static Dash Lean(float x1, float y1, float x2, float y2) { const float k = 0.10f; return new Dash(x1 + (75 - y1) * k, y1, x2 + (75 - y2) * k, y2); }
    //                               A top                     B upper right             C lower right               D bottom                    E lower left                F upper left              G middle
    static readonly Dash[] Seven = { Lean(27, 16, 53, 16), Lean(62, 30, 62, 60), Lean(62, 90, 62, 120), Lean(27, 134, 53, 134), Lean(18, 90, 18, 120), Lean(18, 30, 18, 60), Lean(27, 75, 53, 75) };
    static readonly string[] SevenOn = { "ABCDEF", "BC", "ABGED", "ABGCD", "FGBC", "AFGCD", "AFGECD", "ABC", "ABCDEFG", "ABCDFG" };
    static List<Dash> Digit(int v) { var l = new List<Dash>(); foreach (char c in SevenOn[v]) l.Add(Seven[c - 'A']); return l; }

    // ---- running symbols: clusters of dashes around the centre of the cell. The lattice and the patterns are invented for this face.
    static readonly Dash[][] Mark = {
        new[]{ new Dash(40, 36, 40, 60) },                               // 0  tick above the centre
        new[]{ new Dash(40, 90, 40, 114) },                              // 1  tick below
        new[]{ new Dash(8, 78, 30, 72) },                                // 2  dash to the left, tilted
        new[]{ new Dash(50, 72, 72, 78) },                               // 3  dash to the right, tilted
        new[]{ new Dash(50, 58, 64, 42) },                               // 4  ray up-right
        new[]{ new Dash(30, 58, 16, 42) },                               // 5  ray up-left
        new[]{ new Dash(50, 92, 64, 108) },                              // 6  ray down-right
        new[]{ new Dash(30, 92, 16, 108) },                              // 7  ray down-left
        new[]{ new Dash(54, 20, 68, 31), new Dash(68, 35, 54, 46) },     // 8  chevron, upper right
        new[]{ new Dash(26, 104, 12, 115), new Dash(12, 119, 26, 130) }, // 9  chevron, lower left
        new[]{ new Dash(46, 12, 70, 12) },                               // 10 bar over the top right
        new[]{ new Dash(31, 124, 31, 144), new Dash(43, 124, 43, 144) }, // 11 pair of ticks at the foot
        new[]{ new Dash(40, 6, 40, 26) },                                // 12 tick at the head
        new[]{ new Dash(10, 26, 24, 38) },                               // 13 short ray, upper left corner
    };
    public const int Symbols = 14;
    static readonly int[][] Cluster = {
        new[]{0, 3, 7}, new[]{12, 8, 1, 2}, new[]{5, 6, 11}, new[]{0, 2, 3, 9}, new[]{4, 7, 10}, new[]{12, 5, 3, 1}, new[]{8, 2, 6},
        new[]{0, 1, 13, 3}, new[]{10, 4, 9, 2}, new[]{5, 4, 11, 0}, new[]{13, 3, 7, 12}, new[]{8, 9, 1}, new[]{2, 6, 10, 0}, new[]{12, 13, 6, 11} };
    static List<Dash> Symbol(int n) { var l = new List<Dash>(); foreach (int m in Cluster[n]) l.AddRange(Mark[m]); return l; }

    static Bitmap Lit(List<Dash> ds, float[][] pal)
    {
        int w = CellW + 2 * Pad, h = CellH + 2 * Pad;
        var wire = MaskOf(w, h, g => { using (var pen = StrokePen(Color.White, StrokeW)) Draw(g, pen, Pad, Pad, ds); });
        return Glow(w, h, wire, pal, 9);
    }
    // always-on: the same digits as hairlines, dim, no halo
    static Bitmap Ambient(List<Dash> ds)
    {
        var m = MaskOf(CellW, CellH, g => { using (var pen = StrokePen(Color.White, 2.4f)) Draw(g, pen, 0, 0, ds); });
        var bmp = new Bitmap(CellW, CellH, PixelFormat.Format32bppArgb); var buf = new byte[CellW * CellH * 4];
        for (int i = 0; i < CellW * CellH; i++) { float a = Math.Min(1, m[i] * 1.2f); if (a < 0.03f) continue; buf[i * 4 + 2] = 255; buf[i * 4 + 1] = 26; buf[i * 4] = 26; buf[i * 4 + 3] = (byte)(255 * a); }
        var d = bmp.LockBits(new Rectangle(0, 0, CellW, CellH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(buf, 0, d.Scan0, buf.Length); bmp.UnlockBits(d); return bmp;
    }

    // ------------------------------------------------------------ background: black, with only the red back-light of the display
    public static Bitmap Background()
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        Pixels(bmp, (x, y, c) =>
        {
            float dx = x - 233, dy = y - 233; if (dx * dx + dy * dy > 233 * 233) return Color.Black;
            float ex = dx / 232f, ey = dy / 128f, d = ex * ex + ey * ey; if (d >= 1) return Color.Black;
            float v = (float)Math.Pow(1 - d, 2.2) * 44;                       // peaks at 44/255 red in the middle, true black outside the oval
            return Color.FromArgb(255, Clamp(v), Clamp(v * 0.03f), Clamp(v * 0.03f));
        });
        return bmp;
    }

    // ------------------------------------------------------------ running display
    // One frame per tick of the animation clock (RunFps) for a 5 s loop: symbols that assemble into the time over the first
    // 2 s, the time alone for 1.5 s, then 1.5 s of the time falling apart into symbols again.
    // A frame is the four cells cut from the background with symbols lit, so where it is opaque it covers the time digits
    // underneath. The area is divided into 28 patches, one around each segment of each digit; a patch that is "open" is
    // transparent, which lets that segment of the real time show and takes away the bits of symbol that were there.
    // Each cell keeps its symbol for a random 3 to 6 ticks, i.e. it changes 2 to 4 times a second, independently of the others.
    public static readonly Rectangle RunRect = new Rectangle(CellX[0] - Pad, CellY - Pad, CellX[3] + CellW + Pad - (CellX[0] - Pad), CellH + 2 * Pad);
    public const int RunFps = 12, RunFrames = 60, AsmFrames = 24, HoldFrames = 18;
    static float SegDist(Dash d, float px, float py)
    {
        float vx = d.x2 - d.x1, vy = d.y2 - d.y1, t = ((px - d.x1) * vx + (py - d.y1) * vy) / (vx * vx + vy * vy); t = Math.Max(0, Math.Min(1, t));
        float qx = d.x1 + t * vx - px, qy = d.y1 + t * vy - py; return qx * qx + qy * qy;
    }
    static int[] Shuffled(Random rnd, int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = i; for (int i = n - 1; i > 0; i--) { int j = rnd.Next(i + 1), t = a[i]; a[i] = a[j]; a[j] = t; } return a; }
    static void SaveRun(Bitmap bg, Bitmap[] sym, Func<string, string> P)
    {
        int w = RunRect.Width, h = RunRect.Height; const int N = 28;
        var patch = new int[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float best = float.MaxValue;
            for (int c = 0; c < 4; c++) for (int s = 0; s < 7; s++)
            {
                float d = SegDist(Seven[s], x + RunRect.Left - CellX[c], y + RunRect.Top - CellY);
                if (d < best) { best = d; patch[y * w + x] = c * 7 + s; }
            }
        }
        var rnd = new Random(23); var cur = new[] { -1, -1, -1, -1 }; var left = new int[4];
        int[] a1 = Shuffled(rnd, N), a2 = Shuffled(rnd, N); var opens = new int[N]; var closes = new int[N];
        int fall = AsmFrames + HoldFrames;
        for (int j = 0; j < N; j++) { opens[a1[j]] = 2 + j * (AsmFrames - 4) / (N - 1); closes[a2[j]] = fall + j * (RunFrames - fall - 4) / (N - 1); }
        var open = new float[N]; var alpha = new float[w * h];
        for (int f = 0; f < RunFrames; f++)
        {
            for (int i = 0; i < 4; i++)
            {
                if (left[i]-- > 0) continue;
                int n; do { n = rnd.Next(0, Symbols); } while (Array.IndexOf(cur, n) >= 0);
                cur[i] = n; left[i] = rnd.Next(3, 7) - 1;
            }
            if (f >= AsmFrames && f < fall) continue;                       // the time alone: gen-xml puts a transparent frame here
            for (int k = 0; k < N; k++)                                     // a patch takes two ticks to open or close
                open[k] = f < AsmFrames ? Math.Max(0, Math.Min(1, (f - opens[k] + 1) / 2f)) : 1 - Math.Max(0, Math.Min(1, (f - closes[k] + 1) / 2f));
            for (int i = 0; i < w * h; i++) alpha[i] = 1 - open[patch[i]];
            var soft = Blur(alpha, w, h, 1);
            using (var bmp = bg.Clone(RunRect, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) for (int i = 0; i < 4; i++) g.DrawImage(sym[cur[i]], CellX[i] - Pad - RunRect.Left, CellY - Pad - RunRect.Top, sym[cur[i]].Width, sym[cur[i]].Height);
                Pixels(bmp, (x, y, c) => Color.FromArgb(Clamp(255 * soft[y * w + x]), c.R, c.G, c.B));
                bmp.Save(P("pr_r" + f.ToString("000")), ImageFormat.Png);
            }
        }
    }

    // ------------------------------------------------------------ battery: an icon and ten leaning dashes, one per 10 %
    public const int BatX = 127, BatY = 340, IconW = 44, TickPitch = 16, BatH = 32, TicksW = 10 * TickPitch + 8;
    static List<Dash> BatteryIcon()
    {
        // the outline of a cell drawn the same way as everything else: dashes that stop short of the corners, and a terminal nub
        return new List<Dash> { new Dash(8, 8, 30, 8), new Dash(8, 24, 30, 24), new Dash(4, 11, 4, 21), new Dash(34, 11, 34, 21), new Dash(39, 13, 39, 19) };
    }
    static List<Dash> BatteryTicks()
    {
        var l = new List<Dash>(); for (int i = 0; i < 10; i++) { float x = 6 + i * TickPitch; l.Add(new Dash(x + 4, 6, x, 26)); } return l;
    }
    static Bitmap Strip(List<Dash> ds, int w, float sw)
    {
        var wire = MaskOf(w, BatH, g => { using (var pen = StrokePen(Color.White, sw)) Draw(g, pen, 0, 0, ds); });
        return Glow(w, BatH, wire, Red, 0);
    }
    static Bitmap Flat(int alpha) { var b = new Bitmap(8, 8, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(alpha, 255, 255, 255)); return b; }

    public static void RenderAll(string outDir)
    {
        Func<string, string> P = n => System.IO.Path.Combine(outDir, n + ".png");
        var bg = Background(); bg.Save(P("pr_bg"), ImageFormat.Png);
        for (int v = 0; v < 10; v++) { Lit(Digit(v), Red).Save(P("pr_g" + v), ImageFormat.Png); Ambient(Digit(v)).Save(P("pr_a" + v), ImageFormat.Png); }
        var sym = new Bitmap[Symbols]; for (int n = 0; n < Symbols; n++) sym[n] = Lit(Symbol(n), Red);
        SaveRun(bg, sym, P);
        Strip(BatteryIcon(), IconW, 3.6f).Save(P("pr_bat"), ImageFormat.Png);
        Strip(BatteryTicks(), TicksW, 5f).Save(P("pr_ticks"), ImageFormat.Png);
        Flat(0).Save(P("pr_off"), ImageFormat.Png);
    }
}
