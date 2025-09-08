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

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<FileEntity>().HasKey(x => x.Id);
            b.Entity<FileEntity>().HasIndex(x => x.Sha256).IsUnique();

            b.Entity<PokemonEntity>().HasKey(x => x.Id);
            b.Entity<PokemonEntity>().HasIndex(x => new { x.SpeciesId, x.IsShiny });
            b.Entity<PokemonEntity>().HasIndex(x => x.OriginGame);

            // Configure relationship between Pokemon and File
            b.Entity<PokemonEntity>()
                .HasOne(p => p.File)
                .WithMany()
                .HasForeignKey(p => p.FileId);

            b.Entity<StatsEntity>().HasKey(x => x.PokemonId);
            b.Entity<MoveEntity>().HasKey(x => new { x.PokemonId, x.Slot });
            b.Entity<RelearnMoveEntity>().HasKey(x => new { x.PokemonId, x.Slot });

            b.Entity<TagEntity>().HasKey(x => x.Id);
            b.Entity<TagEntity>().HasIndex(x => x.Name).IsUnique(); // Case-sensitive unique constraint
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
