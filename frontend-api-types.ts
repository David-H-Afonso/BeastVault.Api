/**
 * BEAST VAULT API - TypeScript Interfaces
 *
 * Este archivo contiene TODAS las interfaces TypeScript para el frontend
 * que corresponden exactamente a los endpoints y DTOs de la API de Beast Vault.
 *
 * Generado automáticamente el: 17 de Agosto, 2025
 *
 * NUEVAS FUNCIONALIDADES:
 * - ✅ Detección automática de generaciones (OriginGeneration vs CapturedGeneration)
 * - ✅ Formas dinámicas basadas en Mega Stones y Gigantamax
 * - ✅ Flags CanGigantamax y HasMegaStone para mejor experiencia visual
 * - ✅ Soporte completo para archivos PKM legacy (.pk1, .pk2, etc.)
 *
 * IMPORTANTE: Este archivo debe actualizarse cada vez que cambien los endpoints o DTOs
 */

// ===================================
// TIPOS BÁSICOS Y ENUMS
// ===================================

export type ImportStatus = "imported" | "duplicate" | "error";

export type FileFormat =
  | "pk1"
  | "pk2"
  | "pk3"
  | "pk4"
  | "pk5"
  | "pk6"
  | "pk7"
  | "pk8"
  | "pk9"
  | "pb7"
  | "pb8"
  | "ek1"
  | "ek2"
  | "ek3"
  | "ek4"
  | "ek5"
  | "ek6"
  | "ek7"
  | "ek8"
  | "ek9"
  | "ekx";

export enum TypeFilterMode {
  HasAnyType = 0,
  HasAllTypes = 1,
  HasOnlyTypes = 2,
  PrimaryTypeOnly = 3,
  ExactTypeOrder = 4,
  BothTypesAnyOrder = 5,
}

export enum PokemonSortField {
  // Working sort fields (available in metadata)
  Id = 0,
  PokedexNumber = 1,
  Nickname = 3,
  Level = 4,
  Pokeball = 7,

  // Temporarily disabled sort fields (not in metadata, needs fixes)
  SpeciesName = 2, // Requires PKHeX species name resolution
  OriginGeneration = 5, // Complex generation mapping issues
  CapturedGeneration = 6, // Complex generation mapping issues
  Gender = 8, // Database type conversion issues
  IsShiny = 9, // Boolean to int conversion issues
  Form = 10, // Field mapping issues
  CreatedAt = 11, // No actual CreatedAt field in database
  Favorite = 12, // Boolean to int conversion issues
}

export enum SortDirection {
  Ascending = 0,
  Descending = 1,
}

export enum Gender {
  Unknown = 0,
  Male = 1,
  Female = 2,
}

// ===================================
// INTERFACES DE RESULTADO
// ===================================

export interface PagedResult<T> {
  items: T[];
  total: number;
}

export interface ImportResultDto {
  /** Nombre del archivo subido */
  fileName: string;
  /** Estado del import: "imported", "duplicate", o "error" */
  status: ImportStatus;
  /** ID del Pokémon creado (solo si status es "imported") */
  pokemonId?: number;
  /** Mensaje de error (solo si status es "error") */
  message?: string;
}

// ===================================
// INTERFACES DE CONSULTA (QUERY)
// ===================================

export interface PokemonQuery {
  /** Búsqueda de texto en nickname o nombre del entrenador original */
  search?: string;
  /** Filtrar por ID de especie (ej: 1 = Bulbasaur) */
  speciesId?: number;
  /** Filtrar por ID de forma (ej: 0 = Normal, 1 = Alolan, 2 = Galarian) */
  form?: number;
  /** Filtrar por Pokémon shiny */
  isShiny?: boolean;
  /** Filtrar por ID de Pokébola */
  ballId?: number;
  /** Filtrar por juego de origen */
  originGame?: number;
  /** Filtrar por tipo Tera (Gen 9) */
  teraType?: number;
  /** Número de elementos a saltar (paginación) */
  skip?: number;
  /** Número de elementos a devolver (máximo recomendado: 100) */
  take?: number;
}

