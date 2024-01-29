using System.Reflection;
using XmlResolver;
using XmlResolver.Features;
using XmlResolver.Utils;

namespace Tests;

public class MergeHttpsTests
{
    public static CatalogManager mergeManager = null;
    public static CatalogManager noMergeManager = null;

    [SetUp]
    public void Setup()
    {
        // For convenience, this set of tests uses the data catalog because it's known to contain
        // both http: and https: Uris.

        Uri caturi = null;
        try
        {
            Assembly asm = Assembly.Load("XmlResolverData");
            caturi = UriUtils.GetLocationUri("Org.XmlResolver.catalog.xml", asm);
        }
        catch (Exception)
        {
            Assert.Fail();
        }

        string catalog = caturi.ToString();
        List<string> catlist = new List<string>();
        catlist.Add(catalog);

        XmlResolverConfiguration config = new XmlResolverConfiguration(new List<Uri>(), new List<string>());
        config.SetFeature(ResolverFeature.CATALOG_FILES, catlist);
        config.SetFeature(ResolverFeature.URI_FOR_SYSTEM, true);
        config.SetFeature(ResolverFeature.MERGE_HTTPS, true);
        mergeManager = (CatalogManager)config.GetFeature(ResolverFeature.CATALOG_MANAGER);

        config = new XmlResolverConfiguration(new List<Uri>(), new List<string>());
        config.SetFeature(ResolverFeature.CATALOG_FILES, catlist);
        config.SetFeature(ResolverFeature.URI_FOR_SYSTEM, true);
        config.SetFeature(ResolverFeature.MERGE_HTTPS, false);
        noMergeManager = (CatalogManager)config.GetFeature(ResolverFeature.CATALOG_MANAGER);
    }

    [Test]
    public void Equivalentcp1()
    {
        Assert.That("classpath:path/to/thing",
            Is.EqualTo(noMergeManager.NormalizedForComparison("classpath:/path/to/thing")));
    }

    [Test]
    public void Equivalentcp2()
    {
        Assert.That("classpath:path/to/thing",
            Is.EqualTo(noMergeManager.NormalizedForComparison("classpath:path/to/thing")));
    }

    [Test]
    public void Equivalentcp1m()
    {
        Assert.That("classpath:path/to/thing",
            Is.EqualTo(mergeManager.NormalizedForComparison("classpath:/path/to/thing")));
    }

    [Test]
    public void Equivalentcp2m()
    {
        Assert.That("classpath:path/to/thing", Is.EqualTo(mergeManager.NormalizedForComparison("classpath:path/to/thing")));
    }

    [Test]
    public void Equivalenthttp1()
    {
        Assert.That("https://localhost/path/to/thing",
            Is.EqualTo(noMergeManager.NormalizedForComparison("https://localhost/path/to/thing")));
    }

    [Test]
    public void Equivalenthttp2()
    {
        Assert.That("http://localhost/path/to/thing",
            Is.EqualTo(noMergeManager.NormalizedForComparison("http://localhost/path/to/thing")));
    }

    [Test]
    public void Equivalenthttp3()
    {
        Assert.That("ftp://localhost/path/to/thing",
            Is.EqualTo(noMergeManager.NormalizedForComparison("ftp://localhost/path/to/thing")));
    }

    [Test]
    public void Equivalenthttp1m()
    {
        Assert.That("https://localhost/path/to/thing",
            Is.EqualTo(mergeManager.NormalizedForComparison("https://localhost/path/to/thing")));
    }

    [Test]
    public void Equivalenthttp2m()
    {
        Assert.That("https://localhost/path/to/thing",
            Is.EqualTo(mergeManager.NormalizedForComparison("http://localhost/path/to/thing")));
    }

    [Test]
    public void Equivalenthttp3m()
    {
        Assert.That("ftp://localhost/path/to/thing",
            Is.EqualTo(mergeManager.NormalizedForComparison("ftp://localhost/path/to/thing")));
    }


    [Test]
    public void LookupHttpUri()
    {
        Uri result = mergeManager.LookupUri("http://www.w3.org/2001/xml.xsd");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void LookupHttpSystem()
    {
        Uri result = mergeManager.LookupSystem("http://www.w3.org/MarkUp/DTD/xhtml-basic11.dtd");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void LookupHttpsSystem()
    {
        Uri result = mergeManager.LookupSystem("https://www.rddl.org/rddl-xhtml.dtd");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void LookupHttpUriNoMerge()
    {
        Uri result = noMergeManager.LookupUri("http://www.w3.org/2001/xml.xsd");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void LookupHttpSystemNoMerge()
    {
        Uri result = noMergeManager.LookupSystem("http://www.w3.org/MarkUp/DTD/xhtml-basic11.dtd");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void LookupHttpsSystemNoMerge()
    {
        Uri result = noMergeManager.LookupSystem("https://www.rddl.org/rddl-xhtml.dtd");
        Assert.That(result, Is.Null);
    }
}