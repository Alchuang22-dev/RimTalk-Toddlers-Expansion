using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.UI
{
	/// <summary>
	/// 当选中抱着幼儿的成年人时，提供"和幼儿玩"的Gizmo按钮。
	/// </summary>
	public static class Gizmo_CarriedPlay
	{
		/// <summary>
		/// 获取抱着幼儿时的玩耍Gizmo
		/// </summary>
		/// <param name="carrier">抱着幼儿的成年人</param>
		/// <returns>Gizmo列表</returns>
		public static IEnumerable<Gizmo> GetCarriedPlayGizmos(Pawn carrier)
		{
			if (carrier == null || !carrier.IsColonistPlayerControlled)
			{
				yield break;
			}

			// 检查是否正在抱着幼儿
			if (!ToddlerCarryingUtility.IsCarryingToddler(carrier))
			{
				yield break;
			}

			List<Pawn> carriedToddlers = ToddlerCarryingUtility.GetCarriedToddlers(carrier);
			if (carriedToddlers == null || carriedToddlers.Count == 0)
			{
				yield break;
			}

			Pawn toddler = carriedToddlers.FirstOrDefault();
			if (toddler == null)
			{
				yield break;
			}

			// 检查冷却期
			bool hasCooldown = CarriedPlayUtility.HasPlayCooldown(toddler);
			string cooldownReason = hasCooldown ? "RimTalk_CarriedPlayOnCooldown".Translate() : null;

			// 飞高高按钮
			Command_Action tossUpCommand = new Command_Action
			{
				defaultLabel = "RimTalk_TossUp".Translate(),
				defaultDesc = "RimTalk_TossUpDesc".Translate(toddler.LabelShort),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/TossUp", false) ?? TexCommand.Attack,
				action = () => StartCarriedPlayJob(carrier, toddler, ToddlersExpansionJobDefOf.RimTalk_CarriedPlay_TossUp),
				Order = 100f
			};
			if (hasCooldown)
			{
				tossUpCommand.Disable(cooldownReason);
			}
			yield return tossUpCommand;

			// 逗弄幼儿按钮
			Command_Action tickleCommand = new Command_Action
			{
				defaultLabel = "RimTalk_Tickle".Translate(),
				defaultDesc = "RimTalk_TickleDesc".Translate(toddler.LabelShort),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/Tickle", false) ?? TexCommand.DesirePower,
				action = () => StartCarriedPlayJob(carrier, toddler, ToddlersExpansionJobDefOf.RimTalk_CarriedPlay_Tickle),
				Order = 101f
			};
			if (hasCooldown)
			{
				tickleCommand.Disable(cooldownReason);
			}
			yield return tickleCommand;

			// 转圈按钮
			Command_Action spinCommand = new Command_Action
			{
				defaultLabel = "RimTalk_SpinAround".Translate(),
				defaultDesc = "RimTalk_SpinAroundDesc".Translate(toddler.LabelShort),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/SpinAround", false) ?? TexCommand.ClearPrioritizedWork,
				action = () => StartCarriedPlayJob(carrier, toddler, ToddlersExpansionJobDefOf.RimTalk_CarriedPlay_SpinAround),
				Order = 102f
			};
			if (hasCooldown)
			{
				spinCommand.Disable(cooldownReason);
			}
			yield return spinCommand;
		}

		/// <summary>
		/// 开始抱着幼儿玩耍的Job
		/// </summary>
		private static void StartCarriedPlayJob(Pawn carrier, Pawn toddler, JobDef jobDef)
		{
			if (carrier == null || toddler == null || jobDef == null)
			{
				return;
			}

			// 检查冷却期
			if (CarriedPlayUtility.HasPlayCooldown(toddler))
			{
				Messages.Message("RimTalk_CarriedPlayOnCooldown".Translate(), MessageTypeDefOf.RejectInput, false);
				return;
			}

			// 创建并开始Job
			Job job = JobMaker.MakeJob(jobDef, toddler);
			carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, null, false, false);
		}
	}
}
