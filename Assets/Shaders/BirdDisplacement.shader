Shader "Custom/URP_BirdDisplacement"
{
    // FishDisplacement.shader의 자매 셰이더(인스턴싱 + 정점 애니메이션). BirdFlock이
    // 프리팹에서 꺼낸 새 메시를 GPU 인스턴싱으로 그릴 때 쓴다 — 뼈대 애니메이션을 못
    // 쓰는 대신 날개를 몸통 기준 경첩처럼 위아래로 펄럭이게 흉내 낸다.
    //
    // 날갯짓 축(_SpanAxis/_FlapAxis/_BodyCenter/_BodyHalfWidth)은 BirdFlock이 메시 크기와
    // 프리팹 배치에서 계산해 넣는다 — 모델마다 축이 달라서 고정값을 쓸 수 없다.
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Wing Flap Animation)]
        _FlapSpeed("Flap Speed", Float) = 6.0
        _FlapAmplitude("Flap Amplitude", Range(0, 1)) = 0.4
        _PhaseSpread("Phase Spread (0 = 모두 같은 박자)", Range(0, 1)) = 1.0

        [Header(Wing Axes)]
        _SpanAxis("Span Axis (날개 폭 방향)", Vector) = (1, 0, 0, 0)
        _FlapAxis("Flap Axis (펄럭이는 방향)", Vector) = (0, 1, 0, 0)
        _BodyCenter("Body Center", Vector) = (0, 0, 0, 0)
        _BodyHalfWidth("Body Half Width", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // 저폴리 날개는 한 겹 평면인 경우가 많아 뒷면도 그린다(그림자 패스 포함).
        Cull Off

        // ForwardLit과 ShadowCaster가 함께 쓰는 부분. 그림자가 날갯짓과 어긋나지 않으려면
        // 두 패스가 완전히 같은 변위를 적용해야 한다.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _FlapSpeed;
            float _FlapAmplitude;
            float _PhaseSpread;
            float4 _SpanAxis;
            float4 _FlapAxis;
            float4 _BodyCenter;
            float _BodyHalfWidth;
        CBUFFER_END

        // 인스턴스마다 다른 위상 — 같은 박자로 펄럭이면 무리 전체가 한 마리처럼 보인다.
        float InstancePhase(uint instanceID)
        {
            return frac(instanceID * 0.6180339887) * _PhaseSpread;
        }

        // 날개는 몸통에 달린 경첩처럼 움직인다 — 몸통 폭(_BodyHalfWidth) 안쪽은 그대로 두고,
        // 거기서 멀어질수록(날개 끝으로 갈수록) 크게 흔든다. 거리에 비례하므로 모델 크기와
        // 무관하게 같은 각도로 펄럭인다.
        float3 ApplyWingFlap(float3 positionOS, float phase)
        {
            float along = dot(positionOS - _BodyCenter.xyz, _SpanAxis.xyz);
            float distance = max(abs(along) - _BodyHalfWidth, 0.0);
            float flap = sin(_Time.y * _FlapSpeed + phase * 6.2831853);
            return positionOS + _FlapAxis.xyz * (flap * _FlapAmplitude * distance);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            // RenderMeshInstanced가 넘기는 인스턴스별 변환 행렬을 읽으려면 필요하다
            // (없으면 전부 원점에 겹쳐 그려진다).
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                float fogFactor     : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float phase = 0.0;
            #if defined(UNITY_INSTANCING_ENABLED)
                phase = InstancePhase(UNITY_GET_INSTANCE_ID(input));
            #endif

                float3 positionWS = TransformObjectToWorld(ApplyWingFlap(input.positionOS.xyz, phase));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float3 normalWS = normalize(input.normalWS);
                // Cull Off라 뒷면은 노멀이 반대로 들어온다 — 시야 쪽으로 뒤집어 앞뒷면이
                // 똑같이 밝게 보이게 한다.
                if (dot(normalWS, normalize(input.viewDirWS)) < 0)
                {
                    normalWS = -normalWS;
                }

                // 원본 머티리얼(URP Lit)처럼 입체감이 보이도록 주광 + 환경광만 간단히 계산한다.
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction))
                    + SampleSH(normalWS);

                // 씬 안개(Lighting > Environment > Fog). 직접 만든 셰이더는 안개를 자동으로 받지 않아서 MixFog를 직접 부른다.
                return half4(MixFog(albedo.rgb * lighting, input.fogFactor), albedo.a);
            }
            ENDHLSL
        }

        // URP는 그림자맵을 그릴 때 "LightMode"="ShadowCaster" 패스를 찾는다 — 없으면
        // Renderer의 Shadow Casting Mode를 켜도 에러 없이 그림자가 안 생긴다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            #pragma multi_compile_instancing
            // 방향광(태양)과 점광/스폿은 그림자 방향 계산이 달라 URP가 변형을 고른다.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // ApplyShadowBias가 여기 있다 — Core.hlsl만으로는 안 딸려온다.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            // URP가 그림자 패스를 그릴 때마다 채워주는 전역값.
            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vertShadow(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float phase = 0.0;
            #if defined(UNITY_INSTANCING_ENABLED)
                phase = InstancePhase(UNITY_GET_INSTANCE_ID(input));
            #endif

                float3 positionWS = TransformObjectToWorld(ApplyWingFlap(input.positionOS.xyz, phase));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
