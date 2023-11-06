using System;
using NLog;
using Org.XmlResolver.Features;

namespace Org.XmlResolver;

public class CatalogQuerier
{
    private readonly XmlResolverConfiguration config;
    private readonly ResolverLogger logger;

    public CatalogQuerier()
    {
        config = new XmlResolverConfiguration();
        logger = new(LogManager.GetCurrentClassLogger());
    }

    public CatalogQuerier(XmlResolverConfiguration config)
    {
        this.config = config;
        logger = new(LogManager.GetCurrentClassLogger());
    }

    public XmlResolverConfiguration GetConfiguration()
    {
        return config;
    }

    public CatalogResponse lookup(ResourceRequest request)
    {
        string name = request.GetEntityName();
        string publicId = request.GetPublicId();
        string systemId = request.GetSystemId();
        string baseUri = request.GetBaseUri();
        string nature = request.GetNature();
        string purpose = request.GetPurpose();

        bool resolveEntity;
        string kind; // used for messages
        if (nature == ResolverConstants.EXTERNAL_ENTITY_NATURE
            || nature == ResolverConstants.DTD_NATURE)
        {
            kind = "resolveEntity";
            resolveEntity = true;
        }
        else
        {
            kind = "resolveURI";
            resolveEntity = false;
        }

        if (name == null && publicId == null && systemId == null && baseUri == null)
        {
            logger.Log(ResolverLogger.REQUEST, "%s: null", kind);
            return new CatalogResponse(request);
        }

        if (nature == ResolverConstants.DTD_NATURE && name != null && publicId == null && systemId == null)
        {
            return resolveDoctype(request);
        }

        CatalogManager catalog = (CatalogManager) config.GetFeature(ResolverFeature.CATALOG_MANAGER);
        Uri systemIdUri = null;
        if (systemId != null)
        {
            systemIdUri = new Uri(systemId);
        }

        logger.Log(ResolverLogger.REQUEST, "{0}: {1}{2} (baseURI {3}, publicId: {4}",
            kind, (name == null ? "" : name + " "), systemId, baseUri, publicId);

        Uri resolved = null;
        if (resolveEntity)
        {
            resolved = catalog.LookupEntity(name, systemId, publicId);
            if (resolved == null && systemId != null && (bool)config.GetFeature(ResolverFeature.URI_FOR_SYSTEM))
            {
                resolved = catalog.LookupUri(systemId);
            }
        }
        else
        {
            if (systemId == null)
            {
                return new CatalogResponse(request);
            }

            resolved = catalog.LookupNamespaceUri(systemId, nature, purpose);
        }

        if (resolved != null)
        {
            return new CatalogResponse(request, resolved);
        }

        Uri absSystem = null;
        if (systemId != null && systemIdUri != null && systemIdUri.IsAbsoluteUri)
        {
            absSystem = systemIdUri;
        }
        else
        {
            absSystem = request.GetAbsoluteUri();
        }

        if (absSystem != null)
        {
            if (resolveEntity)
            {
                resolved = catalog.LookupEntity(name, absSystem.ToString(), publicId);
                if (resolved == null && (bool)config.GetFeature(ResolverFeature.URI_FOR_SYSTEM))
                {
                    resolved = catalog.LookupUri(absSystem.ToString());
                }
            }
            else
            {
                resolved = catalog.LookupNamespaceUri(absSystem.ToString(), nature, purpose);
            }
        }

        return new CatalogResponse(request, resolved);
    }

    private CatalogResponse resolveDoctype(ResourceRequest request)
    {
        string name = request.GetEntityName();
        string baseUri = null;

        Uri uri = request.GetAbsoluteUri();
        if (uri != null)
        {
            baseUri = uri.ToString();
        }

        if (baseUri == null)
        {
            logger.Log(ResolverLogger.REQUEST, "resolveDoctype: {0}", name);
        }
        else
        {
            logger.Log(ResolverLogger.REQUEST, "resolveDoctype: {0} ({1})", name, baseUri);
        }

        CatalogManager catalog = (CatalogManager)config.GetFeature(ResolverFeature.CATALOG_MANAGER);
        Uri resolved = catalog.LookupDoctype(name, null, null);
        if (resolved == null)
        {
            logger.Log(ResolverLogger.RESPONSE, "resolveDoctype: null");
            return new CatalogResponse(request);
        }

        logger.Log(ResolverLogger.RESPONSE, "resolveDoctype: {0}", resolved.ToString());
        return new CatalogResponse(request, resolved);
    }
}