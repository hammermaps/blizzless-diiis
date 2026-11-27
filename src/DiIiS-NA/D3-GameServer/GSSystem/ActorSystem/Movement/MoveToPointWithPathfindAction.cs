using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Actions;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem;
using DiIiS_NA.GameServer.GSSystem.TickerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using System;
using System.Linq;

namespace DiIiS_NA.GameServer.GSSystem.ActorSystem.Movement
{
	public class MoveToPointWithPathfindAction : ActorAction
	{
		public Vector3D Heading { get; private set; }
		public float AttackRadius { get; private set; }

		private int _lastDelay = 0;
		//private int _totalDelay = 0;

		private TickTimer Timer;
		private WalkStep _step;
		private bool Stucked = true;

		public MoveToPointWithPathfindAction(Actor owner, Vector3D heading, float attackRadius = 6f)
			: base(owner)
		{
			Heading = heading;
			AttackRadius = attackRadius;
		}

		public override void Start(int tickCounter)
		{
			if (Heading == Owner.Position)
			{
				Started = true;
				Done = true;
				return;
			}

			Started = true;
			
			if (!Done)
				Move();
		}

		private void Move()
		{
			Stucked = true;
			Vector3D destPoint = PowerMath.TranslateDirection2D(Owner.Position, Heading, Owner.Position, Math.Min(MovementHelpers.GetDistance(Owner.Position, Heading), Owner.WalkSpeed * 60));
			if (Owner.Spawner)
				destPoint = Owner.Position;

			var points = PowerMath.GenerateSpreadPositions(Owner.Position, destPoint, 30f, 6).OrderBy((pos) => MovementHelpers.GetDistance(pos, destPoint)).ToList();
			foreach (var point in points)
			{
				bool point_accessible = true;
				for (float i = 0.5f; i <= MovementHelpers.GetDistance(Owner.Position, point); i += 1f)
				{
					var point_check = PowerMath.TranslateDirection2D(Owner.Position, point, Owner.Position, i);
					if (!(Owner.World.CheckLocationForFlag(point_check, DiIiS_NA.Core.MPQ.FileFormats.Scene.NavCellFlags.AllowWalk) && !Owner.World.CheckLocationForFlag(point_check, DiIiS_NA.Core.MPQ.FileFormats.Scene.NavCellFlags.NoNavMeshIntersected)))
					{
						point_accessible = false;
						break;
					}
				}

				if (point_accessible)
				{
					destPoint = point;
					if (Owner.Spawner)
						destPoint = Owner.Position;
					Stucked = false;
					break;
				}
			}

			if (!Stucked && Owner.WalkSpeed > 0f)
			{
				destPoint = MovementHelpers.OnGround(Owner.World, destPoint);
				Owner.Move(destPoint, MovementHelpers.GetFacingAngle(Owner, destPoint));
				var delay = (int)(MovementHelpers.GetDistance(Owner.Position, destPoint) / Owner.WalkSpeed);
				_lastDelay = delay + (6 - (delay % 6));
				_step = new WalkStep(Owner, destPoint);

			}
			else
			{
				_lastDelay = 60;
				_step = null;
			}

			Timer = TickTimer.WaitTicks(
				Owner.World.Game,
				_lastDelay,
				(tick) =>
				{
					// The client stops at the destination; the delay is rounded up, so don't walk on past it.
					_step?.Finish();

					if (Owner == null ||
						Owner.Attributes[GameAttributes.Hitpoints_Cur] == 0 ||
						!HasPlayerWithin(Owner, 50f) ||
						MovementHelpers.GetDistance(Owner.Position, Heading) < AttackRadius ||
						(Owner is Monster && (Owner as Monster).Brain.CurrentAction == null)
					)
					{
						Done = true;
						//ТЕСТ
						Owner.World.BroadcastIfRevealed(plr => new MessageSystem.Message.Definitions.ACD.ACDTranslateSyncMessage()
						{ ActorId = Owner.DynamicID(plr), Position = Owner.Position, Snap = false }, Owner);

					}
					if (!Done)
					{
						Move();
					}
				}
			);
		}

		/// <summary>
		/// Player proximity check for the movement callback. The previous implementation asked the
		/// quad-tree for a fresh list on every movement step just to test for emptiness.
		/// </summary>
		private static bool HasPlayerWithin(Actor owner, float range)
		{
			var world = owner?.World;
			if (world == null)
				return false;

			var rangeSquared = range * range;
			foreach (var player in world.Players.Values)
			{
				if (player == null || player.Position == null) continue;
				var dx = player.Position.X - owner.Position.X;
				var dy = player.Position.Y - owner.Position.Y;
				if (dx * dx + dy * dy <= rangeSquared)
					return true;
			}

			return false;
		}

		public override void Update(int tickCounter)
		{
			_step?.Follow();
			if (Timer != null)
				Timer.Update(tickCounter);
		}

		public override void Cancel(int tickCounter)
		{
			Done = true;
			_step?.Follow();
		}
	}
}
