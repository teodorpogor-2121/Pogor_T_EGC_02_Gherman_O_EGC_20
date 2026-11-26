using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Varianta B: evită obstacolele
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema 6, varianta B: un drum cu trei benzi. Jucătorul stă pe loc (pe Z), obstacolele vin spre el.
/// Stânga / dreapta = schimbă banda (Tween cu EaseOut), Space = salt cu gravitație.
/// Toate mișcările folosesc doar timpul de joc (dt).
/// </summary>
public sealed class RunnerScene : Scene
{
    private const string NewGame = "joc nou";

    private const float LaneWidth = 2.0f;
    private const float PlayerZ = 3.0f;
    private const float GroundY = 0.5f;            // centrul cubului când stă pe drum
    private const float Gravity = 25.0f;
    private const float JumpSpeed = 9.0f;
    private const float LaneChangeTime = 0.15f;    // sub 0,2 s
    private const float SpawnZ = -45.0f;
    private const float DespawnZ = 6.0f;
    private const float BaseSpeed = 12.0f;
    private const float MaxSpeed = 28.0f;
    private const float PlayerHalf = 0.45f;        // semi-latura cutiei jucătorului (puțin mai mică decât cubul)
    private const int MarkCount = 12;
    private const float MarkSpacing = 4.0f;

    private sealed class Obstacle
    {
        public SceneObject Obj { get; }
        public Vector3 Half { get; }
        public bool Tall { get; }

        public Obstacle(SceneObject obj, Vector3 half, bool tall)
        {
            Obj = obj;
            Half = half;
            Tall = tall;
        }
    }

    private readonly Random _random = new();
    private readonly List<Obstacle> _obstacles = new();
    private readonly List<SceneObject> _marks = new();

    private Mesh _cube = null!;
    private SceneObject _player = null!;   // pivot: poziția jucătorului
    private SceneObject _body = null!;     // copilul pivotului: cubul vizibil
    private SceneObject _shadow = null!;
    private Tween _laneTween = null!;

    private int _lane;
    private float _height;                 // înălțimea față de drum
    private float _verticalSpeed;
    private bool _alive;
    private float _survived;               // scorul = timpul supraviețuit
    private float _best;                   // recordul
    private float _speed;
    private float _spawnTimer;

    public override string Name => "evită obstacolele";

    public override string Status => _alive
        ? $"scor {_survived:F1} s | record {_best:F1} s"
        : $"GAME OVER: scor {_survived:F1} s | record {_best:F1} s | R = joc nou";

    public override IEnumerable<string> ActionNames => new[]
    {
        Actions.Left, Actions.Right, Actions.Jump, NewGame,
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 4.5f, 9.5f);
        CameraTarget = new Vector3(0.0f, 0.8f, -6.0f);

        // Dacă LabScene a legat deja aceste acțiuni, Bind nu duplică tastele.
        input.Bind(Actions.Left, Keys.A, Keys.Left);
        input.Bind(Actions.Right, Keys.D, Keys.Right);
        input.Bind(Actions.Jump, Keys.Space);
        input.Bind(NewGame, Keys.R);

        _cube = AddMesh(Shapes.ShadedCube());
        Mesh disk = AddMesh(Shapes.ShadedCylinder(0.5f, 0.02f, 24));

        // Drumul.
        SceneObject road = AddObject("drum", _cube);
        road.Transform.Position = new Vector3(0.0f, -0.05f, -20.0f);
        road.Transform.Scale = new Vector3(3.0f * LaneWidth + 0.2f, 0.1f, 90.0f);
        road.Tint = new Color4(0.22f, 0.23f, 0.27f, 1.0f);

        // Marcajele dintre benzi (BONUS): se deplasează odată cu lumea.
        foreach (float x in new[] { -LaneWidth / 2.0f, LaneWidth / 2.0f })
        {
            for (int i = 0; i < MarkCount; i++)
            {
                SceneObject mark = AddObject("marcaj", _cube);
                mark.Transform.Position = new Vector3(x, 0.02f, 0.0f);
                mark.Transform.Scale = new Vector3(0.08f, 0.02f, 1.5f);
                mark.Tint = new Color4(0.85f, 0.85f, 0.9f, 1.0f);
                _marks.Add(mark);
            }
        }

