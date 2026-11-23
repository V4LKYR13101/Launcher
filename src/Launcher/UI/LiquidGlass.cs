using System.Runtime.Versioning;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;

namespace LauncherApp.UI;

[SupportedOSPlatform("android33.0")]
internal sealed class LiquidGlass : Drawable
{
    private const string Refraction = """
        uniform shader backdrop;
        uniform float4 panel;
        uniform float dp;
        uniform float tint;
        uniform float cornerRadius;
        layout(color) uniform half4 surface;

        half4 main(float2 pixel) {
            float2 center = panel.xy + panel.zw * 0.5;
            float2 p = pixel - center;
            float radius = min(max(cornerRadius, 0.01), min(panel.z, panel.w) * 0.5);
            float2 corner = max(abs(p) - (panel.zw * 0.5 - radius), float2(0.0));
            float lengthToCorner = length(corner);
            float edge = max(0.0, radius - lengthToCorner);
            float2 normal = sign(p) * corner / max(lengthToCorner, 0.01);
            float lens = pow(1.0 - smoothstep(0.0, 20.0 * dp, edge), 1.45);
            float2 bent = pixel - normal * (24.0 * dp * lens) - p * 0.035;
            float2 prism = normal * (3.5 * dp * lens);
            float soft = 2.8 * dp + lens * dp;
            half3 softColor = backdrop.eval(bent + float2(soft, soft)).rgb
                            + backdrop.eval(bent + float2(-soft, soft)).rgb
                            + backdrop.eval(bent + float2(soft, -soft)).rgb
                            + backdrop.eval(bent - float2(soft, soft)).rgb;
            half3 splitColor = half3(backdrop.eval(bent - prism).r,
                                     backdrop.eval(bent).g,
                                     backdrop.eval(bent + prism).b);
            half3 glass = mix(splitColor, softColor * 0.25, 0.36);
            glass = mix(glass, surface.rgb, tint);
            float facing = dot(normal, float2(-0.55, -0.835));
            float rim = 1.0 - smoothstep(0.0, 2.5 * dp, edge);
            float sheen = pow(max(0.0, facing), 2.0);
            float bounce = pow(max(0.0, -facing), 4.0);
            glass = mix(glass, half3(1.0), rim * (0.42 * sheen + 0.16 * bounce) + lens * 0.055 * sheen);
            glass *= 1.0 - lens * 0.10 * max(0.0, -facing);
            return half4(glass, 1.0);
        }
        """;

    private readonly View owner;
    private readonly Color baseColor;
    private readonly Drawable? backdrop;
    private readonly bool light;
    private readonly float density;
    private readonly List<Panel> panels = [];
    private readonly Rect visible = new();
    private readonly Point anchor = new(), location = new();
    private readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
    private readonly RuntimeShader? lens;
    private readonly FrameTracker tracker;
    private ViewTreeObserver? observer;
    private Bitmap? texture, wallpaper;
    private BitmapShader? sample;
    private int textureWidth, textureHeight;
    private int wallpaperDim;
    private bool wallpaperVisible;
    private int sampledGlowAlpha, sampledGlowColor;
    private int alpha;

    public LiquidGlass(View owner, Palette palette, Drawable? backdrop = null)
    {
        this.owner = owner;
        baseColor = palette.Base;
        this.backdrop = backdrop;
        light = palette.Light;
        alpha = 255;
        density = owner.Resources!.DisplayMetrics!.Density;
        tracker = new FrameTracker(this);
        try
        {
            lens = new RuntimeShader(Refraction);
            lens.SetFloatUniform("dp", density);
        }
        catch (Java.Lang.IllegalArgumentException e)
        {
            Android.Util.Log.Warn("LauncherGlass", e.Message ?? "Could not compile the glass shader.");
            lens?.Dispose();
            lens = null;
        }
        owner.ViewAttachedToWindow += (_, _) => StartTracking();
        owner.ViewDetachedFromWindow += (_, _) => StopTracking();
        if (owner.IsAttachedToWindow)
            StartTracking();
    }

