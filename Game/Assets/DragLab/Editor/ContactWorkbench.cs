using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using DragLab.Domain;
namespace DragLab.Editor
{
    public sealed partial class DragWorkbench
    {
        bool contactOpen, contactRunning, contactRecording, contactCenterLocked;
        WheelContactState contact=new WheelContactState();
        float contactMass=1200,contactRadius=.3f,contactInertia=1.1f,contactFrontTorque,contactRearTorque=1000,contactBrake,contactLeftGrip=1,contactRightGrip=1,contactFrontHealth=1,contactRearHealth=1;
        DifferentialKind contactKind;
        double contactClock,contactLastUpdate,contactAccumulator;
        readonly StringBuilder contactCsv=new StringBuilder();
        void OnEnable(){if(contactCsv.Length==0)ResetContact();EditorApplication.update+=AdvanceContact;contactLastUpdate=EditorApplication.timeSinceStartup;}
        void OnDisable(){EditorApplication.update-=AdvanceContact;}
        WheelContactInput ContactInput() { return new WheelContactInput { lockCenter=contactCenterLocked,frontDifferentialHealth=contactFrontHealth,rearDifferentialHealth=contactRearHealth,massKg=contactMass,radiusM=contactRadius,inertiaKgM2=contactInertia,frontTorqueNm=contactFrontTorque,rearTorqueNm=contactRearTorque,brakeNm=new[]{(double)contactBrake,contactBrake,contactBrake,contactBrake},normalLoadN=new[]{contactMass*DragMath.Gravity*.25,contactMass*DragMath.Gravity*.25,contactMass*DragMath.Gravity*.25,contactMass*DragMath.Gravity*.25},friction=new[]{(double)contactLeftGrip,contactRightGrip,contactLeftGrip,contactRightGrip},frontDifferential=new DifferentialSpec{kind=contactKind,torqueBiasRatio=3,preloadNm=30},rearDifferential=new DifferentialSpec{kind=contactKind,torqueBiasRatio=3,preloadNm=30} }; }
        void ContactStep() { WheelContactDynamics.Step(contact,ContactInput(),DragMath.FixedStep);contactClock+=DragMath.FixedStep;if(contactRecording) { contactCsv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11},{12},",WheelContactDynamics.Model,contactMass,contactRadius,contactInertia,contactFrontTorque,contactRearTorque,contactBrake,contactLeftGrip,contactRightGrip,contactFrontHealth,contactRearHealth,contactKind,contactCenterLocked);contactCsv.AppendFormat(CultureInfo.InvariantCulture,"{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R}\n",contactClock,contact.speed,contact.omega[0],contact.omega[1],contact.omega[2],contact.omega[3],contact.slip[0],contact.slip[1],contact.slip[2],contact.slip[3],contact.contactWorkJ,contact.brakeWorkJ,contact.KineticEnergy(contactMass,contactInertia)); } }
        void AdvanceContact() { double now=EditorApplication.timeSinceStartup,elapsed=Math.Min(.1,now-contactLastUpdate);contactLastUpdate=now;if(!contactRunning)return;contactAccumulator+=elapsed;while(contactAccumulator>=DragMath.FixedStep){ContactStep();contactAccumulator-=DragMath.FixedStep;}Repaint(); }
        void ResetContact() { contact=new WheelContactState();contactClock=contactAccumulator=0;contactCsv.Clear();contactCsv.AppendLine("model,mass_kg,radius_m,inertia_kg_m2,front_nm,rear_nm,brake_nm_each,left_grip,right_grip,front_diff_health,rear_diff_health,axle_diff,center_locked,time_s,speed_m_s,fl_rad_s,fr_rad_s,rl_rad_s,rr_rad_s,fl_slip,fr_slip,rl_slip,rr_slip,contact_loss_j,brake_work_j,kinetic_j"); }
        void DrawContactLab()
        {
            contactOpen=EditorGUILayout.Foldout(contactOpen,"Wheel contact lab / isolated torque fixture",true);if(!contactOpen)return;
            EditorGUILayout.HelpBox("Tests the wheel/contact solver directly. This fixture applies wheel torque; it has no engine or transmission and makes no claim about a complete car. Races remain on their previous model until phase 2H couples the driveline.",MessageType.Info);
            using(new EditorGUI.DisabledScope(contactRunning)) {
                contactMass=EditorGUILayout.Slider("Translating mass kg",contactMass,500,4000);contactRadius=EditorGUILayout.Slider("Outer radius m",contactRadius,.2f,.8f);contactInertia=EditorGUILayout.Slider("Inertia per wheel kg m²",contactInertia,.1f,20);
            }
            contactCenterLocked=EditorGUILayout.Toggle("Lock axle means / AWD center fixture",contactCenterLocked);
            contactRecording=EditorGUILayout.Toggle("Record contact CSV (optional)",contactRecording);
            contactFrontHealth=EditorGUILayout.Slider("Front diff health / 0 = disconnected",contactFrontHealth,0,1);contactRearHealth=EditorGUILayout.Slider("Rear diff health / 0 = disconnected",contactRearHealth,0,1);
            contactFrontTorque=EditorGUILayout.Slider("Front axle applied Nm",contactFrontTorque,-2000,6000);contactRearTorque=EditorGUILayout.Slider("Rear axle applied Nm",contactRearTorque,-2000,6000);contactBrake=EditorGUILayout.Slider("Brake torque per wheel Nm",contactBrake,0,3000);
            contactLeftGrip=EditorGUILayout.Slider("Left surface multiplier",contactLeftGrip,0,1.5f);contactRightGrip=EditorGUILayout.Slider("Right surface multiplier",contactRightGrip,0,1.5f);contactKind=(DifferentialKind)EditorGUILayout.EnumPopup("Axle differential",contactKind);
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button(contactRunning?"Pause contact":"Run contact"))contactRunning=!contactRunning;
            if(GUILayout.Button("Step 1/120 s")){contactRunning=false;ContactStep();}if(GUILayout.Button("Reset contact")){contactRunning=false;ResetContact();}
            if(GUILayout.Button("Cut applied torque")){contactFrontTorque=contactRearTorque=0;}EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Repeat 2s launch / 1s open drive / 2s brake")){contactRunning=false;ResetContact();contactRecording=true;float f=contactFrontTorque,r=contactRearTorque,b=contactBrake;contactFrontTorque=0;contactRearTorque=2400;contactBrake=0;for(int i=0;i<240;i++)ContactStep();contactRearTorque=0;for(int i=0;i<120;i++)ContactStep();contactBrake=1800;for(int i=0;i<240;i++)ContactStep();contactFrontTorque=f;contactRearTorque=r;contactBrake=b;}
            EditorGUILayout.LabelField("Time / road",contactClock.ToString("F2")+" s / "+(contact.speed*3.6).ToString("F1")+" km/h / "+contact.distance.ToString("F1")+" m");
            for(int i=0;i<4;i++)EditorGUILayout.LabelField(new[]{"Front left","Front right","Rear left","Rear right"}[i],(contact.omega[i]*FlywheelDynamics.RpmPerRadian).ToString("F0")+" wheel RPM / slip "+contact.slip[i].ToString("P1")+" / contact "+contact.force[i].ToString("F0")+" N");
            EditorGUILayout.LabelField("Work / energy",contact.contactWorkJ.ToString("F0")+" J total contact/differential loss / "+contact.brakeWorkJ.ToString("F0")+" J brake work / "+contact.KineticEnergy(contactMass,contactInertia).ToString("F0")+" J kinetic");
            if(GUILayout.Button("Export contact trace CSV")){string path=EditorUtility.SaveFilePanel("Contact trace","","wheel-contact.csv","csv");if(path.Length>0)File.WriteAllText(path,contactCsv.ToString());}
        }
    }
}
