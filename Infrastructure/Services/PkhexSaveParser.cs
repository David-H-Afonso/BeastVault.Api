using System.Numerics;
using System.Text.Json;
using BeastVault.Api.Domain.Entities;
using PKHeX.Core;

namespace BeastVault.Api.Infrastructure.Services;

public sealed class PkhexSaveParser
{
    public sealed record ParseResult(
        SaveFileEntity SaveFile,
        SaveTrainerEntity Trainer,
        List<SavePokedexEntryEntity> PokedexEntries,
        List<SavePokemonPreviewEntity> PokemonPreviews);

    public Task<ParseResult?> ParseAsync(byte[] bytes, string fileName)
    {
        var originalBytes = bytes.ToArray();
        var sha256 = FileStorageService.ComputeSha256(originalBytes);
        var parseBytes = originalBytes.ToArray();

        return Task.Run(() =>
        {
            var save = LoadCore(parseBytes, fileName);
            if (save is null)
                return null;

            var format = GetFormat(fileName, save);
            var pokedex = ReadPokedex(save);
            var regionalIds = SavePokedexRules.RegionalSpecies((int)save.Version, save.Generation, PkHexStringService.GetVersionName((int)save.Version));
            var trainer = new SaveTrainerEntity
            {
                TrainerName = save.OT ?? string.Empty,
                TrainerId = save.DisplayTID,
                SecretId = save.DisplaySID,
                Gender = save.Gender,
                Language = GetLanguageCode(save.Language),
                Money = save.Money,
                PlayTimeHours = save.PlayedHours,
                PlayTimeMinutes = save.PlayedMinutes,
                PlayTimeSeconds = save.PlayedSeconds,
                BadgeCount = GetBadgeCount(save),
                DexSeen = pokedex.Count(x => x.Seen && regionalIds.Contains(x.SpeciesId)),
                DexCaught = pokedex.Count(x => x.Caught && regionalIds.Contains(x.SpeciesId))
            };

            var entity = new SaveFileEntity
            {
                Sha256 = sha256,
                FileName = fileName,
                OriginalFileName = fileName,
                Format = format,
                Size = originalBytes.LongLength,
                StoredPath = string.Empty,
                RawBlob = originalBytes,
                Generation = save.Generation,
                OriginGame = (int)save.Version,
                GameName = PkHexStringService.GetVersionName((int)save.Version),
                SaveType = GetSaveTypeName(save),
                ChecksumsValid = save.ChecksumsValid,
                Trainer = trainer
            };

            var previews = ReadPokemon(save);
            entity.PokedexEntries = pokedex;
            entity.PokemonPreviews = previews;

            return new ParseResult(entity, trainer, pokedex, previews);
        });
    }

    public SaveFile? Load(byte[] bytes, string fileName)
    {
        return LoadCore(bytes.ToArray(), fileName);
    }

