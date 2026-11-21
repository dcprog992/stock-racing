using UnityEngine;

public static class GameGraphics
{
    public const string Key="Speedway.Graphics";
    public static int Level=>Mathf.Clamp(PlayerPrefs.GetInt(Key,1),0,2);
    public static string Name(int level)=>level==0?"МИНИМУМ":level==2?"УЛЬТРА":"СРЕДНЕ";
    public static void Set(int level){PlayerPrefs.SetInt(Key,Mathf.Clamp(level,0,2));PlayerPrefs.Save();Apply();}
    public static void Apply()
    {
        int q=Level;
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        QualitySettings.antiAliasing=q==0?0:q==1?2:4;
        QualitySettings.shadows=q==0?ShadowQuality.Disable:ShadowQuality.All;
        QualitySettings.shadowResolution=q==2?ShadowResolution.High:ShadowResolution.Medium;
        QualitySettings.shadowDistance=q==0?0:q==1?55:110;
        QualitySettings.shadowCascades=q==2?4:2;
        QualitySettings.pixelLightCount=q==0?0:q==1?1:3;
        QualitySettings.lodBias=q==0?.45f:q==1?.8f:1.5f;
        QualitySettings.globalTextureMipmapLimit=q==0?2:q==1?1:0;
        QualitySettings.realtimeReflectionProbes=q==2;
        foreach(var cam in Object.FindObjectsByType<Camera>())
        {
            cam.depthTextureMode=q==2?DepthTextureMode.Depth:DepthTextureMode.None;cam.allowHDR=q==2;cam.allowMSAA=q>0;
            if(cam.CompareTag("MainCamera"))cam.farClipPlane=q==0?900:q==1?1400:2200;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        int width=q==0?1280:q==1?1664:2340;
        var native=Screen.currentResolution;
        int longEdge=Mathf.Max(native.width,native.height),shortEdge=Mathf.Min(native.width,native.height);
        width=Mathf.Min(width,longEdge);
        if(longEdge>0)Screen.SetResolution(width,Mathf.RoundToInt(width*(float)shortEdge/longEdge),FullScreenMode.FullScreenWindow);
#endif
    }
}
