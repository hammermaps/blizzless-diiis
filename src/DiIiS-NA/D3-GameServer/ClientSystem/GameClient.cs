using DiIiS_NA.Core.Logging;
using DiIiS_NA.GameServer.ClientSystem.Base;
using DiIiS_NA.GameServer.GSSystem.GameSystem;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Tick;
using DiIiS_NA.LoginServer.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiIiS_NA.Core.Extensions;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Base;

namespace DiIiS_NA.GameServer.ClientSystem
{
	public class GameClient : IClient
	{
		private static readonly Logger Logger = LogManager.CreateLogger("GameClient");

		public IConnection Connection { get; set; }
		public BattleClient BnetClient { get; set; }

		private readonly GameBitBuffer _incomingBuffer = new(512);
		private readonly GameBitBuffer _outgoingBuffer = new(ushort.MaxValue);

		private readonly object _clientStreamLock = new object();
		private readonly Queue<(byte[] Data, long Received)> _incomingData = new();
		private bool _parsing;

		public Game Game { get; set; }
		public Player Player { get; set; }

		protected bool _tickingEnabled = false;

		public bool TickingEnabled
		{
			get => _tickingEnabled;
			set => _tickingEnabled = value;
		}
		
		public bool IsLoggingOut { get; set; }

		public GameClient(IConnection connection)
		{
			TickingEnabled = false;
			Connection = connection;
			_outgoingBuffer.WriteInt(32, 0);
		}

		public virtual void Parse(ConnectionDataEventArgs e)
		{
			var received = Stopwatch.GetTimestamp();
			// The socket starts its next read as soon as this method returns. Own the bytes now,
			// and use one worker so delayed packets cannot overtake one another.
			var data = e.Data.ToArray();
			lock (_clientStreamLock)
			{
				_incomingData.Enqueue((data, received));
				if (_parsing) return;
				_parsing = true;
			}
			Task.Run(ParseIncoming);
		}

		private void ParseIncoming()
		{
			while (true)
			{
				(byte[] Data, long Received) incoming;
				lock (_clientStreamLock)
				{
					if (_incomingData.Count == 0 || !Connection.IsOpen())
					{
						_incomingData.Clear();
						_parsing = false;
						return;
					}
					incoming = _incomingData.Dequeue();
				}

				RecordWait(Stopwatch.GetElapsedTime(incoming.Received).TotalMilliseconds);
				try
				{
					_incomingBuffer.AppendData(incoming.Data);
					while (Connection.IsOpen() && _incomingBuffer.CheckAvailable(32))
					{
						int start = _incomingBuffer.Position;
						int size = _incomingBuffer.ReadInt(32);
						if (size < 4 || size > int.MaxValue / 8)
							throw new GameBitBufferException($"Invalid packet size: {size}");
						_incomingBuffer.Position = start;
						if (!_incomingBuffer.CheckAvailable(size * 8)) break;
						int end = start + size * 8;
						_incomingBuffer.Position += 32;

						while ((end - _incomingBuffer.Position) >= 10 && Connection.IsOpen())
						{
							var message = _incomingBuffer.ParseMessage();
							// Without a message class its length is unknown; skip the rest of this packet.
							if (message == null) break;
							if (_incomingBuffer.Position > end)
								throw new GameBitBufferException("Message exceeds its packet boundary.");
							try
							{
								Logger.LogIncomingPacket(message);
								if (message.Id is 96 or 369 or 269)
									message.Consumer = Consumers.Inventory;
								if (message.Id is (int)Opcodes.TeleportToWaypoint or (int)Opcodes.GambleMessage)
									message.Consumer = Consumers.Player;
								if (message.Consumer != Consumers.None)
								{
									if (message.Consumer == Consumers.ClientManager) ClientManager.Instance.Consume(this, message);
									else Game.Route(this, message);
								}
								else if (message is ISelfHandler handler) handler.Handle(this);
								// Declines and busy flags are pure client state; they need no server action.
								else if (message.Id is (int)Opcodes.SetDungeonJoinDeclinedMessage or (int)Opcodes.PlayerBusyMessage)
									Logger.Debug("Ignoring {0} - ID:{1}.", message.GetType().Name, message.Id);
								else if (message.Id != 217)
									Logger.Warn("{0} - ID:{1} has no consumer or self-handler.", message.GetType().Name, message.Id);
							}
							catch (NotImplementedException)
							{
								Logger.Warn("Unhandled game message: 0x{0:X4} {1}", message.Id, message.GetType().Name);
							}
						}
						_incomingBuffer.Position = end;
					}
					// Keep an incomplete header/payload for the next socket receive.
					_incomingBuffer.ConsumeData();
				}
				catch (Exception ex)
				{
					Logger.WarnException(ex, "Parse() exception: ");
					Connection.Disconnect();
				}
			}
		}

