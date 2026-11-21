using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpeedwayFinishOverlay:MonoBehaviour
{
    private SpeedwayRace race;private GameObject panel;private Text rows,title;
    void Start()
    {
        race=GetComponent<SpeedwayRace>();
        var root=new GameObject("Finish results",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=20;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        panel=new GameObject("Results panel",typeof(RectTransform),typeof(Image));var rect=(RectTransform)panel.transform;rect.SetParent(root.transform,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-80);rect.sizeDelta=new Vector2(570,185);
        panel.GetComponent<Image>().color=new Color32(10,18,27,225);panel.GetComponent<Image>().raycastTarget=false;
        title=Label(rect,new Vector2(18,-12),new Vector2(535,28),19);title.color=new Color32(106,227,228,255);
        rows=Label(rect,new Vector2(18,-47),new Vector2(535,126),18);panel.SetActive(false);
    }
    void Update()
    {
        if(!panel)return;bool show=race.HasFinished && !race.IsPaused;panel.SetActive(show);if(!show)return;
        var results=new List<SpeedwayOpponent.Result>();var fleet=race.GetComponent<SpeedwayOpponent>();if(fleet)results.AddRange(fleet.GetResults());
        results.Add(new SpeedwayOpponent.Result{Name="ВЫ",Time=(float)race.PlayerFinishTime,Finished=true,Laps=race.CompletedPlayerLaps});
        results.Sort((a,b)=>{if(a.Finished!=b.Finished)return a.Finished?-1:1;if(a.Finished)return a.Time.CompareTo(b.Time);int laps=b.Laps.CompareTo(a.Laps);return laps!=0?laps:b.Progress.CompareTo(a.Progress);});
        bool all=results.TrueForAll(r=>r.Finished);
        title.text=results.Count==1?"ФИНИШ ТРЕНИРОВКИ · ВАШЕ ВРЕМЯ":(all?"ПОБЕДИТЕЛЬ: ":"ЖДЁМ ФИНИША · ЛИДЕР: ")+results[0].Name;
        var text="";for(int i=0;i<results.Count;i++){var r=results[i];text+=(i+1)+".  "+r.Name+"     "+(r.Finished?Format(r.Time):"ЕЩЁ В ЗАЕЗДЕ")+(i+1<results.Count?"\n":"");}
        rows.text=text;
    }
    static string Format(float seconds){int minutes=(int)(seconds/60);return minutes.ToString("00")+":"+(seconds%60).ToString("00.000",System.Globalization.CultureInfo.InvariantCulture);}
    static Text Label(Transform parent,Vector2 pos,Vector2 size,int fontSize)
    {
        var go=new GameObject("Result text",typeof(RectTransform),typeof(Text));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;
        var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.color=Color.white;t.raycastTarget=false;return t;
    }
}