    public void SetWallpaper(Bitmap? image, int dim, bool visible)
    {
        if (ReferenceEquals(wallpaper, image) && wallpaperDim == dim && wallpaperVisible == visible)
            return;
        wallpaper = image;
        wallpaperDim = dim;
        wallpaperVisible = visible;
        textureWidth = 0;
        InvalidateSelf();
    }

    public void AddPanel(View view, float cornerDp = 22, Color? surface = null, NotificationGroup.StackDepth? stack = null,
        bool persistent = false) => panels.Add(new Panel(view, cornerDp * density, surface, stack, persistent));

    public void AddRecycledPanel(View view, float cornerDp)
    {
        view.ViewAttachedToWindow += (_, _) => AddPanel(view, cornerDp);
        view.ViewDetachedFromWindow += (_, _) =>
        {
            if (panels.RemoveAll(panel => ReferenceEquals(panel.View, view)) > 0)
                InvalidateSelf();
        };
    }

    public void ClearPanels(bool keepPersistent = false)
    {
        if (keepPersistent)
            panels.RemoveAll(panel => !panel.Persistent);
        else
            panels.Clear();
        InvalidateSelf();
    }

    private void StartTracking()
    {
        observer = owner.ViewTreeObserver;
        if (observer?.IsAlive == true)
            observer.AddOnPreDrawListener(tracker);
    }

    private void StopTracking()
    {
        if (observer?.IsAlive == true)
            observer.RemoveOnPreDrawListener(tracker);
        observer = null;
    }

    private void TrackPanels()
    {
        if (panels.Count == 0 || !owner.GetGlobalVisibleRect(visible, anchor))
            return;
        bool changed = false;
        foreach (var panel in panels)
        {
            if (!panel.View.IsShown || !panel.View.GetGlobalVisibleRect(visible, location))
            {
                changed |= panel.Shown;
                panel.Shown = false;
                continue;
            }
            visible.Offset(-anchor.X, -anchor.Y);
            int x = location.X - anchor.X, y = location.Y - anchor.Y;
            int width = panel.View.Width, height = panel.View.Height;
            float reveal = panel.Stack?.Reveal ?? 0;
            if (panel.Shown && panel.X == x && panel.Y == y && panel.Width == width
                && panel.Height == height && panel.Reveal == reveal && panel.Clip.Equals(visible))
                continue;
            panel.Shown = true;
            panel.X = x;
            panel.Y = y;
            panel.Width = width;
            panel.Height = height;
            panel.Reveal = reveal;
            panel.Clip.Set(visible);
            changed = true;
        }
        if (changed || GlowChanged())
            InvalidateSelf();
    }

    private bool GlowChanged() => backdrop is AccentGlowDrawable glow
        && (sampledGlowAlpha != glow.GlowAlpha || sampledGlowColor != glow.Accent.ToArgb());

    public override void Draw(Canvas canvas)
    {
        int width = Bounds.Width(), height = Bounds.Height();
        if (width <= 0 || height <= 0 || !panels.Any(panel => panel.Shown))
            return;
        if (texture is null || width != textureWidth || height != textureHeight || GlowChanged())
            BuildTexture(width, height);
        foreach (var panel in panels)
        {
            if (!panel.Shown)
                continue;
            int save = canvas.Save();
            canvas.ClipRect(panel.Clip);
            float frontHeight = panel.Height;
            Color surface = panel.Surface ?? baseColor;
            float tint = light ? .70f : .58f;
            if (panel.Reveal > 0 && panel.Stack is { } stack)
            {
                frontHeight -= stack.Step * stack.Layers * panel.Reveal;
                Color backing = AccentGlowDrawable.Blend(surface, light ? Color.White : new Color(111, 134, 158), .19f);
                for (int layer = stack.Layers; layer >= 1; layer--)
                {
                    float inset = (2 + 4 * layer) * density;
                    float offset = stack.Step * layer * panel.Reveal;
                    DrawPanel(canvas, panel.X + inset, panel.Y + offset, panel.Width - 2 * inset, frontHeight,
                        panel.Radius, backing, tint);
                }
            }
            DrawPanel(canvas, panel.X, panel.Y, panel.Width, frontHeight, panel.Radius, surface, tint);
            canvas.RestoreToCount(save);
        }
    }

