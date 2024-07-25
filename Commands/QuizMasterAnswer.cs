using System;
using TOHE.Roles.Neutral;

namespace TOHE.Commands;

using static Translator;

public class QuizMasterAnswer : CommandBase
{
    public override string[] Commands { get; set; } = { "answer", "ans", "asw" };

    public override bool CanExecute(string[] args, PlayerControl player)
    {
        return true;
    }

    public override bool Execute(string[] args, PlayerControl player)
    {
        Quizmaster.AnswerByChat(player, args);
        return true;
    }
}