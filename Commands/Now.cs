using System;
using UnityEngine;

namespace TOHE.Commands;

using static Translator;

public class Now : CommandBase
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
            case "r":
            case "roles":
            case "funções":
                Utils.ShowActiveRoles();
                break;
            case "a":
            case "all":
            case "tudo":
                Utils.ShowAllActiveSettings();
                break;
            default:
                Utils.ShowActiveSettings();
                break;
        }

        return true;
    }
}