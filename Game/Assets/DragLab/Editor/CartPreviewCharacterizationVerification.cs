using System;
using System.IO;
using System.Text;
using DragLab.Runtime;
using UnityEditor;
using UnityEngine;

namespace DragLab.Editor
{
    // Editor-only frozen-reference characterization; never selects a game save.
    public static class CartPreviewCharacterizationVerification
    {
        public static void Run()=>RunCore(false,false);
        public static void RunMutationBoundary()=>RunCore(true,false);
        public static void RunCollectAll()=>RunCore(false,true);
        public static void RunCoreRows()=>RunCore(false,true,"core-rows");
        public static void RunTopology()=>RunCore(false,true,"topology");
        public static void RunCorePurchases()=>RunCore(false,true,"core-rows","purchases");
        public static void RunCoreSalesStock()=>RunCore(false,true,"core-rows","sales-stock");
        public static void RunCoreStock()=>RunCore(false,true,"core-rows","stock");
        public static void RunCoreNewSets()=>RunCore(false,true,"core-rows","new-sets");
        public static void RunIdentity()=>RunCore(false,true,"identity");
        public static void RunLimits()=>RunCore(false,true,"supplemental","limits");
        public static void RunMalformed()=>RunCore(false,true,"supplemental","malformed");
        public static void RunNormalization()=>RunCore(false,true,"supplemental","normalization");
        public static void RunSourceDiffs()=>RunCore(false,true,"supplemental","source-diffs");
        public static void RunEnsure()=>RunCore(false,true,"supplemental","ensure");
        public static void RunCommerce()=>RunCore(false,true,"supplemental","commerce");
        public static void RunCommerceMixed()=>RunCore(false,true,"supplemental","commerce-mixed");
        public static void RunCommerceOwner()=>RunCore(false,true,"supplemental","commerce-owner");
        public static void RunCommerceLifetime()=>RunCore(false,true,"supplemental","commerce-lifetime");
        public static void RunGraphClosure()=>RunCore(false,true,"supplemental","graph");
        public static void RunGraphOwners()=>RunCore(false,true,"supplemental","graph-owners");
        public static void RunGraphBlocks()=>RunCore(false,true,"supplemental","graph-blocks");
        public static void RunGraphRoundTrip()=>RunCore(false,true,"supplemental","graph-roundtrip");
        public static void RunValidation()=>RunCore(false,true,"supplemental","validation");
        public static void RunValidationSales()=>RunCore(false,true,"supplemental","validation-sales");
        public static void RunValidationIntegrity()=>RunCore(false,true,"supplemental","validation-integrity");
        public static void RunValidationStock()=>RunCore(false,true,"supplemental","validation-stock");
        public static void RunValidationPositions()=>RunCore(false,true,"supplemental","validation-positions");
        public static void RunOrder()=>RunCore(false,true,"supplemental","order");
        public static void RunOrderSales()=>RunCore(false,true,"supplemental","order-sales");
        public static void RunOrderChain()=>RunCore(false,true,"supplemental","order-chain");
        public static void RunCostAttribution()=>RunCostCore(null);
        public static void RunCostTotals()=>RunCostCore("totals");
        public static void RunCostStages()=>RunCostCore("stages");
        public static void RunCostOperations()=>RunCostCore("operations");
        public static void RunCostBlock()=>RunCostCore("block");
        public static void RunInstalledBuild()=>RunCore(false,true,"installed-build");
        public static void RunInstalledBuildRegression()=>RunCore(false,true,"installed-build-regression");
        public static void RunTopologyMembersCorners()=>RunCore(false,true,"topology","members-corners");
        public static void RunTopologyOwnersParts()=>RunCore(false,true,"topology","owners-parts");
        public static void RunTopologyBlocksLocks()=>RunCore(false,true,"topology","blocks-locks");
        static void RunCostCore(string subset)
        {
            if(!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../reports/save-refactor/S06D2B1G/cost-"+(subset??"all")+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
            Directory.CreateDirectory(root);
            var report=new StringBuilder("S06d2b1g descriptive legacy cost attribution\n");
            int passed=0,failed=0;
            try
            {
                CartPreviewCharacterizationChecks.RunCostAttribution((ok,label)=>
                {
                    report.AppendLine((ok?"PASS ":"FAIL ")+label);
                    if(ok)passed++;else failed++;
                },root,subset);
                if(failed!=0)throw new InvalidOperationException("Cost harness retains "+failed+" failed assertions.");
                report.AppendLine("RESULT PASS count="+passed);
                Debug.Log("S06 cost attribution PASS "+passed+" folder="+root);
            }
            catch(Exception error)
            {
                report.AppendLine("RESULT FAIL passed="+passed+" failedAssertions="+failed);
                report.AppendLine(error.ToString());
                Debug.LogError("S06 cost attribution FAIL folder="+root);
                throw;
            }
            finally{File.WriteAllText(Path.Combine(root,"checks.txt"),report.ToString(),new UTF8Encoding(false));}
        }
        static void RunCore(bool mutationBoundary,bool collectAll,string expansion=null,string subset=null)
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../reports/save-refactor/S06D2B1/" + (expansion!=null?expansion+"-"+(subset==null?"":subset+"-"):mutationBoundary?"boundary-":collectAll?"collect-":"run-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
            Directory.CreateDirectory(root);
            var report = new StringBuilder("S06d2b step1 Editor-only legacy cart characterization\n");
            int count = 0, failures = 0;
            try
            {
                Action<bool,string> check=(ok, label) =>
                {
                    report.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok)
                    {
                        failures++;
                        if(!collectAll)throw new InvalidOperationException(label);
                    }
                    else count++;
                };
                if(expansion=="installed-build-regression")NativePartsBrowserBuildChecks.Run(check,root);
                else if(expansion=="installed-build")CartPreviewCharacterizationChecks.RunInstalledBuildProjectionContracts(check,root,subset);
                else if(expansion=="supplemental")CartPreviewCharacterizationChecks.RunSupplemental(check,root,subset);
                else if(expansion!=null)CartPreviewCharacterizationChecks.RunExpansion(check,root,expansion=="topology",subset,expansion=="identity");
                else if(mutationBoundary)CartPreviewCharacterizationChecks.RunMutationBoundary(check,root);
                else if(collectAll)CartPreviewCharacterizationChecks.RunCollectAll(check,root);
                else CartPreviewCharacterizationChecks.Run(check,root);
                if(failures>0)throw new InvalidOperationException("Characterization retains "+failures+" failed assertions; inspect the complete report.");
                report.AppendLine((mutationBoundary?"DIAGNOSTIC COMPLETE":"RESULT PASS")+" count="+count);
                Debug.Log("S06 cart characterization "+(mutationBoundary?"diagnostic complete ":"managed PASS ")+count + " folder=" + root);
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT FAIL passed="+count+" failedAssertions="+failures);
                report.AppendLine(error.ToString());
                Debug.LogError("S06 cart characterization managed FAIL folder=" + root);
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "checks.txt"), report.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
