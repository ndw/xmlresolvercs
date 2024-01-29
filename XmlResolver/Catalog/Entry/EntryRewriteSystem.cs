using XmlResolver.Utils;

namespace XmlResolver.Catalog.Entry;

public class EntryRewriteSystem: Entry 
{
    public readonly string SystemIdStart;
    public readonly Uri RewritePrefix;
        
    public EntryRewriteSystem(Uri baseUri, string? id, string start, string rewrite) : base(baseUri, id)
    {
        if (start.StartsWith("classpath:/")) {
            // classpath:/path/to/thing is the same as classpath:path/to/thing
            // normalize without the leading slash.
            SystemIdStart = "classpath:" + start.Substring(11);
        } else {
            SystemIdStart = start;
        }

        RewritePrefix = UriUtils.Resolve(baseUri, rewrite);
    }

    public override EntryType GetEntryType() 
    {
        return EntryType.RewriteSystem;
    }
        
    public override string ToString() 
    {
        return $"rewriteSystem {SystemIdStart} {Rarr} {RewritePrefix}";
    }
}
