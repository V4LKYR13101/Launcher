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
        Paragraph("Tap a face to name the person. Then use that name in Files.", card);
        if (!SharedStorage.CanRead(this))
        {
            SettingRow(card, "Allow file access", "Find people in shared photos", RequestFileSearchAccess, "lock");
            return;
        }
        _ = LoadPeople(card, pack, peopleOffset);
    }

    private async Task LoadPeople(LinearLayout card, PhotoSearchPack pack, int offset)
    {
        peopleLoading = true;
        var placeholder = Text("Loading faces…", 13, color: palette.Muted);
        card.AddView(placeholder, Match());
        PhotoContentIndex.Person[] people;
        try { people = await Task.Run(() => pack.People(offset)); }
        catch (Exception error) when (IsImageSearchError(error))
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            if (!IsDestroyed && page == "People")
                placeholder.Text = "Couldn't load face groups. Reopen People to retry.";
            return;
        }
        finally { peopleLoading = false; }
        if (IsDestroyed || page != "People" || offset != peopleOffset || card.Parent is null)
            return;
        card.RemoveView(placeholder);
        if (people.Length == 0)
        {
            Paragraph("No face groups on this page yet. Index images from Image Search to find people.", card);
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
            labels.AddView(Text(Counted(person.Photos, "photo"), 12, color: palette.Muted), Match());
            row.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
            TouchFeedback(row);
            row.Click += (_, _) => Prompt("Name person", person.Name, value => _ = SavePersonName(pack, person.Id, value));
            card.AddView(row, Match());
        }
        if (offset > 0)
            SettingRow(card, "Previous faces", "", () => { peopleOffset -= PeoplePageSize; RenderBody(); }, "back");
        if (people.Length > PeoplePageSize)
            SettingRow(card, "More faces", "", () => { peopleOffset += PeoplePageSize; RenderBody(); }, "chevron");
    }

    private async Task SavePersonName(PhotoSearchPack pack, long id, string name)
    {
        try { await pack.Name(id, name); }
        catch (Exception error) when (IsImageSearchError(error))
        {
            Android.Util.Log.Warn("ImageSearch", error.ToString());
            Toast("Couldn't save this name. Try again.");
        }
        if (!IsDestroyed && page == "People")
        {
            peopleOffset = 0;
            RenderBody();
        }
    }
}
