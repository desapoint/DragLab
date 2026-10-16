using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DragLab.Domain;
using DragLab.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class VerifyFinishSegments
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Capture(string phase)
    {
        if(phase!="before"&&phase!="after")throw new ArgumentException("before or after required");
        string folder=Path.GetFullPath("../reports/finish-segments-20261016/"+phase);Directory.CreateDirectory(folder);
        var obj=new GameObject("Isolated finish digits");obj.layer=31;
        var cameraObj=new GameObject("Isolated finish camera");var camera=cameraObj.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=6.4f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.04f);camera.cullingMask=1<<31;camera.transform.position=new Vector3(0,0,-10);camera.AdditionalCameraData().SetRenderer(0);
        var board=obj.AddComponent<FinishBillboard>();board.Initialize();board.transform.localPosition=Vector3.zero;board.transform.localScale=Vector3.one*1.17f;
        var type=typeof(FinishBillboard);var material=obj.GetComponent<MeshRenderer>().sharedMaterial;
        try
        {
            type.GetProperty("Player").SetValue(board,new FinishReading(new CarState{finished=true,elapsed=18.871,reaction=.183}));
            type.GetProperty("Opponent").SetValue(board,new FinishReading(new CarState{finished=true,elapsed=16.988,reaction=8.183}));
            type.GetMethod("Draw",Private).Invoke(board,null);
            foreach(int width in new[]{1280,1920,3840})Render(camera,width,Path.Combine(folder,"board-"+width+".png"));
            var vertices=(List<Vector3>)type.GetField("vertices",Private).GetValue(board);var colors=(List<Color>)type.GetField("colors",Private).GetValue(board);var triangles=(List<int>)type.GetField("triangles",Private).GetValue(board);
            vertices.Clear();colors.Clear();triangles.Clear();var number=type.GetMethod("Number",Private);
            for(int i=0;i<10;i++){
                number.Invoke(board,new object[]{i.ToString(),-3f+i*.6f,.1f,.34f,.63f,1});
                number.Invoke(board,new object[]{i.ToString(),-3f+i*.6f,-.9f,.29f,.46f,1});
            }
            var mesh=obj.GetComponent<MeshFilter>().sharedMesh;mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            foreach(int width in new[]{1280,1920,3840})Render(camera,width,Path.Combine(folder,"digits-"+width+".png"));
            return folder;
        }
        finally{UnityEngine.Object.DestroyImmediate(obj.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(cameraObj);}
    }
    static UniversalAdditionalCameraData AdditionalCameraData(this Camera camera)=>camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
    static void Render(Camera camera,int width,string path)
    {
        int height=width*9/16;var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;Texture2D image=null;
        try{
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
            image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);}
    }
}
