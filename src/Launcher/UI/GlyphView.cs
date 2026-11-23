using Android.Content;
using Android.Graphics;
using Android.Views;
using LauncherApp.Services;

namespace LauncherApp.UI;

// Launcher control glyphs share a 24-unit grid; app icons come from Android.
public sealed class GlyphView(Context context, string glyph, Color color) : View(context)
{
    private readonly FontIconDrawable? outline = LoadOutline(context, glyph, color);

    private static FontIconDrawable? LoadOutline(Context context, string glyph, Color color)
    {
        string? name = glyph switch
        {
            "settings" => "gear-six",
            "filter" => "funnel-simple",
            "search" => "magnifying-glass",
            "close" => "x",
            _ => null
        };
        if (name is null || BuiltinIcons.Get(context, "builtin:phosphor-light", name) is not { } icon)
            return null;
        icon.SetInk(color);
        icon.SetBounds(1, 1, 23, 23);
        return icon;
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float size = Math.Min(Width, Height) * .82f;
        canvas.Save();
        canvas.Translate((Width - size) / 2, (Height - size) / 2);
        canvas.Scale(size / 24, size / 24);
        if (outline is not null)
        {
            outline.Draw(canvas);
            canvas.Restore();
            return;
        }
        using var p = new Paint(PaintFlags.AntiAlias) { Color = color, StrokeWidth = 1.15f };
        p.SetStyle(Paint.Style.Stroke);
        p.StrokeCap = Paint.Cap.Round;
        p.StrokeJoin = Paint.Join.Round;
        void L(float x, float y, float a, float b) => canvas.DrawLine(x, y, a, b, p);
        void C(float x, float y, float r) => canvas.DrawCircle(x, y, r, p);
        void R(float x, float y, float w, float h, float r = 2) => canvas.DrawRoundRect(x, y, x + w, y + h, r, r, p);
        void Path(params float[] xy)
        {
            using var path = new Android.Graphics.Path();
            path.MoveTo(xy[0], xy[1]);
            for (int i = 2; i < xy.Length; i += 2)
                path.LineTo(xy[i], xy[i + 1]);
            canvas.DrawPath(path, p);
        }
        void Cloud()
        {
            using var path = new Android.Graphics.Path();
            path.MoveTo(5, 18);
            path.CubicTo(-1, 18, 0, 10, 6, 10);
            path.CubicTo(7, 2, 18, 3, 19, 10);
            path.CubicTo(25, 10, 24, 18, 19, 18);
            path.Close();
            canvas.DrawPath(path, p);
        }
        switch (glyph)
        {
            case "close":
                L(6, 6, 18, 18);
                L(18, 6, 6, 18);
                break;
            case "battery":
                R(2, 7, 18, 10, 2);
                L(22, 10, 22, 14);
                R(5, 10, 10, 4, 1);
                break;
            case "resources":
                C(12, 12, 8);
                L(12, 4, 12, 12);
                L(12, 12, 19, 16);
                break;
            case "refresh":
                canvas.DrawArc(4, 4, 20, 20, 40, 285, false, p);
                Path(13, 3, 19, 5, 17, 11);
                break;
            case "rss":
                C(5, 19, 1);
                canvas.DrawArc(-3, 11, 13, 27, -90, 90, false, p);
                canvas.DrawArc(-11, 3, 21, 35, -90, 90, false, p);
                break;
            case "lock":
                R(5, 10, 14, 11, 2);
                using (var shackle = new Android.Graphics.Path())
                {
                    shackle.MoveTo(8, 10);
                    shackle.LineTo(8, 7);
                    shackle.CubicTo(8, 1, 16, 1, 16, 7);
                    shackle.LineTo(16, 10);
                    canvas.DrawPath(shackle, p);
                }
                C(12, 15, 1);
                L(12, 16, 12, 18);
                break;
            case "arrow":
                L(12, 4, 12, 20);
                Path(7, 9, 12, 4, 17, 9);
                break;
            case "filter":
                L(3, 6, 21, 6);
                L(6, 12, 18, 12);
                L(9, 18, 15, 18);
                break;
            case "previous":
                L(4, 5, 4, 19);
                Path(20, 5, 8, 12, 20, 19, 20, 5);
                break;
            case "next":
                L(20, 5, 20, 19);
                Path(4, 5, 16, 12, 4, 19, 4, 5);
                break;
            case "repeat":
                Path(4, 8, 18, 8, 18, 3, 22, 8, 18, 12);
                Path(20, 16, 6, 16, 6, 21, 2, 16, 6, 12);
                break;
            case "shuffle":
                Path(3, 5, 7, 5, 17, 19, 22, 19);
                Path(3, 19, 7, 19, 17, 5, 22, 5);
                Path(18, 2, 22, 5, 18, 8);
                Path(18, 16, 22, 19, 18, 22);
                break;
            case "like":
                Path(9, 10, 13, 3, 15, 3, 15, 10, 21, 10, 19, 21, 9, 21, 9, 10);
                R(3, 10, 5, 11, 1);
                break;
            case "rectangle":
                R(3, 7, 18, 10, 0);
                break;
            case "info":
                C(12, 12, 8);
                L(12, 11, 12, 17);
                C(12, 7, 0.6f);
                break;
            case "portrait":
                R(7, 3, 10, 18, 0);
                break;
            case "triangle-down":
                Path(4, 6, 20, 6, 12, 19, 4, 6);
                break;
            case "diamond":
                Path(3, 12, 12, 5, 21, 12, 12, 19, 3, 12);
                break;
            case "vertical":
                R(10, 4, 4, 16, 1);
                break;
            case "dash":
                R(4, 10, 16, 4, 0);
                break;
            case "triangle":
                Path(5, 4, 20, 19, 5, 19, 5, 4);
                break;
            case "circle":
                C(12, 12, 6);
                break;
            case "pause":
                L(9, 7, 9, 17);
                L(15, 7, 15, 17);
                break;
            case "equals":
                L(6, 9, 18, 9);
                L(6, 15, 18, 15);
                break;
            case "double-dot":
                C(9, 12, 1);
                C(15, 12, 1);
                break;
            case "ring":
                C(12, 12, 8);
                break;
            case "small-circle":
                C(12, 12, 2);
                break;
            case "link":
                R(3, 7, 11, 10, 4);
                R(10, 4, 11, 10, 4);
                break;
            case "zigzag":
                Path(3, 16, 8, 8, 13, 16, 18, 8, 22, 11);
                break;
            case "wave":
                using (var path = new Android.Graphics.Path())
                {
                    path.MoveTo(2, 14);
                    path.CubicTo(8, 3, 14, 23, 22, 10);
                    canvas.DrawPath(path, p);
                }
                break;
            case "star":
                var points = new float[22];
                for (int i = 0; i <= 10; i++)
                {
                    float a = -MathF.PI / 2 + i * MathF.PI / 5;
                    float r = i % 2 == 0 ? 9 : 4;
                    points[i * 2] = 12 + MathF.Cos(a) * r;
                    points[i * 2 + 1] = 12 + MathF.Sin(a) * r;
                }
                Path(points);
                break;
            case "cloud":
                Cloud();
                break;
            case "rain":
                Cloud();
                L(7, 20, 6, 23);
                L(13, 20, 12, 23);
                L(19, 20, 18, 23);
                break;
            case "storm":
                Cloud();
                Path(13, 13, 10, 19, 14, 19, 11, 24);
                break;
            case "wifi":
                p.StrokeWidth = 1.6f;
                canvas.DrawArc(-1, 2, 25, 28, 225, 90, false, p);
                canvas.DrawArc(3, 7, 21, 25, 225, 90, false, p);
                canvas.DrawArc(7, 12, 17, 22, 225, 90, false, p);
                p.SetStyle(Paint.Style.Fill);
                C(12, 19, 1.25f);
                break;
            case "bluetooth":
                p.StrokeWidth = 1.6f;
                Path(12, 2, 18, 7, 6, 17);
                Path(12, 22, 18, 17, 6, 7);
                L(12, 2, 12, 22);
                break;
            case "sound":
                Path(3, 9, 7, 9, 13, 4, 13, 20, 7, 15, 3, 15, 3, 9);
                canvas.DrawArc(8, 5, 22, 19, -60, 120, false, p);
                break;
            case "phone":
                Path(4, 3, 8, 3, 9, 8, 7, 10, 14, 17, 16, 15, 21, 16, 21, 20, 18, 21, 12, 18, 6, 12, 3, 6, 4, 3);
                break;
            case "message":
                R(3, 4, 18, 13, 4);
                Path(7, 17, 7, 21, 12, 17);
                break;
            case "mail":
                R(2, 5, 20, 14);
                Path(2, 6, 12, 13, 22, 6);
                break;
            case "camera":
                R(2, 6, 20, 15, 3);
                Path(7, 6, 9, 3, 15, 3, 17, 6);
                C(12, 13, 4);
                break;
            case "browser":
                C(12, 12, 10);
                C(12, 12, 4);
                L(12, 8, 21, 8);
                L(8.5f, 14, 4, 6);
                L(14, 15.5f, 9, 22);
                break;
            case "clock":
                C(12, 12, 10);
                Path(12, 5, 12, 12, 17, 15);
                break;
            case "calendar":
                R(3, 5, 18, 17);
                L(3, 10, 21, 10);
                L(7, 2, 7, 7);
                L(17, 2, 17, 7);
                L(8, 15, 10, 15);
                L(14, 15, 16, 15);
                break;
            case "settings":
                C(12, 12, 4);
                C(12, 12, 8);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4;
                    L(12 + 8 * MathF.Cos(a), 12 + 8 * MathF.Sin(a), 12 + 11 * MathF.Cos(a), 12 + 11 * MathF.Sin(a));
                }
                break;
            case "photo":
                R(2, 3, 20, 18);
                C(8, 8, 2);
                Path(2, 18, 9, 12, 13, 16, 17, 10, 22, 16);
                break;
            case "play":
                Path(8, 3, 21, 12, 8, 21, 8, 3);
                break;
            case "music":
                Path(9, 17, 9, 5, 21, 3, 21, 15);
                L(9, 9, 21, 7);
                C(6, 18, 3);
                C(18, 16, 3);
                break;
            case "map":
                Path(3, 21, 3, 5, 9, 2, 15, 5, 21, 2, 21, 19, 15, 22, 9, 19, 3, 21);
                L(9, 2, 9, 19);
                L(15, 5, 15, 22);
                break;
            case "person":
                C(12, 7, 4);
                canvas.DrawArc(3, 13, 21, 29, 180, 180, false, p);
                L(3, 21, 21, 21);
                break;
            case "file":
                Path(5, 2, 14, 2, 20, 8, 20, 22, 5, 22, 5, 2);
                Path(14, 2, 14, 8, 20, 8);
                L(8, 13, 17, 13);
                L(8, 17, 15, 17);
                break;
            case "download":
                L(12, 3, 12, 16);
                Path(7, 11, 12, 16, 17, 11);
                Path(4, 17, 4, 21, 20, 21, 20, 17);
                break;
            case "calculator":
                R(4, 2, 16, 20);
                R(7, 5, 10, 4, 1);
                L(8, 13, 10, 13);
                L(14, 13, 16, 13);
                L(9, 16, 9, 19);
                L(7.5f, 17.5f, 10.5f, 17.5f);
                L(14, 17, 16, 17);
                L(14, 19, 16, 19);
                break;
            case "search":
                C(10, 10, 7);
                L(15, 15, 22, 22);
                break;
            case "grid":
                R(3, 3, 7, 7, 1);
                R(14, 3, 7, 7, 1);
                R(3, 14, 7, 7, 1);
                R(14, 14, 7, 7, 1);
                break;
            case "root":
                Path(6, 3, 6, 9, 12, 14, 18, 9, 18, 3);
                L(12, 14, 12, 22);
                L(5, 20, 19, 20);
                break;
            case "sun":
                C(12, 12, 5);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4;
                    L(12 + 8 * MathF.Cos(a), 12 + 8 * MathF.Sin(a), 12 + 11 * MathF.Cos(a), 12 + 11 * MathF.Sin(a));
                }
                break;
            case "focus":
                C(12, 12, 10);
                L(9, 7, 9, 17);
                L(15, 7, 15, 17);
                break;
            case "plus":
                L(12, 4, 12, 20);
                L(4, 12, 20, 12);
                break;
            case "back":
                Path(14, 4, 6, 12, 14, 20);
                L(6, 12, 22, 12);
                break;
            case "down":
                Path(5, 9, 12, 16, 19, 9);
                break;
            case "up":
                Path(5, 15, 12, 8, 19, 15);
                break;
            case "chevron":
                if (LayoutDirection == Android.Views.LayoutDirection.Rtl)
                    Path(15, 5, 8, 12, 15, 19);
                else
                    Path(9, 5, 16, 12, 9, 19);
                break;
            case "more":
                C(5, 12, 1);
                C(12, 12, 1);
                C(19, 12, 1);
                break;
            case "grip":
                p.SetStyle(Paint.Style.Fill);
                for (int y = 6; y <= 18; y += 6)
                {
                    C(9, y, 1.5f);
                    C(15, y, 1.5f);
                }
                break;
            case "pin":
                Path(8, 2, 16, 2, 15, 10, 19, 14, 5, 14, 9, 10, 8, 2);
                L(12, 14, 12, 22);
                break;
            case "check":
                Path(3, 12, 9, 18, 21, 5);
                break;
            case "finance":
                R(2, 5, 20, 15);
                R(14, 10, 8, 6, 1);
                C(17, 13, 1);
                break;
            case "social":
                C(6, 6, 3);
                C(18, 4, 3);
                C(18, 19, 3);
                L(8, 7, 16, 5);
                L(8, 8, 16, 17);
                break;
            case "leaf":
                Path(3, 21, 6, 8, 21, 2, 18, 17, 3, 21);
                L(3, 21, 15, 9);
                break;
            case "send":
                Path(2, 3, 22, 12, 2, 21, 6, 12, 2, 3);
                L(6, 12, 22, 12);
                break;
            case "shop":
                R(3, 7, 18, 15);
                canvas.DrawArc(7, 0, 17, 14, 180, 180, false, p);
                break;
            case "note":
                R(3, 2, 18, 20);
                L(7, 7, 17, 7);
                L(7, 12, 17, 12);
                L(7, 17, 13, 17);
                break;
            default:
                R(3, 3, 18, 18, 5);
                C(12, 12, 4);
                break;
        }
        canvas.Restore();
    }

    public static string ForApp(string label, string package)
    {
        var s = (label + " " + package).ToLowerInvariant();
        bool Has(params string[] values) => values.Any(t => System.Text.RegularExpressions.Regex.IsMatch(s,
            @"(?<![a-z])" + System.Text.RegularExpressions.Regex.Escape(t) + @"(?![a-z])"));
        if (Has("camera"))
            return "camera";
        if (Has("contact", "contacts"))
            return "person";
        if (Has("phone", "dialer"))
            return "phone";
        if (Has("mail", "outlook"))
            return "mail";
        if (Has("telegram"))
            return "send";
        if (Has("message", "messages", "messaging", "whatsapp", "signal", "chat", "w4b"))
            return "message";
        if (Has("chrome", "browser", "edge", "firefox"))
            return "browser";
        if (Has("setting", "settings"))
            return "settings";
        if (Has("calendar"))
            return "calendar";
        if (Has("clock"))
            return "clock";
        if (Has("calculator"))
            return "calculator";
        if (Has("photo", "photos", "gallery", "instagram"))
            return "photo";
        if (Has("music", "spotify", "podcast"))
            return "music";
        if (Has("youtube", "video", "netflix"))
            return "play";
        if (Has("map", "maps", "travel", "uber"))
            return "map";
        if (Has("file", "files", "drive", "document", "documents"))
            return "file";
        if (Has("note", "notes", "todo"))
            return "note";
        if (Has("bank", "wallet", "paypal"))
            return "finance";
        if (Has("shop", "store", "amazon"))
            return "shop";
        if (Has("reddit", "facebook", "twitter"))
            return "social";
        return "app";
    }
}
