// 호수 표면용 URP 커스텀 셰이더 (Shader Graph 미사용, 순수 HLSL, TerrainBlend.shader와 같은 컨벤션)
// - 물가(shoreline)는 마스크로 그리지 않는다. 평평한 물 쿼드를 호수 bbox 전체에 그대로
//   깔아두고, 먼저 그려진 불투명 지형이 깊이 테스트로 잘라내게 한다 — 수면보다 높은
//   지형은 물을 픽셀 단위로 정확히 가리므로 물가가 실제 지오메트리와 항상 일치한다
//   (마스크로 그리면 텍스처 해상도/보간 오차 때문에 미세한 틈이 남았다).
// - _MaskTex의 R채널은 "사용자가 그린 폴리곤 안쪽인지"만 담아, 호수가 아닌 저지대로
//   물이 번지는 것만 막는다. G채널(정규화된 수심)은 얕은 색↔깊은 색 보간 + 포말 위치에 쓴다.
// - "물처럼 보이게" 하는 다섯 가지를 텍스처 없이 순수 계산으로 얹는다(각각 Strength를
//   0으로 두면 꺼짐): (1) 여러 사인파를 합쳐 노멀을 흔드는 잔물결 반짝임, (2) 그 노멀
//   기준 Blinn-Phong 스페큘러, (3) 환경 반사(Reflection Probe가 있으면 주변 지형/나무,
//   없으면 스카이박스 폴백), (4) 그 반사량을 시야각에 따라 키우는 프레넬(정면에서도
//   _ReflectionBase만큼은 반사되게 바닥을 깔아둔다), (5) 수심이 0에 가까운 물가에 흰 포말 라인.
// - 큰 파도(스웰)는 격자 정점의 높이를 움직인다(_WaveHeight). 물가 선은 여전히 지형의 깊이 테스트가
//   만들므로 depth-cutout 방식의 정확도는 그대로이고, 그 선이 파도를 따라 함께 오르내린다.
//   스웰은 바람 방향 주변으로 흩어진 사인파 6개(서로 딱 떨어지지 않는 파장 비율 + 파장에 맞는
//   속도)에 노이즈로 세기/마루선을 흔들어, 사인파 몇 개를 겹친 격자무늬처럼 반복돼 보이지 않게 한다.
// - 비용 배분(모바일): 스웰 높이와 노멀은 정점에서만 계산하고(파장이 정점 간격보다 충분히 길어
//   보간해도 뭉개지지 않는다), 픽셀에서는 가까이서 보이는 잔물결 3개만 얹는다.
// - 라이팅은 TerrainBlend의 툰 셰이딩 없이 GetMainLight + SampleSH로만 심플하게 계산한다.
Shader "Mountains/WaterSurface"
{
    Properties
    {
        // 호수마다 ProceduralTerrainMesh가 MaterialPropertyBlock으로 넣는 마스크라 손으로 바꾸지 않는다.
        [HideInInspector][NoScaleOffset] _MaskTex ("Water Mask (baked, R=coverage G=depth)", 2D) = "black" {}
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.65, 0.65, 0.55)
        _DeepColor ("Deep Color", Color) = (0.08, 0.25, 0.35, 0.9)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1.0

        // 큰 파도(스웰). 정점 높이를 움직여 물가 선도 파도를 따라 오르내리게 한다.
        // Wave Scale은 기준 파의 파수(파장 = 2 x 3.14 / Wave Scale), Wave Speed는 기준 파의 각속도다.
        [Header(Waves)]
        _WaveScale ("Wave Scale (base wave frequency)", Float) = 1.2
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveHeight ("Wave Height (vertex, world units)", Range(0, 1)) = 0.06
        _WindDirection ("Wind Direction (degrees)", Range(0, 360)) = 30
        // 0이면 호수 전체가 같은 세기로 출렁이고, 올릴수록 잔잔한 곳과 출렁이는 곳이 갈리고
        // 마루선이 휘어진다. 그 분포는 바람 방향으로 천천히 흘러간다.
        _WaveChoppiness ("Wave Choppiness (variation)", Range(0, 1)) = 0.5

        // 가까이서 보이는 잔물결. 픽셀에서 노멀만 흔든다.
        [Header(Ripple Shimmer)]
        _WaveStrength ("Wave Normal Strength (detail ripple)", Range(0, 1)) = 0.15

        // 얕은 물가에서 스웰을 줄인다. 물가 선은 수심 0에서 생기므로 1까지 올리면 다시 수평선이 된다.
        [Header(Shore Waves)]
        _ShoreWaveDamping ("Shore Wave Damping", Range(0, 1)) = 0.3

        // 물에 드리운 그림자(나무 등)의 윤곽을 물결 노멀만큼 흔든다. 0이면 흔들지 않는다.
        [Header(Shadow Wobble)]
        _ShadowWobble ("Shadow Wobble (world units per normal tilt)", Range(0, 4)) = 1
        // 마스크 G(정규화 수심)를 월드 단위로 되돌리는 값. ProceduralTerrainMesh가 waterDepth로 채운다.
        [HideInInspector] _MaskDepthRange ("Mask Depth Range", Float) = 8

        [Header(Specular Highlight)]
        _SpecularPower ("Specular Power", Range(1, 256)) = 60
        _SpecularStrength ("Specular Strength", Range(0, 4)) = 1.5
        // 하이라이트에 잔물결을 얼마나 반영할지. 0이면 큰 파도 모양만 따라 해 반사가 한 덩어리로
        // 일렁이고, 1이면 잔물결마다 점처럼 반짝인다(예전 동작).
        _SpecularRipple ("Specular Ripple Amount", Range(0, 1)) = 0.25

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength (전체 배수)", Range(0, 1)) = 0.6
        _ReflectionBase ("Reflection When Looking Down (정면 반사량)", Range(0, 1)) = 0.35
        _ReflectionRoughness ("Reflection Roughness (0=거울, 1=흐릿)", Range(0, 1)) = 0.05

        [Header(Shore Foam)]
        _FoamWidth ("Foam Width (normalized depth)", Range(0, 1)) = 0.08
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // ZTest LEqual(기본값)을 명시적으로 남겨둔다 — 물가를 잘라내는 게 바로 이
            // 깊이 테스트라, 나중에 실수로 Always로 바꾸면 물이 지형을 뚫고 나온다.
            ZTest LEqual
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // Reflection Probe 및 Skybox 반사 바인딩을 위한 필수 Multi-compile 키워드
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _AmbientStrength;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _SpecularPower;
                float _SpecularStrength;
                float _ReflectionStrength;
                float _ReflectionBase;
                float _ReflectionRoughness;
                float _FoamWidth;
                float _FoamStrength;
                float _WaveHeight;
                float _MaskDepthRange;
                float _WindDirection;
                float _WaveChoppiness;
                float _ShoreWaveDamping;
                float _ShadowWobble;
                float _SpecularRipple;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1; // 스웰 기울기로 구한 노멀(정점에서 계산)
                float2 uv          : TEXCOORD2;
                // 이 자리 수면이 파도로 평균보다 얼마나 올라갔는지(월드 단위). 포말 위치를 같이 민다.
                float waveOffset   : TEXCOORD3;
                float fogFactor    : TEXCOORD4;
            };

            // 프로퍼티로 두던 값 중 따로 조절할 일이 거의 없어 고정한 것들(인스펙터 항목을 줄이려고).
            // 잔물결 파장 = 기준 파장 / 3 — 크기는 Wave Scale을 따라가고 세기는 Wave Normal Strength로 조절한다.
            static const float kDetailRippleScale = 3.0;
            // 이 수심(월드 단위)까지 물가 파도를 줄인다. 세기는 Shore Wave Damping 하나로 조절한다.
            static const float kShoreDampingDepth = 0.8;
            // 프레넬 곡선의 휨. 5는 Schlick 근사의 표준값이고, 반사량 자체는 Reflection Strength/Base로 정한다.
            static const float kFresnelPower = 5.0;

            // ---- 스웰(큰 파도) ----
            // 바람 방향 기준 방향 오차(도), 기준 파 대비 파수 배율, 초기 위상. 배율은 서로 딱
            // 떨어지지 않게 골라 합친 무늬가 반복되지 않게 한다(예전 3개는 비율이 비슷해 격자무늬가 됐다).
            #define SWELL_COUNT 6
            static const float kSwellAngle[SWELL_COUNT] = { 0.0, -27.0, 38.0, -61.0, 73.0, 14.0 };
            static const float kSwellFreq[SWELL_COUNT]  = { 1.0, 1.37, 0.73, 1.93, 1.61, 0.54 };
            static const float kSwellPhase[SWELL_COUNT] = { 0.0, 1.7, 4.1, 2.9, 5.3, 0.8 };

            // 시간을 이 각속도의 한 주기로 되감는다. _Time.y는 켜 둔 시간만큼 계속 커져서 몇 시간이
            // 지나면 sin 입력의 float 정밀도가 떨어져 물결이 뚝뚝 끊긴다. 한 주기로 되감으면 결과는 같다.
            float WrappedTime(float omega)
            {
                return fmod(_Time.y, TWO_PI / max(omega, 1e-4));
            }

            // 텍스처 없이 쓰는 값 노이즈. 격자 좌표를 289로 되감아 해시하므로 노이즈가 289칸 주기로
            // 반복된다 — 흘러가는 오프셋을 289칸마다 되감아도 끊기지 않게 하기 위해서다(시간 정밀도 대책).
            // 해시는 sin 대신 곱셈/frac만 쓴다(모바일 GPU에서 sin 해시는 정밀도가 들쭉날쭉하다).
            float Hash21(float2 p)
            {
                p -= 289.0 * floor(p / 289.0);
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 스웰의 높이(x)와 XZ 기울기(yz). 정점에서만 부른다.
            //  - 파장에 맞는 속도: 깊은 물 분산(각속도 ∝ sqrt(파수))을 따라 긴 파도는 빠르고 짧은
            //    파도는 느리다. 예전엔 속도 배율을 임의로 줘서 파들이 같이 행진하듯 보였다.
            //  - 긴 파도일수록 크다(진폭 ∝ 1/배율). 합이 1이 되게 나눠서 최고 높이는 amplitude다.
            //  - 노이즈 두 가지: 좌표를 비틀어(도메인 워핑) 마루선이 곧게 늘어서지 않게 하고, 세기를
            //    흔들어(gust) 잔잔한 곳과 출렁이는 곳을 만든다. 둘 다 바람 방향으로 천천히 흐른다.
            //  - 기울기는 워핑/세기 노이즈의 미분은 빼고 사인파 미분만 합친 근사다. 노이즈가 파장보다
            //    훨씬 크게 변해서 그 항은 눈에 띄지 않는다.
            float3 SampleSwell(float2 positionXZ, float amplitude)
            {
                float windRad = radians(_WindDirection);
                float2 wind = float2(cos(windRad), sin(windRad));
                float baseK = max(_WaveScale, 1e-4);

                // 노이즈 한 칸 ≈ 기준 파장의 2배. 흐르는 오프셋은 노이즈 주기(289칸)로 되감는다.
                float drift = frac(_Time.y * _WaveSpeed * 0.03 / 289.0) * 289.0;
                float2 np = positionXZ * baseK * 0.08 - wind * drift;
                float2 warp = float2(ValueNoise(np), ValueNoise(np + 17.3)) - 0.5;
                float2 p = positionXZ + warp * (TWO_PI / baseK) * _WaveChoppiness;
                float gust = lerp(1.0, 0.25 + 1.5 * ValueNoise(np * 0.6 + 31.7), _WaveChoppiness);

                float height = 0.0;
                float2 gradient = 0.0;
                float totalWeight = 0.0;
                [unroll]
                for (int i = 0; i < SWELL_COUNT; i++)
                {
                    float angle = windRad + radians(kSwellAngle[i]);
                    float2 dir = float2(cos(angle), sin(angle));
                    float k = baseK * kSwellFreq[i];
                    float omega = _WaveSpeed * sqrt(kSwellFreq[i]);
                    float phase = dot(dir, p) * k - omega * WrappedTime(omega) + kSwellPhase[i];

                    float s, c;
                    sincos(phase, s, c);
                    float weight = 1.0 / kSwellFreq[i];
                    height += weight * s;
                    gradient += weight * c * k * dir;
                    totalWeight += weight;
                }

                float scale = amplitude * gust / totalWeight;
                return float3(height * scale, gradient * scale);
            }

            // 정점을 월드 Y로 움직인다. 물가는 원래 "불투명 지형이 깊이 테스트로 물을 가려서"
            // 생기는 선이라, 수면이 오르내리면 그 선도 파도를 따라 들쭉날쭉 움직인다 — 물가를
            // 따로 계산할 필요 없이 depth-cutout 방식이 그대로 유지된다. 정점은
            // ProceduralTerrainMesh가 격자(waterSurfaceSegmentSize)로 깔아 둔다.
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // 얕은 물가에서는 스웰을 줄인다(수심은 마스크 G를 월드 단위로 되돌린 값).
                float depthWS = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, IN.uv, 0).g * _MaskDepthRange;
                float shore = smoothstep(0.0, kShoreDampingDepth, depthWS);
                float amplitude = _WaveHeight * lerp(1.0 - _ShoreWaveDamping, 1.0, shore);

                float3 swell = SampleSwell(positionWS.xz, amplitude);
                positionWS.y += swell.x;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
                // 높이장 y = h(x, z)의 노멀은 (-dh/dx, 1, -dh/dz)다. 반짝임이 실제 파도 모양을 따른다.
                OUT.normalWS = normalize(float3(-swell.y, 1.0, -swell.z));
                OUT.uv = IN.uv;
                OUT.waveOffset = swell.x;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            // 스웰 노멀 위에 가까이서 보이는 잔물결을 얹는다(픽셀 단계, 사인 3개). 세 파가 같은
            // 각속도를 써서 시간 되감기를 한 번만 한다 — 각속도가 서로 다르면 되감는 순간 위상이 튄다.
            float3 DetailRippleNormal(float2 positionXZ, float3 swellNormalWS)
            {
                float windRad = radians(_WindDirection);
                float2 d0 = float2(cos(windRad), sin(windRad));
                // 바람 방향을 +63도, -74도 돌린 방향(회전 행렬 상수).
                float2 d1 = float2(d0.x * 0.454 - d0.y * 0.891, d0.x * 0.891 + d0.y * 0.454);
                float2 d2 = float2(d0.x * 0.276 + d0.y * 0.961, -d0.x * 0.961 + d0.y * 0.276);

                float k = max(_WaveScale, 1e-4) * kDetailRippleScale;
                float omega = _WaveSpeed * sqrt(kDetailRippleScale);
                float phaseT = omega * WrappedTime(omega);
                float2 p = positionXZ * k;

                float w0 = sin(dot(d0, p) - phaseT);
                float w1 = sin(dot(d1, p) * 1.31 - phaseT + 2.1);
                float w2 = sin(dot(d2, p) * 0.83 - phaseT + 4.4);

                float2 tilt = (d0 * w0 + d1 * w1 + d2 * w2) * _WaveStrength;
                return normalize(swellNormalWS + float3(tilt.x, 0.0, tilt.y));
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // R = 폴리곤 안쪽인지(0 또는 1), G = 정규화된 수심(색 보간 + 포말 위치용).
                // R은 이진값이라 0.5를 문턱으로 자른다 — 텍스처 이중선형 필터링이
                // 만드는 1텍셀 폭 전이 구간을 가운데서 깔끔하게 나눈다.
                float2 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rg;
                clip(mask.r - 0.5);
                float depth = mask.g;

                float3 swellNormalWS = normalize(IN.normalWS);
                float3 normalWS = DetailRippleNormal(IN.positionWS.xz, swellNormalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // 그림자는 수면 위치에서 읽는데, 파도가 수면을 위아래로 0.1도 안 움직여서 윤곽이
                // 거의 안 변한다. 물결 노멀의 수평 성분만큼 조회 위치를 옆으로 밀어, 흔들리는 물에
                // 비친 것처럼 윤곽이 일렁이게 한다 — 그림자 샘플 수는 그대로라 비용이 늘지 않는다.
                float3 shadowPositionWS = IN.positionWS + float3(normalWS.x, 0.0, normalWS.z) * _ShadowWobble;
                float4 shadowCoord = TransformWorldToShadowCoord(shadowPositionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS) * _AmbientStrength;
                float3 lightTerm = mainLight.color * (NdotL * mainLight.shadowAttenuation) + ambient;

                float4 tint = lerp(_ShallowColor, _DeepColor, depth);
                float3 litColor = tint.rgb * lightTerm;

                // ---- 스페큘러 하이라이트 ----
                // 잔물결로 흔든 노멀 기준 Blinn-Phong. 그림자 속에서는 반짝이지 않게
                // shadowAttenuation도 곱한다.
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                // 잔물결까지 다 넣은 노멀은 최대 15도 넘게 기울어서, 날카로운 하이라이트(Power 256)면
                // 거의 모든 물결 어딘가가 해를 정면으로 받아 파도마다 점이 박혔다. 실제 윤슬은 해 쪽
                // 한 줄기로 모이고 큰 파도를 따라 일렁이므로, 하이라이트는 큰 파도 노멀 위주로 쓴다.
                float3 specNormalWS = normalize(lerp(swellNormalWS, normalWS, _SpecularRipple));
                float specTerm = pow(saturate(dot(specNormalWS, halfDirWS)), _SpecularPower) * _SpecularStrength;
                float3 specular = mainLight.color * specTerm * mainLight.shadowAttenuation;

                // ---- 환경 반사 ----
                // 반사색은 잔물결로 흔든 노멀 기준 반사 벡터로 환경을 샘플링해서 얻는다 —
                // 씬에 Reflection Probe가 있으면 그걸(주변 지형/나무), 없으면 스카이박스
                // 기반 전역 폴백(하늘)을 비춘다. 큐브맵 한 번 샘플링이라 모바일에서도 가볍다.
                //
                // 반사량은 프레넬(스치듯 볼수록 1에 가까움)에만 맡기지 않고 _ReflectionBase를
                // 바닥으로 깐다 — 프레넬만 쓰면 3인칭 카메라처럼 수면을 내려다보는 각도에서
                // 값이 1~2%까지 떨어져서 반사가 사실상 안 보인다.
                float fresnelCurve = pow(1.0 - saturate(dot(normalWS, viewDirWS)), kFresnelPower);
                float3 reflectVector = reflect(-viewDirWS, normalWS);
                float3 envColor = GlossyEnvironmentReflection(reflectVector, _ReflectionRoughness, 1.0);
                float reflectAmount = saturate(lerp(_ReflectionBase, 1.0, fresnelCurve) * _ReflectionStrength);

                // ---- 물가 포말 ----
                // 수심(depth)이 0에 가까운 물가일수록 흰 테두리를 얹는다.
                // 파도 마루에선 물이 그만큼 두꺼워진 것으로 보고 포말 띠를 물가 쪽으로 민다 —
                // 포말이 제자리에 고정돼 있으면 출렁이는 물가 선과 따로 놀아 보인다.
                float foamDepth = saturate(depth + IN.waveOffset / max(_MaskDepthRange, 1e-4));
                float foam = (1.0 - smoothstep(0.0, max(_FoamWidth, 1e-4), foamDepth)) * _FoamStrength;

                float3 finalColor = lerp(litColor, envColor, reflectAmount) + specular;
                finalColor = lerp(finalColor, 1.0, saturate(foam)); // 포말은 흰색

                // 씬 안개(Lighting > Environment > Fog). 직접 만든 셰이더는 안개를 자동으로 받지 않아서 MixFog를 직접 부른다.
                finalColor = MixFog(finalColor, IN.fogFactor);
                return half4(finalColor, tint.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}