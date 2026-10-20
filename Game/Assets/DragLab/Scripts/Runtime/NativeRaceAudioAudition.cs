using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeRaceHud
    {
        VisualElement auditionBar;
        Label auditionCaption;
        Button auditionStop;
        readonly Button[] auditionMix=new Button[3];
        void InitializeAudioAuditionControls()
        {
            if(!PrototypeGame.AudioAuditionRequested)return;
            auditionBar=new VisualElement{name="audioAuditionControls"};
            auditionBar.style.position=Position.Absolute;auditionBar.style.left=20;auditionBar.style.right=85;auditionBar.style.top=16;
            root.Add(auditionBar);
            auditionCaption=new Label();auditionCaption.style.whiteSpace=WhiteSpace.Normal;auditionBar.Add(auditionCaption);
            var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;row.style.flexWrap=Wrap.Wrap;auditionBar.Add(row);
            for(int i=0;i<3;i++){
                int mode=i;var b=HudButton("audioMix"+i,new[]{"Mixed","Engine only","Tires only"}[i],()=>game.SelectAudioAuditionMode(mode));
                b.style.marginRight=8;row.Add(b);auditionMix[i]=b;
            }
            auditionStop=HudButton("audioStop","Stop",()=>game.StopAudioAudition());row.Add(auditionStop);auditionStop.style.marginRight=8;
            row.Add(HudButton("audioNext","Next car",()=>game.NextAudioAudition()));
        }
        internal bool AudioAuditionControlsFit()
        {
            if(auditionBar==null||auditionBar.worldBound.height<30)return false;
            var buttons=auditionBar.Query<Button>().ToList();
            foreach(var b in buttons)if(b.worldBound.width<40||b.worldBound.height<20||b.worldBound.xMin<0||b.worldBound.xMax>root.worldBound.xMax||b.worldBound.yMax>root.worldBound.yMax)return false;
            for(int i=0;i<buttons.Count;i++)for(int j=i+1;j<buttons.Count;j++)if(buttons[i].worldBound.Overlaps(buttons[j].worldBound))return false;
            return true;
        }
        void RefreshAudioAuditionControls()
        {
            if(auditionBar==null)return;
            auditionBar.EnableInClassList("hidden",!game.AudioAuditionActive||game.NativeRacePause);
            auditionCaption.text=game.AudioAuditionTitle+"\n"+game.AudioAuditionStage+" · Asphalt · Esc: sound settings";
            for(int i=0;i<3;i++)auditionMix[i].EnableInClassList("selected",game.AudioAuditionMode==i);
            auditionStop.Q<Label>().text=game.AudioAuditionStopped?"Resume / restart":"Stop";
        }
    }
}
