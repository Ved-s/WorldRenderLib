float2 player = float2(1, 1);

float4 PSMain(float2 pos : TEXCOORD0) : COLOR
{
	float2 v = player - float2(.5, .5);
    float angle = atan2(v.y, v.x);
	float2 playerpos = float2(cos(angle) / 4 + .5, sin(angle) / 4 + .5);
	bool b = length(playerpos - pos) < .05;
	return float4(b?1:0, 0, 0, 1);
}

technique Main
{
	pass Main
	{
        PixelShader = compile ps_3_0 PSMain();
    }
}