export interface AdvancedPokemonQuery {
  // Filtros básicos
  /** Búsqueda de texto en nickname, nombre OT y notas */
  search?: string;
  /** Filtrar por número específico de la Pokédex (Species ID) */
  pokedexNumber?: number;
  /** Filtrar por nombre de especie (coincidencia parcial) */
  speciesName?: string;
  /** Filtrar por nickname (coincidencia parcial) */
  nickname?: string;
  /** Filtrar por estado shiny */
  isShiny?: boolean;
  /** Filtrar por ID de forma */
  form?: number;
  /** Filtrar por género (0 = indefinido, 1 = macho, 2 = hembra) */
  gender?: number;

  // Filtros de generación
  /** Filtrar por generación donde la especie fue introducida (ej: Rowlet = 7) */
  originGeneration?: number;
  /** Filtrar por generación donde fue capturado (ej: Rowlet en SV = 9) */
  capturedGeneration?: number;

  // Filtros de equipamiento
  /** Filtrar por ID de Pokébola */
  pokeballId?: number;
  /** Filtrar por ID de objeto equipado */
  heldItemId?: number;

  // Filtros de tipo
  /** ID del tipo primario para filtrado de tipos */
  primaryType?: number;
  /** ID del tipo secundario para filtrado de tipos */
  secondaryType?: number;
  /** Modo de filtro de tipos (cómo aplicar los filtros de tipo) */
  typeFilterMode?: TypeFilterMode;
  /** Si enforcar el orden exacto de tipos para filtrado de doble tipo */
  enforceTypeOrder?: boolean;

  // Filtros de nivel y estadísticas
  /** Filtro de nivel mínimo */
  minLevel?: number;
  /** Filtro de nivel máximo */
  maxLevel?: number;

  // Ordenamiento
  /** Campo de ordenamiento primario */
  sortBy?: PokemonSortField;
  /** Dirección de ordenamiento primario */
  sortDirection?: SortDirection;
  /** Campo de ordenamiento secundario (opcional) */
  thenSortBy?: PokemonSortField;
  /** Dirección de ordenamiento secundario */
  thenSortDirection?: SortDirection;

  // Paginación
  /** Número de elementos a saltar (para paginación) */
  skip?: number;
  /** Número de elementos a tomar (máximo recomendado: 100) */
  take?: number;

  // Soporte legacy
  /** @deprecated Usar pokedexNumber en su lugar */
  speciesId?: number;
  /** @deprecated Usar pokeballId en su lugar */
  ballId?: number;
  /** Filtro legacy de juego de origen */
  originGame?: number;
  /** Filtro legacy de tipo Tera */
  teraType?: number;

  // Filtros de tags
  /** IDs de tags que el Pokémon DEBE tener (todos los tags especificados) */
  tagIds?: number[];
  /** Nombres de tags que el Pokémon DEBE tener (todos los tags especificados) */
  tagNames?: string[];
  /** IDs de tags donde el Pokémon PUEDE tener cualquiera de ellos */
  anyTagIds?: number[];
  /** Nombres de tags donde el Pokémon PUEDE tener cualquiera de ellos */
  anyTagNames?: string[];
  /** Filtrar Pokémon que no tienen ningún tag */
  hasNoTags?: boolean;
}

export interface UpdatePokemonDto {
  /** Marcar o desmarcar como favorito (null = sin cambio) */
  favorite?: boolean;
  /** Notas personales sobre el Pokémon (null = sin cambio, string.Empty = limpiar) */
  notes?: string;
}

// ===================================
// INTERFACES DE RESPUESTA DE DATOS
// ===================================

export interface PokemonListItemDto {
  /** ID único del Pokémon en la base de datos */
  id: number;
  /** ID de especie (ej: 1 = Bulbasaur, 25 = Pikachu) */
  speciesId: number;
  /** Nombre de la especie (ej: "Bulbasaur", "Pikachu") */
  speciesName: string;
  /** ID de forma (ej: 0 = Meowth Normal, 1 = Meowth de Alola, 2 = Meowth de Galar) */
  form: number;
  /** Nombre de la forma (ej: "Alolan", "Galarian", "Mega", "Crowned", "Gigantamax") */
  formName: string;
  /** Nickname del Pokémon (null si usa el nombre de la especie) */
  nickname?: string;
  /** Nivel del Pokémon (1-100) */
  level: number;
  /** Si es shiny */
  isShiny: boolean;
  /** ID de la Pokébola en la que fue capturado */
  ballId: number;
  /** Tipo Tera (Gen 9), null si no aplica */
  teraType?: number;
  /** ID del objeto equipado (importante para cambios de forma como Zacian/Zamazenta) */
  heldItemId: number;
  /** Género: 0 = Macho, 1 = Hembra, 2 = Sin género */
  gender: number;
  /** Clave para identificar el sprite (especie+forma+shiny) */
  spriteKey: string;
  /** Generación donde la especie fue introducida por primera vez (campo calculado) */
  originGeneration: number;
  /** Generación donde este Pokémon específico fue capturado/obtenido (campo calculado) */
  capturedGeneration: number;
  /** Si este Pokémon puede Gigantamax (solo archivos Gen 8+) */
  canGigantamax: boolean;
  /** Si este Pokémon tiene una Mega Piedra equipada (afecta la visualización de la forma) */
  hasMegaStone: boolean;
}

