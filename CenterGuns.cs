using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Menu;

namespace CenterGuns;

public class CenterGuns : BasePlugin
{
    public override string ModuleName => "Center Guns Menu";
    public override string ModuleVersion => "1.0.2";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";

    public override void Load(bool hotReload)
    {
        AddCommand("css_guns", "Otwiera menu broni", OnGunsCommand);
        AddCommand("css_gun", "Otwiera menu broni", OnGunsCommand);
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
        // Użycie ChatMenu z opcją cyfrową (wybór bezpośrednio klawiszami 1-9)
        var menu = new ChatMenu("Wybór Broni [1337HUB]");

        menu.AddMenuOption("AK-47 + Deagle", (p, opt) => GiveWeapons(p, "weapon_ak47", "weapon_deagle"));
        menu.AddMenuOption("M4A1-S + USP", (p, opt) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_usp_silencer"));
        menu.AddMenuOption("M4A4 + USP", (p, opt) => GiveWeapons(p, "weapon_m4a1", "weapon_usp_silencer"));
        menu.AddMenuOption("AWP + Deagle", (p, opt) => GiveWeapons(p, "weapon_awp", "weapon_deagle"));

        MenuManager.OpenChatMenu(player, menu);
    }

    private void GiveWeapons(CCSPlayerController player, string primary, string secondary)
    {
        if (player == null || player.PlayerPawn.Value == null) return;

        MenuManager.CloseActiveMenu(player);

        player.RemoveWeapons();
        player.GiveNamedItem(primary);
        player.GiveNamedItem(secondary);
        player.GiveNamedItem("weapon_knife");

        player.PrintToChat($"{Tag} Wybrano zestaw broni!");
    }
}
