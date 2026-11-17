using System.Text.Json.Serialization;
namespace Launcher.Core;

public sealed class CompanionSnapshot
{
    public List<WorkspaceNote> Notes {get;set;}=[];
    public bool FolderShared {get;set;}
    public bool AllowUploads {get;set;}
    public string Device {get;set;}="Android";
}
public sealed class CompanionCommand
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Kind {get;set;}="";
    public string Uri {get;set;}="";
    public string Name {get;set;}="";
    public string Mime {get;set;}="application/octet-stream";
    public string Data {get;set;}="";
    public WorkspaceNote? Note {get;set;}
    public long ExpectedAt {get;set;}
}
public sealed class CompanionFile
{
    public string Uri {get;set;}="";
    public string Name {get;set;}="";
    public string Mime {get;set;}="";
    public long Size {get;set;}
    public bool Folder {get;set;}
}
public sealed class CompanionResult
{
    public string Id {get;set;}="";
    public string Error {get;set;}="";
    public string Name {get;set;}="";
    public string Mime {get;set;}="application/octet-stream";
    public string Data {get;set;}="";
    public List<CompanionFile> Files {get;set;}=[];
}
public sealed class CompanionPair {public string Pin {get;set;}="";public string Token {get;set;}="";}
[JsonSerializable(typeof(CompanionSnapshot))]
[JsonSerializable(typeof(CompanionCommand))]
[JsonSerializable(typeof(CompanionResult))]
[JsonSerializable(typeof(CompanionPair))]
public partial class CompanionJson:JsonSerializerContext;