export interface StatsDto {
  // IVs (Individual Values)
  ivHp: number;
  ivAtk: number;
  ivDef: number;
  ivSpa: number;
  ivSpd: number;
  ivSpe: number;

  // EVs (Effort Values)
  evHp: number;
  evAtk: number;
  evDef: number;
  evSpa: number;
  evSpd: number;
  evSpe: number;

  // Hyper Training
  hyperTrainedHp: boolean;
  hyperTrainedAtk: boolean;
  hyperTrainedDef: boolean;
  hyperTrainedSpa: boolean;
  hyperTrainedSpd: boolean;
  hyperTrainedSpe: boolean;

  // Estadísticas calculadas actuales
  statHp: number;
  statAtk: number;
  statDef: number;
  statSpa: number;
  statSpd: number;
  statSpe: number;
  statHpCurrent: number;
}

export interface TagDto {
  /** ID único del tag */
  id: number;
  /** Nombre del tag */
  name: string;
  /** Ruta de la imagen del tag (opcional) */
  imagePath?: string;
  /** Número de Pokémon que tienen este tag */
  pokemonCount: number;
}

export interface CreateTagDto {
  /** Nombre del tag */
  name: string;
}

export interface UpdateTagDto {
  /** Nombre del tag */
  name: string;
}

export interface MoveDto {
  slot: number;
  moveId: number;
  ppUps: number;
  currentPp: number;
}

export interface RelearnMoveDto {
  slot: number;
  moveId: number;
}

export interface PokemonDetailDto {
  // Información básica
  id: number;
  speciesId: number;
  form: number;
  nickname?: string;
  otName: string;
  tid: number;
  sid: number;
  level: number;
  isShiny: boolean;
  nature: number;
  abilityId: number;
  ballId: number;
  teraType?: number;
  heldItemId: number;
  originGame: number;
  language: string;
  metDate?: string; // ISO date string
  metLocation?: string;
  spriteKey: string;
  favorite: boolean;
  notes?: string;
  gender: number;
  otGender: number;
  otLanguage: string;

  // Campos mejorados de PK9
  encryptionConstant: number;
  personalityId: number;
  experience: number;
  currentFriendship: number;
  formArgument: number;
  isEgg: boolean;
  fatefulEncounter: boolean;
  eggLocation: number;
  eggMetDate?: string; // ISO date string

  // Propiedades físicas
  heightScalar: number;
  weightScalar: number;
  scale: number;

  // Pokérus
  pokerusState: number;
  pokerusDays: number;
  pokerusStrain: number;

  // Estadísticas de concurso
  contestCool: number;
  contestBeauty: number;
  contestCute: number;
  contestSmart: number;
  contestTough: number;
  contestSheen: number;

  // Información del manejador
  currentHandler: number;
  handlingTrainerName: string;
  handlingTrainerGender: number;
  handlingTrainerLanguage: number;
  handlingTrainerFriendship: number;

  // Sistema de memorias
  originalTrainerMemory: number;
  originalTrainerMemoryIntensity: number;
  originalTrainerMemoryFeeling: number;
  originalTrainerMemoryVariable: number;
  handlingTrainerMemory: number;
  handlingTrainerMemoryIntensity: number;
  handlingTrainerMemoryFeeling: number;
  handlingTrainerMemoryVariable: number;

  // Datos relacionados
  stats?: StatsDto;
  moves: MoveDto[];
  relearnMoves: RelearnMoveDto[];
}

