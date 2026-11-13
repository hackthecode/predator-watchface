// Renderer for the Predator watch face bitmaps: an alien hunter's wrist computer.
// Everything is drawn procedurally. The numerals are an invented system (see Glyph).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

public static class PredatorRender
{
    const int S = 466;
    public static string LabelFont = "Bahnschrift SemiBold Condensed";

    // ---- layout shared with tools/gen-xml.ps1
    public static readonly int[] CellX = { 66, 148, 250, 332 };
    public const int CellY = 146, CellW = 70, CellH = 128, Pad = 14;
    public static readonly Rectangle Screen = new Rectangle(38, 122, 390, 212);   // bounding box of the recessed display
    public static readonly int[] BarX = { 108, 248 };
    public const int BarY = 352, BarW = 110, BarH = 12;

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

    // light -> transparent PNG. layers: blur radius, r, g, b, gain
    static readonly float[][] Red = {
        new float[]{0, 1.00f, 0.74f, 0.66f, 1.00f}, new float[]{2, 1.00f, 0.14f, 0.08f, 0.95f}, new float[]{5, 1.00f, 0.05f, 0.03f, 0.75f},
        new float[]{11, 1.00f, 0.03f, 0.02f, 0.50f}, new float[]{22, 0.90f, 0.02f, 0.02f, 0.30f} };
    static readonly float[][] Hot = {
        new float[]{0, 1.00f, 1.00f, 1.00f, 1.00f}, new float[]{2, 1.00f, 0.95f, 0.45f, 0.95f}, new float[]{5, 1.00f, 0.62f, 0.10f, 0.75f},
        new float[]{11, 1.00f, 0.25f, 0.05f, 0.45f} };
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

