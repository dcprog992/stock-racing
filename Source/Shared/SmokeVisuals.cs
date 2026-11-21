using UnityEngine;

public static class SmokeVisuals
{
    static Material material;
    public static Material Material(bool detailed)
    {
        if(material)return material;
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Classic smoke"};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            var uv=new Vector2(x,y)/63f;float radius=(uv-Vector2.one*.5f).magnitude*2;
            float noise=Mathf.PerlinNoise(uv.x*6+11,uv.y*6+8);
            float alpha=Mathf.Pow(Mathf.Clamp01(1-radius+(noise-.5f)*.34f),1.6f);
            texture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        texture.Apply(false,true);material=new Material(Shader.Find("Particles/Standard Unlit")){name="Classic smoke",mainTexture=texture,renderQueue=3000};
        material.SetFloat("_Mode",2);material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHABLEND_ON");return material;
    }
    public static void Configure(ParticleSystem particles,bool engine)
    {
        var main=particles.main;main.maxParticles=140;main.startLifetime=new ParticleSystem.MinMaxCurve(.65f,1.45f);
        main.startSize=new ParticleSystem.MinMaxCurve(.38f,.73f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=engine?new Color(.27f,.28f,.29f,.45f):new Color(.83f,.86f,.87f,.45f);
        var noise=particles.noise;noise.enabled=false;var rotation=particles.rotationOverLifetime;rotation.enabled=false;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Material(false);
    }
}
