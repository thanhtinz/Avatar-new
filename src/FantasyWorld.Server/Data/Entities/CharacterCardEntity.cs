// FantasyWorld.Server/Data/Entities/CharacterCardEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FantasyWorld.Server.Data.Entities;

[Table("character_cards")]
public class CharacterCard
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  CardId      { get; set; }
    public int  Quantity    { get; set; } = 1;
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
}
