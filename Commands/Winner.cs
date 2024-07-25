using System;

namespace TOHE.Commands;

using static Translator;

public class Winner : CommandBase
{
    public override string[] Commands { get; set; } = { "win", "winner", "vencedor" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        if (Main.winnerNameList.Count == 0) Utils.SendMessage(GetString("NoInfoExists"));
        else Utils.SendMessage("Winner: " + string.Join(", ", Main.winnerNameList));
        return true;
    }
}