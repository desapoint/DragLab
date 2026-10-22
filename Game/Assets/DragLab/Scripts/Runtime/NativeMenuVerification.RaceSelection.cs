using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator RaceSelectionChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);yield return Click(input,"navRace");
            Check(game.ScreenName=="Race setup"&&!shell.Root.Q("raceSelectionPage").ClassListContains("hidden"),"Race opens native full-screen selection with persistent navigation");
            Check(game.career.All(e=>shell.Root.Q("raceEvent:"+e.stableId)!=null),"Every authored event is represented without adding or replacing career content");
            var added=game.career.Where(e=>e.stableId.StartsWith("side-",StringComparison.Ordinal)).ToArray();
            Check(game.career.Length==49&&added.Length==28&&added.GroupBy(e=>e.tier).All(g=>g.Count()==4),"All 28 added career races load as four cards per tier alongside the original 21");
            Check(game.career.All(e=>shell.Root.Q<Label>("raceEligibility:"+e.stableId).text==(CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,e)??"Eligible")),"Each event shows its authoritative eligibility reason");
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            var search=shell.Root.Q<TextField>("raceSearch");search.value="no-matching-event-probe";Check(shell.Root.Q("raceEventGrid").Query<Button>().ToList().All(c=>c.ClassListContains("hidden")),"Event search filters the existing roster without recalculating builds");search.value="";
            search.value="Dockside warm-up";Check(shell.Root.Q("raceEventGrid").Query<Button>().ToList().Count(c=>!c.ClassListContains("hidden"))==1&&!shell.Root.Q("raceEvent:side-f-dockside").ClassListContains("hidden"),"New early career content is searchable through the existing native controls");search.value="";
            var blocked=game.career.FirstOrDefault(e=>CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,e)!=null);if(blocked!=null){shell.Root.Q<ScrollView>("raceSelectionScroll").ScrollTo(shell.Root.Q("raceEvent:"+blocked.stableId));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"raceEvent:"+blocked.stableId);Check(!shell.Root.Q("raceStage").enabledInHierarchy&&shell.Root.Q<Label>("raceEntryStatus").text==CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,blocked),"Blocked selected events disable Stage and expose the actual reason");}
            var eligible=game.career.First(e=>CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,e)==null);shell.Root.Q<ScrollView>("raceSelectionScroll").ScrollTo(shell.Root.Q("raceEvent:"+eligible.stableId));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"raceEvent:"+eligible.stableId);
            input.MouseButtonAt(20,120,false);yield return new WaitForSecondsRealtime(.8f);
            var selectedCard=shell.Root.Q("raceEvent:"+eligible.stableId);var selectedImage=ScreenCapture.CaptureScreenshotAsTexture();var selectedBounds=selectedCard.worldBound;
            var selectedEdge=Pixel(selectedImage,selectedBounds.xMin+2,selectedBounds.center.y);Destroy(selectedImage);
            Check(selectedEdge.r-selectedEdge.g>.2f&&selectedCard.ClassListContains("selected"),"Selected race has a persistent red edge after pointer leaves and hover shine ends");
            Check(shell.Root.Q("raceEventGrid").Query<Button>().ToList().Count(b=>b.ClassListContains("selected"))==1,"Exactly one career choice owns the selection highlight");
            Check(shell.Root.Q("raceStage").enabledInHierarchy,"An eligible selected event enables staging");yield return Click(input,"raceStage");Check(shell.ModalOpen&&shell.Root.Q<ScrollView>("modalContent").Query<Label>().ToList().Any(l=>l.text.Contains(DisplayUnits.Money(eligible.entryFee))),"Entry review displays actual captured event fees and rewards");yield return Click(input,"dialogCancel");
            yield return Click(input,"raceCarSelect");Check(shell.Root.Q("raceChooseCar:"+game.Profile.SelectedCar.instanceId)!=null,"Race car picker uses actual owned instance identities");yield return Click(input,"modalClose");yield return Capture("race-career");
            yield return Click(input,"raceMode:Free race");yield return Click(input,"raceDistance:½ mile");yield return new WaitForSecondsRealtime(.15f);Check(Math.Abs(game.NativeRaceDistance-804.672)<.01,"Free distance choices configure the existing race distance");
            var opponent=game.vehicles[0];shell.Root.Q<ScrollView>("raceSelectionScroll").ScrollTo(shell.Root.Q("raceOpponent:"+opponent.spec.id));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"raceOpponent:"+opponent.spec.id);Check(game.vehicles[game.NativeRaceOpponent].spec.id==opponent.spec.id,"Free opponent choices retain the actual vehicle definition");yield return Capture("race-free");
            yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.Root.Q("raceMode:Free race").ClassListContains("selected")&&Math.Abs(game.NativeRaceDistance-804.672)<.01,"F8 retains native mode, opponent and distance");
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");Check(game.ScreenName=="Race setup"&&shell.Root.Q("raceMode:Free race").ClassListContains("selected"),"Settings Cancel returns to the unchanged Race selection");
            foreach(int width in new[]{1024,1920}){Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.3f);Check(shell.Root.Q("raceStage").worldBound.yMax<shell.Root.Q("footerBar").worldBound.yMin&&shell.Root.Q("raceSelectionScroll").worldBound.yMax<=shell.Root.Q("raceStage").worldBound.yMin,"Race list and fixed Stage remain bounded at "+width);yield return Capture("race-"+width);}
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2&&shell.Root.Q("raceStage").worldBound.yMax<=shell.Root.worldBound.yMax,"Race selection uses the shared crisp 4K scale");var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"race-4k.png"),image.EncodeToPNG());RenderTexture.active=active;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Click(input,"raceMode:Records");Check(shell.Root.Q("raceStage").ClassListContains("hidden"),"Records show actual car history without a race entry action");yield return Capture("race-records");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Browsing, searching, configuration, F8, Settings and records never charge, heal or save");
            yield return Click(input,"raceMode:Practice");var captured=game.CaptureNativeEntry("Practice",null);Check(captured.Distance==402.336f,"Practice remains quarter mile and no payout");
            var stale=game.CaptureNativeEntry("Practice",null);stale.Revision--;Check(!game.StartNativeEntry(stale)&&before==JsonUtility.ToJson(game.Profile),"Stale entry is rejected without money or resource publication");
            yield return Click(input,"raceStage");using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"dialogStage");Check(shell.ModalOpen&&game.ScreenName=="Race setup"&&game.Profile.cash==JsonUtility.FromJson<PlayerProfile>(before).cash,"Locked durable entry cannot start a race or charge money");}
            yield return Click(input,"dialogCancel");yield return Click(input,"raceStage");yield return Click(input,"dialogStage");Check(game.ScreenName=="Race"&&game.IsPractice&&game.Race!=null&&game.Profile.careerRuns.Last().practice,"Actual practice staging uses the authoritative race and captured no-payout entry");Check(!game.StartNativeEntry(captured),"Repeated Stage cannot create another entry while racing");game.ReturnToGarage();game.QueueNativeNavigation("Race setup");yield return new WaitForSecondsRealtime(.3f);
            yield return Click(input,"raceMode:Career");shell.Root.Q<ScrollView>("raceSelectionScroll").ScrollTo(shell.Root.Q("raceEvent:"+eligible.stableId));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"raceEvent:"+eligible.stableId);int cash=game.Profile.cash;yield return Click(input,"raceStage");yield return Click(input,"dialogStage");Check(game.ScreenName=="Race"&&game.Profile.cash==cash-eligible.entryFee&&game.Profile.careerRuns.Last().eventId==eligible.stableId,"Confirmed Career entry captures its authored terms and charges its fee once");game.ReturnToGarage();Check(ProfileStore.Load().careerRuns.Any(r=>r.eventId==eligible.stableId),"Captured career entry survives SQLite reload");Check(Math.Abs(game.NativeRaceDistance-804.672)<.01,"Practice and Career entries do not overwrite the configured Free race distance");game.QueueNativeNavigation("Race setup");yield return new WaitForSecondsRealtime(.3f);yield return Click(input,"raceMode:Free race");yield return Click(input,"raceStage");yield return Click(input,"dialogStage");Check(game.ScreenName=="Race"&&!game.IsPractice&&Math.Abs(game.Race.distance-804.672)<.01&&game.Race.opponent.spec.id==opponent.spec.id,"Actual Free race receives the configured distance and opponent without becoming Practice");game.ReturnToGarage();
        }
#endif
    }
}
