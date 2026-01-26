using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 幼儿背负系统的GameComponent。
	/// 负责定期清理无效的背负关系。
	/// </summary>
	public class ToddlerCarryingGameComponent : GameComponent
	{
		/// <summary>
		/// 清理间隔（每600 ticks = 10秒清理一次）
		/// </summary>
		private const int CleanupInterval = 600;

		private int _tickCounter;

		public ToddlerCarryingGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			_tickCounter++;
			if (_tickCounter >= CleanupInterval)
			{
				_tickCounter = 0;
				ToddlerCarryingTracker.CleanupInvalidEntries();
			}
		}

		public override void StartedNewGame()
		{
			ToddlerCarryingTracker.ClearAll();
		}

		public override void LoadedGame()
		{
			// 游戏加载时清除所有背负关系
			// 因为我们不保存背负状态（背负是临时的，商队离开后就结束）
			ToddlerCarryingTracker.ClearAll();
		}

		public override void ExposeData()
		{
			// 不需要保存任何数据
			// 背负关系是临时的，不需要持久化
		}
	}
}