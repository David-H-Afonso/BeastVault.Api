using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Infrastructure.Services;
using PKHeX.Core;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class PokemonParsingAndFilterTests
{
    [Fact]
    public async Task ParseStoredPk8_CalculatesStatsAndMapsSpanishDateAndGender()
    {
        var pokemon = new PK8
        {
            Species = 874,
            Version = GameVersion.SW,
            Language = (int)LanguageID.Spanish,
            OriginalTrainerName = "Rikku",
            OriginalTrainerGender = 1,
            Gender = 0,
            Nature = Nature.Naive,
            MetYear = 25,
            MetMonth = 8,
            MetDay = 24,
            MetLevel = 63,
            TID16 = 9363,
            SID16 = 42,
            IV_HP = 20,
            IV_ATK = 21,
            IV_DEF = 22,
            IV_SPE = 23,
            IV_SPA = 24,
            IV_SPD = 25,
        };
        pokemon.EXP = Experience.GetEXP(63, pokemon.PersonalInfo.EXPGrowth);
        pokemon.RefreshChecksum();

        var bytes = pokemon.DecryptedBoxData;
        var original = bytes.ToArray();
        var result = await new PkhexCoreParser().ParseAsync(bytes, "stonjourner.pk8");

        Assert.NotNull(result);
        Assert.Equal("SPA", result.Pokemon.Language);
        Assert.Equal("Male", result.Pokemon.Gender == 0 ? "Male" : "Other");
        Assert.Equal(new DateTime(2025, 8, 24), result.Pokemon.MetDate);
        Assert.Equal(pokemon.GetStats(pokemon.PersonalInfo)[0], result.Stats!.StatHp);
        Assert.InRange(result.Stats.StatHp, 1, 999);
        Assert.InRange(result.Stats.StatAtk, 1, 999);
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void MainCollectionFilters_ApplyOriginCapturedSidAndOtTogether()
    {
        var pokemon = new[]
        {
            new PokemonEntity { SpeciesId = 874, OriginGame = 44, Sid = 42, OtName = "Rikku" },
            new PokemonEntity { SpeciesId = 25, OriginGame = 50, Sid = 42, OtName = "Rikku" },
            new PokemonEntity { SpeciesId = 875, OriginGame = 44, Sid = 7, OtName = "Other" },
        }.AsQueryable();

        var result = PokemonQueryService.BuildQuery(pokemon, new AdvancedPokemonQuery
        {
            OriginRegion = "Galar",
            CapturedRegion = "Galar",
            Sid = 42,
            OtName = "rik"
        }).ToList();

        Assert.Single(result);
        Assert.Equal(874, result[0].SpeciesId);
    }
}
