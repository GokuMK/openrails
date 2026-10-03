// SPIKE(linux): experiment only; reflects into MonoGame internals.
//
// MonoGame creates a separate SamplerState object for every sampler of every compiled shader. On DesktopGL a
// texture's sampler parameters are re-applied (about ten glTexParameter calls) whenever it is used with a different
// SamplerState object, so switching between techniques of one effect re-applies identical settings for every bound
// texture. This replaces identical sampler states in Open Rails' effects with one shared instance, to measure the
// cost. The proper fix belongs in MonoGame (share states when loading effects, or compare contents).
// Set ORTS_SPIKE_NO_SAMPLER_SHARING=1 to disable for A/B measurements.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Viewer3D
{
    static class SamplerStateSharing
    {
        static readonly Dictionary<string, SamplerState> Shared = new Dictionary<string, SamplerState>();
        static readonly FieldInfo VertexShaderField = typeof(EffectPass).GetField("_vertexShader", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PixelShaderField = typeof(EffectPass).GetField("_pixelShader", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Share(Effect effect)
        {
            if (Environment.GetEnvironmentVariable("ORTS_SPIKE_NO_SAMPLER_SHARING") == "1" || VertexShaderField == null || PixelShaderField == null)
                return;

            int replaced = 0, total = 0;
            foreach (var technique in effect.Techniques)
            {
                foreach (var pass in technique.Passes)
                {
                    foreach (var field in new[] { VertexShaderField, PixelShaderField })
                    {
                        var shader = field.GetValue(pass);
                        var samplers = shader?.GetType().GetProperty("Samplers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(shader) as Array;
                        if (samplers == null)
                            continue;
                        for (var i = 0; i < samplers.Length; i++)
                        {
                            var info = samplers.GetValue(i);
                            var stateField = info.GetType().GetField("state");
                            var state = (SamplerState)stateField.GetValue(info);
                            if (state == null)
                                continue;
                            total++;
                            var key = $"{state.AddressU}|{state.AddressV}|{state.AddressW}|{state.BorderColor.PackedValue}|{state.Filter}|{state.FilterMode}|{state.ComparisonFunction}|{state.MaxAnisotropy}|{state.MaxMipLevel}|{state.MipMapLevelOfDetailBias}";
                            if (!Shared.TryGetValue(key, out var shared))
                            {
                                Shared.Add(key, state);
                                continue;
                            }
                            if (ReferenceEquals(shared, state))
                                continue;
                            stateField.SetValue(info, shared);
                            samplers.SetValue(info, i);
                            replaced++;
                        }
                    }
                }
            }
            Trace.WriteLine($"SPIKE sampler sharing: {effect.GetType().Name}: {replaced} of {total} sampler states replaced; {Shared.Count} distinct overall");
        }
    }
}
