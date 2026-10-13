namespace CursorRotator.Core.Trails;

/// <summary>Catálogo de la galería: cada preset en exactamente una categoría.</summary>
public static class TrailCatalog
{
    public record Entry(TrailPreset Preset, string Tab, string Label, string Tip);

    public static readonly Entry[] All =
    [
        // Geométricos
        new(TrailPreset.MatrixGrid, "Geométricos", "🟩 Matriz cuadriculada", "Columnas en cuadrícula con rastro degradado"),
        new(TrailPreset.Orbital, "Geométricos", "🪐 Órbita gravitacional", "Electrones orbitando el cursor"),
        new(TrailPreset.Dna, "Geométricos", "🧬 ADN / doble hélice", "Dos hebras en fase opuesta + travesaños"),
        new(TrailPreset.Web, "Geométricos", "🕸️ Telaraña dinámica", "Nodos unidos a sus 4 vecinos"),
        new(TrailPreset.Moebius, "Geométricos", "♾️ Cinta de Moebius", "Cinta que se retuerce con sombreado 3D"),
        // Físicos / Fluidos
        new(TrailPreset.Burbujas, "Físicos", "🫧 Burbujas", "Aros que crecen y flotan"),
        new(TrailPreset.Fuego, "Físicos", "🔥 Fuego", "Llamas que suben"),
        new(TrailPreset.Cometa, "Físicos", "☄️ Cometa", "Núcleo con halo y cola de velocidad"),
        new(TrailPreset.Metaballs, "Físicos", "💧 Metaballs (goo)", "Gotas que se fusionan al superponerse"),
        new(TrailPreset.Swarm, "Físicos", "🐝 Enjambre (boids)", "Cohesión, separación y alineación"),
        new(TrailPreset.Ripples, "Físicos", "🌊 Ondas de choque", "Anillos al frenar o girar brusco"),
        new(TrailPreset.DoublePendulum, "Físicos", "🌀 Péndulo doble", "Caos determinista atado al cursor"),
        new(TrailPreset.Wisp, "Físicos", "👻 Fuego fatuo", "Luces con resorte + humo"),
        new(TrailPreset.Ferrofluid, "Físicos", "🧲 Ferrofluido", "Picos según la velocidad"),
        // Abstractos / Sci-Fi
        new(TrailPreset.Tesla, "Sci-Fi", "⚡ Tesla / relámpago", "Arcos fractales con ramificaciones"),
        new(TrailPreset.LineaNeon, "Sci-Fi", "💠 Línea de neón", "Spline luminosa continua"),
        new(TrailPreset.LightRibbon, "Sci-Fi", "🔦 Sable de luz", "Cinta ahusada de cabeza ancha"),
        new(TrailPreset.PixelGlitch, "Sci-Fi", "👾 Pixel / Glitch", "Cuadrados con jitter"),
        new(TrailPreset.HackerRain, "Sci-Fi", "💻 Matriz de datos", "Glifos cayendo"),
        new(TrailPreset.Singularity, "Sci-Fi", "🕳️ Agujero negro", "Absorción con aceleración exponencial"),
        new(TrailPreset.Lorenz, "Sci-Fi", "🦋 Lorenz (mariposa)", "Atractor caótico en 2D"),
        // Partículas sueltas
        new(TrailPreset.PolvoEstrellas, "Partículas", "🌟 Polvo de estrellas", "Puntos con gravedad suave"),
        new(TrailPreset.Destellos, "Partículas", "✨ Destellos", "Estrellas titilantes"),
        new(TrailPreset.Confetti, "Partículas", "🎊 Confeti dinámico", "Polígonos con tumble 3D y gravedad"),
        new(TrailPreset.Degradado, "Partículas", "🌈 Degradado", "Puntos arcoíris"),
    ];

    public static readonly string[] Tabs = ["Geométricos", "Físicos", "Sci-Fi", "Partículas"];

    public static string LabelOf(TrailPreset preset)
    {
        foreach (var e in All)
            if (e.Preset == preset) return e.Label;
        return preset.ToString();
    }
}
