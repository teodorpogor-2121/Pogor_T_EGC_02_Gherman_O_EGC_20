#version 330 core

// Tema acasa, varianta A: ceas analogic desenat pe un singur dreptunghi.
// Coordonate: p in [-1, 1] x [-1, 1], cu originea in centrul cadranului.

in vec2 vUV;
out vec4 FragColor;

uniform bool  uShowUV;
uniform float uTime;
uniform vec4  uTint;

uniform float uHourAngle;     // radiani, de la ora 12, in sensul acelor
uniform float uMinuteAngle;
uniform float uSecondAngle;
uniform float uNight;         // 0 = zi, 1 = noapte
uniform float uMoonPhase;     // 0 = luna noua, 0.5 = luna plina
uniform float uRimWidth;
uniform float uHandWidth;

uniform vec3 uDayFace;
uniform vec3 uNightFace;
uniform vec3 uDayInk;
uniform vec3 uNightInk;
uniform vec3 uAccent;

const float TAU = 6.28318530718;
const float RADIUS = 0.97;

// Acoperirea unei forme data prin distanta cu semn d (negativa in interior),
// cu margine neteda de aproximativ un pixel.
float fill(float d, float px)
{
    return 1.0 - smoothstep(-px, px, d);
}

// Distanta de la p la segmentul ab.
float sdSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

// Directia unui ac: unghiul 0 = ora 12, creste in sensul acelor.
vec2 direction(float angle)
{
    return vec2(sin(angle), cos(angle));
}

// Un ac: segment de la -tail la len pe directia data, cu jumatatea de grosime w.
float hand(vec2 p, float angle, float len, float tail, float w, float px)
{
    vec2 dir = direction(angle);
    return fill(sdSegment(p, -dir * tail, dir * len) - w, px);
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    vec2 p = (vUV - 0.5) * 2.0;
    float px = max(fwidth(p.x), fwidth(p.y));
    float r = length(p);

    vec3 face = mix(uDayFace, uNightFace, uNight);
    vec3 ink = mix(uDayInk, uNightInk, uNight);
    vec3 rim = mix(vec3(0.55, 0.38, 0.18), vec3(0.28, 0.34, 0.55), uNight);

    // 1. Cadranul, usor mai intunecat spre margine.
    vec3 color = face * (1.0 - 0.12 * r * r);

    // 2. Rama, cu un reflex dinspre stanga-sus.
    float inner = RADIUS - uRimWidth;
    float light = 0.85 + 0.35 * dot(p / max(r, 0.001), vec2(-0.7071, 0.7071));
    color = mix(color, rim * light, fill(inner - r, px));

    // 3. Cele 60 de gradatii; la fiecare a cincea (orele) mai lungi si mai groase.
    float stepAngle = TAU / 60.0;
    float index = floor(atan(p.x, p.y) / stepAngle + 0.5);
    float major = step(mod(index + 60.5, 5.0), 1.0);
    vec2 tickDir = direction(index * stepAngle);
    float tickOuter = inner - 0.045;
    float tickLength = mix(0.035, 0.11, major);
    float tickWidth = mix(0.004, 0.014, major);
    float tick = sdSegment(p, tickDir * (tickOuter - tickLength), tickDir * tickOuter) - tickWidth;
    color = mix(color, ink, fill(tick, px));

    // 4. Fereastra cu faza lunii (bonus).
    vec2 center = vec2(0.0, -0.36);
    const float windowRadius = 0.15;
    const float moonRadius = 0.09;
    vec2 w = p - center;
    float inWindow = fill(length(w) - windowRadius, px);

    vec3 sky = mix(vec3(0.22, 0.34, 0.62), vec3(0.02, 0.03, 0.10), uNight);

    // Stelele clipesc (timpul de joc).
    float stars = 0.0;
    stars += fill(length(w - vec2(-0.110,  0.050)) - 0.008, px) * (0.6 + 0.4 * sin(uTime * 3.0));
    stars += fill(length(w - vec2( 0.100,  0.075)) - 0.006, px) * (0.6 + 0.4 * sin(uTime * 2.3 + 1.7));
    stars += fill(length(w - vec2( 0.115, -0.045)) - 0.007, px) * (0.6 + 0.4 * sin(uTime * 4.1 + 3.1));
    stars += fill(length(w - vec2(-0.080, -0.095)) - 0.005, px) * (0.6 + 0.4 * sin(uTime * 2.9 + 4.6));
    sky = mix(sky, vec3(1.0, 0.97, 0.85), clamp(stars, 0.0, 1.0));

    // Linia dintre lumina si umbra este o elipsa: x = cos(2*pi*faza) * sqrt(r^2 - y^2).
    float edge = cos(TAU * uMoonPhase) * sqrt(max(moonRadius * moonRadius - w.y * w.y, 0.0));
    float side = mix(w.x - edge, -edge - w.x, step(0.5, uMoonPhase));
    float lit = smoothstep(-px, px, side);
    vec3 moon = mix(vec3(0.16, 0.18, 0.26), vec3(0.97, 0.94, 0.80), lit);
    sky = mix(sky, moon, fill(length(w) - moonRadius, px));

    color = mix(color, sky, inWindow);
    color = mix(color, rim, fill(abs(length(w) - windowRadius) - 0.010, px));

    // 5. Acele: orar, minutar, secundar.
    color = mix(color, ink, hand(p, uHourAngle, 0.48, 0.08, 0.028 * uHandWidth, px));
    color = mix(color, ink, hand(p, uMinuteAngle, 0.70, 0.10, 0.018 * uHandWidth, px));
    color = mix(color, uAccent, hand(p, uSecondAngle, 0.76, 0.18, 0.006 * uHandWidth, px));

    // 6. Butonul din centru.
    color = mix(color, uAccent, fill(r - 0.045, px));
    color = mix(color, ink, fill(r - 0.018, px));

    // In afara cercului dreptunghiul este transparent.
    float alpha = fill(r - RADIUS, px);
    FragColor = vec4(color, alpha) * uTint;
}
