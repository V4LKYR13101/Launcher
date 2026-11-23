using Android.Content;
using Android.Views;
using Android.Widget;

namespace LauncherApp;

public partial class MainActivity
{
    private const string TodayEditScrollKey = "Today edit";
    private TodayLayoutEditor? todayLayoutEditor;
    private bool offerTodayLayoutUndo;

    private void BeginTodayLayout()
    {
        if (todayLayoutEditor is not null)
            return;

        offerTodayLayoutUndo = false;
        homeScrollPositions.Remove(TodayEditScrollKey);
        todayLayoutEditor = new TodayLayoutEditor(this);
        Navigate("Root");
    }

    private void FinishTodayLayout(bool save)
    {
        if (todayLayoutEditor is not { } editor)
            return;

        editor.StopDrag();
        todayLayoutEditor = null;
        if (save)
        {
            var previous = TodayOrder();
            var available = previous.ToHashSet();
            var visible = editor.Order.Where(available.Contains).ToHashSet();
            var pinned = new Queue<string>(editor.Order.Where(id => visible.Contains(id) && state.RootPinned.Contains(id)));
            var regular = new Queue<string>(editor.Order.Where(id => visible.Contains(id) && !state.RootPinned.Contains(id)));
            var updated = previous.Select(id => visible.Contains(id)
                ? (state.RootPinned.Contains(id) ? pinned : regular).Dequeue() : id).ToList();
            if (!previous.SequenceEqual(updated))
            {
                RememberLayout();
                state.RootOrder = updated;
                Save();
                offerTodayLayoutUndo = true;
            }
        }
        Render();
    }

    private void AddTodayLayoutControls()
    {
        if (todayLayoutEditor is not null)
        {
            var bar = Stack();
            bar.SetPadding(Dp(16), Dp(8), Dp(16), Dp(4));
            var title = Text("Arrange Today", 22, true);
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
                title.AccessibilityHeading = true;
            bar.AddView(title, Match());
            Gap(bar, 4);
            DialogActions(bar, ("Cancel", () => FinishTodayLayout(false)), ("Done", () => FinishTodayLayout(true)));
            shell.AddView(bar, Match());
        }
        else if (offerTodayLayoutUndo)
        {
            var bar = Stack(true);
            bar.SetPadding(Dp(16), 0, Dp(8), 0);
            bar.SetGravity(GravityFlags.CenterVertical);
            bar.AddView(Text("Today layout saved", 13, color: palette.Muted), new LinearLayout.LayoutParams(0, -2, 1));
            bar.AddView(Button("Undo", UndoLayout), new LinearLayout.LayoutParams(-2, -2));
            bar.AddView(IconButton("close", "Dismiss layout confirmation", () => { offerTodayLayoutUndo = false; bar.Visibility = ViewStates.Gone; }),
                new LinearLayout.LayoutParams(Dp(48), Dp(48)));
            shell.AddView(bar, Match());
        }
    }

    private sealed class TodayLayoutEditor(MainActivity host, string[]? restoredOrder = null) : Java.Lang.Object, View.IOnDragListener
    {
        private sealed record Card(string Id, bool Pinned, LinearLayout View, View Up, View Down);

        private readonly List<Card> cards = [];
        private readonly int[] listLocation = new int[2], scrollLocation = new int[2];
        private LinearLayout rows = null!;
        private Card? dragged;
        private string[]? beforeDrag;
        private bool dropped;
        private float fingerY;
        private Action? autoScroll;
        internal string[] Order { get; private set; } = restoredOrder ?? [];

        internal void Render(Dictionary<string, Action> builders, string[] visible)
        {
            StopDrag();
            var available = visible.ToHashSet();
            var displayOrder = Order.Concat(visible).Distinct().Where(available.Contains)
                .OrderByDescending(host.state.RootPinned.Contains).ToArray();
            cards.Clear();
            rows = host.Stack();
            rows.SetOnDragListener(this);
            host.Paragraph("Hold a grip to drag a card, or use the arrows. Pinned cards stay at the top.");
            host.body.AddView(rows, Match());
            foreach (string id in displayOrder)
                AddCard(id, builders[id]);
            UpdateOrder();
        }

        private void AddCard(string id, Action render)
        {
            var content = host.Stack();
            content.DescendantFocusability = DescendantFocusability.BlockDescendants;
            content.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
            var previousBody = host.body;
            try
            {
                host.body = content;
                render();
            }
            finally
            {
                host.body = previousBody;
            }
            if (content.ChildCount == 0)
                return;

            var preview = new FrameLayout(host);
            preview.AddView(content, new FrameLayout.LayoutParams(-1, -2));
            preview.AddView(new View(host) { Clickable = true, ImportantForAccessibility = ImportantForAccessibility.No },
                new FrameLayout.LayoutParams(-1, -1));

            var view = host.Stack();
            var header = host.Stack(true);
            header.SetGravity(GravityFlags.CenterVertical);
            string name = host.CardLabel(id);
            var grip = host.IconButton("grip", "Hold to drag " + name, () => host.Toast("Hold the grip to drag this card."));
            var title = host.Text(name, 14, true);
            title.SetMaxLines(2);
            title.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            var up = host.IconButton("up", "Move earlier: " + name, () => MoveBy(id, -1));
            var down = host.IconButton("down", "Move later: " + name, () => MoveBy(id, 1));
            var item = new Card(id, host.state.RootPinned.Contains(id), view, up, down);
            grip.LongClick += (_, _) => StartDrag(item, header);
            header.AddView(grip, new LinearLayout.LayoutParams(host.Dp(48), host.Dp(48)));
            header.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
            header.AddView(up, new LinearLayout.LayoutParams(host.Dp(48), host.Dp(48)));
            header.AddView(down, new LinearLayout.LayoutParams(host.Dp(48), host.Dp(48)));
            view.AddView(header, Match());
            view.AddView(preview, Match());
            rows.AddView(view, Match());
            cards.Add(item);
        }

