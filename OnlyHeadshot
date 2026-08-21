using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace OnlyHeadshot;

public class OnlyHeadshot : BasePlugin
{
    public override string ModuleName => "Only Headshot & Chat Advert";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";
    private CounterStrikeSharp.API.Modules.Timers.Timer? _advertTimer;

    public override void Load(bool hotReload)
    {
        // Rejestracja zdarzenia zadawania obrażeń (OnTakeDamage)
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);

        // Uruchomienie cyklicznej wiadomości na czacie (co 120 sekund)
        _advertTimer = AddTimer(120.0f, SendAdvertMessage, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;

        // Jeśli atakujący nie istnieje, jest botem lub gracz zadał obrażenia sam sobie — pomiń
        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return HookResult.Continue;

        // Jeśli trafienie NIE BYŁO w głowę (hitgroup != 1)
        if (@event.Hitgroup != 1)
        {
            // Przywróć zdrowie graczowi z powrotem o wartość zadanych obrażeń
            if (victim.PlayerPawn.Value != null)
            {
                victim.PlayerPawn.Value.Health += @event.DmgHealth;
                victim.PlayerPawn.Value.Armor += @event.DmgArmor;
            }
        }

        return HookResult.Continue;
    }

    private void SendAdvertMessage()
    {
        // Ogłoszenie na czacie do wszystkich graczy na serwerze
        Server.PrintToChatAll($"{Tag} Na serwerze obowiązuje tryb \u0002ONLY HEADSHOT\u0001! Liczą się tylko trafienia w głowę.");
    }

    public override void Unload(bool hotReload)
    {
        _advertTimer?.Kill();
    }
}
