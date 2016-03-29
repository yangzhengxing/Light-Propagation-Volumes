#version 150
precision lowp float;

#define BLINN_PHONG

#define SHADOW_CASCADES_COUNT
#define LPV_CASCADES_COUNT

in vec3 positionWorld;
#ifndef NOR_TEX
in vec3 normal;
#endif
in vec2 texCoord;
in vec4 color;
#ifdef SHAD_TEX
in vec3 shadowCoord[SHADOW_CASCADES_COUNT];
#endif
#ifdef NOR_TEX
in mat3 mtbnti;
#endif

uniform vec3 cam;

uniform sampler2D difTex;
#ifdef ALP_TEX
uniform sampler2D alpTex;
#endif
uniform sampler2D speTex;
#ifdef NOR_TEX
uniform sampler2D norTex;
#endif
#ifdef SHAD_TEX
uniform sampler2DShadow shadTex;
uniform sampler3D lpvTexR;
uniform sampler3D lpvTexG;
uniform sampler3D lpvTexB;
#endif
#ifdef SHADOW_JITTER
uniform sampler2D shadDepthTex;
#endif

uniform int type;
#ifdef ALP_TEX
uniform float opacity;
#endif
#ifdef SHAD_TEX
uniform vec4 tiles;
uniform int tileInstances;
uniform vec2 shadowClips[SHADOW_CASCADES_COUNT];
#endif
#ifdef SHADOW_JITTER
uniform vec3 shadowTexSize;
#endif

uniform vec3 lightAmb;
uniform vec3 lightPos;
uniform vec2 lightRange;
uniform vec3 lightColor;
uniform vec4 lightSpeColor;
uniform vec2 fogRange;
uniform vec3 fogColor;

#ifdef SHAD_TEX
uniform vec4 lpvPos[LPV_CASCADES_COUNT];
uniform vec3 lpvCellSize[LPV_CASCADES_COUNT];
uniform vec2 lpvParams;
#endif

out vec4 glFragColor;

