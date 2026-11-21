using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MobileSteeringWheel:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    public const float MaxRotation=180f;
    public float Rotation {get;private set;}
    public bool Held=>pointer.HasValue;
    private int? pointer;
    private float previousAngle;
    private bool validAngle;
    private RectTransform rim;
    public void Initialize(RectTransform visual){rim=visual;Apply();}
    public void OnPointerDown(PointerEventData e)
    {
        if(MobileControls.Editing || pointer.HasValue)return;
        pointer=e.pointerId;e.useDragThreshold=false;
        validAngle=Angle(e,out previousAngle);
        // Keep the current wheel rotation; no snap to the finger's absolute angle.
    }
    public void OnBeginDrag(PointerEventData e){if(pointer==e.pointerId)e.useDragThreshold=false;}
    public void OnDrag(PointerEventData e)
    {
        if(MobileControls.Editing || pointer!=e.pointerId)return;
        if(!Angle(e,out float current)){validAngle=false;return;}
        if(validAngle)Rotation=Mathf.Clamp(Rotation+Mathf.DeltaAngle(previousAngle,current),-MaxRotation,MaxRotation);
        previousAngle=current;validAngle=true;Apply();
    }
    bool Angle(PointerEventData e,out float angle)
    {
        angle=0;var rect=(RectTransform)transform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local))return false;
        var fromCenter=local-rect.rect.center;
        if(fromCenter.sqrMagnitude<Mathf.Pow(Mathf.Min(rect.rect.width,rect.rect.height)*.08f,2))return false;
        angle=Mathf.Atan2(fromCenter.y,fromCenter.x)*Mathf.Rad2Deg;return true;
    }
    public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId){pointer=null;validAngle=false;}}
    public void OnEndDrag(PointerEventData e)=>OnPointerUp(e);
    void Update()
    {
        if(MobileControls.Editing){ResetWheel();return;}
        if(!pointer.HasValue)Rotation=Mathf.MoveTowards(Rotation,0,360f*Time.unscaledDeltaTime);
        Apply();
    }
    void Apply(){if(rim)rim.localRotation=Quaternion.Euler(0,0,Rotation);MobileDriveInput.WheelAxis=-Rotation/MaxRotation;}
    public void ResetWheel(){pointer=null;validAngle=false;Rotation=0;Apply();}
    void OnDisable()=>ResetWheel();
    void OnApplicationFocus(bool focus){if(!focus)ResetWheel();}
    void OnApplicationPause(bool pause){if(pause)ResetWheel();}
}
