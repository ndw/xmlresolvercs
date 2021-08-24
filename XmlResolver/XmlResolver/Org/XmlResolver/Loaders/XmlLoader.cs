using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Xml;
using NLog;
using Org.XmlResolver.Catalog.Entry;
using Org.XmlResolver.Features;
using Org.XmlResolver.Utils;

namespace Org.XmlResolver.Loaders {
    public class XmlLoader : CatalogLoader {
        private readonly object _syncLock = new object();
        protected static ResolverLogger logger = new ResolverLogger(LogManager.GetCurrentClassLogger());

        private static readonly HashSet<string> CATALOG_ELEMENTS
            = new() {"group", "public", "system", "rewriteSystem",
                "delegatePublic", "delegateSystem", "uri", "rewriteURI", "delegateURI",
                "nextCatalog", "uriSuffix", "systemSuffix"};

        private static readonly HashSet<string> TR9401_ELEMENTS
            = new() {"doctype", "document", "dtddecl", "entity", "linktype", "notation", "sgmldecl"};

        protected readonly Dictionary<Uri,EntryCatalog> catalogMap;
        private static Resolver _loaderResolver = null;

        private readonly Stack<Entry> parserStack;
        private readonly Stack<bool> preferPublicStack;
        private readonly Stack<Uri> baseUriStack;

        private bool _preferPublic = true;
        private bool _archivedCatalogs = true;
        private EntryCatalog catalog = null;
        private Locator locator = null;

        public XmlLoader() {
            catalogMap = new Dictionary<Uri, EntryCatalog>();
            parserStack = new();
            preferPublicStack = new();
            baseUriStack = new();
        }

        public Resolver LoaderResolver {
            get
            {
                if (_loaderResolver == null) {
                    XmlResolverConfiguration config = new XmlResolverConfiguration(new List<Uri>(), new List<string>());
                    config.SetFeature(ResolverFeature.PREFER_PUBLIC, true);
                    config.SetFeature(ResolverFeature.CACHE_DIRECTORY, null);
                    config.SetFeature(ResolverFeature.CACHE_UNDER_HOME, false);
                    config.SetFeature(ResolverFeature.ALLOW_CATALOG_PI, false);
                    config.SetFeature(ResolverFeature.CLASSPATH_CATALOGS, false);
                    config.AddAssemblyCatalog("Org.XmlResolver.catalog.xml", Assembly.GetExecutingAssembly());
                    _loaderResolver = new Resolver(config);
                }

                return _loaderResolver;
            }
        }
        
        public EntryCatalog LoadCatalog(Uri caturi) {
            lock (_syncLock) {
                if (catalogMap.ContainsKey(caturi)) {
                    return catalogMap[caturi];
                }

                Stream stream = null;
                try {
                    stream = UriUtils.GetStream(caturi);
                }
                catch (Exception) {
                    logger.Log(ResolverLogger.ERROR, "Failed to load catalog {0}", caturi.ToString());
                    catalog = new EntryCatalog(caturi, null, false);
                    catalogMap.Add(caturi, catalog);
                    return catalog;
                }

                return _LoadCatalog(caturi, stream);
            }
        }

        public EntryCatalog LoadCatalog(Uri caturi, Stream data) {
            lock (_syncLock) {
                if (catalogMap.ContainsKey(caturi)) {
                    return catalogMap[caturi];
                }

                return _LoadCatalog(caturi, data);
            }
        }

        private EntryCatalog _LoadCatalog(Uri caturi, Stream data) {
            return _LoadCatalog(caturi, caturi, data);
        }
        
