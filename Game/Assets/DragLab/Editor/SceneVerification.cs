using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Presentation;
using DragLab.Runtime;

namespace DragLab.Editor
{
    // Synchronous batch check of scene wiring and application boundaries.
    // This is not a substitute for Play Mode/UI or real-device testing.
    public static class SceneVerification
    {
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] sealed class ArtManifest { public ArtLayer[] layers; }
        [Serializable] sealed class ArtLayer { public string id; }

        public static void Run()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string fixtures=Path.Combine(root,"reports/scene-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtures);
            string oldPath=ProfileStore.VerificationPath,oldAudio=SharedAudioSettings.VerificationPath,oldUnits=SharedUnitSettings.VerificationPath;
            ProfileStore.VerificationPath=Path.Combine(fixtures,"save.sqlite3");SharedAudioSettings.VerificationPath=Path.Combine(fixtures,"audio.json");SharedUnitSettings.VerificationPath=Path.Combine(fixtures,"units.json");
            try
            {
                EditorSceneManager.OpenScene("Assets/DragLab/Scenes/FirstSlice.unity");
                var game=UnityEngine.Object.FindFirstObjectByType<PrototypeGame>();Check(game!=null,"Scene contains PrototypeGame.");Invoke(game,"Start");
                Check(game.vehicles.Length>=8&&game.career.Length>=21,"Scene loads authored vehicles and expanded career events.");
                var profile=Field<PlayerProfile>(game,"profile");Check(profile.firstPurchasePending&&profile.SelectedCar==null,"Fresh scene starts at first-car selection without a gifted car.");
                VehicleCollection.Buy(profile,profile.catalog,profile.vehicleStock.listings.First(o=>o.model=="starter-hatch"));Check(ProfileStore.TrySave(profile),"Starter purchase fixture is durable.");game.ReturnToGarage();
                int cash=profile.cash,rep=profile.reputation;var evt=game.career.First(e=>CareerProgression.EntryError(profile,profile.SelectedCar,e)==null);
                game.StartRace(evt);var race=Field<RaceSimulation>(game,"race");Check(race!=null,"Eligible first event stages after purchasing a starter.");
                int ticks=0;while(race.phase!=RacePhase.Finished&&ticks<15000){race.Step(new DriverInput());ticks++;}
                Check(ticks<15000&&race.player.state.finished&&!race.player.state.disqualified,"Default driving completes the pass; victory is not guaranteed.");
                SetField(game,"liveRunTick",(long)ticks);Invoke(game,"FinishRace");profile=Field<PlayerProfile>(game,"profile");
                int expectedCash=cash-evt.entryFee+(race.playerWon?evt.cashWin+evt.firstWinBonus:evt.cashLoss),expectedRep=rep+(race.playerWon?evt.reputationWin:0);
                Check(profile.cash==expectedCash&&profile.reputation==expectedRep&&profile.careerRuns.Last().complete,"Captured outcome settles its actual fee, reward and first-win terms.");
                Invoke(game,"FinishRace");Check(profile.cash==expectedCash&&profile.SelectedCar.completedRuns==1,"Repeated settlement cannot duplicate rewards or pass records.");
                var loaded=ProfileStore.Load();Check(loaded.cash==profile.cash&&loaded.careerWins.Contains(evt.stableId)==race.playerWon,"Actual race outcome survives database reload.");
                game.ReturnToGarage();profile=Field<PlayerProfile>(game,"profile");ProfileStore.Flush();
                Check(File.Exists(ProfileStore.FilePath+".bak"),"Durable recovery database exists.");
                File.WriteAllText(ProfileStore.FilePath,"corrupt isolated primary",Encoding.UTF8);var recovered=ProfileStore.Load();
                Check(recovered.cash==profile.cash&&recovered.SelectedCar.instanceId==profile.SelectedCar.instanceId&&ProfileStore.Status.Contains("Recovered"),"Damaged primary recovers the latest durable assets and balance.");
                Check(ProfileStore.TrySave(recovered),"Recovered database commits normally.");ProfileStore.Flush();File.WriteAllText(ProfileStore.FilePath,"corrupt isolated primary again",Encoding.UTF8);
                var reloaded=ProfileStore.Load();Check(reloaded.cash==profile.cash&&!ProfileStore.ResetRequired,"Saving after recovery preserves a valid recovery database.");
                SetField(game,"profile",reloaded);game.settings=reloaded.driving;profile=reloaded;
                var view=Field<LayeredCarView>(game,"playerView");var art=game.vehicles.First(v=>v.spec.id==profile.SelectedCar.vehicleId).spec.artId;
                var manifest=JsonUtility.FromJson<ArtManifest>(Resources.Load<TextAsset>("Sprites/Vehicles/"+art+"/car-sprite").text);
                var sprites=view.transform.Cast<Transform>().Where(t=>t.GetComponent<SpriteRenderer>()!=null).ToArray();
                Check(sprites.Length==manifest.layers.Length&&manifest.layers.All(layer=>sprites.Any(t=>t.name==layer.id&&t.GetComponent<SpriteRenderer>().sprite!=null)),"Runtime car assembles every authored sprite layer alongside its tire effects.");
                int practiceCash=profile.cash,practiceRep=profile.reputation,practiceRuns=profile.SelectedCar.completedRuns;
                game.StartPractice();race=Field<RaceSimulation>(game,"race");Check(race!=null,"Practice stages with restored ownership.");ticks=0;
                while(race.phase!=RacePhase.Finished&&ticks<15000){race.Step(new DriverInput());ticks++;}
                SetField(game,"liveRunTick",(long)ticks);Invoke(game,"FinishRace");Invoke(game,"FinishRace");profile=Field<PlayerProfile>(game,"profile");
                Check(ticks<15000&&profile.cash==practiceCash&&profile.reputation==practiceRep&&profile.SelectedCar.completedRuns==practiceRuns+1,"Practice records one valid pass without payout or duplicated settlement.");
                var savedPass=ProfileStore.Load().SelectedCar.lastPass;
                Check(savedPass!=null&&Math.Abs(savedPass.elapsedSeconds-race.player.state.elapsed)<1e-6&&savedPass.tune!=profile.SelectedCar.tune&&savedPass.driving.launch==race.player.driver.launch,"Last-pass timing and independent tune/assist snapshots persist.");
                game.ReturnToGarage();Check(Field<string>(game,"screen")=="Garage","Completed practice returns to Garage.");
                File.WriteAllText(Path.Combine(root,"reports/unity-scene-verification.txt"),"PASS: isolated fresh SQLite scene, explicit starter purchase, outcome-based career settlement, duplicate settlement, durable backup recovery, authored sprite assembly, no-payout practice and last-pass snapshot.\nEditor: "+Application.unityVersion+"\nFixture: "+fixtures+"\nNo player save/preferences accessed. Native input/render/audio checks are separate.\n",new UTF8Encoding(false));
                Debug.Log("DRAGLAB SCENE VERIFICATION PASS");
            }
            finally {ProfileStore.Flush();ProfileStore.VerificationPath=oldPath;SharedAudioSettings.VerificationPath=oldAudio;SharedUnitSettings.VerificationPath=oldUnits;ProfileStore.UseSlot("slot-1");}
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("SCENE CHECK FAILED: " + message); }
        static T Field<T>(PrototypeGame game, string field) { return (T)typeof(PrototypeGame).GetField(field, PrivateInstance).GetValue(game); }
        static void SetField(PrototypeGame game, string field, object value) { typeof(PrototypeGame).GetField(field, PrivateInstance).SetValue(game, value); }
        static void Invoke(PrototypeGame game, string method) { typeof(PrototypeGame).GetMethod(method, PrivateInstance).Invoke(game, null); }
    }
}
