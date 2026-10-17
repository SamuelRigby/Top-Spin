using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Résultat d'un tirage : le clip et ses variations proviennent du MÊME déclenchement.
/// Struct en lecture seule : aucune allocation sur le heap.
/// </summary>
public readonly struct AudioPick
{
    public readonly AudioClip Clip;
    public readonly float Volume;
    public readonly float Pitch;

    public AudioPick(AudioClip clip, float volume, float pitch)
    {
        Clip = clip;
        Volume = volume;
        Pitch = pitch;
    }

    public bool IsValid => Clip != null;
}

/// <summary>
/// Container de sons aléatoires : choisit un clip (pondération + anti-répétition)
/// et tire un volume / pitch. Ne joue rien lui-même et ne dépend d'aucun AudioSource.
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
    }

    public const float MinPitchLimit = 0.1f;
    public const float MaxPitchLimit = 3f;

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    [Tooltip("Évite de rejouer le même clip deux fois de suite (si un autre clip est disponible).")]
    [SerializeField] private bool avoidRepeat = true;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;

    [Header("Pitch")]
    [SerializeField, Range(MinPitchLimit, MaxPitchLimit)] private float minPitch = 0.95f;
    [SerializeField, Range(MinPitchLimit, MaxPitchLimit)] private float maxPitch = 1.05f;

    // État runtime propre à chaque instance, jamais sérialisé.
    [NonSerialized] private int _lastIndex = -1;

    /// <summary>True si au moins un clip est réellement sélectionnable.</summary>
    public bool HasClips => SumWeights(-1) > 0f;

    /// <summary>
    /// Tire un clip + volume + pitch. Retourne false (et un pick vide) si rien n'est jouable.
    /// </summary>
    public bool TryPick(out AudioPick pick)
    {
        int index = SelectIndex();
        if (index < 0)
        {
            pick = default;
            return false;
        }

        _lastIndex = index;
        pick = new AudioPick(
            entries[index].clip,
            Roll(minVolume, maxVolume, 0f, 1f),
            Roll(minPitch, maxPitch, MinPitchLimit, MaxPitchLimit));
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
    /// Corrige les valeurs invalides. Les classes [Serializable] n'ont pas d'OnValidate :
    /// appelle cette méthode depuis l'OnValidate du MonoBehaviour qui contient le container.
    /// </summary>
    public void Validate()
    {
        minVolume = Mathf.Clamp01(minVolume);
        maxVolume = Mathf.Clamp(maxVolume, minVolume, 1f);

        minPitch = Mathf.Clamp(minPitch, MinPitchLimit, MaxPitchLimit);
        maxPitch = Mathf.Clamp(maxPitch, minPitch, MaxPitchLimit);

        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].weight < 0f)
                entries[i].weight = 0f;
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
}
