using HalfAndHalf.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HalfAndHalf.Gameplay
{
 [DefaultExecutionOrder(-100)]
 public sealed class SpaceTrialController:MonoBehaviour
 {
  public AppleGame Game;public SpaceTrialVisual Visual;
  public Button SwitchButton;public TMP_Text Label;
  public AudioClip[] PlanetClips,GalaxyClips;
  void Start(){SwitchButton.onClick.AddListener(()=>Select(Visual.Kind=="P04"?"G03":Visual.Kind=="G03"?"P01":"P04"));Select(Visual.Kind);}
  public void Select(string id)
  {
   Game.UiOpen=false;Visual.Kind=id;
   Label.text=id=="P04"?"JUPITER + 4 MOONS":id=="G03"?"COLLIDING GALAXIES":"MOON ROTATION PROTOTYPE";
   SwitchButton.GetComponentInChildren<TMP_Text>().text=id=="P04"?"GALAXIES":id=="G03"?"MOON":"JUPITER";
   // P01/P04 use existing P02 clips temporarily; neither is final object-specific audio.
   Game.Presentation.Audio.ResetAttempt();Game.Presentation.Audio.CutVariants=id=="G03"?GalaxyClips:PlanetClips;
   Game.Initialize(Visual.Definition());
  }
 }
}
