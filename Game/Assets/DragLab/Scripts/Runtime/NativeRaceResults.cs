using System;
using System.Collections.Generic;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class NativeRaceResults : MonoBehaviour
    {
        PrototypeGame game;
        PanelSettings panel;
        VisualElement root, paper;
        ScrollView scroll;
        Label note, comparison;
        Button repairs;
        RaceSimulation bound;
        readonly List<RacingSurface> surfaces=new List<RacingSurface>();
        internal bool Visible=>root!=null&&!root.ClassListContains("hidden");
        internal VisualElement Root=>root;
        internal PanelSettings Panel=>panel;
        internal static bool Supports(string route)=>route=="Results";
        internal void Initialize(PrototypeGame owner)
        {
            game=owner;panel=Instantiate(Resources.Load<PanelSettings>("UI/NativePanel"));panel.sortingOrder-=1;
            var document=gameObject.AddComponent<UIDocument>();document.panelSettings=panel;
            root=new VisualElement{name="raceResults",pickingMode=PickingMode.Ignore};root.AddToClassList("shell");root.AddToClassList("race-results");root.AddToClassList("hidden");
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativePalette"));
            root.styleSheets.Add(Array.Find(Resources.LoadAll<StyleSheet>("UI"),s=>s.name=="NativeShell"));
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativeRaceResults"));
            document.rootVisualElement.Add(root);document.rootVisualElement.pickingMode=PickingMode.Ignore;
            root.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==KeyCode.Tab){root.focusController.IgnoreEvent(evt);evt.StopImmediatePropagation();}},TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(evt=>{root.focusController.IgnoreEvent(evt);evt.StopImmediatePropagation();});
            var dock=new VisualElement{name="resultDock"};dock.AddToClassList("results-dock");root.Add(dock);
            paper=new VisualElement{name="timingSlip"};paper.AddToClassList("timing-paper");dock.Add(paper);
            paper.Add(new TimingPaper());
            scroll=new ScrollView{name="timingSlipScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("slip-scroll");paper.Add(scroll);
            var bar=scroll.verticalScroller;bar.AddToClassList("settings-scrollbar");bar.slider.AddToClassList("settings-scrollbar-slider");bar.lowButton.AddToClassList("scrollbar-arrow");bar.highButton.AddToClassList("scrollbar-arrow");
            var actions=new VisualElement();actions.AddToClassList("slip-actions");dock.Add(actions);
            ActionButton(actions,"resultGarage","Garage",game.QueueResultGarage,false);
            ActionButton(actions,"resultAgain","Race again",game.QueueResultAgain,true);
            InterfaceComparison.SetReplacement("Results",true);
        }
        Button ActionButton(VisualElement parent,string id,string caption,Action action,bool primary)
        {
            var b=new Button(action){name=id,focusable=false,tabIndex=-1};b.AddToClassList("action-button");
            b.Add(new Label(caption){pickingMode=PickingMode.Ignore});var surface=new RacingSurface(b,primary?RacingSurface.Kind.Action:RacingSurface.Kind.SecondaryAction);b.Insert(0,surface);surfaces.Add(surface);parent.Add(b);return b;
        }
        void LateUpdate()=>Refresh();
        internal void Refresh()
        {
            bool show=game.Race!=null&&Supports(game.ComparisonRoute)&&InterfaceComparison.IsReplacement("Results");
            root.EnableInClassList("hidden",!show);foreach(var s in surfaces)s.Tick(show);if(!show)return;
            int width=panel.targetTexture==null?Screen.width:panel.targetTexture.width,height=panel.targetTexture==null?Screen.height:panel.targetTexture.height;
            panel.scale=NativeMenuShell.DisplayScale(width,height);root.EnableInClassList("compact",width/panel.scale<1150||height/panel.scale<700);
            if(bound!=game.Race){bound=game.Race;BuildSlip();}
            if(note.text!=game.ResultNotice){note.text=game.ResultNotice;comparison.text=game.ResultComparison;comparison.EnableInClassList("hidden",string.IsNullOrEmpty(comparison.text));}
            repairs.EnableInClassList("hidden",!bound.player.mechanics.parts.Exists(PrototypeGame.PartFailedForResults));
        }
        void BuildSlip()
        {
            scroll.Clear();surfaces.RemoveAll(s=>s.panel==null);
            var title=new Label("DRAGLAB");title.AddToClassList("slip-brand");scroll.Add(title);
            var meta=new Label((game.IsPractice?"PRACTICE":"RACE")+"  /  "+bound.distance.ToString("F1")+" m");meta.AddToClassList("slip-meta");scroll.Add(meta);
            var outcome=new Label(bound.player.state.disqualified?"RED LIGHT":bound.timedOut?"TIMEOUT":!bound.player.state.finished?"DNF":bound.playerWon?"WIN":"LOSS");outcome.name="slipOutcome";outcome.AddToClassList("slip-outcome");scroll.Add(outcome);
            Row("","YOU","RIVAL",true);
            Row("Elapsed · s",Time(bound.player.state),Time(bound.opponent.state));
            Row("Reaction · s",Reaction(bound.player.state),Reaction(bound.opponent.state));
            Row("Finish · "+DisplayUnits.SpeedLabel(SharedUnitSettings.Current.speed),bound.player.state.finished?DisplayUnits.Speed(bound.player.state.trapSpeed/3.6,SharedUnitSettings.Current.speed).ToString("F1"):"—",bound.opponent.state.finished?DisplayUnits.Speed(bound.opponent.state.trapSpeed/3.6,SharedUnitSettings.Current.speed).ToString("F1"):"—");
            foreach(var split in bound.player.state.splits)
                Row(split.Key.ToString("F1")+" m",split.Value.ToString("F3"),bound.opponent.state.splits.TryGetValue(split.Key,out double other)?other.ToString("F3"):"—");
            note=new Label(game.ResultNotice){name="slipNotice"};note.AddToClassList("slip-note");scroll.Add(note);
            var details=new Foldout{text="Run details",value=false,name="resultDetails"};details.focusable=false;scroll.Add(details);
            comparison=new Label(game.ResultComparison);comparison.AddToClassList("slip-note");comparison.EnableInClassList("hidden",string.IsNullOrEmpty(comparison.text));details.Add(comparison);
            var analysis=new Label(bound.telemetry.Analysis(bound.player));analysis.AddToClassList("slip-note");details.Add(analysis);
            repairs=ActionButton(details,"resultRepairs","Review failures",game.QueueResultRepairs,false);
            ActionButton(details,"resultExport","Export telemetry",game.ExportResultTelemetry,false);
        }
        static string Time(CarState s)=>s.finished?s.elapsed.ToString("F3"):"DNF";
        static string Reaction(CarState s)=>s.reaction>=0?s.reaction.ToString("F3"):"—";
        void Row(string caption,string player,string opponent,bool heading=false)
        {
            var row=new VisualElement();row.AddToClassList("slip-row");if(heading)row.AddToClassList("slip-column-head");
            var c=new Label(caption);c.AddToClassList("slip-caption");row.Add(c);
            var a=new Label(player);a.AddToClassList("slip-number");row.Add(a);var b=new Label(opponent);b.AddToClassList("slip-number");row.Add(b);scroll.Add(row);
        }
        void OnDestroy(){if(panel!=null)Destroy(panel);}
    }

    internal sealed class TimingPaper : VisualElement
    {
        internal TimingPaper(){pickingMode=PickingMode.Ignore;AddToClassList("timing-paper-surface");generateVisualContent+=Draw;}
        void Draw(MeshGenerationContext ctx)
        {
            float w=contentRect.width,h=contentRect.height;if(w<1||h<1)return;var p=ctx.painter2D;
            p.fillColor=new Color(0,0,0,.25f);p.BeginPath();p.MoveTo(new Vector2(5,6));p.LineTo(new Vector2(w,6));p.LineTo(new Vector2(w,h));p.LineTo(new Vector2(5,h));p.ClosePath();p.Fill();
            p.fillColor=new Color(.94f,.93f,.89f);p.BeginPath();p.MoveTo(new Vector2(0,0));
            for(float x=0;x<w-5;x+=8){p.LineTo(new Vector2(x,2));p.LineTo(new Vector2(x+4,0));}
            p.LineTo(new Vector2(w-5,h-8));for(float x=w-5;x>0;x-=8){p.LineTo(new Vector2(x,h-6));p.LineTo(new Vector2(x-4,h-8));}p.LineTo(Vector2.zero);p.ClosePath();p.Fill();
            p.strokeColor=new Color(.25f,.22f,.16f,.035f);p.lineWidth=.6f;
            for(int i=0;i<160;i++){float x=(i*173%997)/997f*(w-10),y=(i*293%991)/991f*(h-12)+5;p.BeginPath();p.MoveTo(new Vector2(x,y));p.LineTo(new Vector2(x+3,y));p.Stroke();}
        }
    }

    public sealed partial class PrototypeGame
    {
        internal NativeRaceResults Results {get;private set;}
        internal string ResultNotice=>notice;
        internal string ResultComparison=>finishedPass==null?"":finishedPass.Compare(previousPass,SharedUnitSettings.Current);
        internal static bool PartFailedForResults(DragLab.Domain.Mechanics.PartInstance part)=>PartFailed(part);
        bool CanResultAction=>race!=null&&race.phase==RacePhase.Finished&&!NativeRacePause&&pendingAction==null;
        internal void QueueResultGarage(){if(CanResultAction)pendingAction=ReturnToGarage;}
        internal void QueueResultAgain(){if(CanResultAction)pendingAction=()=>StartRace(activeEvent,practiceRun);}
        internal void QueueResultRepairs(){if(CanResultAction)pendingAction=()=>{ReturnToGarage();if(screen!="Race")ShowRepairs();};}
        internal void ExportResultTelemetry()
        {
            if(!CanResultAction)return;
            try {string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(DragLab.Infrastructure.ProfileStore.FilePath),"last-run.csv");System.IO.File.WriteAllText(path,race.telemetry.Csv(),new System.Text.UTF8Encoding(false));notice="Exported: "+path;}
            catch(Exception e){notice="Export failed: "+e.Message;}
        }
    }
}