        private EntryCatalog _LoadCatalog(Uri caturi, Uri baseUri, Stream data) {
            catalog = null;
            locator = null;
            parserStack.Clear();
            baseUriStack.Clear();
            baseUriStack.Push(baseUri);
            preferPublicStack.Push(_preferPublic);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Async = false;
            settings.DtdProcessing = DtdProcessing.Ignore; // FIXME: ???
            settings.XmlResolver = _loaderResolver;

            try {
                using (XmlReader reader = XmlReader.Create(data, settings)) {
                    while (reader.Read()) {
                        switch (reader.NodeType) {
                            case XmlNodeType.Element:
                                bool empty = reader.IsEmptyElement;
                                StartElement(reader);
                                if (empty) {
                                    EndElement(reader);
                                }

                                break;
                            case XmlNodeType.EndElement:
                                EndElement(reader);
                                break;
                            default:
                                break;
                        }
                    }
                }

                catalogMap.Add(caturi, catalog);
                return catalog;
            }
            catch (Exception ex)  {
                logger.Log(ResolverLogger.ERROR, "Failed to parse catalog {0}: {1}", caturi.ToString(), ex.Message);

                if (_archivedCatalogs) {
                    catalog = ArchiveCatalog(caturi);
                    // It will already have been added to the map
                }
                else {
                    catalog = new EntryCatalog(caturi, null, false);
                    catalogMap.Add(caturi,catalog);
                }
                
                return catalog;
            }
        }

        private void StartElement(XmlReader reader) {
            if (parserStack.Count == 0) {
                if (reader.NamespaceURI.Equals(ResolverConstants.CATALOG_NS)
                    && reader.LocalName.Equals("catalog")) {
                    string id = reader.GetAttribute("id", "");
                    string prefer = reader.GetAttribute("prefer", "");
                    if (prefer != null) {
                        preferPublicStack.Push("public".Equals(prefer));
                        if (!"public".Equals(prefer) && !"system".Equals(prefer)) {
                            logger.Log(ResolverLogger.ERROR, "Prefer on {0} is neither 'sytem' nor 'public': {1}",
                                    reader.LocalName, prefer);
                        }
                    }

                    catalog = new EntryCatalog(baseUriStack.Peek(), id, preferPublicStack.Peek());
                    
                    if (reader is IXmlLineInfo && ((IXmlLineInfo) reader).HasLineInfo()) {
                        locator = new Locator();
                        catalog.SetLocator(locator);
                    }

                    parserStack.Push(catalog);
                } else {
                    logger.Log(ResolverLogger.ERROR, "Catalog document is not an XML Catalog (ignored): {0}", reader.Name);
                    parserStack.Push(new EntryNull());
                }

                Uri baseUri = baseUriStack.Peek();
                if (reader.GetAttribute("xml:base") != null) {
                    baseUri = UriUtils.Resolve(baseUri, reader.GetAttribute("xml:base"));
                }

                baseUriStack.Push(baseUri);
                preferPublicStack.Push(preferPublicStack.Peek());
                return;
            }

            Entry top = parserStack.Peek();
            if (top.GetEntryType() == Entry.EntryType.NULL) {
                PushNull();
            } else {
                if (ResolverConstants.CATALOG_NS.Equals(reader.NamespaceURI)) {
                    // Technically, the TR9401 extension elements should be in the TR9401 namespace,
                    // but I'm willing to bet lots of folks get that wrong. Be liberal in what mumble mumble...
                    if (CATALOG_ELEMENTS.Contains(reader.LocalName) || TR9401_ELEMENTS.Contains(reader.LocalName)) {
                        CatalogElement(reader);
                    } else {
                        logger.Log(ResolverLogger.ERROR, "Unexpected catalog element (ignored): {0}", reader.LocalName);
                        PushNull();
                    }
                } else if (ResolverConstants.TR9401_NS.Equals(reader.NamespaceURI)) {
                    if (TR9401_ELEMENTS.Contains(reader.LocalName)) {
                        CatalogElement(reader);
                    } else {
                        logger.Log(ResolverLogger.ERROR, "Unexpected catalog element (ignored): {0}", reader.LocalName);
                        PushNull();
                    }
                } else {
                    PushNull();
                }
            }
        }
        
        private void EndElement(XmlReader reader) {
            parserStack.Pop();
            baseUriStack.Pop();
            preferPublicStack.Pop();
        }

