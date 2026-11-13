using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FxKit;

namespace Den3
{
    /// <summary>Большая плитка-ссылка на лабораторную работу с собственной мини-иллюстрацией.</summary>
    public class LabTile : FxCard, IEnterAnim
    {
        private readonly AnimValue hover;
        private float appear = 1f;
        private double time;

        public string Number = "", Heading = "", Body = "", IconName = "bug";
        public int Art;

        public LabTile()
        {
            hover = new AnimValue(this);
            Radius = 18;
            Cursor = Cursors.Hand;
            Animator.Frame += OnFrame;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Frame -= OnFrame;
            base.Dispose(disposing);
        }

        private void OnFrame(float dt)
        {
            if (hover.Value < 0.02f || !IsHandleCreated || !Visible) return;
            time += dt;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f, 240); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f, 360); }

        public void PrepareEnter() { appear = 0f; Invalidate(); }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "appear", 0, 1, 700, Ease.OutCubic, delegate(float v) { appear = v; Invalidate(); }, null, delayMs);
        }

        protected override void DrawContent(Graphics g)
        {
            float h = hover.Value, a = appear;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath clip = Gfx.Round(r, Radius))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                if (h > 0.01f)
                    using (var b = new LinearGradientBrush(new RectangleF(-1, -1, Width + 2, Height + 2),
                               Gfx.A(P.Accent, (int)(44 * h)), Gfx.A(P.Accent2, (int)(10 * h)), 60f))
                        g.FillRectangle(b, r);
                Gfx.Text(g, Number, Fonts.Display(54f), Gfx.A(P.Text, (int)((P.Dark ? 16 : 14) + 22 * h)),
                    new RectangleF(Width - 190, -6, 186, 96), StringAlignment.Far);
                DrawArt(g, new RectangleF(22, Height - 150, Width - 44, 92), h);
                g.Restore(st);
                if (h > 0.01f)
                    using (var pen = new Pen(Gfx.A(P.Accent2, (int)(200 * h)), 1.5f))
                        g.DrawPath(pen, clip);
            }

            float dy = (1 - a) * 22;
            var badge = new RectangleF(24, 26 + dy - 4 * h, 50, 50);
            Gfx.Glow(g, badge, 14, P.Accent, 5, 1.6f, (int)(22 + 40 * h));
            using (LinearGradientBrush b = Gfx.TriBrush(RectangleF.Inflate(badge, 1, 1), 45f))
                Gfx.FillRound(g, b, badge, 14);
            Icons.Draw(g, IconName, RectangleF.Inflate(badge, -13, -13), Color.White, 2f);

            Gfx.Text(g, Heading, Fonts.Display(11.5f), Gfx.A(P.Text, a), new RectangleF(24, 92 + dy, Width - 44, 52),
                StringAlignment.Near, StringAlignment.Near, true);
            Gfx.Text(g, Body, Fonts.Body(9.5f), Gfx.A(P.TextDim, a), new RectangleF(24, 148 + dy, Width - 46, Math.Max(20, Height - 148 - 160)),
                StringAlignment.Near, StringAlignment.Near, true);

            float ax = 24 + 8 * h;
            Gfx.Text(g, "Открыть", Fonts.Semi(9.5f), Gfx.Mix(P.TextDim, P.Accent2, h), new RectangleF(24, Height - 46, 90, 22));
            Icons.Draw(g, "arrow", new RectangleF(ax + 66, Height - 43, 16, 16), Gfx.Mix(P.TextDim, P.Accent2, h), 1.8f);
        }

        // мини-иллюстрация: маленькое дерево решений, оптимальная ветвь подсвечивается при наведении
        private void DrawArt(Graphics g, RectangleF r, float h)
        {
            float t = (float)time;
            int alpha = (int)(70 + 120 * h);
            int levels = Art + 1;
            float colW = Math.Min(70, (r.Width - 30) / (levels + 1));
            var nodes = new List<PointF> { new PointF(r.X + 8, r.Y + r.Height / 2) };
            int pick = (int)(t * 1.5f) % 2;
            PointF cur = nodes[0];
            float spread = r.Height / 2 - 6;
            for (int lv = 0; lv < levels; lv++)
            {
                float x = cur.X + colW;
                PointF up = new PointF(x, cur.Y - spread), dn = new PointF(x, cur.Y + spread);
                bool hot = h > 0.05f;
                using (var pen = new Pen(Gfx.A(P.BorderHi, alpha), 1.6f))
                {
                    g.DrawLine(pen, cur, up);
                    g.DrawLine(pen, cur, dn);
                }
                PointF next = (lv + pick) % 2 == 0 ? up : dn;
                if (hot)
                    using (var pen = new Pen(Gfx.A(P.Accent2, alpha + 60), 2.4f))
                        g.DrawLine(pen, cur, next);
                Shape(g, cur, lv % 2 == 0 ? 0 : 1, hot ? P.Accent2 : P.BorderHi, alpha);
                Shape(g, up == next ? dn : up, 2, P.BorderHi, alpha);
                cur = next;
                spread *= 0.55f;
            }
            Shape(g, cur, 2, h > 0.05f ? P.Accent2 : P.BorderHi, alpha + 40);
        }

        private static void Shape(Graphics g, PointF p, int kind, Color c, int alpha)
        {
            var r = new RectangleF(p.X - 6, p.Y - 6, 12, 12);
            using (var pen = new Pen(Gfx.A(c, Math.Min(255, alpha + 40)), 1.6f))
            using (var b = new SolidBrush(Gfx.A(c, alpha / 3)))
            {
                if (kind == 0) { g.FillRectangle(b, r); g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height); }
                else if (kind == 1) { g.FillEllipse(b, r); g.DrawEllipse(pen, r); }
                else
                {
                    PointF[] tri = { new PointF(r.Left, p.Y), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom) };
                    g.FillPolygon(b, tri);
                    g.DrawPolygon(pen, tri);
                }
            }
        }
    }

    /// <summary>Шапка главной страницы: крупный заголовок и бегущие волны.</summary>
    public class HeroBanner : FxCard
    {
        private double time;
        public string Heading = "", Sub = "";
        public string[] Chips = new string[0];

        public HeroBanner()
        {
            Radius = 18;
            Animator.Frame += OnFrame;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Frame -= OnFrame;
            base.Dispose(disposing);
        }

        private void OnFrame(float dt)
        {
            if (!IsHandleCreated || !Visible || Animator.Instant) return;
            time += dt;
            Invalidate(new Rectangle((int)(Width * 0.58f), 2, (int)(Width * 0.42f) - 4, Height - 4));
        }

        protected override void DrawContent(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath clip = Gfx.Round(r, Radius))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (var b = new LinearGradientBrush(new RectangleF(-1, -1, Width + 2, Height + 2), Gfx.A(P.Accent, P.Dark ? 60 : 40), Gfx.A(P.Accent2, 0), 0f))
                    g.FillRectangle(b, r);
                // волны
                float x0 = Width * 0.6f;
                for (int k = 0; k < 4; k++)
                {
                    var pts = new List<PointF>();
                    for (float x = x0; x <= Width + 8; x += 8)
                    {
                        float u = (x - x0) / (Width - x0);
                        float y = Height * (0.36f + k * 0.12f) + (float)Math.Sin(u * 7 + time * (0.9 + k * 0.25) + k * 1.3) * (10 + k * 4) * u;
                        pts.Add(new PointF(x, y));
                    }
                    Color c = k % 2 == 0 ? P.Accent2 : P.Accent3;
                    using (var pen = new Pen(Gfx.A(c, 150 - k * 22), 2f))
                        g.DrawCurve(pen, pts.ToArray(), 0.5f);
                }
                g.Restore(st);
            }
            Font f = Fonts.Display(21f);
            using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(24, 0, Math.Max(80, Gfx.Measure(Heading, f).Width + 10), Height)))
                Gfx.Text(g, Heading, f, b, new RectangleF(26, 22, Width - 60, 44));
            Gfx.Text(g, Sub, Fonts.Body(10.5f), P.TextDim, new RectangleF(29, 68, Width * 0.56f, 44), StringAlignment.Near, StringAlignment.Near, true);
            float x1 = 29;
            foreach (string chip in Chips)
            {
                float w = Gfx.TextWidth(chip, Fonts.Semi(8.5f)) + 24;
                var cr = new RectangleF(x1, Height - 44, w, 24);
                Gfx.FillRound(g, Gfx.A(P.Surface2, 220), cr, 12);
                Gfx.DrawRound(g, P.BorderHi, cr, 12);
                Gfx.Text(g, chip, Fonts.Semi(8.5f), P.TextDim, cr, StringAlignment.Center);
                x1 += w + 8;
            }
        }
    }
}
