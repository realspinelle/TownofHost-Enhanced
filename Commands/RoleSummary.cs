using System;

namespace TOHE.Commands;

using static Translator;

public class RoleSummary : CommandBase
{
    public override string[] Commands { get; set; } =
        { "rs", "sum", "rolesummary", "sumario", "sumário", "summary", "результат" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.ShowLastRoles();
        return true;
    }
}