using System.Collections.Generic;
using UnityEngine;

public sealed class VehicleDamage:MonoBehaviour
{
    CarController car;Rigidbody body;ParticleSystem smoke;int quality=-1;
    readonly Dictionary<MeshFilter,Mesh> meshes=new();
    readonly List<Mesh> owned=new();
    public float Severity{get;private set;}
    public int DeformedMeshCount=>meshes.Count;
    public float TorqueFactor=>Mathf.Lerp(1,.3f,Mathf.InverseLerp(.5f,1,Severity));
    float nextImpact;
    void Start(){car=GetComponent<CarController>();body=GetComponent<Rigidbody>();}
    public void Impact(Collision collision)
    {
        if(Time.time<nextImpact || collision.contactCount==0)return;
        bool vehicle=collision.collider.GetComponentInParent<CarController>();
        float strongest=0;ContactPoint hit=default;
        for(int i=0;i<collision.contactCount;i++)
        {
            var contact=collision.GetContact(i);float normalSpeed=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal));
            if(normalSpeed>strongest){strongest=normalSpeed;hit=contact;}
        }
        if(strongest<7)return;
        nextImpact=Time.time+.18f;Severity=Mathf.Clamp01(Severity+Mathf.InverseLerp(7,40,strongest)*.8f);
        Deform(hit.point,hit.normal,Mathf.Clamp(strongest*.009f,.06f,.42f));
        if(strongest>(car.ForgiveMistakes?20:16))car.BreakImpactWheel(hit.point,hit.normal,strongest);
        if(vehicle && strongest>24)Debris(hit.point,hit.normal,strongest);
    }
    void Deform(Vector3 point,Vector3 normal,float depth)
    {
        int budget=180000;
        foreach(var filter in GetComponentsInChildren<MeshFilter>())
        {
            var source=filter.sharedMesh;if(!source || !source.isReadable || source.vertexCount>budget)continue;
            bool wheel=false;foreach(var mesh in car.wheelMeshes)if(mesh && filter.transform.IsChildOf(mesh)){wheel=true;break;}if(wheel)continue;
            var renderer=filter.GetComponent<Renderer>();if(!renderer || renderer.bounds.SqrDistance(point)>1.6f)continue;
            string name=filter.name.ToLowerInvariant();if(name.Contains("glass") || name.Contains("steer") || name.Contains("wheel"))continue;
            if(!meshes.TryGetValue(filter,out var meshCopy)){meshCopy=Instantiate(source);meshCopy.MarkDynamic();filter.sharedMesh=meshCopy;meshes[filter]=meshCopy;owned.Add(meshCopy);}
            budget-=source.vertexCount;var vertices=meshCopy.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 world=filter.transform.TransformPoint(vertices[i]);float weight=Mathf.Clamp01(1-Vector3.Distance(world,point)/1.25f);
                if(weight>0)vertices[i]=filter.transform.InverseTransformPoint(world+normal*(depth*weight*weight));
            }
            meshCopy.vertices=vertices;meshCopy.RecalculateBounds();meshCopy.RecalculateNormals();
        }
    }
    void Debris(Vector3 point,Vector3 normal,float impact)
    {
        for(int i=0;i<4;i++)
        {
            var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.name="Impact body debris";piece.transform.position=point+normal*.2f+Random.insideUnitSphere*.15f;piece.transform.localScale=new Vector3(Random.Range(.1f,.28f),.035f,Random.Range(.12f,.35f));
            var rigid=piece.AddComponent<Rigidbody>();rigid.mass=.6f;rigid.linearVelocity=body.GetPointVelocity(point)*.65f+normal*Random.Range(1f,4f)+Random.onUnitSphere*3;rigid.angularVelocity=Random.onUnitSphere*18;
            foreach(var collider in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(collider,piece.GetComponent<Collider>());
            Destroy(piece,8);
        }
    }
    void Update()
    {
        if(Severity<.28f)return;
        if(!smoke)
        {
            var go=new GameObject("Damaged engine smoke");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.85f,1.35f);
            smoke=go.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.6f);main.gravityModifier=-.06f;
            var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.25f;
            var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(4,AnimationCurve.EaseInOut(0,.2f,1,1));
            var color=smoke.colorOverLifetime;color.enabled=true;var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});color.color=fade;smoke.Play();
        }
        if(quality!=GameGraphics.Level){quality=GameGraphics.Level;SmokeVisuals.Configure(smoke,true);}
        var emission=smoke.emission;emission.rateOverTime=Mathf.Lerp(12,65,Severity);
    }
    void OnDestroy(){foreach(var mesh in owned)if(mesh)Destroy(mesh);}
}
