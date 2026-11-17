using Android.App;
using Android.Content;
using Android.Widget;
using Launcher.Core;
namespace LauncherApp;

public partial class MainActivity
{
    private void CreateNoteFromShare(string text)
    {
        if(state.Notes.Count>=200){Toast("The note limit is 200. Export or remove a note first.");return;}
        string value=text.Trim();if(value.Length==0)return;
        var note=new WorkspaceNote{Title=LauncherState.CleanName(value.Split('\n')[0],"Captured note"),Text=value[..Math.Min(value.Length,20000)]};
        state.Notes.Add(note);state.RootOrder.Add("note:"+note.Id);state.CollapsedCards["note:"+note.Id]=false;Save();Navigate("Root");
    }
    private void HandleSharedContent(Intent? intent)
    {
        if(intent?.Action!=Intent.ActionSend)return;
        string text=intent.GetStringExtra(Intent.ExtraText)??"",subject=intent.GetStringExtra(Intent.ExtraSubject)??"";
        intent.RemoveExtra(Intent.ExtraText);intent.RemoveExtra(Intent.ExtraSubject);intent.SetAction(Intent.ActionMain);
        if(text.Length==0){Toast("Share text or a link to capture it in Launcher.");return;}
        text=text[..Math.Min(20000,text.Length)];string captured=text;
        handler.Post(()=>
        {
            EditText? title=null,content=null;
            Sheet("Capture in Launcher",(panel,dialog)=>
            {
                title=FormField(panel,"Title",LauncherState.CleanName(subject.Length>0?subject:captured.Split('\n')[0],"Captured note"));content=FormField(panel,"Content",captured,multiline:true);content.SetFilters([new Android.Text.InputFilterLengthFilter(20000)]);
                Paragraph("Saved locally as a note. Nothing is uploaded.",panel);
            },(footer,dialog)=>DialogActions(footer,("Cancel",dialog.Dismiss),("Save note",()=>
            {
                if(state.Notes.Count>=200){Toast("The note limit is 200.");return;}
                var note=new WorkspaceNote{Title=LauncherState.CleanName(title?.Text,"Captured note"),Text=content?.Text??""};state.Notes.Add(note);state.RootOrder.Add("note:"+note.Id);Save();dialog.Dismiss();Navigate("Root");
            })));
        });
    }
}
