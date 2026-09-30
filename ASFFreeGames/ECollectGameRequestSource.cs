namespace Maxisoft.ASF;

internal enum ECollectGameRequestSource {
	None = 0,
	RequestedByUser = 1,
	Scheduled = 2,

	/// <summary>A bot logged on after the first scheduled run and is caught up using the cached list.</summary>
	BotLoggedOn = 3,
}
