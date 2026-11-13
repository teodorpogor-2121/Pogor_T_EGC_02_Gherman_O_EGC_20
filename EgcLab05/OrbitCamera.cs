using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;


public class OrbitCamera : Camera
{
    // --- Starea camerei ---
    public Vector3 Target;          
    public float Distance;          
    public float Azimuth;          
    public float Elevation;        

    // --- Limite și parametri ---
    public const float MaxElevation = 89.0f;   
    public const float MinDistance = 2.0f;
    public const float MaxDistance = 30.0f;
    public float MouseSensitivity = 0.3f;      
    public float KeyRotationSpeed = 90.0f;     
    public float ZoomStep = 0.5f;              
    public float TargetSpeed = 3.0f;          

    // --- Valori de start ---
    private static readonly Vector3 StartTarget = new(0.0f, 1.0f, 0.0f);
    private const float StartDistance = 8.0f;
    private const float StartAzimuth = 90.0f;
    private const float StartElevation = 20.0f;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "Orbită";

    /// <summary>Poziția pe sferă: țintă + distanță · (cos el · cos az, sin el, cos el · sin az).</summary>
    public override Vector3 Position
    {
        get
        {
            float az = MathHelper.DegreesToRadians(Azimuth);
            float el = MathHelper.DegreesToRadians(Elevation);

            Vector3 offset = new(
                MathF.Cos(el) * MathF.Cos(az),
                MathF.Sin(el),
                MathF.Cos(el) * MathF.Sin(az));

            return Target + Distance * offset;
        }
    }

    /// <summary>Camera privește spre țintă, deci direcția e opusă lui "offset".</summary>
    public override Vector3 Front => Vector3.Normalize(Target - Position);

    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(Position, Target, Vector3.UnitY);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // 1. Mouse: clic stânga ținut + mișcare rotește camera.
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;   
        }

        // 2. Săgețile: rotație de la tastatură, independentă de numărul de cadre (înmulțim cu dt).
        float step = KeyRotationSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth += step;
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth -= step;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += step;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= step;

        // 2b. BONUS 2: W A S D mută ținta în planul orizontal, relativ la direcția privirii.
        Vector3 forward = new(Front.X, 0.0f, Front.Z);
        if (forward.LengthSquared > 1e-6f)
        {
            forward.Normalize();
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
            Vector3 move = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) move += forward;
            if (keyboard.IsKeyDown(Keys.S)) move -= forward;
            if (keyboard.IsKeyDown(Keys.D)) move += right;
            if (keyboard.IsKeyDown(Keys.A)) move -= right;
            if (move.LengthSquared > 0.0f)
            {
                move.Normalize();
                float speed = TargetSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? 2.5f : 1.0f);
                Target += move * speed * dt;
            }
        }

        // 3. Rotița: distanța (NU FOV-ul). Rotița în sus = mai aproape.
        Distance -= mouse.ScrollDelta.Y * ZoomStep;

        // 4. Limite.
        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);
        Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
    }

    /// <summary>
    /// BONUS 1 (FPS -> orbită): ținta e punctul aflat la <paramref name="distance"/> în fața camerei FPS.
    /// Unghiurile vin din vectorul țintă -> cameră, o = -front: elevația = asin(o.y), azimutul = atan2(o.z, o.x).
    /// Poziția rezultată coincide cu cea a camerei FPS, deci imaginea nu "sare".
    /// </summary>
    public void SetFromView(Vector3 position, Vector3 front, float distance)
    {
        front.Normalize();
        Vector3 o = -front;
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
        Target = position + front * Distance;
        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(o.Y)), -MaxElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));
    }

    public override void Reset()
    {
        Target = StartTarget;
        Distance = StartDistance;
        Azimuth = StartAzimuth;
        Elevation = StartElevation;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 p = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {Azimuth % 360.0f,7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({p.X,6:F2}, {p.Y,6:F2}, {p.Z,6:F2})";
    }
}