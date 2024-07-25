using System;

namespace TOHE.Commands;

using static Translator;

public class KillLog : CommandBase
{
    public override string[] Commands { get; set; } = { "kh", "killlog" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.DumpLog();
        return true;
    }
}