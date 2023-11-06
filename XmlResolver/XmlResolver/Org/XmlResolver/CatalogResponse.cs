using System;

namespace Org.XmlResolver;

public class CatalogResponse
{
    public readonly ResourceRequest Request;
    public readonly bool Found;
    public readonly Uri Uri;

    public CatalogResponse(ResourceRequest request)
    {
        Request = request;
        Uri = null;
        Found = false;
    }

    public CatalogResponse(ResourceRequest request, Uri uri)
    {
        Request = request;
        Uri = uri;
        Found = true;
    }

    public bool IsResolved()
    {
        return Found;
    }
    
}