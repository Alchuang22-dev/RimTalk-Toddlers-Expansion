using System;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 抱着幼儿玩耍的工具类。
	/// 处理效果应用、冷却期、RimTalk对话等。
	/// </summary>
	public static class CarriedPlayUtility
	{
		/// <summary>
		/// 玩乐值增加量
		/// </summary>
		private const float JoyGainAmount = 0.15f;

		/// <summary>
		/// 孤独值降低量（负数表示降低）
		/// </summary>
		private const float LonelinessReduction = -0.2f;

		/// <summary>
		/// 检查成年人是否可以和幼儿玩耍（没有冷却期）
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <returns>是否可以玩耍</returns>
		public static bool CanPlayWithToddler(Pawn carrier)
		{
			if (carrier == null)
			{
				return false;
			}

			// 检查是否正在抱着幼儿
			if (!ToddlerCarryingUtility.IsCarryingToddler(carrier))
			{
				return false;
			}

			// 检查冷却期
			if (HasPlayCooldown(carrier))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 检查幼儿是否有玩耍冷却期
		/// </summary>
		/// <param name="toddler">幼儿</param>
		/// <returns>是否有冷却期</returns>
		public static bool HasPlayCooldown(Pawn toddler)
		{
			if (toddler?.health?.hediffSet == null)
			{
				return false;
			}

			return toddler.health.hediffSet.HasHediff(ToddlersExpansionHediffDefOf.RimTalk_CarriedPlayCooldown);
		}

		/// <summary>
		/// 应用玩耍冷却期到幼儿
		/// </summary>
		/// <param name="toddler">幼儿</param>
		public static void ApplyPlayCooldown(Pawn toddler)
		{
			if (toddler?.health?.hediffSet == null)
			{
				return;
			}

			HediffDef cooldownDef = ToddlersExpansionHediffDefOf.RimTalk_CarriedPlayCooldown;
			if (cooldownDef == null)
			{
				return;
			}

			if (!toddler.health.hediffSet.HasHediff(cooldownDef))
			{
				Hediff hediff = HediffMaker.MakeHediff(cooldownDef, toddler);
				toddler.health.AddHediff(hediff);
			}
		}

		/// <summary>
		/// 应用飞高高效果
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		public static void ApplyTossUpEffects(Pawn carrier, Pawn toddler)
		{
			ApplyCommonEffects(carrier, toddler, ToddlersExpansionThoughtDefOf.RimTalk_ToddlerTossedUp, "TossUp");
		}

		/// <summary>
		/// 应用逗弄效果
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		public static void ApplyTickleEffects(Pawn carrier, Pawn toddler)
		{
			ApplyCommonEffects(carrier, toddler, ToddlersExpansionThoughtDefOf.RimTalk_ToddlerTickled, "Tickle");
		}

		/// <summary>
		/// 应用转圈效果
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		public static void ApplySpinAroundEffects(Pawn carrier, Pawn toddler)
		{
			ApplyCommonEffects(carrier, toddler, ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSpunAround, "SpinAround");
		}

		/// <summary>
		/// 应用通用效果
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		/// <param name="toddlerThought">幼儿心情</param>
		/// <param name="playType">玩耍类型（用于RimTalk）</param>
		private static void ApplyCommonEffects(Pawn carrier, Pawn toddler, ThoughtDef toddlerThought, string playType)
		{
			if (carrier == null || toddler == null)
			{
				return;
			}

			// 1. 给幼儿心情加成
			if (toddlerThought != null && toddler.needs?.mood?.thoughts?.memories != null)
			{
				toddler.needs.mood.thoughts.memories.TryGainMemory(toddlerThought, carrier);
			}

			// 2. 给成年人心情加成
			if (ToddlersExpansionThoughtDefOf.RimTalk_PlayedWithToddler != null && carrier.needs?.mood?.thoughts?.memories != null)
			{
				carrier.needs.mood.thoughts.memories.TryGainMemory(ToddlersExpansionThoughtDefOf.RimTalk_PlayedWithToddler, toddler);
			}

			// 3. 增加幼儿玩乐值
			if (toddler.needs?.joy != null)
			{
				toddler.needs.joy.GainJoy(JoyGainAmount, JoyKindDefOf.Social);
			}

			// 4. 降低幼儿孤独值（如果有社交需求）
			// 使用DefDatabase查找Social需求，因为NeedDefOf可能没有Social
			NeedDef socialNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("Social");
			if (socialNeedDef != null)
			{
				Need socialNeed = toddler.needs?.TryGetNeed(socialNeedDef);
				if (socialNeed != null)
				{
					socialNeed.CurLevel -= LonelinessReduction; // 负数变正，增加社交满足
				}
			}

			// 5. 增加成年人娱乐值
			if (carrier.needs?.joy != null)
			{
				carrier.needs.joy.GainJoy(JoyGainAmount * 0.5f, JoyKindDefOf.Social);
			}

			// 6. 应用冷却期到幼儿
			ApplyPlayCooldown(toddler);

			// 7. 发起RimTalk对话
			TryQueueRimTalkConversation(carrier, toddler, playType);

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] {carrier.Name} played {playType} with {toddler.Name}");
			}
		}

		/// <summary>
		/// 尝试发起RimTalk对话
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		/// <param name="playType">玩耍类型</param>
		private static void TryQueueRimTalkConversation(Pawn carrier, Pawn toddler, string playType)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			try
			{
				string prompt = GeneratePlayPrompt(carrier, toddler, playType);
				RimTalkCompatUtility.TryQueueTalk(carrier, toddler, prompt, "User");
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to queue RimTalk conversation: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 生成玩耍对话提示
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <param name="toddler">幼儿</param>
		/// <param name="playType">玩耍类型</param>
		/// <returns>对话提示</returns>
		private static string GeneratePlayPrompt(Pawn carrier, Pawn toddler, string playType)
		{
			string carrierName = carrier.Name?.ToStringShort ?? "Adult";
			string toddlerName = toddler.Name?.ToStringShort ?? "Toddler";

			string playDescription = playType switch
			{
				"TossUp" => $"{carrierName} is tossing {toddlerName} up in the air playfully. The toddler is giggling with joy!",
				"Tickle" => $"{carrierName} is tickling {toddlerName}, making them laugh uncontrollably!",
				"SpinAround" => $"{carrierName} is spinning around while holding {toddlerName}, who is squealing with delight!",
				_ => $"{carrierName} is playing with {toddlerName}."
			};

			return $"{playDescription} Generate a short, heartwarming interaction between them. The adult should speak lovingly to the toddler, and the toddler can respond with simple baby talk or giggles.";
		}

		/// <summary>
		/// 获取冷却期剩余时间描述
		/// </summary>
		/// <param name="carrier">成年人</param>
		/// <returns>剩余时间描述</returns>
		public static string GetCooldownRemainingText(Pawn carrier)
		{
			if (carrier?.health?.hediffSet == null)
			{
				return "";
			}

			Hediff cooldown = carrier.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_CarriedPlayCooldown);
			if (cooldown == null)
			{
				return "";
			}

			HediffComp_Disappears comp = cooldown.TryGetComp<HediffComp_Disappears>();
			if (comp == null)
			{
				return "";
			}

			int ticksRemaining = comp.ticksToDisappear;
			return ticksRemaining.ToStringTicksToPeriod();
		}
	}
}
