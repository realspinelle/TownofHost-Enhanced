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
        string version_text = "";
        try
        {
            foreach (var kvp in Main.playerVersion.OrderBy(pair => pair.Key).ToArray())
            {
                var pc = Utils.GetClientById(kvp.Key)?.Character;
                version_text +=
                    $"{kvp.Key}/{(pc?.PlayerId != null ? pc.PlayerId.ToString() : "null")}:{pc?.GetRealName(clientData: true) ?? "null"}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n";
            }

            if (version_text != "")
            {
                Utils.SendMessage(version_text, player.PlayerId);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.Message, "/version");
            version_text = "Error while getting version : " + e.Message;
            if (version_text != "")
            {
                Utils.SendMessage(version_text, player.PlayerId);
            }
        }

        return true;
    }
}