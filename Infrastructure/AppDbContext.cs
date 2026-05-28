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
        }
    }
}
