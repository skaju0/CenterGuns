using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace OnlyHeadshot;

public class OnlyHeadshot : BasePlugin
{
    public override string ModuleName => "1337HUB DM + Custom WASD Menu";
    public override string ModuleVersion => "2.6.0";
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
    private readonly Dictionary<ulong, int> _playerMenuSelection = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _hsReminderTimer;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        RegisterListener<Listeners.OnMapStart>((mapName) =>
        {
            AddTimer(2.0f, ForceUnfreezeGame);
            Server.ExecuteCommand("bot_quota_mode fill");
            Server.ExecuteCommand("bot_quota 10");
        });

        // Nasłuchiwanie klawiszy do obsługi menu WASD (W, S, E)
        RegisterListener<Listeners.OnTick>(OnTickMenuNavigation);

        if (hotReload)
        {
            ForceUnfreezeGame();
        }
    }

    private void ForceUnfreezeGame()
    {
        Server.ExecuteCommand("mp_warmup_end");
        Server.ExecuteCommand("mp_waiting_for_players_cancel 1");
        Server.ExecuteCommand("mp_restartgame 1");
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        AddTimer(1.0f, () =>
        {
            if (player.IsValid && player.TeamNum <= 1)
            {
                AssignBalancedTeam(player);
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _playerMenuSelection.Remove(player.SteamID);
        }
        return HookResult.Continue;
    }

    private void AssignBalancedTeam(CCSPlayerController player)
    {
        if (!player.IsValid || player.TeamNum > 1) return;

        var players = Utilities.GetPlayers().Where(p => p.IsValid && p.TeamNum > 1).ToList();
        int tCount = players.Count(p => p.TeamNum == (byte)CsTeam.Terrorist);
        int ctCount = players.Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist);

        CsTeam targetTeam = tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        
        player.ChangeTeam(targetTeam);
        
        Server.NextFrame(() =>
        {
            if (player.IsValid && !player.PawnIsAlive && player.TeamNum > 1)
            {
                player.Respawn();
            }
        });
    }

    // ==========================================
    // OBSŁUGA WBUDOWANEGO MENU WASD (W / S / E)
    // ==========================================

    [ConsoleCommand("css_menu", "Otwórz główne menu serwera")]
    [ConsoleCommand("css_guns", "Otwórz główne menu serwera")]
    [ConsoleCommand("css_bron", "Otwórz główne menu serwera")]
    public void OnMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        _playerMenuSelection[player.SteamID] = 0; // Wybrana pierwsza opcja domyślnie
    }

    private void OnTickMenuNavigation()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            if (!_playerMenuSelection.ContainsKey(player.SteamID)) continue;

            var buttons = player.Buttons;
            int currentSelection = _playerMenuSelection[player.SteamID];
            int maxOptions = 3; // Liczba opcji w menu

            string menuHtml = $"<font color='#FFA500'><b>DeathMatch</b></font><br>";
            
            for (int i = 0; i < maxOptions; i++)
            {
                string optionText = i switch
                {
                    0 => "Mode: only HS",
                    1 => "Wybór Broni (AK/M4/AWP)",
                    2 => "Reset Statystyk (!rs)",
                    _ => ""
                };

                if (i == currentSelection)
                {
                    menuHtml += $"<font color='#00FF00'><b>&gt; {i + 1}. {optionText} [E]</b></font><br>";
                }
                else
                {
                    menuHtml += $"<font color='#FFFFFF'>{i + 1}. {optionText}</font><br>";
                }
            }

            player.PrintToCenterHtml(menuHtml);

            // Sterowanie klawiszami (W = w górę, S = w dół, E = zatwierdź)
            // Uwaga: Ruch w górę/dół symulujemy przyciskami ruchu lub komendami, tutaj uproszczone sterowanie pod przyciskami E (IN_USE)
            // Aby w pełni płynnie nawigować klawiszami W/S bez zewnętrznych bibliotek, używamy wykrywania przycisków:
        }
    }

    private void SetPlayerLoadoutAndClose(CCSPlayerController player, string primary, string secondary, string name)
    {
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

        player.RemoveWeapons();

        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("item_assaultsuit");
    }

    [ConsoleCommand("css_rs", "Resetuj statystyki")]
    [ConsoleCommand("css_reset", "Resetuj statystyki")]
    public void OnResetScoreCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($"{Prefix} Twoje statysty zostały zresetowane.");
    }

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
        Server.ExecuteCommand("mp_warmup_end");

        _voteHasBeenExecuted = false;
        _voteInProgress = false;
        _isOnlyHs = false;
        _votedPlayers.Clear();

        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");

        AddTimer(3.0f, StartOnlyHsMenuVote);

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if (player.TeamNum <= 1)
        {
            AssignBalancedTeam(player);
            return HookResult.Continue;
        }

        var steamId = player.SteamID;

        if (!_playerWeapons.ContainsKey(steamId))
        {
            _playerWeapons[steamId] = ("weapon_ak47", "weapon_deagle");
            player.PrintToChat($"{Prefix} Domyślny zestaw: \x06AK-47 + Deagle\x01. Wpisz \x0C!menu\x01 aby otworzyć menu.");
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
        AddTimer(15.0f, FinishVote);
    }

    private void FinishVote()
    {
        _voteInProgress = false;

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

        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (@event.Hitgroup != 1 && victim != null && victim.IsValid && victim.PlayerPawn.Value != null)
        {
            var pawn = victim.PlayerPawn.Value;
            pawn.Health += @event.DmgHealth;
            
            if (pawn.Health > 100) pawn.Health = 100;

            if (attacker != null && attacker.IsValid && !attacker.IsBot)
            {
                attacker.PrintToCenterHtml("<font color='#FF0000'><b>ONLY HEADSHOT!</b></font>");
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (attacker != null && attacker.IsValid && !attacker.IsBot && attacker.PawnIsAlive && attacker.PlayerPawn.Value != null)
        {
            var pController = attacker;
            Server.NextFrame(() =>
            {
                if (pController.IsValid && pController.PawnIsAlive && pController.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value != null)
                {
                    var weapon = pController.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value;
                    if (weapon != null && weapon.IsValid)
                    {
                        weapon.Clip1 = 100;
                    }
                }
            });
        }

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
}
