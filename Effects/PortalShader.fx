sampler2D texSampler;

float4x4 MatrixTransform;

float time;

float2 flipPos;
float2 flipNormal;
bool flipEnabled;

float2 portalPos;
float portalAngle;
float portalSize;
float2 playerPos;

float4 portalColor;

float Random(float2 pos)
{
    float mod = time % 123;

	// https://stackoverflow.com/a/10625698/13645088
    float2 K1 = float2(
        23.14069263277926, // e^pi (Gelfond's constant)
         2.665144142690225 // 2^sqrt(2) (Gelfond Schneider constant)
    );
    return frac(cos(dot(pos % 123, K1) + mod * 0.01) * 123.456) % 1;
}

float2 RotateVector(float2 vec, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return float2(c*vec.x-s*vec.y, s*vec.x+c*vec.y);
}

float PortalSideDot(float2 scPos, float2 sidePos, float angle)
{
    float2 portalSideDir = float2(cos(angle), sin(angle));
    float2 portalSideToPlayer = RotateVector(playerPos - sidePos, angle);
    return dot(normalize(scPos - sidePos), normalize(portalSideToPlayer));
}

void MainVS(inout float4 pos : POSITION, inout float4 color : COLOR0, inout float2 txPos : TEXCOORD0)
{
    if (flipEnabled)
    {
        float2 norm = normalize(flipNormal);
        pos.xy -= flipPos;
        pos.xy += norm * -dot(pos.xy, norm) * 2;
        pos.xy += flipPos;
    }
    pos = mul(pos, MatrixTransform);
}

float4 MainPS(float2 txPos : TEXCOORD0, float2 scPos : VPOS) : COLOR
{
    float2 portalDir = float2(cos(portalAngle), sin(portalAngle));

    if (dot(portalDir, scPos - portalPos) < 0)
        return 0;

    float2 portalSideDir = float2(-portalDir.y, portalDir.x) * (portalSize/2);

    float a = 0.05;

    scPos -= scPos % 2;

    float d = max(PortalSideDot(scPos, portalPos - portalSideDir, 1.5708),
                  PortalSideDot(scPos, portalPos + portalSideDir, -1.5708));

    float i1 = clamp(1 - abs(d) / a, 0, 1);
    float i2 = clamp(1 - abs(d) / a * 2, 0, 1);

    float rand1 = Random(scPos);
    float rand2 = Random(scPos + 1249874);

    i1 = clamp((i1 - i1 * rand1) * (i1 / rand1), 0, 1);
    i2 = clamp((i2 - i2 * rand2) * (i2 / rand2), 0, 1);

    float3 rand3 = float3(Random(scPos+10), Random(scPos+20), Random(scPos+30));
    float4 borderColor = portalColor + float4((rand3 * 2 - 1) * 0.2 * portalColor.rgb, 0);

    if (d < 0)
        return lerp(lerp(tex2D(texSampler, txPos), 0, i2), borderColor, i1);
    
    return lerp(0, borderColor, i1);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}