using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace SimpleDeathmatch;

public class SimpleDeathmatch : BasePlugin
{
    public override string ModuleName => "Simple Deathmatch Core";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "1337HUB";

    private const float RespawnDelay = 1.0f; // Czas odradzania po śmierci w sekundach
    private const float ProtectionDuration = 2.0f; // Czas ochrony po spawnie w sekundach

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // Ochrona startowa (GodMode) po odrodzeniu
        var pawn = player.PlayerPawn.Value;
        if (pawn != null && pawn.IsValid)
        {
            pawn.TakesDamage = false;
            player.PrintToCenterHtml("<font color='#00FF00'><b>OCHRONA STARTOWA (2s)</b></font>");

            AddTimer(ProtectionDuration, () =>
            {
                if (player.IsValid && player.PlayerPawn.Value != null)
                {
                    player.PlayerPawn.Value.TakesDamage = true;
                    player.PrintToCenterHtml("<font color='#FF0000'><b>OCHRONA WYGASŁA</b></font>");
                }
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;

        // Czyszczenie broni po śmierci
        Server.NextFrame(CleanDroppedWeapons);

        // Automatyczne odrodzenie po 1 sekundzie
        if (victim != null && victim.IsValid && !victim.IsBot && victim.TeamNum > 1)
        {
            AddTimer(RespawnDelay, () =>
            {
                if (victim.IsValid && !victim.PawnIsAlive && victim.TeamNum > 1)
                {
                    victim.Respawn();
                }
            });
        }

        return HookResult.Continue;
    }

    private void CleanDroppedWeapons()
    {
        var weapons = Utilities.FindAllEntitiesByDesignerName<CBasePlayerWeapon>("weapon_");
        foreach (var weapon in weapons)
        {
            if (weapon.IsValid && weapon.OwnerEntity.Value == null)
            {
                weapon.Remove();
            }
        }
    }
}