    // ------------------------------------------------------------ noise + pixel helpers
    static float Hash(int x, int y) { unchecked { int n = x * 374761393 + y * 668265263; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xffff) / 65535f; } }
    static float Noise(float x, float y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y); float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }
    static float Fbm(float x, float y) { float s = 0, amp = 0.5f; for (int o = 0; o < 5; o++) { s += Noise(x, y) * amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; } return s; }
    static void Pixels(Bitmap bmp, Func<int, int, Color, Color> f)
    {
        int w = bmp.Width, h = bmp.Height;
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var buf = new byte[w * h * 4]; Marshal.Copy(d.Scan0, buf, 0, buf.Length);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y * w + x) * 4; var c = f(x, y, Color.FromArgb(buf[i + 3], buf[i + 2], buf[i + 1], buf[i])); buf[i + 3] = c.A; buf[i + 2] = c.R; buf[i + 1] = c.G; buf[i] = c.B; }
        Marshal.Copy(buf, 0, d.Scan0, buf.Length); bmp.UnlockBits(d);
    }
    static void AddLight(Bitmap bmp, float cx, float cy, float rx, float ry, float r, float g, float b, float strength)
    {
        Pixels(bmp, (x, y, c) => { float dx = (x - cx) / rx, dy = (y - cy) / ry, d = dx * dx + dy * dy; if (d > 1) return c; float v = (float)Math.Pow(1 - d, 2) * strength;
            return Color.FromArgb(c.A, Clamp(c.R + r * v), Clamp(c.G + g * v), Clamp(c.B + b * v)); });
    }

    // ------------------------------------------------------------ the numerals
    // Each cell is a segment display: eleven LED bars on a 2 x 3 lattice of nodes (two verticals per side, three horizontals,
    // four diagonals). A symbol is a handful of lit bars. The sixteen patterns below are invented for this face:
    // the first ten stand for the digits 0-9, the other six only appear while the display is running.
    static readonly PointF[] Node = { new PointF(14, 12), new PointF(56, 12), new PointF(14, 64), new PointF(56, 64), new PointF(14, 116), new PointF(56, 116) };
    //                              0:LU    1:LL    2:RU    3:RL    4:top   5:mid   6:bot   7:/up   8:/low  9:\up   10:\low
    static readonly int[][] Seg = { new[]{0,2}, new[]{2,4}, new[]{1,3}, new[]{3,5}, new[]{0,1}, new[]{2,3}, new[]{4,5}, new[]{2,1}, new[]{4,3}, new[]{0,3}, new[]{2,5} };
    public static readonly int[][] Pattern = {
        new[]{7, 10}, new[]{0, 8}, new[]{9, 1, 6}, new[]{7, 3}, new[]{4, 9, 8}, new[]{2, 5, 10}, new[]{0, 7, 10, 3}, new[]{1, 9, 2}, new[]{4, 8, 3}, new[]{0, 5, 8, 6},
        new[]{7, 8}, new[]{9, 10, 6}, new[]{4, 1, 3}, new[]{2, 10}, new[]{0, 9, 8}, new[]{5, 7, 1} };
    public const int Symbols = 16;
    static Pen Stroke(Color c, float w) { var p = new Pen(c, w); p.StartCap = LineCap.Triangle; p.EndCap = LineCap.Triangle; p.LineJoin = LineJoin.Miter; return p; }
    // one LED bar between two nodes, pulled back from both so neighbouring bars never touch
    static void Segment(Graphics g, Pen pen, float ox, float oy, int s)
    {
        var a = Node[Seg[s][0]]; var b = Node[Seg[s][1]]; float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy), gap = 6.5f / len;
        g.DrawLine(pen, ox + a.X + dx * gap, oy + a.Y + dy * gap, ox + b.X - dx * gap, oy + b.Y - dy * gap);
    }
    public static void Glyph(Graphics g, int v, float ox, float oy, Color c, float w)
    { using (var pen = Stroke(c, w)) foreach (int s in Pattern[v]) Segment(g, pen, ox, oy, s); }
    static void GlyphAll(Graphics g, float ox, float oy, Color c, float w)
    { using (var pen = Stroke(c, w)) for (int s = 0; s < Seg.Length; s++) Segment(g, pen, ox, oy, s); }

    public static Bitmap LitGlyph(int v, float[][] pal)
    {
        int w = CellW + 2 * Pad, h = CellH + 2 * Pad;
        var wire = MaskOf(w, h, g => Glyph(g, v, Pad, Pad, Color.White, 6.5f));
        return Glow(w, h, wire, pal, 9);
    }
    public static Bitmap AmbientGlyph(int v)
    {
        int w = CellW, h = CellH;
        var m = MaskOf(w, h, g => Glyph(g, v, 0, 0, Color.White, 2.2f));
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); var buf = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { float a = Math.Min(1, m[i] * 1.2f); if (a < 0.03f) continue; buf[i * 4 + 2] = 255; buf[i * 4 + 1] = 40; buf[i * 4] = 30; buf[i * 4 + 3] = (byte)(255 * a); }
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(buf, 0, d.Scan0, buf.Length); bmp.UnlockBits(d); return bmp;
    }

    // ------------------------------------------------------------ housing
    static GraphicsPath ScreenPath(float inset)
    {
        float l = Screen.Left + inset, r = Screen.Right - inset, t = Screen.Top + inset, b = Screen.Bottom - inset, cx = 32 - inset * 0.4f, cy = 58 - inset * 0.4f;
        var p = new GraphicsPath();
        p.AddPolygon(new[] { new PointF(l, t + cy), new PointF(l + cx, t), new PointF(r - cx, t), new PointF(r, t + cy), new PointF(r, b - cy), new PointF(r - cx, b), new PointF(l + cx, b), new PointF(l, b - cy) });
        return p;
    }
    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void Slot(Graphics g, RectangleF r, float rad)
    {
        using (var br = new SolidBrush(Color.FromArgb(255, 7, 3, 3))) g.FillPath(br, RoundRect(r, rad));
        using (var pen = new Pen(Color.FromArgb(220, 0, 0, 0), 2f)) g.DrawPath(pen, RoundRect(r, rad));
        using (var pen = new Pen(Color.FromArgb(90, 170, 175, 190), 1f)) g.DrawArc(pen, r.Left, r.Bottom - 2 * rad, 2 * rad, 2 * rad, 90, 60);
        using (var pen = new Pen(Color.FromArgb(80, 170, 175, 190), 1f)) g.DrawLine(pen, r.Left + rad, r.Bottom + 0.8f, r.Right - rad, r.Bottom + 0.8f);
    }
    static void Engrave(Graphics g, string text, RectangleF r, float size)
    {
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using (var f = new Font(LabelFont, size, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var b = new SolidBrush(Color.FromArgb(230, 0, 0, 0))) g.DrawString(text, f, b, new RectangleF(r.X, r.Y - 1, r.Width, r.Height), sf);
            using (var b = new SolidBrush(Color.FromArgb(170, 150, 156, 170))) g.DrawString(text, f, b, r, sf);
        }
    }
    static void Bolt(Graphics g, float cx, float cy, float r)
    {
        using (var lg = new LinearGradientBrush(new RectangleF(cx - r, cy - r, 2 * r, 2 * r), Color.FromArgb(255, 132, 134, 144), Color.FromArgb(255, 22, 23, 26), 60f)) g.FillEllipse(lg, cx - r, cy - r, 2 * r, 2 * r);
        var tri = new PointF[3]; for (int i = 0; i < 3; i++) { double a = Math.PI * 2 / 3 * i - Math.PI / 2; tri[i] = new PointF(cx + (float)Math.Cos(a) * r * 0.55f, cy + (float)Math.Sin(a) * r * 0.55f); }
        using (var br = new SolidBrush(Color.FromArgb(255, 8, 8, 10))) g.FillPolygon(br, tri);
    }

    public static Bitmap Background()
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g0 = Graphics.FromImage(bmp)) g0.Clear(Color.Black);
        // forged dark alloy: mottled, with a faint grain running around the centre
        Pixels(bmp, (x, y, c) =>
        {
            float dx = x - 233, dy = y - 233, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d > 233) return Color.Black;
            float ang = (float)Math.Atan2(dy, dx);
            float mott = Fbm(x * 0.022f, y * 0.022f), grain = Fbm(ang * 9f, d * 0.35f), pit = Hash(x, y);
            float v = (20 + 30 * mott + 12 * grain) * (0.94f + 0.12f * pit);
            v *= 0.55f + 0.55f * (1 - (dx + dy) / 660f);            // lit from the upper left
            v *= 1 - 0.45f * Smooth((d - 150) / 90f) * 0.7f;
            return Color.FromArgb(255, Clamp(v * 0.97f), Clamp(v * 1.0f), Clamp(v * 1.08f));
        });
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var rnd = new Random(7);
            // armour ribs: overlapping plates fanning out above and below the display
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 7; i++)
                {
                    float a0 = (side < 0 ? 200 : 20) + i * 20, sweep = 17;
                    var rr = new RectangleF(233 - 214, 233 - 214, 428, 428);
                    using (var pen = new Pen(Color.FromArgb(190, 6, 6, 8), 22)) g.DrawArc(pen, rr, a0, sweep);
                    using (var pen = new Pen(Color.FromArgb(255, 34 + i % 2 * 6, 36 + i % 2 * 6, 42 + i % 2 * 6), 18)) g.DrawArc(pen, rr, a0 + 0.6f, sweep - 1.2f);
                    var hi = new RectangleF(233 - 222, 233 - 222, 444, 444);
                    using (var pen = new Pen(Color.FromArgb(side < 0 ? 120 : 45, 190, 195, 210), 1.3f)) g.DrawArc(pen, hi, a0 + 1.2f, sweep - 2.4f);
                    var lo = new RectangleF(233 - 205, 233 - 205, 410, 410);
                    using (var pen = new Pen(Color.FromArgb(200, 0, 0, 0), 1.6f)) g.DrawArc(pen, lo, a0 + 1.2f, sweep - 2.4f);
                }
            // a ring of small engraved numerals inside the ribs, like a serial or a prayer
            for (int i = 0; i < 28; i++)
            {
                double a = (i + 0.5) * Math.PI * 2 / 28; if (Math.Abs(Math.Sin(a)) < 0.42) continue;       // leave the flanks of the display clear
                var st = g.Save();
                g.TranslateTransform(233 + (float)Math.Cos(a) * 186, 233 + (float)Math.Sin(a) * 186); g.RotateTransform((float)(a * 180 / Math.PI) + 90); g.ScaleTransform(0.15f, 0.15f);
                int v = rnd.Next(0, Symbols);
                Glyph(g, v, -35, -64, Color.FromArgb(220, 0, 0, 0), 12f); Glyph(g, v, -31, -60, Color.FromArgb(70, 170, 175, 190), 6f);
                g.Restore(st);
            }
            // wear: hairline scratches and three parallel gouges
            for (int i = 0; i < 90; i++)
            {
                float x = rnd.Next(20, 446), y = rnd.Next(20, 446), len = 6 + (float)rnd.NextDouble() * 34; double a = rnd.NextDouble() * Math.PI;
                using (var pen = new Pen(Color.FromArgb(rnd.Next(10, 42), 215, 220, 235), 0.6f)) g.DrawLine(pen, x, y, x + (float)Math.Cos(a) * len, y + (float)Math.Sin(a) * len);
            }
            for (int i = 0; i < 3; i++)
            {
                float ox = 300 + i * 11, oy = 44 + i * 4;
                using (var pen = new Pen(Color.FromArgb(215, 0, 0, 0), 3.4f)) { pen.StartCap = LineCap.Triangle; pen.EndCap = LineCap.Triangle; g.DrawBezier(pen, ox, oy, ox + 14, oy + 16, ox + 22, oy + 34, ox + 26, oy + 58); }
                using (var pen = new Pen(Color.FromArgb(105, 205, 210, 225), 0.9f)) g.DrawBezier(pen, ox + 2.2f, oy + 1, ox + 16, oy + 17, ox + 24, oy + 35, ox + 28, oy + 58);
            }
            // display recess: bevelled frame, dark red glass with scan lines
            using (var p = ScreenPath(-7)) using (var br = new SolidBrush(Color.FromArgb(255, 9, 9, 11))) g.FillPath(br, p);
            using (var p = ScreenPath(-7)) using (var pen = new Pen(Color.FromArgb(150, 185, 190, 205), 1.4f)) g.DrawPath(pen, p);
            using (var p = ScreenPath(0))
            {
                using (var pg = new PathGradientBrush(p)) { pg.CenterColor = Color.FromArgb(255, 44, 5, 5); pg.SurroundColors = new[] { Color.FromArgb(255, 9, 1, 1) }; pg.FocusScales = new PointF(0.55f, 0.35f); g.FillPath(pg, p); }
                var st = g.Save(); g.SetClip(p);
                using (var pen = new Pen(Color.FromArgb(46, 0, 0, 0), 1f)) for (int y = Screen.Top; y < Screen.Bottom; y += 3) g.DrawLine(pen, Screen.Left, y + 0.5f, Screen.Right, y + 0.5f);
                using (var lg = new LinearGradientBrush(new RectangleF(Screen.Left, Screen.Top - 1, Screen.Width, 30), Color.FromArgb(190, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f)) g.FillRectangle(lg, Screen.Left, Screen.Top, Screen.Width, 29);
                // unlit segments of every cell
                foreach (int cx in CellX) GlyphAll(g, cx, CellY, Color.FromArgb(105, 66, 8, 8), 6.5f);
                // separator: two lit diamonds
                foreach (float cy in new[] { 186f, 234f }) using (var br = new SolidBrush(Color.FromArgb(255, 255, 70, 50))) g.FillPolygon(br, new[] { new PointF(233, cy - 7), new PointF(238, cy), new PointF(233, cy + 7), new PointF(228, cy) });
                g.Restore(st);
                using (var pen = new Pen(Color.FromArgb(235, 0, 0, 0), 3f)) g.DrawPath(pen, p);
            }
            // slots: date above the display, two gauges and the pulse readout below
            Slot(g, new RectangleF(163, 82, 140, 27), 5);
            foreach (int bx in BarX) Slot(g, new RectangleF(bx - 4, BarY - 4, BarW + 8, BarH + 8), 4);
            Slot(g, new RectangleF(190, 382, 86, 32), 5);
            Engrave(g, "PWR", new RectangleF(BarX[0], BarY - 19, BarW, 13), 10); Engrave(g, "TRK", new RectangleF(BarX[1], BarY - 19, BarW, 13), 10);
            Engrave(g, "BIO", new RectangleF(190, 415, 86, 12), 9);
            foreach (var b in new[] { new PointF(100, 100), new PointF(366, 100), new PointF(150, 398), new PointF(316, 398) }) Bolt(g, b.X, b.Y, 5);
        }
        // the display bleeds red onto the frame around it; the separator glows
        AddLight(bmp, 233, 228, 235, 150, 255, 12, 6, 0.10f);
        foreach (float cy in new[] { 186f, 234f }) AddLight(bmp, 233, cy, 16, 18, 255, 30, 20, 0.55f);
        Pixels(bmp, (x, y, c) => { float dx = x - 233, dy = y - 233; return dx * dx + dy * dy > 233 * 233 ? Color.Black : c; });
        return bmp;
    }

    // glass over the display: one soft slanted band and a thin lit upper edge (own layer, slides with the wrist)
    public static Bitmap Highlight()
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
            using (var p = ScreenPath(2))
            {
                g.SetClip(p);
                using (var br = new SolidBrush(Color.FromArgb(13, 235, 240, 255))) g.FillPolygon(br, new[] { new PointF(150, 110), new PointF(236, 110), new PointF(150, 350), new PointF(64, 350) });
                using (var br = new SolidBrush(Color.FromArgb(8, 235, 240, 255))) g.FillPolygon(br, new[] { new PointF(262, 110), new PointF(290, 110), new PointF(204, 350), new PointF(176, 350) });
            }
        }
        return bmp;
    }

    // the three-dot ranging mark
    public static Bitmap TriDot()
    {
        int w = 84, h = 78;
        var m = MaskOf(w, h, g => { foreach (var p in new[] { new PointF(42, 21), new PointF(25, 51), new PointF(59, 51) }) g.FillEllipse(Brushes.White, p.X - 4.6f, p.Y - 4.6f, 9.2f, 9.2f); });
        return Glow(w, h, m, Red, 6);
    }

    // gauge fill: eleven lit segments, revealed left to right by a mask in the XML
    public static Bitmap BarLit()
    {
        var m = MaskOf(BarW, BarH, g => { for (int i = 0; i < 11; i++) g.FillRectangle(Brushes.White, i * 10 + 1.5f, 2.5f, 7, BarH - 5); });
        var L = new[] { new float[] { 0, 1.00f, 0.50f, 0.42f, 1.00f }, new float[] { 2, 1.00f, 0.08f, 0.05f, 0.80f }, new float[] { 5, 1.00f, 0.03f, 0.02f, 0.45f } };
        return Glow(BarW, BarH, m, L, 0);
    }

    // Running display: opaque frames of the four cells (cut from the background, so they cover the time numerals completely)
    // with symbols lit. The right-hand cell changes every frame, the left-hand one slowest, the way a counter runs.
    public static readonly Rectangle RunRect = new Rectangle(CellX[0] - Pad, CellY - Pad, CellX[3] + CellW + Pad - (CellX[0] - Pad), CellH + 2 * Pad);
    public const int RunFrames = 40;
    static readonly int[] Every = { 20, 10, 4, 1 };
    static void SaveRun(Bitmap bg, Bitmap[] lit, Func<string, string> P)
    {
        var rnd = new Random(23); var cur = new int[4];
        for (int f = 0; f < RunFrames; f++)
        {
            for (int i = 0; i < 4; i++) if (f % Every[i] == 0) { int n; do { n = rnd.Next(0, Symbols); } while (n == cur[i] && f > 0); cur[i] = n; }
            using (var bmp = bg.Clone(RunRect, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) for (int i = 0; i < 4; i++) g.DrawImage(lit[cur[i]], CellX[i] - Pad - RunRect.Left, CellY - Pad - RunRect.Top, lit[cur[i]].Width, lit[cur[i]].Height);
                bmp.Save(P("pr_r" + f.ToString("00")), ImageFormat.Png);
            }
        }
    }

    // thermal view: the same housing seen as heat. Cold metal runs blue to violet, the display and the pulse slot run hot.
    static Color Heat(float t)
    {
        t = Math.Max(0, Math.Min(1, t));
        float[][] stops = { new float[]{0f, 2, 2, 20}, new float[]{0.22f, 14, 20, 120}, new float[]{0.42f, 110, 20, 150}, new float[]{0.60f, 215, 35, 60}, new float[]{0.76f, 250, 130, 15}, new float[]{0.90f, 255, 230, 60}, new float[]{1f, 255, 255, 245} };
        for (int i = 1; i < stops.Length; i++) if (t <= stops[i][0])
        {
            float k = (t - stops[i - 1][0]) / (stops[i][0] - stops[i - 1][0]);
            return Color.FromArgb(255, Clamp(stops[i - 1][1] + (stops[i][1] - stops[i - 1][1]) * k), Clamp(stops[i - 1][2] + (stops[i][2] - stops[i - 1][2]) * k), Clamp(stops[i - 1][3] + (stops[i][3] - stops[i - 1][3]) * k));
        }
        return Color.White;
    }
    public static Bitmap Thermal(Bitmap bg)
    {
        var bmp = (Bitmap)bg.Clone();
        var inside = MaskOf(S, S, g => { using (var p = ScreenPath(0)) g.FillPath(Brushes.White, p); });
        inside = Blur(inside, S, S, 5);
        Pixels(bmp, (x, y, c) =>
        {
            float dx = x - 233, dy = y - 233, d2 = dx * dx + dy * dy; if (d2 > 233 * 233) return Color.Black;
            float lum = (c.R * 0.5f + c.G * 0.3f + c.B * 0.2f) / 90f;
            float body = Fbm(x * 0.011f + 3, y * 0.011f + 7);                              // warmth of the arm underneath, in slow blobs
            float wrist = (float)Math.Exp(-((x - 233) * (x - 233) / 30000f + (y - 300) * (y - 300) / 9000f));
            float pulse = (float)Math.Exp(-((x - 233) * (x - 233) + (y - 398) * (y - 398)) / 1500f);
            float t = 0.16f + 0.26f * body + 0.20f * Math.Min(1, lum) + 0.22f * wrist + 0.50f * pulse;
            t = t * (1 - inside[y * S + x]) + (0.12f + 0.10f * body) * inside[y * S + x];  // the display glass itself reads cold
            t += (Hash(x / 2, y / 2) - 0.5f) * 0.035f;                                      // sensor noise in 2 px blocks
            return Heat(t);
        });
        return bmp;
    }
    static Bitmap Flat(int alpha) { var b = new Bitmap(8, 8, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(alpha, 255, 255, 255)); return b; }

    public static void RenderAll(string outDir)
    {
        Func<string, string> P = n => System.IO.Path.Combine(outDir, n + ".png");
        var bg = Background(); bg.Save(P("pr_bg"), ImageFormat.Png);
        // pr_g0-9 = digits; the six extra symbols only appear baked into the running frames pr_r00-39
        var lit = new Bitmap[Symbols];
        for (int v = 0; v < Symbols; v++) { lit[v] = LitGlyph(v, Red); if (v < 10) lit[v].Save(P("pr_g" + v), ImageFormat.Png); }
        SaveRun(bg, lit, P);
        for (int v = 0; v < 10; v++) { LitGlyph(v, Hot).Save(P("pr_t" + v), ImageFormat.Png); AmbientGlyph(v).Save(P("pr_a" + v), ImageFormat.Png); }
        Thermal(bg).Save(P("pr_thermal"), ImageFormat.Png);
        Highlight().Save(P("pr_hl"), ImageFormat.Png);
        TriDot().Save(P("pr_tri"), ImageFormat.Png);
        BarLit().Save(P("pr_bar"), ImageFormat.Png);
        Flat(255).Save(P("pr_on"), ImageFormat.Png); Flat(0).Save(P("pr_off"), ImageFormat.Png);
    }
}