// ===================================
// INTERFACES DE METADATA
// ===================================

export interface TypeInfo {
  id: number;
  name: string;
}

export interface GenerationInfo {
  id: number;
  name: string;
}

export interface GenderInfo {
  id: number;
  name: string;
}

export interface SortFieldInfo {
  name: string;
  value: number;
}

export interface TypeFilterModeInfo {
  name: string;
  value: number;
}

export interface PokemonMetadata {
  types: TypeInfo[];
  generations: number[];
  originGenerations: number[];
  capturedGenerations: number[];
  // genders: GenderInfo[]; // Temporarily disabled - gender filtering not working
  sortFields: SortFieldInfo[]; // Only working sort fields included
  typeFilterModes: TypeFilterModeInfo[];
  defaultPageSize: number;
  maxPageSize: number;
}

// ===================================
// INTERFACES DE COMPARACIÓN
// ===================================

export interface PokemonComparisonResult {
  pokemon1: {
    id: number;
    species: string;
    nickname?: string;
  };
  pokemon2: {
    id: number;
    species: string;
    nickname?: string;
  };
  areIdentical: boolean;
  differences: PokemonDifference[];
  summary: string;
}

export interface PokemonDifference {
  field: string;
  value1: any;
  value2: any;
  description: string;
}

// ===================================
// INTERFACES DE SCAN Y MANTENIMIENTO
// ===================================

export interface ScanResult {
  success: boolean;
  summary: {
    totalProcessed: number;
    newlyImported: number;
    alreadyImported: number;
    deleted: number;
    errors: number;
  };
  details: {
    newlyImported: ImportResultDto[];
    alreadyImported: string[];
    deleted: string[];
    errors: string[];
  };
}

export interface ScanStatus {
  directoryExists: boolean;
  watchPath: string;
  totalPokemonFiles?: number;
  filesByExtension?: Record<string, number>;
  lastModified?: string; // ISO date string
  message?: string;
}

export interface SyncResult {
  totalFilesInDatabase: number;
  removedFiles: string[];
  removedPokemon: string[];
  orphanedBackupsFound: number;
  orphanedUserFilesFound: number;
  mainStorageCleanedUp: number;
  syncSummary: string;
  success: boolean;
  error?: string;
}

export interface FileAnalysisResult {
  databaseFiles: {
    id: number;
    fileName: string;
    storedPath: string;
  }[];
  associatedPokemon: {
    id: number;
    species: string;
    nickname?: string;
  }[];
  physicalFiles: string[];
  backupFiles: string[];
  analysis: {
    totalDatabaseEntries: number;
    totalAssociatedPokemon: number;
    totalPhysicalFiles: number;
    totalBackupFiles: number;
    isConsistent: boolean;
    issues: string[];
  };
}

// ===================================
// INTERFACES EXTENDIDAS DE RESPUESTA API
// ===================================

export interface AdvancedPokemonListResponse {
  items: PokemonListItemDto[];
  total: number;
  stats: {
    queryComplexity: number;
    executionTimeMs: number;
    filterCount: number;
    sortFields: string[];
  };
}

export interface HealthCheckResponse {
  status: "ok";
}

export interface WipeDatabaseResponse {
  message: string;
  deletedBackups: number;
}

export interface DeletePokemonResponse {
  deleted: boolean;
  fileDeleted: boolean;
  backupDeleted?: boolean;
  backupPreserved?: boolean;
  fileName?: string;
}

// ===================================
// INTERFACES DE ARCHIVO Y EXPORT
// ===================================

export interface FileEntity {
  id: number;
  sha256: string;
  fileName: string;
  originalFileName?: string;
  format: FileFormat;
  size: number;
  storedPath: string;
  importedAt: string; // ISO date string
  rawBlob?: number[]; // byte array
}

// ===================================
// INTERFACES DE SERVICIOS EXTERNOS
// ===================================

export interface GameInfo {
  gameId: number;
  name: string;
  generation: number;
}

export interface SpeciesTypeInfo {
  speciesId: number;
  primaryType: number;
  secondaryType?: number;
}

// ===================================
// TIPOS DE ENDPOINT
// ===================================

export type EndpointResponse<T> = {
  data: T;
  status: number;
  headers: Record<string, string>;
};

