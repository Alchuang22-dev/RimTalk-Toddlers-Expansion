using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Kiiro
{
	public sealed class KiiroRefugeeBabyGuardComponent : GameComponent
	{
		private readonly HashSet<int> _protectedPawnIds = new HashSet<int>();

		public KiiroRefugeeBabyGuardComponent(Game game)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();

			List<int> ids = null;
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				ids = _protectedPawnIds.ToList();
			}

			Scribe_Collections.Look(ref ids, "kiiroRefugeeBabyProtectedPawnIds", LookMode.Value);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_protectedPawnIds.Clear();
				if (ids != null)
				{
					for (int i = 0; i < ids.Count; i++)
					{
						_protectedPawnIds.Add(ids[i]);
					}
				}
			}
		}

		public void MarkProtected(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			_protectedPawnIds.Add(pawn.thingIDNumber);
		}

		public bool IsProtected(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			int id = pawn.thingIDNumber;
			if (!_protectedPawnIds.Contains(id))
			{
				return false;
			}

			if (pawn.Destroyed || pawn.Dead || pawn.Faction == Faction.OfPlayer)
			{
				_protectedPawnIds.Remove(id);
				return false;
			}

			return true;
		}

		public void Unmark(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			_protectedPawnIds.Remove(pawn.thingIDNumber);
		}

		public static KiiroRefugeeBabyGuardComponent Get()
		{
			return Current.Game?.GetComponent<KiiroRefugeeBabyGuardComponent>();
		}
	}

	public static class KiiroRefugeeBabyGuardUtility
	{
		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<KiiroRefugeeBabyGuardComponent>() == null)
			{
				Current.Game.components.Add(new KiiroRefugeeBabyGuardComponent(Current.Game));
			}
		}

		public static void MarkOrphanPawn(Pawn pawn)
		{
			KiiroRefugeeBabyGuardComponent component = KiiroRefugeeBabyGuardComponent.Get();
			component?.MarkProtected(pawn);
		}

		public static bool ShouldPreventLeavingMap(Pawn pawn)
		{
			KiiroRefugeeBabyGuardComponent component = KiiroRefugeeBabyGuardComponent.Get();
			return component?.IsProtected(pawn) == true;
		}

		public static void ClearProtectedState(Pawn pawn)
		{
			KiiroRefugeeBabyGuardComponent component = KiiroRefugeeBabyGuardComponent.Get();
			component?.Unmark(pawn);
		}
	}
}
