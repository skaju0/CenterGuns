using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace OnlyHeadshot;

public class OnlyHeadshot : BasePlugin
{
    public override string ModuleName => "1337HUB DM + Guns + OnlyHS Vote";
    public override string ModuleVersion => "1.7.0";
    public override string ModuleAuthor => "1337HUB";

    private const string Prefix = " \x0B[1337HUB.PL]\x01";
    private const float RespawnDelay = 1.0f;
    private const float ProtectionDuration = 2.0f;

    private bool _isOnlyHs = false;
    private bool _voteInProgress = false;
    private bool _voteHasBeenExecuted = false;
    private int _voteYes = 0;
    private int _voteNo = 0;
    private readonly HashSet<ulong> _votedPlayers = new();
    private readonly Dictionary<ulong, (string primary, string secondary)> _playerWeapons = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _hsReminderTimer;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
    }

    // ==========================================
    // AUTOMATYCZNE DOŁĄCZANIE DO DRUŻYNY
    // ==========================================

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if (player.TeamNum <= 1)
        {
            Server.NextFrame(() =>
            {
                if (player.IsValid && player.TeamNum <= 1)
                {
                    player.ChangeTeam(CsTeam.Terrorist);
                }
            });
        }

        return HookResult.Continue;
    }

    // ==========================================
    // DEDYKOWANE KOMENDY WYBORU BRONI (!ak, !m4, !awp)
    // ==========================================

    [ConsoleCommand("css_guns", "Informacja o komendach broni")]
    [ConsoleCommand("css_bron", "Informacja o komendach broni")]
    public void OnGunsCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        player.PrintToChat($"{Prefix} \x06Dostępne komendy wyboru broni:\x01");
        player.PrintToChat(" \x0C!ak\x01 - AK-47 + Deagle");
        player.PrintToChat(" \x0C!m4\x01 - M4A1-S + Deagle");
        player.PrintToChat(" \x0C!m4a4\x01 - M4A4 + Deagle");
        player.PrintToChat(" \x0C!awp\x01 - AWP + Deagle");
    }

    [ConsoleCommand("css_ak", "Szybki wybór AK-47")]
    public void OnSelectAK(CCSPlayerController? player, CommandInfo command) => SetPlayerLoadout(player, "weapon_ak47", "weapon_deagle", "AK-47 + Deagle");

    [ConsoleCommand("css_m4", "Szybki wybór M4A1-S")]
    public void OnSelectM4(CCSPlayerController? player, CommandInfo command) => SetPlayerLoadout(player, "weapon_m4a1_silencer", "weapon_deagle", "M4A1-S + Deagle");

    [ConsoleCommand("css_m4a4", "Szybki wybór M4A4")]
    public void OnSelectM4A4(CCSPlayerController? player, CommandInfo command) => SetPlayerLoadout(player, "weapon_m4a1", "weapon_deagle", "M4A4 + Deagle");

    [ConsoleCommand("css_awp", "Szybki wybór AWP")]
    public void OnSelectAWP(CCSPlayerController? player, CommandInfo command) => SetPlayerLoadout(player, "weapon_awp", "weapon_deagle", "AWP + Deagle");

    private void SetPlayerLoadout(CCSPlayerController? player, string primary, string secondary, string name)
    {
        if (player == null || !player.IsValid) return;

        _playerWeapons[player.SteamID] = (primary, secondary);
        player.PrintToChat($"{Prefix} Wybrano zestaw: \x06{name}\x01. Otrzymasz go przy następnym spawnie.");
    }

    private void GivePlayerLoadout(CCSPlayerController player)
    {
        if (!player.IsValid || !player.PawnIsAlive || player.PlayerPawn.Value == null) return;

        var steamId = player.SteamID;
        var (primary, secondary) = _playerWeapons.ContainsKey(steamId) 
            ? _playerWeapons[steamId] 
            : ("weapon_ak47", "weapon_deagle");

        RemoveAllWeapons(player);

        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("item_assaultsuit");
    }

    private void RemoveAllWeapons(CCSPlayerController player)
    {
        if (player.PlayerPawn.Value == null || player.PlayerPawn.Value.WeaponServices == null) return;

        var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;
        foreach (var weapon in weapons)
        {
            if (weapon.Value != null && weapon.Value.IsValid)
            {
                weapon.Value.Remove();
            }
        }
    }

    // ==========================================
    // LOGIKA GŁOSOWANIA ONLY HS
    // ==========================================

    [ConsoleCommand("css_hs", "Wywołaj głosowanie na OnlyHS")]
    [ConsoleCommand("css_vote", "Wywołaj głosowanie na OnlyHS")]
    public void OnVoteCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        if (_voteInProgress)
        {
            player.PrintToChat($"{Prefix} Głosowanie już trwa!");
            return;
        }

        _voteHasBeenExecuted = false;
        StartOnlyHsMenuVote();
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _voteHasBeenExecuted = false;
        _voteInProgress = false;
        _isOnlyHs = false;
        _votedPlayers.Clear();

        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");
        Server.ExecuteCommand("mp_respawn_after_death_delay 1");

        AddTimer(3.0f, StartOnlyHsMenuVote);

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if (player.TeamNum <= 1)
        {
            player.ChangeTeam(CsTeam.Terrorist);
            return HookResult.Continue;
        }

        var steamId = player.SteamID;

        if (!_playerWeapons.ContainsKey(steamId))
        {
            _playerWeapons[steamId] = ("weapon_ak47", "weapon_deagle");
            player.PrintToChat($"{Prefix} Domyślny zestaw: \x06AK-47 + Deagle\x01. Wpisz \x0C!ak\x01, \x0C!m4\x01 lub \x0C!awp\x01 aby zmienić.");
        }

        Server.NextFrame(() => GivePlayerLoadout(player));

        if (!_voteInProgress && !_voteHasBeenExecuted)
        {
            AddTimer(2.0f, StartOnlyHsMenuVote);
        }

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

    private void StartOnlyHsMenuVote()
    {
        if (_voteInProgress || _voteHasBeenExecuted) return;

        _voteInProgress = true;
        _voteHasBeenExecuted = true;
        _voteYes = 0;
        _voteNo = 0;
        _votedPlayers.Clear();

        Server.PrintToChatAll($"{Prefix} Rozpoczęto głosowanie na tryb \x0C[ONLY HEADSHOT]\x01!");

        var voteMenu = new ChatMenu(" Czy włączyć tryb ONLY HEADSHOT? ");
        voteMenu.AddMenuOption("TAK (Only Headshot)", (player, option) => ProcessVote(player, true));
        voteMenu.AddMenuOption("NIE (Normalne obrażenia)", (player, option) => ProcessVote(player, false));

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
            player.PrintToChat($"{Prefix} Już oddałeś głos!");
            return;
        }

        _votedPlayers.Add(player.SteamID);
        if (vote) _voteYes++; else _voteNo++;

        player.PrintToChat($"{Prefix} Oddano głos na: {(vote ? "\x06TAK\x01" : "\x02NIE\x01")}");
    }

    private void FinishVote()
    {
        _voteInProgress = false;

        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            MenuManager.CloseActiveMenu(player);
        }

        if (_voteYes > _voteNo)
        {
            _isOnlyHs = true;
            Server.PrintToChatAll($"{Prefix} Wynik: \x06TAK\x01 ({_voteYes} vs {_voteNo}). Włączono \x0C[ONLY HEADSHOT]\x01!");
            
            _hsReminderTimer?.Kill();
            _hsReminderTimer = AddTimer(60.0f, () =>
            {
                if (_isOnlyHs)
                {
                    Server.PrintToChatAll($"{Prefix} \x0C[PRZYPOMNIENIE]\x01 Aktywny tryb \x06ONLY HEADSHOT\x01!");
                }
            }, TimerFlags.REPEAT);
        }
        else
        {
            _isOnlyHs = false;
            Server.PrintToChatAll($"{Prefix} Wynik: \x02NIE\x01 ({_voteYes} vs {_voteNo}). Gramy w trybie standardowym.");
        }
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!_isOnlyHs) return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;

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
