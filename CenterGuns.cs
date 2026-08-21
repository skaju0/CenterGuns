using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Menu;

namespace CenterGuns;

public class CenterGuns : BasePlugin
{
    public override string ModuleName => "Center Guns Menu";
    public override string ModuleVersion => "1.0.5";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";

    public override void Load(bool hotReload)
    {
        AddCommand("css_guns", "Otwiera menu broni", OnGunsCommand);
        AddCommand("css_gun", "Otwiera menu broni", OnGunsCommand);

        // Komendy szybkiego wyboru (!1, !2, !3... bez otwierania menu)
        AddCommand("css_1", "Wybór zewstawu 1", (p, info) => GiveWeapons(p, "weapon_ak47", "weapon_deagle"));
        AddCommand("css_2", "Wybór zewstawu 2", (p, info) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_deagle"));
        AddCommand("css_3", "Wybór zewstawu 3", (p, info) => GiveWeapons(p, "weapon_m4a1", "weapon_deagle"));
        AddCommand("css_4", "Wybór zewstawu 4", (p, info) => GiveWeapons(p, "weapon_awp", "weapon_deagle"));
        AddCommand("css_5", "Wybór zewstawu 5", (p, info) => GiveWeapons(p, "weapon_ak47", "weapon_revolver"));
        AddCommand("css_6", "Wybór zewstawu 6", (p, info) => GiveWeapons(p, "weapon_ak47", "weapon_revolver"));
        AddCommand("css_7", "Wybór zewstawu 7", (p, info) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_revolver"));
        AddCommand("css_8", "Wybór zewstawu 8", (p, info) => GiveWeapons(p, "weapon_m4a1", "weapon_revolver"));
        AddCommand("css_9", "Wybór zewstawu 9", (p, info) => GiveWeapons(p, "weapon_awp", "weapon_revolver"));
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

    private void OnGunsCommand(CCSPlayerController? player, CounterStrikeSharp.API.Modules.Commands.CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        OpenGunMenu(player);
    }

    private void OpenGunMenu(CCSPlayerController player)
    {
        // Tytuł stylizowany pod barwy 1337HUB
        string title = "<span style='color: #ff1e1e; font-weight: bold;'>[1337HUB.PL]</span> <span style='color: #ffffff;'>WYBIERZ BROŃ</span>";
        var menu = new CenterHtmlMenu(title, this);

        menu.AddMenuOption("AK + DEAGLE", (p, opt) => GiveWeapons(p, "weapon_ak47", "weapon_deagle"));
        menu.AddMenuOption("M4A1-S + DEAGLE", (p, opt) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_deagle"));
        menu.AddMenuOption("M4A4 + DEAGLE", (p, opt) => GiveWeapons(p, "weapon_m4a1", "weapon_deagle"));
        menu.AddMenuOption("AWP + DEAGLE", (p, opt) => GiveWeapons(p, "weapon_awp", "weapon_deagle"));
        menu.AddMenuOption("AK + REWOLWER", (p, opt) => GiveWeapons(p, "weapon_ak47", "weapon_revolver"));
        menu.AddMenuOption("AK + REWOLWER", (p, opt) => GiveWeapons(p, "weapon_ak47", "weapon_revolver"));
        menu.AddMenuOption("M4A1-S + REWOLWER", (p, opt) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_revolver"));
        menu.AddMenuOption("M4A4 + REWOLWER", (p, opt) => GiveWeapons(p, "weapon_m4a1", "weapon_revolver"));
        menu.AddMenuOption("AWP + REWOLWER", (p, opt) => GiveWeapons(p, "weapon_awp", "weapon_revolver"));

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void GiveWeapons(CCSPlayerController? player, string primary, string secondary)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return;

        MenuManager.CloseActiveMenu(player);

        player.RemoveWeapons();
        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");

        player.PrintToChat($"{Tag} Wybrano zestaw broni!");
    }
}
