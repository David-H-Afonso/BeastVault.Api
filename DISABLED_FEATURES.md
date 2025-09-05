# Pokemon API - Disabled Features

This document tracks temporarily disabled features in the Pokemon API that need to be fixed.

## Last Updated

August 17, 2025

## Disabled Filters

### Gender Filter

- **Status**: Temporarily disabled
- **Issue**: Database type conversion and query generation issues
- **Location**: `PokemonEndpoints.cs` metadata endpoint
- **Code**:

```csharp
var genders = new[]
{
    new { Id = 0, Name = "Unknown" },
    new { Id = 1, Name = "Male" },
    new { Id = 2, Name = "Female" }
};
```

### Form Filter

- **Status**: Temporarily disabled
- **Issue**: Field mapping and query generation issues
- **Location**: Form-based filtering in advanced query
- **Notes**: Form field exists but filtering logic needs debugging

### Held Item Filter

- **Status**: Temporarily disabled
- **Issue**: Complex item ID mapping and query generation
- **Location**: Held item filtering in advanced query
- **Notes**: HeldItemId field exists but filtering needs optimization

## Disabled Sort Fields

### SpeciesName

- **Status**: Temporarily disabled
- **Issue**: Requires PKHeX species name resolution for proper sorting
- **Enum Value**: `PokemonSortField.SpeciesName = 2`
- **Notes**: Currently using SpeciesId as proxy, but needs actual species name lookup

### OriginGeneration

- **Status**: Temporarily disabled
- **Issue**: Complex generation mapping from OriginGame values
- **Enum Value**: `PokemonSortField.OriginGeneration = 5`
- **Notes**: Generation calculation logic exists but has edge cases

### CapturedGeneration

- **Status**: Temporarily disabled
- **Issue**: Complex generation mapping from species introduction generations
- **Enum Value**: `PokemonSortField.CapturedGeneration = 6`
- **Notes**: Species-to-generation mapping needs refinement

### Gender

- **Status**: Temporarily disabled
- **Issue**: Database type conversion issues with Gender field
- **Enum Value**: `PokemonSortField.Gender = 8`
- **Notes**: Gender field exists but sorting has type conversion problems

### IsShiny

- **Status**: Temporarily disabled
- **Issue**: Boolean to integer conversion for database sorting
- **Enum Value**: `PokemonSortField.IsShiny = 9`
- **Notes**: Boolean fields need explicit conversion to int for proper sorting

### Form

- **Status**: Temporarily disabled
- **Issue**: Form field mapping and sorting issues
- **Enum Value**: `PokemonSortField.Form = 10`
- **Notes**: Form field exists but sorting logic needs debugging

### CreatedAt

- **Status**: Temporarily disabled
- **Issue**: No actual CreatedAt field in database
- **Enum Value**: `PokemonSortField.CreatedAt = 11`
- **Notes**: Currently using Id as proxy for creation time

### Favorite

- **Status**: Temporarily disabled
- **Issue**: Boolean to integer conversion for database sorting
- **Enum Value**: `PokemonSortField.Favorite = 12`
- **Notes**: Boolean fields need explicit conversion to int for proper sorting

## Currently Working Features

### Working Sort Fields

- **Id**: Primary key sorting - works correctly
- **PokedexNumber**: Species ID sorting - works correctly
- **Nickname**: Text field sorting - works correctly
- **Level**: Integer field sorting - works correctly
- **Pokeball**: Ball ID sorting - works correctly

### Working Filters

- **Types**: Primary/Secondary type filtering - works correctly
- **Generations**: Generation-based filtering - works correctly
- **Level Range**: Min/Max level filtering - works correctly
- **Shiny Status**: Boolean filtering - works correctly
- **Text Search**: Nickname/species search - works correctly

## Fixes Needed

1. **Boolean Field Sorting**: Implement proper boolean to integer conversion for sorting
2. **Generation Mapping**: Refine generation calculation logic for edge cases
3. **Species Name Resolution**: Add proper species name lookup for sorting
4. **Database Schema**: Consider adding CreatedAt timestamp field
5. **Form Handling**: Debug form field mapping and filtering
6. **Gender Processing**: Fix gender field type handling
7. **Item Mapping**: Optimize held item filtering performance

## Testing Required

After fixes are implemented, test with:

- Various Pokemon with different genders
- Pokemon with different forms (Mega, Alolan, Galarian, etc.)
- Pokemon holding different items
- Pokemon from different generations
- Shiny and non-shiny Pokemon
- Different sort combinations