export type ApiError = {
  message: string;
  status: number;
  detail?: string;
  type?: string;
  traceId?: string;
};

// ===================================
// CONSTANTES ÚTILES
// ===================================

export const API_CONSTANTS = {
  DEFAULT_PAGE_SIZE: 50,
  MAX_PAGE_SIZE: 500,
  MAX_LEVEL: 100,
  MIN_LEVEL: 1,
  SUPPORTED_FILE_EXTENSIONS: [
    ".pk1",
    ".pk2",
    ".pk3",
    ".pk4",
    ".pk5",
    ".pk6",
    ".pk7",
    ".pk8",
    ".pk9",
    ".pb7",
    ".pb8",
    ".pb9",
    ".pa8",
    ".pa9",
    ".ek1",
    ".ek2",
    ".ek3",
    ".ek4",
    ".ek5",
    ".ek6",
    ".ek7",
    ".ek8",
    ".ek9",
    ".ekx",
  ] as const,
  POKEMON_GENDERS: {
    MALE: 0,
    FEMALE: 1,
    GENDERLESS: 2,
  } as const,
} as const;

// ===================================
// FUNCIONES DE UTILIDAD DE TIPOS
// ===================================

export type PokemonEndpoints = {
  // GET endpoints
  "/pokemon": {
    query: PokemonQuery;
    response: PagedResult<PokemonListItemDto>;
  };
  "/pokemon/advanced": {
    query: AdvancedPokemonQuery;
    response: AdvancedPokemonListResponse;
  };
  "/pokemon/metadata": {
    response: PokemonMetadata;
  };
  "/pokemon/{id}": {
    params: { id: number };
    response: PokemonDetailDto;
  };
  "/pokemon/{id}/showdown": {
    params: { id: number };
    response: string; // text/plain
  };
  "/pokemon/compare/{id1}/{id2}": {
    params: { id1: number; id2: number };
    response: PokemonComparisonResult;
  };
};

// PATCH endpoints
export interface PatchEndpoints {
  "/pokemon/{id}": {
    params: { id: number };
    body: UpdatePokemonDto;
    response: void; // 204 No Content
  };
}

// POST endpoints for tags
export type TagPostEndpoints = {
  "/tags": {
    body: CreateTagDto;
    response: TagDto;
  };
  "/tags/{id}/image": {
    params: { id: number };
    body: FormData; // multipart/form-data with image file
    response: TagDto;
  };
  "/pokemon/{pokemonId}/tags/{tagId}": {
    params: { pokemonId: number; tagId: number };
    response: void; // 204 No Content
  };
};

// PUT endpoints for tags
export type TagPutEndpoints = {
  "/tags/{id}": {
    params: { id: number };
    body: UpdateTagDto;
    response: TagDto;
  };
};

// DELETE endpoints
export interface DeleteEndpoints {
  "/pokemon/{id}/database": {
    params: { id: number };
    response: DeletePokemonResponse;
  };
  "/pokemon/{id}/backup": {
    params: { id: number };
    response: DeletePokemonResponse;
  };
}

// DELETE endpoints for tags
export type TagDeleteEndpoints = {
  "/tags/{id}": {
    params: { id: number };
    response: void; // 204 No Content
  };
  "/tags/{id}/image": {
    params: { id: number };
    response: TagDto;
  };
  "/pokemon/{pokemonId}/tags/{tagId}": {
    params: { pokemonId: number; tagId: number };
    response: void; // 204 No Content
  };
};

export type ImportEndpoints = {
  "/import": {
    body: FormData; // multipart/form-data with files
    response: ImportResultDto[];
  };
};

export type FileEndpoints = {
  "/files/{id}": {
    params: { id: number };
    response: Blob; // application/octet-stream
  };
  "/export/{pokemonId}": {
    params: { pokemonId: number };
    response: Blob; // application/octet-stream
  };
  "/export/database/{pokemonId}": {
    params: { pokemonId: number };
    response: Blob; // application/octet-stream
  };
};

export type TagEndpoints = {
  // GET endpoints
  "/tags": {
    response: TagDto[];
  };
  "/tags/{id}": {
    params: { id: number };
    response: TagDto;
  };
  "/tags/{id}/pokemon": {
    params: { id: number };
    response: PagedResult<PokemonListItemDto>;
  };
};

