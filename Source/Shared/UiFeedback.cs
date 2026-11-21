using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UiFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    bool hover,pressed;
    public void OnPointerEnter(PointerEventData e){hover=true;}
    public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
    public void OnPointerDown(PointerEventData e){pressed=true;}
    public void OnPointerUp(PointerEventData e){pressed=false;}
    void OnDisable(){hover=pressed=false;transform.localScale=Vector3.one;}
    void Update()
    {
        float target=pressed?.965f:hover?1.016f:1f;
        transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*target,1-Mathf.Exp(-24*Time.unscaledDeltaTime));
    }
}

public sealed class UiPageMotion:MonoBehaviour
{
    CanvasGroup group;RectTransform rect;Vector2 home;float elapsed;
    void Awake(){rect=GetComponent<RectTransform>();home=rect.anchoredPosition;group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();}
    public void Play(){elapsed=0;group.alpha=0;rect.anchoredPosition=home+Vector2.down*14;enabled=true;}
    void Update(){elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.22f);float ease=1-Mathf.Pow(1-t,3);group.alpha=ease;rect.anchoredPosition=home+Vector2.down*(14*(1-ease));if(t>=1)enabled=false;}
}

public sealed class UiSounds:MonoBehaviour
{
    static UiSounds instance;
    AudioSource source;AudioClip click,tick;float lastTick=-1;
    static UiSounds Get()
    {
        if(instance)return instance;
        var go=new GameObject("Interface audio");DontDestroyOnLoad(go);instance=go.AddComponent<UiSounds>();
        instance.source=go.AddComponent<AudioSource>();instance.source.playOnAwake=false;instance.source.ignoreListenerPause=true;instance.source.spatialBlend=0;instance.source.volume=.24f;
        instance.click=Tone(360,.07f);instance.tick=Tone(520,.026f);return instance;
    }
    public static void Click()=>Get().source.PlayOneShot(Get().click);
    public static void Slider(float value){var sound=Get();if(Time.unscaledTime-sound.lastTick<.055f)return;sound.lastTick=Time.unscaledTime;sound.source.PlayOneShot(sound.tick,.65f);}
    static AudioClip Tone(float hz,float length)
    {
        var samples=new float[Mathf.CeilToInt(length*44100)];
        for(int i=0;i<samples.Length;i++){float t=i/44100f;float envelope=Mathf.Min(1,t/.003f)*Mathf.Exp(-t*65);samples[i]=(Mathf.Sin(t*hz*2*Mathf.PI)+.2f*Mathf.Sin(t*hz*4*Mathf.PI))*envelope*.65f;}
        var clip=AudioClip.Create("UI feedback",samples.Length,1,44100,false);clip.SetData(samples,0);return clip;
    }
    void OnDestroy(){if(click)Destroy(click);if(tick)Destroy(tick);if(instance==this)instance=null;}
}

