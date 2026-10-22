using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEditor;
using UnityEngine;
using DriveType=DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    // Explicit content authoring only: loading a save never creates or rewrites events.
    public static class CareerRacePack
    {
        public const int AddedCount=28, TotalCount=49;
        enum Rule { Open, Front, Rear, AllWheel, Natural, Turbo, Lightweight, Heavy, Small }
        sealed class Race
        {
            public readonly int tier,slot,power;public readonly string slug,title,model,driver;public readonly Rule rule;public readonly float hp;
            public Race(int tier,int slot,string slug,string title,string model,string driver,int power,Rule rule=Rule.Open,float hp=0)
            {this.tier=tier;this.slot=slot;this.slug=slug;this.title=title;this.model=model;this.driver=driver;this.power=power;this.rule=rule;this.hp=hp;}
            public string Tier=>"FEDCBAS"[tier].ToString();
            public string Id=>"side-"+Tier.ToLowerInvariant()+"-"+slug;
            public string Prerequisite=>tier==0?(slot<2?"first-pass":"club-run"):"ladder-"+"fedcba"[tier-1]+"-rival";
            public int Rep=>tier==0?(slot<2?2:4):12+tier*8+(slot<2?0:2);
            public int Fee=>tier==0?(slot<2?0:slot==2?15:20):40+tier*20+slot*5;
            public int Net=>Rewards[tier][slot];
            public float Distance=>slot==0?201.168f:slot==3?804.672f:402.336f;
        }
        static readonly int[][] Rewards={new[]{125,175,200,225},new[]{450,500,525,550},new[]{600,650,675,700},new[]{750,800,825,875},new[]{950,1000,1050,1125},new[]{1200,1250,1325,1400},new[]{1600,1700,1800,1950}};
        static readonly Race[] Races={
            new Race(0,0,"dockside","Dockside warm-up","sprig-sport","Nico",0,Rule.Open,150),
            new Race(0,1,"hatch-meet","Backroad hatch meet","rook","Bea",0,Rule.Front,150),
            new Race(0,2,"small-capacity","Small-capacity cup","finch-sport","Kit",0,Rule.Small,170),
            new Race(0,3,"rear-drive-rookie","Rear-drive rookie run","swift","Tess",0,Rule.Rear,180),
            new Race(1,0,"short-shift","Short-shift showdown","cinder-sport","Ivo",0,Rule.Open,240),
            new Race(1,1,"all-motor","All-motor club","sable-sport","Lena",0,Rule.Natural,240),
            new Race(1,2,"boost-street","Boost street challenge","kestrel-track","Remy",0,Rule.Turbo,300),
            new Race(1,3,"touring-pull","Touring long pull","touring-six","Sana",0,Rule.Open,320),
            new Race(2,0,"featherweight","Featherweight sprint","lark-sport","Finn",1,Rule.Lightweight,350),
            new Race(2,1,"roadster-night","Roadster night","solstice","Mara",0,Rule.Rear,400),
            new Race(2,2,"turbo-chase","Turbo chase","osprey","Ari",1,Rule.Turbo,450),
            new Race(2,3,"workhorse","Workhorse long haul","timber-diesel-tow","Owen",0,Rule.Heavy,450),
            new Race(3,0,"four-wheel-launch","Four-wheel launch club","boreal","Eden",1,Rule.AllWheel,550),
            new Race(3,1,"front-row","Front-row shootout","cinder-sport","Zara",2,Rule.Front,500),
            new Race(3,2,"six-pot","Six-pot night meet","meridian-sport","Ellis",1,Rule.Natural,550),
            new Race(3,3,"after-hours","After-hours muscle","v8-muscle","Cruz",1,Rule.Rear,650),
            new Race(4,0,"boost-launch","Boost launch battle","tempest-rs","Noor",2,Rule.AllWheel,750),
            new Race(4,1,"street-pressure","Street pressure","osprey-sport","Drew",2,Rule.Turbo,750),
            new Race(4,2,"big-coupe","Big coupe grudge","v8-muscle","Rhea",2,Rule.Rear,800),
            new Race(4,3,"heavy-express","Heavy express","atlas","Jae",0,Rule.Heavy,800),
            new Race(5,0,"flash-launch","Flash launch trophy","boreal-sport","Lex",3,Rule.AllWheel,1100),
            new Race(5,1,"track-pressure","Track pressure","kestrel-track","Avery",3,Rule.Turbo,1100),
            new Race(5,2,"blower-battle","Blower battle","super-v8","Rory",1,Rule.Rear,1100),
            new Race(5,3,"grand-touring","Grand touring challenge","solstice-sport","Skye",3,Rule.Rear,1200),
            new Race(6,0,"launch-masters","Launch masters","rally-sedan","Quinn",3,Rule.AllWheel),
            new Race(6,1,"blower-crown","Blower crown","super-v8","Kai",3,Rule.Rear),
            new Race(6,2,"heavy-hitters","Heavy hitters","atlas-sport","Wren",2,Rule.Heavy),
            new Race(6,3,"midnight-mile","Midnight long run","v8-muscle","Phoenix",3,Rule.Open)
        };
        static readonly string Folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/career-races-20261022"));
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        [Serializable] sealed class Hardware { public List<RivalPart> parts; }
        internal static string ReportDirectory(string fallback)
        {var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--draglab-career-report");return at<0?fallback:Path.GetFullPath(args[at+1]);}
        static string Fingerprint(UnityEngine.Object value)=>EditorJsonUtility.ToJson(value);
        static uint Seed(string id){uint seed=2166136261;foreach(char c in id)seed=unchecked((seed^c)*16777619);return seed;}
        public static bool IsSide(CareerEventDefinition e)=>e.stableId.StartsWith("side-",StringComparison.Ordinal);
        internal static bool SetRewards(CareerEventDefinition e)
        {
            var row=Races.SingleOrDefault(r=>r.Id==e.stableId);if(row==null)return false;
            e.cashWin=row.Net+e.entryFee;e.cashLoss=e.entryFee/2;e.firstWinBonus=row.tier==0?75+row.slot*15:(int)(row.Net*.35)/25*25;return true;
        }
        [MenuItem("DragLab/Prepare Additional Career Races")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            var before=Resources.LoadAll<CareerEventDefinition>("Data/Events").Where(e=>!IsSide(e)).ToDictionary(e=>e.stableId,Fingerprint);
            var catalog=MechanicsTransactions.Catalog();var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").ToDictionary(v=>v.spec.id);
            foreach(var row in Races)
            {
                string path="Assets/DragLab/Resources/Data/Events/40-"+row.Id+".asset";
                // Stable authored assets/IDs survive repeated Prepare and future save loads.
                if(AssetDatabase.LoadAssetAtPath<CareerEventDefinition>(path)!=null)continue;
                var e=ScriptableObject.CreateInstance<CareerEventDefinition>();e.stableId=row.Id;e.displayName=row.title;e.rivalName=row.driver;e.tier=row.Tier;e.opponent=cars[row.model];
                e.eventKind=row.slot==0?"Sprint":row.slot==3?"Long run":row.rule==Rule.Open?"Club race":"Restricted challenge";
                e.distanceM=row.Distance;e.prerequisite=row.Prerequisite;e.reputationRequired=row.Rep;e.entryFee=row.Fee;e.reputationWin=1;e.maximumHp=row.hp;SetRewards(e);
                e.opponentTune=e.opponent.CreateStockTune();e.opponentTune.engineLevel=row.power;e.opponentTune.tireLevel=Math.Min(3,row.tier==0?0:row.tier);e.opponentTune.gearboxLevel=row.power>0?Math.Min(3,row.power+1):0;e.opponentTune.exhaustLevel=Math.Min(2,row.power);
                switch(row.rule){
                    case Rule.Front:e.restrictDrive=true;e.requiredDrive=DriveType.FWD;break;
                    case Rule.Rear:e.restrictDrive=true;e.requiredDrive=DriveType.RWD;break;
                    case Rule.AllWheel:e.restrictDrive=true;e.requiredDrive=DriveType.AWD;break;
                    case Rule.Natural:e.naturallyAspiratedOnly=true;break;
                    case Rule.Turbo:e.turboOnly=true;break;
                    case Rule.Lightweight:e.maximumMassKg=1250;break;
                    case Rule.Heavy:e.minimumMassKg=1700;break;
                    case Rule.Small:e.maximumDisplacementL=1.6f;break;
                }
                uint seed=Seed(row.Id);var style=(RivalStyle)((row.tier+row.slot)%5);
                e.rival=new RivalSetup{authored=true,authoringVersion=5,seed=(int)(seed&0x7fffffff)|1,driverId=row.driver,style=style,
                    health=row.tier==0?.87f:.94f,mileageKm=row.tier==0?65000+seed%55000:12000+seed%65000,
                    reactionSeconds=style==RivalStyle.Novice?.32f:style==RivalStyle.Conservative?.28f:.18f,throttle=style==RivalStyle.Conservative?.97f:1,
                    shiftFraction=style==RivalStyle.Novice?.96f:1,reactionJitterSeconds=.08f,shiftJitterRpm=style==RivalStyle.Novice?160:90,
                    shiftDelayMin=.025f,shiftDelayMax=style==RivalStyle.Novice?.18f:.10f,throttleWander=.018f,
                    hesitationInterval=style==RivalStyle.Novice?6:0,hesitationSeconds=style==RivalStyle.Novice?.06f:0,
                    limiterHoldSeconds=style==RivalStyle.Aggressive?.08f:0,riskWhenBehind=style==RivalStyle.Aggressive,riskBehindMeters=25,riskLimiterHoldSeconds=.20f,
                    flatShiftWhenRisking=style==RivalStyle.Aggressive,confidenceFromBuild=style==RivalStyle.Confident,openingThrottle=.85f,openingUntilMeters=75,openingEmergencyGap=20,confidenceReactionDelay=.15f};
                if(row.power>0)foreach(var category in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.CylinderBlock,PartCategory.Clutch,PartCategory.Flywheel,PartCategory.Cooling,PartCategory.FuelSystem,PartCategory.Ignition}){
                    var part=catalog.Find(catalog.Id(row.model,category,Math.Min(3,row.power+1)));
                    if(part!=null&&catalog.Fits(e.opponent.spec,part))e.rival.parts.Add(new RivalPart{category=category,definitionId=part.id,health=.93f+(int)category%4*.01f});
                }
                AssetDatabase.CreateAsset(e,path);
            }
            AssetDatabase.SaveAssets();
            var after=Resources.LoadAll<CareerEventDefinition>("Data/Events");
            if(before.Any(p=>Fingerprint(after.Single(e=>e.stableId==p.Key))!=p.Value))throw new InvalidOperationException("Existing career content was modified.");
            File.WriteAllText(Path.Combine(Folder,"authoring-verification.txt"),"PASS all "+before.Count+" pre-existing events remain semantically unchanged\nPASS additional assets only; existing stable IDs, prerequisites, rewards and captured run terms retained\n",new UTF8Encoding(false));
        }
        public static void PrepareAndVerify(){Prepare();Run();}
        static RaceSimulation Duel(CareerEventDefinition e,MechanicalCatalog catalog,int seed)
        {
            var capture=CareerOpponents.Capture(e,catalog,1,seed);var player=capture.Create();
            player.driver.launch=LaunchMode.Automatic;player.driver.shifting=ShiftMode.Automatic;player.driver.reactionSeconds=.08;player.driver.wearMode=WearMode.None;
            var race=new RaceSimulation(player,capture.Create(),e.distanceM,capture.behavior);
            try{for(int tick=0;tick<30000&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput{throttle=1});return race;}
            finally{race.CloseOpponentWorker();}
        }
        static PlayerProfile EligibilityProfile(CareerEventDefinition e,string model,CareerEventDefinition[] all,MechanicalCatalog catalog)
        {
            var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=model;p.selectedVehicleId=model;MechanicsOwnership.Ensure(p,catalog);p.cash=100000;p.reputation=1000;p.careerWins=all.Where(x=>x!=e).Select(x=>x.stableId).ToList();return p;
        }
        [MenuItem("DragLab/Verify Additional Career Races")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);int checks=0;var log=new StringBuilder();
            void Check(bool ok,string label){checks++;log.AppendLine((ok?"PASS ":"FAIL ")+label);File.WriteAllText(Path.Combine(Folder,"verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));if(!ok)throw new InvalidOperationException(label);}
            var catalog=MechanicsTransactions.Catalog();var all=Resources.LoadAll<CareerEventDefinition>("Data/Events");var events=all.Where(IsSide).OrderBy(e=>e.reputationRequired).ThenBy(e=>e.stableId,StringComparer.Ordinal).ToArray();
            Check(all.Length==TotalCount&&all.Select(e=>e.stableId).Distinct().Count()==TotalCount,"49 unique events including all original 21");
            Check(events.Length==AddedCount&&Races.All(r=>events.Any(e=>e.stableId==r.Id)),"28 explicitly authored additional races");
            Check(events.GroupBy(e=>e.tier).Count()==7&&events.GroupBy(e=>e.tier).All(g=>g.Count()==4),"Four new races in every F–S tier");
            Check(events.Select(e=>e.opponent.spec.id).Distinct().Count()>=20,"At least twenty different rival model/trim configurations");
            var authored=all.ToDictionary(e=>e.stableId,Fingerprint);string rng=JsonUtility.ToJson(UnityEngine.Random.state);
            var matrix=new StringBuilder("event,tier,rival,model,distance_m,rep,prerequisite,fee,repeat_net,first_bonus,opponent_hp,attempt_seed,player_total,rival_total,reference_won\n");
            foreach(var e in events)
            {
                Check(e.prerequisite!=e.stableId&&all.Any(x=>x.stableId==e.prerequisite),e.stableId+" existing reachable prerequisite");
                var ancestors=new HashSet<string>();for(var parent=e;!string.IsNullOrEmpty(parent.prerequisite);parent=all.Single(x=>x.stableId==parent.prerequisite))Check(ancestors.Add(parent.stableId),e.stableId+" acyclic unlock chain");
                var twin=UnityEngine.Object.Instantiate(e);bool rewards=SetRewards(twin)&&e.cashWin==twin.cashWin&&e.firstWinBonus==twin.firstWinBonus&&e.cashLoss==twin.cashLoss;UnityEngine.Object.DestroyImmediate(twin);
                Check(rewards&&e.cashWin>e.entryFee&&e.cashLoss<=e.entryFee/2&&e.firstWinBonus<=e.cashWin-e.entryFee&&e.reputationWin==1,e.stableId+" reproducible bounded rewards; no paid-loss farming or repeat REP");
                var qualifier=all.Single(x=>x.stableId=="ladder-"+e.tier.ToLowerInvariant()+"-qualifier");
                Check(e.cashWin-e.entryFee<qualifier.cashWin-qualifier.entryFee,e.stableId+" side-event income cannot obsolete this tier's main ladder prize");
                var capture=CareerOpponents.Capture(e,catalog,1,42);var replay=CareerOpponents.Capture(e,catalog,1,42);var varied=CareerOpponents.Capture(e,catalog,1,43);
                Check(CareerOpponents.ValidateEntry(capture.Entry())==null,e.stableId+" valid captured physical hardware and human driver habits");
                Check(JsonUtility.ToJson(capture.Entry())==JsonUtility.ToJson(replay.Entry()),e.stableId+" same attempt seed reproduces setup and driver behavior");
                Check(JsonUtility.ToJson(new Hardware{parts=capture.Entry().hardware})==JsonUtility.ToJson(new Hardware{parts=varied.Entry().hardware})&&capture.driver.reactionSeconds!=varied.driver.reactionSeconds,e.stableId+" attempts vary driving while retaining the starting car");
                double hp=DragMath.PeakHorsepower(capture.spec,capture.tune);
                Check(e.maximumHp<=0||hp<=e.maximumHp,e.stableId+" rival respects its published HP ceiling ("+hp.ToString("F1",Inv)+")");
                var p=EligibilityProfile(e,e.opponent.spec.id,all,catalog);
                Check(CareerProgression.EntryError(p,p.SelectedCar,e)==null,e.stableId+" at least one real catalogue car can enter");
                string wrong=null,reason=null;
                if(e.restrictDrive){wrong=e.requiredDrive==DriveType.FWD?"swift":"starter-hatch";reason=e.requiredDrive+" only.";}
                else if(e.naturallyAspiratedOnly){wrong="sport-coupe";reason="Naturally aspirated hardware required.";}
                else if(e.turboOnly){wrong="starter-hatch";reason="Turbo hardware required.";}
                else if(e.maximumMassKg>0){wrong="utility-pickup";reason="Installed dry mass outside event limits.";}
                else if(e.minimumMassKg>0){wrong="starter-hatch";reason="Installed dry mass outside event limits.";}
                else if(e.maximumDisplacementL>0){wrong="aster";reason="Maximum "+e.maximumDisplacementL+" litre engine.";}
                if(wrong!=null){var denied=EligibilityProfile(e,wrong,all,catalog);Check(CareerProgression.EntryError(denied,denied.SelectedCar,e)==reason,e.stableId+" actual installed hardware enforces the advertised restriction");}
                p.careerWins.Remove(e.prerequisite);Check(CareerProgression.EntryError(p,p.SelectedCar,e)!=null,e.stableId+" prerequisite enforced");p.careerWins.Add(e.prerequisite);
                p.reputation=e.reputationRequired-1;Check(CareerProgression.EntryError(p,p.SelectedCar,e)!=null,e.stableId+" REP gate enforced");p.reputation=1000;
                var results=new List<RaceSimulation>();foreach(int seed in new[]{42,43}){
                    var race=Duel(e,catalog,seed);results.Add(race);
                    Check(race.ResultReady&&race.player.state.finished&&race.opponent.state.finished&&!race.opponent.state.engineFailed&&!race.opponent.state.drivetrainFailed,e.stableId+" physical race completes at attempt "+seed);
                    matrix.AppendLine(string.Join(",",e.stableId,e.tier,e.rivalName,e.opponent.spec.id,e.distanceM.ToString("F3",Inv),e.reputationRequired,e.prerequisite,e.entryFee,e.cashWin-e.entryFee,e.firstWinBonus,hp.ToString("F1",Inv),seed,(race.player.state.elapsed+race.player.state.reaction).ToString("F3",Inv),(race.opponent.state.elapsed+race.opponent.state.reaction).ToString("F3",Inv),race.playerWon));
                    File.WriteAllText(Path.Combine(Folder,"race-matrix.csv"),matrix.ToString(),new UTF8Encoding(false));
                }
                Check(results.Any(r=>r.playerWon),e.stableId+" a clean same-hardware reference can win; no forced result");
                var win=results.First(r=>r.playerWon);int cash=p.cash,rep=p.reputation;string run="new-career-"+e.stableId;
                CareerProgression.Enter(p,run,p.SelectedCar.instanceId,e,false);CareerProgression.Settle(p,run,win,false);
                Check(p.cash==cash-e.entryFee+e.cashWin+e.firstWinBonus&&p.reputation==rep+1,e.stableId+" actual first finish pays captured terms and first-only REP");
                int paid=p.cash;CareerProgression.Enter(p,run+"-repeat",p.SelectedCar.instanceId,e,false);CareerProgression.Settle(p,run+"-repeat",win,false);
                Check(p.cash==paid-e.entryFee+e.cashWin&&p.reputation==rep+1,e.stableId+" repeat finish pays only repeat net");
                bool duplicate=false;paid=p.cash;try{CareerProgression.Settle(p,run+"-repeat",win,false);}catch(InvalidOperationException){duplicate=true;}Check(duplicate&&p.cash==paid,e.stableId+" duplicate finish never repays");
                Check(authored[e.stableId]==Fingerprint(e),e.stableId+" physical tests do not mutate authored rival");
                Debug.Log("CAREER PACK PASS "+e.stableId+" / "+checks);
            }
            Check(all.All(e=>authored[e.stableId]==Fingerprint(e)),"All old and new career assets unchanged by simulation and settlement");
            Check(JsonUtility.ToJson(UnityEngine.Random.state)==rng,"Private seeded rival variation leaves gameplay global RNG intact");
            Debug.Log("CAREER PACK VERIFIED "+checks+" checks / "+events.Length+" races / "+Folder);
        }
    }
}
