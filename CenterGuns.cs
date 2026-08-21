using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;

namespace CenterGuns;

public class CenterGuns : BasePlugin
{
    public override string ModuleName => "Center Guns Menu";
    public override string ModuleVersion => "1.0.9";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";
    private readonly Dictionary<ulong, string> _playerSecondary = new();

    public override void Load(bool hotReload)
    {
        AddCommand("css_guns", "Otwiera menu broni", OnGunsCommand);
        AddCommand("css_gun", "Otwiera menu broni", OnGunsCommand);

        // Komendy szybkiego wyboru z czatu: !1, !2, !3, !4 oraz !r
        AddCommand("css_1", "Wybór AK", (p, info) => GiveKit(p, "weapon_ak47"));
        AddCommand("css_2", "Wybór M4A1-S", (p, info) => GiveKit(p, "weapon_m4a1_silencer"));
        AddCommand("css_3", "Wybór M4A4", (p, info) => GiveKit(p, "weapon_m4a1"));
        AddCommand("css_4", "Wybór AWP", (p, info) => GiveKit(p, "weapon_awp"));
        AddCommand("css_r", "Przełącz pistolet", OnTogglePistolCommand);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player != null && player.IsValid && !player.IsBot)
        {
            OpenGunMenu(player);
        }

        return HookResult.Continue;
    }

    private void OnGunsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        OpenGunMenu(player);
    }

    private void OnTogglePistolCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        ulong steamId = player.SteamID;
        string currentPistol = _playerSecondary.GetValueOrDefault(steamId, "weapon_deagle");
        string newPistol = currentPistol == "weapon_deagle" ? "weapon_revolver" : "weapon_deagle";

        _playerSecondary[steamId] = newPistol;

        string pistolName = newPistol == "weapon_revolver" ? "REWOLWER" : "DEAGLE";
        player.PrintToChat($"{Tag} Zmieniono pistolet na: \u0006{pistolName}\u0001!");

        // Odświeżenie otwartego menu po przełączeniu pistoletu
        OpenGunMenu(player);
    }

    private void OpenGunMenu(CCSPlayerController player)
    {
        string pistolName = _playerSecondary.GetValueOrDefault(player.SteamID, "weapon_deagle") == "weapon_revolver" ? "REWOLWER" : "DEAGLE";

        string content = "<b><span style='color: #ff1e1e;'>[1337HUB.PL]</span> <span style='color: #ffffff;'>WYBIERZ BROŃ</span></b><br><br>" +
                         "<span style='color: #70b03a;'>!1</span> AK + " + pistolName + "<br>" +
                         "<span style='color: #70b03a;'>!2</span> M4A1-S + " + pistolName + "<br>" +
                         "<span style='color: #70b03a;'>!3</span> M4A4 + " + pistolName + "<br>" +
                         "<span style='color: #70b03a;'>!4</span> AWP + " + pistolName + "<br><br>" +
                         "<span style='color: #e6a100;'>!r</span> ZAMIEŃ DEAGLE NA REWOLWER I NA ODWRÓT";

        var menu = new CenterHtmlMenu(content, this);
        menu.AddMenuOption("", (p, opt) => { });

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void GiveKit(CCSPlayerController? player, string primary)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return;

        MenuManager.CloseActiveMenu(player);

        string secondary = _playerSecondary.GetValueOrDefault(player.SteamID, "weapon_deagle");

        player.RemoveWeapons();
        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");

        player.PrintToChat($"{Tag} Wybrano zestaw broni!");
    }
}
