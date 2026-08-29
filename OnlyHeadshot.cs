using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace OnlyHeadshot;

public class OnlyHeadshot : BasePlugin
{
    public override string ModuleName => "1337HUB DM + Native WASD Menu";
    public override string ModuleVersion => "2.13.0";
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
    
    private readonly Dictionary<ulong, (int menuId, int selectedIndex)> _playerMenus = new();
    private readonly Dictionary<ulong, DateTime> _lastButtonPress = new();

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

        RegisterListener<Listeners.OnTick>(OnTickMenuSystem);

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
            _playerMenus.Remove(player.SteamID);
            _lastButtonPress.Remove(player.SteamID);
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

    [ConsoleCommand("css_menu", "Otwórz główne menu serwera")]
    [ConsoleCommand("css_guns", "Otwórz główne menu serwera")]
    [ConsoleCommand("css_bron", "Otwórz główne menu serwera")]
    public void OnMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        _playerMenus[player.SteamID] = (1, 0);
    }

    private void OnTickMenuSystem()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            if (_voteInProgress && !_votedPlayers.Contains(player.SteamID))
            {
                if (!_playerMenus.ContainsKey(player.SteamID) || _playerMenus[player.SteamID].menuId != 3)
                {
                    _playerMenus[player.SteamID] = (3, 0);
                }
            }

            if (!_playerMenus.ContainsKey(player.SteamID)) continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) continue;

            var (menuId, selectedIndex) = _playerMenus[player.SteamID];
            var buttons = player.Buttons;

            bool canPress = !_lastButtonPress.ContainsKey(player.SteamID) || 
                            (DateTime.Now - _lastButtonPress[player.SteamID]).TotalMilliseconds > 200;

            int maxOptions = menuId switch
            {
                1 => 3, 
                2 => 5, 
                3 => 2, 
                _ => 3
            };

            if (canPress)
            {
                if ((buttons & PlayerButtons.Forward) != 0)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = maxOptions - 1;
                    _playerMenus[player.SteamID] = (menuId, selectedIndex);
                    _lastButtonPress[player.SteamID] = DateTime.Now;
                }
                else if ((buttons & PlayerButtons.Back) != 0)
                {
                    selectedIndex++;
                    if (selectedIndex >= maxOptions) selectedIndex = 0;
                    _playerMenus[player.SteamID] = (menuId, selectedIndex);
                    _lastButtonPress[player.SteamID] = DateTime.Now;
                }
                else if ((buttons & PlayerButtons.Use) != 0)
                {
                    ExecuteMenuAction(player, menuId, selectedIndex);
                    _lastButtonPress[player.SteamID] = DateTime.Now;
                    continue;
                }
            }

            string html = RenderMenuHtml(menuId, selectedIndex);
            player.PrintToCenterHtml(html);
        }
    }

    private string RenderMenuHtml(int menuId, int selectedIndex)
    {
        // Nowoczesny, przejrzysty design z wyrazistymi kolorami i mocno powiększonymi "przyciskami" sterowania na dole
        string html = "<div style='background: linear-gradient(135deg, rgba(15,15,15,0.95), rgba(35,35,35,0.95)); padding: 14px; border-radius: 8px; width: 350px; font-family: monospace; color: white; text-align: left; border: 2px solid #F39C12; box-shadow: 0 0 15px rgba(0,0,0,0.8);'>" +
                      "<div style='text-align: center; border-bottom: 2px solid #F39C12; padding-bottom: 6px; margin-bottom: 8px;'>" +
                      "<span style='color: #F39C12; font-size: 16px; font-weight: bold;'>[ 1337HUB.PL ]</span><br>" +
                      "<span style='color: #FFFFFF; font-size: 13px; letter-spacing: 1px;'>DEATHMATCH MENU</span></div>";

        if (menuId == 1)
        {
            string[] options = { "Mode: only HS", "Wybór Broni (AK/M4/AWP)", "Reset Statystyk (!rs)" };
            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedIndex)
                    html += $"<div style='background-color: rgba(46, 204, 113, 0.25); border-left: 4px solid #2ECC71; padding: 5px 8px; margin: 4px 0; border-radius: 4px;'>" +
                            $"<span style='color: #2ECC71; font-weight: bold; font-size: 14px;'>▶ {i + 1}. {options[i]} <b style='color: #FFF;'>[E]</b></span></div>";
                else
                    html += $"<div style='padding: 5px 8px; margin: 4px 0;'>" +
                            $"<span style='color: #CCCCCC; font-size: 13px;'>&nbsp;&nbsp;{i + 1}. {options[i]}</span></div>";
            }
        }
        else if (menuId == 2)
        {
            string[] weapons = { "AK-47 + Deagle", "M4A1-S + Deagle", "M4A4 + Deagle", "AWP + Deagle", "Powrót do menu" };
            for (int i = 0; i < weapons.Length; i++)
            {
                if (i == selectedIndex)
                    html += $"<div style='background-color: rgba(46, 204, 113, 0.25); border-left: 4px solid #2ECC71; padding: 5px 8px; margin: 4px 0; border-radius: 4px;'>" +
                            $"<span style='color: #2ECC71; font-weight: bold; font-size: 14px;'>▶ {i + 1}. {weapons[i]} <b style='color: #FFF;'>[E]</b></span></div>";
                else
                    html += $"<div style='padding: 5px 8px; margin: 4px 0;'>" +
                            $"<span style='color: #CCCCCC; font-size: 13px;'>&nbsp;&nbsp;{i + 1}. {weapons[i]}</span></div>";
            }
        }
        else if (menuId == 3)
        {
            html += "<div style='color: #00FFFF; text-align: center; font-size: 13px; font-weight: bold; margin-bottom: 6px;'>GŁOSOWANIE NA ONLY HEADSHOT</div>";
            string[] voteOptions = { "TAK (Włącz Only HS)", "NIE (Tryb Normalny)" };
            for (int i = 0; i < voteOptions.Length; i++)
            {
                if (i == selectedIndex)
                    html += $"<div style='background-color: rgba(46, 204, 113, 0.25); border-left: 4px solid #2ECC71; padding: 5px 8px; margin: 4px 0; border-radius: 4px;'>" +
                            $"<span style='color: #2ECC71; font-weight: bold; font-size: 14px;'>▶ {i + 1}. {voteOptions[i]} <b style='color: #FFF;'>[E]</b></span></div>";
                else
                    html += $"<div style='padding: 5px 8px; margin: 4px 0;'>" +
                            $"<span style='color: #CCCCCC; font-size: 13px;'>&nbsp;&nbsp;{i + 1}. {voteOptions[i]}</span></div>";
            }
        }

        // Duży, czytelny panel sterowania na samym dole z obramowanymi "przyciskami" klawiatury
        html += "<br><div style='border-top: 1px dashed #666; padding-top: 8px; text-align: center;'>" +
                "<span style='background-color: #333; color: #FFF; padding: 2px 6px; border-radius: 3px; font-weight: bold;'>W</span> <span style='color: #AAA; font-size: 11px;'>GÓRA</span> &nbsp;&nbsp;" +
                "<span style='background-color: #333; color: #FFF; padding: 2px 6px; border-radius: 3px; font-weight: bold;'>S</span> <span style='color: #AAA; font-size: 11px;'>DÓŁ</span> &nbsp;&nbsp;" +
                "<span style='background-color: #27AE60; color: #FFF; padding: 2px 6px; border-radius: 3px; font-weight: bold;'>E</span> <span style='color: #2ECC71; font-weight: bold; font-size: 11px;'>WYBIERZ</span>" +
                "</div></div>";

        return html;
    }

    private void ExecuteMenuAction(CCSPlayerController player, int menuId, int selectedIndex)
    {
        if (menuId == 1)
        {
            if (selectedIndex == 0)
            {
                if (_voteInProgress)
                {
                    player.PrintToChat($"{Prefix} Głosowanie już trwa!");
                    _playerMenus.Remove(player.SteamID);
                }
                else
                {
                    _voteHasBeenExecuted = false;
                    StartOnlyHsMenuVote();
                }
            }
            else if (selectedIndex == 1)
            {
                _playerMenus[player.SteamID] = (2, 0);
            }
            else if (selectedIndex == 2)
            {
                ResetPlayerScore(player);
                _playerMenus.Remove(player.SteamID);
            }
        }
        else if (menuId == 2)
        {
            if (selectedIndex == 0) SetPlayerLoadoutAndClose(player, "weapon_ak47", "weapon_deagle", "AK-47 + Deagle");
            else if (selectedIndex == 1) SetPlayerLoadoutAndClose(player, "weapon_m4a1_silencer", "weapon_deagle", "M4A1-S + Deagle");
            else if (selectedIndex == 2) SetPlayerLoadoutAndClose(player, "weapon_m4a1", "weapon_deagle", "M4A4 + Deagle");
            else if (selectedIndex == 3) SetPlayerLoadoutAndClose(player, "weapon_awp", "weapon_deagle", "AWP + Deagle");
            else if (selectedIndex == 4) _playerMenus[player.SteamID] = (1, 0);
        }
        else if (menuId == 3)
        {
            bool voteChoice = (selectedIndex == 0);
            ProcessVote(player, voteChoice);
            _playerMenus.Remove(player.SteamID);
        }
    }

    private void SetPlayerLoadoutAndClose(CCSPlayerController player, string primary, string secondary, string name)
    {
        _playerWeapons[player.SteamID] = (primary, secondary);
        player.PrintToChat($"{Prefix} Wybrano zestaw: \x06{name}\x01. Otrzymasz go przy następnym spawnie.");
        _playerMenus.Remove(player.SteamID);
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
        ResetPlayerScore(player);
    }

    private void ResetPlayerScore(CCSPlayerController player)
    {
        if (!player.IsValid) return;

        player.Score = 0;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iScore");

        player.PrintToChat($"{Prefix} Twoje statysty zostały pomyślnie zresetowane.");
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
            player.PrintToCenterHtml("<font color='#00FF00'><b>[1337HUB.PL] OCHRONA STARTOWA (2s)</b></font>");

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
        Server.PrintToChatAll($"{Prefix} Otwórz \x0C!menu\x01 lub poczekaj, aby oddać głos!");

        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            _playerMenus[player.SteamID] = (3, 0);
        }

        AddTimer(15.0f, FinishVote);
    }

    private void ProcessVote(CCSPlayerController player, bool vote)
    {
        if (!player.IsValid) return;

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
            if (_playerMenus.TryGetValue(player.SteamID, out var menu) && menu.menuId == 3)
            {
                _playerMenus.Remove(player.SteamID);
            }
        }

        if (_voteYes > _voteNo)
        {
            _isOnlyHs = true;
            Server.PrintToChatAll($"{Prefix} Wynik: \x06TAK\x01 ({_voteYes} vs {_voteNo}). Włączono \x0C[ONLY HEADSHOT]\x01!");
            
            _hsReminderTimer?.Link();
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
                attacker.PrintToCenterHtml("<font color='#FF0000'><b>[1337HUB.PL] ONLY HEADSHOT!</b></font>");
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
