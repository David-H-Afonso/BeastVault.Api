using BeastVault.Api.Infrastructure.Services;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class SavePokedexRulesTests
{
    [Theory]
    [InlineData(35, 151)]
    [InlineData(12, 210)]
    [InlineData(24, 454)]
    [InlineData(44, 400)]
    [InlineData(47, 242)]
    [InlineData(50, 400)]
    [InlineData(52, 232)]
    public void RegionalSpecies_UsesTheConfiguredDex(int originGame, int expectedCount)
    {
        var species = SavePokedexRules.RegionalSpecies(originGame, 9, string.Empty);

        Assert.Equal(expectedCount, species.Count);
    }

    [Fact]
    public void CompatibilitySpecies_RespectsTheSaveSpeciesLimit()
    {
        var species = SavePokedexRules.CompatibilitySpecies(50, 9, 1010);

        Assert.Equal(1010, species.Max());
        Assert.DoesNotContain(1011, species);
    }

    [Fact]
    public void GroupedSaveVersionsUseTheSameDexAsTheirConcreteGames()
    {
        var heartGoldRegional = SavePokedexRules.RegionalSpecies(65, 4, string.Empty);
        var heartGoldNational = SavePokedexRules.ExpandedSpecies(65, 4, 493);
        var goldRegional = SavePokedexRules.RegionalSpecies(55, 2, string.Empty);
        var goldNational = SavePokedexRules.ExpandedSpecies(55, 2, 251);

        Assert.Equal(256, heartGoldRegional.Count);
        Assert.Contains(179, heartGoldNational);
        Assert.Equal(493, heartGoldNational.Max());
        Assert.Equal(251, goldRegional.Count);
        Assert.Contains(179, goldNational);
        Assert.Equal(251, goldNational.Max());
    }

    [Fact]
    public void ScarletSeparatesRegionalDlcAndCompatibilitySets()
    {
        var regional = SavePokedexRules.RegionalSpecies(50, 9, "Scarlet");
        var expanded = SavePokedexRules.ExpandedSpecies(50, 9);
        var compatibility = SavePokedexRules.CompatibilitySpecies(50, 9);

        Assert.Equal(400, regional.Count);
        Assert.Equal(664, expanded.Count);
        Assert.Equal(733, compatibility.Count);
        Assert.True(compatibility.Count < SavePokedexRules.NationalMax(9));
        Assert.NotEmpty(compatibility.Except(expanded));
    }

    [Fact]
    public void VersionExclusiveSpecies_IsReadFromTheCatalog()
    {
        Assert.True(SavePokedexRules.IsVersionExclusive(50, 1007));
        Assert.False(SavePokedexRules.IsVersionExclusive(51, 1007));
        Assert.True(SavePokedexRules.IsVersionExclusive(51, 1006));
    }

    [Theory]
    [InlineData(1, 270)]
    [InlineData(2, 273)]
    [InlineData(3, 52)]
    [InlineData(4, 23)]
    [InlineData(5, 27)]
    [InlineData(7, 56)]
    [InlineData(8, 37)]
    [InlineData(10, 86)]
    [InlineData(11, 79)]
    [InlineData(12, 79)]
    [InlineData(20, 10)]
    [InlineData(21, 13)]
    [InlineData(22, 10)]
    [InlineData(23, 13)]
    [InlineData(24, 6)]
    [InlineData(25, 6)]
    [InlineData(26, 138)]
    [InlineData(27, 140)]
    [InlineData(30, 37)]
    [InlineData(31, 27)]
    [InlineData(32, 37)]
    [InlineData(33, 20)]
    [InlineData(35, 13)]
    [InlineData(36, 13)]
    [InlineData(37, 13)]
    [InlineData(38, 27)]
    [InlineData(39, 56)]
    [InlineData(40, 37)]
    [InlineData(41, 52)]
    [InlineData(42, 25)]
    [InlineData(43, 23)]
    [InlineData(44, 83)]
    [InlineData(45, 77)]
    [InlineData(48, 10)]
    [InlineData(49, 13)]
    [InlineData(50, 27)]
    [InlineData(51, 27)]
    public void ConcreteGameVersionsHaveExclusiveSpecies(int originGame, int speciesId)
    {
        Assert.True(SavePokedexRules.IsVersionExclusive(originGame, speciesId));
    }

    [Theory]
    [InlineData(53)]
    [InlineData(54)]
    [InlineData(56)]
    [InlineData(58)]
    [InlineData(65)]
    [InlineData(74)]
    [InlineData(76)]
    public void GroupedVersionsDoNotInventExclusiveSpecies(int originGame)
    {
        Assert.False(SavePokedexRules.IsVersionExclusive(originGame, 27));
    }
}
