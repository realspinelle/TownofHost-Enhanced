using System;

namespace TOHE.Commands;

using static Translator;

public class GameResults : CommandBase
{
    public override string[] Commands { get; set; } = { "gr", "gameresults", "resultados" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.ShowLastResult();
        return true;
    }
}