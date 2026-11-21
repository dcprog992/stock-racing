using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MobileButtonLayout:MonoBehaviour,IPointerDownHandler,IDragHandler,IEndDragHandler
{
    public string Id {get;private set;}
    public float Scale {get;private set;}=1;
    private RectTransform rect;
    private Vector2 originalPosition,originalSize;
    string Key=>"Speedway.TouchLayout."+Id;
    public void Initialize(string id,RectTransform target)
    {
        Id=id;rect=target;originalPosition=rect.anchoredPosition;originalSize=rect.sizeDelta;
        rect.anchoredPosition=new Vector2(PlayerPrefs.GetFloat(Key+".x",originalPosition.x),PlayerPrefs.GetFloat(Key+".y",originalPosition.y));
        SetScale(PlayerPrefs.GetFloat(Key+".scale",1),false);
    }
    public void SetScale(float scale,bool save=true)
    {
        Scale=Mathf.Clamp(scale,.65f,1.65f);rect.localScale=Vector3.one*Scale;
        if(save){Clamp();Save();}
    }
    public void Restore()
    {
        PlayerPrefs.DeleteKey(Key+".x");PlayerPrefs.DeleteKey(Key+".y");PlayerPrefs.DeleteKey(Key+".scale");
        rect.anchoredPosition=originalPosition;SetScale(1,false);
    }
    public void OnPointerDown(PointerEventData e)
    {if(MobileControls.Editing)FindAnyObjectByType<MobileLayoutEditor>()?.Select(this);}
    public void OnDrag(PointerEventData e)
    {
        if(!MobileControls.Editing)return;
        var parent=(RectTransform)rect.parent;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out var now) && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position-e.delta,e.pressEventCamera,out var before))rect.anchoredPosition+=now-before;
        Clamp();
    }
    public void OnEndDrag(PointerEventData e){if(MobileControls.Editing)Save();}
    void Clamp()
    {
        var area=(RectTransform)rect.parent;
        Vector2 size=Vector2.Scale(originalSize,Vector2.one*Scale);
        Vector2 minimum=area.rect.min+Vector2.Scale(size,rect.pivot),maximum=area.rect.max-Vector2.Scale(size,Vector2.one-rect.pivot);
        var p=rect.localPosition;p.x=Mathf.Clamp(p.x,minimum.x,maximum.x);p.y=Mathf.Clamp(p.y,minimum.y,maximum.y);rect.localPosition=p;
    }
    public void Save(){PlayerPrefs.SetFloat(Key+".x",rect.anchoredPosition.x);PlayerPrefs.SetFloat(Key+".y",rect.anchoredPosition.y);PlayerPrefs.SetFloat(Key+".scale",Scale);PlayerPrefs.Save();}
}