void main()
{
  vec4 fragDif = texture(difTex, texCoord);

  if(((type & 0x20000000) != 0) && (fragDif.a < 0.8))
    discard;

#ifndef ALP_TEX
  float alpha = 1.0;
#else
  vec3 fragAlp = texture(alpTex, texCoord).rgb;
  float alpha = (fragAlp.r + fragAlp.g + fragAlp.b) * 0.3333333334 * color.a * opacity;

  if(alpha == 0.0)
    discard;
#endif

  vec3 fragSpe = texture(speTex, texCoord).rgb;

#ifndef NOR_TEX
  vec3 normalDir = normalize(normal);
#else
  vec3 fragNor = texture(norTex, texCoord).rgb;
  vec3 normalDir = normalize(mtbnti * normalize(fragNor * 2.0 - 1.0));
#endif

#ifdef NOR_TEX_DEBUG
  glFragColor = vec4(normalDir * 0.5 + 0.5, alpha);
  return;
#endif

#ifndef SHAD_TEX
  vec3 lpvColor = vec3(0.0, 0.0, 0.0);
#else
  vec4 sh = vec4(0.2821, -0.4886 * -normalDir.y, 0.4886 * -normalDir.z, -0.4886 * -normalDir.x);
  vec3 p = (lpvPos[0].xyz + positionWorld) * lpvCellSize[0];
  vec4 lpvShR0 = texture(lpvTexR, p);
  vec4 lpvShG0 = texture(lpvTexG, p);
  vec4 lpvShB0 = texture(lpvTexB, p);
  vec3 lpvColor = vec3(dot(sh, lpvShR0), dot(sh, lpvShG0), dot(sh, lpvShB0)) * lpvParams.y;
  if((lpvColor.x < 0.0) || (lpvColor.y < 0.0) || (lpvColor.z < 0.0))
    lpvColor = vec3(0.0);
#endif

  float fragDist = distance(cam, positionWorld);

#ifndef SHAD_TEX
  float depthVis = 1.0;
#else
  vec3 sCoord[SHADOW_CASCADES_COUNT];
  for(int i = 0; i < SHADOW_CASCADES_COUNT; i++)
    sCoord[i] = shadowCoord[i];

  int shadowIndex = 0;
  for(shadowIndex = 0; shadowIndex < SHADOW_CASCADES_COUNT; shadowIndex++)
  {
    if(fragDist <= shadowClips[shadowIndex].x)
      break;
  }

  float depthVis = 1.0;
  if(shadowIndex < SHADOW_CASCADES_COUNT)
  {
#ifndef SHADOW_JITTER
    int x = shadowIndex % int(tiles.x);
    int y = shadowIndex / int(tiles.x);
    vec2 tileMin = vec2(float(x), float(y)) * tiles.zw;
    depthVis = texture(shadTex, vec3(sCoord[shadowIndex].xy * tiles.zw + tileMin, sCoord[shadowIndex].z));
#else
    for(int i = 2; i < 4/*SHADOW_CASCADES_COUNT*/; i++)
    {
      if(shadowIndex > i)
        continue;

      int x = i % int(tiles.x);
      int y = i / int(tiles.x);
      vec2 tileMin = vec2(float(x), float(y)) * tiles.zw;
      /*float depthStart = 0.1 * float(i);
      float depthEnd = 0.1 * float(i + 1);*/
      float depthStart = 0.0;
      float depthEnd = 1.3;
      if(i == 3)
      {
        depthStart = 1.0;
        depthEnd = 50.0;
      }
      float penumbraVisOffset = (sCoord[i].z - texture(shadDepthTex, sCoord[i].xy * tiles.zw + tileMin).r) * shadowClips[i].y;
      if((penumbraVisOffset >= depthStart) && (penumbraVisOffset < depthEnd))
        depthVis -= 1.0 - texture(shadTex, vec3(sCoord[i].xy * tiles.zw + tileMin, sCoord[i].z));
    }
    depthVis = max(0.0, depthVis);
#endif
  }
#endif

  vec3 viewDir = normalize(cam - positionWorld);
  vec3 lightDir = lightPos - positionWorld;

  float lightDist = clamp((length(lightDir) - lightRange.x) / (lightRange.y - lightRange.x) * -1.0 + 1.0, 0.0, 1.0);
  lightDir = normalize(lightDir);
  float lightDot = max(0.0, dot(normalDir, lightDir)) * depthVis;

  vec3 colorDif = lightColor * lightDot * lightDist + lightAmb + lpvColor;
#ifndef BLINN_PHONG
  float lightSpecDot = max(0.0, dot(viewDir, reflect(-lightDir, normalDir))); // phong
#else
  float lightSpecDot = max(0.0, dot(normalDir, normalize(lightDir + viewDir))); // blinn-phong
#endif
  float fresnelSpe = min(pow(1.0 - dot(viewDir, normalDir), 4.0) + 0.25, 1.0);
  vec3 colorSpe = lightSpeColor.rgb * pow(lightSpecDot, lightSpeColor.a) * lightDot * lightDist * fresnelSpe;

  float fogDist = clamp((fragDist - fogRange.x) / (fogRange.y - fogRange.x), 0.0, 1.0);
  /*float fogDot = pow(max(0.0, dot(normalize(cam - lightPos), viewDir)), 16.0);
  float fresPow = clamp(pow(1.0 - dot(viewDir, normalDir) * 0.5, 8.0), 0.0, 1.0) * 1.0;*/

#ifdef ALP_TEX
  alpha += (colorSpe.r + colorSpe.g + colorSpe.b) * 0.3333333334;
#endif
  glFragColor = vec4(mix(fragDif.rgb * color.rgb * colorDif + fragSpe * colorSpe/* + fresPow * fogColor*/, fogColor/* + fogDot * lightColor*/, fogDist), alpha);
}
