using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<FileEntity> Files => Set<FileEntity>();
        public DbSet<PokemonEntity> Pokemon => Set<PokemonEntity>();
        public DbSet<StatsEntity> Stats => Set<StatsEntity>();
        public DbSet<MoveEntity> Moves => Set<MoveEntity>();
        public DbSet<RelearnMoveEntity> RelearnMoves => Set<RelearnMoveEntity>();
        public DbSet<TagEntity> Tags => Set<TagEntity>();
        public DbSet<PokemonTagEntity> PokemonTags => Set<PokemonTagEntity>();
        public DbSet<PokemonBoxEntity> PokemonBoxes => Set<PokemonBoxEntity>();
        public DbSet<PokemonBoxSlotEntity> PokemonBoxSlots => Set<PokemonBoxSlotEntity>();
        public DbSet<FileTagEntity> FileTags => Set<FileTagEntity>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
        public DbSet<PokedexEntry> PokedexEntries => Set<PokedexEntry>();
        public DbSet<PokedexPokemon> PokedexPokemon => Set<PokedexPokemon>();
        public DbSet<PokedexItem> PokedexItems => Set<PokedexItem>();
        public DbSet<PokedexMove> PokedexMoves => Set<PokedexMove>();
        public DbSet<PokedexAbility> PokedexAbilities => Set<PokedexAbility>();
        public DbSet<PokedexEvolutionChain> PokedexEvolutionChains => Set<PokedexEvolutionChain>();
        public DbSet<PokedexType> PokedexTypes => Set<PokedexType>();
        public DbSet<BulbapediaCache> BulbapediaCache => Set<BulbapediaCache>();
        public DbSet<PokedexFlavorEntry> PokedexFlavorEntries => Set<PokedexFlavorEntry>();
        public DbSet<PokedexLocation> PokedexLocations => Set<PokedexLocation>();
        public DbSet<CachedImage> CachedImages => Set<CachedImage>();
        public DbSet<PokedexSpriteEntry> PokedexSpriteEntries => Set<PokedexSpriteEntry>();
        public DbSet<HouseholdConnection> HouseholdConnections => Set<HouseholdConnection>();
        public DbSet<HouseholdAuthorizationCode> HouseholdAuthorizationCodes => Set<HouseholdAuthorizationCode>();
        public DbSet<HouseholdAccessToken> HouseholdAccessTokens => Set<HouseholdAccessToken>();
        public DbSet<HouseholdRefreshToken> HouseholdRefreshTokens => Set<HouseholdRefreshToken>();
        public DbSet<SaveFileEntity> SaveFiles => Set<SaveFileEntity>();
        public DbSet<SaveTrainerEntity> SaveTrainers => Set<SaveTrainerEntity>();
        public DbSet<SavePokedexEntryEntity> SavePokedexEntries => Set<SavePokedexEntryEntity>();
        public DbSet<SavePokemonPreviewEntity> SavePokemonPreviews => Set<SavePokemonPreviewEntity>();
        public DbSet<TcgSetEntity> TcgSets => Set<TcgSetEntity>();
        public DbSet<TcgCardEntity> TcgCards => Set<TcgCardEntity>();
        public DbSet<UserTcgCardEntity> UserTcgCards => Set<UserTcgCardEntity>();
        public DbSet<UserApiCredentialEntity> UserApiCredentials => Set<UserApiCredentialEntity>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasKey(x => x.Id);
            b.Entity<User>().HasIndex(x => x.Username).IsUnique();

            b.Entity<UserPreference>().HasKey(x => x.UserId);
            b.Entity<UserPreference>()
                .HasOne(p => p.User)
                .WithOne(u => u.Preferences)
                .HasForeignKey<UserPreference>(p => p.UserId);

            b.Entity<PokedexEntry>().HasKey(x => x.SpeciesId);
            b.Entity<PokedexEntry>().Property(x => x.SpeciesId).ValueGeneratedNever();

            b.Entity<PokedexPokemon>().HasKey(x => x.PokemonId);
            b.Entity<PokedexPokemon>().Property(x => x.PokemonId).ValueGeneratedNever();
            b.Entity<PokedexPokemon>().HasIndex(x => x.SpeciesId);

            b.Entity<PokedexItem>().HasKey(x => x.ItemId);
            b.Entity<PokedexItem>().Property(x => x.ItemId).ValueGeneratedNever();
            b.Entity<PokedexItem>().HasIndex(x => x.Category);

            b.Entity<PokedexMove>().HasKey(x => x.MoveId);
            b.Entity<PokedexMove>().Property(x => x.MoveId).ValueGeneratedNever();
            b.Entity<PokedexMove>().HasIndex(x => x.Type);

            b.Entity<PokedexAbility>().HasKey(x => x.AbilityId);
            b.Entity<PokedexAbility>().Property(x => x.AbilityId).ValueGeneratedNever();

            b.Entity<PokedexEvolutionChain>().HasKey(x => x.ChainId);
            b.Entity<PokedexEvolutionChain>().Property(x => x.ChainId).ValueGeneratedNever();

            b.Entity<PokedexType>().HasKey(x => x.TypeId);
            b.Entity<PokedexType>().Property(x => x.TypeId).ValueGeneratedNever();

            b.Entity<FileEntity>().HasKey(x => x.Id);
            b.Entity<FileEntity>().HasIndex(x => new { x.UserId, x.Sha256 }).IsUnique();
            b.Entity<FileEntity>().HasIndex(x => x.UserId);
            b.Entity<FileEntity>()
                .HasOne(f => f.User)
                .WithMany(u => u.Files)
                .HasForeignKey(f => f.UserId);

            b.Entity<PokemonEntity>().HasKey(x => x.Id);
            b.Entity<PokemonEntity>().HasIndex(x => new { x.SpeciesId, x.IsShiny });
            b.Entity<PokemonEntity>().HasIndex(x => x.OriginGame);
            b.Entity<PokemonEntity>().HasIndex(x => x.UserId);
            b.Entity<PokemonEntity>()
                .HasOne(p => p.User)
                .WithMany(u => u.Pokemon)
                .HasForeignKey(p => p.UserId);

            // Configure relationship between Pokemon and File
            b.Entity<PokemonEntity>()
                .HasOne(p => p.File)
                .WithMany()
                .HasForeignKey(p => p.FileId);

            b.Entity<StatsEntity>().HasKey(x => x.PokemonId);
            b.Entity<MoveEntity>().HasKey(x => new { x.PokemonId, x.Slot });
            b.Entity<RelearnMoveEntity>().HasKey(x => new { x.PokemonId, x.Slot });
            b.Entity<MoveEntity>()
                .HasOne<PokemonEntity>()
                .WithMany(p => p.Moves)
                .HasForeignKey(m => m.PokemonId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<RelearnMoveEntity>()
                .HasOne<PokemonEntity>()
                .WithMany(p => p.RelearnMoves)
                .HasForeignKey(m => m.PokemonId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<TagEntity>().HasKey(x => x.Id);
            b.Entity<TagEntity>().HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            b.Entity<TagEntity>().HasIndex(x => x.UserId);
            b.Entity<TagEntity>()
                .HasOne(t => t.User)
                .WithMany(u => u.Tags)
                .HasForeignKey(t => t.UserId);
            b.Entity<PokemonTagEntity>().HasKey(x => new { x.PokemonId, x.TagId });
            b.Entity<FileTagEntity>().HasKey(x => new { x.FileId, x.TagId });

            // Configure many-to-many relationship for Pokemon and Tags
            b.Entity<PokemonTagEntity>()
                .HasOne(pt => pt.Pokemon)
                .WithMany(p => p.PokemonTags)
                .HasForeignKey(pt => pt.PokemonId);

            b.Entity<PokemonTagEntity>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.PokemonTags)
                .HasForeignKey(pt => pt.TagId);

            // Configure many-to-many relationship for Files and Tags
            b.Entity<FileTagEntity>()
                .HasOne(ft => ft.File)
                .WithMany(f => f.FileTags)
                .HasForeignKey(ft => ft.FileId);

            b.Entity<FileTagEntity>()
                .HasOne(ft => ft.Tag)
                .WithMany(t => t.FileTags)
                .HasForeignKey(ft => ft.TagId);

            b.Entity<PokemonBoxEntity>().HasKey(x => x.Id);
            b.Entity<PokemonBoxEntity>().HasIndex(x => new { x.UserId, x.SortOrder });
            b.Entity<PokemonBoxEntity>()
                .HasOne(x => x.User)
                .WithMany(u => u.PokemonBoxes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<PokemonBoxSlotEntity>().HasKey(x => new { x.BoxId, x.SlotIndex });
            b.Entity<PokemonBoxSlotEntity>().HasIndex(x => x.PokemonId).IsUnique();
            b.Entity<PokemonBoxSlotEntity>()
                .HasOne(x => x.Box)
                .WithMany(bx => bx.Slots)
                .HasForeignKey(x => x.BoxId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<PokemonBoxSlotEntity>()
                .HasOne(x => x.Pokemon)
                .WithOne(p => p.BoxSlot)
                .HasForeignKey<PokemonBoxSlotEntity>(x => x.PokemonId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SaveFileEntity>().HasKey(x => x.Id);
            b.Entity<SaveFileEntity>().HasIndex(x => new { x.UserId, x.Sha256 }).IsUnique();
            b.Entity<SaveFileEntity>().HasIndex(x => new { x.UserId, x.ImportedAt });
            b.Entity<SaveFileEntity>()
                .HasOne(x => x.User)
                .WithMany(x => x.SaveFiles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SaveTrainerEntity>().HasKey(x => x.SaveFileId);
            b.Entity<SaveTrainerEntity>()
                .HasOne(x => x.SaveFile)
                .WithOne(x => x.Trainer)
                .HasForeignKey<SaveTrainerEntity>(x => x.SaveFileId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SavePokedexEntryEntity>().HasKey(x => new { x.SaveFileId, x.SpeciesId });
            b.Entity<SavePokedexEntryEntity>()
                .HasOne(x => x.SaveFile)
                .WithMany(x => x.PokedexEntries)
                .HasForeignKey(x => x.SaveFileId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SavePokemonPreviewEntity>().HasKey(x => x.Id);
            b.Entity<SavePokemonPreviewEntity>().HasIndex(x => new { x.SaveFileId, x.Location, x.BoxIndex, x.SlotIndex }).IsUnique();
            b.Entity<SavePokemonPreviewEntity>().HasIndex(x => x.PokemonHash);
            b.Entity<SavePokemonPreviewEntity>()
                .HasOne(x => x.SaveFile)
                .WithMany(x => x.PokemonPreviews)
                .HasForeignKey(x => x.SaveFileId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<TcgSetEntity>().HasKey(x => x.Id);
            b.Entity<TcgSetEntity>().HasIndex(x => new { x.Provider, x.ProviderSetId }).IsUnique();
            b.Entity<TcgSetEntity>().HasIndex(x => x.ReleaseDate);
            b.Entity<TcgSetEntity>().HasIndex(x => x.OfficialCode);

            b.Entity<TcgCardEntity>().HasKey(x => x.Id);
            b.Entity<TcgCardEntity>().HasIndex(x => new { x.Provider, x.ProviderCardId }).IsUnique();
            b.Entity<TcgCardEntity>().HasIndex(x => new { x.SetId, x.Number });
            b.Entity<TcgCardEntity>().HasIndex(x => x.Name);
            b.Entity<TcgCardEntity>()
                .HasOne(x => x.Set)
                .WithMany(x => x.Cards)
                .HasForeignKey(x => x.SetId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<UserTcgCardEntity>().HasKey(x => x.Id);
            b.Entity<UserTcgCardEntity>()
                .HasIndex(x => new { x.UserId, x.CardId, x.Variant, x.Condition, x.Language })
                .IsUnique();
            b.Entity<UserTcgCardEntity>().HasIndex(x => new { x.UserId, x.AddedAt });
            b.Entity<UserTcgCardEntity>()
                .HasOne(x => x.User)
                .WithMany(x => x.TcgCards)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<UserTcgCardEntity>()
                .HasOne(x => x.Card)
                .WithMany(x => x.OwnedEntries)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<UserApiCredentialEntity>().HasKey(x => new { x.UserId, x.Provider });
            b.Entity<UserApiCredentialEntity>()
                .HasOne(x => x.User)
                .WithMany(x => x.ApiCredentials)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bulbapedia and enrichment entities
            b.Entity<BulbapediaCache>().HasKey(x => x.Id);
            b.Entity<BulbapediaCache>().HasIndex(x => x.SpeciesId);

            b.Entity<PokedexFlavorEntry>().HasKey(x => x.Id);
            b.Entity<PokedexFlavorEntry>().HasIndex(x => new { x.SpeciesId, x.Language, x.GameVersion });

            b.Entity<PokedexLocation>().HasKey(x => x.Id);
            b.Entity<PokedexLocation>().HasIndex(x => new { x.SpeciesId, x.Game });

            b.Entity<CachedImage>().HasKey(x => x.Id);
            b.Entity<CachedImage>().HasIndex(x => x.SourceUrl).IsUnique();
            b.Entity<CachedImage>().HasIndex(x => x.SpeciesId);

            b.Entity<PokedexSpriteEntry>().HasKey(x => x.Id);
            b.Entity<PokedexSpriteEntry>().HasIndex(x => new { x.SpeciesId, x.GameSlug });
            b.Entity<PokedexSpriteEntry>().HasIndex(x => x.PokemonId);

            b.Entity<HouseholdConnection>().HasKey(x => x.Id);
            b.Entity<HouseholdConnection>().HasIndex(x => new { x.UserId, x.ClientId });
            b.Entity<HouseholdConnection>()
                .HasOne(x => x.User)
                .WithMany(x => x.HouseholdConnections)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<HouseholdAuthorizationCode>().HasKey(x => x.Id);
            b.Entity<HouseholdAuthorizationCode>().HasIndex(x => x.CodeHash).IsUnique();
            b.Entity<HouseholdAuthorizationCode>().HasIndex(x => x.ExpiresAt);
            b.Entity<HouseholdAuthorizationCode>()
                .HasOne(x => x.Connection)
                .WithMany(x => x.AuthorizationCodes)
                .HasForeignKey(x => x.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<HouseholdAccessToken>().HasKey(x => x.Id);
            b.Entity<HouseholdAccessToken>().HasIndex(x => x.TokenHash).IsUnique();
            b.Entity<HouseholdAccessToken>().HasIndex(x => new { x.ConnectionId, x.FamilyId });
            b.Entity<HouseholdAccessToken>().HasIndex(x => x.ExpiresAt);
            b.Entity<HouseholdAccessToken>()
                .HasOne(x => x.Connection)
                .WithMany(x => x.AccessTokens)
                .HasForeignKey(x => x.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<HouseholdRefreshToken>().HasKey(x => x.Id);
            b.Entity<HouseholdRefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
            b.Entity<HouseholdRefreshToken>().HasIndex(x => new { x.ConnectionId, x.FamilyId });
            b.Entity<HouseholdRefreshToken>().HasIndex(x => x.ExpiresAt);
            b.Entity<HouseholdRefreshToken>()
                .HasOne(x => x.Connection)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<HouseholdRefreshToken>()
                .HasOne(x => x.ReplacedByToken)
                .WithMany()
                .HasForeignKey(x => x.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
