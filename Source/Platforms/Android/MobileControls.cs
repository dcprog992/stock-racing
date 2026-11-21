using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileControls:MonoBehaviour
{
    public static bool Editing;
    public bool PreviewOnly;
    private SpeedwayRace race;
    private CarController car;
    private CameraFollow view;
    private RectTransform safe;
    private GameObject driving,manual;
    private GameObject wheelControl,leftControl,rightControl;
    private bool lastWheelMode;
    private Button start;
    private Image transmissionIcon;
    private readonly Dictionary<string,Sprite> sprites=new();
    private Rect previousSafe;
    private int previousWidth,previousHeight;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitialScene()=>Loaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
#if UNITY_ANDROID || NASCAR_MOBILE
        MobileDriveInput.Reset();
        if(FindAnyObjectByType<SpeedwayRace>() && !FindAnyObjectByType<MobileControls>())
            new GameObject("Mobile controls").AddComponent<MobileControls>();
#endif
    }
    void Start()
    {
        race=FindAnyObjectByType<SpeedwayRace>();if(!race && !PreviewOnly){Destroy(gameObject);return;}
        car=race?race.car:null;if(car)car.automaticTransmission=PlayerPrefs.GetInt("Speedway.Mobile.Automatic",1)!=0;
        view=Camera.main?Camera.main.GetComponent<CameraFollow>():null;
        var atlas=Resources.Load<Texture2D>("MobileControlAtlas");
        if(atlas)
        {
            Add("gas",atlas,130,64,180,295);Add("brake",atlas,455,85,300,240);
            Add("handbrake",atlas,850,90,295,250);Add("left",atlas,1293,100,130,225);Add("right",atlas,1720,100,135,225);
            Add("camera",atlas,110,460,230,220);Add("manual",atlas,500,450,220,295);Add("auto",atlas,910,440,170,300);
            Add("pause",atlas,1268,480,190,220);Add("settings",atlas,1625,460,260,255);
        }
        gameObject.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;GetComponent<Canvas>().sortingOrder=30;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
        gameObject.AddComponent<GraphicRaycaster>();
        safe=Rect("Safe area",transform,Vector2.zero,Vector2.zero,Vector2.zero);Full(safe);
        driving=Rect("Driving touch controls",safe,Vector2.zero,Vector2.zero,Vector2.zero).gameObject;Full((RectTransform)driving.transform);
        Hold("Steer left","left","",new(0,0),new(40,45),new(165,165),v=>MobileDriveInput.Left=v);
        Hold("Steer right","right","",new(0,0),new(230,45),new(165,165),v=>MobileDriveInput.Right=v);
        leftControl=driving.transform.Find("Steer left").gameObject;rightControl=driving.transform.Find("Steer right").gameObject;
        var wheelTexture=Resources.Load<Texture2D>("MobileSteeringWheel");
        var wheel=Surface("Steering wheel",null,null,new(0,0),new(40,45),new(350,350),null);wheelControl=wheel.gameObject;
        wheel.GetComponent<Image>().color=new Color(0,0,0,0);
        var rim=Rect("Rim",wheel,Vector2.one*.5f,Vector2.zero,new Vector2(350,350));
        var rimImage=rim.gameObject.AddComponent<Image>();rimImage.raycastTarget=false;rimImage.preserveAspect=true;
        if(wheelTexture){var sprite=Sprite.Create(wheelTexture,new Rect(110,140,1020,1020),Vector2.one*.5f,100);sprites["steering-wheel"]=sprite;rimImage.sprite=sprite;}
        wheel.gameObject.AddComponent<MobileSteeringWheel>().Initialize(rim);
        lastWheelMode=DrivingSettings.TouchWheel;UpdateSteeringMode();
        Hold("Accelerator","gas","",new(1,0),new(-40,45),new(135,230),v=>MobileDriveInput.Gas=v);
        Hold("Brake","brake","",new(1,0),new(-205,45),new(175,160),v=>MobileDriveInput.Brake=v);
        Hold("Handbrake","handbrake","",new(1,0),new(-205,235),new(175,115),v=>MobileDriveInput.Handbrake=v);
        manual=Rect("Manual gearbox",driving.transform,Vector2.zero,Vector2.zero,Vector2.zero).gameObject;Full((RectTransform)manual.transform);
        Hold("Clutch","brake","СЦЕПЛ.",new(1,0),new(-410,45),new(145,155),v=>MobileDriveInput.Clutch=v,parent:manual.transform);
        ActionButton("Gear up",null,"+",new(1,0),new(-410,225),new(145,125),MobileDriveInput.ShiftUp,manual.transform);
        ActionButton("Gear down",null,"−",new(1,0),new(-580,225),new(145,125),MobileDriveInput.ShiftDown,manual.transform);
        ActionButton("Pause","pause","",new(1,1),new(-40,-25),new(95,90),()=>{MobileDriveInput.Reset();race.PauseRace();});
        ActionButton("Settings","settings","",new(1,1),new(-155,-25),new(95,90),()=>{MobileDriveInput.Reset();race.PauseRace();FindAnyObjectByType<GameMenu>()?.ShowSettings();});
        ActionButton("Camera","camera","",new(1,1),new(-270,-25),new(95,90),()=>view?.CycleView());
        var modeButton=ActionButton("Transmission","auto","",new(1,1),new(-385,-25),new(95,90),()=>
        {
            car.automaticTransmission=!car.automaticTransmission;MobileDriveInput.Clutch=false;
            PlayerPrefs.SetInt("Speedway.Mobile.Automatic",car.automaticTransmission?1:0);PlayerPrefs.Save();
        });
        transmissionIcon=modeButton.transform.Find("Icon").GetComponent<Image>();
        start=ActionButton("Start countdown",null,"СТАРТ",new(.5f,0),new(0,45),new(225,88),()=>race.MobilePrimaryAction());
        Layout();
    }
    void Add(string name,Texture2D atlas,int x,int y,int w,int h)
    {sprites[name]=Sprite.Create(atlas,new Rect(x,atlas.height-y-h,w,h),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);}
    void Update()
    {
        if(!driving)return;
        UpdateSteeringMode();
        if(Editing){driving.SetActive(true);manual.SetActive(true);start.gameObject.SetActive(true);GetComponent<Canvas>().sortingOrder=75;if(previousSafe!=Screen.safeArea || previousWidth!=Screen.width || previousHeight!=Screen.height)Layout();return;}
        GetComponent<Canvas>().sortingOrder=30;
        if(!race)return;
        bool visible=race.GameplayVisible;
        if(driving.activeSelf!=visible){driving.SetActive(visible);MobileDriveInput.Reset();}
        if(visible)
        {
            manual.SetActive(!car.automaticTransmission);start.gameObject.SetActive(race.MobileWaitingForStart);
            if(sprites.TryGetValue(car.automaticTransmission?"auto":"manual",out var icon))transmissionIcon.sprite=icon;
        }
        if(previousSafe!=Screen.safeArea || previousWidth!=Screen.width || previousHeight!=Screen.height)Layout();
    }
    void UpdateSteeringMode()
    {
        bool useWheel=DrivingSettings.TouchWheel;
        if(useWheel!=lastWheelMode){MobileDriveInput.Reset();wheelControl.GetComponent<MobileSteeringWheel>().ResetWheel();lastWheelMode=useWheel;}
        wheelControl.SetActive(useWheel);leftControl.SetActive(!useWheel);rightControl.SetActive(!useWheel);
    }
    void Layout()
    {
        previousSafe=Screen.safeArea;previousWidth=Screen.width;previousHeight=Screen.height;
        if(!safe || Screen.width==0 || Screen.height==0)return;
        safe.anchorMin=new Vector2(previousSafe.xMin/Screen.width,previousSafe.yMin/Screen.height);
        safe.anchorMax=new Vector2(previousSafe.xMax/Screen.width,previousSafe.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
    }
    void OnApplicationPause(bool pause){if(pause){CancelHeldControls();if(race && race.GameplayVisible)race.PauseRace();}}
    void OnApplicationFocus(bool focus){if(!focus)CancelHeldControls();}
    void CancelHeldControls(){foreach(var button in GetComponentsInChildren<MobileHoldButton>(true))button.Cancel();foreach(var wheel in GetComponentsInChildren<MobileSteeringWheel>(true))wheel.ResetWheel();MobileDriveInput.Reset();}
    void OnDestroy(){MobileDriveInput.Reset();foreach(var sprite in sprites.Values)if(sprite)Destroy(sprite);}
    Button ActionButton(string name,string icon,string caption,Vector2 anchor,Vector2 pos,Vector2 size,Action action,Transform parent=null)
    {
        var r=Surface(name,icon,caption,anchor,pos,size,parent);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(()=>action());return b;
    }
    void Hold(string name,string icon,string caption,Vector2 anchor,Vector2 pos,Vector2 size,Action<bool> action,Transform parent=null)
    {var r=Surface(name,icon,caption,anchor,pos,size,parent);r.gameObject.AddComponent<MobileHoldButton>().Initialize(action,r.GetComponent<Image>());}
    RectTransform Surface(string name,string icon,string caption,Vector2 anchor,Vector2 pos,Vector2 size,Transform parent)
    {
        var r=Rect(name,parent?parent:driving.transform,anchor,pos,size);
        var image=r.gameObject.AddComponent<Image>();image.color=new Color32(9,17,26,112);image.raycastTarget=true;
        if(icon!=null)
        {
            var child=Rect("Icon",r,Vector2.one*.5f,Vector2.zero,size-new Vector2(32,26));
            var i=child.gameObject.AddComponent<Image>();sprites.TryGetValue(icon,out var s);i.sprite=s;i.preserveAspect=true;i.raycastTarget=false;i.color=new Color(1,1,1,.87f);
        }
        if(!string.IsNullOrEmpty(caption))
        {
            var child=Rect("Label",r,Vector2.one*.5f,icon==null?Vector2.zero:new Vector2(0,-size.y*.35f),new Vector2(size.x,icon==null?size.y:32));
            var t=child.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=caption.Length>2?22:54;t.alignment=TextAnchor.MiddleCenter;t.text=caption;t.color=Color.white;t.raycastTarget=false;
        }
        r.gameObject.AddComponent<MobileButtonLayout>().Initialize(name,r);
        return r;
    }
    static void Full(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
}
public sealed class MobileHoldButton:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
{
    readonly HashSet<int> pointers=new();private Action<bool> action;private Image image;private Color normal;
    public void Initialize(Action<bool> callback,Image target){action=callback;image=target;normal=target.color;}
    public void OnPointerDown(PointerEventData e){if(MobileControls.Editing)return;pointers.Add(e.pointerId);Apply();}
    public void OnPointerUp(PointerEventData e){pointers.Remove(e.pointerId);Apply();}
    public void OnPointerExit(PointerEventData e){ /* Keep the captured finger until PointerUp, even outside the button. */ }
    void Apply(){bool held=pointers.Count>0;action?.Invoke(held);if(image)image.color=held?new Color32(65,160,165,190):normal;}
    public void Cancel(){pointers.Clear();action?.Invoke(false);if(image)image.color=normal;}
    void OnDisable()=>Cancel();
}
