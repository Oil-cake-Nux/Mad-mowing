Shader "Custom/InfiniteTile"
{
    Properties
    {

        // 是否启用“避免纹理重复”的算法（0=关闭，1=开启）
        // 开启后会使用噪声纹理选取不同偏移来打散重复感
        [MaterialToggle] _AvoidRepitition ("Avoid Texture Repitition", Int) = 1

        // 主纹理
        _MainTex ("Texture", 2D) = "white" {}

        // 噪声纹理
        // 注意：它不是噪声函数，而是一张低频噪声贴图
        _NoiseTex ("Texture", 2D) = "white" {}

        // 平铺缩放：uv 的缩放因子（越大，纹理看起来越“密”/更小块）
        _Scale ("Scale", Float) = 1

        // 颜色叠乘：最终输出颜色会乘以 Tint（可用于整体变色/变暗/透明度等）
        _Tint ("Tint", Color) = (1, 1, 1, 1)

        // 冲击波（ripple/shockwave）半径参数：
        // 一般由脚本驱动（例如 InfiniteBackground.Shockwave(...)）
        // >0 时开启冲击波位移效果
        _Shockwave ("Ripple", Float) = 1

        // 冲击波“带宽”：d 在 [radius-width, radius+width] 区间内才会产生位移
        _ShockwaveWidth ("Ripple Width", Float) = 0.2

        // 冲击波强度：位移幅度缩放
        _ShockwaveIntensity ("Ripple Intensity", Float) = 1

        // 重置偏移：用于“无限背景重置/平移”时的偏移量（通常脚本写入）
        _ResetOffset ("Reset Offset", Vector) = (0,0,0,0)

        // 临时重置偏移：配合 ResetBlend 做平滑过渡
        _TempResetOffset ("Temp Reset Offset", Vector) = (0,0,0,0)

        // 重置混合系数：0~1
        // 0：完全使用当前偏移计算出的纹理
        // 1：完全使用“重置后偏移”计算出的纹理
        _ResetBlend ("Reset Blend", Float) = 0

        // 是否处于重置过渡中（0=否，1=是）
        [MaterialToggle] _Resetting ("Resetting", Int) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

 
            struct appdata
            {
                half4 vertex : POSITION;  // 顶点位置（模型空间）
                half2 uv : TEXCOORD0;     // 模型自带 uv（但本 shader 实际主要用 worldPosition 来做“世界坐标平铺”）
            };


            struct v2f
            {
                half2 uv : TEXCOORD0;             // 传递 UV（这里会用 TRANSFORM_TEX 乘上 _MainTex_ST）
                half2 worldPosition : TEXCOORD1;  // 顶点的世界坐标 (x,y)：用于实现“无限背景/世界坐标平铺”
                half4 vertex : SV_POSITION;       // 裁剪空间坐标（用于渲染）
            };

            // ===========================
            // Uniform/材质参数（Properties 对应）
            // ===========================
            int _AvoidRepitition;
            sampler2D _MainTex;
            sampler2D _NoiseTex;
            half4 _MainTex_ST;          // Unity 自动注入：_MainTex 的 Tiling/Offset（Inspector 的材质贴图参数）
            half _Scale;
            half4 _Tint;

            // 冲击波参数（由脚本动态更新）
            half _Shockwave;
            half _ShockwaveWidth;
            half _ShockwaveIntensity;
            half2 _PlayerPosition;      // 玩家世界坐标（用于计算冲击波中心距离 d）

            // 重置/过渡参数（用于平滑“背景偏移重置”）
            half _ResetBlend;
            int _Resetting;
            half2 _ResetOffset;
            half2 _TempResetOffset;

            //传 uv（尽管片元主要使用 worldPosition 推导 uv）
            v2f vert (appdata_full v)
            {
                v2f o;

                // 把模型空间顶点乘以 objectToWorld，得到世界坐标
                // 只取 xy：因为背景通常在 2D 平面上铺
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xy;

                // Unity 提供的帮助函数：模型空间->裁剪空间
                o.vertex = UnityObjectToClipPos(v.vertex);

                // 处理贴图 tiling/offset（如果材质面板有改 tiling/offset）
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            // 辅助函数：sum(half3) 返回三通道之和
            // 在后面用于“根据两次采样颜色差异微调 smoothstep”，减少可见接缝
            half sum(half3 v) { return v.x+v.y+v.z; }


            fixed4 frag (v2f i) : SV_Target
            {
                // 1) 以世界坐标生成 uv，实现“无限平铺”（不依赖模型本身 uv）
                //    uv = (worldPos + resetOffset) / scale
                half2 uv = (i.worldPosition + _ResetOffset)*1/_Scale;

                // =========================================================
                // 2) 冲击波（Shockwave/Ripple）效果：在特定半径带内扭曲 uv
                //    思路：
                //    - 计算像素点到玩家中心的距离 d
                //    - 若 d 落在 [radius-width, radius+width] 区间
                //      则沿着“从原点指向该点”的方向反向偏移 uv
                //    视觉上形成一圈扩散的涟漪/冲击波
                // =========================================================
                if (_Shockwave > 0)
                {
                    half d = length(i.worldPosition - _PlayerPosition);

                    // 只在冲击波带宽范围内产生扭曲
                    if (d > _Shockwave-_ShockwaveWidth && d < _Shockwave+_ShockwaveWidth)
                    {
                        // d的值越接近_Shockwave，UV坐标位移越大；
                        // 越靠近带宽边界位移越小
                        // normalize(i.worldPosition) 表示一个方向向量（这里以世界原点为参考方向）
                        // * _ShockwaveIntensity 控制强度
                        uv.xy -= normalize(i.worldPosition) * (_ShockwaveWidth-abs(_Shockwave - d)) *  _ShockwaveIntensity;
                    }
                }

                fixed4 col = 0;

                // =========================================================
                // 3) 纹理反重复（核心算法）：
                //    用一张低频噪声纹理来选择“8 种偏移方案”中的两种，
                //    然后在两种采样结果间做插值，从而打散肉眼可见的平铺重复。
                //
                //    关键点：
                //    - k = noise(0.005 * uv)：低频噪声（0~1）
                //    - l = k * 8：映射到 0~8，得到 8 个区间
                //    - ia=floor(l), ib=ia+1：取相邻两段
                //    - offa/offb：用 sin(...) 生成“伪随机偏移”
                //    - 用 ddx/ddy 传入采样函数，确保 mipmap/各向异性更正确
                // =========================================================
                if (_AvoidRepitition)
                {
                    // 从噪声纹理取低频随机值（只取 x 通道即可）
                    // 0.005*uv：极低频，让同一大片区域共享相近 k（避免高频噪声产生颗粒感）
                    half k = tex2D( _NoiseTex, 0.005*uv ).x;
        
                    //ddx 和 ddy 是 Shader（CG/HLSL）中的像素导数函数（也叫屏幕空间导数函数），用于在像素着色器中计算某个变量在屏幕空间上的变化率（梯度）。
                    //计算变量在 屏幕水平方向（X 轴，从左到右）的像素间差值（变化率）
                    half2 duvdx = ddx( uv );
                    half2 duvdy = ddy( uv );
                    
                    // 将 k 映射到 0~8，分成 8 个“偏移段”
                    half l = k*8.0;

                    // ia：当前段索引（整数）
                    half ia = floor(l);

                    // f：当前段内的小数部分（0~1）
                    half f = l-ia;

                    // ib：下一段索引（用于插值）
                    half ib = ia + 1.0;
                    
                    ///以下四句代码的作用就是为了打散“同一张纹理平铺后周期性重复”的视觉规律
                    // 用 sin 生成两个“伪随机”偏移（随 ia/ib 改变）
                    // 这里的 3.0,7.0 是人为挑选的系数，用来打散相关性
                    half2 offa = sin(half2(3.0,7.0)*ia);
                    half2 offb = sin(half2(3.0,7.0)*ib);

                    // 分别采样两次主纹理（uv + offa / uv + offb）
                    // 使用带导数的 tex2D 版本：比普通版本更加精确
                    half3 cola = tex2D( _MainTex, uv + offa, duvdx, duvdy ).xyz;
                    half3 colb = tex2D( _MainTex, uv + offb, duvdx, duvdy ).xyz;
                    
                    // 在两次采样之间插值：
                    // smoothstep(0.2,0.8,...)：避免硬切换产生明显断层
                    // f-0.1*sum(cola-colb)：根据两采样颜色差异稍微偏移插值位置，减少“接缝感”
                    col.xyz = lerp( cola, colb, smoothstep(0.2,0.8,f-0.1*sum(cola-colb)) );


                    // 4) Resetting 逻辑：
                    //    当背景需要“重置 offset/过渡”时：
                    //    - 再用 “_ResetOffset + _TempResetOffset” 计算一份新的 uv
                    //    - 再做一遍反重复采样得到新的 col
                    //    - 最后用 _ResetBlend 在旧 col 与新 col 之间混合
                    //
                    //    目的：平滑切换背景偏移，避免瞬间跳变导致纹理突变
                    if (_Resetting)
                    {
                        // 使用额外的临时重置偏移，生成“重置目标 uv”
                        uv = (i.worldPosition + _ResetOffset + _TempResetOffset)*1/_Scale;

                        // 重复一遍“噪声选择偏移 + 两次采样插值”的流程
                        k = tex2D( _NoiseTex, 0.005*uv ).x;
                        // 计算uv坐标在屏幕水平方向的像素差值（右侧像素uv - 当前像素uv）
                        duvdx = ddx( uv );
                        duvdy = ddy( uv );
                        
                        l = k*8.0;
                        ia = floor(l);
                        f = l-ia;
                        ib = ia + 1.0;
                        
                        offa = sin(half2(3.0,7.0)*ia);
                        offb = sin(half2(3.0,7.0)*ib);

                        cola = tex2D( _MainTex, uv + offa, duvdx, duvdy ).xyz;
                        colb = tex2D( _MainTex, uv + offb, duvdx, duvdy ).xyz;

                        // _ResetBlend：在“当前结果 col”和“重置目标结果”之间混合
                        // _ResetBlend=0 -> 保持原 col
                        // _ResetBlend=1 -> 完全切到重置目标纹理
                        col.xyz = lerp(col.xyz, lerp( cola, colb, smoothstep(0.2,0.8,f-0.1*sum(cola-colb)) ), _ResetBlend);
                    }
                }
                else
                {
                    // 关闭反重复：直接按 uv 采样主纹理（最普通的平铺，会明显重复）
                    col = tex2D(_MainTex, uv);
                }

                // 最终输出：叠乘 Tint（可整体调色/透明度等）
                return col*_Tint;
            }
            ENDCG
        }
    }
}
