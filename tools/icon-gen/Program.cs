using SkiaSharp;
using System;
using System.IO;

// Renders cosmic-themed PWA icons:
//   deep-indigo background + gold sun sigil (12 rays + core disc).
// Maskable variants: sigil sized to ~80% of canvas (10% padding each side).
// Standard variants: sigil sized to ~90% of canvas.

internal static class IconGen
{
    private static readonly SKColor BgColor = new SKColor(0x0a, 0x05, 0x18);

    public static void DrawIcon(string path, int size, double sigilFraction)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(BgColor);

        var center = size / 2f;
        // Sigil "diameter" (rays span -100 to +100 in source SVG => total 200 units).
        var sigilDiameter = size * sigilFraction;
        var unitScale = (float)(sigilDiameter / 200.0);

        var coreRadius = 36f * unitScale;
        var rayLength = 100f * unitScale;

        var gradient = SKShader.CreateRadialGradient(
            new SKPoint(center, center),
            rayLength,
            new[]
            {
                new SKColor(0xff, 0xf5, 0xd4),
                new SKColor(0xff, 0xd7, 0x6b),
                new SKColor(0xc0, 0x8a, 0x00)
            },
            new[] { 0f, 0.4f, 1f },
            SKShaderTileMode.Clamp);

        using var goldPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = gradient
        };

        using var bloomPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = gradient,
            ImageFilter = SKImageFilter.CreateBlur(6f * unitScale, 6f * unitScale)
        };

        void DrawRay(SKPaint paint, float angleDeg)
        {
            canvas.Save();
            canvas.Translate(center, center);
            canvas.RotateDegrees(angleDeg);
            using var rayPath = new SKPath();
            rayPath.MoveTo(0, -100f * unitScale);
            rayPath.LineTo(8f * unitScale, -50f * unitScale);
            rayPath.LineTo(-8f * unitScale, -50f * unitScale);
            rayPath.Close();
            canvas.DrawPath(rayPath, paint);
            canvas.Restore();
        }

        // Bloom pass.
        for (int i = 0; i < 12; i++) DrawRay(bloomPaint, i * 30f);
        canvas.DrawCircle(center, center, coreRadius, bloomPaint);

        // Crisp pass.
        for (int i = 0; i < 12; i++) DrawRay(goldPaint, i * 30f);
        canvas.DrawCircle(center, center, coreRadius, goldPaint);

        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        data.SaveTo(fs);
        Console.WriteLine($"Wrote {path} ({size}x{size})");
    }

    public static void Main(string[] args)
    {
        var iconRoot = Path.GetFullPath(args.Length > 0 ? args[0] : "../../web/wwwroot/img/icons");
        var wwwroot = Path.GetFullPath(args.Length > 1 ? args[1] : "../../web/wwwroot");

        // Standard PWA icons: sigil ~90% of canvas.
        DrawIcon(Path.Combine(iconRoot, "icon-192.png"), 192, 0.90);
        DrawIcon(Path.Combine(iconRoot, "icon-512.png"), 512, 0.90);

        // Maskable: sigil ~80% (10% safe-area padding each side).
        DrawIcon(Path.Combine(iconRoot, "icon-maskable-192.png"), 192, 0.80);
        DrawIcon(Path.Combine(iconRoot, "icon-maskable-512.png"), 512, 0.80);

        // Apple touch icon: 180x180, fully opaque (alpha is from bg fill which is opaque).
        DrawIcon(Path.Combine(iconRoot, "apple-touch-icon.png"), 180, 0.90);

        // Favicon: 32x32 PNG-in-ICO.
        var faviconPng = Path.Combine(Path.GetTempPath(), "favicon-32.png");
        DrawIcon(faviconPng, 32, 0.90);

        var pngBytes = File.ReadAllBytes(faviconPng);
        var icoPath = Path.Combine(wwwroot, "favicon.ico");
        using (var fs = File.Create(icoPath))
        using (var bw = new BinaryWriter(fs))
        {
            // ICONDIR
            bw.Write((ushort)0);   // reserved
            bw.Write((ushort)1);   // type 1 = ICO
            bw.Write((ushort)1);   // count

            // ICONDIRENTRY
            bw.Write((byte)32);    // width
            bw.Write((byte)32);    // height
            bw.Write((byte)0);     // palette colors
            bw.Write((byte)0);     // reserved
            bw.Write((ushort)1);   // color planes
            bw.Write((ushort)32);  // bpp
            bw.Write((uint)pngBytes.Length);
            bw.Write((uint)22);    // offset to image data (6 + 16)

            bw.Write(pngBytes);
        }
        File.Delete(faviconPng);
        Console.WriteLine($"Wrote {icoPath} (PNG-in-ICO, {pngBytes.Length} bytes payload)");
    }
}