    private static SaveFile? LoadCore(byte[] bytes, string fileName)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(bytes, out var save, fileName) || save is null)
            {
                if (!FileUtil.TryGetMemoryCard(bytes, out var memoryCard) || memoryCard is null ||
                    !SaveUtil.TryGetSaveFile(memoryCard, out save) || save is null)
                {
                    return null;
                }
            }

            save.Metadata.SetExtraInfo(fileName);
            if (save.Generation <= 3)
                SaveLanguage.TryRevise(save);
            return save;
        }
        catch
        {
            return null;
        }
    }

    public static PKM? GetPokemon(SaveFile save, SavePokemonPreviewEntity preview)
    {
        try
        {
            var pokemon = preview.Location switch
            {
                SavePokemonLocation.Party when preview.SlotIndex < save.PartyCount =>
                    save.GetPartySlotAtIndex(preview.SlotIndex),
                SavePokemonLocation.Box when preview.BoxIndex.HasValue && save.HasBox &&
                    preview.BoxIndex.Value < save.BoxCount && preview.SlotIndex < save.BoxSlotCount =>
                    save.GetBoxSlotAtIndex(preview.BoxIndex.Value, preview.SlotIndex),
                _ => null
            };
            return pokemon is { Species: > 0, Valid: true } ? pokemon : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<SavePokedexEntryEntity> ReadPokedex(SaveFile save)
    {
        var result = new List<SavePokedexEntryEntity>();
        if (!save.HasPokeDex)
            return result;

        var maxSpecies = SavePokedexRules.NationalMax(save.Generation);
        for (ushort species = 1; species <= maxSpecies; species++)
        {
            try
            {
                result.Add(new SavePokedexEntryEntity
                {
                    SpeciesId = species,
                    SpeciesName = PkHexStringService.GetSpeciesName(species),
                    Seen = save.GetSeen(species),
                    Caught = save.GetCaught(species)
                });
            }
            catch (ArgumentOutOfRangeException)
            {
                // Some games expose a regional dex smaller than the generation's national range.
            }
        }
        return result;
    }

    private static List<SavePokemonPreviewEntity> ReadPokemon(SaveFile save)
    {
        var result = new List<SavePokemonPreviewEntity>();

        for (var slot = 0; slot < save.PartyCount; slot++)
        {
            try
            {
                AddPreview(result, save.GetPartySlotAtIndex(slot), SavePokemonLocation.Party, null, slot);
            }
            catch
            {
                // A corrupt individual slot should not prevent preserving the save.
            }
        }

        if (!save.HasBox)
            return result;

        for (var box = 0; box < save.BoxCount; box++)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                try
                {
                    AddPreview(result, save.GetBoxSlotAtIndex(box, slot), SavePokemonLocation.Box, box, slot);
                }
                catch
                {
                    // Continue scanning the remaining slots.
                }
            }
        }

        return result;
    }

    private static void AddPreview(
        ICollection<SavePokemonPreviewEntity> result,
        PKM pokemon,
        SavePokemonLocation location,
        int? boxIndex,
        int slotIndex)
    {
        if (pokemon.Species == 0 || !pokemon.Valid)
            return;

        var partyBytes = pokemon.DecryptedPartyData;
        var storedBytes = pokemon.DecryptedBoxData;
        var moves = new[] { pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4 }
            .Where(x => x > 0)
            .Select(move => PkHexStringService.GetMoveName(move))
            .ToList();
        result.Add(new SavePokemonPreviewEntity
        {
            Location = location,
            BoxIndex = boxIndex,
            SlotIndex = slotIndex,
            SpeciesId = pokemon.Species,
            SpeciesName = PkHexStringService.GetSpeciesName(pokemon.Species),
            Nickname = string.IsNullOrWhiteSpace(pokemon.Nickname) ? null : pokemon.Nickname,
            Level = pokemon.CurrentLevel,
            IsShiny = pokemon.IsShiny,
            IsEgg = pokemon.IsEgg,
            Form = pokemon.Form,
            Gender = pokemon.Gender,
            Nature = (int)pokemon.Nature,
            NatureName = PkHexStringService.GetNatureName((int)pokemon.Nature),
            AbilityName = PkHexStringService.GetAbilityName(pokemon.Ability),
            HeldItemName = PkHexStringService.GetItemName(pokemon.HeldItem),
            MovesJson = JsonSerializer.Serialize(moves),
            PokemonHash = FileStorageService.ComputeSha256(partyBytes),
            PokemonStoredHash = FileStorageService.ComputeSha256(storedBytes)
        });
    }

    private static int? GetBadgeCount(SaveFile save)
    {
        return save switch
        {
            SAV1 value => CountBits(value.Badges),
            SAV2 value => CountBits(value.Badges),
            SAV3 value => CountBits(value.Badges),
            SAV4HGSS value => CountBits(value.Badges | (value.Badges16 << 8)),
            SAV4 value => CountBits(value.Badges),
            SAV5 value => CountBits(value.Misc.Badges),
            SAV6 value => CountBits(value.Badges),
            SAV8SWSH value => value.Badges,
            SAV8BS value => value.MyStatus.BadgeCount,
            _ => null
        };
    }

    private static int CountBits(int value) => BitOperations.PopCount((uint)value);

    private static string GetFormat(string fileName, SaveFile save)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension))
            return extension.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(save.Extension) ? "main" : save.Extension.TrimStart('.').ToLowerInvariant();
    }

    private static string GetSaveTypeName(SaveFile save)
    {
        var name = save.GetType().Name;
        return name.StartsWith("SAV", StringComparison.Ordinal) ? name[3..] : name;
    }

    private static string GetLanguageCode(int language)
    {
        return language switch
        {
            1 => "JPN",
            2 => "ENG",
            3 => "FRE",
            4 => "ITA",
            5 => "GER",
            7 or 11 => "SPA",
            8 => "KOR",
            9 => "CHS",
            10 => "CHT",
            _ => "UNK"
        };
    }
}
