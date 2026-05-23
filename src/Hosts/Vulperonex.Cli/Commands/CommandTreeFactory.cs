internal static class CommandTreeFactory
{
    public static ICommandDispatcher Create()
    {
        var commands = new List<IConsoleCommand>
        {
            new RuleCommand(),
            new TimerCommand(),
            new ConfigCommand(),
            new MemberCommand(),
            new SimulateCommand(),
            new TwitchCommand(),
        };
        commands.Add(new HelpCommand(() => commands));
        return new CommandDispatcher(commands);
    }
}
