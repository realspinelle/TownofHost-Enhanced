using System;
using TOHE.Roles.Neutral;

namespace TOHE.Commands;

using static Translator;

public class QuizMasterQuestion : CommandBase
{
    public override string[] Commands { get; set; } = { "qmquiz" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return true;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Quizmaster.ShowQuestion(player);
        return true;
    }
}