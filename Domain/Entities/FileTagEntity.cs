namespace BeastVault.Api.Domain.Entities;

public class FileTagEntity
{
    public int FileId { get; set; }
    public int TagId { get; set; }

    public FileEntity File { get; set; } = null!;
    public TagEntity Tag { get; set; } = null!;
}
