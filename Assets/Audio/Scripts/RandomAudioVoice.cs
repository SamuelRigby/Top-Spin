using UnityEngine;

/// <summary>
/// Joue UN AudioPick sur l'AudioSource du même GameObject, avec fade-in, fade-out (fin naturelle
/// ou Stop), offset de départ et loop. Chaque instance gère ses propres fades : plusieurs sons
/// simultanés (sur des AudioSources différentes) restent totalement indépendants.
///
/// Le composant se désactive tout seul quand il n'a rien à faire (aucun Update inutile).
/// Il prend le contrôle de source.volume / pitch / clip / loop pendant la lecture.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class RandomAudioVoice : MonoBehaviour
{
    /// <summary>Fondu très court quand un nouveau son remplace un son en cours sur la même source.</summary>
    public const float ReplaceFadeSeconds = 0.05f;

    private const float MinRemainingSeconds = 0.01f;

    private AudioSource _source;

    private bool _active;
    private bool _stopping;
    private bool _hasPending;
    private AudioPick _pending;

    private float _baseVolume = 1f;
    private float _pitch = 1f;
    private float _clipLength;
    private bool _loop;

    private float _fadeIn;          // fade-in effectif (réduit si le clip est trop court)
    private float _fadeOut;         // fade-out de fin naturelle effectif (idem)
    private float _stopFadeDefault; // fade-out configuré, utilisé par Stop()

    private float _fadeInElapsed;
    private float _lastGain;

    private float _stopDuration;
    private float _stopElapsed;
    private float _stopStartGain;

    /// <summary>Détruit le GameObject à la fin du son (pour les sources temporaires).</summary>
    public bool DestroyGameObjectWhenFinished { get; set; }

    /// <summary>True si aucun son n'est en cours (ni en attente de remplacement).</summary>
    public bool IsFinished => !_active && !_hasPending;

    // ------------------------------------------------------------------ API

    /// <summary>
    /// Raccourci : récupère (ou ajoute) le composant sur l'AudioSource et lance le tirage.
    /// Retourne le handle à conserver pour appeler Stop() plus tard (utile pour les loops).
    /// </summary>
    public static RandomAudioVoice Play(AudioSource source, in AudioPick pick)
    {
        if (source == null) return null;

        RandomAudioVoice voice = source.GetComponent<RandomAudioVoice>();
        if (voice == null) voice = source.gameObject.AddComponent<RandomAudioVoice>();

        voice.Begin(pick);
        return voice;
    }

    /// <summary>
    /// Lance le son. Si un son joue déjà sur cette source, il est éteint par un fondu très court
    /// (ReplaceFadeSeconds) avant que le nouveau démarre : pas de coupure brutale.
    /// </summary>
    public void Begin(in AudioPick pick)
    {
        if (!pick.IsValid || !EnsureSource()) return;

        if (_active && _source.isPlaying)
        {
            _pending = pick; // le dernier remplacement demandé gagne
            _hasPending = true;
            BeginStopFade(ReplaceFadeSeconds);
            return;
        }

        StartNow(pick);
    }

    /// <summary>Arrête avec le fade-out du clip (0 = immédiat). Fonctionne aussi en loop.</summary>
    public void Stop() => Stop(_stopFadeDefault);

    /// <summary>Arrête avec un fade-out personnalisé (0 = immédiat).</summary>
    public void Stop(float fadeDuration)
    {
        _hasPending = false; // un Stop explicite annule un remplacement en attente
        if (!_active || !EnsureSource()) return;

        if (!_source.isPlaying)
        {
            EndPlayback();
            return;
        }

        BeginStopFade(Mathf.Max(0f, fadeDuration));
    }

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        EnsureSource();
        enabled = false; // réveillé uniquement quand un son démarre
    }

    private void Update()
    {
        if (!_active)
        {
            enabled = false;
            return;
        }

        if (!_source.isPlaying) // fin naturelle (ou source stoppée de l'extérieur)
        {
            EndPlayback();
            return;
        }

        float dt = Time.unscaledDeltaTime; // l'audio continue de jouer même si timeScale = 0
        float gain = 1f;

        if (_fadeInElapsed < _fadeIn)
        {
            _fadeInElapsed += dt;
            gain = Mathf.Clamp01(_fadeInElapsed / _fadeIn);
        }

        // Fade-out de fin naturelle : basé sur la position de lecture réelle, donc correct
        // avec un offset de départ, un pitch différent de 1 ou une pause de l'audio.
        if (!_loop && _fadeOut > 0f)
        {
            float remaining = (_clipLength - _source.time) / _pitch;
            if (remaining < _fadeOut)
                gain = Mathf.Min(gain, Mathf.Clamp01(remaining / _fadeOut));
        }

        // Fade-out sur Stop() / remplacement : part du gain courant, sans à-coup.
        if (_stopping)
        {
            _stopElapsed += dt;
            float k = 1f - _stopElapsed / _stopDuration;
            if (k <= 0f)
            {
                EndPlayback();
                return;
            }
            gain = Mathf.Min(gain, _stopStartGain * k);
        }

        _lastGain = gain;
        _source.volume = _baseVolume * gain;
    }

    // ------------------------------------------------------------------ interne

    private bool EnsureSource()
    {
        if (_source == null) _source = GetComponent<AudioSource>();
        return _source != null;
    }

    private void StartNow(in AudioPick pick)
    {
        AudioClip clip = pick.Clip;

        _clipLength = clip.length;
        _pitch = Mathf.Max(0.01f, pick.Pitch);
        _loop = pick.Loop;
        _baseVolume = Mathf.Clamp01(pick.Volume);
        _stopFadeDefault = Mathf.Max(0f, pick.FadeOut);

        // Offset valide : jamais au-delà de la fin du clip, jamais négatif.
        float offset = Mathf.Clamp(pick.StartOffset, 0f, Mathf.Max(0f, _clipLength - MinRemainingSeconds));

        _fadeIn = Mathf.Max(0f, pick.FadeIn);
        _fadeOut = _stopFadeDefault;

        // Clip (ou fin de clip après offset) plus court que les fades : on les réduit proportionnellement.
        // Les loops n'ont pas de fin naturelle : fades inchangés.
        if (!_loop)
        {
            float playable = Mathf.Max(0f, (_clipLength - offset) / _pitch);
            float total = _fadeIn + _fadeOut;
            if (total > playable && total > 0f)
            {
                float scale = playable / total;
                _fadeIn *= scale;
                _fadeOut *= scale;
            }
        }

        _fadeInElapsed = 0f;
        _stopping = false;
        _hasPending = false;
        _lastGain = _fadeIn > 0f ? 0f : 1f; // fade-in 0 = démarrage immédiat à pleine intensité

        _source.Stop();
        _source.clip = clip;
        _source.loop = _loop;
        _source.pitch = _pitch;
        _source.volume = _baseVolume * _lastGain;
        if (offset > 0f) _source.time = offset;
        _source.Play();

        _active = true;
        enabled = true;
    }

    private void BeginStopFade(float duration)
    {
        if (duration <= 0f)
        {
            EndPlayback();
            return;
        }

        // Un fade déjà en cours n'est remplacé que par un fade plus court.
        if (_stopping && duration >= _stopDuration - _stopElapsed) return;

        _stopping = true;
        _stopStartGain = _lastGain;
        _stopDuration = duration;
        _stopElapsed = 0f;
        enabled = true;
    }

    private void EndPlayback()
    {
        _source.Stop();
        _source.volume = _baseVolume; // ne laisse pas la source muette si elle est réutilisée
        _active = false;
        _stopping = false;

        if (_hasPending)
        {
            _hasPending = false;
            AudioPick next = _pending;
            StartNow(next);
            return;
        }

        enabled = false;
        if (DestroyGameObjectWhenFinished) Destroy(gameObject);
    }
}
