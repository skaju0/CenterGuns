using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace HubDeathmatch;

public class HubDeathmatch : BasePlugin
{
    public override string ModuleName => "1337HUB DM + Aim + OnlyHS Menu Vote";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "1337HUB";

    private const string Prefix = " \x0B[1337HUB.PL]\x01";
    private const float RespawnDelay = 1.0f;
    private const float ProtectionDuration = 2.0f;

    private bool _isOnlyHs = false;
    private bool _voteInProgress = false;
    private int _voteYes = 0;
    private int _voteNo = 0;
    private readonly HashSet<ulong> _votedPlayers = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _hsReminderTimer;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!_voteInProgress && _votedPlayers.Count == 0)
        {
            // Odraczamy otwarcie menu o 2 sekundy po starcie rundy, żeby gracz zdążył się załadować
            AddTimer(2.0f, StartOnlyHsMenuVote);
        }
        return HookResult.Continue;
    }

    private void StartOnlyHsMenuVote()
    {
        _voteInProgress = true;
        _voteYes = 0;
        _voteNo = 0;
        _votedPlayers.Clear();

        Server.PrintToChatAll($"{Prefix} Rozpoczęto głosowanie na tryb \x0C[ONLY HEADSHOT]\x01! Wybierz opcję z menu.");

        // Tworzymy menu wyboru
        var voteMenu = new ChatMenu(" Czy włączyć tryb ONLY HEADSHOT? ");
        
        voteMenu.AddMenuOption("TAK (Only Headshot)", (player, option) => ProcessVote(player, true));
        voteMenu.AddMenuOption("NIE (Standardowe obrażenia)", (player, option) => ProcessVote(player, false));

        // Wyświetlamy menu wszystkim graczom na serwerze
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            MenuManager.OpenChatMenu(player, voteMenu);
        }

        AddTimer(15.0f, FinishVote);
    }

    private void ProcessVote(CCSPlayerController player, bool vote)
    {
        if (!player.IsValid || !_voteInProgress) return;

        if (_votedPlayers.Contains(player.SteamID))
        {
            player.PrintToChat($"{Prefix} Oddałeś już swój głos!");
            return;
        }

        _votedPlayers.Add(player.SteamID);
        if (vote) _voteYes++; else _voteNo++;

        player.PrintToChat($"{Prefix} Oddano głos na: {(vote ? "\x06TAK\x01" : "\x02NIE\x01")}");
    }

    private void FinishVote()
    {
        _voteInProgress = false;

        // Zamknięcie menu u graczy, którzy nie zagłosowali
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            MenuManager.CloseActiveMenu(player);
        }

        if (_voteYes > _voteNo)
        {
            _isOnlyHs = true;
            Server.PrintToChatAll($"{Prefix} Wynik głosowania: \x06TAK\x01 ({_voteYes} do {_voteNo}). Włączono tryb \x0C[ONLY HEADSHOT]\x01!");
            
            _hsReminderTimer?.Kill();
            _hsReminderTimer = AddTimer(90.0f, () =>
            {
                if (_isOnlyHs)
                {
                    Server.PrintToChatAll($"{Prefix} \x0C[PRZYPOMNIENIE]\x01 Na serwerze aktywny jest tryb \x06ONLY HEADSHOT\x01!");
                }
            }, TimerFlags.REPEAT);
        }
        else
        {
            _isOnlyHs = false;
            Server.PrintToChatAll($"{Prefix} Wynik głosowania: \x02NIE\x01 ({_voteYes} do {_voteNo}). Gramy w trybie standardowym.");
        }
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!_isOnlyHs) return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;

        // Hitgroup 1 = Głowa
        if (@event.Hitgroup != 1 && victim != null && victim.IsValid && victim.PlayerPawn.Value != null)
        {
            victim.PlayerPawn.Value.Health += @event.DmgHealth;
            victim.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

            if (attacker != null && attacker.IsValid && !attacker.IsBot)
            {
                attacker.PrintToCenterHtml("<font color='#FF0000'><b>ONLY HEADSHOT!</b></font>");
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

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
                }
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;

        Server.NextFrame(CleanDroppedWeapons);

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
