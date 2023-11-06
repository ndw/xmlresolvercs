using System;
using Org.XmlResolver.Utils;

namespace Org.XmlResolver;

public class ResourceRequest
{
    private readonly XmlResolverConfiguration _config;
    private readonly string _nature;
    private readonly string _purpose;
    private string _uri = null;
    private string _baseUri = null;
    private string _entityName = null;
    private string _publicId = null;
    private string _encoding = null;
    private bool _openStream = true;
    private bool _followRedirects = true;

    protected ResourceRequest(XmlResolverConfiguration config, string nature, string purpose)
    {
        _config = config;
        _nature = nature;
        _purpose = purpose;
    }

    protected void SetUri(string uri)
    {
        _uri = uri;
    }

    protected void SetBaseUri(string baseUri)
    {
        _baseUri = baseUri;
    }

    protected void SetEntityName(string name)
    {
        _entityName = name;
    }

    public string GetUri()
    {
        return _uri;
    }

    public string GetBaseUri()
    {
        return _baseUri;
    }

    public Uri GetAbsoluteUri()
    {
        if (_baseUri != null)
        {
            Uri abs = new Uri(_baseUri);
            if (abs.IsAbsoluteUri)
            {
                if (String.IsNullOrEmpty(_uri))
                {
                    return abs;
                }

                return UriUtils.Resolve(abs, _uri);
            }
        }

        if (_uri != null)
        {
            Uri abs = new Uri(_uri);
            if (abs.IsAbsoluteUri)
            {
                return abs;
            }
        }

        return null;
    }

    public string GetNature()
    {
        return _nature;
    }

    public string GetPurpose()
    {
        return _purpose;
    }

    public string GetSystemId()
    {
        return _uri;
    }

    public void SetPublicId(string publicId)
    {
        _publicId = publicId;
    }

    public string GetPublicId()
    {
        return _publicId;
    }

    public string GetEntityName()
    {
        return _entityName;
    }

    public void SetEncoding(string encoding)
    {
        _encoding = encoding;
    }

    public string GetEncoding()
    {
        return _encoding;
    }

    public void SetOpenStream(bool open)
    {
        _openStream = open;
    }

    public bool IsOpenStream()
    {
        return _openStream;
    }

    public void SetFollowRedirects(bool follow)
    {
        _followRedirects = follow;
    }

    public bool IsFollowRedirects()
    {
        return _followRedirects;
    }
    
}