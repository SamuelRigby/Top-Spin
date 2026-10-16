using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Résultat d'un tirage : le clip et ses variations proviennent du MÊME déclenchement.
/// Struct en lecture seule : aucune allocation sur le heap.
/// Volume = volume du clip x tirage min/max du container (déjà combinés, borné 0..1).
/// </summary>
public readonly struct AudioPick
{
    public readonly AudioClip Clip;
    public readonly float Volume;
    public readonly float Pitch;

    /// <summary>Durée du fade-in en secondes (0 = démarrage immédiat).</summary>
    public readonly float FadeIn;
    /// <summary>Durée du fade-out en secondes (0 = fin immédiate).</summary>
    public readonly float FadeOut;
    /// <summary>Position de départ dans le clip, en secondes (déjà tirée si Random Start Offset).</summary>
    public readonly float StartOffset;
    public readonly bool Loop;

    /// <summary>Constructeur historique (sans fades, sans offset, sans loop).</summary>
    public AudioPick(AudioClip clip, float volume, float pitch)
        : this(clip, volume, pitch, 0f, 0f, 0f, false)
    {
    }

    public AudioPick(AudioClip clip, float volume, float pitch,
                     float fadeIn, float fadeOut, float startOffset, bool loop)
    {
        Clip = clip;
        Volume = volume;
        Pitch = pitch;
        FadeIn = fadeIn;
        FadeOut = fadeOut;
        StartOffset = startOffset;
        Loop = loop;
    }

    public bool IsValid => Clip != null;
}

/// <summary>
/// Container de sons aléatoires : choisit un clip (pondération + anti-répétition)
/// et tire un volume / pitch. Ne joue rien lui-même et ne possède aucun AudioSource.
/// Les réglages spatiaux (courbes, reverb) sont appliqués sur un AudioSource fourni
/// par l'appelant via ConfigureSource(). La lecture (fades, offset, stop) est assurée
/// par RandomAudioVoice.
/// À utiliser comme champ [SerializeField] dans n'importe quel MonoBehaviour.
/// </summary>
[Serializable]
public class RandomAudioContainer
{
    [Serializable]
    public class Entry
    {
        public AudioClip clip;

        [Min(0f), Tooltip("Poids relatif. 1 partout = tirage uniforme. 0 = jamais choisi.")]
        public float weight = 1f;

        [Range(0f, 1f), Tooltip("Volume propre à ce clip (mixage asset par asset). " +
                                "Volume final = ce volume x tirage Min/Max Volume du container.")]
        public float volume = 1f;

        [Min(0f), Tooltip("Durée du fade-in en secondes. 0 = démarrage immédiat.")]
        public float fadeIn = 0f;

        [Min(0f), Tooltip("Durée du fade-out en secondes. 0 = fin immédiate. " +
                          "Appliqué à la fin naturelle ET lors d'un Stop() (y compris en loop).")]
        public float fadeOut = 0f;

        [Tooltip("Démarre à une position aléatoire dans le fichier. " +
                 "Sans Loop : garde toujours de quoi jouer les fades (min. 0,1 s). " +
                 "Avec Loop : n'importe où (idéal pour pads / textures).")]
        public bool randomStartOffset = false;

        [Tooltip("Joue en boucle jusqu'à un Stop() (fade-out appliqué). " +
                 "Ignoré par Soundscape, qui ne joue que des événements ponctuels.")]
        public bool loop = false;
    }

    public const float MinPitchLimit = 0.1f;
    public const float MaxPitchLimit = 3f;

    private const float MinTailSeconds = 0.1f; // lecture minimale restante après un offset aléatoire

    // Courbe constante utilisée pour remettre le Spread à 0° quand le container n'a pas de courbe perso.
    private static readonly AnimationCurve ZeroSpreadCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    [Tooltip("Évite de rejouer le même clip deux fois de suite (si un autre clip est disponible).")]
    [SerializeField] private bool avoidRepeat = true;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;

    [Header("Pitch")]
    [SerializeField, Range(MinPitchLimit, MaxPitchLimit)] private float minPitch = 0.95f;
    [SerializeField, Range(MinPitchLimit, MaxPitchLimit)] private float maxPitch = 1.05f;

