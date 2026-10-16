using UnityEngine;

/// <summary>
/// Composant OPTIONNEL prêt à l'emploi : un container + quelques AudioSources en round-robin.
/// Chaque son a son propre pitch/volume/fades, même quand plusieurs sons se chevauchent.
/// Appelle Play() depuis n'importe quel script (animation event, UnityEvent, code...).
/// Appelle StopAll() pour arrêter les sons en cours avec leur fade-out (indispensable pour les loops).
///
/// Attention : le container prend le contrôle du rolloff, du Spread et du Bypass Reverb Zones
/// des AudioSources listées (réécrits à chaque Play), ainsi que de leur volume/pitch/clip/loop.
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

        AudioSource voice = AcquireVoice();
        if (voice == null) return false;

        container.ConfigureSource(voice);       // rolloff, spread, reverb : réécrits à chaque lecture
        RandomAudioVoice.Play(voice, pick);     // fades, offset, loop
        return true;
    }

    /// <summary>Arrête tous les sons en cours avec le fade-out de chaque clip (0 = immédiat).</summary>
    public void StopAll()
    {
        if (voices == null) return;

        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i] != null && voices[i].TryGetComponent(out RandomAudioVoice voice))
                voice.Stop();
        }
    }

    // Préfère une source libre ; si toutes jouent, remplace la suivante en rotation
    // (RandomAudioVoice fait alors un fondu très court au lieu d'une coupure brutale).
    private AudioSource AcquireVoice()
    {
        int count = voices.Length;
        AudioSource busy = null;
        int busyIndex = -1;

        for (int n = 0; n < count; n++)
        {
            int i = (_nextVoice + n) % count;
            AudioSource candidate = voices[i];
            if (candidate == null) continue;

            if (!candidate.isPlaying)
            {
                _nextVoice = (i + 1) % count;
                return candidate;
            }

            if (busy == null)
            {
                busy = candidate;
                busyIndex = i;
            }
        }

        if (busy != null) _nextVoice = (busyIndex + 1) % count;
        return busy;
    }
}
