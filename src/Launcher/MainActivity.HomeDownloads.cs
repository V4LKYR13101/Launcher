using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.UI;

namespace LauncherApp;

public partial class MainActivity
{
    private FrameLayout? homeDownloadRegion;
    private LinearLayout? homeDownloadList;
    private View? homeFavoritesDock;
    private readonly List<HomeDownloadCard> homeDownloadCards = [];

    private sealed record HomeDownloadCard(long Id, LinearLayout Card, TextView Name, TextView ProgressText, ProgressBar Progress);

    private void AddHomeDownloads()
    {
        if (homeDownloadRegion?.Parent is ViewGroup previous)
            previous.RemoveView(homeDownloadRegion);
        homeDownloadCards.Clear();
        homeDownloadList = Stack();
        var viewport = new ScrollView(this)
        {
            OverScrollMode = OverScrollMode.Never,
            VerticalScrollBarEnabled = false
        };
        viewport.AddView(homeDownloadList, new ScrollView.LayoutParams(-1, -2));

        homeDownloadRegion = new FrameLayout(this) { Visibility = ViewStates.Gone };
        homeDownloadRegion.AddView(viewport, new FrameLayout.LayoutParams(-1, -2, GravityFlags.CenterVertical));
        int width = Math.Min(272, Math.Max(120, (int)(Resources!.Configuration!.ScreenWidthDp * .66f)));
        scene.AddView(homeDownloadRegion, new FrameLayout.LayoutParams(Dp(width), 0, GravityFlags.Top | GravityFlags.CenterHorizontal));
        UpdateHomeDownloads();
    }

    private void UpdateHomeDownloads()
    {
        if (page != "Tiles" || searching || homeDownloadRegion?.Parent != scene || homeDownloadList is null)
            return;
        var transfers = ActiveDownloads.Snapshot();
        bool visible = transfers.Length != 0;
        if (homeBattery is { } battery)
        {
            var batteryVisibility = visible ? ViewStates.Gone : ViewStates.Visible;
            if (battery.Visibility != batteryVisibility)
            {
                battery.Visibility = batteryVisibility;
                if (!visible)
                    battery.Post(PositionHomeBattery);
            }
        }

        if (!transfers.Select(transfer => transfer.Id).SequenceEqual(homeDownloadCards.Select(card => card.Id)))
        {
            homeDownloadList.RemoveAllViews();
            homeDownloadCards.Clear();
            for (int i = 0; i < transfers.Length; i++)
            {
                var card = MakeHomeDownloadCard(transfers[i].Id);
                homeDownloadCards.Add(card);
                homeDownloadList.AddView(card.Card, new LinearLayout.LayoutParams(-1, -2)
                {
                    BottomMargin = i + 1 < transfers.Length ? Dp(8) : 0
                });
            }
        }

        for (int i = 0; i < transfers.Length; i++)
        {
            var transfer = transfers[i];
            var card = homeDownloadCards[i];
            string value = transfer.Percent is { } percent ? $"{percent}%" :
                transfer.Bytes > 0 ? StorageSize(transfer.Bytes) : "…";
            bool nameChanged = card.Name.Text != transfer.Name;
            if (nameChanged)
                card.Name.Text = transfer.Name;
            if (nameChanged || card.ProgressText.Text != value)
            {
                card.ProgressText.Text = value;
                card.Card.ContentDescription = transfer.Percent is { } amount
                    ? $"{transfer.Name}, {amount}% downloaded"
                    : $"{transfer.Name}, downloading, {value}";
            }
            if (card.Progress.Indeterminate != (transfer.Percent is null))
                card.Progress.Indeterminate = transfer.Percent is null;
            if (transfer.Percent is { } progress && card.Progress.Progress != progress)
                card.Progress.Progress = progress;
        }

        homeDownloadRegion.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
        if (visible)
            PositionHomeDownloads();
    }

    private HomeDownloadCard MakeHomeDownloadCard(long id)
    {
        var panel = Stack();
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && liquidGlass is { } glass)
            glass.AddRecycledPanel(panel, 18);
        else
            panel.Background = Background(palette.Card, 18, palette.Divider);
        panel.SetPaddingRelative(Dp(13), Dp(10), Dp(13), Dp(11));
        panel.ImportantForAccessibility = ImportantForAccessibility.Yes;

        var header = Stack(true);
        header.SetGravity(GravityFlags.CenterVertical);
        var icon = new GlyphView(this, "download", palette.Accent) { ImportantForAccessibility = ImportantForAccessibility.No };
        header.AddView(icon, new LinearLayout.LayoutParams(Dp(16), Dp(16)) { MarginEnd = Dp(7) });
        var name = Text("", 12, true);
        name.SetSingleLine(true);
        name.Ellipsize = Android.Text.TextUtils.TruncateAt.Middle;
        name.ImportantForAccessibility = ImportantForAccessibility.No;
        header.AddView(name, new LinearLayout.LayoutParams(0, -2, 1));
        var value = Text("", 11, true, palette.Accent);
        value.SetSingleLine(true);
        value.ImportantForAccessibility = ImportantForAccessibility.No;
        header.AddView(value, new LinearLayout.LayoutParams(-2, -2) { MarginStart = Dp(8) });
        panel.AddView(header, Match());

        var progress = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 100,
            ProgressTintList = ColorStateList.ValueOf(palette.Accent),
            IndeterminateTintList = ColorStateList.ValueOf(palette.Accent),
            ProgressBackgroundTintList = ColorStateList.ValueOf(palette.Divider),
            ImportantForAccessibility = ImportantForAccessibility.No
        };
        panel.AddView(progress, new LinearLayout.LayoutParams(-1, Dp(5)) { TopMargin = Dp(9) });
        return new(id, panel, name, value, progress);
    }

    private void PositionHomeDownloads()
    {
        if (page != "Tiles" || searching || homeDownloadRegion is not { Visibility: ViewStates.Visible } region || region.Parent != scene)
            return;
        var lower = homeFavoritesDock is { IsAttachedToWindow: true, Height: > 0 } dock ? dock : homeDrawerMarker;
        if (lower is not { IsAttachedToWindow: true, Height: > 0 })
            return;
        var upper = statusNotificationText is { IsShown: true, Height: > 0 } marker ? marker : statusDate;
        using var lowerBounds = new Rect(0, 0, lower.Width, lower.Height);
        scene.OffsetDescendantRectToMyCoords(lower, lowerBounds);
        int top = scene.Height / 2;
        if (upper is { IsAttachedToWindow: true, Height: > 0 })
        {
            using var upperBounds = new Rect(0, 0, upper.Width, upper.Height);
            scene.OffsetDescendantRectToMyCoords(upper, upperBounds);
            top = upperBounds.Bottom;
        }
        int inset = Dp(8);
        int height = Math.Max(0, lowerBounds.Top - top - inset * 2);
        var layout = (FrameLayout.LayoutParams)region.LayoutParameters!;
        if (layout.TopMargin == top + inset && layout.Height == height)
            return;
        layout.TopMargin = top + inset;
        layout.Height = height;
        region.LayoutParameters = layout;
    }
}
