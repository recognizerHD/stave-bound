using System;
using Jotunn.Entities;

namespace Stavebound.Portals
{
    /// <summary>
    /// Base for this mod's console commands, which exists for one reason: an exception thrown inside
    /// a console command reaches the Unity log and <em>nothing else</em>. The terminal prints no
    /// output at all, which reads exactly like a command that ran and had nothing to say.
    /// <para>
    /// That cost a debugging round trip once already. Here the failure lands where the person who
    /// typed the command is looking.
    /// </para>
    /// </summary>
    internal abstract class StaveboundCommand : ConsoleCommand
    {
        public sealed override void Run(string[] args) => Run(args, Console.instance);

        public sealed override void Run(string[] args, Terminal context)
        {
            if (context == null)
            {
                return;
            }

            try
            {
                Execute(args ?? Array.Empty<string>(), context);
            }
            catch (Exception exception)
            {
                context.AddString($"Stavebound: {Name} failed - {exception.GetType().Name}: {exception.Message}");
                Jotunn.Logger.LogError($"{Name} threw: {exception}");
            }
        }

        /// <summary>The command body. Anything it throws is reported to the terminal.</summary>
        protected abstract void Execute(string[] args, Terminal context);

        /// <summary>
        /// Writes a line to the console <em>and</em> the log.
        /// <para>
        /// Console output vanishes with the session and can only be shared as a screenshot. These
        /// commands exist to diagnose a registry that spans two machines, so their answers belong
        /// somewhere both ends can be compared after the fact — which means the log.
        /// </para>
        /// </summary>
        protected static void Echo(Terminal context, string line)
        {
            context.AddString(line);
            Jotunn.Logger.LogInfo(line);
        }
    }
}
