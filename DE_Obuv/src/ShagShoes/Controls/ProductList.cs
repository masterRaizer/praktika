using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FxKit;
using ShagShoes.Logic;
using ShagShoes.Models;

namespace ShagShoes.Controls
{
    /// <summary>
    /// Каталог моделей обуви по макету: изображение, «Производство | Наименование», категория,
    /// количество («много» / «мало»), состав и цена. Модели, которых осталось 3 пары или меньше,
    /// подсвечиваются светло-красным. Список прокручивается колесом мыши и полосой справа.
    /// </summary>
    public sealed class ProductList : FxControl, IEnterAnim
    {
        private const int ItemHeight = 132;
        private const int ItemGap = 10;
        private const int ImageWidth = 152;
        private const int PriceWidth = 190;
        private const int BarWidth = 10;

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private readonly List<Product> products = new List<Product>();
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly AnimValue scroll;
        private Image placeholder;
        private float enter = 1f;
        private int hoverIndex = -1;
        private bool dragBar;
        private int dragY;
        private float dragScroll;
        private string message = "Каталог загружается…";

        public ProductList()
        {
            scroll = new AnimValue(this);
        }

        /// <summary>Папка с изображениями моделей (имена файлов хранятся в базе).</summary>
        public string ImagesFolder { get; set; }

        /// <summary>Картинка-заглушка из ресурсов для моделей без изображения.</summary>
        public string PlaceholderFile { get; set; }

        public void SetProducts(IEnumerable<Product> items)
        {
            products.Clear();
            products.AddRange(items);
            message = products.Count == 0 ? "В каталоге пока нет моделей" : null;
            hoverIndex = -1;
            scroll.Set(0);
            Invalidate();
        }

        /// <summary>Вместо списка показывается сообщение (например, база недоступна).</summary>
        public void ShowMessage(string text)
        {
            products.Clear();
            message = text;
            scroll.Set(0);
            Invalidate();
        }

        // ---------- анимация появления ----------

