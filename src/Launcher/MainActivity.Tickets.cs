using Android.Content;
using Android.Widget;
using Launcher.Core;
namespace LauncherApp;

public partial class MainActivity
{
    private string? attachingTicket;
    private void BuildTickets(LinearLayout panel)
    {
        panel.AddView(RootAction(Localize(Resource.String.ui_add_journey_action), () => EditTicket(new(), true), true), new LinearLayout.LayoutParams(-2, -2));
        foreach (var ticket in state.Tickets.ToArray())
        {
            var item = ticket;
            SettingRow(panel, item.Title, string.Join(" · ", new[] { item.Route, item.Date, item.Reference }.Where(s => s.Length > 0)), () => EditTicket(item, false), "file");
            if (item.Attachment.Length > 0)
                RootActions(panel, (Localize(Resource.String.ui_open_attachment), () => OpenIntent(new Intent(Intent.ActionView, Android.Net.Uri.Parse(item.Attachment)).AddFlags(ActivityFlags.GrantReadUriPermission))));
        }
    }
    private void EditTicket(TravelTicket ticket, bool create)
    {
        EditText? title = null, route = null, date = null, reference = null;
        Sheet(create ? Localize(Resource.String.ui_new_journey) : ticket.Title, (panel, dialog) =>
        {
            title = FormField(panel, Localize(Resource.String.ui_title), ticket.Title, Localize(Resource.String.ui_e_g_flight_home));
            route = FormField(panel, Localize(Resource.String.ui_route), ticket.Route, Localize(Resource.String.ui_e_g_johannesburg_to_cape_town));
            date = FormField(panel, Localize(Resource.String.ui_date_departure), ticket.Date, Localize(Resource.String.ui_e_g_18_nov_14_30));
            reference = FormField(panel, Localize(Resource.String.ui_booking_reference), ticket.Reference);
            if (!create)
            {
                Gap(panel, 12);
                RootActions(panel, (Localize(Resource.String.ui_attach_boarding_pass), () => { attachingTicket = ticket.Id; var pick = new Intent(Intent.ActionOpenDocument).SetType("*/*")!.AddCategory(Intent.CategoryOpenable)!.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission); StartActivityForResult(pick, 813); dialog.Dismiss(); }
                ));
                RootActions(panel, (Localize(Resource.String.ui_remove_journey), () => Confirm(Localize(Resource.String.ui_remove_journey_2), Localize(Resource.String.ui_remove_this_card_the_original_attachment_stays_where_it), Localize(Resource.String.ui_remove), () => { state.Tickets.Remove(ticket); Save(); dialog.Dismiss(); RenderBody(); })));
            }
            Paragraph(Localize(Resource.String.ui_keep_a_journey_and_its_boarding_pass_together_tickets), panel);
        }, (footer, dialog) => DialogActions(footer, (Localize(Resource.String.ui_cancel), dialog.Dismiss), (Localize(Resource.String.ui_save_journey), () =>
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
