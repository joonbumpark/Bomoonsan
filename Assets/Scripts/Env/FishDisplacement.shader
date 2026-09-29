Shader "Custom/URP_FishDisplacement"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.1, 0.4, 0.8, 1.0)

        [Header(Fresnel Settings)]
        _FresnelColor("Fresnel Color", Color) = (0.5, 0.9, 1.0, 1.0)
        _FresnelPower("Fresnel Power (Lower = Stronger)", Range(0.5, 5.0)) = 1.5
        _FresnelIntensity("Fresnel Intensity", Range(0.0, 5.0)) = 2.0

        [Header(Fish Wave Animation)]
        _WaveSpeed("Swim Speed", Float) = 8.0
        _WaveFrequency("Wave Frequency", Float) = 2.0
        _WaveAmplitude("Wave Amplitude", Range(0, 0.5)) = 0.12
        _HeadOffset("Head Offset (몸통 축 기준 문턱)", Float) = -0.2
        _PhaseSpread("Phase Spread (0 = 모두 같은 박자)", Range(0, 1)) = 1.0

        [Header(Model Axes)]
        _LengthAxis("Length Axis (머리에서 꼬리 방향)", Vector) = (0, 0, 1, 0)
        _SideAxis("Side Axis (꼬리가 흔들리는 방향)", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ForwardLit과 ShadowCaster가 함께 쓰는 부분. 그림자 모양이 헤엄치는 자세와
        // 어긋나지 않으려면 두 패스가 완전히 같은 꼬리 파동을 적용해야 한다.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _FresnelColor;
            half _FresnelPower;
            half _FresnelIntensity;
            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;
            float _HeadOffset;
            float _PhaseSpread;
            float4 _LengthAxis;
            float4 _SideAxis;
        CBUFFER_END

        // 인스턴스마다 다른 위상 — 같은 박자로 꼬리를 흔들면 무리 전체가 한 마리처럼
        // 보인다. 황금비를 곱해 번호가 이웃해도 위상이 흩어지게 한다.
        float InstancePhase(uint instanceID)
        {
            return frac(instanceID * 0.6180339887) * _PhaseSpread;
        }

        // 파동은 오브젝트 공간(몸통 기준)에서 만든다. 월드 공간으로 계산하면 물고기가
        // 방향을 틀 때 파동 축이 몸과 어긋나고, 이동하는 것만으로 위상이 변해 꼬리가
        // 제멋대로 떨린다. 길이/좌우 축을 프로퍼티로 받는 이유는 FBX마다 몸이 누운 축이
        // 달라서다 — 길이 축을 잘못 잡으면 꼬리가 아니라 몸 전체가 앞뒤로 늘었다 줄었다 한다.
        float3 ApplyTailWave(float3 positionOS, float phase)
        {
            float3 lengthAxis = normalize(_LengthAxis.xyz);
            float3 sideAxis = normalize(_SideAxis.xyz);

            float along = dot(positionOS, lengthAxis);
            float mask = saturate((_HeadOffset - along) * 1.5);
            float wave = sin(_Time.y * _WaveSpeed
                + along * _WaveFrequency
                + phase * 6.2831853) * _WaveAmplitude * mask;

            return positionOS + sideAxis * wave;
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

            // GPU 인스턴싱 변형을 컴파일한다. 이게 없으면 RenderMeshInstanced로 넘긴
            // 물고기가 인스턴스별 변환 행렬을 못 읽어 전부 같은 자리에 겹쳐 그려진다.
            #pragma multi_compile_instancing

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

                float3 positionWS = TransformObjectToWorld(ApplyTailWave(input.positionOS.xyz, phase));

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
                float3 viewDirWS = normalize(input.viewDirWS);

                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // 씬 안개(Lighting > Environment > Fog). 직접 만든 셰이더는 안개를 자동으로 받지 않아서 MixFog를 직접 부른다.
                return half4(MixFog(albedo.rgb + fresnel * _FresnelColor.rgb, input.fogFactor), albedo.a);
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
            Cull Back

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

                float3 positionWS = TransformObjectToWorld(ApplyTailWave(input.positionOS.xyz, phase));
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
