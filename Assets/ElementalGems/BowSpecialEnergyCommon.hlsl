// Shared by the built-in and URP passes. All surfaces use soft, texture-free masks.
float BowHash(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}
float BowNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(BowHash(cell), BowHash(cell + float2(1,0)), f.x),
                lerp(BowHash(cell + float2(0,1)), BowHash(cell + 1), f.x), f.y);
}
float BowEnergyMask(float2 uv, float mode, float time)
{
    if (mode < .5)
    {
        // Hot ribbon centre inside a broad feathered aura; flow breaks up straight bands.
        float edge = saturate(1 - abs(uv.y * 2 - 1));
        float flow = BowNoise(float2(uv.x * 32 - time * 5, uv.y * 3 + time));
        float ends = smoothstep(0,.025,uv.x) * smoothstep(0,.035,1-uv.x);
        return pow(edge,1.8) * (.65 + flow * .55) * ends;
    }
    float2 p = uv * 2 - 1;
    float radius2 = dot(p,p);
    if (mode < 1.5)
    {
        // A circular core and large soft halo, not rectangular particle cards.
        float disc = saturate(1 - radius2);
        return (exp2(-radius2 * 9) + exp2(-radius2 * 3) * .28) * disc * disc;
    }
    if (mode < 2.5)
    {
        // UV.y follows the pointed surface from its tip to the broad trailing edge.
        float side = smoothstep(0,.17,uv.x) * smoothstep(0,.17,1-uv.x);
        float tail = 1 - smoothstep(.66,1,uv.y);
        float flow = BowNoise(float2(uv.x * 5 + time * .8, uv.y * 8 - time * 5));
        float detail = BowNoise(float2(uv.x * 13 - time, uv.y * 19 - time * 9));
        float filaments = pow(saturate(1 - abs(sin(uv.x * 19 + flow * 3 + uv.y * 7 - time * 4))),3);
        return side * tail * (.4 + flow * .5 + detail * .22 + filaments * .35);
    }
    if (mode > 3.5)
    {
        // Broad longitudinal plasma flow: nested streams and moving bright knots.
        // This surface bridges the core and open ribbons without becoming a solid tube.
        float flow = BowNoise(float2(uv.x*15-time*2.7,uv.y*4+time*.4));
        float detail = BowNoise(float2(uv.x*37-time*6,uv.y*9));
        float bend = sin(uv.x*17-time*3.5)*.09 + (flow-.5)*.14;
        float crossSection = abs(uv.y*2-1+bend);
        float soft = pow(saturate(1-crossSection),1.35);
        float strands = pow(saturate(1-abs(sin(uv.y*14+flow*3+uv.x*9-time*4))),4);
        float ends = smoothstep(0,.08,uv.x)*smoothstep(0,.055,1-uv.x);
        return soft*ends*(.24+flow*.42+detail*.25+strands*.45);
    }
    // Billowing wisps have soft eroded edges and a lower-energy centre.
    float cloud = BowNoise(uv * 5 + float2(time * .4,-time * .8));
    float detail = BowNoise(uv * 11 - time * .3);
    return pow(saturate(1 - radius2),2) * smoothstep(.15,.8,cloud * .7 + detail * .3);
}