    [Header("Spatialisation (appliquée par ConfigureSource)")]
    [Tooltip("Remplace le rolloff par défaut par la courbe ci-dessous.")]
    [SerializeField] private bool useCustomRolloff = false;

    [Tooltip("Volume (Y, 0..1) selon la distance à l'Audio Listener (X, 0..1 = 0..Max Distance de la source).")]
    [SerializeField] private AnimationCurve rolloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Active la courbe de Spread ci-dessous (sinon Spread = 0°).")]
    [SerializeField] private bool useCustomSpread = false;

    [Tooltip("Spread (Y, 0..1 = 0..360°) selon la distance à l'Audio Listener (X, 0..1 = 0..Max Distance).")]
    [SerializeField] private AnimationCurve spreadCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [Tooltip("Ignore les Audio Reverb Zones pour les sons de ce container.")]
    [SerializeField] private bool bypassReverbZones = false;

    // État runtime propre à chaque instance, jamais sérialisé.
    [NonSerialized] private int _lastIndex = -1;

    /// <summary>True si au moins un clip est réellement sélectionnable.</summary>
    public bool HasClips => SumWeights(-1) > 0f;

    /// <summary>
    /// Tire un clip + volume + pitch (+ fades, offset, loop). Retourne false (et un pick vide)
    /// si rien n'est jouable.
    /// </summary>
    public bool TryPick(out AudioPick pick) => TryPick(out pick, false);

    /// <summary>
    /// Idem, avec forceOneShot = true pour ignorer le Loop du clip (le tirage de l'offset
    /// tient alors compte d'une lecture unique).
    /// </summary>
    public bool TryPick(out AudioPick pick, bool forceOneShot)
    {
        int index = SelectIndex();
        if (index < 0)
        {
            pick = default;
            return false;
        }

        _lastIndex = index;
        Entry entry = entries[index];

        bool loop = entry.loop && !forceOneShot;
        float fadeIn = Mathf.Max(0f, entry.fadeIn);
        float fadeOut = Mathf.Max(0f, entry.fadeOut);
        float offset = entry.randomStartOffset
            ? RollStartOffset(entry.clip.length, loop, fadeIn + fadeOut)
            : 0f;

        // Combinaison : volume du clip x tirage du container, borné à 0..1 (limite d'AudioSource.volume).
        float volume = Mathf.Clamp01(Mathf.Clamp01(entry.volume) * Roll(minVolume, maxVolume, 0f, 1f));

        pick = new AudioPick(
            entry.clip,
            volume,
            Roll(minPitch, maxPitch, MinPitchLimit, MaxPitchLimit),
            fadeIn,
            fadeOut,
            offset,
            loop);
        return true;
    }

    /// <summary>Version pratique : pick vide (Clip == null) si rien n'est jouable.</summary>
    public AudioPick Pick()
    {
        TryPick(out AudioPick pick);
        return pick;
    }

    /// <summary>Clip seul (null si rien n'est jouable). Ne tire pas de volume/pitch.</summary>
    public AudioClip GetRandomClip()
    {
        int index = SelectIndex();
        if (index < 0) return null;

        _lastIndex = index;
        return entries[index].clip;
    }

    /// <summary>Oublie le dernier clip joué (anti-répétition).</summary>
    public void ResetHistory() => _lastIndex = -1;

    /// <summary>
    /// Applique les réglages spatiaux du container sur un AudioSource : rolloff (courbe perso
    /// ou fallbackRolloff), Spread (courbe perso ou 0°) et Bypass Reverb Zones.
    /// À appeler à CHAQUE lecture : les trois réglages sont toujours réécrits, donc une source
    /// réutilisée par plusieurs containers ne garde jamais l'état du précédent.
    /// Ne touche ni à Spatial Blend, ni aux distances Min/Max (à régler sur la source).
    /// </summary>
    public void ConfigureSource(AudioSource source, AudioRolloffMode fallbackRolloff = AudioRolloffMode.Logarithmic)
    {
        if (source == null) return;

        source.bypassReverbZones = bypassReverbZones;

        if (useCustomRolloff && HasKeys(rolloffCurve))
        {
            // La courbe est étalée par Unity sur [0, maxDistance] : définir Min/Max Distance AVANT.
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloffCurve);
            source.rolloffMode = AudioRolloffMode.Custom;
        }
        else
        {
            source.rolloffMode = fallbackRolloff;
        }

