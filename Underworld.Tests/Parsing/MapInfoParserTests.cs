using System.Text;
using Underworld;
using Xunit;

namespace Underworld.Tests.Parsing;

public class MapInfoParserTests
{
    [Fact]
    public void ParseMapInfo_ResolvesLookupAndIncludes()
    {
        var vfs = new VirtualFileSystem();
        const string languageLump = """
[default]
HUSTR_TEST = "Hangar";
;
""";
        vfs.AddFile("LANGUAGE", Encoding.UTF8.GetBytes(languageLump));
        vfs.AddFile("extras.txt", Encoding.UTF8.GetBytes("map MAP02 \"Extra\" { }"));

        var primary = "map MAP01 lookup \"HUSTR_TEST\" { }\ninclude \"extras.txt\"";
        var result = MapInfoParser.ParseMapInfo(vfs, Encoding.UTF8.GetBytes(primary));

        Assert.NotNull(result);
        Assert.Equal("Hangar", result!["MAP01"]);
        Assert.Equal("Extra", result["MAP02"]);
    }

    [Fact]
    public void ParseUMapInfo_ReadsLevelNames()
    {
        var vfs = new VirtualFileSystem();
        var umap = "MAP MAP05 { levelname = \"Cool Base\" }";

        var result = MapInfoParser.ParseUMapInfo(vfs, Encoding.UTF8.GetBytes(umap));

        Assert.NotNull(result);
        Assert.Equal("Cool Base", result!["MAP05"]);
    }

    [Fact]
    public void ParseEMapInfo_ReadsLevelNames()
    {
        var vfs = new VirtualFileSystem();
        var emap = """
[MAP01]
levelname = Entryway

[MAP02]
levelname = Plant
""";

        var result = MapInfoParser.ParseEMapInfo(vfs, Encoding.UTF8.GetBytes(emap));

        Assert.NotNull(result);
        Assert.Equal("Entryway", result!["MAP01"]);
        Assert.Equal("Plant", result["MAP02"]);
    }
}