export type ScanEndpoints = {
  "/scan/directory": {
    method: "POST";
    response: ScanResult;
  };
  "/scan/status": {
    response: ScanStatus;
  };
};

export type MaintenanceEndpoints = {
  "/maintenance/sync": {
    method: "POST";
    response: SyncResult;
  };
  "/maintenance/analyze/{pokemonId}": {
    params: { pokemonId: number };
    response: FileAnalysisResult;
  };
};

export type AdminEndpoints = {
  "/admin/wipe-database": {
    method: "POST";
    response: WipeDatabaseResponse;
  };
};

export type HealthEndpoints = {
  "/health": {
    response: HealthCheckResponse;
  };
};

// Tipo unión de todos los endpoints
export type AllEndpoints = PokemonEndpoints &
  ImportEndpoints &
  FileEndpoints &
  TagEndpoints &
  TagPostEndpoints &
  TagPutEndpoints &
  TagDeleteEndpoints &
  ScanEndpoints &
  MaintenanceEndpoints &
  AdminEndpoints &
  HealthEndpoints;

// ===================================
// COMENTARIOS FINALES
// ===================================

/**
 * NOTAS IMPORTANTES PARA EL FRONTEND:
 *
 * 1. PAGINACIÓN: Usar skip/take para paginación. El máximo recomendado es take=100.
 *
 * 2. FECHAS: Todas las fechas se devuelven como strings ISO (ejemplo: "2025-08-17T15:30:00Z").
 *    Usar new Date(dateString) para convertir a objetos Date de JavaScript.
 *
 * 3. ARCHIVOS: Los endpoints de archivos devuelven Blobs. Usar URL.createObjectURL()
 *    para crear URLs de descarga.
 *
 * 4. FORMDATA: El endpoint de import requiere FormData con archivos.
 *    Ejemplo: const formData = new FormData(); formData.append('files', file);
 *
 * 5. FILTROS AVANZADOS: Usar AdvancedPokemonQuery para consultas complejas con
 *    filtrado por tipos, generaciones, ordenamiento múltiple, etc.
 *
 * 6. FILTRADO POR TAGS: Ejemplo de filtros de tags:
 *    - tagIds: [1, 2] = Pokémon que tienen AMBOS tags 1 Y 2
 *    - anyTagIds: [1, 2] = Pokémon que tienen tag 1 O tag 2 (o ambos)
 *    - tagNames: ["Favoritos", "Competitivo"] = Pokémon con ambos tags por nombre
 *    - anyTagNames: ["Favoritos", "Shiny"] = Pokémon con cualquiera de estos tags
 *    - hasNoTags: true = Pokémon sin ningún tag asignado
 *
 * 7. NOMBRES DE ESPECIES Y FORMAS: El endpoint devuelve tanto speciesName como formName
 *    - speciesName: "Meowth", "Moltres", "Zacian"
 *    - formName: "Alolan", "Galarian", "Mega", "Crowned", "Gigantamax" (vacío para forma base)
 *
 * 8. TIPOS OPCIONALES: Los campos marcados con ? son opcionales y pueden ser undefined.
 *
 * 8. TIPOS OPCIONALES: Los campos marcados con ? son opcionales y pueden ser undefined.
 *
 * 9. ENUMS: Los enums numéricos deben usarse con sus valores numéricos, no los nombres.
 *
 * 10. ERRORES: Todos los endpoints pueden devolver errores HTTP estándar (400, 404, 500).
 *     Manejar estos errores apropiadamente en el frontend.
 *
 * 11. CORS: Asegúrate de que el frontend esté configurado para hacer peticiones al puerto
 *     correcto de la API (generalmente https://localhost:7xxx o http://localhost:5xxx).
 *
 * 12. TAGS: Sistema completo de etiquetado de Pokémon disponible.
 *     - GET /tags: Obtener todos los tags con conteo de Pokémon
 *     - CRUD completo: crear, editar, eliminar tags
 *     - Asignar/desasignar tags a Pokémon específicos
 *     - Subir/eliminar imágenes de tags
 *     - Obtener lista de Pokémon por tag
 *
 * 13. ORGANIZACIÓN API: Los endpoints están organizados por categorías
 *     (Pokemon, Import, Files, Tags, etc.) para mejor organización en Swagger/OpenAPI.
 */
