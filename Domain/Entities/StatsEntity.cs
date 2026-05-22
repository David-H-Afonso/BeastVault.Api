namespace BeastVault.Api.Domain.Entities;

public class StatsEntity
{
    public int PokemonId { get; set; }
    public int IvHp { get; set; }
    public int IvAtk { get; set; }
    public int IvDef { get; set; }
    public int IvSpa { get; set; }
    public int IvSpd { get; set; }
    public int IvSpe { get; set; }
    public int EvHp { get; set; }
    public int EvAtk { get; set; }
    public int EvDef { get; set; }
    public int EvSpa { get; set; }
    public int EvSpd { get; set; }
    public int EvSpe { get; set; }
    public bool HyperTrainedHp { get; set; }
    public bool HyperTrainedAtk { get; set; }
    public bool HyperTrainedDef { get; set; }
    public bool HyperTrainedSpa { get; set; }
    public bool HyperTrainedSpd { get; set; }
    public bool HyperTrainedSpe { get; set; }

    public int StatHp { get; set; }
    public int StatAtk { get; set; }
    public int StatDef { get; set; }
    public int StatSpa { get; set; }
    public int StatSpd { get; set; }
    public int StatSpe { get; set; }
    public int StatHpCurrent { get; set; }
}