    private void DrawPanel(Canvas canvas, float x, float y, float width, float height, float radius, Color surface, float tint)
    {
        if (width <= 0 || height <= 0)
            return;
        if (canvas.IsHardwareAccelerated && lens is not null)
        {
            lens.SetFloatUniform("panel", x, y, width, height);
            lens.SetFloatUniform("tint", tint);
            lens.SetFloatUniform("cornerRadius", radius);
            lens.SetColorUniform("surface", surface);
            paint.SetShader(lens);
            paint.Alpha = wallpaperVisible && wallpaper is null ? alpha * (120 + wallpaperDim) / 255 : alpha;
        }
        else
        {
            paint.SetShader(null);
            paint.Color = surface;
            paint.Alpha = alpha * 160 / 255;
        }
        canvas.DrawRoundRect(x, y, x + width, y + height, radius, radius, paint);
    }

    private void BuildTexture(int width, int height)
    {
        // Share one lightly downsampled background across all groups on the page.
        float scale = Math.Min(1, 680f / Math.Max(width, height));
        bool resizeSampler = width != textureWidth || height != textureHeight || sample is null;
        textureWidth = width;
        textureHeight = height;
        int bitmapWidth = Math.Max(1, (int)(width * scale));
        int bitmapHeight = Math.Max(1, (int)(height * scale));
        if (texture is null || texture.Width != bitmapWidth || texture.Height != bitmapHeight)
            texture = Bitmap.CreateBitmap(bitmapWidth, bitmapHeight, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(texture);
        using var ink = new Paint(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
        canvas.Scale(texture.Width / (float)width, texture.Height / (float)height);
        canvas.DrawColor(baseColor);
        if (wallpaper is { } image)
        {
            float zoom = Math.Max(width / (float)image.Width, height / (float)image.Height);
            float dx = (image.Width * zoom - width) / 2, dy = (image.Height * zoom - height) / 2;
            using var crop = new RectF(-dx, -dy, width + dx, height + dy);
            canvas.DrawBitmap(image, null, crop, ink);
            canvas.DrawColor(new Color(baseColor.R, baseColor.G, baseColor.B, (byte)(255 * wallpaperDim / 100)));
        }
        if (backdrop is AccentGlowDrawable glow)
        {
            sampledGlowAlpha = glow.GlowAlpha;
            sampledGlowColor = glow.Accent.ToArgb();
        }
        backdrop?.Draw(canvas);
        if (lens is null || !resizeSampler)
            return;
        sample = new BitmapShader(texture, Shader.TileMode.Clamp!, Shader.TileMode.Clamp!)
        {
            FilterMode = (int)BitmapShaderFilterMode.Linear
        };
        using var map = new Matrix();
        map.SetScale(width / (float)texture.Width, height / (float)texture.Height);
        sample.SetLocalMatrix(map);
        lens.SetInputShader("backdrop", sample);

    }

    public override void SetAlpha(int value) { alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? filter) => paint.SetColorFilter(filter);
    public override int Opacity => (int)Format.Translucent;

    private sealed class FrameTracker(LiquidGlass glass) : Java.Lang.Object, ViewTreeObserver.IOnPreDrawListener
    {
        public bool OnPreDraw()
        {
            glass.TrackPanels();
            return true;
        }
    }

    private sealed class Panel(View view, float radius, Color? surface, NotificationGroup.StackDepth? stack, bool persistent)
    {
        public View View { get; } = view;
        public float Radius { get; } = radius;
        public Color? Surface { get; } = surface;
        public NotificationGroup.StackDepth? Stack { get; } = stack;
        public bool Persistent { get; } = persistent;
        public Rect Clip { get; } = new();
        public bool Shown { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Reveal { get; set; }
    }
}
