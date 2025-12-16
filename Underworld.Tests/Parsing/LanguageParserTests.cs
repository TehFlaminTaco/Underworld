using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Underworld;
using Underworld.Models;
using Xunit;

namespace Underworld.Tests.Parsing;

public class LanguageParserTests
{
    [Fact]
    public void ParseLanguageCSV_ReadsIdentifierColumn()
    {
        const string csv = "Identifier,default\nHUSTR_1,Hangar\nHUSTR_2,Plant\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        var dict = LanguageParser.ParseLanguageCSV(reader);

        Assert.Equal("Hangar", dict["HUSTR_1"]);
        Assert.Equal("Plant", dict["HUSTR_2"]);
    }

    [Fact]
    public void ParseLanguageLump_ResolvesLookupChainAndGlobalFallback()
    {
        WadLists.GLOBAL_LANGUAGE = new Dictionary<string, string>
        {
            ["GLOBAL_NAME"] = "Global Replacement"
        };

        const string lump = """
[default]
BASE = "Base";
CHAIN = "$BASE";
GLOBAL_REF = "$GLOBAL_NAME";
;
[eng]
CHAIN = "$GLOBAL_REF";
FINAL = "$CHAIN";
;
""";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(lump)));

        var dict = LanguageParser.ParseLanguageLump(reader);

        Assert.Equal("Base", dict["BASE"]);
        Assert.Equal("Global Replacement", dict["CHAIN"]);
        Assert.Equal("Global Replacement", dict["FINAL"]);
    }

    [Fact]
    public void ParseLanguage_PrefersCsvOverLanguageLumps()
    {
        var vfs = new VirtualFileSystem();
        vfs.AddFile("language.csv", Encoding.UTF8.GetBytes("Identifier,default\nHUSTR_A,CSV Value\n"));
        vfs.AddFile("LANGUAGE", Encoding.UTF8.GetBytes("[default]\nHUSTR_A = \"Lump Value\";"));

        var dict = LanguageParser.ParseLanguage(vfs);

        Assert.NotNull(dict);
        Assert.Equal("CSV Value", dict!["HUSTR_A"]);
    }
}
