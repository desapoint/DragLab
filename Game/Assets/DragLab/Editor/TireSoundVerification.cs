using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class TireSoundVerification
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/tire-sound-refinement"));
        public static void CaptureBaseline()
        {
            if(Directory.Exists(Folder)&&Directory.GetFiles(Folder,"before-*.wav").Length>0)
                throw new InvalidOperationException("Before auditions already exist; preserve the historical local baseline.");
            RenderAuditions("before");
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var report=new StringBuilder();int checks=0;
            Action<bool,string> check=(ok,label)=>{report.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)throw new InvalidOperationException(label);checks++;};
            foreach(int rate in new[]{44100,48000})foreach(int channels in new[]{1,2}) {
                var burnout=Frame(0,18,1);var skid=Frame(28,28,1);
                var a=new float[rate*channels];var b=new float[a.Length];
                new TireSoundSynth().Fill(a,channels,rate,burnout);new TireSoundSynth().Fill(b,channels,rate,skid);
                check(a.All(Finite)&&b.All(Finite)&&a.Max(x=>Math.Abs(x))<.5&&b.Max(x=>Math.Abs(x))<.5,"Burnout/skid output stays finite with headroom / "+rate+" / "+channels);
                check(Rms(a)>.008&&Rms(b)>.008,"Sustained friction remains audible / "+rate+" / "+channels);
                check(!a.SequenceEqual(b),"Moving skids retain a different texture from stationary burnouts / "+rate+" / "+channels);
                var continued=new TireSoundSynth();var recovered=new TireSoundSynth();
                continued.Fill(a,channels,rate,burnout);recovered.Fill(b,channels,rate,burnout);
                var sustained=new float[128*channels];var regained=new float[sustained.Length];
                continued.Fill(sustained,channels,rate,burnout);recovered.Fill(regained,channels,rate,Frame(0,18,0));
                check(Math.Abs(sustained[0]-regained[0])<.002,"Regaining grip does not abruptly cut the screech waveform / "+rate+" / "+channels);
                var quieter=new float[a.Length];new TireSoundSynth().Fill(quieter,channels,rate,Frame(0,18,.3));
                check(Rms(quieter)<Rms(a)*.5,"Lower contact intensity gives quieter friction / "+rate+" / "+channels);
                var synth=new TireSoundSynth();var chirp=new float[rate/10*channels];synth.Fill(chirp,channels,rate,burnout);
                synth.Fill(b,channels,rate,Frame(0,0,0));
                check(Rms(b,b.Length-rate/10*channels)<Rms(chirp)*.01,"A short slip chirp attenuates by at least 40 dB within one second of grip recovery / "+rate+" / "+channels);
                // Running with a different audio callback size must not change continuous sound.
                synth=new TireSoundSynth();var chunked=new float[a.Length];var block=new float[257*channels];int position=0;
                while(position+block.Length<=chunked.Length){synth.Fill(block,channels,rate,burnout);Array.Copy(block,0,chunked,position,block.Length);position+=block.Length;}
                var tail=new float[chunked.Length-position];synth.Fill(tail,channels,rate,burnout);Array.Copy(tail,0,chunked,position,tail.Length);
                check(a.SequenceEqual(chunked),"Static slip is sample-exact across callback boundaries / "+rate+" / "+channels);
                synth.Fill(block,channels,rate,null);synth.Fill(block,channels,rate,Frame(0,0,0));
                check(block.All(x=>x==0),"Reset cannot leak old tire-filter energy / "+rate+" / "+channels);
            }
            foreach(string dimension in new[]{"compound","width","radius"}){
                var contact=new TireFeedback{slidingSpeed=18,intensity=1,slip=1,grip=1,normalLoadN=3500,radiusM=.3,widthScale=1,compoundGrip=1};
                var changed=contact;if(dimension=="compound")changed.compoundGrip=2.9;else if(dimension=="width")changed.widthScale=1.5;else changed.radiusM=.36;
                var a=new float[48000];var b=new float[48000];
                new TireSoundSynth().Fill(a,1,48000,new TireSoundFrame(default,default,contact,contact,0,1));
                new TireSoundSynth().Fill(b,1,48000,new TireSoundFrame(default,default,changed,changed,0,1));
                check(!a.SequenceEqual(b)&&Rms(b)>.005&&b.All(Finite),"Independent "+dimension+" changes asphalt tire timbre with finite audible output at identical contact work");
            }
            {
                var a=new float[48000];var synth=new TireSoundSynth();synth.Fill(a,1,48000,Frame(0,18,1));double runningRms=Rms(a);
                synth.Fill(a,1,48000,new TireSoundFrame(default,default,default,default,0,1,false));
                check(Rms(a,a.Length-4800)<runningRms*.01,"Stopping contact audio before a race finishes releases all tire resonances");
                synth.Fill(a,1,48000,Frame(0,18,1),0);synth.Fill(a,1,48000,Frame(0,18,1),0);
                check(a.All(v=>v==0),"Tire category mute produces exact zero after its smoothed transition");
            }
            RenderAuditions("after");
            File.WriteAllText(Path.Combine(Folder,"sound-verification.txt"),report+"Checks: "+checks+"\n",new UTF8Encoding(false));
            TireFeedbackVerification.VerifyEffectsAt(Folder);
            Debug.Log("TIRE SOUND PASS: "+checks+" + retained contact/DSP/effects gate");
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static double Rms(float[] data,int start=0){double sum=0;for(int i=start;i<data.Length;i++)sum+=data[i]*data[i];return Math.Sqrt(sum/(data.Length-start));}
        static TireSoundFrame Frame(double speed,double slide,double intensity)
        {
            var tire=new TireFeedback{slidingSpeed=slide,intensity=intensity,slip=intensity};
            return new TireSoundFrame(default,default,tire,tire,speed,1);
        }
        static void RenderAuditions(string revision)
        {
            Directory.CreateDirectory(Folder);
            foreach(string name in new[]{"stationary-burnout","acceleration-slip","locked-braking","shift-chirp"}) {
                int rate=44100;var output=new float[rate*4];var block=new float[735];var synth=new TireSoundSynth();
                for(int i=0;i<240;i++) {
                    var frame=name=="stationary-burnout"?Frame(0,18,1):name=="acceleration-slip"?Frame(16,8,.85):name=="locked-braking"?Frame(28,28,1):Frame(18,8,i>=30&&i<38?.85:0);
                    synth.Fill(block,1,rate,frame);Array.Copy(block,0,output,i*block.Length,block.Length);
                }
                EngineSoundAudition.WriteWav(Path.Combine(Folder,revision+"-"+name+".wav"),output,rate);
            }
        }
    }
}
