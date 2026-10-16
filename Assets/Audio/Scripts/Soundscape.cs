using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Génère des sons ponctuels 3D à des positions aléatoires autour d'un Transform de référence
/// (craquements, grincements, débris...). Ne gère ni les ambiances continues, ni les sons de gameplay.
/// Dépend de RandomAudioContainer (sélection, réglages spatiaux) et RandomAudioVoice (lecture, fades).
/// Les clips en Loop sont joués en lecture unique : un Soundscape ne génère que des événements ponctuels.
/// </summary>
public class Soundscape : MonoBehaviour
{
    [System.Serializable]
    public class ContainerEntry
    {
        public string label = "Nouveau container";

        [Min(0f), Tooltip("Poids relatif de ce container. 0 = jamais choisi.")]
        public float weight = 1f;

        public RandomAudioContainer container = new RandomAudioContainer();
    }

    private const float MinAllowedInterval = 0.05f; // garde-fou anti-boucle incontrôlée

    [Header("Activation")]
    [Tooltip("Démarre automatiquement quand le composant est activé.")]
    [SerializeField] private bool startActive = true;

    [Header("Référence")]
    [Tooltip("Centre de génération (le joueur ou sa caméra). Si vide, le Transform de ce GameObject est utilisé.")]
    [SerializeField] private Transform reference;

    [Header("Fréquence (secondes)")]
    [SerializeField, Min(MinAllowedInterval)] private float minInterval = 3f;
    [SerializeField, Min(MinAllowedInterval)] private float maxInterval = 10f;

    [Header("Zone de génération (mètres)")]
    [SerializeField, Min(0f)] private float minRadius = 8f;
    [SerializeField, Min(0f)] private float maxRadius = 30f;

    [Header("Sons")]
    [SerializeField] private List<ContainerEntry> containers = new List<ContainerEntry>();

