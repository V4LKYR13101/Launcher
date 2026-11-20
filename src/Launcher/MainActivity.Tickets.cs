using Android.Content;
using Android.Widget;
using Launcher.Core;
namespace LauncherApp;

public partial class MainActivity
{
    private string? attachingTicket;
    private void BuildTickets(LinearLayout panel)
    {
        panel.AddView(RootAction("+ Add journey", () => EditTicket(new(), true), true), new LinearLayout.LayoutParams(-2, -2));
        foreach (var ticket in state.Tickets.ToArray())
        {
            var item = ticket;
            SettingRow(panel, item.Title, string.Join(" · ", new[] { item.Route, item.Date, item.Reference }.Where(s => s.Length > 0)), () => EditTicket(item, false), "file");
            if (item.Attachment.Length > 0)
                RootActions(panel, ("Open attachment", () => OpenIntent(new Intent(Intent.ActionView, Android.Net.Uri.Parse(item.Attachment)).AddFlags(ActivityFlags.GrantReadUriPermission))));
        }
    }
    private void EditTicket(TravelTicket ticket, bool create)
    {
        EditText? title = null, route = null, date = null, reference = null;
        Sheet(create ? "New journey" : ticket.Title, (panel, dialog) =>
        {
            title = FormField(panel, "Title", ticket.Title, "e.g. Flight home");
            route = FormField(panel, "Route", ticket.Route, "e.g. Johannesburg to Cape Town");
            date = FormField(panel, "Date / departure", ticket.Date, "e.g. 18 Nov, 14:30");
            reference = FormField(panel, "Booking reference", ticket.Reference);
            if (!create)
            {
                Gap(panel, 12);
                RootActions(panel, ("Attach boarding pass", () => { attachingTicket = ticket.Id; var pick = new Intent(Intent.ActionOpenDocument).SetType("*/*")!.AddCategory(Intent.CategoryOpenable)!.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission); StartActivityForResult(pick, 813); dialog.Dismiss(); }
                ));
                RootActions(panel, ("Remove journey", () => Confirm("Remove journey?", "Remove this card? The original attachment stays where it is.", "Remove", () => { state.Tickets.Remove(ticket); Save(); dialog.Dismiss(); RenderBody(); })));
            }
            Paragraph("Keep a journey and its boarding pass together. Tickets are entered here; Launcher does not read your email or booking accounts.", panel);
        }, (footer, dialog) => DialogActions(footer, ("Cancel", dialog.Dismiss), ("Save journey", () =>
        {
            ticket.Title = LauncherState.CleanName(title?.Text, "Journey");
            ticket.Route = route?.Text ?? "";
            ticket.Date = date?.Text ?? "";
            ticket.Reference = reference?.Text ?? "";
            if (create)
                state.Tickets.Add(ticket);
            Save();
            dialog.Dismiss();
            Navigate("Root");
        }
        )));
    }
}
