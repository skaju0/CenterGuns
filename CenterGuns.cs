using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Menu;

namespace CenterGuns;

public class CenterGuns : BasePlugin
{
    public override string ModuleName => "Center Guns Menu";
    public override string ModuleVersion => "1.0.6";
    public override string ModuleAuthor => "1337HUB";

    private const string Tag = " \u000B[1337HUB.PL]\u0001";

    public override void Load(bool hotReload)
    {
        AddCommand("css_guns", "Otwiera menu broni", OnGunsCommand);
        AddCommand("css_gun", "Otwiera menu broni", OnGunsCommand);

        // Komendy szybkiego wyboru (!1 - !9)
        AddCommand("css_1", "Zestaw 1", (p, info) => GiveWeapons(p, "weapon_ak47", "weapon_deagle"));
        AddCommand("css_2", "Zestaw 2", (p, info) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_deagle"));
        AddCommand("css_3", "Zestaw 3", (p, info) => GiveWeapons(p, "weapon_m4a1", "weapon_deagle"));
        AddCommand("css_4", "Zestaw 4", (p, info) => GiveWeapons(p, "weapon_awp", "weapon_deagle"));
        AddCommand("css_5", "Zestaw 5", (p, info) => GiveWeapons(p, "weapon_ak47", "weapon_revolver"));
        AddCommand("css_6", "Zestaw 6", (p, info) => GiveWeapons(p, "weapon_m4a1_silencer", "weapon_revolver"));
        AddCommand("css_7", "Zestaw 7", (p, info) => GiveWeapons(p, "weapon_m4a1", "weapon_revolver"));
        AddCommand("css_8", "Zestaw 8", (p, info) => GiveWeapons(p, "weapon_awp", "weapon_revolver"));
        AddCommand("css_9", "Zestaw 9", (p, info) => GiveWeapons(p, "weapon_ssg08", "weapon_deagle"));
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
        // Wyłączenie automatycznego stronicowania CS# - budujemy własny layout HTML
        string content = "<b><span style='color: #ff1e1e;'>[1337HUB.PL]</span> <span style='color: #ffffff;'>WYBIERZ BROŃ</span></b><br>" +
                         "<span style='color: #70b03a;'>!1</span> AK + DEAGLE<br>" +
                         "<span style='color: #70b03a;'>!2</span> M4A1-S + DEAGLE<br>" +
                         "<span style='color: #70b03a;'>!3</span> M4A4 + DEAGLE<br>" +
                         "<span style='color: #70b03a;'>!4</span> AWP + DEAGLE<br>" +
                         "<span style='color: #70b03a;'>!5</span> AK + REWOLWER<br>" +
                         "<span style='color: #70b03a;'>!6</span> M4A1-S + REWOLWER<br>" +
                         "<span style='color: #70b03a;'>!7</span> M4A4 + REWOLWER<br>" +
                         "<span style='color: #70b03a;'>!8</span> AWP + REWOLWER<br>" +
                         "<span style='color: #70b03a;'>!9</span> SCOUT + DEAGLE";

        var menu = new CenterHtmlMenu(content, this);

        // Dodajemy puste wywołania, by ramka przetworzyła natywne zbieranie danych
        menu.AddMenuOption("", (p, opt) => { });

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
