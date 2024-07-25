using System;

namespace TOHE.Commands;

using static Translator;

public class Rename : CommandBase
{
    public override string[] Commands { get; set; } = { "rn", "rename", "renomear", "переименовать" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return true;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        if (Options.PlayerCanSetName.GetBool() || player.FriendCode.GetDevUser().IsDev ||
            player.FriendCode.GetDevUser().NameCmd || Utils.IsPlayerVIP(player.FriendCode))
        {
            if (GameStates.IsInGame)
            {
                Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
                return true;
            }

            if (args.Length < 1) return true;
            if (args.Join(delimiter: " ").Length is > 10 or < 1)
            {
                Utils.SendMessage(GetString("Message.AllowNameLength"), player.PlayerId);
                return true;
            }

            Main.AllPlayerNames[player.PlayerId] = args.Join(delimiter: " ");
            Utils.SendMessage(string.Format(GetString("Message.SetName"), args.Join(delimiter: " ")),
                player.PlayerId);
        }

        return true;
    }
}