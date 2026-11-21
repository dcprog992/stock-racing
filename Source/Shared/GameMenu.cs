using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public sealed class GameMenu:MonoBehaviour
{
    private static int escapeFrame=-1;
    public static bool EscapeHandled=>escapeFrame==Time.frameCount;
    public string Page {get;private set;}="home";
    private GameObject overlay;
    private RectTransform content,sidebar;
    private Font font;
    private Sprite brandSprite;
    private SpeedwayRace race;
    private bool training;
    private int laps=1;
    private readonly Color white=new Color32(239,244,249,255),muted=new Color32(148,165,185,255);
    private readonly Color cyan=new Color32(106,227,228,255),dark=new Color32(12,20,31,255),card=new Color32(24,36,51,255);
    IEnumerator Start()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        race=FindAnyObjectByType<SpeedwayRace>();
        var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=Screen.width/(float)Screen.height<1600f/900?0:1;
        gameObject.AddComponent<GraphicRaycaster>();
        overlay=Panel(transform,"Front end backdrop",Vector2.zero,new Vector2(0,0),new Vector2(0,0),new Color32(5,10,18,255)).gameObject;
        Stretch(overlay.GetComponent<RectTransform>());
        var frame=Panel(overlay.transform,"Menu frame",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1440,790),dark);
        Panel(frame,"Accent stripe",new Vector2(0,1),Vector2.zero,new Vector2(1440,4),cyan);
        sidebar=Panel(frame,"Navigation",new Vector2(0,1),new Vector2(0,-4),new Vector2(300,786),new Color32(16,25,38,255));
        content=Panel(frame,"Page",new Vector2(0,1),new Vector2(340,-42),new Vector2(1050,715),dark);
        yield return null;
        if(race && race.GameplayVisible)Hide();else if(race)ShowRace();else ShowHome();
    }
    void Update()
    {
        var pixels=GetComponent<Canvas>().pixelRect;
        if(pixels.height>0)GetComponent<CanvasScaler>().matchWidthOrHeight=pixels.width/pixels.height<1600f/900?0:1;
        if(Input.GetKeyDown(KeyCode.Escape) && overlay && overlay.activeSelf && !GameFlow.Loading)
        {
            if(Page=="settings" || Page=="controls" || Page=="driving"){escapeFrame=Time.frameCount;Back();}
            else if(Page=="setup"){escapeFrame=Time.frameCount;ShowHome();}
        }
#if UNITY_ANDROID
        if(MobileControls.Editing && Input.GetKeyDown(KeyCode.Escape))escapeFrame=Time.frameCount;
#endif
    }
    void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--){var go=parent.GetChild(i).gameObject;go.SetActive(false);Destroy(go);}}
    void Begin(string page,string title,string subtitle)
    {
        if(!overlay)return;
        Page=page;overlay.SetActive(true);Clear(content);Clear(sidebar);
        if(page!="loading"){var motion=content.GetComponent<UiPageMotion>();if(!motion)motion=content.gameObject.AddComponent<UiPageMotion>();motion.Play();}
        Text(sidebar,"STOCK RACING",27,white,24,-32,260,42);
        Text(sidebar,"OVAL RACING\nEXPERIENCE",16,cyan,26,-82,250,55);
        Text(sidebar,"RACING EXPERIENCE",12,muted,26,-155,250,22);
        Button(sidebar,"nav-play",race?"ЗАЕЗД":"ИГРАТЬ",26,-220,248,62,()=>{if(race)ShowRace();else ShowSetup();},page=="home" || page=="setup" || page=="race");
        Button(sidebar,"nav-settings","НАСТРОЙКИ",26,-298,248,62,ShowSettings,page=="settings");
        Button(sidebar,"nav-controls","УПРАВЛЕНИЕ",26,-376,248,62,ShowDrivingSettings,page=="controls" || page=="driving");
        if(race)Button(sidebar,"nav-home","ГЛАВНОЕ МЕНЮ",26,-565,248,62,()=>GameFlow.Home(this));
        else
        {
#if !UNITY_ANDROID
            Button(sidebar,"nav-exit","ВЫХОД",26,-565,248,62,()=>Application.Quit());
#endif
        }
        Text(sidebar,"STOCK RACING / 0.6.2\n"+(Application.platform==RuntimePlatform.Android?"MOBILE EDITION":"RACING EDITION"),13,muted,26,-707,250,45);
        Text(content,title,44,white,0,0,1010,62);
        Text(content,subtitle,19,muted,2,-74,1010,58);
    }
    public void ShowHome()
    {
        Begin("home","ТВОЙ СЛЕДУЮЩИЙ РЕКОРД","Высокая скорость. Точная траектория. Ещё один круг.");
        var hero=Panel(content,"Speedway hero",new Vector2(0,1),new Vector2(0,-155),new Vector2(1010,335),card);
        var logo=Resources.Load<Texture2D>("StockRacingIcon");
        if(logo){if(!brandSprite)brandSprite=Sprite.Create(logo,new Rect(0,0,logo.width,logo.height),Vector2.one*.5f);var image=Panel(hero,"Stock Racing logo",new Vector2(0,1),new Vector2(685,-15),new Vector2(305,305),Color.white).GetComponent<Image>();image.sprite=brandSprite;image.preserveAspect=true;image.raycastTarget=false;}
        Text(hero,"STOCK\nRACING",42,white,32,-32,610,120);
        Text(hero,"YOYLELAND INTERNATIONAL SPEEDWAY",15,cyan,35,-190,650,30);
        Text(hero,"Тренируйся с призраком своего рекорда\nили выходи на старт с тремя соперниками.",20,muted,35,-240,630,65);
        Button(content,"play","ИГРАТЬ   →",0,-535,420,86,ShowSetup,true);
        Text(content,"Выбери режим и дистанцию перед загрузкой трассы.",17,muted,0,-652,990,38);
    }
    public void ShowSetup()
    {

        Begin("setup","НАСТРОЙКА ЗАЕЗДА","Выбери режим, дистанцию и соперников перед загрузкой трассы.");
        Text(content,"01  /  РЕЖИМ",15,cyan,0,-140,900,28);
        Button(content,"mode-standard","ОБЫЧНЫЙ ЗАЕЗД",0,-181,488,78,()=>{training=false;ShowSetup();},!training);
        Button(content,"mode-training","ТРЕНИРОВКА",520,-181,488,78,()=>{training=true;ShowSetup();},training);
        Text(content,"Игрок + три соперника. Старт в четыре полосы.",17,muted,8,-274,490,50);
        Text(content,"Призрак лучшего заезда. Без ИИ-соперников.",17,muted,528,-274,480,50);
        Text(content,"02  /  ДИСТАНЦИЯ",15,cyan,0,-341,900,28);
        Button(content,"laps-1","1 КРУГ",0,-383,234,60,()=>{laps=1;ShowSetup();},laps==1);
        Button(content,"laps-3","3 КРУГА",254,-383,234,60,()=>{laps=3;ShowSetup();},laps==3);
        Text(content,"03  /  СЛОЖНОСТЬ СОПЕРНИКОВ",15,cyan,0,-478,1000,28);
        if(!training)
        {
            string[] ids={"easy","medium","hard","hardcore"};
            for(int i=0;i<4;i++)
            {
                var level=(RaceDifficulty.Level)i;
                Button(content,"difficulty-"+ids[i],RaceDifficulty.Name(level),i*258,-520,234,60,()=>{RaceDifficulty.Set(level);ShowSetup();},RaceDifficulty.Current==level);
            }
            Text(content,RaceDifficulty.Description(RaceDifficulty.Current),17,muted,0,-592,1010,30);
        }
        else Text(content,"В тренировке сложность не применяется: призрак повторяет твой рекорд.",19,muted,0,-528,1010,60);
        Text(content,(training?"ТРЕНИРОВКА":"ОБЫЧНЫЙ ЗАЕЗД")+" / "+(laps==1?"1 КРУГ":"3 КРУГА")+(training?"":" / "+RaceDifficulty.Name(RaceDifficulty.Current)),17,white,0,-653,665,42);
        Button(content,"start-race","НА ТРАССУ   →",690,-642,318,65,()=>GameFlow.Play(training,laps,this),true);
    }
    public void ShowSettings()
    {
        Begin("settings","НАСТРОЙКИ","Изменения сохраняются и применяются сразу.");
        Button(content,"driving-settings","РУЛЬ И УПРАВЛЕНИЕ",600,-145,410,62,ShowDrivingSettings);
        Text(content,"ГРАФИКА",15,cyan,0,-158,900,28);
        var value=Text(content,GameGraphics.Name(GameGraphics.Level),29,white,0,-200,1010,46);
        var rail=Panel(content,"Graphics quality slider",new Vector2(0,1),new Vector2(18,-291),new Vector2(970,38),new Color(0,0,0,0));
        var slider=rail.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=2;slider.wholeNumbers=true;
        var line=Panel(rail,"Quality rail",new Vector2(0,.5f),new Vector2(0,0),new Vector2(970,6),new Color32(51,72,91,255));
        var handle=Panel(rail,"Quality handle",new Vector2(0,.5f),Vector2.zero,new Vector2(30,38),cyan);
        slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.direction=Slider.Direction.LeftToRight;
        slider.value=GameGraphics.Level;
        var description=Text(content,QualityDescription(GameGraphics.Level),19,muted,0,-403,1010,75);
        slider.onValueChanged.AddListener(UiSounds.Slider);
        slider.onValueChanged.AddListener(v=>{GameGraphics.Set(Mathf.RoundToInt(v));value.text=GameGraphics.Name(GameGraphics.Level);description.text=QualityDescription(GameGraphics.Level);});
        Text(content,"МИНИМУМ",16,muted,0,-350,300,30);
        var middle=Text(content,"СРЕДНЕ",16,muted,350,-350,300,30);middle.alignment=TextAnchor.UpperCenter;
        var ultra=Text(content,"УЛЬТРА",16,muted,710,-350,300,30);ultra.alignment=TextAnchor.UpperRight;
        bool forgiveness=PlayerPrefs.GetInt("Speedway.ForgiveMistakes",1)!=0;
        var assistButton=Button(content,"forgiveness","ПРОЩЕНИЕ ОШИБОК: "+(forgiveness?"ВКЛ":"ВЫКЛ"),0,-519,700,68,null);
        assistButton.onClick.AddListener(()=>{forgiveness=!forgiveness;PlayerPrefs.SetInt("Speedway.ForgiveMistakes",forgiveness?1:0);PlayerPrefs.Save();if(race)race.car.ForgiveMistakes=forgiveness;assistButton.GetComponentInChildren<Text>().text="ПРОЩЕНИЕ ОШИБОК: "+(forgiveness?"ВКЛ":"ВЫКЛ");});
        Text(content,"Помощь при потере устойчивости на высокой скорости.",17,muted,0,-597,990,28);
        Button(content,"speed-units","СКОРОСТЬ: "+SpeedUnits.Label,725,-519,283,68,()=>{SpeedUnits.SetMiles(!SpeedUnits.Miles);ShowSettings();});
#if UNITY_ANDROID
        Button(content,"button-layout","РАСПОЛОЖЕНИЕ КНОПОК",0,-647,700,60,()=>MobileLayoutEditor.Open(this));
#endif
        Button(content,"back","НАЗАД",758,-647,250,60,Back);
    }
    public void ShowDrivingSettings()
    {
        Begin("driving","РУЛЬ И УПРАВЛЕНИЕ","Настрой отклик машины под себя. Выбор сохраняется.");
        Text(content,"ЧУВСТВИТЕЛЬНОСТЬ РУЛЯ",15,cyan,0,-158,1010,28);
        var readout=Text(content,Mathf.RoundToInt(DrivingSettings.Sensitivity*100)+"%",32,white,0,-198,1010,48);
        var rail=Panel(content,"Steering sensitivity",new Vector2(0,1),new Vector2(18,-285),new Vector2(970,40),new Color(0,0,0,0));
        var slider=rail.gameObject.AddComponent<Slider>();slider.minValue=.5f;slider.maxValue=2;
        Panel(rail,"Steering rail",new Vector2(0,.5f),Vector2.zero,new Vector2(970,6),new Color32(51,72,91,255));
        var handle=Panel(rail,"Steering handle",new Vector2(0,.5f),Vector2.zero,new Vector2(30,0),cyan);
        slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.value=DrivingSettings.Sensitivity;
        slider.onValueChanged.AddListener(UiSounds.Slider);
        slider.onValueChanged.AddListener(v=>{DrivingSettings.SetSensitivity(v);readout.text=Mathf.RoundToInt(DrivingSettings.Sensitivity*100)+"%";});
        Text(content,"50% — плавнее                         125% — стандарт                         200% — резче",17,muted,0,-345,1010,30);
        Text(content,"Выше чувствительность — больше поворот колёс и быстрее отклик.\nНа высокой скорости резкий поворот может сорвать машину в занос.",19,muted,0,-398,1010,68);
#if UNITY_ANDROID
        Text(content,"ТИП УПРАВЛЕНИЯ РУЛЁМ",15,cyan,0,-490,1010,28);
        Button(content,"steering-arrows","СТРЕЛКИ",0,-535,310,62,()=>{DrivingSettings.SetTouchWheel(false);ShowDrivingSettings();},!DrivingSettings.TouchWheel);
        Button(content,"steering-wheel","РУЛЬ ПАЛЬЦЕМ",330,-535,330,62,()=>{DrivingSettings.SetTouchWheel(true);ShowDrivingSettings();},DrivingSettings.TouchWheel);
        Button(content,"button-layout","РАСПОЛОЖЕНИЕ И РАЗМЕР",0,-647,470,60,()=>MobileLayoutEditor.Open(this));
        Button(content,"driving-help","КАК УПРАВЛЯТЬ",490,-647,250,60,ShowControls);
#else
        Text(content,"КЛАВИАТУРА",15,cyan,0,-490,1010,28);
        Text(content,"A / D — руль. Регулятор меняет отклик твоей машины.\nНастройки соперников остаются независимыми.",20,white,0,-532,1010,68);
        Button(content,"driving-help","КЛАВИШИ И ПОДСКАЗКИ",0,-647,700,60,ShowControls);
#endif
        Button(content,"back","НАЗАД",758,-647,250,60,Back);
    }

    static string QualityDescription(int q)=>q==0?"Максимум производительности: без теней, проще сглаживание и текстуры.":q==1?"Баланс детализации и плавности: тени, сглаживание 2×, средняя детализация.":"Максимальная детализация: дальние тени, текстуры и сглаживание 4×.\nНа телефоне возможны нагрев и снижение частоты кадров.";
    public void ShowControls()
    {
        Begin("controls","УПРАВЛЕНИЕ","Всё, что нужно для первого заезда.");
#if UNITY_ANDROID
        string[] names={"РУЛЬ И ПЕДАЛИ","КОРОБКА ПЕРЕДАЧ","ЛАУНЧ-КОНТРОЛЬ","СТАРТ ЗАЕЗДА"};
        string[] info={"Слева — руль или стрелки, справа — педали.\nТип и чувствительность: Настройки → Руль и управление.","M / A переключает ручную и автоматическую коробку.\nВ ручном режиме доступны сцепление и + / −.","Удерживай газ и ручник одновременно.\nДля старта отпусти ручник.","Остановись перед линией и нажми СТАРТ.\nДвижение во время отсчёта отменяет его."};
#else
        string[] names={"ДВИЖЕНИЕ","КОРОБКА ПЕРЕДАЧ","КАМЕРА И ИНТЕРФЕЙС","СТАРТ И МЕНЮ"};
        string[] info={"W — газ  /  S — тормоз  /  A, D — руль\nПробел — ручник  /  W + Пробел — лаунч","E / Q — передачи вверх / вниз  /  Shift — сцепление\nT — автоматическая / ручная коробка","C — смена камеры  /  I — скрыть приборы","Enter — отсчёт перед линией  /  Esc — пауза\nBackspace — начать заново"};
#endif
        for(int i=0;i<4;i++){float y=-154-i*116;Text(content,names[i],15,cyan,0,y,990,26);Text(content,info[i],20,white,0,y-37,1010,65);}
        Button(content,"back","НАЗАД",758,-647,250,60,Back);
    }
    public void ShowRace()
    {
        if(!overlay)return;
        if(!race){ShowHome();return;}
        if(race.GameplayVisible){Hide();return;}
        if(race.IsReady){ShowHome();return;}
        Begin("race",race.IsFinished?"ФИНИШ":"ПАУЗА",race.SessionDescription);
        if(race.IsFinished)
        {
            var result=Panel(content,"Result card",new Vector2(0,1),new Vector2(0,-160),new Vector2(1010,245),card);
            Text(result,"ВРЕМЯ ЗАЕЗДА",16,cyan,28,-26,950,32);
            Text(result,race.ResultTime,72,white,26,-70,950,100);
            Text(result,"ЛУЧШЕЕ  /  "+race.BestTime,22,muted,30,-188,950,40);
        }
        else
        {
            Text(content,"Передохни. Трасса подождёт.",28,white,0,-185,1000,60);
            Button(content,"resume","ПРОДОЛЖИТЬ   →",0,-300,485,86,()=>race.ResumeRace(),true);
        }
        Button(content,"restart","НАЧАТЬ ЗАНОВО",0,-468,485,74,()=>race.RestartRace());
        Button(content,"return-home","В ГЛАВНОЕ МЕНЮ",520,-468,488,74,()=>GameFlow.Home(this));
        Text(content,"Настройки графики и помощь доступны слева.",18,muted,0,-595,1010,48);
    }
    void OnDestroy(){if(brandSprite)Destroy(brandSprite);}
    void Back(){if(race)ShowRace();else ShowHome();}
    public void Hide(){if(overlay)overlay.SetActive(false);}
    public void ShowLoading(float progress)
    {
        Begin("loading","ЗАГРУЗКА ТРАССЫ","Готовим машину, соперников и трассу.");
        foreach(var b in sidebar.GetComponentsInChildren<Button>())b.interactable=false;
        Text(content,Mathf.RoundToInt(progress*100)+"%",76,white,0,-235,1000,120);
        var rail=Panel(content,"Loading rail",new Vector2(0,1),new Vector2(0,-405),new Vector2(1010,8),card);
        Panel(rail,"Loading progress",new Vector2(0,1),Vector2.zero,new Vector2(1010*progress,8),cyan);
    }
    RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));var r=(RectTransform)go.transform;r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;
        var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=true;return r;
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    Text Text(Transform parent,string caption,int size,Color color,float x,float y,float w,float h)
    {
        var go=new GameObject("Text",typeof(RectTransform),typeof(Text));var r=(RectTransform)go.transform;r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
        var text=go.GetComponent<Text>();text.font=font;text.text=caption;text.fontSize=size;text.color=color;text.raycastTarget=false;return text;
    }
    Button Button(Transform parent,string id,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool selected=false)
    {
        var r=Panel(parent,id,new Vector2(0,1),new Vector2(x,y),new Vector2(w,h),selected?cyan:card);
        var b=r.gameObject.AddComponent<Button>();r.gameObject.AddComponent<UiFeedback>();b.onClick.AddListener(UiSounds.Click);b.targetGraphic=r.GetComponent<Image>();if(action!=null)b.onClick.AddListener(action);
        var text=Text(r,caption,21,selected?dark:white,18,0,w-36,h);text.alignment=TextAnchor.MiddleCenter;
        var colors=b.colors;colors.highlightedColor=new Color(.75f,.9f,1);colors.pressedColor=new Color(.5f,.75f,.8f);b.colors=colors;return b;
    }
}