        // Umbra (pe drum) și jucătorul: un pivot cu doi copii (corpul) și doi "ochi" mici.
        _shadow = AddObject("umbra jucătorului", disk);
        _shadow.Tint = new Color4(0.05f, 0.05f, 0.08f, 1.0f);

        _player = AddObject("jucător", null);
        _body = AddObject("corp", _cube, _player);
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            SceneObject eye = AddObject("ochi", _cube, _player);
            eye.Transform.Position = new Vector3(x, 0.15f, -0.5f);   // în față = spre -Z
            eye.Transform.Scale = new Vector3(0.15f);
            eye.Tint = new Color4(0.05f, 0.05f, 0.1f, 1.0f);
        }

        StartGame();
    }

    private static float LaneX(int lane) => (lane - 1) * LaneWidth;

    private Vector3 PlayerPosition => new(_laneTween.Value.X, GroundY + _height, PlayerZ);

    // --- Joc nou ---------------------------------------------------------------------------

    private void StartGame()
    {
        foreach (Obstacle o in _obstacles)
        {
            Objects.Remove(o.Obj);
        }
        _obstacles.Clear();

        for (int i = 0; i < _marks.Count; i++)
        {
            int k = i % MarkCount;
            Vector3 p = _marks[i].Transform.Position;
            _marks[i].Transform.Position = new Vector3(p.X, p.Y, 4.0f - k * MarkSpacing);
        }

        _lane = 1;
        _laneTween = new Tween(new Vector3(LaneX(_lane), 0.0f, 0.0f), new Vector3(LaneX(_lane), 0.0f, 0.0f),
                               LaneChangeTime, Easing.EaseOut);
        _height = 0.0f;
        _verticalSpeed = 0.0f;
        _survived = 0.0f;
        _speed = BaseSpeed;
        _spawnTimer = 1.0f;
        _alive = true;
        _body.Tint = new Color4(0.3f, 0.85f, 1.0f, 1.0f);
        ApplyPlayerTransform();
    }

    // --- Actualizarea, la fiecare cadru -------------------------------------------------------

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(NewGame))
        {
            StartGame();
            ConsolePanel.PrintMessage("joc nou");
        }
        if (!_alive) return;

        HandleInput(input);

        _survived += dt;
        _speed = MathF.Min(MaxSpeed, BaseSpeed + 0.4f * _survived);   // lumea accelerează

        _laneTween.Update(dt);
        UpdatePlayerPhysics(dt);
        UpdateWorld(dt);
        ApplyPlayerTransform();
        CheckCollisions();
    }

    private void HandleInput(Input input)
    {
        // Apăsare (nu ținere): o apăsare = o bandă.
        if (input.WasPressed(Actions.Left) && _lane > 0)
        {
            _lane--;
            _laneTween.GoTo(new Vector3(LaneX(_lane), 0.0f, 0.0f), LaneChangeTime);
        }
        if (input.WasPressed(Actions.Right) && _lane < 2)
        {
            _lane++;
            _laneTween.GoTo(new Vector3(LaneX(_lane), 0.0f, 0.0f), LaneChangeTime);
        }
        if (input.WasPressed(Actions.Jump) && _height <= 0.0f)
        {
            _verticalSpeed = JumpSpeed;
        }
    }

    private void UpdatePlayerPhysics(float dt)
    {
        if (_height > 0.0f || _verticalSpeed > 0.0f)
        {
            _verticalSpeed -= Gravity * dt;     // întâi viteza, apoi poziția
            _height += _verticalSpeed * dt;
            if (_height <= 0.0f)
            {
                _height = 0.0f;
                _verticalSpeed = 0.0f;
            }
        }
    }

    private void UpdateWorld(float dt)
    {
        float move = _speed * dt;

        // Obstacolele vin spre jucător; cele depășite dispar.
        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle o = _obstacles[i];
            Vector3 p = o.Obj.Transform.Position;
            p.Z += move;
            o.Obj.Transform.Position = p;
            if (p.Z > DespawnZ)
            {
                Objects.Remove(o.Obj);
                _obstacles.RemoveAt(i);
            }
        }

        // Marcajele: când ies din cadru, se întorc în spate.
        foreach (SceneObject mark in _marks)
        {
            Vector3 p = mark.Transform.Position;
            p.Z += move;
            if (p.Z > DespawnZ) p.Z -= MarkCount * MarkSpacing;
            mark.Transform.Position = p;
        }

        // Frecvența obstacolelor crește în timp (intervalul scade până la 0,5 s).
        _spawnTimer -= dt;
        if (_spawnTimer <= 0.0f)
        {
            SpawnObstacles();
            _spawnTimer += MathF.Max(0.5f, 1.5f - 0.025f * _survived);
        }
    }

    private void SpawnObstacles()
    {
        int lane = _random.Next(3);
        SpawnObstacle(lane);

        // Din ce în ce mai des, apare și un al doilea obstacol, pe altă bandă (una rămâne mereu liberă).
        if (_random.NextDouble() < MathF.Min(0.35f, 0.01f * _survived))
        {
            SpawnObstacle((lane + 1 + _random.Next(2)) % 3);
        }
    }

    private void SpawnObstacle(int lane)
    {
        bool tall = _random.NextDouble() < 0.5;
        var size = tall ? new Vector3(1.6f, 2.5f, 1.0f) : new Vector3(1.6f, 0.6f, 1.0f);

        SceneObject obj = AddObject(tall ? "obstacol înalt" : "obstacol jos", _cube);
        obj.Transform.Scale = size;
        obj.Transform.Position = new Vector3(LaneX(lane), size.Y / 2.0f, SpawnZ);
        obj.Tint = tall ? new Color4(0.9f, 0.25f, 0.25f, 1.0f) : new Color4(1.0f, 0.65f, 0.2f, 1.0f);

        _obstacles.Add(new Obstacle(obj, size / 2.0f, tall));
    }

    private void ApplyPlayerTransform()
    {
        Vector3 p = PlayerPosition;
        _player.Transform.Position = p;

        float shadowSize = 1.1f / (1.0f + 0.25f * _height);
        _shadow.Transform.Position = new Vector3(p.X, 0.03f, p.Z);
        _shadow.Transform.Scale = new Vector3(shadowSize, 1.0f, shadowSize);
    }

    // --- Coliziuni: AABB (cutii aliniate cu axele) ------------------------------------------------

    private void CheckCollisions()
    {
        Vector3 p = PlayerPosition;
        foreach (Obstacle o in _obstacles)
        {
            Vector3 d = o.Obj.Transform.Position - p;
            bool overlapX = MathF.Abs(d.X) < PlayerHalf + o.Half.X;
            bool overlapY = MathF.Abs(d.Y) < PlayerHalf + o.Half.Y;
            bool overlapZ = MathF.Abs(d.Z) < PlayerHalf + o.Half.Z;
            if (overlapX && overlapY && overlapZ)
            {
                GameOver();
                return;
            }
        }
    }

    private void GameOver()
    {
        _alive = false;
        _best = MathF.Max(_best, _survived);
        _body.Tint = new Color4(1.0f, 0.2f, 0.2f, 1.0f);
        ConsolePanel.PrintMessage($"coliziune! scor {_survived:F1} s, record {_best:F1} s (R = joc nou)");
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Joc            : {(_alive ? "în desfășurare" : "terminat")}   scor {_survived:F1} s   record {_best:F1} s";
        yield return $"Lume           : viteza {_speed:F1} u/s   obstacole {_obstacles.Count}";
        yield return $"Jucător        : banda {_lane}   înălțime {_height:F2}   v_y = {_verticalSpeed:F2}";
    }
}