        private void CatalogElement(XmlReader reader) {
            string id = reader.GetAttribute("id", "");
            string name = reader.GetAttribute("name", "");
            string uri = reader.GetAttribute("uri", "");
            string caturi = reader.GetAttribute("catalog", "");
            string start, prefix, suffix, publicId;

            Uri baseUri = baseUriStack.Peek();
            if (reader.GetAttribute("xml:base") != null) {
                baseUri = UriUtils.Resolve(baseUri, reader.GetAttribute("xml:base"));
            }

            if (locator != null) {
                IXmlLineInfo li = (IXmlLineInfo) reader;
                locator.BaseUri = baseUri;
                locator.LineNumber = li.LineNumber;
                locator.LinePosition = li.LinePosition;
            }

            bool preferPublic = preferPublicStack.Peek();

            Entry entry = null;

            switch (reader.LocalName) {
                case "group":
                    String prefer = reader.GetAttribute("prefer", "");
                    if (prefer != null) {
                        preferPublic = "public".Equals(prefer);
                        if (!"public".Equals(prefer) && !"system".Equals(prefer)) {
                            logger.Log(ResolverLogger.ERROR, "Prefer on {0} is neither 'system' nor 'public': {1}",
                                reader.LocalName, prefer);
                        }
                    }

                    entry = catalog.AddGroup(baseUri, id, preferPublic);
                    break;
                case "public":
                    // In XML, there will always be a system identifier.
                    publicId = reader.GetAttribute("publicId", "");
                    entry = catalog.AddPublic(baseUri, id, publicId, uri, preferPublic);
                    break;
                case "system":
                    String systemId = reader.GetAttribute("systemId", "");
                    entry = catalog.AddSystem(baseUri, id, systemId, uri);
                    break;
                case "rewriteSystem":
                    start = reader.GetAttribute("systemIdStartString", "");
                    prefix = reader.GetAttribute("rewritePrefix", "");
                    entry = catalog.AddRewriteSystem(baseUri, id, start, prefix);
                    break;
                case "systemSuffix":
                    suffix = reader.GetAttribute("systemIdSuffix", "");
                    entry = catalog.AddSystemSuffix(baseUri, id, suffix, uri);
                    break;
                case "delegatePublic":
                    start = reader.GetAttribute("publicIdStartString", "");
                    entry = catalog.AddDelegatePublic(baseUri, id, start, caturi, preferPublic);
                    break;
                case "delegateSystem":
                    start = reader.GetAttribute("systemIdStartString", "");
                    entry = catalog.AddDelegateSystem(baseUri, id, start, caturi);
                    break;
                case "uri":
                    String nature = reader.GetAttribute("nature", ResolverConstants.RDDL_NS);
                    String purpose = reader.GetAttribute("purpose", ResolverConstants.RDDL_NS);
                    entry = catalog.AddUri(baseUri, id, name, uri, nature, purpose);
                    break;
                case "uriSuffix":
                    suffix = reader.GetAttribute("uriSuffix", "");
                    entry = catalog.AddUriSuffix(baseUri, id, suffix, uri);
                    break;
                case "rewriteURI":
                    start = reader.GetAttribute("uriStartString", "");
                    prefix = reader.GetAttribute("rewritePrefix", "");
                    entry = catalog.AddRewriteUri(baseUri, id, start, prefix);
                    break;
                case "delegateURI":
                    start = reader.GetAttribute("uriStartString", "");
                    entry = catalog.AddDelegateUri(baseUri, id, start, caturi);
                    break;
                case "nextCatalog":
                    entry = catalog.AddNextCatalog(baseUri, id, caturi);
                    break;
                case "doctype":
                    entry = catalog.AddDoctype(baseUri, id, name, uri);
                    break;
                case "document":
                    entry = catalog.AddDocument(baseUri, id, uri);
                    break;
                case "dtddecl":
                    publicId = reader.GetAttribute("publicId", "");
                    entry = catalog.AddDtdDecl(baseUri, id, publicId, uri);
                    break;
                case "entity":
                    entry = catalog.AddEntity(baseUri, id, name, uri);
                    break;
                case "linktype":
                    entry = catalog.AddLinktype(baseUri, id, name, uri);
                    break;
                case "notation":
                    entry = catalog.AddNotation(baseUri, id, name, uri);
                    break;
                case "sgmldecl":
                    entry = catalog.AddSgmlDecl(baseUri, id, uri);
                    break;
                default:
                    // This shouldn't happen!
                    break;
            }

            if (reader.HasAttributes) {
                for (var pos = 0; pos < reader.AttributeCount; pos++) {
                    reader.MoveToAttribute(pos);
                    if (ResolverConstants.XMLRESOURCE_EXT_NS.Equals(reader.NamespaceURI)) {
                        entry.SetProperty(reader.LocalName, reader.Value);
                    }
                }
            }

            if (entry == null) {
                entry = new EntryNull();
            }
            
            parserStack.Push(entry);
            baseUriStack.Push(baseUri);
            preferPublicStack.Push(preferPublic);
        }
        
