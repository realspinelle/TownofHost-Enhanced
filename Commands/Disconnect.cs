using System;
using UnityEngine;

namespace TOHE.Commands;

public class Disconnect : CommandBase
{
    public override string[] Commands { get; set; } = { "n", "now", "atual" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        switch (args[0])
        {
            case "crew":
            case "tripulante":
                GameManager.Instance.enabled = false;
                GameManager.Instance.RpcEndGame(GameOverReason.HumansDisconnect, false);
                break;
        
            case "imp":
            case "impostor":
                GameManager.Instance.enabled = false;
                GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                break;
        
            default:
                Utils.SendMessage("crew | imp", player.PlayerId);
                if (TranslationController.Instance.currentLanguage.languageID == SupportedLangs.Brazilian)
                {
                    Utils.SendMessage("tripulante | impostor", player.PlayerId);
                }
                break;
        }
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Admin, 0);

        return true;
    }
    //             case "/dis":
    //             case "/disconnect":
    //             case "/desconectar":
    //                 canceled = true;
    //                 subArgs = args.Length < 2 ? "" : args[1];
    //                 switch (subArgs)
    //                 {
    //                     case "crew":
    //                     case "tripulante":
    //                         GameManager.Instance.enabled = false;
    //                         GameManager.Instance.RpcEndGame(GameOverReason.HumansDisconnect, false);
    //                         break;
    //
    //                     case "imp":
    //                     case "impostor":
    //                         GameManager.Instance.enabled = false;
    //                         GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
    //                         break;
    //
    //                     default:
    //                         __instance.AddChat(PlayerControl.LocalPlayer, "crew | imp");
    //                         if (TranslationController.Instance.currentLanguage.languageID == SupportedLangs.Brazilian)
    //                         {
    //                             __instance.AddChat(PlayerControl.LocalPlayer, "tripulante | impostor");
    //                         }
    //                         cancelVal = "/dis";
    //                         break;
    //                 }
    //                 ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Admin, 0);
}