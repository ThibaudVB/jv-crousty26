using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Linq;
public static class NightUrpShaderProbe {
 public static void Run() {
  var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
  var pipeline=UniversalRenderPipelineAsset.Create(renderer);
  GraphicsSettings.defaultRenderPipeline=pipeline;
  Shader.globalRenderPipeline="UniversalPipeline";
  ShaderUtil.allowAsyncCompilation=false;
  foreach(var name in new[]{"CityGlow","Atmosphere"}) {
   var shader=Resources.Load<Material>("Crousty/"+name).shader;
   var material=new Material(shader);
   if(name=="CityGlow" && material.GetTag("RenderPipeline",false)!="UniversalPipeline") throw new System.Exception("URP subshader not selected");
   ShaderUtil.CompilePass(material,0,true);
   var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message).ToArray();
   if(errors.Length>0) { File.WriteAllText("urp-glow-result.txt","FAILED "+name+" "+string.Join("\n",errors)); EditorApplication.Exit(1); return; }
  }
  File.WriteAllText("urp-glow-result.txt","PASSED URP CityGlow and Atmosphere shader compilation");
 }
}

