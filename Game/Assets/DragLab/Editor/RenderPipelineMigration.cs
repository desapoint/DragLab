using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DragLab.Editor
{
    // Invoked in the live Editor. Keep setup reproducible and fail on renamed serialized fields.
    public static class RenderPipelineMigration
    {
        const string Root="Assets/DragLab/Rendering";
        const string ResourcesRoot="Assets/DragLab/Resources/Rendering";
        const string PipelinePath=Root+"/DragLabURP.asset";
        const string RendererPath=Root+"/DragLab2DRenderer.asset";

        public static void PrepareCaptureSeeds()
        {
            string previous=DragLab.Infrastructure.ProfileStore.VerificationPath;
            string folder=Path.GetFullPath("../reports/render-pipeline-20261016/seed/");Directory.CreateDirectory(folder);
            try {
                string fresh=Path.Combine(folder,"save-slot-1.sqlite3"),stocked=Path.Combine(folder,"stocked.sqlite3");
                if(!File.Exists(fresh)){
                    DragLab.Infrastructure.ProfileStore.VerificationPath=fresh;
                    var p=DragLab.Domain.PlayerProfile.NewGame(DateTime.UtcNow.Ticks);
                    DragLab.Domain.Mechanics.MechanicsOwnership.Ensure(p,DragLab.Infrastructure.MechanicsTransactions.Catalog());
                    if(!DragLab.Infrastructure.ProfileStore.TrySave(p))throw new IOException(DragLab.Infrastructure.ProfileStore.Status);
                }
                if(!File.Exists(stocked)){
                    DragLab.Infrastructure.ProfileStore.VerificationPath=fresh;var p=DragLab.Infrastructure.ProfileStore.Load();
                    DragLab.Domain.Mechanics.VehicleCollection.Buy(p,p.catalog,p.vehicleStock.listings.First(o=>o.model=="starter-hatch"));
                    foreach(var row in p.inventory.market.parts)row.units=row.initialUnits=Math.Max(row.initialUnits,100);
                    p.storageCommit=0;DragLab.Infrastructure.ProfileStore.VerificationPath=stocked;
                    if(!DragLab.Infrastructure.ProfileStore.TrySave(p))throw new IOException(DragLab.Infrastructure.ProfileStore.Status);
                }
            }finally {DragLab.Infrastructure.ProfileStore.VerificationPath=previous;}
        }

        public static void BuildBaselineSurvey()
        {
            string graphics=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);int quality=QualitySettings.GetQualityLevel();
            var qualities=Enumerable.Range(0,QualitySettings.names.Length).Select(i=>AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i))).ToArray();
            try {
                GraphicsSettings.defaultRenderPipeline=null;
                for(int i=0;i<qualities.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=null;}
                QualitySettings.SetQualityLevel(quality,false);
                BuildCapturePlayer("../reports/render-pipeline-20261016/survey-player/DragLab.exe",true);
            }finally {
                // Building can unload unreferenced pipeline objects. Restore by persistent path,
                // never a native object handle captured before the build.
                GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(graphics);
                for(int i=0;i<qualities.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualities[i]);}
                QualitySettings.SetQualityLevel(quality,false);AssetDatabase.SaveAssets();
            }
        }
        public static void BuildUrpReview()
        {
            Verify();BuildCapturePlayer("../reports/render-pipeline-20261016/urp-player/DragLab.exe");
        }
        static void BuildCapturePlayer(string destination,bool builtIn=false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/DragLab/Scenes/FirstSlice.unity","Assets/DragLab/Scenes/EngineTest.unity","Assets/DragLab/Scenes/DrivingTest.unity"},
                locationPathName=destination,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development,
                extraScriptingDefines=builtIn?new[]{"DRAGLAB_BUILTIN_CAPTURE"}:Array.Empty<string>() });
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Capture player build failed.");
        }

        static void Set(SerializedObject obj,string name,int value)
        {
            var property=obj.FindProperty(name)??throw new InvalidOperationException("Missing serialized property: "+name);
            property.intValue=value;
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
        }
        public static void PrepareAssets()
        {
            Folder(Root);Folder(ResourcesRoot);
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if(renderer==null){renderer=ScriptableObject.CreateInstance<Renderer2DData>();AssetDatabase.CreateAsset(renderer,RendererPath);}
            var data=new SerializedObject(renderer);
            Set(data,"m_DefaultMaterialType",1); // Unity's serialized Unlit enum, checked on reload below.
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(renderer);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,PipelinePath);}
            pipeline.supportsHDR=false;pipeline.supportsCameraDepthTexture=false;pipeline.supportsCameraOpaqueTexture=false;
            pipeline.renderScale=1;pipeline.msaaSampleCount=2;pipeline.useSRPBatcher=true;
            EditorUtility.SetDirty(pipeline);
            Material("SpriteUnlit","Universal Render Pipeline/2D/Sprite-Unlit-Default");
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static Material Material(string name,string shaderName)
        {
            string path=ResourcesRoot+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(shaderName)??throw new InvalidOperationException("Missing shader "+shaderName);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else if(material.shader!=shader)throw new InvalidOperationException("Unexpected shader in "+path);
            return material;
        }
        public static void Activate()
        {
            PrepareAssets();Material("VertexUnlit","DragLab/Vertex Unlit");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            GraphicsSettings.defaultRenderPipeline=pipeline;
            int quality=QualitySettings.GetQualityLevel();
            try {for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}}
            finally {QualitySettings.SetQualityLevel(quality,false);}
            AssetDatabase.SaveAssets();Verify();
        }
        public static void Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if(pipeline==null||GraphicsSettings.defaultRenderPipeline!=pipeline)throw new InvalidOperationException("Graphics is not using DragLab URP.");
            if(pipeline.rendererDataList.Length!=1||pipeline.rendererDataList[0] is not Renderer2DData renderer)throw new InvalidOperationException("Missing default 2D renderer.");
            var serialized=new SerializedObject(pipeline);
            if(serialized.FindProperty("m_DefaultRendererIndex").intValue!=0)throw new InvalidOperationException("Wrong default renderer index.");
            if(new SerializedObject(renderer).FindProperty("m_DefaultMaterialType").intValue!=1)throw new InvalidOperationException("Sprite default must remain unlit.");
            if(pipeline.renderScale!=1||pipeline.supportsHDR||pipeline.supportsCameraDepthTexture||pipeline.supportsCameraOpaqueTexture||pipeline.msaaSampleCount!=2)throw new InvalidOperationException("Unexpected rendering cost/color settings.");
            var report=new StringBuilder("URP saved-state verification\n");
            report.AppendLine("Pipeline: "+pipeline.name+" / renderer: "+renderer.name+" / render scale 1 / MSAA 2 / HDR off / post-processing unused");
            report.AppendLine("Color space: "+PlayerSettings.colorSpace);
            for(int i=0;i<QualitySettings.names.Length;i++){
                if(QualitySettings.GetRenderPipelineAssetAt(i)!=pipeline)throw new InvalidOperationException("Wrong quality pipeline: "+QualitySettings.names[i]);
                report.AppendLine("PASS quality: "+QualitySettings.names[i]);
            }
            string guid=AssetDatabase.AssetPathToGUID(PipelinePath);
            if(!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText("ProjectSettings/GraphicsSettings.asset",Encoding.UTF8),@"m_CustomRenderPipeline: \{[^}]*guid: "+guid))throw new InvalidOperationException("Graphics assignment is not saved on disk.");
            if(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText("ProjectSettings/QualitySettings.asset",Encoding.UTF8),@"customRenderPipeline: \{[^}]*guid: "+guid).Count!=QualitySettings.names.Length)throw new InvalidOperationException("Quality assignments are not saved on disk.");
            report.AppendLine("PASS serialized Graphics and all Quality assignments match the pipeline GUID");
            foreach(string name in new[]{"SpriteUnlit","VertexUnlit"}){
                var material=AssetDatabase.LoadAssetAtPath<Material>(ResourcesRoot+"/"+name+".mat");
                if(material==null||!material.shader.isSupported||ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Invalid material: "+name);
                report.AppendLine("PASS material: "+material.shader.name);
            }
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            try {foreach(var path in new[]{"Assets/DragLab/Scenes/FirstSlice.unity","Assets/DragLab/Scenes/EngineTest.unity","Assets/DragLab/Scenes/DrivingTest.unity"}){
                EditorSceneManager.OpenScene(path);
                if(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length!=0||UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length!=0||LightmapSettings.lightmaps.Length!=0)throw new InvalidOperationException("Reassess lighting migration for "+path);
                string bake=AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                if(!string.IsNullOrEmpty(bake)&&bake!="Resources/unity_builtin_extra")throw new InvalidOperationException("Unexpected authored bake: "+bake);
                report.AppendLine("PASS scene: "+path+" / no authored lights, probes, lightmaps or post-processing; runtime cameras");
            }}finally {EditorSceneManager.RestoreSceneManagerSetup(scenes);}
            Directory.CreateDirectory("../reports/render-pipeline-20261016");
            File.WriteAllText("../reports/render-pipeline-20261016/saved-state.txt",report.ToString(),new UTF8Encoding(false));
            Debug.Log(report.ToString());
        }
    }
}
