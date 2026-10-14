// Copy into Assets/Editor of an isolated project, then run Unity with
// -batchmode -executeMethod PoliceIntegrationChecks.Run -logFile <path>.
// Uses a separate physics scene to exercise real front, rear and side contacts.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PoliceIntegrationChecks
{
    const string Key="PoliceIntegrationChecks";
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double deadline;
    static PoliceChaseManager manager;
    static BusController drive;
    static Rigidbody bus;
    static BusCarCrash crash;
    static BusDamage damage;
    static BoxCollider box;
    static Scene isolated;
    static PhysicsScene physics;
    static int errors;
    static PoliceIntegrationChecks()
    {
        deadline=EditorApplication.timeSinceStartup+120;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=> { if(SessionState.GetInt(Key,0)>0 && (t==LogType.Exception||t==LogType.Error) && !s.Contains("UnityEditor.Search."))errors++; };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        SessionState.SetInt(Key,1);
        EditorApplication.EnterPlaymode();
    }
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Field(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static void Property(object o,string name,object value)=>Field(o,"<"+name+">k__BackingField",value);
    static void Check(bool value,string text){if(!value)throw new Exception("POLICE_CHECK_FAILED: "+text);Debug.Log("POLICE_CHECK_OK: "+text);}
    static PoliceCarAI Car(Vector3 at,Quaternion rotation,bool ram=false)
    {
        var car=(PoliceCarAI)Call(manager,"CreateCar");
        ((List<PoliceCarAI>)manager.Cars).Add(car);
        car.StartChase();
        car.transform.SetParent(null);
        if(isolated.IsValid()) SceneManager.MoveGameObjectToScene(car.gameObject,isolated);
        car.transform.SetPositionAndRotation(at,rotation);
        var rb=car.GetComponent<Rigidbody>();rb.position=at;rb.rotation=rotation;
        Property(car,"IsRammingBus",ram);
        Property(car,"Speed",12f);
        car.SeesBus=true;
        Field(car,"yaw",rotation.eulerAngles.y);
        return car;
    }
    static void ResetBus(Quaternion rotation)
    {
        bus.position=new Vector3(0,100,0);bus.rotation=rotation;
        bus.transform.SetPositionAndRotation(bus.position,rotation);
        bus.linearVelocity=Vector3.zero;bus.angularVelocity=Vector3.zero;
        damage.ExtinguishFire();Field(damage,"lastHitTime",-999f);
        Physics.SyncTransforms();
    }
    static void Remove(PoliceCarAI car){UnityEngine.Object.DestroyImmediate(car.gameObject);}
    static void Tick()
    {
        if(SessionState.GetInt(Key,0)==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Police checks timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1f)return;
            SessionState.SetInt(Key,2);
            manager=UnityEngine.Object.FindAnyObjectByType<PoliceChaseManager>();
            drive=UnityEngine.Object.FindAnyObjectByType<BusController>();
            bus=drive.GetComponent<Rigidbody>();crash=drive.GetComponent<BusCarCrash>();
            damage=drive.GetComponent<BusDamage>();box=drive.GetComponent<BoxCollider>();
            manager.enabled=false;drive.enabled=false;
            foreach(var car in new List<PoliceCarAI>(manager.Cars))Remove(car);
            ((List<PoliceCarAI>)manager.Cars).Clear();
            Vector3 forward=bus.rotation*Vector3.forward;
            Field(manager,"patrolCount",0);Field(manager,"calmUntil",0f);Field(manager,"chasing",false);Field(manager,"suspicion",0f);
            var observer=Car(bus.position-forward*18,Quaternion.LookRotation(forward));
            Property(observer,"State",PoliceCarAI.Mode.Patrol);
            bus.linearVelocity=Vector3.zero;Call(manager,"FixedUpdate");
            Check(!manager.ChaseStarted,"A visible stationary bus does not trigger a pursuit");
            bus.linearVelocity=forward*20;observer.Blind(5);Call(manager,"FixedUpdate");
            Check(!manager.ChaseStarted,"A blinded officer cannot detect speeding");
            Field(observer,"blindUntil",0f);Field(manager,"suspicion",.35f);Call(manager,"FixedUpdate");
            Check(manager.ChaseStarted,"An officer who sees speeding starts a pursuit");
            Remove(observer);Field(manager,"chasing",false);
            bus.linearVelocity=forward*8f;
            var front=Car(bus.position+forward*18,Quaternion.LookRotation(-forward));
            Call(front,"FixedUpdate");
            Check(!front.IsRammingBus,"Head-on officer cannot ram");
            Vector3 goal=(Vector3)Call(front,"PursuitPoint",front.transform.position,forward);
            Check(Mathf.Abs(Vector3.Dot(goal-bus.position,Vector3.Cross(Vector3.up,forward)))>=8f,"Officer in front moves to flank");
            Remove(front);
            bus.linearVelocity=Vector3.zero;
            var parkedTarget=Car(bus.position-forward*18,Quaternion.LookRotation(forward));
            Property(parkedTarget,"Speed",0f);
            Call(parkedTarget,"FixedUpdate");
            Check(parkedTarget.IsRammingBus&&parkedTarget.Speed>0f,"Officer accelerates into a rear attack against a parked bus");
            var parkedBody=parkedTarget.GetComponent<Rigidbody>();
            parkedBody.position=bus.position-forward*(manager.BusHalfLength+parkedTarget.HalfLength+3f);
            parkedTarget.transform.position=parkedBody.position;
            Field(parkedTarget,"nextRam",Time.time+3f);Property(parkedTarget,"Speed",0f);
            Call(parkedTarget,"FixedUpdate");
            Check(!parkedTarget.IsRammingBus&&parkedTarget.Speed==0f,"Officer waits behind a stopped bus during cooldown instead of circling");
            Field(parkedTarget,"nextRam",0f);Call(parkedTarget,"FixedUpdate");
            Check(parkedTarget.IsRammingBus,"Officer can attack a stopped bus again after cooldown");
            Remove(parkedTarget);
            bus.linearVelocity=forward*8f;
            var rear=Car(bus.position-forward*18,Quaternion.LookRotation(forward));
            Call(rear,"FixedUpdate");
            Check(rear.IsRammingBus,"Aligned faster officer can catch bus from behind");
            var second=Car(bus.position-forward*23,Quaternion.LookRotation(forward));
            Call(second,"FixedUpdate");
            Check(!second.IsRammingBus,"Only one officer attacks at a time");
            manager.RearRamContact();Call(rear,"FixedUpdate");
            Check(!rear.IsRammingBus,"Shared recovery prevents repeated rams");
            Remove(rear);Remove(second);
            bus.linearVelocity=forward*100f;Property(manager,"BusInSight",true);Field(manager,"chasing",true);Field(manager,"heat",1);
            Check(manager.ChaseSpeed<40f,"Police catch-up speed is capped");
            Check((int)manager.GetType().GetProperty("PursuitLimit",Flags).GetValue(manager)==2,"Initial pursuit limited to two officers");

            isolated=SceneManager.CreateScene("Police physical collision checks",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics=isolated.GetPhysicsScene();
            bus.transform.SetParent(null);SceneManager.MoveGameObjectToScene(bus.gameObject,isolated);
            bus.useGravity=false;bus.constraints=RigidbodyConstraints.FreezeRotation;
            foreach(float yaw in new[]{0f,90f})
            {
                ResetBus(Quaternion.Euler(0,yaw,0));forward=bus.rotation*Vector3.forward;
                Vector3 nose=box.transform.TransformPoint(box.center+Vector3.forward*box.size.z*.5f);
                var cop=Car(nose+forward*2.5f,Quaternion.LookRotation(-forward),true);
                bus.linearVelocity=forward*10f;
                int heat=manager.HeatLevel, count=crash.CarsExploded;
                for(int i=0;i<10&&!cop.IsDestroyed;i++){Call(crash,"FixedUpdate");physics.Simulate(.02f);}
                Check(cop.IsDestroyed,"Front impact destroys charging officer, rotation "+yaw);
                Check(damage.DamagePenalty==0f,"Front impact does not damage bus");
                Check(crash.CarsExploded==count+1,"Explosion counted once");
                Check(manager.HeatLevel==Mathf.Min(5,heat+1),"Destroyed officer raises alert");
                Remove(cop);
            }
            ResetBus(Quaternion.identity);
            Vector3 frontBumper=box.transform.TransformPoint(box.center+Vector3.forward*box.size.z*.5f);
            var incoming=Car(frontBumper+Vector3.forward*2.5f,Quaternion.LookRotation(Vector3.back),true);
            var incomingBody=incoming.GetComponent<Rigidbody>();
            int frontCount=crash.CarsExploded;
            for(int i=0;i<30&&!incoming.IsDestroyed;i++)
            {
                incomingBody.MovePosition(incomingBody.position+Vector3.back*8f*.02f);
                Call(crash,"FixedUpdate");physics.Simulate(.02f);
            }
            Check(incoming.IsDestroyed&&crash.CarsExploded==frontCount+1,"Physical head-on hit destroys officer even with stationary bus");
            Check(damage.DamagePenalty==0f,"Collision callback order cannot cause frontal damage");
            Remove(incoming);
            ResetBus(Quaternion.identity);
            Vector3 tail=box.transform.TransformPoint(box.center-Vector3.forward*box.size.z*.5f);
            var behind=Car(tail-Vector3.forward*2.5f,Quaternion.identity,true);
            var copBody=behind.GetComponent<Rigidbody>();
            bus.linearVelocity=Vector3.forward*8f;
            int before=crash.CarsExploded;
            for(int i=0;i<40&&damage.DamagePenalty==0f;i++)
            {
                copBody.MovePosition(copBody.position+Vector3.forward*12f*.02f);
                Call(crash,"FixedUpdate");physics.Simulate(.02f);
            }
            Check(!behind.IsDestroyed&&crash.CarsExploded==before,"Rear pursuit collision preserves officer");
            Check(damage.HitsTaken==1&&Mathf.Abs(damage.DamagePenalty-.05f)<.001f,"Rear impact counts once and reduces speed by five percent");
            Remove(behind);
            ResetBus(Quaternion.identity);
            tail=box.transform.TransformPoint(box.center-Vector3.forward*box.size.z*.5f);
            var stationaryRear=Car(tail-Vector3.forward*2.5f,Quaternion.identity,true);
            copBody=stationaryRear.GetComponent<Rigidbody>();
            for(int i=0;i<40&&damage.DamagePenalty==0f;i++)
            {
                copBody.MovePosition(copBody.position+Vector3.forward*3f*.02f);
                Call(crash,"FixedUpdate");physics.Simulate(.02f);
            }
            Check(damage.HitsTaken==1,"Stationary bus takes rear impact damage");
            Check((float)stationaryRear.GetType().GetField("ramRetreatUntil",Flags).GetValue(stationaryRear)>Time.time,"Officer backs off after impact to permit a later attack");
            Remove(stationaryRear);

            ResetBus(Quaternion.identity);
            Field(manager,"chasing",false);Field(manager,"heat",0);
            var civilian=new GameObject("Civilian collision test"){tag="voitures"};
            SceneManager.MoveGameObjectToScene(civilian,isolated);
            var civilianCollider=civilian.AddComponent<BoxCollider>();
            civilian.transform.position=bus.position+Vector3.forward*10;
            Field(crash,"lastSpeedKmh",80f);
            Check((bool)Call(crash,"TryDestroyCar",civilianCollider,civilian.transform.position,80f),"Civilian car is destroyed by actual crash handler");
            Check(!manager.ChaseStarted&&manager.HeatLevel==0,"Destroying a civilian does not start a chase or increase heat");
            var destroyedOfficer=Car(bus.position+Vector3.right*20,Quaternion.identity);
            destroyedOfficer.DestroyByBus();
            Check(manager.ChaseStarted&&manager.HeatLevel>0,"Destroying a police car still starts a chase");
            Remove(destroyedOfficer);
            ResetBus(Quaternion.identity);
            var side=Car(bus.position+Vector3.right*(box.size.x*.5f+3f),Quaternion.LookRotation(Vector3.left),true);
            copBody=side.GetComponent<Rigidbody>();
            for(int i=0;i<25;i++){copBody.MovePosition(copBody.position+Vector3.left*8f*.02f);Call(crash,"FixedUpdate");physics.Simulate(.02f);}
            Check(!side.IsDestroyed&&damage.DamagePenalty==0f,"Side contact is neither frontal explosion nor rear damage");
            Remove(side);
            ResetBus(Quaternion.identity);
            var capture=bus.GetComponentInChildren<PoliceCatchZone>(true);
            Check(damage.hitsToDestroy==10,"Existing scene uses ten-hit resistance");
            Call(damage,"TakeHit",bus.position);
            Call(damage,"TakeHit",bus.position);
            Check(damage.HitsTaken==1,"Cooldown prevents duplicate damage");
            damage.ExtinguishFire();
            Check(damage.HitsTaken==0&&damage.DamagePenalty==0f&&!damage.IsDamaged,"Garage restores full resistance and speed");
            for(int hit=1;hit<=9;hit++)
            {
                Field(damage,"lastHitTime",-999f);
                Call(damage,"TakeHit",bus.position);
                Check(damage.HitsTaken==hit&&!capture.IsCaught,"Bus survives damage hit "+hit);
            }
            Check(Mathf.Abs(damage.SpeedMultiplier-.55f)<.001f,"Nine hits leave 55 percent maximum speed");
            Field(damage,"lastHitTime",-999f);
            Call(damage,"TakeHit",bus.position);
            Check(damage.HitsTaken==10&&capture.IsCaught,"Tenth damaging hit ends the run");
            Field(damage,"lastHitTime",-999f);
            Call(damage,"TakeHit",bus.position);
            Check(damage.HitsTaken==10,"Destroyed bus ignores further damage");
            Check(errors==0,"No runtime errors");
            Debug.Log("POLICE_CHECKS_DONE failures=0");
            SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
}
