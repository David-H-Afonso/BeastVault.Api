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
    public void NationalSpecies_RespectsTheSaveSpeciesLimit()
    {
        var species = SavePokedexRules.NationalSpecies(50, 9, 1010);

        Assert.Equal(1010, species.Max());
        Assert.DoesNotContain(1011, species);
    }

    [Fact]
    public void VersionExclusiveSpecies_IsReadFromTheCatalog()
    {
        Assert.True(SavePokedexRules.IsVersionExclusive(50, 1007));
        Assert.False(SavePokedexRules.IsVersionExclusive(51, 1007));
        Assert.True(SavePokedexRules.IsVersionExclusive(51, 1006));
    }
}
