using System;
using UnityEngine;

namespace TOHE.Commands;

using static Translator;

public class HideName : CommandBase
{
    public override string[] Commands { get; set; } = { "hn", "hidename", "semnome" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return player.PlayerId == AmongUsClient.Instance.HostId;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Main.HideName.Value =
            args.Length > 1 ? args.Skip(1).Join(delimiter: " ") : Main.HideName.DefaultValue.ToString();
        GameStartManagerPatch.GameStartManagerStartPatch.HideName.text =
            ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";
        return true;
    }
}