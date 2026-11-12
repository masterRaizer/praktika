using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;

namespace FxKit
{
    /// <summary>Набор цветов одной темы оформления.</summary>
    public sealed class Palette
    {
        public bool Dark;
        public Color Bg, BgAlt;
        public Color Surface, Surface2, SurfaceHi;
        public Color Border, BorderHi;
        public Color Text, TextDim, TextMute;
        public Color Accent, Accent2, Accent3;
        public Color Good, Warn, Bad;
        public Color Grid;
        public Color[] Series;
        public Color[] Blobs;
    }

    /// <summary>
    /// Текущая тема. Все элементы Fx берут цвета отсюда в момент отрисовки,
    /// поэтому смена темы сводится к перерисовке окна.
    /// </summary>
    public static class Theme
    {
        private static readonly Palette dark = new Palette
        {
            Dark = true,
            Bg = Hex("070A15"), BgAlt = Hex("0E1328"),
            Surface = Hex("121829"), Surface2 = Hex("1A2138"), SurfaceHi = Hex("242D4B"),
            Border = Hex("262F4D"), BorderHi = Hex("3B4772"),
            Text = Hex("ECEFFA"), TextDim = Hex("A6AFCD"), TextMute = Hex("6E779B"),
            Accent = Hex("7C6CFF"), Accent2 = Hex("22D3EE"), Accent3 = Hex("F471B5"),
            Good = Hex("2CCB74"), Warn = Hex("FAB219"), Bad = Hex("F0596B"),
            Grid = Hex("212945"),
            Series = new[] { Hex("3987E5"), Hex("D95926"), Hex("199E70"), Hex("C98500"), Hex("D55181"), Hex("9085E9") },
            Blobs = new[] { Hex("6D4BFF"), Hex("0FA9D2"), Hex("D43A93"), Hex("2747D9") }
        };

        private static readonly Palette light = new Palette
        {
            Dark = false,
            Bg = Hex("E7EBF7"), BgAlt = Hex("F4F1FB"),
            Surface = Hex("FFFFFF"), Surface2 = Hex("F0F3FA"), SurfaceHi = Hex("E3E8F5"),
            Border = Hex("D9DFEE"), BorderHi = Hex("B7C1DB"),
            Text = Hex("131A2F"), TextDim = Hex("4A5576"), TextMute = Hex("8690AC"),
            Accent = Hex("5B47E0"), Accent2 = Hex("0C98BA"), Accent3 = Hex("D23F8E"),
            Good = Hex("0C9648"), Warn = Hex("B67600"), Bad = Hex("D03B3B"),
            Grid = Hex("E4E8F2"),
            Series = new[] { Hex("2A78D6"), Hex("EB6834"), Hex("1BAF7A"), Hex("EDA100"), Hex("E87BA4"), Hex("6250D6") },
            Blobs = new[] { Hex("B9ABFF"), Hex("A5E3F5"), Hex("F9C4DE"), Hex("B8C9FF") }
        };

        private static Palette current = dark;

        public static Palette P { get { return current; } }
        public static bool IsDark { get { return current.Dark; } }

        /// <summary>Срабатывает после смены темы.</summary>
        public static event EventHandler Changed;

        public static void Set(bool darkTheme)
        {
            Palette next = darkTheme ? dark : light;
            if (next == current) return;
            current = next;
            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        public static Color Hex(string rgb)
        {
            int v = Convert.ToInt32(rgb, 16);
            return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
        }
    }

    /// <summary>
    /// Собственные шрифты проекта (папка Fonts рядом с exe): Manrope — основной текст,
    /// Unbounded — заголовки, JetBrains Mono — числа и журналы.
    /// Если файл не найден, используется системный шрифт, программа продолжает работать.
    /// </summary>
    public static class Fonts
    {
        private sealed class Face
        {
            public FontFamily Family;
            public FontStyle Style;
        }

