using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed class NativeWarningChange
    {
        internal SetupWarning Current,Planned;
        internal string State;
        internal SetupWarning Display=>Planned??Current;
    }
    internal static class NativeWarningOverview
    {
        static string Key(SetupWarning w)=>w.title+(w.title=="Component needs attention"?"\n"+w.explanation:"");
        internal static NativeWarningChange[] Combine(IEnumerable<SetupWarning> current,IEnumerable<SetupWarning> planned,bool compare)
        {
            var a=current.GroupBy(Key).ToDictionary(g=>g.Key,g=>g.OrderByDescending(w=>w.critical).First());
            var b=(compare?planned??Enumerable.Empty<SetupWarning>():Enumerable.Empty<SetupWarning>()).GroupBy(Key).ToDictionary(g=>g.Key,g=>g.OrderByDescending(w=>w.critical).First());
            return a.Keys.Union(b.Keys).Select(key=>{a.TryGetValue(key,out var old);b.TryGetValue(key,out var next);return new NativeWarningChange{Current=old,Planned=next,State=!compare?"Current":old==null?"Introduced":next==null?"Resolved":"Remaining"};}).OrderBy(w=>w.State=="Introduced"?0:w.State=="Remaining"?1:w.State=="Current"?0:2).ThenByDescending(w=>w.Display.critical).ThenBy(w=>w.Display.title,StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static string Related(SetupWarning w)
        {
            string title=w.title;var slots=new List<PartCategory>();
            foreach(PartCategory slot in Enum.GetValues(typeof(PartCategory)))if(Regex.IsMatch(title,@"\b"+slot+@"\b")||title=="Component needs attention"&&Regex.IsMatch(w.explanation,@"\b"+slot+@"\b"))slots.Add(slot);
            if(slots.Count>0)return string.Join(" · ",slots.Select(s=>PartSemantics.Friendly(s.ToString())));
            switch(title){
                case "Oil quantity below safe pickup minimum":case "Oil needs service":return "Oil / lubrication";
                case "Coolant quantity limits circulation":case "No active cooling":return "Cooling / coolant";
                case "Cam needs stronger valve springs":return "Camshaft · valve springs · valves";
                case "Braking unavailable":return "Brakes";
                case "No drive path to the road":case "Open center spins a disconnected shaft":case "AWD running on its surviving axle":return "Transmission / drivetrain";
                case "Clutch may slip instead of accelerating":return "Clutch";
                case "Excessive wheelspin is likely":case "Tire condition limits traction":return "Tires / grip";
                case "Large/heavy tires can make launches sluggish":return "Tires · wheels · gearing";
                case "No combustion rev limiter":return "Rev limiter · engine internals";
                default:return "Build";
            }
        }
    }
    internal sealed partial class NativeMenuShell
    {
        readonly Dictionary<string,SetupWarning[]> ownedWarningSnapshots=new Dictionary<string,SetupWarning[]>();
        PlayerProfile warningProfile;long warningRevision=-1;
        SetupWarning[] CurrentOwnedWarnings(OwnedCar car)
        {
            if(warningProfile==game.Profile&&warningRevision==game.Profile.revision&&ownedWarningSnapshots.TryGetValue(car.instanceId,out var captured))return captured;
            var cached=InspectOwned(car);if(cached?.Stats!=null)return cached.Stats.warnings.ToArray();
            try{var build=MechanicsOwnership.Resolve(game.Profile,car,game.Profile.catalog);var issues=new List<SetupWarning>();
                if(build.errors.Count>0)issues.Add(new SetupWarning("Build cannot race",string.Join("; ",build.errors),"Fit missing compatible parts, release reserved components or replace the failed hardware.",true));
                foreach(string warning in build.warnings)issues.Add(new SetupWarning("Component needs attention",warning,"Inspect the fitted components and applicable service options."));
                if(issues.Count>0)return issues.ToArray();
            }catch(Exception){}
            return new[]{new SetupWarning("Inspection unavailable","Current build data could not be inspected.","Check the fitted engine and component information.")};
        }
        void OpenOwnedWarnings(string id)
        {
            var car=game.Profile.cars.Find(c=>c.instanceId==id);if(car==null)return;
            OpenCombinedWarnings(CurrentOwnedWarnings(car),null,false,"Warnings · "+VehicleCollection.DisplayName(game.Profile.catalog,car));
        }
        Button WarningButton(VisualElement parent,string id,SetupWarning[] warnings,Action open,Action<VisualElement,RacingSurface.Kind> decorate,bool iconOnly=false)
        {
            var b=new Button(open){name=id,text=iconOnly?"":"Warnings · "+warnings.Length,tabIndex=-1,focusable=false};b.AddToClassList("action-button");b.AddToClassList("warning-button");b.AddToClassList(iconOnly?"warning-icon-button":"car-stats-button");
            parent.Add(b);decorate(b,RacingSurface.Kind.SecondaryAction);b.Insert(1,new FilledMenuIcon("Warning"));
            SetWarningButton(b,warnings,iconOnly);
            b.RegisterCallback<PointerDownEvent>(e=>e.StopPropagation());b.RegisterCallback<PointerUpEvent>(e=>e.StopPropagation());b.RegisterCallback<ClickEvent>(e=>e.StopPropagation());return b;
        }
        void SetWarningButton(Button button,SetupWarning[] warnings,bool iconOnly=false)
        {
            button.EnableInClassList("car-critical",warnings.Any(w=>w.critical));button.EnableInClassList("car-warning",warnings.Length>0&&!warnings.Any(w=>w.critical));button.EnableInClassList("warning-clear",warnings.Length==0);
            if(!iconOnly)button.Q<Label>().text="Warnings · "+warnings.Length;
            button.tooltip=warnings.Length==0?"No warnings reported":"Warnings · "+warnings.Length+(warnings.Any(w=>w.critical)?" · Critical":"");button.Q<FilledMenuIcon>()?.MarkDirtyRepaint();
        }
        void OpenCombinedWarnings(SetupWarning[] current,SetupWarning[] planned,bool comparison,string title="Warnings",string unavailable=null)
        {
            bool compare=comparison&&planned!=null;var entries=NativeWarningOverview.Combine(current,planned,compare);
            OpenDialog(title,null,new[]{new DialogAction("Close",CloseDialog)},populate:content=>{
                var scope=new Label("Static inspection");scope.AddToClassList("warning-suggestion");content.Add(scope);
                if(unavailable!=null){var missing=new Label(unavailable){name="warningUnavailable",enableRichText=false};missing.AddToClassList("car-warning");content.Add(missing);}
                if(entries.Length==0){content.Add(new Label("No warnings"){name="warningsEmpty"});return;}
                foreach(var section in entries.GroupBy(e=>e.State)){
                    var group=new VisualElement{name="warningsGroup:"+section.Key};group.AddToClassList("warning-group");content.Add(group);group.Add(new Label(section.Key+" · "+section.Count()));
                    foreach(var item in section){var w=item.Display;var block=new VisualElement();block.AddToClassList("warning-item");group.Add(block);
                        var heading=new Label(w.title){enableRichText=false};heading.AddToClassList("warning-title");block.Add(heading);
                        string severity=item.State=="Resolved"?(item.Current.critical?"Critical":"Attention")+" → Clear":item.Current!=null&&item.Planned!=null&&item.Current.critical!=item.Planned.critical?(item.Current.critical?"Critical":"Attention")+" → "+(item.Planned.critical?"Critical":"Attention"):w.critical?"Critical":"Attention";
                        var meta=new Label(severity+" · "+NativeWarningOverview.Related(w)){enableRichText=false};meta.AddToClassList(item.State=="Resolved"?"car-ready":w.critical?"car-critical":"car-warning");block.Add(meta);
                        if(item.Current!=null&&item.Planned!=null&&item.Current.explanation!=item.Planned.explanation){block.Add(new Label("Current: "+SharedUnitSettings.TorqueText(item.Current.explanation)){enableRichText=false});block.Add(new Label("Planned: "+SharedUnitSettings.TorqueText(item.Planned.explanation)){enableRichText=false});}
                        else block.Add(new Label(SharedUnitSettings.TorqueText(w.explanation)){enableRichText=false});
                        if(item.State!="Resolved"&&!string.IsNullOrWhiteSpace(w.suggestion)){var suggestion=new Label(SharedUnitSettings.TorqueText(w.suggestion)){enableRichText=false};suggestion.AddToClassList("warning-suggestion");block.Add(suggestion);}
                    }
                }
            });
        }
    }
}
