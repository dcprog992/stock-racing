using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class SpeedwayOpponent : MonoBehaviour
{
    private sealed class Rival
    {
        public GameObject Vehicle;
        public Rigidbody Body;
        public CarController Car;
        public Vector3 Spawn, EntryOffset, StageTarget, Previous;
        public Quaternion Rotation;
        public float Lane, Height;
        public readonly List<Transform> Wheels = new();
        public Vector3[] Path;
        public float[] SpeedPlan;
        public int Nearest, NextGate, Laps;
        public bool CrossedStart, Staged, Finished;
        public float Pace, ThrottleLimit, LaunchDelay;
        public bool RaceRunning;
        public float PassOffset, PassWanted, FinishTime, BrakeRequest;
        public Rigidbody PassLead;
        public int Slot;
        public Vector3 NavigationPrevious;
        public float Travelled;
        public float StuckTime, RecoveryTime;
        public bool Recovering;
    }
    private struct Sample
    {
        public float Time, Distance, Blend;
        public Vector3 Position;
        public int Index;
    }
    private readonly List<Rival> rivals = new();
    private Sample[] route;
    private int targetLaps;
    private float rollingStart;
    private Vector3 startForward;
    private SpeedwayRace.Gate[] gates;
    private CarController player;
    private bool racing, stopped;
    private double raceStarted;
    private RaceDifficulty.Level difficulty;
    private RaceDifficulty.Profile tuning;
    public RaceDifficulty.Level Difficulty=>difficulty;
    public int CompletedLaps
    {
        get { int count = int.MaxValue; foreach (var r in rivals) count = Mathf.Min(count, r.Laps); return count == int.MaxValue ? 0 : count; }
    }
    public bool Racing => racing;
    public int VehicleCount => rivals.Count;
    public bool ReadyToStart
    {
        get { if (rivals.Count != 3 || stopped) return false; foreach (var r in rivals) if (!r.Staged || r.Body.linearVelocity.magnitude > .3f) return false; return true; }
    }

    public void Initialize(CarController player, SpeedwayRace.Gate start, Transform startMarker = null)
    {
        this.player = player;RacePerformance.Configure(player);
        difficulty=RaceDifficulty.Current;tuning=RaceDifficulty.For(difficulty);
        var race = player.GetComponentInParent<SpeedwayRace>();
        if (race == null) race = FindAnyObjectByType<SpeedwayRace>();
        gates = race.gates;
        LoadRoute(start);
        // Keep the player at the approach position; the player drives up to the gate.
        string[] models = { "Camaro2018", "Chevrolet2017", "Toyota2016" };
        for (int i = 0; i < 3; i++)
        {
            var model = Resources.Load<GameObject>("RivalCars/" + models[i]);
            if (model == null) throw new InvalidOperationException("Не найдены модель или точка соперника " + (i + 1));
            var marker=GameObject.Find("opponent"+(i+1));
            if(!marker)throw new InvalidOperationException("Не найдена точка opponent"+(i+1));
            Vector3 spawn=marker.transform.position;
            float distance=Vector3.Dot(spawn-start.position,startForward);
            // Start at the authored marker, then approach the line in that marker's lane.
            Vector3 spot=spawn-startForward*(distance+6f);
            Surface(spot,out var parking,out var parkingNormal);
            var r = new Rival { Spawn=spawn,Rotation=marker.transform.rotation,Pace=(.985f+i*.01f)*tuning.Speed,Slot=i,ThrottleLimit=tuning.Throttle,LaunchDelay=tuning.Reaction>0?tuning.Reaction+i*.06f:0 };
            r.Vehicle = new GameObject("Соперник " + (i + 1) + " — " + models[i]);
            var visual = Instantiate(model, r.Vehicle.transform);
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            BuildWheels(r);
            if (r.Wheels.Count != 4) throw new InvalidOperationException("Не найдены четыре колеса: " + models[i]);
            r.Lane = Vector3.Dot(r.Spawn - route[0].Position, Vector3.Cross(Vector3.up, startForward));
            r.StageTarget=parking+parkingNormal*.08f;
            Surface(r.Spawn, out var ground, out var normal);
            r.Vehicle.transform.SetPositionAndRotation(ground + normal * .08f,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(marker.transform.forward, normal), normal));
            BuildPhysics(r, player);
            r.Previous = r.Body.position;
            rivals.Add(r);
        }
        PrepareLanes();
        foreach (var r in rivals) { r.Nearest = Nearest(r.Path, r.Body.position, -1); }
    }

    private void BuildPhysics(Rival r, CarController source)
    {
        var sourceBody = source.GetComponent<Rigidbody>();
        var collider = r.Vehicle.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, .82f, 0f);
        collider.size = new Vector3(1.82f, 1.08f, 4.65f);
        foreach (var c in source.GetComponentsInChildren<Collider>())
            if (c is not WheelCollider) { collider.sharedMaterial = c.sharedMaterial; break; }
        r.Body = r.Vehicle.AddComponent<Rigidbody>();
        r.Body.mass = sourceBody.mass;
        r.Body.linearDamping = sourceBody.linearDamping;
        r.Body.angularDamping = sourceBody.angularDamping;
        r.Body.interpolation = RigidbodyInterpolation.Interpolate;
        r.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        r.Body.maxAngularVelocity = sourceBody.maxAngularVelocity;
        r.Body.solverIterations = Mathf.Max(12, sourceBody.solverIterations);
        r.Body.solverVelocityIterations = Mathf.Max(8, sourceBody.solverVelocityIterations);
        r.Car = r.Vehicle.AddComponent<CarController>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), r.Car);
        r.Car.wheelColliders = new WheelCollider[4];
        r.Car.wheelMeshes = new Transform[4];
        // BuildWheels groups rear-left, rear-right, front-left, front-right.
        int[] order = { 2, 3, 0, 1 };
        float radiusTotal = 0f;
        Vector3 axleCenter = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            var mesh = r.Wheels[order[i]];
            Vector3 local = r.Vehicle.transform.InverseTransformPoint(mesh.position);
            Bounds bounds = new Bounds(); bool hasBounds = false;
            foreach (var renderer in mesh.GetComponentsInChildren<MeshRenderer>())
            {
                var b = renderer.localBounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 p = r.Vehicle.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(p);
                }
            }
            var template = source.wheelColliders[i];
            var wheelObject = new GameObject("WheelCollider " + i);
            wheelObject.transform.SetParent(r.Vehicle.transform, false);
            var wheel = wheelObject.AddComponent<WheelCollider>();
            wheel.radius = Mathf.Clamp(Mathf.Max(bounds.size.y, bounds.size.z) * .5f, .28f, .39f);
            local.y = wheel.radius + template.suspensionDistance * template.suspensionSpring.targetPosition;
            wheelObject.transform.localPosition = local;
            axleCenter += local;
            radiusTotal += wheel.radius;
            wheel.mass = template.mass;
            wheel.wheelDampingRate = template.wheelDampingRate;
            wheel.suspensionDistance = template.suspensionDistance;
            wheel.suspensionSpring = template.suspensionSpring;
            wheel.forwardFriction = template.forwardFriction;
            wheel.sidewaysFriction = template.sidewaysFriction;
            wheel.forceAppPointDistance = template.forceAppPointDistance;
            wheel.ConfigureVehicleSubsteps(10f, 8, 6);
            r.Car.wheelColliders[i] = wheel;
            r.Car.wheelMeshes[i] = mesh;
        }
        r.Car.wheelRadius = radiusTotal * .25f;
        r.Car.RivalPowerMultiplier=tuning.Power;RacePerformance.Configure(r.Car,tuning.Power);
        r.Car.centerOfMassOffset = new Vector3(axleCenter.x * .25f, .25f, axleCenter.z * .25f);
        r.Car.ForgiveMistakes = source.ForgiveMistakes;
        r.Car.UseExternalDriver();
        var bars = source.GetComponentsInChildren<AntiRollBar>();
        for (int axle = 0; axle < 2; axle++)
        {
            var bar = r.Vehicle.AddComponent<AntiRollBar>();
            bar.WheelL = r.Car.wheelColliders[axle * 2];
            bar.WheelR = r.Car.wheelColliders[axle * 2 + 1];
            bar.AntiRoll = bars.Length > axle ? bars[axle].AntiRoll : 5000f;
        }
        var sound = source.GetComponent<EngineAudioController>();
        if (sound != null)
        {
            var copy = r.Vehicle.AddComponent<EngineAudioController>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sound), copy);
            copy.car = r.Car; copy.sourceA = copy.sourceB = null;
        }
    }

    public void BeginRace(int laps)
    {
        if (!ReadyToStart) return;
        targetLaps = Mathf.Max(1, laps);
        foreach (var r in rivals)
        {
            r.RaceRunning=true;
            r.Laps = r.NextGate = 0; r.CrossedStart = r.Finished = false;
            r.Previous = r.Body.position; r.Nearest = Nearest(r.Path, r.Body.position, -1);
            r.NavigationPrevious = r.Body.position; r.Travelled = 0f;
            r.EntryOffset = Vector3.Project(r.Body.position - r.Path[r.Nearest], Vector3.Cross(Vector3.up, startForward));
        }
        raceStarted=Time.timeAsDouble;
        racing = true;
    }
    public void StopRace()
    {
        racing = false; stopped = true;
        foreach (var r in rivals) r.Car.SetDriverInputs(0f, 1f, 0f);
    }

    private void FixedUpdate()
    {
        foreach (var r in rivals)
        {
            if(!r.Vehicle.activeInHierarchy)continue;
            if (stopped || r.Car.DetachedWheelCount > 0)
            { r.Car.SetDriverInputs(0f, 1f, 0f); continue; }
            if (!racing)
            {
                Stage(r);
                continue;
            }
            if(Time.timeAsDouble-raceStarted<r.LaunchDelay){r.Car.SetDriverInputs(0,1,0);continue;}
            if(!r.Finished)CountGates(r);
            r.Travelled += Vector3.ProjectOnPlane(r.Body.position - r.NavigationPrevious, Vector3.up).magnitude;
            r.NavigationPrevious = r.Body.position;
            r.Nearest = Nearest(r.Path, r.Body.position, r.Nearest);
            float speed = r.Body.linearVelocity.magnitude;
            float lookahead = Mathf.Clamp(12f + speed * .45f, 12f, 52f);
            Vector3 target = Ahead(r.Path,r.Nearest,lookahead);
            target += r.EntryOffset * (1f-Mathf.SmoothStep(0f,1f,r.Travelled/320f));
            float wanted = r.SpeedPlan[r.Nearest] * r.Pace;
            wanted=PlanTraffic(r,ref target,wanted);
            wanted=SafeDrivingSpeed(r,target,wanted);
            Drive(r, target, wanted);
        }
    }

    private void Stage(Rival r)
    {
        Vector3 delta=r.StageTarget-r.Body.position;
        float distance=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;
        float remaining=Vector3.Dot(delta,startForward);
        float yaw=Vector3.Angle(Vector3.ProjectOnPlane(r.Vehicle.transform.forward,Vector3.up),startForward);
        float speed=r.Body.linearVelocity.magnitude;
        if(r.Staged && (distance>.8f || yaw>4f || speed>.4f))r.Staged=false;
        if(distance<.55f && yaw<3f && speed<.2f && r.Body.angularVelocity.magnitude<.08f)r.Staged=true;
        if(r.Staged){r.Car.SetDriverInputs(0,1,0);r.Car.SetDriverSteeringAngle(0);return;}
        if(remaining<-.45f && speed<1f){r.Car.SetDriverInputs(.16f,0,0,true);return;}
        float wanted=remaining>.15f?Mathf.Min(4f,Mathf.Sqrt(2f*1.2f*Mathf.Max(0,remaining-.15f))):0f;
        Drive(r,r.StageTarget+startForward*6f,wanted);
    }

    private void CountGates(Rival r)
    {
        var gate = gates[r.NextGate];
        Vector3 from = r.Previous, to = r.Body.position;
        from.y = to.y = gate.position.y;
        bool grounded = false;
        foreach (var wheel in r.Car.wheelColliders) grounded |= wheel.isGrounded;
        if (grounded && gate.Crossed(from, to))
        {
            if (r.NextGate == 0)
            {
                if (r.CrossedStart) { r.Laps++; if (r.Laps >= targetLaps) {r.Finished = true;r.FinishTime=(float)(Time.fixedTimeAsDouble-raceStarted);} }
                r.CrossedStart = true;
            }
            r.NextGate = (r.NextGate + 1) % gates.Length;
        }
        r.Previous = r.Body.position;
    }

    private float PlanTraffic(Rival r,ref Vector3 target,float wanted)
    {
        Vector3 tangent=Vector3.ProjectOnPlane(Ahead(r.Path,r.Nearest,12)-r.Path[r.Nearest],Vector3.up).normalized;
        Vector3 right=Vector3.Cross(Vector3.up,tangent);
        float speed=Mathf.Max(0,Vector3.Dot(r.Body.linearVelocity,tangent));
        Rigidbody lead=null;float gap=float.PositiveInfinity;
        for(int i=-1;i<rivals.Count;i++)
        {
            var other=i<0?player.GetComponent<Rigidbody>():rivals[i].Body;if(other==r.Body)continue;
            RelativeToLine(r,other.position,out float z,out float x);
            float own=Vector3.Dot(r.Body.position-r.Path[r.Nearest],right);
            if(z>0 && z<18+speed*.9f && Mathf.Abs(x-own)<2.25f && z<gap){lead=other;gap=z;}
        }
        float desiredOffset=0;r.BrakeRequest=0;
        if(r.PassLead && Mathf.Abs(r.PassWanted)>.1f)
        {
            RelativeToLine(r,r.PassLead.position,out float leadAlong,out _);
            if(leadAlong>-14f && leadAlong<220f)desiredOffset=r.PassWanted;
            else r.PassLead=null;
        }
        if(lead)
        {
            float leadSpeed=Mathf.Max(0,Vector3.Dot(lead.linearVelocity,tangent));
            float closing=speed-leadSpeed;
            float safe=7f+speed*tuning.Headway;
            if(gap<safe+15f)wanted=Mathf.Min(wanted,Mathf.Max(0,leadSpeed+(gap-safe)*.65f));
            if(closing>2f && gap/closing<.9f)r.BrakeRequest=Mathf.Clamp01((.9f-gap/closing)*2f);
            if(leadSpeed<8f || closing>tuning.PassClosing || r.PassLead==lead)
            {
                float side=r.PassWanted!=0?Mathf.Sign(r.PassWanted):(r.Slot==2?-1:1);
                for(int attempt=0;attempt<2;attempt++)
                {
                    float candidate=side*(attempt==0?3.1f:-3.1f);
                    bool free=true;
                    for(int i=-1;i<rivals.Count;i++)
                    {
                        var other=i<0?player.GetComponent<Rigidbody>():rivals[i].Body;if(other==r.Body)continue;
                        RelativeToLine(r,other.position+other.linearVelocity*.6f,out float along,out float lateral);
                        if(Mathf.Abs(lateral-candidate)<2.5f && Mathf.Abs(along-speed*.6f)<18f){free=false;break;}
                    }
                    if(!free || !SafeCorridor(r.Body.position+right*candidate,target+right*candidate))continue;
                    desiredOffset=candidate;r.PassLead=lead;break;
                }
                // Lift while the pass is being prepared; emergency braking is reserved for a short closing gap.
                if(Mathf.Abs(r.PassOffset-desiredOffset)>.6f || desiredOffset==0)
                    wanted=Mathf.Min(wanted,Mathf.Max(0,leadSpeed+(gap-safe)*.8f));
                if(gap<7f && speed>leadSpeed+3f)wanted=Mathf.Min(wanted,Mathf.Max(0,leadSpeed-3f));
            }
        }
        else if(Mathf.Abs(r.PassOffset)>.1f && desiredOffset==0)
        {
            // Do not pull back across a car that is still alongside or just behind.
            for(int i=-1;i<rivals.Count;i++)
            {
                var other=i<0?player.GetComponent<Rigidbody>():rivals[i].Body;if(other==r.Body)continue;
                Vector3 delta=other.position-r.Body.position;
                if(Mathf.Abs(Vector3.Dot(delta,tangent))<13f && Mathf.Abs(Vector3.Dot(delta,right))<4.5f){desiredOffset=r.PassWanted;break;}
            }
        }
        r.PassWanted=desiredOffset;
        r.PassOffset=Mathf.MoveTowards(r.PassOffset,desiredOffset,1.45f*Time.fixedDeltaTime);
        target+=right*r.PassOffset;
        return wanted;
    }
    private static void RelativeToLine(Rival r,Vector3 position,out float along,out float lateral)
    {
        int nearest=r.Nearest,step=0;float best=float.PositiveInfinity;
        for(int j=-30;j<=75;j++)
        {
            int index=(r.Nearest+j+r.Path.Length)%r.Path.Length;Vector3 delta=position-r.Path[index];delta.y=0;
            if(delta.sqrMagnitude<best){best=delta.sqrMagnitude;nearest=index;step=j;}
        }
        Vector3 tangent=Vector3.ProjectOnPlane(r.Path[(nearest+1)%r.Path.Length]-r.Path[(nearest+r.Path.Length-1)%r.Path.Length],Vector3.up).normalized;
        along=step*2f;lateral=Vector3.Dot(position-r.Path[nearest],Vector3.Cross(Vector3.up,tangent));
    }

    private static void Drive(Rival r, Vector3 target, float wanted)
    {
        Vector3 local = r.Vehicle.transform.InverseTransformPoint(target);
        float forwardSpeed = Vector3.Dot(r.Body.linearVelocity, r.Vehicle.transform.forward);
        if (r.Recovering)
        {
            r.RecoveryTime -= Time.fixedDeltaTime;
            bool backing = r.RecoveryTime > 0f;
            r.Car.SetDriverInputs(backing && forwardSpeed > -3.5f ? .35f : 0f,
                backing ? (forwardSpeed < -3.5f ? .4f : 0f) : 1f,
                backing ? -Mathf.Sign(local.x) * .8f : 0f, true);
            if (!backing && r.Body.linearVelocity.magnitude < .4f) { r.Recovering = false; r.StuckTime = 0f; }
            return;
        }
        r.StuckTime = wanted > 6f && r.Body.linearVelocity.magnitude < .7f ? r.StuckTime + Time.fixedDeltaTime : 0f;
        if (r.StuckTime > 2f) { r.Recovering = true; r.RecoveryTime = 2.5f; return; }
        float wheelbase = Mathf.Abs(r.Car.wheelColliders[0].transform.localPosition.z - r.Car.wheelColliders[2].transform.localPosition.z);
        float angle = Mathf.Atan2(2f * wheelbase * local.x, local.x * local.x + local.z * local.z) * Mathf.Rad2Deg;
        if(r.Staged && forwardSpeed>12f)
        {
            float side=Vector3.Dot(r.Body.linearVelocity,r.Vehicle.transform.right);
            float beta=Mathf.Atan2(side,forwardSpeed);
            float wantedYaw=forwardSpeed*Mathf.Tan(angle*Mathf.Deg2Rad)/Mathf.Max(.5f,wheelbase);
            float actualYaw=Vector3.Dot(r.Body.angularVelocity,r.Vehicle.transform.up);
            angle+=beta*Mathf.Rad2Deg*.10f+Mathf.Atan2(wheelbase*(wantedYaw-actualYaw),forwardSpeed)*Mathf.Rad2Deg*1f;
        }
        float speedLimit = Mathf.Atan2((r.Car.ForgiveMistakes ? 14f : 23f) * wheelbase, r.Body.linearVelocity.sqrMagnitude) * Mathf.Rad2Deg;
        float steering = local.z < 0f ? Mathf.Sign(local.x) : angle / Mathf.Max(.1f, Mathf.Min(r.Car.maxSteerAngle, speedLimit));
        float speed = Vector3.Dot(r.Body.linearVelocity, r.Vehicle.transform.forward);
        float error = wanted - speed;
        float throttle = Mathf.Min(r.RaceRunning?r.ThrottleLimit:1f,Mathf.Clamp01(error * (wanted < 6f ? .45f : .32f)));
        // On a clear oval regulate pace with throttle. Brake only for an imminent obstruction or recovery.
        float brake = Mathf.Max(r.BrakeRequest,wanted < 12f && speed>wanted+2f ? Mathf.Clamp01((speed-wanted)*.12f) : 0f);
        if(r.RaceRunning && wanted>12f && speed>wanted+2f)brake=Mathf.Max(brake,Mathf.Clamp((speed-wanted-2f)*.12f,0,.7f));
        if (wanted < .1f) { throttle = 0f; brake = 1f; }
        r.Car.SetDriverInputs(throttle, brake, steering);
        float authority=Mathf.Min(r.Car.maxSteerAngle,Mathf.Atan2(32f*wheelbase,Mathf.Max(1,r.Body.linearVelocity.sqrMagnitude))*Mathf.Rad2Deg);
        r.Car.SetDriverSteeringAngle(Mathf.Clamp(angle,-authority,authority));
    }

    private static int Nearest(Vector3[] path, Vector3 p, int previous)
    {
        int result = 0; float best = float.PositiveInfinity;
        int begin = previous < 0 ? 0 : previous - 8;
        int end = previous < 0 ? path.Length : previous + 36;
        for (int j = begin; j < end; j++)
        {
            int i = (j % path.Length + path.Length) % path.Length;
            float d = Vector3.ProjectOnPlane(path[i] - p, Vector3.up).sqrMagnitude;
            if (d < best) { best = d; result = i; }
        }
        return result;
    }
    private static Vector3 Ahead(Vector3[] path, int index, float distance)
    {
        for (int n = 0; n < path.Length; n++)
        {
            int next = (index + 1) % path.Length;
            float segment = Vector3.Distance(path[index], path[next]);
            if (segment >= distance) return Vector3.Lerp(path[index], path[next], distance / Mathf.Max(segment, .001f));
            distance -= segment; index = next;
        }
        return path[index];
    }
    private static Vector3[] SmoothPath(Vector3[] original)
    {
        var points = new List<Vector3>();
        Vector3 previous = original[0]; float carry = 0f;
        points.Add(previous);
        for (int i = 1; i <= original.Length; i++)
        {
            Vector3 next = original[i % original.Length];
            float length = Vector3.Distance(previous, next);
            while (carry + length >= 4f)
            {
                float step = 4f - carry;
                previous = Vector3.Lerp(previous, next, step / Mathf.Max(.001f, length));
                points.Add(previous); length -= step; carry = 0f;
            }
            carry += length; previous = next;
        }
        var result = new Vector3[points.Count];
        for (int pass = 0; pass < 12; pass++)
        {
            for (int i = 0; i < result.Length; i++) result[i] = (points[(i + result.Length - 1) % result.Length] + points[i] * 2f + points[(i + 1) % result.Length]) * .25f;
            points = new List<Vector3>(result);
        }
        return points.ToArray();
    }

    // Route preparation and model wheel grouping follow below.
    public float BestRecordedLapSeconds => route == null ? 0f : route[^1].Time;

    private void PrepareLanes()
    {
        // Trim the standing-start tail: the loop begins and ends at the same start-line crossing.
        var first=0;var last=route.Length-1;
        for(int i=1;i<route.Length;i++)
        {
            Vector3 a=route[i-1].Position,b=route[i].Position;a.y=b.y=gates[0].position.y;
            if(gates[0].Crossed(a,b)){if(first==0)first=i;last=i;}
        }
        var recorded=new List<Vector3>();
        for(int i=first;i<last;i++)recorded.Add(route[i].Position);
        if(recorded.Count<20)throw new InvalidDataException("Recorded lap is incomplete");
        var guide=ResampleRecorded(recorded.ToArray());
        JoinStandingStartLoop(guide);
        foreach(var rival in rivals){rival.Path=new Vector3[guide.Length];rival.SpeedPlan=new float[guide.Length];}
        for(int i=0;i<guide.Length;i++)
        {
            Vector3 tangent=Vector3.ProjectOnPlane(guide[(i+2)%guide.Length]-guide[(i+guide.Length-2)%guide.Length],Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,tangent);
            Vector3 center=guide[i];
            if(!ClearSurface(center,out var ground,out _))
            {
                bool found=false;
                for(float d=.5f;d<=8 && !found;d+=.5f)foreach(float sign in new[]{-1f,1f})
                    if(ClearSurface(center+right*d*sign,out ground,out _)){found=true;break;}
                if(!found)throw new InvalidDataException("Recorded guide left the track");
            }
            center=ground;
            float low=0,high=0;
            while(low>-16 && ClearSurface(center+right*(low-.5f),out _,out _))low-=.5f;
            while(high<16 && ClearSurface(center+right*(high+.5f),out _,out _))high+=.5f;
            // Move the three-lane group only as far as a road edge requires, never to the road centre.
            float spacing=Mathf.Min(2.8f,Mathf.Max(0,(high-low-.4f)*.5f));
            float group=Mathf.Clamp(0,low+spacing+.2f,high-spacing-.2f);
            foreach(var rival in rivals)
            {
                // Order agrees with the spawn grid, avoiding a crossover immediately after launch.
                int order=0;foreach(var other in rivals)if(other.Lane<rival.Lane-.01f || (Mathf.Abs(other.Lane-rival.Lane)<=.01f && other.Slot<rival.Slot))order++;
                Vector3 p=center+right*(group+(order-1)*spacing);
                Surface(p,out p,out _);rival.Path[i]=p;
                rival.SpeedPlan[i]=88.5f;
            }
        }
        BuildCornerSpeedPlans();
        foreach(var rival in rivals)rival.EntryOffset=Vector3.ProjectOnPlane(rival.Spawn-rival.Path[0],Vector3.up);
    }
    private void BuildCornerSpeedPlans()
    {
        foreach(var r in rivals)
        {
            int n=r.Path.Length;
            for(int i=0;i<n;i++)
            {
                Vector3 a=r.Path[(i+n-12)%n],b=r.Path[i],c=r.Path[(i+12)%n];
                Vector3 ab=b-a,bc=c-b;ab.y=bc.y=0;
                float curve=Vector3.Angle(ab,bc)*Mathf.Deg2Rad/Mathf.Max(1,(ab.magnitude+bc.magnitude)*.5f);
                r.SpeedPlan[i]=Mathf.Min(88.5f,Mathf.Sqrt(20f/Mathf.Max(.0001f,curve))/r.Pace);
            }
            for(int pass=0;pass<3;pass++)for(int i=n-1;i>=0;i--)
                r.SpeedPlan[i]=Mathf.Min(r.SpeedPlan[i],Mathf.Sqrt(r.SpeedPlan[(i+1)%n]*r.SpeedPlan[(i+1)%n]+2*2.5f*Vector3.Distance(r.Path[i],r.Path[(i+1)%n])));
        }
    }
    private static Vector3[] ResampleRecorded(Vector3[] source)
    {
        var points=new List<Vector3>();Vector3 previous=source[0];float carry=0;points.Add(previous);
        for(int i=1;i<=source.Length;i++)
        {
            Vector3 next=source[i%source.Length];float length=Vector3.Distance(previous,next);
            while(carry+length>=2f){float step=2f-carry;previous=Vector3.Lerp(previous,next,step/Mathf.Max(.001f,length));points.Add(previous);length-=step;carry=0;}
            carry+=length;previous=next;
        }
        // A single half-metre-scale noise filter, not twelve passes that alter the racing line.
        var filtered=new Vector3[points.Count];
        for(int i=0;i<points.Count;i++)filtered[i]=points[i]*.8f+(points[(i+points.Count-1)%points.Count]+points[(i+1)%points.Count])*.1f;
        return filtered;
    }
    private static void JoinStandingStartLoop(Vector3[] guide)
    {
        int n=guide.Length,k=Mathf.Min(90,n/8),from=n-k,to=k;
        Vector3 a=guide[from],b=guide[to];
        float length=Vector3.Distance(a,b);
        Vector3 da=(guide[from+3]-guide[from-3]).normalized*length;
        Vector3 db=(guide[to+3]-guide[to-3]).normalized*length;
        // Only the standing-start seam is bridged. The rest of the recorded lap stays intact.
        for(int j=0;j<=2*k;j++)
        {
            float t=j/(float)(2*k),t2=t*t,t3=t2*t;
            guide[(from+j)%n]=(2*t3-3*t2+1)*a+(t3-2*t2+t)*da+(-2*t3+3*t2)*b+(t3-t2)*db;
        }
    }

    private void LoadRoute(SpeedwayRace.Gate start)
    {
        startForward = Vector3.ProjectOnPlane(start.forward,Vector3.up).normalized;
        var asset = Resources.Load<TextAsset>("RivalCars/BestStandingLap");
        route = asset ? ReadReplay(asset.bytes) : null;
        string saved = Path.Combine(Application.persistentDataPath,$"{gameObject.scene.name}.1L.standing-start.ghost");
        if (File.Exists(saved))
        {
            try
            {
                var current = ReadReplay(File.ReadAllBytes(saved));
                if (current != null && (route == null || current[^1].Time < route[^1].Time)) route = current;
            }
            catch (IOException) { }
        }
        if (route == null) throw new InvalidDataException("Нет корректной записи полного лучшего круга.");
    }

    private Sample[] ReadReplay(byte[] bytes)
    {
        if (bytes.Length < 8) return null;
        using var reader = new BinaryReader(new MemoryStream(bytes,false));
        if (reader.ReadInt32() != 1) return null;
        int count = reader.ReadInt32();
        if(count < 3 || count > 18001 || bytes.Length != 8L + count*32L)return null;
        var result = new Sample[count];
        int nextGate = 0, crossings = 0;
        for(int i=0;i<count;i++)
        {
            float time=reader.ReadSingle();
            Vector3 p=new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            for(int q=0;q<4;q++)reader.ReadSingle();
            if(!float.IsFinite(time)||!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)||(i>0 && time<=result[i-1].Time))return null;
            result[i]=new Sample{Time=time,Position=p};
            if(i>0)
            {
                Vector3 from=result[i-1].Position,to=p;from.y=to.y=gates[nextGate].position.y;
                if(gates[nextGate].Crossed(from,to)){crossings++;nextGate=(nextGate+1)%gates.Length;}
            }
        }
        return crossings>=gates.Length+1 && result[^1].Time>15f ? result : null;
    }

    public float MaximumRouteError { get; private set; }
    public float OffRoadSeconds { get; private set; }

    private float SafeDrivingSpeed(Rival r, Vector3 target, float wanted)
    {
        float error=RouteError(r);
        if(r.Travelled>320f)MaximumRouteError=Mathf.Max(MaximumRouteError,error);
        if(!TrySurface(r.Body.position,out _,out _)){OffRoadSeconds+=Time.fixedDeltaTime;wanted=Mathf.Min(wanted,8f);}
        // Large departures need recovery; banking and ordinary slip are not reasons to brake.
        if(error>8f && r.Travelled>320f)wanted=Mathf.Min(wanted,Mathf.Max(12f,60f-error*3f));
        return wanted;
    }

    private static float RouteError(Rival r)
    {
        Vector3 tangent=Vector3.ProjectOnPlane(r.Path[(r.Nearest+1)%r.Path.Length]-r.Path[(r.Nearest+r.Path.Length-1)%r.Path.Length],Vector3.up).normalized;
        Vector3 p=r.Body.position-Vector3.Cross(Vector3.up,tangent)*r.PassOffset;p.y=0f;
        float best=float.PositiveInfinity;
        for(int j=-1;j<=0;j++)
        {
            int a=(r.Nearest+j+r.Path.Length)%r.Path.Length,b=(a+1)%r.Path.Length;
            Vector3 from=r.Path[a],to=r.Path[b];from.y=to.y=0f;
            Vector3 delta=to-from;
            Vector3 closest=from+delta*Mathf.Clamp01(Vector3.Dot(p-from,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
            best=Mathf.Min(best,Vector3.Distance(p,closest));
        }
        return best;
    }

    private bool SafeCorridor(Vector3 from,Vector3 to)
    {
        for(int i=1;i<=5;i++)if(!ClearSurface(Vector3.Lerp(from,to,i/5f),out _,out _))return false;
        return true;
    }

    private static readonly Collider[] barrierHits = new Collider[32];
    private bool ClearSurface(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        if (!TrySurface(p, out point, out normal)) return false;
        int count = Physics.OverlapSphereNonAlloc(point + normal * .7f, tuning.Clearance, barrierHits);
        for (int i = 0; i < count; i++)
            for (var node = barrierHits[i].transform; node != null; node = node.parent)
                if (node.name.Contains("barrier", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
    private Sample At(float time)
    {
        int lo = 0, hi = route.Length - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (route[mid].Time <= time) lo = mid; else hi = mid; }
        float t = Mathf.InverseLerp(route[lo].Time, route[hi].Time, time);
        return new Sample { Time = time, Position = Vector3.Lerp(route[lo].Position, route[hi].Position, t),
            Distance = Mathf.Lerp(route[lo].Distance, route[hi].Distance, t), Index = lo, Blend = t };
    }

    private Vector3 DirectionAt(float time)
    {
        Vector3 direction = At(Mathf.Min(time + .25f, route[^1].Time)).Position - At(Mathf.Max(0f, time - .1f)).Position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > .00001f ? direction.normalized : startForward;
        float join = Mathf.Max(1f - Mathf.Clamp01((time - rollingStart) / 1.5f),
            Mathf.Clamp01((time - (route[^1].Time - .75f)) / .75f));
        return Vector3.Lerp(direction, startForward, join).normalized;
    }

    private static void Surface(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        if (TrySurface(p, out point, out normal)) return;
        throw new InvalidOperationException("Траектория соперника вышла за полотно: " + p);
    }

    private static bool TrySurface(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        // RaycastAll исключает кузова машин, даже когда они рядом с точкой запроса.
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 20f, Vector3.down, 45f))
            if (hit.collider.name == "Track collision" && hit.normal.y > .5f) { point = hit.point; normal = hit.normal; return true; }
        point = default; normal = Vector3.up; return false;
    }

    private static void BuildWheels(Rival rival)
    {
        var groups = new List<Transform>[4];
        for (int i = 0; i < 4; i++) groups[i] = new List<Transform>();
        foreach (var renderer in rival.Vehicle.GetComponentsInChildren<MeshRenderer>())
        {
            var b = renderer.bounds;
            string part = renderer.name;
            bool wheelPart = part.StartsWith("Wheel_", StringComparison.Ordinal) ||
                part.Contains("TYRE", StringComparison.OrdinalIgnoreCase) ||
                part.Contains("ROTOR", StringComparison.OrdinalIgnoreCase);
            if (!wheelPart || part.Contains("STEERING", StringComparison.OrdinalIgnoreCase)) continue;
            groups[(b.center.x > 0f ? 1 : 0) + (b.center.z > 0f ? 2 : 0)].Add(renderer.transform);
        }
        for (int i = 0; i < 4; i++)
        {
            if (groups[i].Count == 0) continue;
            Bounds bounds = groups[i][0].GetComponent<MeshRenderer>().bounds;
            foreach (var part in groups[i]) bounds.Encapsulate(part.GetComponent<MeshRenderer>().bounds);
            var pivot = new GameObject("Wheel pivot " + i).transform;
            pivot.SetParent(rival.Vehicle.transform.GetChild(0), false);
            pivot.position = bounds.center;
            foreach (var part in groups[i]) part.SetParent(pivot, true);
            rival.Wheels.Add(pivot);
        }
    }

    public struct Result
    {
        public string Name;public float Time;public bool Finished;public int Laps;public float Progress;
    }
    public Result[] GetResults()
    {
        var results=new Result[rivals.Count];
        for(int i=0;i<rivals.Count;i++){var r=rivals[i];results[i]=new Result{Name=r.Vehicle.name.Replace("Соперник "+(i+1)+" — ",""),Time=r.FinishTime,Finished=r.Finished,Laps=r.Laps,Progress=r.NextGate+r.Nearest/(float)r.Path.Length};}
        return results;
    }
    private void OnDestroy()
    {
        foreach (var rival in rivals) if (rival.Vehicle != null)
        {
            if (Application.isPlaying) Destroy(rival.Vehicle);
            else DestroyImmediate(rival.Vehicle);
        }
        rivals.Clear();
    }
}




