using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;

namespace DragLab.Runtime
{
    internal sealed class NativeStatsPanel : VisualElement
    {
        internal NativeStatsSnapshot Current { get; }
        internal NativeStatsSnapshot Proposed { get; }
        internal NativeStatsPanel(CarInspection current,UnitPreferences units,IEnumerable<NativeStatGroup> groups,
            IDictionary<string,bool> collapsed,Action<NativeStatDefinition> info,Action<VisualElement,RacingSurface.Kind> decorate,CarInspection proposed=null)
        {
            Current=new NativeStatsSnapshot(current);Proposed=proposed==null?null:new NativeStatsSnapshot(proposed);
            AddToClassList("native-stats");
            foreach(var group in groups) {
                var container=new VisualElement {name="statsGroupFrame:"+group.Id};container.AddToClassList("stat-group");Add(container);
                decorate(container,RacingSurface.Kind.Panel);
                var body=new VisualElement {name="statsBody:"+group.Id};body.AddToClassList("stat-grid");
                bool closed=collapsed.TryGetValue(group.Id,out var saved)&&saved;
                var toggle=new Button {name="statsGroup:"+group.Id,text=group.Title};toggle.AddToClassList("action-button");toggle.AddToClassList("stat-group-toggle");
                var arrow=new FilledMenuIcon("Expand");arrow.AddToClassList("stat-chevron");toggle.Add(arrow);container.Add(toggle);decorate(toggle,RacingSurface.Kind.Button);
                void UpdateGroup(){body.EnableInClassList("hidden",closed);arrow.EnableInClassList("collapsed",closed);toggle.tooltip=closed?"Expand "+group.Title:"Collapse "+group.Title;}
                toggle.clicked+=()=>{closed=!closed;collapsed[group.Id]=closed;UpdateGroup();};UpdateGroup();container.Add(body);
                foreach(var definition in group.Definitions)body.Add(StatRow(definition,units,info));
            }
        }
        VisualElement StatRow(NativeStatDefinition definition,UnitPreferences units,Action<NativeStatDefinition> info)
        {
            var row=new VisualElement {name="stat:"+definition.Id};row.AddToClassList("stat-row");
            var line=new VisualElement();line.AddToClassList("stat-line");row.Add(line);
            var caption=new VisualElement();caption.AddToClassList("stat-caption");line.Add(caption);
            var label=new Label(definition.Label) {enableRichText=false};caption.Add(label);
            var help=new Button(()=>info(definition)) {name="statInfo:"+definition.Id,text="i",tooltip=definition.Label};help.AddToClassList("info-button");caption.Add(help);
            string current=definition.Value(Current,units),value=Proposed==null?current:current+" → "+definition.Value(Proposed,units);
            var number=new Label(value) {name="statValue:"+definition.Id,enableRichText=false};number.AddToClassList("stat-value");line.Add(number);
            if(definition.Number==null)return row;
            var previous=definition.Number(Current);var next=Proposed==null?previous:definition.Number(Proposed);
            if(!previous.HasValue||!next.HasValue)return row;
            double minimum=Math.Min(definition.Minimum,Math.Min(previous.Value,next.Value)),maximum=Math.Max(definition.Maximum,Math.Max(previous.Value,next.Value));
            double range=Math.Max(.001,maximum-minimum),a=(previous.Value-minimum)/range,b=(next.Value-minimum)/range,delta=next.Value-previous.Value;
            var bar=new VisualElement();bar.AddToClassList("stat-bar");row.Add(bar);
            var fill=new VisualElement();fill.AddToClassList("stat-bar-fill");fill.style.width=Length.Percent((float)a*100);bar.Add(fill);
            string effect=Math.Abs(delta)<.00001?"Unchanged":definition.BenefitDirection==0?"Tradeoff":delta*definition.BenefitDirection>0?"Better":"Worse";
            if(Proposed!=null) {
                var change=new VisualElement();change.AddToClassList("stat-bar-change");change.AddToClassList("stat-"+effect.ToLowerInvariant());
                change.style.left=Length.Percent((float)Math.Min(a,b)*100);change.style.width=Length.Percent((float)Math.Abs(b-a)*100);bar.Add(change);
            }
            var bounds=new VisualElement();bounds.AddToClassList("stat-range");row.Add(bounds);
            bounds.Add(new Label(definition.Convert(minimum,units).ToString("0.##")+" "+definition.UnitLabel(units)));
            if(Proposed!=null) {
                var difference=new Label(definition.Convert(delta,units).ToString("+0.##;-0.##;0")+" "+definition.UnitLabel(units)+" · "+effect);difference.AddToClassList("stat-delta");difference.AddToClassList("stat-"+effect.ToLowerInvariant());bounds.Add(difference);
            }
            bounds.Add(new Label(definition.Convert(maximum,units).ToString("0.##")+" "+definition.UnitLabel(units)));
            return row;
        }
    }
}
