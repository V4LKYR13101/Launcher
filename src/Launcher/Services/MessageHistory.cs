using Android.Content;
using Android.Database.Sqlite;
using System.Security.Cryptography;
using System.Text;
namespace LauncherApp.Services;
public sealed record SavedMessage(string Package,string Thread,string Title,string Sender,string Text,long Time,bool Outgoing);
public sealed class MessageHistory(Context context):SQLiteOpenHelper(context,"message-history.db",null,1)
{
    private static readonly object Gate=new();
    public override void OnCreate(SQLiteDatabase? db)
    {db!.ExecSQL("CREATE TABLE messages(id TEXT PRIMARY KEY,package TEXT NOT NULL,thread TEXT NOT NULL,title TEXT NOT NULL,sender TEXT NOT NULL,text TEXT NOT NULL,time INTEGER NOT NULL,outgoing INTEGER NOT NULL)");db.ExecSQL("CREATE INDEX message_thread ON messages(package,thread,time)");}
    public override void OnUpgrade(SQLiteDatabase? db,int oldVersion,int newVersion){}
    public void Add(SavedMessage message)
    {
        lock(Gate)
        {
            string text=message.Text[..Math.Min(4000,message.Text.Length)];
            var values=new ContentValues();values.Put("id",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Package+"\n"+message.Thread+"\n"+message.Time+"\n"+message.Sender+"\n"+text))));
            values.Put("package",message.Package);values.Put("thread",message.Thread);values.Put("title",message.Title);values.Put("sender",message.Sender);values.Put("text",text);values.Put("time",message.Time);values.Put("outgoing",message.Outgoing?1:0);
            var db=WritableDatabase!;db.InsertWithOnConflict("messages",null,values,Conflict.Ignore);
            db.Delete("messages","time<?",[DateTimeOffset.Now.AddDays(-30).ToUnixTimeMilliseconds().ToString()]);
            db.ExecSQL("DELETE FROM messages WHERE id NOT IN (SELECT id FROM messages ORDER BY time DESC LIMIT 5000)");
        }
    }
    public SavedMessage[] Read(string? package=null,string? thread=null)
    {
        lock(Gate)
        {
            WritableDatabase!.Delete("messages","time<?",[DateTimeOffset.Now.AddDays(-30).ToUnixTimeMilliseconds().ToString()]);
            string where=package is null?"":thread is null?" WHERE package=?":" WHERE package=? AND thread=?";
            using var cursor=ReadableDatabase!.RawQuery("SELECT package,thread,title,sender,text,time,outgoing FROM messages"+where+" ORDER BY time DESC LIMIT 500",package is null?null:thread is null?[package]:[package,thread]);var result=new List<SavedMessage>();
            while(cursor!.MoveToNext())result.Add(new(cursor.GetString(0)!,cursor.GetString(1)!,cursor.GetString(2)!,cursor.GetString(3)!,cursor.GetString(4)!,cursor.GetLong(5),cursor.GetInt(6)==1));return result.ToArray();
        }
    }
    public void Clear(){lock(Gate)WritableDatabase!.Delete("messages",null,null);}
}
