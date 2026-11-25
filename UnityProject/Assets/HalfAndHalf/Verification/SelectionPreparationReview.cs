#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using System.Collections.Generic;
using UnityEngine;
namespace HalfAndHalf.Verification
{
 // QA instrumentation only. Active solely for synchronous Select; all snapshots are taken after it returns.
 public static class SelectionPreparationReview
 {
  public static bool Active,PreparedCacheLoadedThisSelect;
  public static double CacheLoadMs,VisualMs, HeaderUiMs, DefinitionMs, GameInitMs, HudMs, SaveMs, CarGridMs, CarVolumeMs, CarCacheMeshMs, CarWholeMs, VoxelGridMs, VoxelWholeMs, VoxelDecorMs, ExteriorCacheMs, ExteriorEdgesMs, ExteriorCapsMs, SaveSerializeMs, SaveFlushMs, SaveValidateMs, SaveCommitMs;
  public static double Start()=>Active?Time.realtimeSinceStartupAsDouble:0;
  public static void Finish(ref double slot,double started){if(Active)slot+=(Time.realtimeSinceStartupAsDouble-started)*1000;}
  public static void Begin(){CacheLoadMs=0;PreparedCacheLoadedThisSelect=false;VisualMs=0;HeaderUiMs=0;DefinitionMs=0;GameInitMs=0;HudMs=0;SaveMs=0;CarGridMs=0;CarVolumeMs=0;CarCacheMeshMs=0;CarWholeMs=0;VoxelGridMs=0;VoxelWholeMs=0;VoxelDecorMs=0;ExteriorCacheMs=0;ExteriorEdgesMs=0;ExteriorCapsMs=0;SaveSerializeMs=0;SaveFlushMs=0;SaveValidateMs=0;SaveCommitMs=0;Active=true;}
  public static void End(){Active=false;}
  public static Dictionary<string,double> Snapshot()=>new Dictionary<string,double>{{"cacheLoadMs",CacheLoadMs},{"visualMs",VisualMs},{"headerUiMs",HeaderUiMs},{"definitionMs",DefinitionMs},{"gameInitMs",GameInitMs},{"hudMs",HudMs},{"saveMs",SaveMs},{"carGridMs",CarGridMs},{"carVolumeMs",CarVolumeMs},{"carCacheMeshMs",CarCacheMeshMs},{"carWholeMs",CarWholeMs},{"voxelGridMs",VoxelGridMs},{"voxelWholeMs",VoxelWholeMs},{"voxelDecorMs",VoxelDecorMs},{"exteriorCacheMs",ExteriorCacheMs},{"exteriorEdgesMs",ExteriorEdgesMs},{"exteriorCapsMs",ExteriorCapsMs},{"saveSerializeMs",SaveSerializeMs},{"saveFlushMs",SaveFlushMs},{"saveValidateMs",SaveValidateMs},{"saveCommitMs",SaveCommitMs}};
 }
}

#endif
