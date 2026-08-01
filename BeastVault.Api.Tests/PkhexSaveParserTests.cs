using System.Security.Cryptography;
using BeastVault.Api.Infrastructure.Services;
using PKHeX.Core;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class PkhexSaveParserTests
{
    [Theory]
    [InlineData(GameVersion.B, 5, "Black")]
    [InlineData(GameVersion.BD, 8, "Brilliant Diamond")]
    public async Task ParseAsync_ReadsRepresentativeSaveMetadata(
        GameVersion version,
        int generation,
        string gameName)
    {
        var save = BlankSaveFile.Get(version, "UNIT", LanguageID.English);
        save.PlayedHours = 42;
        save.PlayedMinutes = 17;
        var bytes = save.Write().ToArray();

        var result = await new PkhexSaveParser().ParseAsync(bytes, "main");

        Assert.NotNull(result);
        Assert.Equal(generation, result.SaveFile.Generation);
        Assert.Equal(gameName, result.SaveFile.GameName);
        Assert.Equal("UNIT", result.Trainer.TrainerName);
        Assert.Equal(42, result.Trainer.PlayTimeHours);
        Assert.Equal(17, result.Trainer.PlayTimeMinutes);
        Assert.True(result.SaveFile.ChecksumsValid);
    }

    [Fact]
    public async Task ParseAsync_ReadsGenerationOneSave()
    {
        var save = (SAV1)BlankSaveFile.Get(GameVersion.RD, "RED", LanguageID.English);
        save.Data[0x2F2D] = 0xFF;
        save.Data[0x30C1] = 0xFF;
        var bytes = save.Write().ToArray();

        var result = await new PkhexSaveParser().ParseAsync(bytes, "pokemon-red.sav");

        Assert.NotNull(result);
        Assert.Equal(1, result.SaveFile.Generation);
        Assert.Equal("RED", result.Trainer.TrainerName);
        Assert.Equal("sav", result.SaveFile.Format);
        Assert.Equal(151, result.PokedexEntries.Count);
    }

    [Fact]
    public async Task ParseAsync_AndLoad_DoNotMutateCallerBytes()
    {
        var save = BlankSaveFile.Get(GameVersion.BD, "DAWN", LanguageID.English);
        var bytes = save.Write().ToArray();
        var expected = bytes.ToArray();
        var expectedHash = Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant();
        var parser = new PkhexSaveParser();

        var result = await parser.ParseAsync(bytes, "main");

        Assert.NotNull(result);
        Assert.Equal(expected, bytes);
        Assert.Equal(expected, result.SaveFile.RawBlob);
        Assert.NotSame(bytes, result.SaveFile.RawBlob);
        Assert.Equal(expectedHash, result.SaveFile.Sha256);

        var loadBytes = expected.ToArray();
        Assert.NotNull(parser.Load(loadBytes, "main"));
        Assert.Equal(expected, loadBytes);
    }

    [Fact]
    public async Task ParseAsync_ReturnsNullForUnknownData()
    {
        var result = await new PkhexSaveParser().ParseAsync([1, 2, 3, 4], "invalid.sav");
        Assert.Null(result);
    }
}