		private const int WaitStatsSeconds = 30;
		private readonly Stopwatch _waitStatsWatch = Stopwatch.StartNew();
		private double _waitTimeTotal, _waitTimeMax;
		private int _waitCount;

		/// <summary>
		/// Logs how long the client's data waited before being handled every <see cref="WaitStatsSeconds"/> seconds. It
		/// waits for a thread pool thread and for the client's previous data, so long waits delay the player's actions
		/// before the game sees them.
		/// </summary>
		private void RecordWait(double milliseconds)
		{
			_waitCount++;
			_waitTimeTotal += milliseconds;
			_waitTimeMax = Math.Max(_waitTimeMax, milliseconds);
			if (_waitStatsWatch.Elapsed.TotalSeconds < WaitStatsSeconds) return;

			Logger.Debug("Client data in the last {0}s: {1} packets, average wait {2:0.0} ms, slowest {3:0.0} ms.",
				WaitStatsSeconds, _waitCount, _waitTimeTotal / _waitCount, _waitTimeMax);
			_waitCount = 0;
			_waitTimeTotal = _waitTimeMax = 0;
			_waitStatsWatch.Restart();
		}

		public void SendMessage(Opcodes opcode) => SendMessage(new SimpleMessage(opcode));
		
		private int _lastReplicatedTick;
		public virtual void SendMessage(GameMessage message)
		{
			//System.Threading.Thread.Sleep(50);
			lock (_outgoingBuffer)
			{
				if (Game.TickCounter > _lastReplicatedTick && TickingEnabled && message is not GameTickMessage /*&& !(message is EndOfTickMessage)*/ && !Player.BetweenWorlds)
				{
					/*var endMessage = new EndOfTickMessage()
					{
							Field0 = this.Game.TickCounter,
							Field1 = this.LastReplicatedTick
					};
					Logger.LogOutgoingPacket(endMessage);
					_outgoingBuffer.EncodeMessage(endMessage);
					Connection.Send(_outgoingBuffer.GetPacketAndReset());*/

					_lastReplicatedTick = Game.TickCounter;
					var tickMessage = new GameTickMessage(Game.TickCounter);
					Logger.LogOutgoingPacket(tickMessage);
					_outgoingBuffer.EncodeMessage(tickMessage);
					Connection.Send(_outgoingBuffer.GetPacketAndReset());
					_dataSent = false;
				}
				//if (message is GameTickMessage)
				//message = new GameTickMessage(this.Game.TickCounter); //reassigning new tick value
				Logger.LogOutgoingPacket(message);
				_outgoingBuffer.EncodeMessage(message); // change ConsoleTarget's level to Level.Dump in program.cs if u want to see messages on console.

				if (TickingEnabled)
				{
					var data = _outgoingBuffer.GetPacketAndReset();
					Connection.Send(data);
				}
				_dataSent = true;
				//if (flushImmediately) this.SendTick();
			}
		}

		public void SendBytes(byte[] data) => Connection.Send(data);

		private bool _dataSent = true;

		public bool OpenWorldDefined = false;

		public void SendTick()
		{
			//if (_outgoingBuffer.Length <= 32) return;
			lock (_outgoingBuffer)
			{
				if (!_dataSent) return;

				if (TickingEnabled && Game.TickCounter > _lastReplicatedTick)
				{
					/*this.SendMessage(new EndOfTickMessage()
					{
						Field0 = this.Game.TickCounter,
						Field1 = this.LastReplicatedTick
					}); // send the tick end.*/
					SendMessage(new GameTickMessage(Game.TickCounter)); // send the tick.
					_lastReplicatedTick = Game.TickCounter;
					//this.SendMessage(new GameTickMessage(0)); //before client enters game causes freeze with PvP scoreboard
					/*this.SendMessage(new EndOfTickMessage()
					{
						Field0 = this.Game.TickCounter,
						Field1 = 0
					}); // send the tick end*/
					_dataSent = false;
					FlushOutgoingBuffer();
				}
			}
		}

		public virtual void FlushOutgoingBuffer()
		{
			lock (_outgoingBuffer)
			{
				if (_outgoingBuffer.Length <= 32) return;

				var data = _outgoingBuffer.GetPacketAndReset();
				Connection.Send(data);
			}
		}
	}

	public static class OpcodesExtensions
	{
		/// <summary>
		/// Sends a simple message with the given opcode.
		/// </summary>
		/// <param name="opcode">The opcode</param>
		/// <param name="clients">The InGameClients</param>
		public static void SendTo(this Opcodes opcode, params GameClient[] clients) { foreach (var client in clients) client.SendMessage(opcode); }
		
		/// <summary>
		/// Sends a message with a given opcode.
		/// </summary>
		/// <param name="message">Message to send to client</param>
		/// <param name="clients">The InGameClients</param>
		public static void SendTo(this GameMessage message, params GameClient[] clients)  { foreach (var client in clients) client.SendMessage(message); }
	}
}
