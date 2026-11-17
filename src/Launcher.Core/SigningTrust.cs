namespace Launcher.Core;

public static class SigningTrust
{
    public const string LegacyCertificate="4948630F9E81B77D80DE6846166A3D36BD27AB59279E494D176C029CB150DC7D";
    public const string ProductionCertificate="D33549B0E23F10F9AB9C4DB4E706E5AAF95603297C06DC5CC9FA06F24E3DFE9D";
    public static bool Accepts(IEnumerable<string> installed,IEnumerable<string> incoming,IEnumerable<string> verifiedLineage)
    {
        string[] old=installed.Select(s=>s.ToUpperInvariant()).Distinct().Order().ToArray(),next=incoming.Select(s=>s.ToUpperInvariant()).Distinct().Order().ToArray();
        if(old.Length==0||next.Length==0)return false;
        if(old.SequenceEqual(next))return true;
        if(old.Length!=1||next.Length!=1||old[0]!=LegacyCertificate||next[0]!=ProductionCertificate)return false;
        var history=verifiedLineage.Select(s=>s.ToUpperInvariant()).ToHashSet();return history.Contains(LegacyCertificate)&&history.Contains(ProductionCertificate);
    }
}
