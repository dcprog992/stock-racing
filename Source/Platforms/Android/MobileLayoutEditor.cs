using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class MobileLayoutEditor:MonoBehaviour
{
    private GameMenu menu;
    private MobileControls controls;
    private MobileButtonLayout selected;
    private Slider slider;
    private Text caption;
    private bool preview;
    public static void Open(GameMenu menu)
    {
        if(FindAnyObjectByType<MobileLayoutEditor>())return;
        var editor=new GameObject("Button layout editor").AddComponent<MobileLayoutEditor>();editor.menu=menu;
    }
    IEnumerator Start()
    {
        MobileDriveInput.Reset();MobileControls.Editing=true;
        controls=FindAnyObjectByType<MobileControls>();preview=!controls;
        if(preview){controls=new GameObject("Layout preview controls").AddComponent<MobileControls>();controls.PreviewOnly=true;}
        menu.Hide();
        var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
        gameObject.AddComponent<GraphicRaycaster>();
        var panel=Rect("Layout editor panel",transform,new Vector2(.5f,1),new Vector2(0,-22),new Vector2(820,220));
        panel.gameObject.AddComponent<Image>().color=new Color32(14,25,39,248);
        Label(panel,"ПЕРЕТАЩИ КНОПКУ · ИЗМЕНИ ЕЁ РАЗМЕР",21,new Vector2(20,-15),new Vector2(780,35));
        caption=Label(panel,"Выбери кнопку на экране",18,new Vector2(20,-60),new Vector2(780,30));
        var rail=Rect("Button size",panel,new Vector2(0,1),new Vector2(25,-111),new Vector2(770,30));
        slider=rail.gameObject.AddComponent<Slider>();slider.minValue=.65f;slider.maxValue=1.65f;
        var bg=Rect("Size rail",rail,new Vector2(0,.5f),Vector2.zero,new Vector2(770,6));bg.gameObject.AddComponent<Image>().color=new Color32(64,87,108,255);
        var handle=Rect("Size handle",rail,new Vector2(0,.5f),Vector2.zero,new Vector2(28,34));handle.gameObject.AddComponent<Image>().color=new Color32(106,227,228,255);
        slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.interactable=false;
        slider.onValueChanged.AddListener(UiSounds.Slider);
        slider.onValueChanged.AddListener(v=>{if(selected){selected.SetScale(v);UpdateCaption();}});
        Button(panel,"layout-reset","СБРОСИТЬ",new Vector2(20,-162),()=>{foreach(var item in controls.GetComponentsInChildren<MobileButtonLayout>(true))item.Restore();PlayerPrefs.Save();if(selected)Select(selected);});
        Button(panel,"layout-save","ГОТОВО",new Vector2(560,-162),Close);
        yield return null;
        foreach(var button in controls.GetComponentsInChildren<Button>(true))button.interactable=false;
    }
    public void Select(MobileButtonLayout item){selected=item;slider.interactable=true;slider.SetValueWithoutNotify(item.Scale);UpdateCaption();}
    void UpdateCaption(){caption.text=Name(selected.Id)+"  /  РАЗМЕР "+Mathf.RoundToInt(selected.Scale*100)+"%";}
    static string Name(string id)=>id switch{"Accelerator"=>"ГАЗ","Brake"=>"ТОРМОЗ","Handbrake"=>"РУЧНИК","Steering wheel"=>"РУЛЬ","Steer left"=>"РУЛЬ ВЛЕВО","Steer right"=>"РУЛЬ ВПРАВО","Clutch"=>"СЦЕПЛЕНИЕ","Gear up"=>"ПЕРЕДАЧА +","Gear down"=>"ПЕРЕДАЧА −","Camera"=>"КАМЕРА","Transmission"=>"КОРОБКА","Pause"=>"ПАУЗА","Settings"=>"НАСТРОЙКИ",_=>"СТАРТ"};
    public void Close()
    {
        if(controls)foreach(var item in controls.GetComponentsInChildren<MobileButtonLayout>(true))item.Save();
        MobileControls.Editing=false;MobileDriveInput.Reset();
        if(controls){if(preview)Destroy(controls.gameObject);else foreach(var button in controls.GetComponentsInChildren<Button>(true))button.interactable=true;}
        menu.ShowDrivingSettings();Destroy(gameObject);
    }
    void OnDestroy(){MobileControls.Editing=false;MobileDriveInput.Reset();}
    void Update(){if(Input.GetKeyDown(KeyCode.Escape))Close();}
    RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
    Text Label(Transform parent,string text,int size,Vector2 pos,Vector2 bounds)
    {var r=Rect("Label",parent,new Vector2(0,1),pos,bounds);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;return t;}
    void Button(Transform parent,string name,string text,Vector2 pos,UnityEngine.Events.UnityAction action)
    {var r=Rect(name,parent,new Vector2(0,1),pos,new Vector2(240,45));r.gameObject.AddComponent<Image>().color=new Color32(35,66,81,255);r.gameObject.AddComponent<Button>().onClick.AddListener(action);var label=Label(r,text,18,Vector2.zero,new Vector2(240,45));label.alignment=TextAnchor.MiddleCenter;}
}

