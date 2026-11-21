using UnityEngine;
using UnityEngine.UI;
public sealed class StartSignal:MonoBehaviour
{
    RectTransform panel;readonly RawImage[] lamps=new RawImage[20];AudioSource sound;AudioClip yellow,green;int phase=-1;float reveal;
    public int Phase=>phase;public float Reveal=>reveal;
    public void Build(Transform parent)
    {
        transform.SetParent(parent,false);panel=gameObject.AddComponent<RectTransform>();panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,1);panel.sizeDelta=new Vector2(465,295);panel.localScale=Vector3.one*.55f;
        var atlas=Resources.Load<Texture2D>("StartLightsAtlas");
        for(int column=0;column<5;column++)for(int row=0;row<4;row++)
        {
            var go=new GameObject("Start lamp "+column+" / "+row,typeof(RectTransform),typeof(RawImage));var rect=go.GetComponent<RectTransform>();rect.SetParent(panel,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(column*94,-row*73);rect.sizeDelta=new Vector2(80,73);
            var image=go.GetComponent<RawImage>();image.texture=atlas;image.raycastTarget=false;
            float top=87+row*224f,bottom=Mathf.Min(984,top+224f);image.uvRect=new Rect(110f/1518f,1-bottom/1036f,230f/1518f,(bottom-top)/1036f);image.color=new Color(.13f,.13f,.13f,1);lamps[column*4+row]=image;
        }
        sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.34f;sound.priority=10;
        yellow=Tone("Start ready",660,.12f);green=Tone("Start go",990,.34f);panel.gameObject.SetActive(false);
    }
    public void Show(bool staging,bool counting,float remaining,bool go,bool paused)
    {
        if(paused){panel.gameObject.SetActive(false);return;}
        bool visible=staging || counting || go;int next=counting?Mathf.Clamp(3-Mathf.CeilToInt(remaining),0,2):go?3:-1;
        if(!visible){panel.gameObject.SetActive(false);reveal=0;phase=-1;sound.Stop();return;}
        panel.gameObject.SetActive(true);reveal=Mathf.MoveTowards(reveal,1,Time.unscaledDeltaTime/.55f);float ease=1-Mathf.Pow(1-reveal,3);panel.anchoredPosition=new Vector2(0,Mathf.Lerp(320,-105,ease));
        for(int i=0;i<lamps.Length;i++){bool lit=next==3?i%4==3:next>=0 && i%4<=next;lamps[i].color=lit?Color.white:new Color(.13f,.13f,.13f,1);}
        if(next==phase)return;phase=next;if(next>=0)sound.PlayOneShot(next==3?green:yellow);else sound.Stop();
    }
    static AudioClip Tone(string name,float hz,float seconds)
    {
        const int rate=44100;var data=new float[Mathf.CeilToInt(rate*seconds)];
        for(int i=0;i<data.Length;i++){float t=i/(float)rate;float envelope=Mathf.Min(1,t/.009f)*Mathf.Min(1,(seconds-t)/.04f);data[i]=(Mathf.Sin(2*Mathf.PI*hz*t)*.72f+Mathf.Sin(2*Mathf.PI*hz*1.5f*t)*.18f)*envelope*.65f;}
        var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
    void OnDestroy(){if(yellow)Destroy(yellow);if(green)Destroy(green);}
}
