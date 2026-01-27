using System;
using System.Collections.Generic;
using RimWorld;
using RimTalk_ToddlersExpansion.Core;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 幼儿背负系统的公共API工具类�?
	/// 允许成年人背着/抱着幼儿移动，用于商队、访客等场景�?
	/// </summary>
	public static class ToddlerCarryingUtility
	{
		/// <summary>
		/// 幼儿在胸前的渲染偏移量（根据朝向�?
		/// X = 左右偏移，Y = 渲染图层（正值在前，负值在后），Z = 上下偏移
		/// </summary>
		private static readonly Dictionary<Rot4, Vector3> CarryOffsets = new Dictionary<Rot4, Vector3>
		{
			{ Rot4.North, new Vector3(-0.12f, -0.1f, -0.1f) },    // 面向北（背对镜头），幼儿在背后，图层在大人后�?
			{ Rot4.South, new Vector3(0.12f, 0.1f, -0.1f) },   // 面向南（正对镜头），幼儿在胸前偏右侧
			{ Rot4.East, new Vector3(0.15f, -0.05f, -0.05f) }, // 面向东，幼儿在左侧偏�?
			{ Rot4.West, new Vector3(0.15f, 0.05f, -0.05f) }   // 面向西，幼儿在右侧偏�?
		};

		/// <summary>
		/// 幼儿被抱着时的缩放比例
		/// </summary>
		private const float CarriedToddlerScale = 0.7f;

		/// <summary>
		/// 尝试让载体背起幼�?
		/// </summary>
		/// <param name="carrier">背负者（成年人）</param>
		/// <param name="toddler">被背的幼�?/param>
		/// <returns>是否成功</returns>
		public static bool TryMountToddler(Pawn carrier, Pawn toddler)
		{
			if (carrier == null || toddler == null)
			{
				return false;
			}

			// 验证载体是成年人
			if (!IsValidCarrier(carrier))
			{
				return false;
			}

			// 验证幼儿可以被背
			if (!CanBeCarried(toddler))
			{
				return false;
			}

			// 检查幼儿是否已经被背着
			if (IsBeingCarried(toddler))
			{
				return false;
			}

			// 检查载体是否已经背着太多幼儿
			if (GetCarriedToddlerCount(carrier) >= GetMaxCarryCapacity(carrier))
			{
				return false;
			}

			// 清除幼儿的寻路状态
			ClearToddlerPathingState(toddler);

			// 注册背负关系
			ToddlerCarryingTracker.RegisterCarrying(carrier, toddler);
			ToddlerCarryDesireUtility.TryEndWantToBeHeld(toddler, Prefs.DevMode);

			// 给幼儿分配"被抱着"的Job
			TryAssignBeingCarriedJob(toddler, carrier);

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] {carrier.Name} 开始背着 {toddler.Name}");
			}

			return true;
		}

		/// <summary>
		/// 尝试给幼儿分�?被抱着"的Job
		/// </summary>
		private static void TryAssignBeingCarriedJob(Pawn toddler, Pawn carrier)
		{
			if (toddler?.jobs == null || carrier == null)
			{
				return;
			}

			CarriedToddlerStateUtility.EnsureCarriedJob(toddler, carrier, true);
		}

		/// <summary>
		/// 让幼儿从载体身上下来
		/// </summary>
		/// <param name="toddler">被背的幼�?/param>
		/// <returns>是否成功</returns>
		public static bool DismountToddler(Pawn toddler)
		{
			if (toddler == null)
			{
				return false;
			}

			if (!IsBeingCarried(toddler))
			{
				return false;
			}

			Pawn carrier = GetCarrier(toddler);
			ToddlerCarryingTracker.UnregisterCarrying(toddler);

			// 结束幼儿�?被抱着"Job
			TryEndBeingCarriedJob(toddler);

			if (Prefs.DevMode && carrier != null)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] {toddler.Name} �?{carrier.Name} 身上下来");
			}

			return true;
		}

		/// <summary>
		/// 尝试结束幼儿�?被抱着"Job
		/// </summary>
		private static void TryEndBeingCarriedJob(Pawn toddler)
		{
			if (toddler?.jobs == null)
			{
				return;
			}

			try
			{
				if (CarriedToddlerStateUtility.IsCarriedStateJob(toddler.CurJobDef))
				{
					toddler.jobs.EndCurrentJob(JobCondition.Succeeded);
				}
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to end carried job: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 获取背着指定幼儿的载�?
		/// </summary>
		/// <param name="toddler">幼儿</param>
		/// <returns>载体，如果没有被背则返回null</returns>
		public static Pawn GetCarrier(Pawn toddler)
		{
			if (toddler == null)
			{
				return null;
			}

			return ToddlerCarryingTracker.GetCarrier(toddler);
		}

		/// <summary>
		/// 获取指定载体背着的所有幼�?
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>幼儿列表</returns>
		public static List<Pawn> GetCarriedToddlers(Pawn carrier)
		{
			if (carrier == null)
			{
				return new List<Pawn>();
			}

			return ToddlerCarryingTracker.GetCarriedToddlers(carrier);
		}

		/// <summary>
		/// 检查幼儿是否正在被背着
		/// </summary>
		/// <param name="toddler">幼儿</param>
		/// <returns>是否被背着</returns>
		public static bool IsBeingCarried(Pawn toddler)
		{
			return GetCarrier(toddler) != null;
		}

		/// <summary>
		/// 检查pawn是否正在背着幼儿
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>是否在背幼儿</returns>
		public static bool IsCarryingToddler(Pawn carrier)
		{
			return GetCarriedToddlerCount(carrier) > 0;
		}

		/// <summary>
		/// 获取载体背着的幼儿数�?
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>数量</returns>
		public static int GetCarriedToddlerCount(Pawn carrier)
		{
			return GetCarriedToddlers(carrier).Count;
		}

		/// <summary>
		/// 获取载体最多可以背几个幼儿
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>最大数�?/returns>
		public static int GetMaxCarryCapacity(Pawn carrier)
		{
			// 默认最多背1个，可以根据体型、能力等调整
			return 1;
		}

		/// <summary>
		/// 检查pawn是否可以作为载体
		/// </summary>
		/// <param name="pawn">pawn</param>
		/// <returns>是否可以</returns>
		public static bool IsValidCarrier(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			// 必须是人�?
			if (!pawn.RaceProps.Humanlike)
			{
				return false;
			}

			// 不能是幼儿或儿童
			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Baby() || pawn.DevelopmentalStage == DevelopmentalStage.Child)
			{
				return false;
			}

			// 不能倒下
			if (pawn.Downed)
			{
				return false;
			}

			// 必须能移�?
			if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 检查幼儿是否可以被�?
		/// </summary>
		/// <param name="pawn">幼儿</param>
		/// <returns>是否可以</returns>
		public static bool CanBeCarried(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			// 必须是幼儿或婴儿
			if (!ToddlersCompatUtility.IsToddler(pawn) && !pawn.DevelopmentalStage.Baby())
			{
				return false;
			}

			// 不能已经被背着
			if (IsBeingCarried(pawn))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 获取幼儿被抱着时相对于载体的渲染偏�?
		/// </summary>
		/// <param name="carrierRotation">载体的朝�?/param>
		/// <returns>偏移向量</returns>
		public static Vector3 GetCarryOffset(Rot4 carrierRotation)
		{
			if (CarryOffsets.TryGetValue(carrierRotation, out Vector3 offset))
			{
				return offset;
			}

			return Vector3.zero;
		}

		/// <summary>
		/// 获取幼儿被抱着时的缩放比例
		/// </summary>
		/// <returns>缩放比例</returns>
		public static float GetCarriedScale()
		{
			return CarriedToddlerScale;
		}

		/// <summary>
		/// 让商�?访客组中的成年人背起所有幼�?
		/// </summary>
		/// <param name="pawns">商队成员列表</param>
		public static void AutoAssignCarryingForGroup(List<Pawn> pawns)
		{
			if (pawns == null || pawns.Count == 0)
			{
				return;
			}

			// 找出所有可以作为载体的成年�?
			List<Pawn> carriers = new List<Pawn>();
			List<Pawn> toddlersToCarry = new List<Pawn>();

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (IsValidCarrier(pawn))
				{
					carriers.Add(pawn);
				}
				else if (CanBeCarried(pawn))
				{
					toddlersToCarry.Add(pawn);
				}
			}

			if (carriers.Count == 0 || toddlersToCarry.Count == 0)
			{
				return;
			}

			// 分配幼儿给成年人
			int carrierIndex = 0;
			for (int i = 0; i < toddlersToCarry.Count; i++)
			{
				Pawn toddler = toddlersToCarry[i];

				// 找到一个还有容量的载体
				int attempts = 0;
				while (attempts < carriers.Count)
				{
					Pawn carrier = carriers[carrierIndex];
					if (TryMountToddler(carrier, toddler))
					{
						break;
					}

					carrierIndex = (carrierIndex + 1) % carriers.Count;
					attempts++;
				}

				carrierIndex = (carrierIndex + 1) % carriers.Count;
			}

			if (Prefs.DevMode)
			{
				int carriedCount = toddlersToCarry.Count - toddlersToCarry.FindAll(t => !IsBeingCarried(t)).Count;
				Log.Message($"[RimTalk_ToddlersExpansion] 自动分配背负: {carriedCount}/{toddlersToCarry.Count} 个幼儿被背起");
			}
		}

		/// <summary>
		/// 清除所有与指定pawn相关的背负关�?
		/// </summary>
		/// <param name="pawn">pawn</param>
		public static void ClearAllCarryingRelations(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			// 如果是载体，放下所有幼�?
			List<Pawn> carried = GetCarriedToddlers(pawn);
			for (int i = carried.Count - 1; i >= 0; i--)
			{
				DismountToddler(carried[i]);
			}

			// 如果是幼儿，从载体身上下�?
			if (IsBeingCarried(pawn))
			{
				DismountToddler(pawn);
			}
		}

		/// <summary>
		/// 清除幼儿的寻路状态
		/// </summary>
		/// <param name="toddler">幼儿</param>
		private static void ClearToddlerPathingState(Pawn toddler)
		{
			if (toddler?.pather == null)
			{
				return;
			}

			try
			{
				// 停止任何正在进行的移动
				if (toddler.pather.Moving)
				{
					toddler.pather.StopDead();
				}
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to clear toddler pathing state: {ex.Message}");
				}
			}
		}
		
	}
}


