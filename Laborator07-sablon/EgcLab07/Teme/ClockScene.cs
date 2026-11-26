// Tema acasa - Varianta A: ceasul
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Ceas analogic desenat în întregime de Shaders/clock.frag, pe un singur dreptunghi.
/// C# calculează doar unghiurile acelor, tema (zi / noapte) și faza lunii.
/// Space = trecere lină între zi și noapte.
/// </summary>
public sealed class ClockScene : Scene
{
    private const string ToggleTheme = "ceas: zi / noapte";

    private const float JumpDuration = 0.2f;   // cât durează "saltul" secundarului, în secunde
    private const float ThemeDuration = 1.5f;  // cât durează trecerea zi <-> noapte
    private const double MoonCycle = 120.0;    // un ciclu al lunii, în secunde de joc

    private Material _face = null!;

    // Tween pe Vector3; folosim doar componenta X: 0 = zi, 1 = noapte.
    private Tween _night = null!;
    private bool _isNight;

    private double _seconds = 10 * 3600 + 10 * 60;   // ora afișată la pornire: 10:10:00
    private double _elapsed;                         // timpul de joc petrecut în scenă

    public override string Name => "tema: ceas";

    public override IEnumerable<string> ActionNames => new[] { ToggleTheme };

    public override string Status => $"{FormatTime()} | {(_isNight ? "noapte" : "zi")}";

    protected override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 0.0f, 5.0f);
        CameraTarget = Vector3.Zero;

        input.Bind(ToggleTheme, Keys.Space);

        Mesh quad = AddMesh(Shapes.Quad(4.0f, 4.0f));

        _face = AddMaterial("ceas", "procedural.vert", "clock.frag")
            .Param("uRimWidth", 0.02f, 0.15f, 0.07f)
            .Param("uHandWidth", 0.5f, 2.0f, 1.0f);
        _face.Transparent = true;   // în afara cadranului alfa = 0

        _face.Uniforms.Set("uDayFace", new Vector3(0.96f, 0.94f, 0.88f));
        _face.Uniforms.Set("uNightFace", new Vector3(0.08f, 0.10f, 0.18f));
        _face.Uniforms.Set("uDayInk", new Vector3(0.10f, 0.11f, 0.14f));
        _face.Uniforms.Set("uNightInk", new Vector3(0.82f, 0.88f, 1.00f));
        _face.Uniforms.Set("uAccent", new Vector3(0.90f, 0.20f, 0.15f));

        _night = new Tween(Vector3.Zero, Vector3.Zero, ThemeDuration, Easing.EaseInOut);

        SendUniforms();   // valorile inițiale

        AddObject("cadran", quad, _face);
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(ToggleTheme))
        {
            _isNight = !_isNight;
            _night.GoTo(_isNight ? Vector3.One : Vector3.Zero, ThemeDuration);
            ConsolePanel.PrintMessage($"tema: {(_isNight ? "noapte" : "zi")}");
        }

        // dt este timpul de joc: pauza și scara timpului funcționează automat.
        _seconds += dt;
        _elapsed += dt;
        _night.Update(dt);

        SendUniforms();
    }

    /// <summary>Calculează unghiurile (radiani, de la ora 12, în sensul acelor) și le trimite shaderului.</summary>
    private void SendUniforms()
    {
        // Secundarul: la începutul fiecărei secunde sare de la poziția veche la cea nouă, cu EaseOut.
        double whole = Math.Floor(_seconds);
        float progress = Math.Min((float)(_seconds - whole) / JumpDuration, 1.0f);
        double shownSecond = whole % 60.0 - 1.0 + Easing.EaseOut(progress);

        _face.Uniforms.Set("uSecondAngle", (float)(shownSecond / 60.0 * Math.Tau));
        _face.Uniforms.Set("uMinuteAngle", (float)(_seconds % 3600.0 / 3600.0 * Math.Tau));
        _face.Uniforms.Set("uHourAngle", (float)(_seconds % 43200.0 / 43200.0 * Math.Tau));

        _face.Uniforms.Set("uNight", _night.Value.X);
        _face.Uniforms.Set("uMoonPhase", MoonPhase());
    }

    /// <summary>Faza lunii în [0, 1): 0 = lună nouă, 0.5 = lună plină.</summary>
    private float MoonPhase() => (float)((0.15 + _elapsed / MoonCycle) % 1.0);

    private string FormatTime()
    {
        int total = (int)(_seconds % 86400.0);
        return $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Ora ceasului   : {FormatTime()}";
        yield return $"Tema           : {(_isNight ? "noapte" : "zi")} (uNight = {_night.Value.X:0.##})";
        yield return $"Faza lunii     : {MoonPhase():0.##}";
    }
}
