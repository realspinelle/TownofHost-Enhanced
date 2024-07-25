using System;

namespace TOHE.Commands;

using static Translator;

public class LastResult : CommandBase
{
    public override string[] Commands { get; set; } = { "l", "lastresult", "fimdejogo" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.ShowKillLog();
        Utils.ShowLastRoles();
        Utils.ShowLastResult();
        return true;
    }
}