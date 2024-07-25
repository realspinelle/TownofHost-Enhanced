using System;

namespace TOHE.Commands;

using static Translator;

public class GhostInfo : CommandBase
{
    public override string[] Commands { get; set; } = { "ghostinfo" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.SendMessage(GetString("Message.GhostRoleInfo"), player.PlayerId);
        return true;
    }
}