        private void UpdateOrder()
        {
            Order = cards.Select(card => card.Id).ToArray();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.Up.Enabled = CanMove(i, i - 1);
                card.Down.Enabled = CanMove(i, i + 1);
                card.Up.Alpha = card.Up.Enabled ? 1 : .3f;
                card.Down.Alpha = card.Down.Enabled ? 1 : .3f;
            }
        }

        private bool CanMove(int from, int to) => from >= 0 && to >= 0 && to < cards.Count && cards[from].Pinned == cards[to].Pinned;

        private void MoveBy(string id, int direction)
        {
            int from = cards.FindIndex(card => card.Id == id);
            int to = from + direction;
            if (CanMove(from, to))
                Move(from, to);
        }

        private void Move(int from, int to)
        {
            var card = cards[from];
            cards.RemoveAt(from);
            cards.Insert(to, card);
            rows.RemoveViewAt(from);
            rows.AddView(card.View, to);
            UpdateOrder();
        }

        private void StartDrag(Card card, View shadow)
        {
            if (dragged is not null)
                return;
            beforeDrag = Order;
            dragged = card;
            dropped = false;
            using var data = ClipData.NewPlainText("Today card", card.Id);
            using var thumbnail = new View.DragShadowBuilder(shadow);
            if (rows.StartDragAndDrop(data, thumbnail, this, 0))
                card.View.Alpha = .55f;
            else
                EndDrag();
        }

        public bool OnDrag(View? view, DragEvent? e)
        {
            if (e is null || dragged is null || !ReferenceEquals(view, rows) || !ReferenceEquals(e.LocalState, this))
                return false;

            switch (e.Action)
            {
                case DragAction.Location:
                    rows.GetLocationOnScreen(listLocation);
                    float y = e.GetY();
                    fingerY = listLocation[1] + y;
                    MoveToFinger(y);
                    UpdateAutoScroll();
                    break;
                case DragAction.Exited:
                    StopAutoScroll();
                    break;
                case DragAction.Drop:
                    dropped = true;
                    StopAutoScroll();
                    break;
                case DragAction.Ended:
                    EndDrag();
                    break;
            }
            return true;
        }

        private void MoveToFinger(float y)
        {
            if (dragged is not { } card || rows.IsLayoutRequested)
                return;

            int from = cards.IndexOf(card), to = from;
            while (CanMove(from, to - 1) && y < cards[to - 1].View.Top + cards[to - 1].View.Height / 2f)
                to--;
            while (CanMove(from, to + 1) && y > cards[to + 1].View.Top + cards[to + 1].View.Height / 2f)
                to++;
            if (from != to)
                Move(from, to);
        }

        private int ScrollDirection()
        {
            host.scroll.GetLocationOnScreen(scrollLocation);
            float y = fingerY - scrollLocation[1];
            return y < host.Dp(52) ? -1 : y > host.scroll.Height - host.Dp(52) ? 1 : 0;
        }

        private void UpdateAutoScroll()
        {
            if (autoScroll is not null || ScrollDirection() == 0)
                return;
            autoScroll = () =>
            {
                int direction = ScrollDirection();
                if (dragged is null || direction == 0 || !host.scroll.CanScrollVertically(direction))
                {
                    StopAutoScroll();
                    return;
                }
                host.scroll.ScrollBy(0, direction * host.Dp(10));
                rows.GetLocationOnScreen(listLocation);
                MoveToFinger(fingerY - listLocation[1]);
                host.handler.PostDelayed(autoScroll!, 20);
            };
            host.handler.PostDelayed(autoScroll, 20);
        }

        private void StopAutoScroll()
        {
            if (autoScroll is not { } callback)
                return;
            host.handler.RemoveCallbacks(callback);
            autoScroll = null;
        }

        internal void StopDrag()
        {
            if (dragged is null)
                return;
            rows.CancelDragAndDrop();
            if (dragged is not null)
                EndDrag();
        }

        private void EndDrag()
        {
            StopAutoScroll();
            if (dragged is { } card)
                card.View.Alpha = 1;
            dragged = null;
            if (!dropped && beforeDrag is { } order && !Order.SequenceEqual(order))
            {
                for (int to = 0; to < order.Length; to++)
                {
                    int from = cards.FindIndex(card => card.Id == order[to]);
                    if (from != to)
                        Move(from, to);
                }
            }
            beforeDrag = null;
        }
    }
}
