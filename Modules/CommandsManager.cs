using System;
using System.Reflection;
using TOHE.Commands;

namespace TOHE.Modules;

public class CommandsManager
{
    private static Dictionary<string, CommandBase> Commands = new();

    public static void Init()
    {
        var AssCommands = Assembly.GetAssembly(typeof(CommandBase))!
            .GetTypes()
            .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(CommandBase)));
        foreach (var command in AssCommands)
        {
            CommandBase cmd = (CommandBase)Activator.CreateInstance(command);
            foreach (var txtcmd in cmd.Commands)
            {
                if (Commands.GetValueOrDefault(txtcmd) != null)
                {
                    Logger.Info("duplicated command " + txtcmd, "CommandsManager");
                    continue;
                }
                Commands.Add(txtcmd, cmd);
                Logger.Info("Loaded Command: " + txtcmd, "CommandsManager");
            }
        }
    }

    public static CommandBase GetCommand(string command)
    {
        return Commands.GetValueOrDefault(command);
    }
}