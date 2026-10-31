using System.Collections.Generic;
using UnityEngine;

// Wspólna część emiterów efektów chromy: tworzy cząstki wokół postaci, porusza je i wygasza.
// Rysowaniem cząstek zajmują się klasy pochodne: WorldChromaFx (sprite'y w świecie gry) i UiChromaFx (obrazki w interfejsie).
public abstract class ChromaFxEmitter : MonoBehaviour
{
    protected class Particle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public float life;
        public float size;
        public float rotation;
        public float spin;
        public float phase;
        public Color colorA;
        public Color colorB;
        public GameObject view;
    }

    const int MaxParticles = 40;

    protected readonly List<Particle> particles = new List<Particle>();

    ChromaFxLook look;
    float spawnAccumulator;

    protected virtual float Delta => Time.deltaTime;
    protected virtual float RateScale => 1f;

    // Gdzie jest postać: punkt pod stopami, wysokość i szerokość. false = postaci teraz nie widać (martwa, ukryta).
    protected abstract bool GetBody(out Vector2 feet, out float height, out float width);
    protected abstract void CreateView(Particle particle, int spriteKind);
    protected abstract void ApplyView(Particle particle, float alpha, float scale, Color color);
    protected abstract void DestroyView(Particle particle);

    protected void SetLook(ChromaFxLook newLook)
    {
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
            spawnAccumulator += style.rate * look.strength * RateScale * dt;

            while (spawnAccumulator >= 1f && particles.Count < MaxParticles)
            {
                spawnAccumulator -= 1f;
                Spawn(style, feet, height);
            }

            spawnAccumulator = Mathf.Min(spawnAccumulator, 2f);
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle particle = particles[i];
            particle.age += dt;

            if (particle.age >= particle.life)
            {
                DestroyView(particle);
                particles.RemoveAt(i);
                continue;
            }

            particle.position += particle.velocity * dt;
            particle.position.x += Mathf.Sin(particle.age * 3f + particle.phase) * style.sway * height * dt;
            particle.rotation += particle.spin * dt;

            float t = particle.age / particle.life;
            float alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - t) / 0.4f);
            float scale = style.shrink ? 1f - 0.6f * t : 1f;

            if (style.twinkle)
            {
                scale *= 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(particle.age * 9f + particle.phase));
            }

            Color color = style.colorOverLife ? Color.Lerp(particle.colorA, particle.colorB, t) : particle.colorA;
            ApplyView(particle, alpha, scale, color);
        }
    }

    void Spawn(FxStyle style, Vector2 feet, float height)
    {
        Particle particle = new Particle
        {
            position = feet + new Vector2(Random.Range(-style.spawnX, style.spawnX), Random.Range(style.spawnYMin, style.spawnYMax)) * height,
            velocity = new Vector2(Random.Range(style.velXMin, style.velXMax), Random.Range(style.velYMin, style.velYMax)) * height,
            life = style.life * Random.Range(0.7f, 1.2f),
            size = Random.Range(style.sizeMin, style.sizeMax) * height,
            rotation = style.spin > 0f ? Random.Range(0f, 360f) : 0f,
            spin = style.spin > 0f ? Random.Range(-style.spin, style.spin) : 0f,
            phase = Random.Range(0f, 6.28f)
        };

        particle.colorA = ChromaFx.PickColor(look);
        particle.colorB = style.colorOverLife ? look.colorB : particle.colorA;

        if (style.colorOverLife)
        {
            particle.colorA = look.colorA;
        }

        CreateView(particle, style.sprite);
        particles.Add(particle);
    }
}
