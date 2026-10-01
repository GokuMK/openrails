// SPIKE(linux): SkiaSharp replacement for the GDI/TextRenderer glyph code in WindowText.
// It provides exactly what CharacterGroup needs: line height, GDI-style ABC widths,
// and white-on-black glyph rasterization at pen positions.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Orts.Viewer3D.Popups
{
    sealed class GlyphRasterizer
    {
        readonly object Lock = new object();
        readonly string Family;
        readonly SKFontStyle Style;
        readonly SKFont Font;
        readonly Dictionary<char, SKFont> FallbackFonts = new Dictionary<char, SKFont>();

        public readonly int LineHeight;
        public readonly float Ascent;

        public GlyphRasterizer(string family, int sizeInPixels, System.Drawing.FontStyle style)
        {
            Family = family;
            var bold = (style & System.Drawing.FontStyle.Bold) != 0;
            var italic = (style & System.Drawing.FontStyle.Italic) != 0;
            Style = new SKFontStyle(bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            Font = CreateFont(SKTypeface.FromFamilyName(family, Style) ?? SKTypeface.Default, sizeInPixels);
            Ascent = -Font.Metrics.Ascent;
            LineHeight = (int)Math.Ceiling(Font.Spacing);
        }

        static SKFont CreateFont(SKTypeface typeface, float size)
        {
            return new SKFont(typeface, size)
            {
                Edging = SKFontEdging.Antialias,
                Hinting = SKFontHinting.Normal,
                Subpixel = false,
            };
        }

        SKFont FontFor(char character)
        {
            if (Font.Typeface.ContainsGlyph(character))
                return Font;
            if (!FallbackFonts.TryGetValue(character, out var font))
            {
                var typeface = SKFontManager.Default.MatchCharacter(Family, Style, null, character);
                font = typeface == null ? Font : CreateFont(typeface, Font.Size);
                FallbackFonts.Add(character, font);
            }
            return font;
        }

        /// <summary>
        /// Returns GDI-compatible ABC widths: A = left bearing, B = ink width, C = right bearing.
        /// </summary>
        public Vector3 GetAbcWidths(char character)
        {
            lock (Lock)
            {
                var font = FontFor(character);
                var advance = font.MeasureText(character.ToString(), out SKRect bounds);
                if (bounds.IsEmpty)
                    return new Vector3(0, advance, 0);
                return new Vector3(bounds.Left, bounds.Width, advance - bounds.Right);
            }
        }

        /// <summary>
        /// Renders characters in white on black; each pen origin is the top-left of its cell.
        /// Returns 4 bytes per pixel with equal colour channels.
        /// </summary>
        public byte[] Render(int width, int height, char[] characters, Point[] origins)
        {
            lock (Lock)
            {
                using (var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)))
                using (var canvas = new SKCanvas(bitmap))
                using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
                {
                    canvas.Clear(SKColors.Black);
                    for (var i = 0; i < characters.Length; i++)
                    {
                        if (char.IsControl(characters[i]))
                            continue;
                        canvas.DrawText(characters[i].ToString(), origins[i].X, origins[i].Y + Ascent, FontFor(characters[i]), paint);
                    }
                    canvas.Flush();
                    return bitmap.Bytes;
                }
            }
        }
    }
}
