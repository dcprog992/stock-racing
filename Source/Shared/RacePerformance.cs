using UnityEngine;

public static class RacePerformance
{
    public const float PlayerTorque=755f*1.30f*.90f*.88f;
    static PhysicsMaterial contact;
    public static void Configure(CarController car,float multiplier=1f)
    {
        car.engineTorque=PlayerTorque*multiplier;
        car.clutchCapacity=1170f*multiplier;
        car.gearRatios=new[]{2.4f,1.78f,1.4f,1.16f,1f,.90f};
        car.finalDrive=3.35f;car.autoUpshiftRPM=8000;car.autoDownshiftRPM=4400;
        car.shiftTime=.12f;car.autoShiftCooldown=.5f;
        car.aeroDrag=.32f;car.TopSpeedKmh=320f;
        if(!contact)contact=new PhysicsMaterial("Stock Racing sliding body contact")
        {dynamicFriction=0,staticFriction=0,bounciness=.04f,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
        foreach(var collider in car.GetComponentsInChildren<Collider>())
            if(collider is not WheelCollider){collider.sharedMaterial=contact;collider.contactOffset=.025f;}
    }
}

