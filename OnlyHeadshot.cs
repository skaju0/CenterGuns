using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace OnlyHeadshot;

public class OnlyHeadshot : BasePlugin
{
    public override string ModuleName => "Only Headshot & Chat Advert";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";
    private CounterStrikeSharp.API.Modules.Timers.Timer? _advertTimer;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);

        // Cykliczna wiadomość na czacie co 120 sekund
        _advertTimer = AddTimer(120.0f, SendAdvertMessage, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return HookResult.Continue;

        // Jeśli trafienie NIE BYŁO w głowę (Hitgroup != 1)
        if (@event.Hitgroup != 1)
        {
            var pawn = victim.PlayerPawn.Value;
            if (pawn != null && pawn.IsValid)
            {
                // Przywrócenie HP oraz pancerza (używamy ArmorValue zamiast Armor)
                pawn.Health += @event.DmgHealth;
                pawn.ArmorValue += @event.DmgArmor;
            }
        }

        return HookResult.Continue;
    }

    private void SendAdvertMessage()
    {
        Server.PrintToChatAll($"{Tag} Na serwerze obowiązuje tryb \u0002ONLY HEADSHOT\u0001! Liczą się tylko trafienia w głowę.");
    }

    public override void Unload(bool hotReload)
    {
        _advertTimer?.Kill();
    }
}
