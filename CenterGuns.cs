using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace CenterGuns;

public class CenterGuns : BasePlugin
{
    public override string ModuleName => "Center Guns Menu";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";

    public override void Load(bool hotReload)
    {
        AddCommand("css_guns", "Otwiera menu broni", OnGunsCommand);
        AddCommand("css_gun", "Otwiera menu broni", OnGunsCommand);

        RegisterListener<Listeners.OnPlayerSpawn>(playerSlot =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player != null && player.IsValid && !player.IsBot)
            {
                OpenGunMenu(player);
            }
        });
    }

    private void OnGunsCommand(CCSPlayerController? player, CounterStrikeSharp.API.Modules.Commands.CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        OpenGunMenu(player);
    }

    private void OpenGunMenu(CCSPlayerController player)
    {
        // Menu graficzne po lewej stronie (CenterHtmlMenu)
        var menu = new CenterHtmlMenu("Wybór Broni [1337HUB]", this);

        menu.AddMenuOption("AK-47 + Deagle", (p, opt) => GiveWeapons(p, "weapon_ak47", "weapon_deagle"));
        menu.AddMenuOption("M4A1-S + USP", (p, opt) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_usp_silencer"));
        menu.AddMenuOption("M4A4 + USP", (p, opt) => GiveWeapons(p, "weapon_m4a1", "weapon_usp_silencer"));
        menu.AddMenuOption("AWP + Deagle", (p, opt) => GiveWeapons(p, "weapon_awp", "weapon_deagle"));

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void GiveWeapons(CCSPlayerController player, string primary, string secondary)
    {
        if (player == null || player.PlayerPawn.Value == null) return;

        player.RemoveWeapons();
        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");

        player.PrintToChat($"{Tag} Wybrano zestaw broni!");
    }
}
