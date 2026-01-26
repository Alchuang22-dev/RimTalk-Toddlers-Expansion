using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 追踪幼儿背负关系的静态追踪器。
	/// 使用Dictionary存储载体和被背幼儿的映射关系。
	/// </summary>
	public static class ToddlerCarryingTracker
	{
		/// <summary>
		/// 幼儿 -> 载体 的映射
		/// </summary>
		private static readonly Dictionary<Pawn, Pawn> ToddlerToCarrier = new Dictionary<Pawn, Pawn>();

		/// <summary>
		/// 载体 -> 幼儿列表 的映射
		/// </summary>
		private static readonly Dictionary<Pawn, List<Pawn>> CarrierToToddlers = new Dictionary<Pawn, List<Pawn>>();

		/// <summary>
		/// 注册背负关系
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <param name="toddler">幼儿</param>
		public static void RegisterCarrying(Pawn carrier, Pawn toddler)
		{
			if (carrier == null || toddler == null)
			{
				return;
			}

			// 先清除幼儿之前的背负关系
			UnregisterCarrying(toddler);

			// 注册新关系
			ToddlerToCarrier[toddler] = carrier;

			if (!CarrierToToddlers.TryGetValue(carrier, out List<Pawn> toddlers))
			{
				toddlers = new List<Pawn>();
				CarrierToToddlers[carrier] = toddlers;
			}

			if (!toddlers.Contains(toddler))
			{
				toddlers.Add(toddler);
			}
		}

		/// <summary>
		/// 取消幼儿的背负关系
		/// </summary>
		/// <param name="toddler">幼儿</param>
		public static void UnregisterCarrying(Pawn toddler)
		{
			if (toddler == null)
			{
				return;
			}

			if (!ToddlerToCarrier.TryGetValue(toddler, out Pawn carrier))
			{
				return;
			}

			ToddlerToCarrier.Remove(toddler);

			if (CarrierToToddlers.TryGetValue(carrier, out List<Pawn> toddlers))
			{
				toddlers.Remove(toddler);
				if (toddlers.Count == 0)
				{
					CarrierToToddlers.Remove(carrier);
				}
			}
		}

		/// <summary>
		/// 获取背着指定幼儿的载体
		/// </summary>
		/// <param name="toddler">幼儿</param>
		/// <returns>载体</returns>
		public static Pawn GetCarrier(Pawn toddler)
		{
			if (toddler == null)
			{
				return null;
			}

			ToddlerToCarrier.TryGetValue(toddler, out Pawn carrier);
			return carrier;
		}

		/// <summary>
		/// 获取载体背着的所有幼儿
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>幼儿列表（副本）</returns>
		public static List<Pawn> GetCarriedToddlers(Pawn carrier)
		{
			if (carrier == null)
			{
				return new List<Pawn>();
			}

			if (CarrierToToddlers.TryGetValue(carrier, out List<Pawn> toddlers))
			{
				return new List<Pawn>(toddlers);
			}

			return new List<Pawn>();
		}

		/// <summary>
		/// 清除所有与指定pawn相关的背负数据
		/// </summary>
		/// <param name="pawn">pawn</param>
		public static void ClearPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			// 如果是幼儿，取消其背负关系
			UnregisterCarrying(pawn);

			// 如果是载体，取消所有其背着的幼儿的关系
			if (CarrierToToddlers.TryGetValue(pawn, out List<Pawn> toddlers))
			{
				// 复制列表以避免迭代时修改
				List<Pawn> toddlersCopy = new List<Pawn>(toddlers);
				for (int i = 0; i < toddlersCopy.Count; i++)
				{
					UnregisterCarrying(toddlersCopy[i]);
				}
			}
		}

		/// <summary>
		/// 清除所有背负数据
		/// </summary>
		public static void ClearAll()
		{
			ToddlerToCarrier.Clear();
			CarrierToToddlers.Clear();
		}

		/// <summary>
		/// 清理无效的条目（死亡、销毁的pawn）
		/// </summary>
		public static void CleanupInvalidEntries()
		{
			// 找出无效的幼儿
			List<Pawn> invalidToddlers = new List<Pawn>();
			foreach (KeyValuePair<Pawn, Pawn> kvp in ToddlerToCarrier)
			{
				Pawn toddler = kvp.Key;
				Pawn carrier = kvp.Value;

				if (toddler == null || toddler.Dead || toddler.Destroyed ||
					carrier == null || carrier.Dead || carrier.Destroyed)
				{
					invalidToddlers.Add(toddler);
				}
			}

			// 清除无效条目
			for (int i = 0; i < invalidToddlers.Count; i++)
			{
				UnregisterCarrying(invalidToddlers[i]);
			}
		}

		/// <summary>
		/// 获取所有正在背负幼儿的载体
		/// </summary>
		/// <returns>载体列表</returns>
		public static List<Pawn> GetAllCarriers()
		{
			return new List<Pawn>(CarrierToToddlers.Keys);
		}

		/// <summary>
		/// 获取所有被背着的幼儿
		/// </summary>
		/// <returns>幼儿列表</returns>
		public static List<Pawn> GetAllCarriedToddlers()
		{
			return new List<Pawn>(ToddlerToCarrier.Keys);
		}
	}
}