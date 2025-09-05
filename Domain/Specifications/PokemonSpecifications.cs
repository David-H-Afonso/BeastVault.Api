using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Domain.Specifications;

/// <summary>
/// Specification for filtering Pokemon by Pokedex number (Species ID)
/// </summary>
public class PokedexNumberSpecification : IPokemonSpecification
{
    private readonly int _pokedexNumber;

    public PokedexNumberSpecification(int pokedexNumber)
    {
        _pokedexNumber = pokedexNumber;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.SpeciesId == _pokedexNumber);
    }
}

/// <summary>
/// Specification for filtering Pokemon by origin generation (when species was introduced)
/// </summary>
public class OriginGenerationSpecification : IPokemonSpecification
{
    private readonly int _generation;

    public OriginGenerationSpecification(int generation)
    {
        _generation = generation;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        var speciesInGeneration = PokemonGameInfoService.GetSpeciesInGeneration(_generation);
        return query.Where(p => speciesInGeneration.Contains(p.SpeciesId));
    }
}

/// <summary>
/// Specification for filtering Pokemon by captured generation (when Pokemon was caught)
/// </summary>
public class CapturedGenerationSpecification : IPokemonSpecification
{
    private readonly int _generation;

    public CapturedGenerationSpecification(int generation)
    {
        _generation = generation;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        var games = PokemonGameInfoService.GetGamesByGeneration(_generation);
        var gameIds = games.Select(g => g.GameId).ToList();
        return query.Where(p => gameIds.Contains(p.OriginGame));
    }
}

/// <summary>
/// Specification for filtering Pokemon by Pokeball
/// </summary>
public class PokeballSpecification : IPokemonSpecification
{
    private readonly int _ballId;

    public PokeballSpecification(int ballId)
    {
        _ballId = ballId;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.BallId == _ballId);
    }
}

/// <summary>
/// Specification for filtering Pokemon by species name
/// </summary>
public class SpeciesNameSpecification : IPokemonSpecification
{
    private readonly string _speciesName;

    public SpeciesNameSpecification(string speciesName)
    {
        _speciesName = speciesName;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        var speciesIds = PokemonGameInfoService.GetSpeciesIdsByName(_speciesName);
        return query.Where(p => speciesIds.Contains(p.SpeciesId));
    }
}

/// <summary>
/// Specification for filtering Pokemon by nickname
/// </summary>
public class NicknameSpecification : IPokemonSpecification
{
    private readonly string _nickname;

    public NicknameSpecification(string nickname)
    {
        _nickname = nickname;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => (p.Nickname ?? "").Contains(_nickname));
    }
}

/// <summary>
/// Specification for filtering Pokemon by type with advanced options
/// </summary>
public class TypeSpecification : IPokemonSpecification
{
    private readonly TypeFilterOptions _options;

    public TypeSpecification(TypeFilterOptions options)
    {
        _options = options;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        var speciesWithTypes = PokemonGameInfoService.GetSpeciesWithTypes(_options);
        return query.Where(p => speciesWithTypes.Contains(p.SpeciesId));
    }
}

/// <summary>
/// Specification for filtering Pokemon by gender
/// </summary>
public class GenderSpecification : IPokemonSpecification
{
    private readonly int _gender;

    public GenderSpecification(int gender)
    {
        _gender = gender;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.Gender == _gender);
    }
}

/// <summary>
/// Specification for filtering Pokemon by shiny status
/// </summary>
public class ShinySpecification : IPokemonSpecification
{
    private readonly bool _isShiny;

    public ShinySpecification(bool isShiny)
    {
        _isShiny = isShiny;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.IsShiny == _isShiny);
    }
}

/// <summary>
/// Specification for filtering Pokemon by form
/// </summary>
public class FormSpecification : IPokemonSpecification
{
    private readonly int _form;

    public FormSpecification(int form)
    {
        _form = form;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.Form == _form);
    }
}

/// <summary>
/// Specification for text search across multiple fields
/// </summary>
public class TextSearchSpecification : IPokemonSpecification
{
    private readonly string _searchText;

    public TextSearchSpecification(string searchText)
    {
        _searchText = searchText;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p =>
            (p.Nickname ?? "").Contains(_searchText) ||
            p.OtName.Contains(_searchText) ||
            (p.Notes ?? "").Contains(_searchText));
    }
}

/// <summary>
/// Specification for filtering Pokemon by level range
/// </summary>
public class LevelRangeSpecification : IPokemonSpecification
{
    private readonly int? _minLevel;
    private readonly int? _maxLevel;

    public LevelRangeSpecification(int? minLevel, int? maxLevel)
    {
        _minLevel = minLevel;
        _maxLevel = maxLevel;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        if (_minLevel.HasValue)
            query = query.Where(p => p.Level >= _minLevel.Value);

        if (_maxLevel.HasValue)
            query = query.Where(p => p.Level <= _maxLevel.Value);

        return query;
    }
}

/// <summary>
/// Specification for filtering Pokemon by origin game (legacy support)
/// </summary>
public class LegacyOriginGameSpecification : IPokemonSpecification
{
    private readonly int _originGame;

    public LegacyOriginGameSpecification(int originGame)
    {
        _originGame = originGame;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.OriginGame == _originGame);
    }
}

/// <summary>
/// Specification for filtering Pokemon by Tera type
/// </summary>
public class TeraTypeSpecification : IPokemonSpecification
{
    private readonly int _teraType;

    public TeraTypeSpecification(int teraType)
    {
        _teraType = teraType;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.TeraType == _teraType);
    }
}

/// <summary>
/// Specification for filtering Pokemon by held item
/// </summary>
public class HeldItemSpecification : IPokemonSpecification
{
    private readonly int _heldItemId;

    public HeldItemSpecification(int heldItemId)
    {
        _heldItemId = heldItemId;
    }

    public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
    {
        return query.Where(p => p.HeldItemId == _heldItemId);
    }
}
