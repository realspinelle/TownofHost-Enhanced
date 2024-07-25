using System;

namespace TOHE.Commands;

using static Translator;

public class Dump : CommandBase
{
    public override string[] Commands { get; set; } = { "version", "v" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.DumpLog();
        return true;
    }
}