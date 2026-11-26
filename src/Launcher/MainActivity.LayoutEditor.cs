using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Launcher.Core;
namespace LauncherApp;

public partial class MainActivity
{
    private readonly Stack<string> layoutUndo = [];
    private List<string> TodayOrder() => state.RootOrder.Concat(new[] { "overview", "calendar", "weather", "media", "focus", "notes", "timers", "reminders", "calculator", "feeds", "tickets", "resources" }).Concat(state.Notes.Select(n => "note:" + n.Id)).Concat(state.ExtraCards.Select(c => c.Id)).Concat(state.Widgets.Select(w => "widget:" + w.Id)).Distinct().ToList();
    private void MoveTodayCard(string id, int direction)
    {
        var order = TodayOrder();
        int from = order.IndexOf(id), to = from + direction;
        if (from < 0 || to < 0 || to >= order.Count)
            return;
        RememberLayout();
        (order[from], order[to]) = (order[to], order[from]);
        state.RootOrder = order;
    }
    private void RememberLayout()
    {
        var snapshot = new LauncherState { Apps = state.Apps, Drawers = state.Drawers, RootOrder = state.RootOrder, RootHidden = state.RootHidden, RootPinned = state.RootPinned, Widgets = state.Widgets };
        layoutUndo.Push(snapshot.Serialize());
        if (layoutUndo.Count > 6)
        {
            var keep = layoutUndo.Take(6).Reverse().ToArray();
            layoutUndo.Clear();
            foreach (string item in keep)
                layoutUndo.Push(item);
        }
    }
    private void UndoLayout() => WithProtectedSettings(() =>
    {
        if (layoutUndo.Count == 0)
        {
            Toast(Localize(Resource.String.ui_there_are_no_layout_changes_to_undo_in_this));
            return;
        }
        var old = LauncherState.Deserialize(layoutUndo.Pop());
        offerTodayLayoutUndo = false;
        state.Apps = old.Apps;
        state.Drawers = old.Drawers;
        state.RootOrder = old.RootOrder;
        state.RootHidden = old.RootHidden;
        state.RootPinned = old.RootPinned;
        state.Widgets = old.Widgets;
        AppOrganization.Reconcile(state, apps);
        Save();
        DismissSheets();
        Render();
        Toast(Localize(Resource.String.ui_layout_change_undone));
    });
    private string CardLabel(string id) => id.StartsWith("widget:") && int.TryParse(id[7..], out int widget) ? WidgetLabel(widget) : id.StartsWith("note:") ? state.Notes.FirstOrDefault(n => "note:" + n.Id == id)?.Title ?? Localize(Resource.String.ui_note) : state.ExtraCards.FirstOrDefault(c => c.Id == id)?.Title ?? id switch { "overview" => Localize(Resource.String.ui_at_a_glance), "calendar" => Localize(Resource.String.ui_agenda), "media" => Localize(Resource.String.ui_now_playing), "feeds" => Localize(Resource.String.ui_news_feeds), "resources" => Localize(Resource.String.ui_phone_resources), _ => char.ToUpperInvariant(id[0]) + id[1..] };
    private void ShowLayoutEditor() => Choose(Resource.String.ui_edit_your_layout, [Localize(Resource.String.ui_arrange_app_drawers), Localize(Resource.String.ui_arrange_today_cards), Localize(Resource.String.ui_move_apps_in_bulk), Localize(Resource.String.ui_undo_last_layout_change), Localize(Resource.String.ui_arrange_favorites)], i =>
    {
        if (i == 1)
        {
            BeginTodayLayout();
            return;
        }
        if (i == 3)
        {
            UndoLayout();
            return;
        }
        if (i == 2)
        {
            BulkMoveApps();
            return;
        }
        bool drawers = i == 0;
        var order = drawers ? state.Drawers.Select(d => d.Id).ToList() : AppOrganization.Visible(state, apps).Where(a => !a.PrivateProfile && state.Apps[a.Id].Pinned).Select(a => a.Id).ToList();
        Sheet(drawers ? Localize(Resource.String.ui_arrange_drawers) : Localize(Resource.String.ui_arrange_favorites), (panel, dialog) =>
        {
            Paragraph(Localize(Resource.String.ui_hold_a_grip_and_drag_a_row_into_place), panel);
            var rows = Stack();
            panel.AddView(rows, Match());
            if (!drawers)
                Paragraph(order.Count == 0 ? Localize(Resource.String.ui_hold_an_app_and_choose_pin_to_favorites_to) : Localize(Resource.String.ui_the_first_five_visible_favorites_appear_in_the_dock), panel);
            void Reorder(string source, string target)
            {
                int from = order.IndexOf(source), to = order.IndexOf(target);
                if (from < 0 || to < 0 || from == to)
                    return;
                order.RemoveAt(from);
                order.Insert(to, source);
                Rebuild();
            }
            void Rebuild()
            {
                rows.RemoveAllViews();
                foreach (string id in order.ToArray())
                {
                    string label = drawers ? state.Drawers.First(d => d.Id == id).Name : apps.FirstOrDefault(a => a.Id == id) is { } app ? AppOrganization.DisplayName(state, app) : Localize(Resource.String.ui_unavailable_app);
                    var row = Stack(true);
                    row.SetGravity(GravityFlags.CenterVertical);
                    row.Background = Surface(palette.Card, 14);
                    row.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
                    var grip = IconButton("more", Localize(Resource.String.ui_drag) + label, () => Toast(Localize(Resource.String.ui_hold_the_grip_to_drag_or_use_the_arrows)));
                    grip.LongClick += (_, _) => grip.StartDragAndDrop(ClipData.NewPlainText("Launcher layout", id), new View.DragShadowBuilder(row), null, 0);
                    row.AddView(grip, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
                    var title = Text(label, 14, true);
                    title.SetMaxLines(2);
                    row.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
                    foreach (var (glyph, direction) in new[] { ("up", -1), ("down", 1) })
                    {
                        int next = order.IndexOf(id) + direction;
                        var button = IconButton(glyph, (direction < 0 ? Localize(Resource.String.ui_move_earlier_2) : Localize(Resource.String.ui_move_later_2)) + label, () => { if (next >= 0 && next < order.Count) Reorder(id, order[next]); });
                        button.Enabled = next >= 0 && next < order.Count;
                        button.Alpha = button.Enabled ? 1 : .25f;
                        row.AddView(button, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
                    }
                    row.SetOnDragListener(new LayoutDrop(id, Reorder, () => row.Background = Background(new Color(palette.Accent.R, palette.Accent.G, palette.Accent.B, (byte)35), 14), () => row.Background = Surface(palette.Card, 14)));
                    var lp = Match();
                    lp.SetMargins(0, 0, 0, Dp(8));
                    rows.AddView(row, lp);
                }
            }
            Rebuild();
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_save_layout), () => { RememberLayout(); if (drawers) state.Drawers = order.Select(id => state.Drawers.First(d => d.Id == id)).ToList(); else { for (int index = 0; index < order.Count; index++) if (state.Apps.TryGetValue(order[index], out var rule)) rule.Order = index; } Save(); dialog.Dismiss(); RenderBody(); Toast(Localize(Resource.String.ui_layout_saved_undo_is_available_in_edit_layout)); })));
    });
    private sealed class LayoutDrop(string target, Action<string, string> drop, Action entered, Action exited) : Java.Lang.Object, View.IOnDragListener
    {
        public bool OnDrag(View? view, DragEvent? e)
        {
            if (e is null)
                return false;
            if (e.Action == DragAction.Started)
                return e.ClipDescription?.Label?.ToString() == "Launcher layout";
            if (e.Action == DragAction.Entered)
                entered();
            if (e.Action is DragAction.Exited or DragAction.Ended)
                exited();
            if (e.Action == DragAction.Drop)
            {
                exited();
                string? source = e.ClipData?.GetItemAt(0)?.Text;
                if (source is not null)
                    drop(source, target);
            }
            return true;
        }
    }
    private void BulkMoveApps()
    {
        var selected = new HashSet<string>();
        string filter = "";
        Sheet(Localize(Resource.String.ui_move_apps_together), (panel, dialog) =>
        {
            var search = FormField(panel, Localize(Resource.String.ui_find_apps), "", Localize(Resource.String.ui_app_name_or_package), search: true);
            var rows = Stack();
            panel.AddView(rows, Match());
            void Rebuild()
            {
                rows.RemoveAllViews();
                foreach (var app in apps.Where(a => !a.PrivateProfile && (a.Label.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || a.PackageName.Contains(filter, StringComparison.OrdinalIgnoreCase))))
                {
                    var box = new CheckBox(this) { Text = AppOrganization.DisplayName(state, app), Checked = selected.Contains(app.Id), TextSize = 14, Typeface = regular };
                    box.SetTextColor(palette.Text);
                    box.ButtonTintList = AccentTint();
                    box.SetMinimumHeight(Dp(48));
                    box.CheckedChange += (_, e) => { if (e.IsChecked) selected.Add(app.Id); else selected.Remove(app.Id); };
                    rows.AddView(box, Match());
                }
            }
            search.TextChanged += (_, _) => { filter = search.Text ?? ""; Rebuild(); };
            Rebuild();
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_move_selected), () =>
        {
            if (selected.Count == 0)
            {
                Toast(Localize(Resource.String.ui_select_at_least_one_app));
                return;
            }
            WithProtectedSettings(() => Choose(Resource.String.ui_destination_drawer, state.Drawers.Select(DrawerName).ToArray(), index => { RememberLayout(); foreach (string id in selected) state.Apps[id].DrawerId = state.Drawers[index].Id; Save(); DismissSheets(); RenderBody(); Toast(Counted(selected.Count, Resource.Plurals.app) + Localize(Resource.String.ui_moved)); }));
        }
        )));
    }
}
