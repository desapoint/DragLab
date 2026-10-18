using System;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        string racePausePage;
        internal string RacePausePage => racePausePage;
        bool SyncRacePause()
        {
            bool show=game.NativeRacePause;
            root.EnableInClassList("race-dialog-only",show);
            if(!show)
            {
                if(racePausePage!=null){racePausePage=null;if(ModalOpen)CloseDialog();}
                root.Q<Button>("modalClose").focusable=true;
                return false;
            }
            root.RemoveFromClassList("hidden");
            int width=panel.targetTexture==null?Screen.width:panel.targetTexture.width,height=panel.targetTexture==null?Screen.height:panel.targetTexture.height;
            NativeMobile.Fit(root,panel,width,height);root.EnableInClassList("compact",width/panel.scale<1150);
            if(!ModalOpen||racePausePage==null)ShowRacePause();
            return true;
        }
        internal void EscapeRacePause()
        { if(ModalOpen)CloseDialog(); }
        void ShowRacePause()
        {
            racePausePage="menu";
            OpenDialog("Paused",null,new[]{new DialogAction("Resume",CloseDialog,true)},
                ()=>{racePausePage=null;if(game.NativeRacePause)game.PauseRace(false);},content=>{
                    var options=new VisualElement();options.AddToClassList("race-pause-options");content.Add(options);
                    PauseOption(options,"pauseSound","Sound",ShowRaceSound);
                    PauseOption(options,"pauseControls","Controls",ShowRaceControls);
                    PauseOption(options,"pauseQuit","Quit race",ConfirmQuitRace);
                });
            PointerOnlyRaceActions();
        }
        void PointerOnlyRaceActions()
        { root.Q("modalCard").Query<Button>().ForEach(button=>button.focusable=false); }
        void PauseOption(VisualElement parent,string id,string text,Action action)
        {
            var button=new Button(action){name=id,text=text};button.AddToClassList("action-button");parent.Add(button);
            AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());
        }
        void ShowRaceSound()
        {
            racePausePage="sound";
            float baselineMaster=SharedAudioSettings.MasterVolume,baselineOpponent=SharedAudioSettings.OpponentVolume;
            float master=baselineMaster,opponent=baselineOpponent;
            Label error=null;
            Action changed=()=>{error.text="";root.Q("dialogApply").SetEnabled(Math.Abs(master-baselineMaster)>.0001||Math.Abs(opponent-baselineOpponent)>.0001);};
            OpenDialog("Sound",null,new[]{new DialogAction("Back",CloseDialog),new DialogAction("Apply",()=>{
                if(!game.ApplyRaceAudio(master,opponent,out string failure)){error.text=failure;return;}
                CloseDialog();
            },true)},()=>racePausePage=null,content=>{
                AddRaceAudioRange(content,"pauseMaster","Master volume",()=>master,v=>master=v,()=>baselineMaster,changed);
                AddRaceAudioRange(content,"pauseOpponent","Opponent volume",()=>opponent,v=>opponent=v,()=>baselineOpponent,changed);
                error=new Label{name="pauseSoundError"};error.AddToClassList("settings-status");content.Add(error);
            });
            changed();
            PointerOnlyRaceActions();
        }
        void AddRaceAudioRange(VisualElement content,string id,string label,Func<float> get,Action<float> set,Func<float> reset,Action changed)
        {
            var row=new VisualElement();row.AddToClassList("race-audio-range");row.Add(new Label(label));content.Add(row);
            row.Add(new NativeTuningRange(id,()=>get()*100,v=>set(v/100),()=>0,()=>100,()=>reset()*100,1,.1f,"%",changed,message=>root.Q<Label>("pauseSoundError").text=message));
        }
        void ShowRaceControls()
        {
            racePausePage="controls";
            OpenDialog("Controls",null,new[]{new DialogAction("Back",CloseDialog)},()=>racePausePage=null,content=>{
                foreach(var binding in new[]{new[]{"Touch pedals","Hold · slide up to ease off"},new[]{"Touch shifting",DrivingPreferences.ReverseTouchShifting?"↓ upshift · ↑ downshift":"↑ upshift · ↓ downshift"},new[]{"Throttle","W / Space"},new[]{"Brake","S"},new[]{"Clutch","Left Shift"},new[]{"Engage / shift up","E / Up"},new[]{"Shift down","Q / Down"},new[]{"Launch / restart / ready","Enter"},new[]{"Pause","Esc"},new[]{"Compare interface","F8 · while driving"}})
                {
                    var row=new VisualElement();row.AddToClassList("setting-row");if(binding[0]=="Compare interface")row.AddToClassList("desktop-only");var caption=new VisualElement();caption.AddToClassList("setting-caption");caption.Add(new Label(binding[0]));row.Add(caption);var key=new Label(binding[1]);key.AddToClassList("setting-control");row.Add(key);content.Add(row);
                }
            });
            PointerOnlyRaceActions();
        }
        void ConfirmQuitRace()
        {
            racePausePage="quit";Label error=null;
            OpenDialog("Quit race?","Fuel used, wear and damage are kept. Entry fees are not refunded.",new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Quit race",()=>{
                game.ReturnToGarage();
                if(game.ScreenName=="Race"){error.text=game.NativeNotice;return;}
                CloseDialog();
            },true)},()=>racePausePage=null,content=>{error=new Label{name="pauseQuitError"};error.AddToClassList("settings-status");content.Add(error);});
            PointerOnlyRaceActions();
        }
    }

    public sealed partial class PrototypeGame
    {
        internal bool ApplyRaceAudio(float master,float opponent,out string error)
        {
            if(!NativeRacePause){error="Pause is no longer open.";return false;}
            // The limited dialog cannot change driver rules, units, ownership or progression.
            return PreferenceCommit.TryApply(profile,SharedUnitSettings.Current,master,opponent,out error);
        }
    }
}
