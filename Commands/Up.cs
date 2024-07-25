namespace TOHE.Commands;

using static Translator;

public class Up : CommandBase
{
    public override string[] Commands { get; set; } = { "up" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        if (!PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp){
            Utils.SendMessage($"{GetString("InvalidPermissionCMD")}", player.PlayerId);
            return false;
        }

        if (!Options.EnableUpMode.GetBool())
        {
            Utils.SendMessage(string.Format(GetString("Message.YTPlanDisabled"), GetString("EnableYTPlan")),
                player.PlayerId);
            return false;
        }

        if (!GameStates.IsLobby)
        {
            Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
            return false;
        } 
        Utils.SendRolesInfo(args[0], player.PlayerId, isUp: true);
        return true;
    }
}