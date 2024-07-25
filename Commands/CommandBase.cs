namespace TOHE.Commands;

public abstract class CommandBase
{
    public abstract string[] Commands { get; set; }
    public abstract bool CanExecute(string[] args, PlayerControl player);
    public abstract bool Execute(string[] args, PlayerControl player);
}