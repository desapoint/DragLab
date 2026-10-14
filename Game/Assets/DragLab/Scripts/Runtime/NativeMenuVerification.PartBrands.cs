using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator PartBrandInterfaceChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);yield return Click(input,"navBuild");yield return Click(input,"partsSource:Shop");yield return Click(input,"partsTab0");yield return WaitParts();
            string state=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            NativePartEntry[] Visible()=>shell.PartBrowser.Entries.Where(e=>shell.Root.Q("partCard:"+e.Id)!=null&&!shell.Root.Q("partCard:"+e.Id).ClassListContains("hidden")).ToArray();
            foreach(string focus in PartBrands.FocusChoices){
                yield return Click(input,"partsFilters");Check(shell.Root.Q<Button>("partAvailability:Express")==null,"Brand filter keeps Express absent from availability");yield return PartDialogClick(input,"partBrandFocus:"+focus);yield return Click(input,"dialogApply");yield return WaitParts();
                var entries=Visible();Check(entries.Length>0&&entries.All(e=>e.BrandNames.Any(b=>PartBrands.Focus(b)==focus)),"Applied focus filters actual shop suppliers: "+focus);Check(shell.PartBrowser.EvaluationCount==0,"Brand/focus filtering does not run per-part dynos: "+focus);
                if(focus=="OEM+")yield return Capture("shop-oem-plus");
            }
            yield return Click(input,"partsFilters");yield return PartDialogClick(input,"partBrandFocus:All");yield return PartDialogClick(input,"partBrand:Atlas OE");yield return Capture("part-brand-filter");yield return Click(input,"dialogCancel");Check(shell.PartQuery.BrandFocus=="Competition"&&shell.PartQuery.Brands.Count==0,"Cancel discards supplier and focus drafts");
            yield return Click(input,"partsFilters");yield return PartDialogClick(input,"partBrandFocus:All");yield return PartDialogClick(input,"partBrand:Atlas OE");yield return Click(input,"dialogApply");yield return WaitParts();Check(Visible().Length>0&&Visible().All(e=>e.BrandNames.Contains("Atlas OE")),"Individual supplier filters the current section");
            var part=Visible().First();var card=shell.Root.Q("partCard:"+part.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.15f);Check(card.Query<Label>().ToList().Any(l=>l.text.Contains("OEM")),"Cards expose supplier focus alongside specification and condition");yield return Click(input,card.name);Check(shell.Root.Q("partDetailFacts").Query<Label>().ToList().Any(l=>l.text=="Atlas OE · OEM"),"Detail page separates supplier focus from material/specification");yield return Capture("part-brand-detail");yield return Click(input,"partBack");
            yield return Click(input,"partsFilters");yield return PartDialogClick(input,"partBrand:Relay Components");yield return Click(input,"dialogApply");yield return WaitParts();Check(Visible().SelectMany(e=>e.BrandNames).Distinct().OrderBy(s=>s).SequenceEqual(new[]{"Atlas OE","Relay Components"}),"Multiple selected brands form an OR selection");
            yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.PartQuery.Brands.Count==2&&shell.PartQuery.BrandFocus=="All","F8 retains brand filter choices");
            yield return Click(input,"partsClear");yield return WaitParts();Check(shell.PartQuery.Brands.Count==0&&shell.PartQuery.BrandFocus=="All","Clear restores all suppliers and focuses");
            Check(state==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Brand browsing and draft actions do not save, spend, heal or change player state");
        }
#endif
    }
}
