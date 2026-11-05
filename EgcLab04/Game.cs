using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)
    private Mesh _satelliteMesh = null!;   // T5: forma satelitului (nu face parte din _scene)
    private float _orbitAngle;             // T5: unghiul orbitei, in grade
    private Vector2 _pivot = Vector2.Zero;


    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { Position = new Vector2(-1.2f, 1.0f) });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
          new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        _satelliteMesh = Shapes.Triangle();   // T5   <-- AICI, inainte de }
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];

        // Shift tinut apasat: toate miscarile obiectului sunt de 3 ori mai rapide.
        float boost = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;

        const float moveSpeed = 2.0f;   // unitati de lume pe secunda
        float speed = moveSpeed * boost;
        if (KeyboardState.IsKeyDown(Keys.Left)) sel.Transform.Position.X -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down)) sel.Transform.Position.Y -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up)) sel.Transform.Position.Y += speed * dt;

        // T2: rotatia: 90 de grade pe secunda. Q = sens trigonometric, E = sens orar.
        if (KeyboardState.IsKeyDown(Keys.Q)) sel.Transform.Rotation += 90.0f * boost * dt;
        if (KeyboardState.IsKeyDown(Keys.E)) sel.Transform.Rotation -= 90.0f * boost * dt;

        // T2: scalarea se face prin INMULTIRE, nu prin adunare. Prin adunare scara ar putea ajunge
        // la 0 si apoi negativa (forma s-ar oglindi). Prin inmultire se apropie de 0 fara sa-l atinga,
        // iar marirea si micsorarea sunt simetrice.
        float scaleFactor = MathF.Pow(1.5f, boost * dt);   // factor de 1,5 pe secunda
        float s = sel.Transform.Scale.X;
        if (KeyboardState.IsKeyDown(Keys.X)) s *= scaleFactor;   // mareste
        if (KeyboardState.IsKeyDown(Keys.Z)) s /= scaleFactor;   // micsoreaza
        s = Math.Clamp(s, 0.1f, 10.0f);
        sel.Transform.Scale = new Vector2(s, s);
        
        
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            Vector2 w = ScreenToWorld(MouseState.Position);
            Console.WriteLine($"pixel={MouseState.Position}  lume={w}");
            sel.Transform.Position = w;
        }
        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O)) _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F)) _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R)) ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // T3: clic stanga: obiectul selectat se muta sub cursor (Depth ramane neschimbat).
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            sel.Transform.Position = ScreenToWorld(MouseState.Position);
        }

        // TODO (T4, facultativ): clic dreapta = pivot; tasta P ținută = rotație în jurul pivotului
        // T4: clic dreapta = alege pivotul (punctul din lume aflat sub cursor).
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position);
        }

        // T4: cat timp P este tinut apasat, obiectul se roteste in jurul pivotului cu 90 de grade pe secunda.
        if (KeyboardState.IsKeyDown(Keys.P))
        {
            float angleDeg = 90.0f * dt;
            float rad = MathHelper.DegreesToRadians(angleDeg);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            // pivotul devine origine, rotim, apoi il ducem inapoi
            Vector2 offset = sel.Transform.Position - _pivot;
            Vector2 rotated = new Vector2(offset.X * cos - offset.Y * sin,
                                          offset.X * sin + offset.Y * cos);
            sel.Transform.Position = _pivot + rotated;

            // unghiul propriu creste la fel, ca obiectul sa priveasca mereu spre pivot in acelasi fel
            sel.Transform.Rotation += angleDeg;
        }
        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------
        // T5: o rotatie completa la 4 secunde = 360 / 4 = 90 de grade pe secunda.
        _orbitAngle = (_orbitAngle + 90.0f * dt) % 360.0f;

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // TODO (T5, facultativ): satelitul obiectului selectat
        // T5: transformarea locala (scalare, translatie, rotatie) inmultita cu modelul parintelui.
        // T5: cand obiectul selectat este scalat, orbita se mareste/micsoreaza odata cu el, pentru ca
        // matricea modelului parintelui contine scalarea, iar ea se aplica si translatiei locale (1,2),
        // deci raza orbitei devine 1,2 * scara parintelui (iar satelitul se scaleaza si el).
        Matrix4 satelliteModel =
            Matrix4.CreateScale(0.3f, 0.3f, 1.0f)
          * Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f)
          * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle))
          * _scene[_selected].Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModel);
        _shader.SetColor("uTint", Color4.White);
        _satelliteMesh.Draw();
        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        
        _shader.Dispose();
        _satelliteMesh.Dispose();   // T5
        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        
        // Pasul 1: pixel -> NDC.
        // T3: y are semnul minus (1 - ...) deoarece in pixeli y creste in jos (origine stanga-sus),
        // iar in NDC y creste in sus. Fara el, obiectul ar ajunge in punctul simetric fata de axa orizontala.
        float xNdc = 2.0f * mousePixels.X / ClientSize.X - 1.0f;
        float yNdc = 1.0f - 2.0f * mousePixels.Y / ClientSize.Y;

        // Pasul 2: NDC -> lume, cu inversa lui view * projection (recalculata la fiecare clic,
        // fiindca se schimba cu camera si cu fereastra).
        Matrix4 inverse = (_camera.ViewMatrix * _camera.ProjectionMatrix).Inverted();
        Vector4 world = new Vector4(xNdc, yNdc, 0.0f, 1.0f) * inverse;

        return new Vector2(world.X, world.Y);
    }


    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
