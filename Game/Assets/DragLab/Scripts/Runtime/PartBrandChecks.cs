using System;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public static class PartBrandChecks
    {
        public static void Run(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec),false);
            foreach(string focus in PartBrands.FocusChoices)check(catalog.parts.Any(p=>PartBrands.Focus(p.brand)==focus),"Authored supply includes "+focus);
            check(PartBrands.Profiles.Select(p=>p.name).Distinct().Count()==PartBrands.Profiles.Count,"Brand names are unique and have a single declared focus");
            var physical=catalog.parts.Select(Physical).ToArray();var presentation=catalog.parts.Select(JsonUtility.ToJson).ToArray();PartBrands.Apply(catalog);
            check(catalog.parts.Select(Physical).SequenceEqual(physical),"Brand assignment preserves every mechanical field, price, fitment, ID and tuning permission");
            check(catalog.parts.Select(JsonUtility.ToJson).SequenceEqual(presentation),"Reapplying brand presentation is idempotent without duplicate supplier/name text");
            foreach(var p in catalog.parts){
                check(PartBrands.Profiles.Any(b=>b.name==p.brand)&&p.name.StartsWith(p.brand+" ",StringComparison.Ordinal),"Known focused supplier and consistent name: "+p.id);
                if(p.id.EndsWith("/0",StringComparison.Ordinal))check(p.brand=="Atlas OE","Factory specification remains OEM: "+p.id);
            }
            check(catalog.parts.Any(p=>p.id.EndsWith("/0",StringComparison.Ordinal)&&p.quality=="Forged"&&p.brand=="Atlas OE"),"Factory-forged internals are OEM, not automatically competition-branded");
            check(catalog.parts.Where(p=>p.id.EndsWith("/ServiceLife",StringComparison.Ordinal)).All(p=>p.brand=="Relay Components"),"Long-service fixed replacements use the OEM+ supplier");
            check(PartBrands.Focus("Workshop Custom")=="Custom","Unknown authored brands remain filterable as Custom");
            var entries=catalog.parts.Select(p=>new NativePartEntry{Definition=p,Compatibility="Compatible",Source="New",StockUnits=1,Price=p.price}).ToArray();var query=new NativePartQuery();
            foreach(var brand in PartBrands.Profiles){
                query.Brands.Clear();query.Brands.Add(brand.name);query.BrandFocus="All";
                check(entries.Where(e=>query.Matches(e,int.MaxValue)).Select(e=>e.Definition.id).SequenceEqual(catalog.parts.Where(p=>p.brand==brand.name).Select(p=>p.id)),"Exact brand filter: "+brand.name);
                query.BrandFocus=brand.focus;check(entries.Where(e=>query.Matches(e,int.MaxValue)).All(e=>e.Definition.brand==brand.name),"Brand and matching focus combine: "+brand.name);
                query.BrandFocus=brand.focus=="OEM"?"Competition":"OEM";check(!entries.Any(e=>query.Matches(e,int.MaxValue)),"Different brand/focus do not leak: "+brand.name);
            }
            query=new NativePartQuery{BrandFocus="Tuning"};query.Brands.Add("IonWorks");query.Brands.Add("Vortex Dynamics");var clone=query.Copy();clone.Brands.Add("Atlas OE");check(!query.Brands.Contains("Atlas OE")&&clone.Brands.Count==3,"Modal brand selection copies independently for Cancel/Apply");
            check(entries.Where(e=>query.Matches(e,int.MaxValue)).All(e=>e.Definition.brand=="IonWorks"||e.Definition.brand=="Vortex Dynamics"),"Multiple brands form a union within the selected focus");
            int matches=entries.Count(e=>query.Matches(e,int.MaxValue));query.Search="OEM";check(!entries.Any(e=>query.Matches(e,int.MaxValue)),"Search combines with brand/focus instead of replacing them");query.Search="";query.Tuning="Adjustable";check(entries.Where(e=>query.Matches(e,int.MaxValue)).All(e=>e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable),"Brand focus does not grant tuning permissions");
            var mixed=new NativePartEntry{Definition=catalog.parts.First(p=>p.category==PartCategory.Tires),FittedBrands=new[]{"Atlas OE","Trackline Racing"},Compatibility="Compatible",Source="Installed"};
            query=new NativePartQuery{BrandFocus="Competition"};query.Brands.Add("Trackline Racing");check(query.Matches(mixed,int.MaxValue),"Mixed tire/rim entries match their actual corner supplier");query.Brands.Clear();query.Brands.Add("Atlas OE");check(!query.Matches(mixed,int.MaxValue),"Mixed entries require brand and focus to match the same constituent");
            var car=vehicles[0];var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);profile.SelectedCar.vehicleId=profile.selectedVehicleId=car.spec.id;MechanicsOwnership.Ensure(profile,catalog);string before=JsonUtility.ToJson(profile);var market=profile.inventory.market;int[] quantities=market.parts.Select(row=>row.units).ToArray();PartBrands.Apply(catalog);MechanicsOwnership.Ensure(profile,catalog);
            check(before==JsonUtility.ToJson(profile)&&quantities.SequenceEqual(market.parts.Select(row=>row.units)),"Rebranding and normalization retain exact assets, condition, resources, seeds, stock and reservations");
            var custom=catalog.parts[0].Copy();custom.brand="Workshop Custom";custom.name=custom.brand+" reference part";PartCatalogOverrides.Apply(catalog,new PartCatalogOverrideData{parts=new System.Collections.Generic.List<PartDefinition>{custom}});check(catalog.Find(custom.id).brand==custom.brand,"Explicit catalog author overrides remain authoritative");
        }
        static string Physical(PartDefinition part){var p=part.Copy();p.brand=p.name=p.description="";return JsonUtility.ToJson(p);}
    }
}
