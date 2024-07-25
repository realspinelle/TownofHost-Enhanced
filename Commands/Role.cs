using System;

namespace TOHE.Commands;

public class Role : CommandBase
{
    public override string[] Commands { get; set; } = { "r", "role", "р", "роль" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    { 
        Utils.SendRolesInfo(args[0], player.PlayerId);
        return true;
    }
}