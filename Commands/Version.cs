using System;

namespace TOHE.Commands;

using static Translator;

public class Version : CommandBase
{
    public override string[] Commands { get; set; } = { "version", "v" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return true;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        string versionText = "";
        try
        {
            foreach (var kvp in Main.playerVersion.OrderBy(pair => pair.Key).ToArray())
            {
                var pc = Utils.GetClientById(kvp.Key)?.Character;
                versionText +=
                    $"{kvp.Key}/{(pc?.PlayerId != null ? pc.PlayerId.ToString() : "null")}:{pc?.GetRealName(clientData: true) ?? "null"}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n";
            }

            if (versionText != "")
            {
                Utils.SendMessage(versionText, player.PlayerId);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.Message, "/version");
            versionText = "Error while getting version : " + e.Message;
            if (versionText != "")
            {
                Utils.SendMessage(versionText, player.PlayerId);
            }
        }

        return true;
    }
}