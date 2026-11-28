using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace DiIiS_NA.Core.Logging
{
	public class ConsoleTarget : LogTarget
	{
		/// <summary>
		/// Console writes happen on a dedicated thread. With MethodTrace enabled a busy rift produces
		/// 150-200 lines per second; Spectre parses the markup of every line and writes to stdout
		/// synchronously, which showed up as 47-121 ms ticks in the game loop (the same ticks the
		/// Slow tick warning reported in the actor phase). Formatting stays on the caller so the
		/// timestamp is the event's, only parsing and writing move.
		/// </summary>
		private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> PendingWrites = new();
		private static readonly SemaphoreSlim PendingSignal = new(0);

		/// <summary>Upper bound of lines waiting to be written; beyond it lines are dropped.</summary>
		private const int MaxPendingWrites = 20000;

		static ConsoleTarget()
		{
			var writer = new Thread(WriterLoop)
			{
				IsBackground = true,
				Name = "console-log-writer"
			};
			writer.Start();

			// Command line runs exit before the writer drains; flush what is already queued.
			AppDomain.CurrentDomain.ProcessExit += (_, __) =>
			{
				while (PendingWrites.TryDequeue(out var write))
				{
					try { write(); } catch { }
				}
			};
		}

		private static void WriterLoop()
		{
			while (true)
			{
				PendingSignal.Wait();

				while (PendingWrites.TryDequeue(out var write))
				{
					try
					{
						write();
					}
					catch
					{
						// A broken console must not take the writer thread down.
					}
				}
			}
		}

		private static void QueueWrite(Action write)
		{
			if (PendingWrites.Count > MaxPendingWrites)
				return; // drop under load instead of letting the queue grow without bound

			PendingWrites.Enqueue(write);
			PendingSignal.Release();
		}

		/// <param name="minLevel">Minimum level of messages to emit</param>
		/// <param name="maxLevel">Maximum level of messages to emit</param>
		/// <param name="includeTimeStamps">Include timestamps in log?</param>
		public ConsoleTarget(Logger.Level minLevel, Logger.Level maxLevel, bool includeTimeStamps, string timeStampFormat)
		{
			MinimumLevel = minLevel;
			MaximumLevel = maxLevel;
			IncludeTimeStamps = includeTimeStamps;
			TimeStampFormat = timeStampFormat;
		}
		
		
		/// <param name="level">Log level.</param>
		/// <param name="logger">Source of the log message.</param>
		/// <param name="message">Log message.</param>
		public override void LogMessage(Logger.Level level, string logger, string message)
		{
			var timeStamp = IncludeTimeStamps ? "[[" + DateTime.Now.ToString(TimeStampFormat) + "]] " : "";
			var markup = $"{timeStamp}{SetColor(level, true)}[[{level.ToString(),8}]][/] {SetColor(level)}[[{Cleanup(logger),20}]]: {AnsiTarget.Cleanup(message)}[/]";

			QueueWrite(() => AnsiConsole.MarkupLine(markup));
		}

		/// <param name="level">Log level.</param>
		/// <param name="logger">Source of the log message.</param>
		/// <param name="message">Log message.</param>
		/// <param name="exception">Exception to be included with log message.</param>
		public override void LogException(Logger.Level level, string logger, string message, Exception exception)
		{
			var timeStamp = IncludeTimeStamps ? "[[" + DateTime.Now.ToString(TimeStampFormat) + "]] " : "";
			
			var markup = $"{timeStamp}{SetColor(level, true)}[[{level.ToString(),8}]][/] {SetColor(level)}[[{Cleanup(logger),20}]]: {Cleanup(message)}[/] - [underline red on white][[{exception.GetType().Name}]][/][red] {Cleanup(exception.Message)}[/]";

			QueueWrite(() =>
			{
				AnsiConsole.MarkupLine(markup);
				AnsiConsole.WriteException(exception);
			});
		}
		
		
		/// <summary>
		/// Performs a cleanup on the target.
		/// All [ becomes [[, and ] becomes ]] (for ignoring ANSI codes)
		/// To use a style, use $[..]$abc$[/]$.
		/// Example:
		/// Logger.Warn("This is a $[red]$red$[/]$ message");
		/// instead of
		/// Logger.Warn("This is a [red]red[/] message");
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		string Cleanup(string x) => AnsiTarget.Beautify(x.Replace("[", "[[").Replace("]", "]]").Replace("$[[/]]$", "[/]").Replace("$[[", "[").Replace("]]$", "]"));
		

		/// <param name="level"></param>
		private static string SetColor(Logger.Level level, bool withBackground = false)
        {
            string postfix = withBackground ? " on grey19" : "";
            return level switch
            {
                Logger.Level.PacketDump => $"[grey30{postfix}]",
                Logger.Level.Debug => $"[grey39{postfix}]",
                Logger.Level.Trace => $"[purple{postfix}]",
                Logger.Level.Info => $"[white{postfix}]",
                Logger.Level.QuestLog => $"[darkseagreen2{postfix}]",
                Logger.Level.Success => $"[green3_1{postfix}]",
                Logger.Level.Warn => $"[darkorange3_1{postfix}]",
                Logger.Level.Error => $"[indianred1{postfix}]",
                Logger.Level.Fatal => $"[red3{postfix}]",
                _ => $"[grey54{postfix}]"
            };
        }
	}
}
