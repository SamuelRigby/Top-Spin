using UnityEngine;

/// <summary>
/// Composant OPTIONNEL prêt à l'emploi : un container + quelques AudioSources en round-robin.
/// Chaque son a son propre pitch/volume, même quand plusieurs sons se chevauchent.
/// Appelle Play() depuis n'importe quel script (animation event, UnityEvent, code...).
/// </summary>
public class RandomSfxPlayer : MonoBehaviour
{
    [SerializeField] private RandomAudioContainer container = new RandomAudioContainer();

    [Tooltip("AudioSources utilisées en rotation (désactive 'Play On Awake' sur chacune). " +
             "Plus il y en a, plus de sons peuvent se chevaucher.")]
    [SerializeField] private AudioSource[] voices;

    private int _nextVoice;

    private void Reset()
    {
        voices = GetComponents<AudioSource>();
    }

    private void OnValidate()
    {
        container?.Validate();
    }

    /// <summary>Retourne false si rien n'a été joué (container vide, pas de source...).</summary>
    public bool Play()
    {
        if (voices == null || voices.Length == 0) return false;
        if (!container.TryPick(out AudioPick pick)) return false;

        _nextVoice %= voices.Length;
        AudioSource voice = voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % voices.Length;

        if (voice == null) return false;

        voice.clip = pick.Clip;
        voice.volume = pick.Volume;
        voice.pitch = pick.Pitch;
        voice.Play(); // coupe le son précédent de CETTE source uniquement
        return true;
    }
}
