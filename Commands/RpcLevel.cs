using System;
using TOHE.Roles.Neutral;

namespace TOHE.Commands;

using static Translator;

public class RpcLevel : CommandBase
{
    public override string[] Commands { get; set; } = { "level", "nível", "nivel" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.ClientId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Utils.SendMessage(string.Format(GetString("Message.SetLevel"), args[0]), PlayerControl.LocalPlayer.PlayerId);
        _ = int.TryParse(args[0], out int input);
        if (input is < 1 or > 999)
        {
            Utils.SendMessage(GetString("Message.AllowLevelRange"), PlayerControl.LocalPlayer.PlayerId);
            return true;
        }

        var number = Convert.ToUInt32(input);
        PlayerControl.LocalPlayer.RpcSetLevel(number - 1);
        return true;
    }
}