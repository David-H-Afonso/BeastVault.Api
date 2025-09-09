using System.Text;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Domain.ValueObjects
{
    public static class ShowdownExport
    {
        public static string From(PokemonEntity p, StatsEntity? s, List<MoveEntity> moves)
        {
            var sb = new StringBuilder();
            // Usar PKHeX.Core strings en lugar de nuestros enums
            var speciesName = PkHexStringService.GetSpeciesName(p.SpeciesId, p.Form);
            var ballName = PkHexStringService.GetBallName(p.BallId);
            var abilityName = PkHexStringService.GetAbilityName(p.AbilityId);
            var natureName = PkHexStringService.GetNatureName(p.Nature);
            string? teraTypeName = p.TeraType.HasValue ? PkHexStringService.GetTypeName(p.TeraType.Value) : null;

            // Nickname (Species) (M/F/Genderless) @ Item (0=Male, 1=Female, 2=Genderless)
            string genderSuffix = p.Gender == 0 ? " (M)" : p.Gender == 1 ? " (F)" : " (Genderless)";
            var nickname = string.IsNullOrWhiteSpace(p.Nickname) ? speciesName : $"{p.Nickname} ({speciesName})";
            sb.Append($"{nickname}{genderSuffix}");
            if (p.HeldItemId > 0)
            {
                var itemName = PkHexStringService.GetItemName(p.HeldItemId);
                sb.Append($" @ {itemName}");
            }
            sb.AppendLine();
            sb.AppendLine($"Ability: {abilityName}");
            if (teraTypeName != null) sb.AppendLine($"Tera Type: {teraTypeName}");
            sb.AppendLine($"Level: {p.Level}");
            if (p.IsShiny) sb.AppendLine("Shiny: Yes");
            sb.AppendLine($"{natureName} Nature");
            sb.AppendLine($"Ball: {ballName}");
            // OT block multiline
            sb.AppendLine($"OT: {p.OtName}");
            sb.AppendLine($"TID: {p.Tid}");
            sb.AppendLine($"SID: {p.Sid}");
            // OTGender: 0=male, 1=female
            string otGenderStr = p.OTGender == 0 ? "Male" : p.OTGender == 1 ? "Female" : "Unknown";
            sb.AppendLine($"OTGender: {otGenderStr}");
            sb.AppendLine($"Language: {PkHexStringService.GetLanguageFullName(p.OTLanguage)}");
            if (s != null)
            {
                sb.AppendLine($"IVs: {s.IvHp} HP / {s.IvAtk} Atk / {s.IvDef} Def / {s.IvSpa} SpA / {s.IvSpd} SpD / {s.IvSpe} Spe");
                sb.AppendLine($"EVs: {s.EvHp} HP / {s.EvAtk} Atk / {s.EvDef} Def / {s.EvSpa} SpA / {s.EvSpd} SpD / {s.EvSpe} Spe");
            }
            foreach (var m in moves.OrderBy(x => x.Slot))
            {
                var moveName = PkHexStringService.GetMoveName(m.MoveId);
                sb.AppendLine($"- {moveName}");
            }
            return sb.ToString();
        }
    }
}
