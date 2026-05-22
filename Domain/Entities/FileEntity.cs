namespace BeastVault.Api.Domain.Entities;

public class FileEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Sha256 { get; set; }
    public required string FileName { get; set; }
    public string? OriginalFileName { get; set; }
    public required string Format { get; set; }
    public long Size { get; set; }
    public required string StoredPath { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public byte[]? RawBlob { get; set; }

    public ICollection<FileTagEntity> FileTags { get; set; } = new List<FileTagEntity>();
    public User User { get; set; } = null!;
}