        private static readonly Dictionary<string, Face> faces = new Dictionary<string, Face>();
        private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        private static readonly List<PrivateFontCollection> keepAlive = new List<PrivateFontCollection>();
        private static bool loaded;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
            Add("body", dir, "Manrope-Regular.ttf", FontStyle.Regular, "Manrope", "Segoe UI");
            Add("medium", dir, "Manrope-Medium.ttf", FontStyle.Regular, "Manrope Medium", "Segoe UI");
            Add("semi", dir, "Manrope-SemiBold.ttf", FontStyle.Regular, "Manrope SemiBold", "Segoe UI Semibold");
            Add("bold", dir, "Manrope-Bold.ttf", FontStyle.Bold, "Manrope", "Segoe UI");
            Add("extra", dir, "Manrope-ExtraBold.ttf", FontStyle.Regular, "Manrope ExtraBold", "Segoe UI Black");
            Add("display", dir, "Unbounded-Bold.ttf", FontStyle.Bold, "Unbounded", "Segoe UI");
            Add("displayLight", dir, "Unbounded-Regular.ttf", FontStyle.Regular, "Unbounded", "Segoe UI Light");
            Add("mono", dir, "JetBrainsMono-Regular.ttf", FontStyle.Regular, "JetBrains Mono", "Consolas");
            Add("monoBold", dir, "JetBrainsMono-Bold.ttf", FontStyle.Bold, "JetBrains Mono", "Consolas");
        }

        /// <summary>Проверка, что шрифт действительно создаётся и измеряется.</summary>
        private static bool Usable(FontFamily fam, FontStyle style)
        {
            try
            {
                if (!fam.IsStyleAvailable(style)) return false;
                using (var f = new Font(fam, 10f, style, GraphicsUnit.Point))
                    return f.GetHeight() > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryFamily(string key, FontFamily fam, FontStyle wanted)
        {
            foreach (FontStyle st in new[] { wanted, FontStyle.Regular, FontStyle.Bold })
            {
                if (!Usable(fam, st)) continue;
                faces[key] = new Face { Family = fam, Style = st };
                return true;
            }
            return false;
        }

        /// <summary>Порядок поиска: файл из папки Fonts → установленный в системе шрифт с тем же именем → системный запасной.</summary>
        private static void Add(string key, string dir, string file, FontStyle style, params string[] names)
        {
            try
            {
                string path = Path.Combine(dir, file);
                if (File.Exists(path))
                {
                    // Отдельная коллекция на файл: семейство однозначно соответствует файлу.
                    var pfc = new PrivateFontCollection();
                    pfc.AddFontFile(path);
                    if (pfc.Families.Length > 0 && TryFamily(key, pfc.Families[0], style))
                    {
                        keepAlive.Add(pfc);
                        Native.RegisterFont(path);
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // переходим к поиску по имени
            }
            foreach (string name in names)
            {
                try
                {
                    var fam = new FontFamily(name);
                    if (string.Equals(fam.Name, name, StringComparison.OrdinalIgnoreCase) && TryFamily(key, fam, style)) return;
                }
                catch (Exception)
                {
                    // такого шрифта в системе нет
                }
            }
            faces[key] = new Face { Family = FontFamily.GenericSansSerif, Style = FontStyle.Regular };
        }

        public static Font Get(string face, float size)
        {
            Load();
            string key = face + "|" + size.ToString("0.##");
            Font f;
            if (cache.TryGetValue(key, out f)) return f;
            Face fc;
            if (!faces.TryGetValue(face, out fc)) fc = faces["body"];
            try { f = new Font(fc.Family, size, fc.Style, GraphicsUnit.Point); }
            catch (Exception) { f = new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Point); }
            cache[key] = f;
            return f;
        }

        public static Font Body(float size) { return Get("body", size); }
        public static Font Medium(float size) { return Get("medium", size); }
        public static Font Semi(float size) { return Get("semi", size); }
        public static Font Bold(float size) { return Get("bold", size); }
        public static Font Extra(float size) { return Get("extra", size); }
        public static Font Display(float size) { return Get("display", size); }
        public static Font DisplayLight(float size) { return Get("displayLight", size); }
        public static Font Mono(float size) { return Get("mono", size); }
        public static Font MonoBold(float size) { return Get("monoBold", size); }
    }
}