    [Header("Spatialisation 3D")]
    [SerializeField, Min(0.01f)] private float minDistance = 2f;
    [SerializeField, Min(0.01f)] private float maxDistance = 40f;
    [Tooltip("Rolloff utilisé par les containers SANS courbe de volume personnalisée.")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [Tooltip("Optionnel : AudioMixerGroup de sortie (ex. groupe SFX / Ambiance).")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Limites")]
    [SerializeField, Min(1)] private int maxSimultaneousSounds = 5;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool drawGizmos = true;

    private readonly List<RandomAudioVoice> _voices = new List<RandomAudioVoice>();
    private bool _isRunning;
    private float _timer;
    private bool _warnedMissingReference;
    private Vector3 _lastSpawnPosition;
    private bool _hasLastSpawn;

    public bool IsRunning => _isRunning;

    // ------------------------------------------------------------- contrôle

    /// <summary>Démarre la génération (sans effet si déjà démarrée).</summary>
    public void StartSoundscape()
    {
        if (_isRunning) return;

        _isRunning = true;
        ScheduleNext();
        if (debugLogs) Debug.Log("[Soundscape] Démarré.", this);
    }

    /// <summary>Arrête la planification. Les sons en cours finissent normalement.</summary>
    public void StopSoundscape()
    {
        if (!_isRunning) return;

        _isRunning = false;
        if (debugLogs) Debug.Log("[Soundscape] Arrêté.", this);
    }

    /// <summary>
    /// Éteint les sons en cours avec le fade-out de chaque clip (0 = coupure immédiate).
    /// À combiner avec StopSoundscape() pour un arrêt complet en douceur.
    /// </summary>
    public void FadeOutActiveSounds()
    {
        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i] != null) _voices[i].Stop();
        }
    }

    // ------------------------------------------------------------- Unity

    private void OnEnable()
    {
        if (startActive) StartSoundscape();
    }

    private void OnDisable()
    {
        _isRunning = false;
    }

    private void OnValidate()
    {
        maxInterval = Mathf.Max(minInterval, maxInterval);
        maxRadius = Mathf.Max(minRadius, maxRadius);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        if (containers == null) return;
        for (int i = 0; i < containers.Count; i++)
        {
            containers[i]?.container?.Validate();
        }
    }

    private void Update()
    {
        CleanupFinishedVoices(); // peu coûteux : au plus maxSimultaneousSounds éléments

        if (!_isRunning) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        SpawnSound();
        ScheduleNext(); // toujours replanifié, même si le son a été ignoré
    }

    // ------------------------------------------------------------- planification

    private void ScheduleNext()
    {
        float lo = Mathf.Max(MinAllowedInterval, Mathf.Min(minInterval, maxInterval));
        float hi = Mathf.Max(lo, Mathf.Max(minInterval, maxInterval));
        _timer = Random.Range(lo, hi);
    }

    private void SpawnSound()
    {
        // Limite vérifiée AVANT le tirage : un événement ignoré ne consomme pas l'anti-répétition.
        if (_voices.Count >= maxSimultaneousSounds)
        {
            if (debugLogs) Debug.Log("[Soundscape] Limite de sons simultanés atteinte, événement ignoré.", this);
            return;
        }

        RandomAudioContainer container = SelectContainer();
        if (container == null)
        {
            if (debugLogs) Debug.Log("[Soundscape] Aucun container avec un clip valide, événement ignoré.", this);
            return;
        }

        // forceOneShot : le Loop des clips est ignoré (sinon un son en boucle occuperait un slot à jamais).
        if (!container.TryPick(out AudioPick pick, true)) return;

        Vector3 position = RandomPositionAround(GetCenter());
        PlayAt(position, pick, container);

        _lastSpawnPosition = position;
        _hasLastSpawn = true;

        if (debugLogs)
        {
            Debug.Log($"[Soundscape] '{pick.Clip.name}' à {position} (vol {pick.Volume:F2}, pitch {pick.Pitch:F2}, " +
                      $"offset {pick.StartOffset:F2}s, fade {pick.FadeIn:F2}/{pick.FadeOut:F2}s) " +
                      $"- {_voices.Count}/{maxSimultaneousSounds} sons actifs.", this);
        }
    }

    // ------------------------------------------------------------- sélection

    private RandomAudioContainer SelectContainer()
    {
        if (containers == null) return null;

        float total = 0f;
        for (int i = 0; i < containers.Count; i++)
        {
            if (IsUsable(containers[i])) total += containers[i].weight;
        }
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        RandomAudioContainer lastUsable = null;

        for (int i = 0; i < containers.Count; i++)
        {
            if (!IsUsable(containers[i])) continue;

            lastUsable = containers[i].container;
            roll -= containers[i].weight;
            if (roll < 0f) return lastUsable;
        }

        return lastUsable; // filet de sécurité (imprécisions flottantes)
    }

    private static bool IsUsable(ContainerEntry entry)
    {
        return entry != null && entry.weight > 0f && entry.container != null && entry.container.HasClips;
    }

    // ------------------------------------------------------------- position

    private Vector3 GetCenter()
    {
        if (reference != null) return reference.position;

        if (!_warnedMissingReference)
        {
            _warnedMissingReference = true;
            Debug.LogWarning("[Soundscape] Aucun Transform de référence assigné : " +
                             "utilisation de la position de ce GameObject. Assigne le joueur dans l'Inspector.", this);
        }
        return transform.position;
    }

    /// <summary>
    /// Point uniformément réparti dans le VOLUME de la coquille sphérique [minRadius, maxRadius].
    /// Le rayon est tiré via la racine cubique pour compenser la croissance du volume en r³.
    /// </summary>
    private Vector3 RandomPositionAround(Vector3 center)
    {
        float rMin = Mathf.Max(0f, Mathf.Min(minRadius, maxRadius));
        float rMax = Mathf.Max(rMin, Mathf.Max(minRadius, maxRadius));

        float r3 = Random.Range(rMin * rMin * rMin, rMax * rMax * rMax);
        float radius = Mathf.Pow(r3, 1f / 3f);

        return center + Random.onUnitSphere * radius;
    }

    // ------------------------------------------------------------- lecture

    private void PlayAt(Vector3 position, AudioPick pick, RandomAudioContainer container)
    {
        // GameObject non parenté : il reste en place même si le joueur (ou ce Soundscape) bouge.
        GameObject go = new GameObject("Soundscape Voice");
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = outputGroup;

        source.spatialBlend = 1f; // 100 % 3D
        source.minDistance = Mathf.Max(0.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.dopplerLevel = 0f; // source immobile : pas d'effet Doppler parasite

        // Distances définies AVANT : rolloff (preset ou courbe perso), spread, bypass reverb zones.
        container.ConfigureSource(source, rolloffMode);

        // La voix gère fades / offset et détruit elle-même le GameObject à la fin du son.
        RandomAudioVoice voice = go.AddComponent<RandomAudioVoice>();
        voice.DestroyGameObjectWhenFinished = true;
        voice.Begin(pick);

        _voices.Add(voice);
    }

    private void CleanupFinishedVoices()
    {
        for (int i = _voices.Count - 1; i >= 0; i--)
        {
            RandomAudioVoice voice = _voices[i];
            if (voice == null || voice.IsFinished) // le GameObject est détruit par la voix elle-même
                _voices.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------- gizmos

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 center = reference != null ? reference.position : transform.position;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f); // rayon minimal
        Gizmos.DrawWireSphere(center, minRadius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f); // rayon maximal
        Gizmos.DrawWireSphere(center, maxRadius);

        if (_hasLastSpawn && Application.isPlaying)
        {
            Gizmos.color = Color.red; // dernier son généré
            Gizmos.DrawSphere(_lastSpawnPosition, 0.3f);
        }
    }
}
