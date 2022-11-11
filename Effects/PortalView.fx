sampler texSampler : register(S0);

float2 player;
float2 portalCenter;
float portalSize;
float time;
float4 portalColor;

#define Pi 3.1415926
#define TwoPi 6.28319
#define HalfPi 1.5708

float Random(float2 pos)
{
	// https://stackoverflow.com/a/10625698/13645088
    float2 K1 = float2(
        23.14069263277926, // e^pi (Gelfond's constant)
         2.665144142690225 // 2^sqrt(2) (Gelfond Schneider constant)
    );
    return frac(cos(dot(pos, K1)) * 12345.678 + time);
}

float GetAngle(float2 pos, float2 origin, float angle)
{
    pos -= origin;
    float pa = atan2(pos.y, pos.x) - angle;

    pa += (pa > Pi) ? -TwoPi : (pa < -Pi) ? TwoPi : 0;
    pa = abs(pa);
    return pa;
}

bool TrimAngle(float2 pos, float2 origin, float angle, float angled)
{
    pos -= origin;
    float pa = atan2(pos.y, pos.x) - angle;

    pa += (pa > Pi) ? -TwoPi : (pa < -Pi) ? TwoPi : 0;
    pa = abs(pa);
    return pa < angled;
}

float FixAngle(float angle) // -pi -> pi to 0 to 2pi
{
    if (angle < 0)
        return angle + TwoPi;
    return angle;
}

float AngleTo(float2 pos, float2 to)
{
    float2 vec = pos - to;
    return FixAngle(atan2(vec.y, vec.x));
}

float4 MainPS(float2 pos : TEXCOORD0) : COLOR
{
    float angle = AngleTo(portalCenter, float2(.5, .5));

    if (TrimAngle(pos, portalCenter, angle, HalfPi))
        return 0;

    float portalAngleA = angle - HalfPi;
    float2 portalA = portalCenter + float2(cos(portalAngleA), sin(portalAngleA)) * (portalSize / 2);

    float portalAngleB = angle + HalfPi;
    float2 portalB = portalCenter + float2(cos(portalAngleB), sin(portalAngleB)) * (portalSize / 2);

    float angleA = AngleTo(player, portalA);
    float angleB = AngleTo(player, portalB);

    float k = 3.1;

    float noiseB = max(0, GetAngle(pos, portalB, angleB) - k) / (Pi - k);
    float noiseA = max(0, GetAngle(pos, portalA, angleA) - k) / (Pi - k);
    float noise1 = max(noiseA, noiseB);

    noiseB = max(0, GetAngle(pos, portalB, angleB - 0.007) - k) / (Pi - k);
    noiseA = max(0, GetAngle(pos, portalA, angleA + 0.007) - k) / (Pi - k);
    float noise2 = max(noiseA, noiseB);
    
    float rnd1 = Random(pos + float2(12, 34)) * max(0, noise1 - .5) * 2;
    float rnd2 = Random(pos) * noise2;

    float4 tex = tex2D(texSampler, pos);
    //tex = float4(.3, .8, 0, 1);

    if (TrimAngle(pos, player, angleB + HalfPi, HalfPi)
	 || TrimAngle(pos, player, angleA - HalfPi, HalfPi))
    {
        return lerp(0, portalColor, rnd2) + lerp(0, tex, rnd1);
    }

    return lerp(tex, portalColor, rnd1);
}


technique Main
{
    pass Main
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}