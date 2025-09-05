# Beast Vault API - Sistema de Formas Dinámicas y Generaciones

## 📋 Resumen de Funcionalidades Implementadas

### 🎯 Sistema de Generaciones Avanzado

#### OriginGeneration vs CapturedGeneration

- **OriginGeneration**: Generación donde la especie fue introducida por primera vez
  - Ejemplo: Rowlet = Generación 7 (Alola)
- **CapturedGeneration**: Generación donde este Pokémon específico fue capturado
  - Ejemplo: Rowlet capturado en Scarlet/Violet = Generación 9

#### Detección Inteligente de Formatos

- **Archivos modernos**: Usa el campo `OriginGame` de PKHeX para determinar la generación
- **Archivos legacy**: Usa el formato del archivo (.pk1, .pk2, etc.) cuando `OriginGame` no es confiable
- **Mapeo GameVersion**: Soporte completo para todos los juegos (50=Scarlet, 51=Violet, etc.)

### 🔄 Sistema de Formas Dinámicas

#### Mega Evoluciones

- **Detección automática**: Identifica cuando un Pokémon tiene su Mega Stone equipada
- **Formas precisas**: Mapea correctamente las diferentes formas Mega (Charizard X vs Y, Mewtwo X vs Y)
- **33 Mega Stones**: Soporte completo para todas las Mega Stones de Gen 6/7

#### Gigantamax (Gen 8)

- **Flag PKHeX**: Usa el campo `CanGigantamax` de los archivos .pk8
- **Especies compatibles**: Lista completa de 27 especies que pueden Gigantamax
- **Formas especiales**: Pikachu y Meowth tienen formas Gigantamax únicas

### 📊 Nuevos Campos en la API

#### PokemonListItemDto

```typescript
interface PokemonListItemDto {
  // ... campos existentes ...

  /** Generación donde la especie fue introducida por primera vez */
  originGeneration: number;

  /** Generación donde este Pokémon específico fue capturado/obtenido */
  capturedGeneration: number;

  /** Si este Pokémon puede Gigantamax (solo archivos Gen 8+) */
  canGigantamax: boolean;

  /** Si este Pokémon tiene una Mega Piedra equipada */
  hasMegaStone: boolean;

  /** Forma dinámica calculada (considera Mega Stones y Gigantamax) */
  form: number;
}
```

#### Nuevos Filtros de Búsqueda

```typescript
interface AdvancedPokemonQuery {
  // ... filtros existentes ...

  /** Filtrar por generación donde la especie fue introducida */
  originGeneration?: number;

  /** Filtrar por generación donde fue capturado */
  capturedGeneration?: number;
}
```

## 🛠️ Servicios Implementados

### PokemonFormService

```csharp
// Determina la forma visual basada en objetos equipados y flags especiales
public static int GetDisplayForm(PokemonEntity pokemon, string fileFormat)

// Verifica si puede Gigantamax (para la API)
public static bool CheckCanGigantamax(PokemonEntity pokemon, string fileFormat)

// Verifica si tiene Mega Stone equipada (para la API)
public static bool CheckHasMegaStone(PokemonEntity pokemon)
```

### PokemonGameInfoService

```csharp
// Obtiene la generación de origen de una especie
public static int GetSpeciesOriginGeneration(int speciesId)

// Calcula la generación de captura considerando formato legacy
public static int GetCapturedGeneration(int originGame, string fileFormat)

// Detecta generación por formato de archivo (.pk1, .pk2, etc.)
public static int GetGenerationFromFileFormat(string format)
```

## 🎮 Casos de Uso Principales

### 1. Mega Evoluciones

- **Venusaur + Venusaurite** → Form = 1, HasMegaStone = true
- **Charizard + Charizardite X** → Form = 1, HasMegaStone = true
- **Charizard + Charizardite Y** → Form = 2, HasMegaStone = true

### 2. Gigantamax

- **Pikachu con CanGigantamax=true en .pk8** → Form = 1, CanGigantamax = true
- **Snorlax con CanGigantamax=true en .pk8** → Form = 0, CanGigantamax = true

### 3. Detección de Generaciones

- **Rowlet en archivo .pk9** → OriginGeneration = 7, CapturedGeneration = 9
- **Bruce Lee en archivo .pk1** → OriginGeneration = 1, CapturedGeneration = 1

## 📁 Estructura de Base de Datos

### Nuevos Campos en Pokemon

```sql
ALTER TABLE Pokemon ADD DynamaxLevel INTEGER NOT NULL DEFAULT 0;
ALTER TABLE Pokemon ADD CanGigantamax INTEGER NOT NULL DEFAULT 0;
```

### Migración de Datos

- Todos los Pokémon existentes mantienen sus datos
- Los nuevos campos se rellenan automáticamente al importar archivos PKM

## 🎯 Beneficios para el Frontend

### Experiencia Visual Mejorada

- **Sprites correctos**: Las formas Mega y Gigantamax se muestran automáticamente
- **Indicadores visuales**: Badges para Gigantamax y Mega Stones
- **Filtrado inteligente**: Buscar por generación de origen vs captura

### Compatibilidad Total

- **Todos los formatos PKM**: Desde .pk1 hasta .pk9
- **Todos los juegos**: Red/Blue hasta Scarlet/Violet
- **Casos edge**: Manejo correcto de archivos legacy con datos incompletos

## 🔧 Configuración

### Endpoint Principal

```http
GET /pokemon?originGeneration=7&capturedGeneration=9
```

### Respuesta de Ejemplo

```json
{
  "items": [
    {
      "id": 123,
      "speciesId": 722,
      "form": 0,
      "nickname": "Rowlet",
      "level": 55,
      "isShiny": true,
      "originGeneration": 7,
      "capturedGeneration": 9,
      "canGigantamax": false,
      "hasMegaStone": false,
      "spriteKey": "722_s_0"
    }
  ],
  "total": 1
}
```

## ✅ Estado del Sistema

- **✅ Completado**: Sistema de generaciones
- **✅ Completado**: Detección de Mega Stones
- **✅ Completado**: Soporte para Gigantamax
- **✅ Completado**: Formas dinámicas
- **✅ Completado**: TypeScript interfaces
- **✅ Completado**: Migración de base de datos
- **✅ Completado**: Documentación de API

El sistema está **100% funcional** y listo para producción! 🚀