        source.SetCustomCurve(AudioSourceCurveType.Spread,
            useCustomSpread && HasKeys(spreadCurve) ? spreadCurve : ZeroSpreadCurve);
    }

    /// <summary>
    /// Corrige les valeurs invalides. Les classes [Serializable] n'ont pas d'OnValidate :
    /// appelle cette méthode depuis l'OnValidate du MonoBehaviour qui contient le container.
    /// </summary>
    public void Validate()
    {
        minVolume = Mathf.Clamp01(minVolume);
        maxVolume = Mathf.Clamp(maxVolume, minVolume, 1f);

        minPitch = Mathf.Clamp(minPitch, MinPitchLimit, MaxPitchLimit);
        maxPitch = Mathf.Clamp(maxPitch, minPitch, MaxPitchLimit);

        if (rolloffCurve == null) rolloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        if (spreadCurve == null) spreadCurve = AnimationCurve.Constant(0f, 1f, 0f);
        ClampCurveValues(rolloffCurve);
        ClampCurveValues(spreadCurve);

        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e == null) continue;

            e.weight = Mathf.Max(0f, e.weight);
            e.volume = Mathf.Clamp01(e.volume);
            e.fadeIn = Mathf.Max(0f, e.fadeIn);
            e.fadeOut = Mathf.Max(0f, e.fadeOut);
        }
    }

    // ---------------------------------------------------------------- interne

    private int SelectIndex()
    {
        if (entries == null || entries.Length == 0) return -1;

        int excluded = avoidRepeat ? _lastIndex : -1;
        float total = SumWeights(excluded);

        // Aucun autre clip disponible : on autorise la répétition plutôt que de rester muet.
        if (total <= 0f && excluded >= 0)
        {
            excluded = -1;
            total = SumWeights(-1);
        }

        if (total <= 0f) return -1;

        float roll = Random.Range(0f, total);
        int lastSelectable = -1;

        for (int i = 0; i < entries.Length; i++)
        {
            if (!IsSelectable(i, excluded)) continue;

            lastSelectable = i;
            roll -= entries[i].weight;
            if (roll < 0f) return i;
        }

        // Filet de sécurité contre les imprécisions flottantes.
        return lastSelectable;
    }

    private float SumWeights(int excluded)
    {
        if (entries == null) return 0f;

        float total = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            if (IsSelectable(i, excluded))
                total += entries[i].weight;
        }
        return total;
    }

    private bool IsSelectable(int index, int excluded)
    {
        if (index == excluded) return false;
        Entry e = entries[index];
        return e != null && e.clip != null && e.weight > 0f;
    }

    // Tolérant même si min > max ou si les valeurs ont été modifiées au runtime.
    private static float Roll(float a, float b, float lowLimit, float highLimit)
    {
        float lo = Mathf.Clamp(Mathf.Min(a, b), lowLimit, highLimit);
        float hi = Mathf.Clamp(Mathf.Max(a, b), lowLimit, highLimit);
        return Mathf.Approximately(lo, hi) ? lo : Random.Range(lo, hi);
    }

    // Loop : n'importe où dans le fichier. Sans loop : on garde au moins max(0,1 s, fades) de lecture.
    // Clip trop court pour cela : offset 0 (aucune erreur possible).
    private static float RollStartOffset(float clipLength, bool loop, float fadeTotal)
    {
        float maxOffset = loop ? clipLength : clipLength - Mathf.Max(MinTailSeconds, fadeTotal);
        return maxOffset > 0f ? Random.Range(0f, maxOffset) : 0f;
    }

    private static bool HasKeys(AnimationCurve curve) => curve != null && curve.length > 0;

    // Les valeurs Y doivent rester dans 0..1 (volume / spread normalisé). L'axe X est laissé libre :
    // Unity étale toute la plage de la courbe sur Max Distance.
    private static void ClampCurveValues(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            Keyframe key = curve[i];
            float clamped = Mathf.Clamp01(key.value);
            if (Mathf.Approximately(clamped, key.value)) continue;

            key.value = clamped;
            curve.MoveKey(i, key);
        }
    }
}
