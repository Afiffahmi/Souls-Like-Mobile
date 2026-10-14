#ifndef NUSANTARA_CHARACTER_REVEAL_INCLUDED
#define NUSANTARA_CHARACTER_REVEAL_INCLUDED
float _CharacterRevealActive;
float3 _CharacterRevealCenter, _CharacterRevealRight, _CharacterRevealUp, _CharacterRevealForward;
float4 _CharacterRevealSettings, _CharacterRevealCameraPosition;
float _CharacterRevealRearDepth;
float4 _CharacterRevealShadowDirection;
float _CharacterRevealGroundHeight;

void ApplyCharacterRevealSurface(float3 worldPosition, float preserveSupport)
{
    #if !defined(UNITY_PASS_META)
    if (_CharacterRevealActive < 0.5) return;
    float3 delta = worldPosition - _CharacterRevealCenter;
    if (preserveSupport > 0.5 && worldPosition.y <= _CharacterRevealSettings.z) return;
    // Clear the back of nearby meshes too, otherwise a hole exposes their inner shell.
    if (dot(delta, _CharacterRevealForward) >=
        _CharacterRevealRearDepth - _CharacterRevealSettings.w) return;
    #if defined(UNITY_PASS_SHADOWCASTER)
    // Foliage uses this shadow pass. Clear light rays crossing the opening;
    // retain surrounding canopy shading and leave the player's shader untouched.
    if (_CharacterRevealShadowDirection.w < 0.5 || _CharacterRevealShadowDirection.y >= -0.01) return;
    // Clear light rays crossing the reveal column, including uneven ground below it.
    float2 rayStart = float2(dot(delta, _CharacterRevealRight), dot(delta, _CharacterRevealUp));
    float2 rayDirection = float2(dot(_CharacterRevealShadowDirection.xyz, _CharacterRevealRight),
        dot(_CharacterRevealShadowDirection.xyz, _CharacterRevealUp));
    float maxTravel = max(0.0, (_CharacterRevealGroundHeight - _CharacterRevealSettings.x - worldPosition.y)
        / _CharacterRevealShadowDirection.y);
    float travel = clamp(-dot(rayStart, rayDirection) / max(dot(rayDirection, rayDirection), 0.0001), 0.0, maxTravel);
    worldPosition += _CharacterRevealShadowDirection.xyz * travel;
    delta = worldPosition - _CharacterRevealCenter;
    #endif
    // Orthographic: camera-plane circle. Perspective: project onto the target plane.
    if (_CharacterRevealCameraPosition.w > 0.0)
    {
        float3 ray = worldPosition - _CharacterRevealCameraPosition.xyz;
        float depth = dot(ray, _CharacterRevealForward);
        if (depth <= 0.001) return;
        delta = _CharacterRevealCameraPosition.xyz + ray * (_CharacterRevealCameraPosition.w / depth)
            - _CharacterRevealCenter;
    }
    float2 plane = float2(dot(delta, _CharacterRevealRight), dot(delta, _CharacterRevealUp));
    float distanceToCenter = length(plane);
    float radius = _CharacterRevealSettings.x;
    float feather = max(_CharacterRevealSettings.y, 0.0001);
    float coverage = saturate((radius - distanceToCenter) / feather);
    // Stable world-space dithering only along the soft edge; the center is fully clear.
    float noise = frac(52.9829189 * frac(dot(floor(plane * 100.0), float2(0.06711056, 0.00583715))));
    clip((1.0 - coverage) - (noise * 0.998 + 0.001));
    #endif
}

// Leaves are never walking surfaces, including the underside of a canopy.
void ApplyCharacterReveal(float3 worldPosition)
{
    ApplyCharacterRevealSurface(worldPosition, 0.0);
}

// Keep upward-facing floors underfoot without retaining walls, trunks or railings.
void ApplyCharacterReveal(float3 worldPosition, float3 worldNormal)
{
    ApplyCharacterRevealSurface(worldPosition, normalize(worldNormal).y > 0.65 ? 1.0 : 0.0);
}
#endif