        public void PrepareEnter()
        {
            enter = 0f;
            Invalidate();
        }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "enter", 0, 1, 900, Ease.OutCubic, delegate(float t)
            {
                enter = t;
                Invalidate();
            }, null, delayMs);
        }

        // ---------- прокрутка и мышь ----------

        private float ContentHeight
        {
            get { return products.Count * (ItemHeight + ItemGap); }
        }

        private float MaxScroll
        {
            get { return Math.Max(0, ContentHeight - ItemGap - Height); }
        }

        private int IndexAt(int y)
        {
            float pos = y + scroll.Value;
            int index = (int)(pos / (ItemHeight + ItemGap));
            if (index < 0 || index >= products.Count) return -1;
            return pos - index * (ItemHeight + ItemGap) <= ItemHeight ? index : -1;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            float next = scroll.Target - Math.Sign(e.Delta) * (ItemHeight + ItemGap);
            scroll.To(Gfx.Clamp(next, 0, MaxScroll), 280, Ease.OutCubic);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && MaxScroll > 0 && e.X >= Width - BarWidth)
            {
                dragBar = true;
                dragY = e.Y;
                dragScroll = scroll.Value;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragBar = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragBar)
            {
                float k = ContentHeight / Math.Max(1, Height);
                scroll.Set(Gfx.Clamp(dragScroll + (e.Y - dragY) * k, 0, MaxScroll));
                return;
            }
            int index = e.X < Width - BarWidth ? IndexAt(e.Y) : -1;
            if (index != hoverIndex)
            {
                hoverIndex = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverIndex = -1;
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (scroll.Target > MaxScroll) scroll.Set(MaxScroll);
        }

        // ---------- изображения ----------

        private Image LoadImage(string path)
        {
            // копия в памяти, чтобы файл не оставался заблокированным
            byte[] bytes = File.ReadAllBytes(path);
            using (var stream = new MemoryStream(bytes))
            using (Image source = Image.FromStream(stream))
            {
                return new Bitmap(source);
            }
        }

        private Image GetPlaceholder()
        {
            if (placeholder != null) return placeholder;
            try
            {
                if (!string.IsNullOrEmpty(PlaceholderFile) && File.Exists(PlaceholderFile))
                {
                    placeholder = LoadImage(PlaceholderFile);
                }
            }
            catch (Exception)
            {
                placeholder = null;   // повреждённый файл — рисуем пустую рамку
            }
            return placeholder;
        }

        private Image GetImage(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.ImageFile) || string.IsNullOrEmpty(ImagesFolder))
            {
                return GetPlaceholder();
            }
            Image image;
            if (imageCache.TryGetValue(product.ImageFile, out image))
            {
                return image ?? GetPlaceholder();
            }
            try
            {
                string path = Path.Combine(ImagesFolder, product.ImageFile);
                image = File.Exists(path) ? LoadImage(path) : null;
            }
            catch (Exception)
            {
                image = null;
            }
            imageCache[product.ImageFile] = image;
            return image ?? GetPlaceholder();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Image image in imageCache.Values)
                {
                    if (image != null) image.Dispose();
                }
                imageCache.Clear();
                if (placeholder != null) placeholder.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---------- отрисовка ----------

        /// <summary>Светло-красная подсветка; в тёмной теме — приглушённый красный того же оттенка.</summary>
        private static Color LowStockColor
        {
            get { return P.Dark ? Theme.Hex("4A2630") : Theme.Hex("FFD6D6"); }
        }

        private static string Money(decimal value)
        {
            return value.ToString("N2", Ru) + " руб.";
        }

        protected override void Draw(Graphics g)
        {
            if (message != null)
            {
                Icons.Draw(g, "box", new RectangleF(Width / 2f - 18, Height / 2f - 52, 36, 36), P.TextMute, 1.6f);
                Gfx.Text(g, message, Fonts.Medium(10f), P.TextDim,
                    new RectangleF(40, Height / 2f - 8, Width - 80, 70), StringAlignment.Center, StringAlignment.Near, true);
                return;
            }

            float width = Width - BarWidth - 6;
            int shown = 0;
            for (int i = 0; i < products.Count; i++)
            {
                float y = i * (ItemHeight + ItemGap) - scroll.Value;
                if (y + ItemHeight < 0 || y > Height) continue;
                float local = Gfx.Clamp(enter * 2.6f - shown * 0.12f, 0, 1);
                shown++;
                if (local <= 0.01f) continue;
                float dy = (1 - Ease.OutCubic(local)) * 18;
                DrawItem(g, products[i], new RectangleF(0, y + dy, width, ItemHeight), i == hoverIndex, local);
            }

            if (MaxScroll > 0)
            {
                float barHeight = Math.Max(30, Height * Height / ContentHeight);
                float barY = (Height - barHeight) * (scroll.Value / MaxScroll);
                Gfx.FillRound(g, Gfx.A(P.TextMute, dragBar ? 200 : 110), new RectangleF(Width - 6, barY, 4, barHeight), 2);
            }
        }

        private void DrawItem(Graphics g, Product product, RectangleF r, bool hover, float alpha)
        {
            bool lowStock = PriceCalculator.IsLowStock(product.TotalPairs);
            Color fill = lowStock ? LowStockColor : P.Surface2;
            if (hover) fill = Gfx.Mix(fill, lowStock ? P.Bad : P.Accent, P.Dark ? 0.08f : 0.05f);
            Gfx.FillRound(g, Gfx.A(fill, alpha), r, 12);
            Color border = lowStock ? Gfx.A(P.Bad, P.Dark ? 150 : 120) : (hover ? P.BorderHi : P.Border);
            Gfx.DrawRound(g, Gfx.A(border, alpha), r, 12, hover ? 1.4f : 1f);

            // изображение (или заглушка picture.png)
            var imageRect = new RectangleF(r.X + 12, r.Y + 12, ImageWidth, r.Height - 24);
            DrawImageCover(g, GetImage(product), imageRect, 10, alpha);
            if (lowStock)
            {
                DrawTag(g, "заканчивается", P.Bad, new PointF(imageRect.X + 6, imageRect.Bottom - 26), alpha);
            }

            // тексты по макету
            float x = imageRect.Right + 18;
            float textWidth = r.Right - PriceWidth - x - 10;
            Color text = Gfx.A(P.Text, alpha);
            Color dim = Gfx.A(P.TextDim, alpha);
            Color mute = Gfx.A(P.TextMute, alpha);
            Gfx.Text(g, product.Title, Fonts.Semi(11.5f), text, new RectangleF(x, r.Y + 12, textWidth, 24));
            Gfx.Text(g, product.Category, Fonts.Medium(9.5f), dim, new RectangleF(x, r.Y + 40, textWidth, 20));

            string quantityCaption = "Количество:";
            Font quantityFont = Fonts.Medium(9.5f);
            float captionWidth = Gfx.TextWidth(quantityCaption, quantityFont) + 6;
            Gfx.Text(g, quantityCaption, quantityFont, dim, new RectangleF(x, r.Y + 62, captionWidth + 4, 20));
            string quantityText = PriceCalculator.GetQuantityText(product.TotalPairs);
            Color quantityColor = product.TotalPairs > PriceCalculator.ManyPairsThreshold ? P.Good : (lowStock ? P.Bad : P.Warn);
            Gfx.Text(g, quantityText, Fonts.Bold(9.5f), Gfx.A(quantityColor, alpha), new RectangleF(x + captionWidth, r.Y + 62, 80, 20));

            Gfx.Text(g, "Состав: " + product.Composition, Fonts.Body(8.5f), mute, new RectangleF(x, r.Y + 86, textWidth, 34),
                StringAlignment.Near, StringAlignment.Near, true);

            // цена
            using (var pen = new Pen(Gfx.A(lowStock ? P.Bad : P.Border, P.Dark ? 0.5f : 0.6f)))
            {
                g.DrawLine(pen, r.Right - PriceWidth, r.Y + 18, r.Right - PriceWidth, r.Bottom - 18);
            }
            var priceRect = new RectangleF(r.Right - PriceWidth + 12, r.Y, PriceWidth - 28, r.Height);
            if (product.HasDiscount)
            {
                string oldPrice = Money(product.Price);
                Font oldFont = Fonts.Medium(9.5f);
                float oldWidth = Gfx.TextWidth(oldPrice, oldFont);
                var oldRect = new RectangleF(priceRect.Right - oldWidth - 2, r.Y + 26, oldWidth + 4, 20);
                Gfx.Text(g, oldPrice, oldFont, mute, oldRect, StringAlignment.Far);
                using (var strike = new Pen(Gfx.A(P.Bad, alpha), 1.4f))
                {
                    g.DrawLine(strike, oldRect.X, oldRect.Y + 11f, oldRect.Right - 2, oldRect.Y + 11f);
                }
                using (LinearGradientBrush brush = Gfx.AccentBrush(new RectangleF(priceRect.X, r.Y + 44, priceRect.Width, 30)))
                {
                    Gfx.Text(g, Money(product.FinalPrice), Fonts.Display(13f), brush,
                        new RectangleF(priceRect.X, r.Y + 46, priceRect.Width, 30), StringAlignment.Far);
                }
                string discount = "−" + PriceCalculator.DiscountPercent.ToString("0", Ru) + " %";
                Font tagFont = Fonts.Bold(8.5f);
                float tagWidth = Gfx.TextWidth(discount, tagFont) + 16;
                DrawTag(g, discount, P.Good, new PointF(priceRect.Right - tagWidth, r.Y + 86), alpha);
            }
            else
            {
                Gfx.Text(g, Money(product.FinalPrice), Fonts.Display(13f), text,
                    new RectangleF(priceRect.X, r.Y + 30, priceRect.Width, r.Height - 60), StringAlignment.Far);
            }
        }

        private static void DrawTag(Graphics g, string text, Color color, PointF at, float alpha)
        {
            Font font = Fonts.Bold(8.5f);
            float width = Gfx.TextWidth(text, font) + 16;
            var rect = new RectangleF(at.X, at.Y, width, 20);
            Gfx.FillRound(g, Gfx.A(color, (int)(235 * alpha)), rect, 10);
            Gfx.Text(g, text, font, Gfx.A(Color.White, alpha), rect, StringAlignment.Center);
        }

        private static void DrawImageCover(Graphics g, Image image, RectangleF r, float radius, float alpha)
        {
            using (GraphicsPath path = Gfx.Round(r, radius))
            {
                Gfx.FillRound(g, Gfx.A(P.SurfaceHi, alpha), r, radius);
                if (image != null)
                {
                    // заполнение рамки с обрезкой по большей стороне
                    float scale = Math.Max(r.Width / image.Width, r.Height / image.Height);
                    float w = r.Width / scale, h = r.Height / scale;
                    var src = new RectangleF((image.Width - w) / 2, (image.Height - h) / 2, w, h);
                    GraphicsState state = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Gfx.DrawImageAlpha(g, image, r, src, alpha);
                    g.Restore(state);
                }
                using (var pen = new Pen(Gfx.A(P.Border, alpha)))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }
}
