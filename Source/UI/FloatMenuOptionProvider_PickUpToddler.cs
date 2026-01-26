using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.UI
{
	/// <summary>
	/// 提供抱起幼儿的右键菜单选项（RimWorld 1.6方式）
	/// </summary>
	public class FloatMenuOptionProvider_PickUpToddler : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool RequiresManipulation => true;

		protected override bool AppliesInt(FloatMenuContext context)
		{
			Pawn pawn = context.FirstSelectedPawn;
			if (pawn == null || pawn.IsMutant)
			{
				return false;
			}
			
			// 检查是否是有效的载体
			if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
			{
				return false;
			}
			
			// 检查容量是否已满
			if (ToddlerCarryingUtility.GetCarriedToddlerCount(pawn) >= ToddlerCarryingUtility.GetMaxCarryCapacity(pawn))
			{
				return false;
			}
			
			return true;
		}

		protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
		{
			// 检查点击的pawn是否是幼儿或婴儿
			if (!IsToddlerOrBaby(clickedPawn))
			{
				return null;
			}
			
			// 不能抱起自己
			Pawn carrier = context.FirstSelectedPawn;
			if (carrier == clickedPawn)
			{
				return null;
			}
			
			// 载体不能是幼儿
			if (IsToddlerOrBaby(carrier))
			{
				return null;
			}
			
			// 检查是否已经被抱着
			if (ToddlerCarryingUtility.IsBeingCarried(clickedPawn))
			{
				return null;
			}
			
			// 不能抱起敌对派系的幼儿
			if (clickedPawn.Faction != null && clickedPawn.Faction.HostileTo(carrier.Faction))
			{
				return null;
			}
			
			// 检查能否到达
			if (!carrier.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				return new FloatMenuOption(
					"RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), 
					null);
			}
			
			// 检查是否有操作能力
			if (!carrier.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				return new FloatMenuOption(
					"RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort) + ": " + "Incapable".Translate().CapitalizeFirst(), 
					null);
			}
			
			// 创建有效的选项
			Pawn toddlerCopy = clickedPawn;
			return FloatMenuUtility.DecoratePrioritizedTask(
				new FloatMenuOption("RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort), delegate
				{
					toddlerCopy.SetForbidden(value: false, warnOnFail: false);
					Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddlerCopy);
					job.count = 1;
					carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}), 
				carrier, 
				clickedPawn);
		}
		
		/// <summary>
		/// 检查pawn是否是幼儿或婴儿
		/// </summary>
		private static bool IsToddlerOrBaby(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}
			
			// 检查是否是婴儿
			if (pawn.DevelopmentalStage.Baby())
			{
				return true;
			}
			
			// 检查是否是幼儿 - 使用LifeStage检测
			if (pawn.ageTracker?.CurLifeStage != null)
			{
				string lifeStageName = pawn.ageTracker.CurLifeStage.defName;
				if (lifeStageName == "HumanlikeToddler" || lifeStageName.Contains("Toddler"))
				{
					return true;
				}
			}
			
			return false;
		}
	}
}