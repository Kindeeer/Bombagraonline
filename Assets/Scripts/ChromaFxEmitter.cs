using System.Collections.Generic;
using UnityEngine;

// Wspólna część emiterów efektów chromy: tworzy cząstki wokół postaci, porusza je i wygasza.
// Rysowaniem cząstek zajmują się klasy pochodne: WorldChromaFx (sprite'y w świecie gry) i UiChromaFx (obrazki w interfejsie).
public abstract class ChromaFxEmitter : MonoBehaviour
{
    protected class Particle
    {
        public FxStyle style;
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public float life;
        public float size;
        public float rotation;
        public float spin;
        public float phase;
        public Color tint;
        // true = rysowana przed postacią, false = za nią
        public bool front;
        // miejsce w kolejności rysowania świata gry (ustawia WorldChromaFx)
        public float sort;
        public GameObject view;
    }

    const int MaxParticles = 40;

    // pod koniec życia płomień ciemnieje jak stygnący żar
    static readonly Color EmberTint = new Color(0.8f, 0.5f, 0.45f, 1f);

    protected readonly List<Particle> particles = new List<Particle>();

    ChromaFxLook look;
    float spawnAccumulator;
    float extraAccumulator;

    protected ChromaFxLook Look => look;

    protected virtual float Delta => Time.deltaTime;
    protected virtual float RateScale => 1f;

    // Gdzie jest postać: punkt pod stopami, wysokość i szerokość. false = postaci teraz nie widać (martwa, ukryta).
    protected abstract bool GetBody(out Vector2 feet, out float height, out float width);
    protected abstract void CreateView(Particle particle, Sprite sprite);
    protected abstract void ApplyView(Particle particle, float alpha, float scale, Color color);
    protected abstract void DestroyView(Particle particle);

    protected void SetLook(ChromaFxLook newLook)
    {
        // inne kolory albo inny efekt: stare cząstki znikają, żeby nie mieszać dwóch wyglądów
        if (newLook.effect != look.effect || newLook.colorA != look.colorA || newLook.colorB != look.colorB || newLook.rainbow != look.rainbow)
        {
            Clear();
        }

        look = newLook;
        enabled = newLook.HasParticles;

        if (!newLook.HasParticles)
        {
            Clear();
        }
    }

    protected void Clear()
    {
        foreach (Particle particle in particles)
        {
            DestroyView(particle);
        }

        particles.Clear();
        spawnAccumulator = 0f;
        extraAccumulator = 0f;
    }

    void OnDisable()
    {
        Clear();
    }

    void OnDestroy()
    {
        Clear();
    }

    void Update()
    {
        float dt = Delta;

        if (dt <= 0f || !look.HasParticles)
        {
            return;
        }

        bool visible = GetBody(out Vector2 feet, out float height, out float width);
        FxStyle style = ChromaFx.StyleOf(look.effect);

        if (visible)
        {
            Emit(style, ref spawnAccumulator, dt, feet, height);

            if (style.extra != null)
            {
                Emit(style.extra, ref extraAccumulator, dt, feet, height);
            }
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle particle = particles[i];
            FxStyle own = particle.style;
            particle.age += dt;

            if (particle.age >= particle.life)
            {
                DestroyView(particle);
                particles.RemoveAt(i);
                continue;
            }

            particle.position += particle.velocity * dt;
            particle.position.x += Mathf.Sin(particle.age * 3f + particle.phase) * own.sway * height * dt;

            // obracanie (śnieg, liście) albo kołysanie na boki (płomienie, serduszka, gwiazdki)
            if (own.wobble > 0f)
            {
                particle.rotation = Mathf.Sin(particle.age * 5f + particle.phase) * own.wobble;
            }
            else
            {
                particle.rotation += particle.spin * dt;
            }

            float t = particle.age / particle.life;

            // cząstka wyskakuje z lekkim przeskokiem (jak wyciśnięta), a pod koniec blednie
            float pop = t < 0.18f ? PopIn(t / 0.18f) : 1f;
            float alpha = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.35f);
            float scale = pop * (own.shrink ? 1f - 0.55f * t : 1f);

            if (own.twinkle)
            {
                scale *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(particle.age * 7f + particle.phase));
            }

            Color color = own.colorOverLife ? Color.Lerp(particle.tint, particle.tint * EmberTint, t) : particle.tint;
            ApplyView(particle, alpha, scale, color);
        }
    }

    // Od 0 do 1 z przeskokiem ponad 1 po drodze.
    static float PopIn(float k)
    {
        const float overshoot = 1.9f;
        k -= 1f;
        return 1f + k * k * ((overshoot + 1f) * k + overshoot);
    }

    void Emit(FxStyle style, ref float accumulator, float dt, Vector2 feet, float height)
    {
        accumulator += style.rate * look.strength * RateScale * dt;

        while (accumulator >= 1f && particles.Count < MaxParticles)
        {
            accumulator -= 1f;
            Spawn(style, feet, height);
        }

        accumulator = Mathf.Min(accumulator, 2f);
    }

    void Spawn(FxStyle style, Vector2 feet, float height)
    {
        Particle particle = new Particle
        {
            style = style,
            position = feet + new Vector2(Random.Range(-style.spawnX, style.spawnX), Random.Range(style.spawnYMin, style.spawnYMax)) * height,
            velocity = new Vector2(Random.Range(style.velXMin, style.velXMax), Random.Range(style.velYMin, style.velYMax)) * height,
            life = style.life * Random.Range(0.75f, 1.2f),
            size = Random.Range(style.sizeMin, style.sizeMax) * height,
            rotation = style.spin > 0f ? Random.Range(0f, 360f) : 0f,
            spin = style.spin > 0f ? Random.Range(-style.spin, style.spin) : 0f,
            phase = Random.Range(0f, 6.28f),
            tint = ChromaFx.PickTint(look),
            front = Random.value >= style.behind
        };

        CreateView(particle, ChromaFx.SpriteOf(style.sprite, look));
        particles.Add(particle);
    }
}
