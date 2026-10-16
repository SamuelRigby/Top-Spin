using UnityEngine;

public class CharacterAnimationAudio : MonoBehaviour
{
    [Header("Footsteps")]
    [SerializeField] private RandomSfxPlayer footstepWalk;
    [SerializeField] private RandomSfxPlayer footstepRun;
    [SerializeField] private RandomSfxPlayer footstepJump;
    [SerializeField] private RandomSfxPlayer footstepLand;

    [Header("Cloth")]
    [SerializeField] private RandomSfxPlayer clothWalk;
    [SerializeField] private RandomSfxPlayer clothRun;
    [SerializeField] private RandomSfxPlayer clothJump;
    [SerializeField] private RandomSfxPlayer clothLand;

    // Footsteps
    public void FootstepWalk() => Play(footstepWalk);
    public void FootstepRun()  => Play(footstepRun);
    public void FootstepJump() => Play(footstepJump);
    public void FootstepLand() => Play(footstepLand);

    // Cloth
    public void ClothWalk() => Play(clothWalk);
    public void ClothRun()  => Play(clothRun);
    public void ClothJump() => Play(clothJump);
    public void ClothLand() => Play(clothLand);

    private void Play(RandomSfxPlayer player)
    {
        if (player != null)
            player.Play();
    }
}