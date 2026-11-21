using UnityEngine;

public static class MobileDriveInput
{
    public static bool Left,Right,Gas,Brake,Handbrake,Clutch;
    private static bool up,down;
    public static float WheelAxis;
    public static float Throttle => Gas ? 1f : 0f;
    public static float Steering => DrivingSettings.TouchWheel ? Mathf.Clamp(WheelAxis,-1f,1f) : (Right ? 1f : 0f)-(Left ? 1f : 0f);
    public static void ShiftUp()=>up=true;
    public static void ShiftDown()=>down=true;
    public static bool ConsumeUp(){bool value=up;up=false;return value;}
    public static bool ConsumeDown(){bool value=down;down=false;return value;}
    public static void Reset(){Left=Right=Gas=Brake=Handbrake=Clutch=up=down=false;WheelAxis=0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Initialize()=>Reset();
}
