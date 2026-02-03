using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.AI
{
	/// <summary>
	/// 让成年人去抱起同 Lord 中未被背的幼儿
	/// 用于商队/访客离开地图前的 duty
	/// </summary>
	public class JobGiver_PickUpUncarriedToddler : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			// 检查是否是有效的载体
			if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
			{
				return null;
			}

			// 检查是否还有背负容量
			int currentCount = ToddlerCarryingUtility.GetCarriedToddlerCount(pawn);
			int maxCapacity = ToddlerCarryingUtility.GetMaxCarryCapacity(pawn);
			if (currentCount >= maxCapacity)
			{
				return null;
			}

			// 获取 pawn 所属的 Lord
			Lord lord = pawn.GetLord();
			if (lord == null || lord.ownedPawns.NullOrEmpty())
			{
				return null;
			}

			// 找到同 Lord 中未被背且可以被预约的幼儿
			Pawn toddlerToPickUp = FindUncarriedAndReservableToddler(pawn, lord);
			if (toddlerToPickUp == null)
			{
				return null;
			}

			// 创建抱幼儿的 Job
			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddlerToPickUp);
			job.locomotionUrgency = LocomotionUrgency.Jog;

			return job;
		}

		/// <summary>
		/// 找到同 Lord 中距离最近的未被背且可以被预约的幼儿
		/// </summary>
		private Pawn FindUncarriedAndReservableToddler(Pawn carrier, Lord lord)
		{
			Pawn closestToddler = null;
			float closestDist = float.MaxValue;

			List<Pawn> pawns = lord.ownedPawns;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				
				// 检查是否是可以被背的幼儿
				if (!ToddlerCarryingUtility.CanBeCarried(p))
					continue;

				// 检查是否已经被背着
				if (ToddlerCarryingUtility.IsBeingCarried(p))
					continue;

				// 检查是否可以到达
				if (!carrier.CanReach(p, PathEndMode.Touch, Danger.Some))
					continue;

				// 关键检查：是否可以被预约（没有其他人正在去抱这个幼儿）
				if (!carrier.CanReserve(p, 1, -1, null, false))
					continue;

				// 计算距离
				float dist = carrier.Position.DistanceToSquared(p.Position);
				if (dist < closestDist)
				{
					closestDist = dist;
					closestToddler = p;
				}
			}

			return closestToddler;
		}
	}
}