        private void PushNull() {
            parserStack.Push(new EntryNull());
            baseUriStack.Push(baseUriStack.Peek());
            preferPublicStack.Push(preferPublicStack.Peek());
        }
        
        public void SetPreferPublic(bool prefer) {
            _preferPublic = prefer;
        }

        public bool GetPreferPublic() {
            return _preferPublic;
        }

        public void SetArchivedCatalogs(bool archived) {
            _archivedCatalogs = archived;
        }

        public bool GetArchivedCatalogs() {
            return _archivedCatalogs;
        }

        private EntryCatalog ArchiveCatalog(Uri caturi) {
            if (caturi.Scheme != "file") {
                // Only support file: URIs at the moment
                return new EntryCatalog(caturi, null, true);
            }

            string path = caturi.AbsolutePath;
            HashSet<string> catalogSet = new HashSet<string>();
            bool firstEntry = true;
            string leadingDir = null;
            
            ZipArchive zipRead = ZipFile.OpenRead(path);
            foreach (ZipArchiveEntry entry in zipRead.Entries) {
                int pos = entry.FullName.IndexOf("/");
                if (firstEntry) {
                    if (pos >= 0) {
                        leadingDir = entry.FullName.Substring(0, pos);
                    }

                    firstEntry = false;
                }
                else {
                    if (leadingDir != null) {
                        if (pos < 0 || !leadingDir.Equals(entry.FullName.Substring(0, pos))) {
                            leadingDir = null;
                        }
                    }
                }

                if (entry.FullName.EndsWith("catalog.xml")) {
                    catalogSet.Add(entry.FullName);
                }
            }

            string catpath = null;
            if (leadingDir != null) {
                if (catalogSet.Contains(leadingDir + "/catalog.xml")) {
                    catpath = "/" + leadingDir + "/catalog.xml";
                }
                if (catalogSet.Contains(leadingDir + "/org/xmlresolver/catalog.xml")) {
                    catpath = "/" + leadingDir + "/org/xmlresolver/catalog.xml";
                }
            }
            else {
                if (catalogSet.Contains("catalog.xml")) {
                    catpath = "/catalog.xml";
                }
                if (catalogSet.Contains("org/xmlresolver/catalog.xml")) {
                    catpath = "/org/xmlresolver/catalog.xml";
                }
            }

            if (catpath != null) {
                string packuri = caturi.ToString();
                packuri = packuri.Replace(":", "%3A");
                packuri = packuri.Replace("/", ",");
                packuri = "pack://" + packuri + catpath;
                Stream s = UriUtils.GetStream(packuri);
                if (s != null) {
                    return _LoadCatalog(caturi, UriUtils.NewUri(packuri), s);
                }
            }
            
            return new EntryCatalog(caturi, null, true);
        }
    }
}