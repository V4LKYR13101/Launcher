using Android.Graphics;
using Android.Views;
using Android.Widget;
using LauncherApp.Services;
using LauncherApp.Services.ImageSearch;

namespace LauncherApp;

public partial class MainActivity
{
    private const int PeoplePageSize = 25;
    private int peopleOffset, displayedPeopleRevision;
    private bool peopleLoading;

    private void RenderPeopleSettings()
    {
        var pack = PhotoSearchPack.Faces(this);
        displayedPeopleRevision = pack.Revision;
        var card = AddSettingsCard();
        Paragraph(Localize(Resource.String.ui_tap_a_face_to_name_the_person_then_use), card);
        if (!SharedStorage.CanRead(this))
        {
            SettingRow(card, Localize(Resource.String.ui_allow_file_access), Localize(Resource.String.ui_find_people_in_shared_photos), RequestFileSearchAccess, "lock");
            return;
        }
        _ = LoadPeople(card, pack, peopleOffset);
    }

    private async Task LoadPeople(LinearLayout card, PhotoSearchPack pack, int offset)
    {
        peopleLoading = true;
        var placeholder = Text(Localize(Resource.String.ui_loading_faces), 13, color: palette.Muted);
        card.AddView(placeholder, Match());
        PhotoContentIndex.Person[] people;
        try { people = await Task.Run(() => pack.People(offset)); }
        catch (Exception error) when (IsImageSearchError(error))
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            if (!IsDestroyed && page == "People")
                placeholder.Text = Localize(Resource.String.ui_couldn_t_load_face_groups_reopen_people_to_retry);
            return;
        }
        finally { peopleLoading = false; }
        if (IsDestroyed || page != "People" || offset != peopleOffset || card.Parent is null)
            return;
        card.RemoveView(placeholder);
        if (people.Length == 0)
        {
            Paragraph(Localize(Resource.String.ui_no_face_groups_on_this_page_yet_index_images), card);
        }
        foreach (var person in people.Take(PeoplePageSize))
        {
            string name = person.Name.Length == 0 ? "Name this person" : person.Name;
            var row = Stack(true);
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, Dp(6), 0, Dp(6));
            var preview = new ImageView(this)
            {
                Background = Surface(palette.Card, 12),
                ClipToOutline = true,
                ImportantForAccessibility = ImportantForAccessibility.No
            };
            using var bitmap = BitmapFactory.DecodeByteArray(person.Preview, 0, person.Preview.Length);
            preview.SetImageBitmap(bitmap);
            preview.SetScaleType(ImageView.ScaleType.CenterCrop);
            row.AddView(preview, new LinearLayout.LayoutParams(Dp(58), Dp(58)) { MarginEnd = Dp(12) });
            var labels = Stack();
            labels.AddView(Text(name, 15, true), Match());
            labels.AddView(Text(Counted(person.Photos, Resource.Plurals.photo), 12, color: palette.Muted), Match());
            row.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
            TouchFeedback(row);
            row.Click += (_, _) => Prompt(Resource.String.ui_name_person, person.Name, value => _ = SavePersonName(pack, person.Id, value));
            card.AddView(row, Match());
        }
        if (offset > 0)
            SettingRow(card, Localize(Resource.String.ui_previous_faces), "", () => { peopleOffset -= PeoplePageSize; RenderBody(); }, "back");
        if (people.Length > PeoplePageSize)
            SettingRow(card, Localize(Resource.String.ui_more_faces), "", () => { peopleOffset += PeoplePageSize; RenderBody(); }, "chevron");
    }

    private async Task SavePersonName(PhotoSearchPack pack, long id, string name)
    {
        try { await pack.Name(id, name); }
        catch (Exception error) when (IsImageSearchError(error))
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            Toast(Localize(Resource.String.ui_couldn_t_save_this_name_try_again));
        }
        if (!IsDestroyed && page == "People")
        {
            peopleOffset = 0;
            RenderBody();
        }
    }
}
